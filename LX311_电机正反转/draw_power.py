# -*- coding: utf-8 -*-
"""图 1：主回路接线图 + 电机端子接线（LX-CU311 正反转）"""
import matplotlib.pyplot as plt
from dwg import *

fig, ax = new_fig(13.0, 8.9)
ax.set_xlim(0, 13.0)
ax.set_ylim(0, 8.9)

txt(ax, 0.45, 8.55, "主回路接线图（三相 380V  正反转）", 15.5, BLUE, ha="left")
txt(ax, 0.45, 8.20, "LX-CU311 电机正反转  ·  图号 LX311-M-01  ·  2026-10", 9, GREY, ha="left")

Y1, Y2, Y3 = 7.30, 6.55, 5.80
X_S = 1.05
X_QF, X_FR = 2.25, 3.60
X_SPLIT = 4.85
X_KM1 = 6.00
X_KM2 = 6.75
X_M = 9.55
Y_L = [4.72, 4.00, 3.28]

for y, name in ((Y1, "L1"), (Y2, "L2"), (Y3, "L3")):
    line(ax, [(X_S, y), (X_M, y)], lw=1.9)
    txt(ax, 0.72, y, name, 12, K, ha="right")
txt(ax, 0.10, 7.78, "三相 380V 50Hz", 8.6, GREY, ha="left")

# ---------------- QF1 ----------------
for y in (Y1, Y2, Y3):
    contact_h(ax, X_QF, y, kind="sw")
line(ax, [(X_QF + 0.30, Y3 - 0.18), (X_QF + 0.30, Y1 + 0.20)], lw=1.0, ls=(0, (3, 2)))
txt(ax, X_QF, 7.70, "QF1", 11)
txt(ax, X_QF, 5.48, "3P C16", 8.4, GREY)

# ---------------- FR ----------------
for y in (Y1, Y2, Y3):
    heater_h(ax, X_FR, y)
line(ax, [(X_FR + 0.30, Y3 - 0.18), (X_FR + 0.30, Y1 + 0.20)], lw=1.0, ls=(0, (3, 2)))
txt(ax, X_FR, 7.70, "FR", 11)
txt(ax, X_FR, 5.48, "热继电器", 8.4, GREY)

for y in (Y1, Y2, Y3):
    dot(ax, X_SPLIT, y)

# ---------------- KM1 ----------------
for y in (Y1, Y2, Y3):
    contact_h(ax, X_KM1, y, kind="no")
line(ax, [(X_KM1 + 0.30, Y3 - 0.18), (X_KM1 + 0.30, Y1 + 0.20)], lw=1.0, ls=(0, (3, 2)))
txt(ax, X_KM1, 7.70, "KM1", 11)
txt(ax, X_KM1, 5.48, "正转", 8.4, GREY)

# ---------------- KM2 支路 ----------------
for y0, yl in zip((Y1, Y2, Y3), Y_L):
    line(ax, [(X_SPLIT, y0), (X_SPLIT, yl)])
    line(ax, [(X_SPLIT, yl), (X_KM2 - 0.23, yl)])
    contact_h(ax, X_KM2, yl, kind="no")
    txt(ax, X_KM2 - 0.10, yl + 0.30, "KM2", 8.6)
line(ax, [(X_KM2 + 0.36, Y_L[2] - 0.10), (X_KM2 + 0.36, Y_L[0] + 0.10)],
     lw=1.0, ls=(0, (3, 2)))

RISE = [(8.30, Y_L[0], Y3), (8.58, Y_L[1], Y2), (8.86, Y_L[2], Y1)]
for xr, yfrom, yto in RISE:
    line(ax, [(X_KM2 + 0.23, yfrom), (xr, yfrom)])
    line(ax, [(xr, yfrom), (xr, yto)])
    dot(ax, xr, yto)

txt(ax, 8.10, 2.93, "换相：KM2 出线 L1 与 L3 对调（U1←L3，W1←L1）", 8.4, RED)

# ---------------- 电机 ----------------
for y, name in ((Y1, "U1"), (Y2, "V1"), (Y3, "W1")):
    term(ax, X_M, y)
    txt(ax, X_M - 0.20, y + 0.22, name, 10)
    line(ax, [(X_M + 0.075, y), (10.20, y)])
box(ax, 10.20, 5.05, 1.55, 2.95, "")
txt(ax, 10.97, 6.38, "M", 22, K)
txt(ax, 10.97, 5.98, "3~", 13, K)
txt(ax, 10.97, 5.62, "1.5 kW", 9.5)
txt(ax, 10.97, 5.38, "380V / Y", 9.5)
line(ax, [(10.97, 5.05), (10.97, 4.62)])
earth(ax, 10.97, 4.62)

# ---------------- 电机接线盒 ----------------
box(ax, 1.05, 0.75, 3.50, 1.85, "")
txt(ax, 2.80, 2.82, "电机接线盒（6 端子）", 10.5)
TX = [1.72, 2.70, 3.68]
for x, n1, n2 in zip(TX, ["U1", "V1", "W1"], ["W2", "U2", "V2"]):
    term(ax, x, 2.10)
    term(ax, x, 1.46)
    txt(ax, x, 2.30, n1, 9.5)
    txt(ax, x, 1.24, n2, 9.5)
    line(ax, [(x, 2.02), (x, 1.54)])
    line(ax, [(x, 1.46), (x, 1.10)])
for x in TX:
    dot(ax, x, 1.10)
line(ax, [(TX[0], 1.10), (TX[2], 1.10)], lw=1.6)
txt(ax, 2.72, 0.93, "Y 短接片（W2-U2-V2）", 8.2, RED)
txt(ax, 2.80, 0.52, "Δ 接法：U1-W2、V1-U2、W1-V2 竖联（220V）", 7.4, GREY)

txt(ax, 4.95, 1.55, "U1 V1 W1 由 KM1 / KM2 出线接入", 8.6, GREY, ha="left")
line(ax, [(4.55, 1.48), (4.88, 1.48)], lw=1.0, ls=(0, (3, 2)))

# ---------------- 设备表 ----------------
rows = [
    ("QF1", "DZ47-63  3P  C16（按电机功率选）"),
    ("FR", "3UA59 热继电器，整定 1.05~1.1×Ie"),
    ("KM1/KM2", "AC-3 接触器，线圈 220V 50Hz"),
    ("M", "三相异步 1.5kW  380V  星形"),
    ("PE", "电机外壳、PLC、开关电源 PE 共地"),
]
bx, by, bw = 7.30, 0.62, 5.45
bh = 0.34 * len(rows) + 0.10
ax.add_patch(Rectangle((bx, by), bw, bh, fill=False, ec=GREY, lw=1.0))
for i, (k, v) in enumerate(rows):
    yy = by + bh - 0.26 - i * 0.34
    txt(ax, bx + 0.12, yy, k, 9, BLUE, ha="left")
    txt(ax, bx + 1.10, yy, v, 9, K, ha="left")
txt(ax, bx + 0.12, by + bh + 0.16, "设备表", 9.5, GREY, ha="left")

plt.subplots_adjust(left=0.005, right=0.995, top=0.995, bottom=0.005)
plt.savefig(r"E:\deepseek\LX311_电机正反转\图1_主回路接线图.png", dpi=200,
            facecolor="white", bbox_inches="tight", pad_inches=0.1)
print("ok")
