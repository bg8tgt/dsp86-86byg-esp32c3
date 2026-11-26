# -*- coding: utf-8 -*-
"""
PRG_MotorFwdRev 的行为模型（10 ms 扫描周期），用于验证 ST 逻辑：
  1) KM1 / KM2 任何时刻不同时为 1
  2) 换向（KM1 -> KM2 或反向）间隔 >= 300 ms
  3) 热继 / 急停 -> 立即停机 + 故障锁存，复位后不自动重启
  4) 停止后不自动重启
这不是 PLC 运行时，只验证算法本身。
"""
import random

DT = 0.010          # 10 ms 扫描
PT = 0.300          # 死区 300 ms


class PLC:
    def __init__(self):
        self.xFwdPB = False
        self.xRevPB = False
        self.xStopPB = True
        self.xFR = True
        self.xEStop = True
        self.xResetPB = False

        self.xKM1 = False
        self.xKM2 = False
        self.xLampRun = False
        self.xLampFault = False

        self.xFault = False
        self.xFwdReq = False
        self.xRevReq = False
        self.xFwdOld = False
        self.xRevOld = False
        self.et = 0.0
        self.q = False
        self.iStep = 0

    def _ton(self, inp):
        if inp:
            self.et += DT
            self.q = self.et >= PT - 1e-9
        else:
            self.et = 0.0
            self.q = False

    def scan(self):
        # 1 故障锁存
        if (not self.xFR) or (not self.xEStop):
            self.xFault = True
        elif self.xResetPB and self.xFR and self.xEStop:
            self.xFault = False

        # 2 上升沿
        fwd_edge = self.xFwdPB and not self.xFwdOld
        rev_edge = self.xRevPB and not self.xRevOld
        self.xFwdOld = self.xFwdPB
        self.xRevOld = self.xRevPB

        # 3 请求
        if fwd_edge and not self.xFault:
            self.xFwdReq = True
            self.xRevReq = False
        if rev_edge and not self.xFault:
            self.xRevReq = True
            self.xFwdReq = False
        if (not self.xStopPB) or self.xFault:
            self.xFwdReq = False
            self.xRevReq = False

        # 4 死区定时
        self._ton(self.iStep == 0)

        # 5 状态机
        if self.iStep == 0:
            if self.xStopPB and not self.xFault and self.q:
                if self.xFwdReq:
                    self.iStep = 1
                elif self.xRevReq:
                    self.iStep = 2
        elif self.iStep == 1:
            if (not self.xStopPB) or self.xFault or self.xRevReq:
                self.iStep = 0
        elif self.iStep == 2:
            if (not self.xStopPB) or self.xFault or self.xFwdReq:
                self.iStep = 0
        else:
            self.iStep = 0

        # 6 输出
        self.xKM1 = (self.iStep == 1)
        self.xKM2 = (self.iStep == 2)
        self.xLampRun = (self.iStep != 0)
        self.xLampFault = self.xFault

        assert not (self.xKM1 and self.xKM2), "互锁失效：KM1/KM2 同时为 1"


def run(plc, seconds):
    """返回 (t, 快照) 列表，每 10 ms 一个"""
    log = []
    for k in range(int(seconds / DT)):
        t = (k + 1) * DT
        plc.scan()
        log.append((t, (plc.xKM1, plc.xKM2, plc.xFault, plc.xLampRun)))
    return log


def first_true(log, idx, after=0.0):
    for t, s in log:
        if t >= after and s[idx]:
            return t
    return None


print("=== 场景 1：启动 → 停止 ===")
p = PLC()
p.xFwdPB = True
log = run(p, 0.05)
p.xFwdPB = False
log += run(p, 0.45)
t_on = first_true(log, 0)
assert p.xKM1 and not p.xKM2, "正转未吸合"
p.xStopPB = False
log2 = run(p, 0.05)
p.xStopPB = True
run(p, 0.40)
assert not p.xKM1 and not p.xKM2, "停止后未断开"
print(f"  正转吸合于 t={t_on:.2f}s，停止后立即断开，0.4s 内未自启 → OK")

