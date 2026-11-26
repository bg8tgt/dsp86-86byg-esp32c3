#pragma once
#include <Arduino.h>

static const char INDEX_HTML[] PROGMEM = R"HTML(
<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>DSP86 步进控制器</title>
<style>
:root{--bg:#f2f6fc;--card:#fff;--txt:#1b2430;--muted:#64748b;--accent:#2563eb;--accent2:#1d4ed8;--ok:#16a34a;--bad:#dc2626;--warn:#d97706;--line:#e2e8f0;--shadow:0 6px 24px rgba(15,23,42,.08)}
*{box-sizing:border-box}
body{margin:0;background:linear-gradient(180deg,#eef4ff 0,#f7f9fc 260px);color:var(--txt);font:15px/1.6 system-ui,-apple-system,"Segoe UI",Roboto,"Microsoft YaHei",sans-serif}
.wrap{max-width:1000px;margin:0 auto;padding:20px 16px 60px}
header{display:flex;align-items:center;gap:12px;flex-wrap:wrap;margin-bottom:14px}
header h1{font-size:22px;margin:0;letter-spacing:.5px}
.badge{font-size:12px;color:var(--muted);border:1px solid var(--line);background:#fff;border-radius:999px;padding:3px 10px}
.dot{width:10px;height:10px;border-radius:50%;background:#94a3b8;box-shadow:0 0 0 4px rgba(148,163,184,.15)}
.dot.on{background:var(--ok);box-shadow:0 0 0 4px rgba(22,163,74,.15)}
.tabs{display:flex;gap:6px;background:#fff;border:1px solid var(--line);border-radius:14px;padding:6px;box-shadow:var(--shadow);position:sticky;top:10px;z-index:5}
.tabs button{flex:1;border:0;background:transparent;color:var(--muted);padding:10px 8px;border-radius:10px;font-size:15px;cursor:pointer;transition:.18s}
.tabs button.active{background:var(--accent);color:#fff;box-shadow:0 4px 12px rgba(37,99,235,.25)}
.tabs button:hover:not(.active){background:#f1f5f9;color:var(--txt)}
main{margin-top:16px}
section[hidden]{display:none}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:14px}
.card{background:var(--card);border:1px solid var(--line);border-radius:16px;padding:16px 18px;box-shadow:var(--shadow)}
.card h2{font-size:15px;margin:0 0 12px;color:var(--muted);font-weight:600;letter-spacing:.4px}
.rows{display:grid;grid-template-columns:auto 1fr;gap:8px 14px;font-size:14px}
.rows .k{color:var(--muted)}
.rows .v{text-align:right;font-variant-numeric:tabular-nums;word-break:break-all}
.big{font-size:26px;font-weight:700}
.pill{display:inline-flex;align-items:center;gap:6px;border-radius:999px;padding:3px 10px;font-size:12px;background:#f1f5f9;color:var(--muted)}
.pill.ok{background:#dcfce7;color:#166534}
.pill.bad{background:#fee2e2;color:#991b1b}
.pill.warn{background:#fef3c7;color:#92400e}
form{margin:0}
label{display:block;font-size:13px;color:var(--muted);margin:10px 0 5px}
input[type=text],input[type=password],input[type=number]{width:100%;border:1px solid var(--line);border-radius:10px;padding:10px 12px;font-size:15px;background:#fff;color:var(--txt);outline:none}
input:focus{border-color:var(--accent);box-shadow:0 0 0 3px rgba(37,99,235,.15)}
.row{display:grid;grid-template-columns:1fr 1fr;gap:12px}
.btn{display:inline-flex;align-items:center;justify-content:center;gap:6px;border:0;border-radius:10px;padding:10px 14px;font-size:14px;cursor:pointer;background:var(--accent);color:#fff;transition:.18s}
.btn:hover{background:var(--accent2)}
.btn.ghost{background:#f1f5f9;color:var(--txt)}
.btn.ghost:hover{background:#e2e8f0}
.btn.danger{background:var(--bad)}
.btn.warn{background:var(--warn)}
.btn:disabled{opacity:.5;cursor:not-allowed}
.btns{display:flex;gap:8px;flex-wrap:wrap;margin-top:14px}
.jog{display:grid;grid-template-columns:repeat(4,1fr);gap:8px;margin-top:10px}
.jog .btn{padding:12px 6px;font-size:15px}
.hint{font-size:12.5px;color:var(--muted);margin-top:10px}
.msg{font-size:13px;margin-top:10px;min-height:18px}
.msg.ok{color:var(--ok)} .msg.bad{color:var(--bad)}
.net{display:flex;align-items:center;justify-content:space-between;gap:10px;border:1px solid var(--line);border-radius:10px;padding:10px 12px;margin-top:8px;cursor:pointer;background:#fff}
.net:hover{border-color:var(--accent);background:#f8fbff}
.net .ssid{font-weight:600;font-size:14px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.net .meta{font-size:12px;color:var(--muted);white-space:nowrap}
.mono{font-family:ui-monospace,Consolas,monospace;font-size:13px}
.progress{height:10px;background:#e2e8f0;border-radius:999px;overflow:hidden;margin-top:12px;display:none}
.progress.show{display:block}
.progress > i{display:block;height:100%;width:0;background:linear-gradient(90deg,#3b82f6,#22c55e);transition:width .2s}
.ota-drop{border:1.5px dashed #cbd5e1;border-radius:12px;padding:18px;text-align:center;color:var(--muted);margin-top:10px}
.ota-drop.drag{border-color:var(--accent);background:#eff6ff}
footer{margin-top:26px;text-align:center;color:var(--muted);font-size:12px}
@media(max-width:640px){.row{grid-template-columns:1fr}.jog{grid-template-columns:repeat(2,1fr)}}
</style>
</head>
<body>
<div class="wrap">
<header>
  <h1>DSP86 步进控制器</h1>
  <span class="dot" id="dot"></span>
  <span class="badge">FW <b id="fw">--</b></span>
  <span class="badge" id="apBadge">AP: DSP86-C3</span>
</header>

<nav class="tabs">
  <button data-tab="dash" class="active">概览</button>
  <button data-tab="motor">电机</button>
  <button data-tab="pitch">俯仰</button>
  <button data-tab="wifi">WiFi</button>
  <button data-tab="ota">OTA 升级</button>
</nav>

<main>
<!-- 概览 -->
<section id="dash">
  <div class="grid">
    <div class="card">
      <h2>WiFi 连接（STA）</h2>
      <div class="rows">
        <div class="k">状态</div><div class="v"><span class="pill" id="staPill">--</span></div>
        <div class="k">SSID</div><div class="v" id="staSsid">--</div>
        <div class="k">IP</div><div class="v mono" id="staIp">--</div>
        <div class="k">信号</div><div class="v" id="staRssi">--</div>
        <div class="k">MAC</div><div class="v mono" id="mac">--</div>
      </div>
    </div>

    <div class="card">
      <h2>热点（常开）</h2>
      <div class="rows">
        <div class="k">SSID</div><div class="v" id="apSsid">--</div>
        <div class="k">IP</div><div class="v mono" id="apIp">--</div>
        <div class="k">已连接设备</div><div class="v" id="apClients">--</div>
        <div class="k">访问地址</div><div class="v mono">192.168.4.1</div>
      </div>
      <div class="hint">热点始终开启；STA 未联网时也能通过热点配置。</div>
    </div>

    <div class="card">
      <h2>系统</h2>
      <div class="rows">
        <div class="k">运行时间</div><div class="v" id="uptime">--</div>
        <div class="k">可用内存</div><div class="v" id="heap">--</div>
        <div class="k">固件版本</div><div class="v" id="fw2">--</div>
        <div class="k">mDNS</div><div class="v mono">dsp86.local</div>
      </div>
    </div>

    <div class="card">
      <h2>电机状态</h2>
      <div class="rows">
        <div class="k">位置</div><div class="v big" id="pos">0</div>
        <div class="k">速度</div><div class="v" id="speed">0 Hz</div>
        <div class="k">使能</div><div class="v"><span class="pill" id="enaPill">--</span></div>
        <div class="k">限位</div><div class="v"><span class="pill" id="limitPill">--</span></div>
        <div class="k">俯仰角</div><div class="v" id="pitch">--</div>
      </div>
    </div>
  </div>
</section>

<!-- 电机 -->
<section id="motor" hidden>
  <div class="grid">
    <div class="card">
      <h2>实时状态</h2>
      <div class="rows">
        <div class="k">位置（步）</div><div class="v big" id="mPos">0</div>
        <div class="k">速度</div><div class="v" id="mSpeed">0 Hz</div>
        <div class="k">使能</div><div class="v"><span class="pill" id="mEna">--</span></div>
        <div class="k">限位</div><div class="v"><span class="pill" id="mLimit">--</span></div>
        <div class="k">俯仰角</div><div class="v" id="mPitch">--</div>
      </div>
      <div class="btns">
        <button class="btn ghost" id="btnStop">停止</button>
        <button class="btn ghost" id="btnZero">位置清零</button>
      </div>
    </div>

    <div class="card">
      <h2>参数设置</h2>
      <form id="motorForm">
        <div class="row">
          <div>
            <label>脉冲/转（细分）</label>
            <input type="number" id="fSpr" min="200" max="51200" step="1" value="400">
          </div>
          <div>
            <label>最大速度（Hz）</label>
            <input type="number" id="fSpeed" min="100" max="200000" step="100" value="2000">
          </div>
        </div>
        <div class="row">
          <div>
            <label>加速度（步/s²）</label>
            <input type="number" id="fAccel" min="100" max="1000000" step="100" value="4000">
          </div>
          <div>
            <label>方向</label>
            <label style="display:flex;align-items:center;gap:8px;margin-top:12px;color:var(--txt)">
              <input type="checkbox" id="fInvert" style="width:18px;height:18px"> 反向
            </label>
          </div>
        </div>
        <div class="btns">
          <button class="btn" type="submit">保存参数</button>
          <button class="btn ghost" type="button" id="btnEnaOn">使能</button>
          <button class="btn ghost" type="button" id="btnEnaOff">失能</button>
        </div>
        <div class="msg" id="motorMsg"></div>
      </form>
    </div>

    <div class="card" style="grid-column:1/-1">
      <h2>点动</h2>
      <div class="jog">
        <button class="btn ghost" data-jog="-400">−1 圈</button>
        <button class="btn ghost" data-jog="-40">−40 步</button>
        <button class="btn ghost" data-jog="40">+40 步</button>
        <button class="btn ghost" data-jog="400">+1 圈</button>
      </div>
      <div class="row" style="margin-top:10px">
        <div><label>自定义步数（可负）</label><input type="number" id="fJog" value="400" step="10"></div>
        <div style="display:flex;align-items:flex-end"><button class="btn" id="btnJog" style="width:100%">执行</button></div>
      </div>
      <div class="hint">按当前细分计算：1 圈 = 脉冲/转 步。限位有效时禁止点动。</div>
    </div>
  </div>
</section>


<!-- 俯仰 -->
<section id="pitch" hidden>
  <div class="grid">
    <div class="card">
      <h2>俯仰姿态</h2>
      <div class="rows">
        <div class="k">当前角度</div><div class="v big" id="pAngle">--</div>
        <div class="k">目标角度</div><div class="v" id="pTarget">--</div>
        <div class="k">误差</div><div class="v" id="pErr">--</div>
        <div class="k">传感器电压</div><div class="v" id="pVolt">--</div>
        <div class="k">闭环状态</div><div class="v"><span class="pill" id="pState">--</span></div>
      </div>
      <div class="hint">范围 -30°（俯）～ +70°（仰）；传感器 0–3.3 V 接 GPIO3。</div>
    </div>

    <div class="card">
      <h2>目标角度</h2>
      <input type="range" id="pSlider" min="-30" max="70" step="0.5" value="0" style="width:100%">
      <div class="row" style="margin-top:10px">
        <div><label>角度（°）</label><input type="number" id="pInput" min="-30" max="70" step="0.5" value="0"></div>
        <div style="display:flex;align-items:flex-end"><button class="btn" id="pApply" style="width:100%">应用目标</button></div>
      </div>
      <div class="btns">
        <button class="btn ghost" data-preset="-30">-30° 俯</button>
        <button class="btn ghost" data-preset="0">0°</button>
        <button class="btn ghost" data-preset="70">+70° 仰</button>
      </div>
      <div class="btns">
        <button class="btn" id="pEnable">启用闭环</button>
        <button class="btn danger" id="pStop">停止</button>
      </div>
      <div class="msg" id="pMsg"></div>
    </div>

    <div class="card" style="grid-column:1/-1">
      <h2>两点校准（配合俯仰角测试仪）</h2>
      <p class="hint">手动把云台摇到机械下限 -30° 与上限 +70°，分别点下面两个按钮，记录当前传感器电压。校准后角度换算按两点线性插值。</p>
      <div class="btns">
        <button class="btn ghost" id="pCalMin">记录下限 -30°</button>
        <button class="btn ghost" id="pCalMax">记录上限 +70°</button>
      </div>
      <div class="msg" id="pCalMsg"></div>
    </div>
  </div>
</section>

<!-- WiFi -->
<section id="wifi" hidden>
  <div class="grid">
    <div class="card">
      <h2>当前 WiFi</h2>
      <div class="rows">
        <div class="k">STA 状态</div><div class="v"><span class="pill" id="wSta">--</span></div>
        <div class="k">STA SSID</div><div class="v" id="wSsid">--</div>
        <div class="k">STA IP</div><div class="v mono" id="wIp">--</div>
        <div class="k">热点</div><div class="v" id="wAp">--</div>
      </div>
      <div class="hint">热点常开：连不上路由器时，用手机连 DSP86-C3 后访问 192.168.4.1。</div>
    </div>

    <div class="card">
      <h2>连接新 WiFi</h2>
      <label>SSID</label>
      <input type="text" id="ssid" placeholder="2.4 GHz WiFi 名称">
      <label>密码</label>
      <input type="password" id="pass" placeholder="WiFi 密码">
      <div class="btns">
        <button class="btn" id="btnConnect">保存并连接</button>
        <button class="btn ghost" id="btnScan">扫描附近 WiFi</button>
      </div>
      <div class="msg" id="wifiMsg"></div>
      <div class="hint">C3 只支持 2.4 GHz；5 GHz 的 SSID 会连接失败。</div>
    </div>

    <div class="card" style="grid-column:1/-1">
      <h2>扫描结果</h2>
      <div id="netList" class="hint">点击“扫描附近 WiFi”开始。</div>
    </div>
  </div>
</section>

<!-- OTA -->
<section id="ota" hidden>
  <div class="grid">
    <div class="card" style="grid-column:1/-1">
      <h2>OTA 固件升级</h2>
      <div class="rows">
        <div class="k">当前版本</div><div class="v" id="oFw">--</div>
        <div class="k">升级方式</div><div class="v">浏览器上传 .bin（HTTP OTA）</div>
      </div>
      <div class="ota-drop" id="drop">
        <span id="dropText">点击选择固件，或把 <b>.bin</b> 文件拖到这里</span>
        <input type="file" id="fwFile" accept=".bin" hidden>
      </div>
      <div class="btns">
        <button class="btn" id="btnOta" disabled>开始升级</button>
      </div>
      <div class="progress" id="prog"><i id="progBar"></i></div>
      <div class="msg" id="otaMsg"></div>
      <div class="hint">升级过程中不要断电；完成后设备会自动重启。固件需使用与本项目相同的分区表（含 OTA）。</div>
    </div>
  </div>
</section>
</main>

<footer>ESP32-C3 · DSP86 · 86BYG450 · <span id="footFw">--</span></footer>
</div>

<script>
const $ = s => document.querySelector(s);
const $$ = s => Array.from(document.querySelectorAll(s));
let st = {}, formReady = false, pitchReady = false, scanning = false;

function fmt(n){ return (n===undefined||n===null) ? '--' : n; }
function up(sec){
  sec = sec|0; const d=Math.floor(sec/86400), h=Math.floor(sec%86400/3600), m=Math.floor(sec%3600/60), s=sec%60;
  return (d?d+'天 ':'') + String(h).padStart(2,'0')+':'+String(m).padStart(2,'0')+':'+String(s).padStart(2,'0');
}
function rssiText(r){ if(!r) return '--'; return r+' dBm'; }

async function getJSON(url){
  const r = await fetch(url, {cache:'no-store'});
  if(!r.ok) throw new Error('HTTP '+r.status);
  return r.json();
}
async function postJSON(url, obj){
  const r = await fetch(url, {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(obj||{})});
  return {ok:r.ok, data: await r.json().catch(()=>({}))};
}

function setTab(name){
  $$('.tabs button').forEach(b=>b.classList.toggle('active', b.dataset.tab===name));
  ['dash','motor','pitch','wifi','ota'].forEach(n=>{ $('#'+n).hidden = (n!==name); });
}

$$('.tabs button').forEach(b=>b.addEventListener('click', ()=>setTab(b.dataset.tab)));

function applyStatus(j){
  st = j;
  const w = j.wifi||{}, m = j.motor||{};
  $('#dot').className = 'dot' + (w.connected ? ' on' : '');
  $('#fw').textContent = j.fw; $('#fw2').textContent = j.fw; $('#oFw').textContent = j.fw; $('#footFw').textContent = 'FW '+j.fw;
  $('#uptime').textContent = up(j.uptime); $('#heap').textContent = Math.round((j.heap||0)/1024)+' KB';

  $('#staPill').textContent = w.connected ? '已连接' : (w.status||'未连接');
  $('#staPill').className = 'pill ' + (w.connected?'ok':'bad');
  $('#staSsid').textContent = fmt(w.ssid); $('#staIp').textContent = w.ip||'--';
  $('#staRssi').textContent = rssiText(w.rssi); $('#mac').textContent = w.mac||'--';
  $('#apSsid').textContent = w.ap_ssid||'--'; $('#apIp').textContent = w.ap_ip||'--';
  $('#apClients').textContent = (w.ap_clients|0) + ' 台';
  $('#apBadge').textContent = 'AP: ' + (w.ap_ssid||'--') + ' · ' + (w.ap_ip||'');

  $('#pos').textContent = m.pos|0; $('#speed').textContent = (m.speed||0).toFixed(1)+' Hz';
  $('#enaPill').textContent = m.enabled?'使能':'失能'; $('#enaPill').className='pill '+(m.enabled?'ok':'warn');
  $('#limitPill').textContent = m.limit?'触发':'正常'; $('#limitPill').className='pill '+(m.limit?'bad':'ok');
  $('#pitch').textContent = (m.pitch||0).toFixed(1)+'°';

  $('#mPos').textContent = m.pos|0; $('#mSpeed').textContent = (m.speed||0).toFixed(1)+' Hz';
  $('#mEna').textContent = m.enabled?'使能':'失能'; $('#mEna').className='pill '+(m.enabled?'ok':'warn');
  $('#mLimit').textContent = m.limit?'触发':'正常'; $('#mLimit').className='pill '+(m.limit?'bad':'ok');
  $('#mPitch').textContent = (m.pitch||0).toFixed(1)+'° (V='+(m.pitch_v||0).toFixed(2)+')';

  $('#wSta').textContent = w.connected?'已连接':'未连接'; $('#wSta').className='pill '+(w.connected?'ok':'bad');
  $('#wSsid').textContent = fmt(w.ssid); $('#wIp').textContent = w.ip||'--';
  $('#wAp').textContent = (w.ap_ssid||'')+' / '+(w.ap_ip||'');

  const pt = j.pitch || {};
  $('#pAngle').textContent = (pt.angle||0).toFixed(1)+'°';
  $('#pTarget').textContent = (pt.target||0).toFixed(1)+'°';
  $('#pErr').textContent = (pt.error||0).toFixed(2)+'°';
  $('#pVolt').textContent = (pt.volt||0).toFixed(3)+' V';
  $('#pState').textContent = pt.fault ? '传感器故障' : (pt.enabled ? '闭环运行' : '停止');
  $('#pState').className = 'pill ' + (pt.fault ? 'bad' : (pt.enabled ? 'ok' : 'warn'));
  if(!pitchReady){
    $('#pSlider').value = pt.target||0;
    $('#pInput').value = pt.target||0;
    pitchReady = true;
  }

  if(!formReady){
    $('#fSpr').value = m.spr||400; $('#fSpeed').value = m.max_speed||2000;
    $('#fAccel').value = m.accel||4000; $('#fInvert').checked = !!m.invert;
    formReady = true;
  }
}

async function refresh(){
  try { applyStatus(await getJSON('/api/status')); }
  catch(e){ $('#dot').className='dot'; }
}
setInterval(refresh, 1000);
refresh();

// 电机参数
$('#motorForm').addEventListener('submit', async e=>{
  e.preventDefault();
  const body = {
    steps_per_rev: +$('#fSpr').value,
    max_speed: +$('#fSpeed').value,
    accel: +$('#fAccel').value,
    invert: $('#fInvert').checked
  };
  const r = await postJSON('/api/motor', body);
  const msg = $('#motorMsg');
  msg.textContent = r.ok ? '已保存并生效' : '保存失败';
  msg.className = 'msg ' + (r.ok?'ok':'bad');
  formReady = false; refresh();
});

async function enable(on){
  await postJSON('/api/enable', {on});
  refresh();
}
$('#btnEnaOn').onclick = ()=>enable(true);
$('#btnEnaOff').onclick = ()=>enable(false);
$('#btnStop').onclick = async ()=>{ await postJSON('/api/stop'); refresh(); };
$('#btnZero').onclick = async ()=>{ await postJSON('/api/zero'); refresh(); };

async function jog(steps){
  const r = await postJSON('/api/jog', {steps});
  if(!r.ok){
    const msg = $('#motorMsg');
    msg.textContent = r.data.msg || '点动失败（可能限位触发）';
    msg.className = 'msg bad';
  }
  refresh();
}
$$('[data-jog]').forEach(b=>b.onclick = ()=>jog(+b.dataset.jog));
$('#btnJog').onclick = ()=>jog(+$('#fJog').value);


// 俯仰
function syncPitch(v){ $('#pSlider').value = v; $('#pInput').value = v; }
$('#pSlider').oninput = e=>{ $('#pInput').value = e.target.value; };
$('#pInput').oninput = e=>{ $('#pSlider').value = e.target.value; };

async function setPitch(a){
  const msg = $('#pMsg');
  const r = await postJSON('/api/pitch/target', {angle:+a});
  if(r.ok){ msg.textContent='目标已设置'; msg.className='msg ok'; }
  else { msg.textContent='设置失败'; msg.className='msg bad'; }
  refresh();
}
$('#pApply').onclick = ()=>setPitch($('#pInput').value);
$$('[data-preset]').forEach(b=>b.onclick = ()=>{ syncPitch(b.dataset.preset); setPitch(b.dataset.preset); });

$('#pEnable').onclick = async ()=>{
  const msg = $('#pMsg');
  const r = await postJSON('/api/pitch/enable', {on:true});
  if(r.ok){ msg.textContent='闭环已启用'; msg.className='msg ok'; }
  else { msg.textContent = r.data.msg==='sensor fault' ? '传感器故障，无法启用' : '超出行程，无法启用'; msg.className='msg bad'; }
  refresh();
};
$('#pStop').onclick = async ()=>{
  await postJSON('/api/pitch/stop', {});
  $('#pMsg').textContent='已停止'; $('#pMsg').className='msg';
  refresh();
};

async function calPitch(point){
  const msg = $('#pCalMsg');
  const r = await postJSON('/api/pitch/cal', {point});
  if(r.ok){ msg.textContent = point==='min' ? '已记录 -30° 电压' : '已记录 +70° 电压'; msg.className='msg ok'; }
  else { msg.textContent = r.data.msg || '校准失败'; msg.className='msg bad'; }
  refresh();
}
$('#pCalMin').onclick = ()=>calPitch('min');
$('#pCalMax').onclick = ()=>calPitch('max');

// WiFi
$('#btnScan').onclick = async ()=>{
  if(scanning) return;
  scanning = true;
  const list = $('#netList');
  list.innerHTML = '<div class="hint">扫描中（约 2 秒）…</div>';
  try{
    const arr = await getJSON('/api/scan');
    if(!arr.length){ list.innerHTML = '<div class="hint">没有发现网络</div>'; return; }
    list.innerHTML = '';
    arr.sort((a,b)=>(b.rssi||-999)-(a.rssi||-999));
    arr.forEach(n=>{
      const el = document.createElement('div');
      el.className='net';
      el.innerHTML = '<div><div class="ssid"></div><div class="meta"></div></div><div class="meta"></div>';
      el.querySelector('.ssid').textContent = n.ssid || '(隐藏)';
      el.querySelector('.meta').textContent = (n.enc==='OPEN'?'开放':'加密') + ' · CH' + n.ch;
      el.lastElementChild.textContent = (n.rssi||0)+' dBm';
      el.onclick = ()=>{ $('#ssid').value = n.ssid||''; $('#pass').focus(); };
      list.appendChild(el);
    });
  }catch(e){
    list.innerHTML = '<div class="hint">扫描失败</div>';
  }finally{ scanning = false; }
};

$('#btnConnect').onclick = async ()=>{
  const ssid = $('#ssid').value.trim();
  const password = $('#pass').value;
  const msg = $('#wifiMsg');
  if(!ssid){ msg.textContent='请输入 SSID'; msg.className='msg bad'; return; }
  msg.textContent='正在保存并连接…'; msg.className='msg';
  const r = await postJSON('/api/wifi', {ssid, password});
  if(r.ok){ msg.textContent='已保存，正在连接。可稍后刷新查看状态。'; msg.className='msg ok'; }
  else { msg.textContent='保存失败'; msg.className='msg bad'; }
  setTimeout(refresh, 1200);
};

// OTA
let otaFile = null;
const drop = $('#drop'), file = $('#fwFile'), btnOta = $('#btnOta');
drop.onclick = e=>{ if(e.target === file) return; file.click(); };
drop.ondragover = e=>{ e.preventDefault(); drop.classList.add('drag'); };
drop.ondragleave = ()=>drop.classList.remove('drag');
drop.ondrop = e=>{
  e.preventDefault(); drop.classList.remove('drag');
  if(e.dataTransfer.files.length){ file.files = e.dataTransfer.files; onFile(); }
};
file.onchange = onFile;
function onFile(){
  otaFile = file.files[0] || null;
  if(otaFile){
    $('#dropText').textContent = '已选择：' + otaFile.name + '（点击可重新选择）';
    btnOta.disabled = false;
  } else { btnOta.disabled = true; }
}

btnOta.onclick = ()=>{
  if(!otaFile) return;
  const msg = $('#otaMsg'), prog = $('#prog'), bar = $('#progBar');
  prog.classList.add('show'); bar.style.width='0%';
  msg.textContent='上传中…'; msg.className='msg';
  btnOta.disabled = true;

  const fd = new FormData();
  fd.append('firmware', otaFile, otaFile.name);
  const xhr = new XMLHttpRequest();
  xhr.open('POST','/api/ota');
  xhr.upload.onprogress = e=>{
    if(e.lengthComputable) bar.style.width = Math.round(e.loaded/e.total*100)+'%';
  };
  xhr.onload = ()=>{
    if(xhr.status===200){
      bar.style.width='100%';
      msg.textContent='升级成功，设备正在重启…'; msg.className='msg ok';
      setTimeout(()=>location.reload(), 5000);
    } else {
      msg.textContent='升级失败：'+(xhr.responseText||xhr.status); msg.className='msg bad';
      btnOta.disabled = false;
    }
  };
  xhr.onerror = ()=>{ msg.textContent='上传出错'; msg.className='msg bad'; btnOta.disabled=false; };
  xhr.send(fd);
};
</script>
</body>
</html>
)HTML";
