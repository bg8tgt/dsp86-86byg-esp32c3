# -*- coding: utf-8 -*-
"""图 3：接线端子图（X1 主回路 / X2 控制信号 / X3 24V 输出）"""
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Circle
from dwg import *

W, H = 14.4, 10.6
fig, ax = new_fig(W, H)
ax.set_xlim(0, W)
ax.set_ylim(0, H)

txt(ax, 0.55, 10.15, "接线端子图（X1 主回路 / X2 控制信号 / X3 24V 输出）", 15, BLUE, ha="left")
txt(ax, 0.55, 9.82, "LX-CU311 电机正反转  ·  图号 LX311-M-03  ·  2026-10", 8.8, GREY, ha="left")
txt(ax, 9.20, 9.82, "左侧＝柜内（PLC/接触器）    右侧＝现场（电缆）", 8.4, GREY, ha="left")

PITCH = 0.62
TOP = 8.35


def strip(x, rows, title, sub):
    n = len(rows)
    h = n * PITCH
    # 导轨 + 端子排底色
    ax.add_patch(Rectangle((x - 0.30, TOP - h - 0.06), 0.60, h + 0.12,
                           facecolor="#eef2f7", edgecolor="#9aa7b4", lw=0.9, zorder=2))
    txt(ax, x, TOP + 0.62, title, 10.2, BLUE)
    txt(ax, x, TOP + 0.30, sub, 8.2, GREY)
    for i, (num, inner, outer, wn) in enumerate(rows):
        y = TOP - (i + 0.5) * PITCH
        face = "#ffffff"
        ax.add_patch(Rectangle((x - 0.26, y - 0.19), 0.52, 0.38, facecolor=face,
                               edgecolor=K, lw=1.1, zorder=4))
        ax.text(x, y, num, fontsize=9.2, color=K, ha="center", va="center", zorder=7)
        # 左侧（柜内）
        line(ax, [(x - 0.26, y), (x - 0.62, y)], lw=1.4)
        txt(ax, x - 0.70, y, inner, 8.2, ha="right")
        txt(ax, x - 0.70, y + 0.135, wn[0], 6.8, GREY, ha="right")
        # 右侧（现场/负载）
        line(ax, [(x + 0.26, y), (x + 0.62, y)], lw=1.4)
        txt(ax, x + 0.70, y, outer, 8.2, ha="left")
        txt(ax, x + 0.70, y + 0.135, wn[1], 6.8, GREY, ha="left")


# ---------------- X1 主回路 ----------------
x1 = [
    ("1", "KM1:2、KM2:6", "U1  电机", ("U1", "W1-1")),
    ("2", "KM1:4、KM2:4", "V1  电机", ("V1", "W1-2")),
    ("3", "KM1:6、KM2:2", "W1  电机", ("W1", "W1-3")),
    ("4", "PE 排", "PE  电机外壳", ("PE", "W1-4")),
]
strip(3.05, x1, "X1  主回路", "4 mm²   UK-6N")

# ---------------- X2 控制信号 ----------------
x2 = [
    ("1", "DI0", "SB1  正转", ("101", "101")),
    ("2", "DI1", "SB2  反转", ("102", "102")),
    ("3", "DI2", "SB3  停止 NC", ("103", "103")),
    ("4", "DI3", "FR 97-98 NC", ("104", "104")),
    ("5", "DI4", "SB0  急停 NC", ("105", "105")),
    ("6", "DI5", "SB4  复位", ("106", "106")),
    ("7", "0V 轨", "按钮站 公共", ("107", "107")),
    ("8", "备用", "备用", ("108", "108")),
]
strip(7.65, x2, "X2  控制信号", "1.0 mm²   UK-2.5B")

# ---------------- X3 24V 输出 ----------------
x3 = [
    ("1", "F24V+ A8", "KA1/KA2/HL +", ("201", "201")),
    ("2", "COM B8", "0V", ("202", "202")),
    ("3", "DO0", "KA1 线圈 −", ("203", "203")),
    ("4", "DO1", "KA2 线圈 −", ("204", "204")),
    ("5", "DO6", "HL1 −", ("205", "205")),
    ("6", "DO7", "HL2 −", ("206", "206")),
]
strip(12.30, x3, "X3  24V 输出", "1.0 mm²   UK-2.5B")

# ---------------- 电缆标注 ----------------
txt(ax, 0.55, 5.62, "W1：4×1.5 mm² 屏蔽电缆 → 电机", 8.2, GREY, ha="left")
txt(ax, 0.55, 5.38, "W2：8×0.75 mm² → 按钮站", 8.2, GREY, ha="left")

# ---------------- 底部：选型与说明 ----------------
bx, by, bw = 0.55, 1.55, 8.55
rows_txt = [
    ("端子排", "X1：UK-6N（4 mm²，4 位）  X2/X3：UK-2.5B（2.5 mm²）"),
    ("标记条", "按端子号 1、2、3… 打印，两端各贴一条"),
    ("短接", "X2:7 与 X3:2 同属 0V，柜内短接片连通（或共接 0V 排）"),
    ("备用", "X2:8 预留（现场传感器/第二急停），不接线时上紧螺钉"),
]
bh = 0.30 * len(rows_txt) + 0.25
ax.add_patch(Rectangle((bx, by), bw, bh, fill=False, ec=GREY, lw=1.0))
for i, (k, v) in enumerate(rows_txt):
    yy = by + bh - 0.32 - i * 0.30
    txt(ax, bx + 0.12, yy, k, 8.4, BLUE, ha="left")
    txt(ax, bx + 0.95, yy, v, 8.2, K, ha="left")
txt(ax, bx, by + bh + 0.16, "端子排选型", 8.8, GREY, ha="left")

notes = [
    "① 端子号两侧一致：柜内侧接 PLC/接触器，外侧接现场电缆；同一端子只接一根线，多根时加跨接片。",
    "② X1 主回路按相序接线：端子 1/2/3 对应 U1/V1/W1，其中 KM2 的 2、6 号出线已对调（见图 1 LX311-M-01）。",
    "③ 控制信号用 1.0 mm²，主回路 4 mm²，PE 用黄绿双色线；屏蔽层单端接地。",
    "④ 急停 SB0 的 220V 常闭触点若在柜外，另设 X4 两位端子（L、N 回路），不得与控制信号共用端子。",
    "⑤ 接线完成逐点核线：断电用万用表通断档，按本图从左到右逐条确认。",
]
for i, s in enumerate(notes):
    txt(ax, 0.55, 0.95 - i * 0.19, s, 7.9, "#333333", ha="left")

txt(ax, 9.55, 3.10, "线号规则", 8.8, GREY, ha="left")
for i, s in enumerate(["1xx   控制信号（X2）", "2xx   24V 输出（X3）", "U1/V1/W1/PE   主回路", "W1 / W2   现场电缆"]):
    txt(ax, 9.55, 2.80 - i * 0.24, s, 8.2, "#333333", ha="left")
plt.subplots_adjust(left=0.005, right=0.995, top=0.995, bottom=0.005)
plt.savefig(r"E:\deepseek\LX311_电机正反转\图3_接线端子图.png", dpi=190,
            facecolor="white", bbox_inches="tight", pad_inches=0.1)
print("ok")