print("=== 场景 2：运行中直接按反转（换向死区）===")
p = PLC()
p.xFwdPB = True
run(p, 0.05)
p.xFwdPB = False
run(p, 0.30)
assert p.xKM1
t_off = None
p.xRevPB = True
t0 = 0.35
steps = run(p, 0.05)
p.xRevPB = False
steps += run(p, 0.60)
t_rev = first_true(steps, 1)
# 记录 KM1 断开时刻：简单起见用上面 assert 后的扫描
gap = t_rev - 0.0  # 占位
# 重新精确测量
p = PLC()
p.xFwdPB = True
run(p, 0.05)
p.xFwdPB = False
log = run(p, 0.30)
assert p.xKM1
p.xRevPB = True
log = run(p, 0.60)   # 按下反转后持续观察
p.xRevPB = False
t_km1_off = None
for i, (t, s) in enumerate(log):
    if not s[0] and t_km1_off is None:
        t_km1_off = t
    if s[1] and t_km1_off is not None:
        t_km2_on = t
        break
gap = t_km2_on - t_km1_off
print(f"  KM1 断开 → KM2 吸合间隔 = {gap*1000:.0f} ms")
assert gap >= PT - DT, "换向死区不足"
assert p.xKM2 and not p.xKM1
print("  → OK（先断后合，>=300ms）")

print("=== 场景 3：热继跳闸 → 锁存 → 复位 → 手动重启 ===")
p = PLC()
p.xFwdPB = True
run(p, 0.05)
p.xFwdPB = False
run(p, 0.30)
assert p.xKM1
p.xFR = False
run(p, 0.05)
assert not p.xKM1 and p.xFault, "热继跳闸未停机"
p.xFR = True
run(p, 0.30)
assert not p.xKM1, "热继恢复后自动重启（危险）"
assert p.xFault, "故障未锁存"
p.xResetPB = True
run(p, 0.05)
p.xResetPB = False
run(p, 0.30)
assert not p.xKM1, "复位后自动重启（危险）"
p.xFwdPB = True
run(p, 0.05)
p.xFwdPB = False
run(p, 0.20)
assert p.xKM1, "复位后手动启动失败"
print("  → OK（跳闸即停、故障锁存、复位后必须手动再启动）")

print("=== 场景 4：急停 ===")
p = PLC()
p.xFwdPB = True
run(p, 0.05)
p.xFwdPB = False
run(p, 0.30)
p.xEStop = False
run(p, 0.02)
assert not p.xKM1 and p.xFault
print("  → OK")

print("=== 场景 5：随机模糊测试（600 s 随机按钮）===")
random.seed(7)
p = PLC()
km1_prev = 0
last_state_since = 0.0
min_gap = 9e9
gap_timer = None
for k in range(60000):
    r = random.random()
    p.xFwdPB = (0.15 < r < 0.35)
    p.xRevPB = (0.45 < r < 0.65)
    p.xStopPB = (r < 0.95)          # 5% 时间按停止（NC 断开）
    p.xFR = not (0.97 < r < 0.975)
    p.xEStop = not (0.985 < r < 0.99)
    p.xResetPB = (0.90 < r < 0.93)
    p.scan()
    assert not (p.xKM1 and p.xKM2), f"互锁失败 @scan {k}"
    # 统计 KM1/KM2 切换间隔
    if p.xKM1 and not p.xKM2:
        if gap_timer is not None:
            min_gap = min(min_gap, (k * DT) - gap_timer)
            gap_timer = None
    elif p.xKM2 and not p.xKM1:
        if gap_timer is not None:
            min_gap = min(min_gap, (k * DT) - gap_timer)
            gap_timer = None
    else:
        if gap_timer is None:
            gap_timer = k * DT
print(f"  600 s 内 KM1/KM2 从未同时为 1；最小换向间隔 = {min_gap*1000:.0f} ms（含 5% 停止/跳闸干扰）")
if min_gap < 9e9:
    assert min_gap >= PT - 3 * DT, "存在短于死区的换向"
print("  → OK")

print("\n全部场景通过。")
