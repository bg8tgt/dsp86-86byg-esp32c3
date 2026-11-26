# -*- coding: utf-8 -*-
"""图 2：控制原理图（220V 接触器回路 + 24V PLC 接线）"""
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Circle
from dwg import *

W, H = 12.9, 16.2
fig, ax = new_fig(W, H)
ax.set_xlim(0, W)
ax.set_ylim(0, H)

txt(ax, 0.55, 15.80, "控制原理图（220V 接触器回路 + 24V PLC 接线）", 15.5, BLUE, ha="left")
txt(ax, 0.55, 15.45, "LX-CU311 电机正反转  ·  图号 LX311-M-02  ·  2026-10", 9, GREY, ha="left")

# ============================================================
# 一、220V 接触器回路
# ============================================================
XL, XN = 1.00, 8.55
Y_R1, Y_R2 = 14.55, 13.45

line(ax, [(XL, Y_R2 - 0.40), (XL, Y_R1 + 0.42)])
line(ax, [(XN, Y_R2 - 0.40), (XN, Y_R1 + 0.42)])
txt(ax, XL, Y_R1 + 0.60, "L  220V", 9.5, GREY)
txt(ax, XN, Y_R1 + 0.60, "N", 9.5, GREY)

line(ax, [(XL, Y_R1), (1.64, Y_R1)])
contact_h(ax, 1.92, Y_R1, kind="sw")
txt(ax, 1.92, Y_R1 + 0.34, "QF2  2A", 9.2)
line(ax, [(2.08, Y_R1), (2.64, Y_R1)])
contact_h(ax, 2.94, Y_R1, kind="nc")
txt(ax, 2.94, Y_R1 + 0.34, "SB0 急停", 9.2)
line(ax, [(3.12, Y_R1), (3.68, Y_R1)])
contact_h(ax, 3.98, Y_R1, kind="nc")
txt(ax, 3.98, Y_R1 + 0.34, "FR 95-96", 9.2)
line(ax, [(4.16, Y_R1), (4.88, Y_R1)])
dot(ax, 4.88, Y_R1)

line(ax, [(4.88, Y_R1), (5.10, Y_R1)])
contact_h(ax, 5.42, Y_R1, kind="no")
txt(ax, 5.42, Y_R1 + 0.34, "KA1", 9.2)
line(ax, [(5.58, Y_R1), (6.20, Y_R1)])
contact_h(ax, 6.52, Y_R1, kind="nc")
txt(ax, 6.52, Y_R1 + 0.34, "KM2 互锁", 9.2)
line(ax, [(6.68, Y_R1), (7.62, Y_R1)])
coil_h(ax, 8.00, Y_R1, label="KM1 线圈 220V")

line(ax, [(8.38, Y_R1), (XN, Y_R1)])

line(ax, [(4.88, Y_R1), (4.88, Y_R2)])
line(ax, [(4.88, Y_R2), (5.10, Y_R2)])
contact_h(ax, 5.42, Y_R2, kind="no")
txt(ax, 5.42, Y_R2 + 0.34, "KA2", 9.2)
line(ax, [(5.58, Y_R2), (6.20, Y_R2)])
contact_h(ax, 6.52, Y_R2, kind="nc")
txt(ax, 6.52, Y_R2 + 0.34, "KM1 互锁", 9.2)
line(ax, [(6.68, Y_R2), (7.62, Y_R2)])
coil_h(ax, 8.00, Y_R2, label="KM2 线圈 220V")

line(ax, [(8.38, Y_R2), (XN, Y_R2)])

notes_220 = [
    "急停 SB0、热继 FR 的常闭触点硬线",
    "串在接触器线圈回路，PLC 故障或",
    "死机也不影响停机。",
    "KM1 线圈串 KM2 常闭辅助触点，",
    "KM2 线圈串 KM1 常闭，与程序互",
    "锁构成双重保护。",
    "接触器按 AC-3 选用，线圈 220V。",
    "KA1/KA2 触点由 DO0/DO1 驱动（见下）。",
]
txt(ax, 9.15, 14.75, "220V 回路说明", 9.8, BLUE, ha="left")
for i, s in enumerate(notes_220):
    txt(ax, 9.15, 14.40 - i * 0.24, s, 8.4, "#333333", ha="left")

# ============================================================
# 二、24V PLC 接线
# ============================================================
PX, PW = 1.00, 2.70
PY, PH = 2.15, 9.25

box(ax, PX, PY, PW, PH, "")
txt(ax, PX + PW / 2, PY + PH - 0.40, "LX-CU311", 12.5)
txt(ax, PX + PW / 2, PY + PH - 0.72, "16DI / 16DO", 8.6, GREY)
txt(ax, PX + PW / 2, PY + PH - 0.96, "DO：NPN 型 24V", 8.0, GREY)

X_T = PX + PW
PINS = [
    ("DI0", 10.85, "SB1 正转"), ("DI1", 10.33, "SB2 反转"),
    ("DI2", 9.81, "SB3 停止"), ("DI3", 9.29, "FR 97-98"),
    ("DI4", 8.77, "SB0 急停②"), ("DI5", 8.25, "SB4 复位"),
    ("SS0", 7.63, ""), ("SS1", 7.23, ""),
    ("DO0", 6.58, ""), ("DO1", 6.06, ""),
    ("DO6", 5.54, ""), ("DO7", 5.02, ""),
    ("F24V+", 4.35, ""), ("COM", 2.78, ""),
]
for name, y, _ in PINS:
    txt(ax, X_T - 0.10, y, name, 8.6, ha="right")
    term(ax, X_T, y, r=0.05)

