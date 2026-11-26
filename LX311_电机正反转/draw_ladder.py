# -*- coding: utf-8 -*-
"""LX-CU311 电机正反转梯形图（基础版）——槽位布局，标签上下分离"""
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib import font_manager as fm
from matplotlib.patches import Rectangle, Circle

for f in (r"C:\Windows\Fonts\simhei.ttf", r"C:\Windows\Fonts\msyh.ttc"):
    try:
        fm.fontManager.addfont(f)
    except Exception:
        pass
plt.rcParams["font.family"] = ["SimHei", "Microsoft YaHei"]

W, H = 11.2, 15.75
fig, ax = plt.subplots(figsize=(W, H))
ax.set_xlim(0, W)
ax.set_ylim(0, H)
ax.axis("off")

LW = 1.7
C = "#111111"
BLUE = "#0b4f9c"
GREY = "#5a5a5a"

X0 = 1.00
SLOT = 1.86
Y0 = 13.20
DY = 2.35
BR = 0.56          # 并联支路垂直偏移


def hline(x1, x2, y):
    ax.plot([x1, x2], [y, y], color=C, lw=LW, solid_capstyle="round")


def vline(x, y1, y2, lw=LW):
    ax.plot([x, x], [y1, y2], color=C, lw=lw, solid_capstyle="round")


def contact(cx, y, kind, label, tag="", side=1):
    g, h = 0.19, 0.26
    hline(cx - 0.58, cx - g, y)
    hline(cx + g, cx + 0.58, y)
    vline(cx - g, y - h, y + h)
    vline(cx + g, y - h, y + h)
    if kind == "nc":
        ax.plot([cx - g - 0.08, cx + g + 0.08], [y - h - 0.05, y + h + 0.05],
                color=C, lw=LW)
    if side > 0:
        ax.text(cx, y + h + 0.10, label, ha="center", va="bottom", fontsize=10.5)
        if tag:
            ax.text(cx, y - h - 0.10, tag, ha="center", va="top", fontsize=8.3, color=GREY)
    else:
        ax.text(cx, y - h - 0.10, label, ha="center", va="top", fontsize=10.5)
        if tag:
            ax.text(cx, y + h + 0.10, tag, ha="center", va="bottom", fontsize=8.3, color=GREY)


def coil(cx, y, label, tag="", kind="out"):
    if kind == "out":
        ax.add_patch(Circle((cx, y), 0.17, fill=False, ec=C, lw=LW))
    else:
        ax.add_patch(Rectangle((cx - 0.20, y - 0.17), 0.40, 0.34, fill=False, ec=C, lw=LW))
        ax.text(cx, y - 0.005, kind.upper(), ha="center", va="center",
                fontsize=10, fontweight="bold")
    ax.text(cx, y + 0.26, label, ha="center", va="bottom", fontsize=10.5)
    if tag:
        ax.text(cx, y - 0.26, tag, ha="center", va="top", fontsize=8.3, color=GREY)


def rung(y, cells, coil_spec, tag):
    ax.text(0.30, y, tag, fontsize=12, color=BLUE, ha="center", va="center",
            fontweight="bold")
    x = X0
    for cell in cells:
        if isinstance(cell, tuple):
            contact(x + SLOT / 2, y, cell[0], cell[1], cell[2] if len(cell) > 2 else "")
            x += SLOT
        else:
            offs = ([BR, -BR] if len(cell) == 2 else [0.0])
            span = BR if len(cell) == 2 else 0.0
            vline(x, y - span, y + span)
            for br, off in zip(cell, offs):
                cx = x + SLOT / 2
                side = 1 if off >= 0 else -1
                contact(cx, y + off, br[0], br[1], "", side)
                hline(x, cx - 0.58, y + off)
                hline(cx + 0.58, x + SLOT, y + off)
            vline(x + SLOT, y - span, y + span)
            x += SLOT
    hline(x, x + 0.60, y)
    coil(x + 0.78, y, coil_spec[0], coil_spec[2] if len(coil_spec) > 2 else "", coil_spec[1])


ax.text(W / 2, 14.78, "和利时 LX-CU311 电机正反转控制  梯形图（基础版）",
        ha="center", va="center", fontsize=17, color=BLUE)
ax.text(W / 2, 14.40, "SB1 正转   SB2 反转   SB3 停止   FR 热继电器   SB0 急停   SB4 复位",
        ha="center", va="center", fontsize=10.5, color=GREY)

rung(Y0, [("nc", "热继 FR", "DI3 常闭"), ("nc", "急停 SB0", "DI4 常闭")],
     ("故障锁存 M0.0", "s", "置位"), "1")
rung(Y0 - DY, [("no", "复位 SB4", "DI5 常开"), ("no", "热继正常", "DI3"),
               ("no", "急停正常", "DI4")],
     ("故障锁存 M0.0", "r", "复位"), "2")
rung(Y0 - 2 * DY, [[("no", "SB1 正转", "DI0"), ("no", "KM1 自锁", "保持")],
                   ("no", "SB3 停止", "DI2 常闭"), ("nc", "故障", "M0.0"),
                   ("nc", "KM2 互锁", "反向")],
     ("KM1 正转接触器", "out", "DO0"), "3")
rung(Y0 - 3 * DY, [[("no", "SB2 反转", "DI1"), ("no", "KM2 自锁", "保持")],
                   ("no", "SB3 停止", "DI2 常闭"), ("nc", "故障", "M0.0"),
                   ("nc", "KM1 互锁", "正向")],
     ("KM2 反转接触器", "out", "DO1"), "4")
rung(Y0 - 4 * DY, [[("no", "KM1 运行", "正转"), ("no", "KM2 运行", "反转")]],
     ("运行指示灯", "out", "DO6"), "5")
rung(Y0 - 5 * DY, [("no", "故障锁存", "M0.0")],
     ("故障指示灯", "out", "DO7"), "6")

vline(X0, Y0 - 5 * DY - 0.55, Y0 + 0.95, lw=2.6)
ax.text(X0, Y0 + 1.12, "L+24V", ha="center", fontsize=9, color=GREY)

notes = [
    "① 停止、急停、热继全部接常闭触点：断线、失电、按钮损坏都会直接停机（故障安全）。",
    "② 梯形图只做软件互锁；接触器线圈回路必须再串对方常闭辅助触点做硬件互锁，两道互锁缺一不可。",
    "③ 本体 DO 为 NPN 型 24VDC 输出（0.5A/点），经中间继电器 KA 驱动 220V 接触器线圈；",
    "    线圈两端并 1N4007 续流二极管（阴极接 +24V），否则关断瞬间会击穿输出管。",
    "④ 本图为入门版：停机后可立即换向、无死区。工程版（ST）增加了 300ms 换向死区、故障复位确认。",
]
ax.text(0.55, 0.62, "\n".join(notes), ha="left", va="top", fontsize=9.8,
        color="#333333", linespacing=1.75)

plt.subplots_adjust(left=0.015, right=0.985, top=0.995, bottom=0.005)
out = r"E:\deepseek\LX311_电机正反转\ladder_basic.png"
plt.savefig(out, dpi=170, facecolor="white", bbox_inches="tight", pad_inches=0.12)
print("saved", out)


