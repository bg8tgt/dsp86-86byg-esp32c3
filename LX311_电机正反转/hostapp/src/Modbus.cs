using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace Lx311Host
{
    public interface IPlc
    {
        bool Connect(string ip, int port, byte unit);
        void Close();
        bool Connected { get; }
        bool[] ReadCoils(ushort start, ushort count);
        ushort[] ReadHolding(ushort start, ushort count);
        void WriteCoil(ushort addr, bool value);
        string Name { get; }
    }

    /// <summary>Modbus TCP 主站（功能码 01 / 03 / 05）</summary>
    public class ModbusTcp : IPlc
    {
        TcpClient _tcp;
        NetworkStream _ns;
        readonly object _sync = new object();
        ushort _tid = 1;
        byte _unit = 1;
        int _timeout = 900;

        public string Name { get { return "Modbus TCP"; } }
        public bool Connected
        {
            get { return _tcp != null && _tcp.Connected && _ns != null; }
        }

        public bool Connect(string ip, int port, byte unit)
        {
            Close();
            try
            {
                TcpClient c = new TcpClient();
                IAsyncResult ar = c.BeginConnect(ip, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(1500, false))
                {
                    c.Close();
                    return false;
                }
                c.EndConnect(ar);
                c.NoDelay = true;
                NetworkStream s = c.GetStream();
                s.ReadTimeout = _timeout;
                s.WriteTimeout = _timeout;
                _tcp = c;
                _ns = s;
                _unit = unit;
                return true;
            }
            catch
            {
                Close();
                return false;
            }
        }

        public void Close()
        {
            try { if (_ns != null) _ns.Close(); } catch { }
            try { if (_tcp != null) _tcp.Close(); } catch { }
            _ns = null;
            _tcp = null;
        }

        byte[] Transact(byte[] pdu)
        {
            lock (_sync)
            {
                if (!Connected) return null;
                try
                {
                    ushort tid = _tid++;
                    byte[] frame = new byte[7 + pdu.Length];
                    frame[0] = (byte)(tid >> 8); frame[1] = (byte)(tid & 0xFF);
                    frame[2] = 0; frame[3] = 0;
                    frame[4] = (byte)((pdu.Length + 1) >> 8);
                    frame[5] = (byte)((pdu.Length + 1) & 0xFF);
                    frame[6] = _unit;
                    Buffer.BlockCopy(pdu, 0, frame, 7, pdu.Length);
                    _ns.Write(frame, 0, frame.Length);
                    _ns.Flush();

                    byte[] head = ReadExact(6);
                    if (head == null) return null;
                    ushort rtid = (ushort)((head[0] << 8) | head[1]);
                    int len = (head[4] << 8) | head[5];
                    if (len < 3) return null;
                    byte[] body = ReadExact(len);      // 单元号 + PDU
                    if (body == null || body.Length < 2) return null;
                    if (rtid != tid) return null;
                    if (body[0] != _unit) return null;
                    byte[] resp = new byte[body.Length - 1];
                    Buffer.BlockCopy(body, 1, resp, 0, resp.Length);
                    if (resp.Length == 0 || (resp[0] & 0x80) != 0) return null;
                    return resp;
                }
                catch
                {
                    Close();
                    return null;
                }
            }
        }

        byte[] ReadExact(int n)
        {
            byte[] buf = new byte[n];
            int got = 0;
            while (got < n)
            {
                int r = _ns.Read(buf, got, n - got);
                if (r <= 0) return null;
                got += r;
            }
            return buf;
        }

        public bool[] ReadCoils(ushort start, ushort count)
        {
            byte[] pdu = new byte[] { 0x01, (byte)(start >> 8), (byte)(start & 0xFF),
                                      (byte)(count >> 8), (byte)(count & 0xFF) };
            byte[] r = Transact(pdu);
            if (r == null || r.Length < 3) return null;
            int bytes = r[1];
            if (r.Length < 2 + bytes) return null;
            bool[] v = new bool[count];
            for (int i = 0; i < count; i++)
                v[i] = ((r[2 + i / 8] >> (i % 8)) & 1) == 1;
            return v;
        }

        public ushort[] ReadHolding(ushort start, ushort count)
        {
            byte[] pdu = new byte[] { 0x03, (byte)(start >> 8), (byte)(start & 0xFF),
                                      (byte)(count >> 8), (byte)(count & 0xFF) };
            byte[] r = Transact(pdu);
            if (r == null || r.Length < 3) return null;
            int bytes = r[1];
            if (r.Length < 2 + bytes) return null;
            ushort[] v = new ushort[count];
            for (int i = 0; i < count; i++)
                v[i] = (ushort)((r[2 + i * 2] << 8) | r[3 + i * 2]);
            return v;
        }

        public void WriteCoil(ushort addr, bool value)
        {
            byte[] pdu = new byte[] { 0x05, (byte)(addr >> 8), (byte)(addr & 0xFF),
                                      (byte)(value ? 0xFF : 0x00), 0x00 };
            Transact(pdu);
        }
    }

    /// <summary>离线模拟器：内部跑与 PLC 相同的状态机，便于无硬件演示</summary>
    public class SimPlc : IPlc
    {
        readonly object _sync = new object();
        DateTime _t0 = DateTime.Now;
        DateTime _last = DateTime.Now;
        DateTime _deadSince = DateTime.Now.AddSeconds(-1);
        int _step;
        bool _fault;
        bool _fwdReq, _revReq, _stopCmd;
        double _runSec;
        int _starts;

        public bool FaultInjection;   // 模拟热继动作
        public string Name { get { return "离线模拟"; } }
        public bool Connected { get { return true; } }

        public bool Connect(string ip, int port, byte unit) { return true; }
        public void Close() { }

        public void Tick()
        {
            lock (_sync)
            {
                DateTime now = DateTime.Now;
                double dt = (now - _last).TotalSeconds;
                if (dt > 0.5) dt = 0.5;
                _last = now;

                if (FaultInjection) _fault = true;
                if (_stopCmd) { _fwdReq = false; _revReq = false; }
                if (_fault) { _fwdReq = false; _revReq = false; }

                int prev = _step;
                bool deadOk = (now - _deadSince).TotalSeconds >= 0.3;
                if (_step == 0)
                {
                    if (deadOk && !_fault && !_stopCmd)
                    {
                        if (_fwdReq) _step = 1;
                        else if (_revReq) _step = 2;
                    }
                }
                else
                {
                    bool change = (_step == 1) ? _revReq : _fwdReq;
                    if (_fault || _stopCmd || change)
                    {
                        _step = 0;
                        _deadSince = now;
                    }
                }

                if (_step != 0 && prev == 0) { _starts++; _runSec = 0; }
                if (_step != 0) _runSec += dt;
            }
        }

        public bool[] ReadCoils(ushort start, ushort count)
        {
            lock (_sync)
            {
                bool[] all = new bool[64];
                all[0x10] = _step == 1;
                all[0x11] = _step == 2;
                all[0x12] = _fault;
                all[0x13] = false;
                all[0x14] = FaultInjection;
                all[0x15] = _step != 0;
                bool[] v = new bool[count];
                for (int i = 0; i < count; i++)
                    if (start + i < 64) v[i] = all[start + i];
                return v;
            }
        }

        public ushort[] ReadHolding(ushort start, ushort count)
        {
            lock (_sync)
            {
                ushort[] all = new ushort[16];
                all[0] = (ushort)Math.Min(65535, (int)_runSec);
                all[1] = (ushort)Math.Min(65535, _starts);
                all[2] = (ushort)(FaultInjection ? 1 : (_fault ? 2 : 0));
                all[4] = (ushort)Math.Min(65535, (int)(_runSec / 60));
                ushort[] v = new ushort[count];
                for (int i = 0; i < count; i++)
                    if (start + i < 16) v[i] = all[start + i];
                return v;
            }
        }

        public void WriteCoil(ushort addr, bool value)
        {
            lock (_sync)
            {
                if (addr == 0x00 && value) { _fwdReq = true; _revReq = false; }
                else if (addr == 0x01 && value) { _revReq = true; _fwdReq = false; }
                else if (addr == 0x02) { _stopCmd = value; }
                else if (addr == 0x03 && value) { _fault = false; FaultInjection = false; }
            }
        }
    }
}