X_0V, X_24V = 8.00, 7.10

for name, y, dev in PINS[:6]:
    line(ax, [(X_T, y), (4.95, y)])
    kind = "nc" if name in ("DI2", "DI3", "DI4") else "no"
    contact_h(ax, 5.30, y, kind=kind, w=0.44, h=0.15)
    txt(ax, 5.30, y + 0.27, dev, 8.0)
    line(ax, [(5.48, y), (X_0V, y)])

for name, y, _ in PINS[6:8]:
    line(ax, [(X_T, y), (X_24V, y)])
    dot(ax, X_24V, y)

OUT = [("DO0", 6.58, "KA1", "coil"), ("DO1", 6.06, "KA2", "coil"),
       ("DO6", 5.54, "HL1", "lamp"), ("DO7", 5.02, "HL2", "lamp")]
for name, y, dev, kind in OUT:
    line(ax, [(X_T, y), (4.95, y)])
    if kind == "coil":
        ax.add_patch(Rectangle((4.95, y - 0.17), 1.10, 0.34, fill=False, ec=K, lw=1.5, zorder=4))
        txt(ax, 5.50, y, dev, 9.2)
    else:
        ax.add_patch(Circle((5.50, y), 0.16, fill=False, ec=K, lw=1.5, zorder=4))
        line(ax, [(5.39, y - 0.11), (5.61, y + 0.11)], lw=1.0)
        line(ax, [(5.39, y + 0.11), (5.61, y - 0.11)], lw=1.0)
        txt(ax, 5.50, y + 0.26, dev, 8.6)
    line(ax, [(6.05, y), (X_24V, y)])
    dot(ax, X_24V, y)

# F24V+ → +24V 母线
line(ax, [(X_T, 4.35), (X_24V, 4.35)])
dot(ax, X_24V, 4.35)

# 0V 母线 + COM
line(ax, [(X_0V, 1.30), (X_0V, 11.05)], lw=2.0)
line(ax, [(X_T, 2.78), (3.86, 2.78), (3.86, 1.30), (X_0V, 1.30)])
dot(ax, X_0V, 1.30)

line(ax, [(X_24V, 4.15), (X_24V, 7.85)], lw=2.0)
txt(ax, X_0V, 11.22, "0V", 10, GREY)
txt(ax, X_24V, 8.02, "+24V（F24V+）", 9.2, GREY)

# --- 24V 电源 ---
box(ax, 4.45, 1.52, 1.95, 0.72, "")
txt(ax, 5.42, 2.04, "24V 开关电源 ≥30W", 8.2)
txt(ax, 5.42, 1.76, "+24V / 0V 输出", 7.8, GREY)
line(ax, [(6.40, 1.98), (X_24V, 1.98), (X_24V, 4.15)])
line(ax, [(6.40, 1.70), (X_0V, 1.70)])
dot(ax, X_0V, 1.70)

# --- 续流二极管详图 ---
dx, dy = 9.05, 3.55
box(ax, dx, dy - 0.75, 3.30, 1.78, "")
txt(ax, dx + 1.65, dy + 0.88, "感性负载必须加续流二极管", 8.6, BLUE)
line(ax, [(dx + 0.35, dy + 0.38), (dx + 2.95, dy + 0.38)])
line(ax, [(dx + 0.35, dy - 0.18), (dx + 2.95, dy - 0.18)])
# 线圈
ax.add_patch(Rectangle((dx + 1.32, dy - 0.02), 0.62, 0.40, fill=False, ec=K, lw=1.4))
txt(ax, dx + 1.63, dy + 0.18, "KA", 8.2)
txt(ax, dx + 1.63, dy + 0.60, "线圈", 7.8)
line(ax, [(dx + 1.63, dy + 0.38), (dx + 1.63, dy + 0.38)])
# 二极管支路
line(ax, [(dx + 0.98, dy + 0.38), (dx + 0.98, dy - 0.18)])
diode(ax, dx + 0.98, dy + 0.10)
line(ax, [(dx + 2.30, dy + 0.38), (dx + 2.30, dy - 0.18)])
txt(ax, dx + 0.86, dy + 0.16, "1N4007", 7.2, GREY, ha="right")
txt(ax, dx + 3.02, dy + 0.38, "+24V", 7.4, GREY, ha="left")
txt(ax, dx + 3.02, dy - 0.18, "DO", 7.4, GREY, ha="left")
txt(ax, dx + 1.65, dy - 0.55, "阴极朝 +24V，反接会短路输出", 7.2, RED)

# --- 底部说明 ---
notes = [
    "① DI 组按 NPN 模式：SS0/SS1 接 +24V，按钮另一端接 0V；若改 PNP，SS 接 0V、按钮接 +24V，程序输入逻辑取反。",
    "② SB3 停止、FR、SB0 急停均用常闭触点：断线、失电、按钮损坏都等同“按下停止”，属故障安全接法。",
    "③ FR 用两组常闭：95-96 进 220V 线圈回路，97-98 进 DI3；SB0 同样用两组常闭（一组硬线、一组进 DI4）。",
    "④ DO 为 NPN 漏型输出：负载一端接 F24V+，另一端接 DO；单点 0.5A，感性负载必须并续流二极管。",
]
for i, s in enumerate(notes):
    txt(ax, 0.55, 0.95 - i * 0.22, s, 8.2, "#333333", ha="left")

plt.subplots_adjust(left=0.005, right=0.995, top=0.995, bottom=0.005)
plt.savefig(r"E:\deepseek\LX311_电机正反转\图2_控制原理图.png", dpi=190,
            facecolor="white", bbox_inches="tight", pad_inches=0.1)
print("ok")


