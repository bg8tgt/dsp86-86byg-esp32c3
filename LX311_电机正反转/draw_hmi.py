# -*- coding: utf-8 -*-
"""威纶通 HMI 组态画面稿 v2：深色 + 3D 悬浮按钮（800×480）"""
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, Rectangle, Circle
from matplotlib import font_manager as fm
from matplotlib.colors import to_rgb

for f in (r"C:\Windows\Fonts\simhei.ttf", r"C:\Windows\Fonts\msyh.ttc"):
    try:
        fm.fontManager.addfont(f)
    except Exception:
        pass
plt.rcParams["font.family"] = ["SimHei", "Microsoft YaHei"]

W, H = 800, 480
fig, ax = plt.subplots(figsize=(8, 4.8))
ax.set_xlim(0, W)
ax.set_ylim(0, H)
ax.axis("off")
fig.subplots_adjust(left=0, right=1, top=1, bottom=0)

INK = "#e8eef7"
MUTE = "#8fa0b8"

GREEN = ("#3ee08a", "#12a35c")
BLUE = ("#5aa9ff", "#1f5fd0")
RED = ("#ff7b6e", "#c62828")
AMBER = ("#ffc65c", "#cf7d0a")


def mix(c1, c2, t):
    a, b = np.array(to_rgb(c1)), np.array(to_rgb(c2))
    return tuple(a + (b - a) * t)


def grad_round(x, y, w, h, r, c1, c2, z=6, alpha=1.0):
    """圆角矩形竖直渐变：c1 顶部色，c2 底部色"""
    clip = FancyBboxPatch((x, y), w, h,
                          boxstyle=f"round,pad=0,rounding_size={r}",
                          facecolor="none", edgecolor="none", zorder=z)
    ax.add_patch(clip)
    n = 90
    for i in range(n):
        t = i / (n - 1)
        col = mix(c2, c1, t)
        rr = Rectangle((x, y + h * t), w, h / n * 1.7,
                       facecolor=col, edgecolor="none", alpha=alpha, zorder=z)
        rr.set_clip_path(clip)
        ax.add_patch(rr)
    return clip


def shadow(x, y, w, h, r, layers=5, spread=7, dy=-5, alpha=0.30):
    for i in range(layers, 0, -1):
        k = i / layers
        ax.add_patch(FancyBboxPatch((x - spread * k * 0.5, y + dy - spread * k * 0.45),
                                    w + spread * k, h + spread * k,
                                    boxstyle=f"round,pad=0,rounding_size={r + k * 4}",
                                    facecolor="black", edgecolor="none",
                                    alpha=alpha * (0.16 + 0.10 * (1 - k)), zorder=2))


def txt(x, y, s, size, c=INK, ha="left", va="center", weight="normal", alpha=1.0):
    ax.text(x, y, s, fontsize=size, color=c, ha=ha, va=va,
            fontweight=weight, alpha=alpha, zorder=20)


def btn3d(x, y, w, h, label, colors, glow=False, size=14.5):
    r = 14
    if glow:
        for k, a in ((16, 0.09), (10, 0.13), (5, 0.18)):
            ax.add_patch(FancyBboxPatch((x - k / 2, y - k / 2), w + k, h + k,
                                        boxstyle=f"round,pad=0,rounding_size={r + k / 2}",
                                        facecolor=colors[0], edgecolor="none",
                                        alpha=a, zorder=3))
    shadow(x, y, w, h, r)
    grad_round(x, y, w, h, r, colors[0], colors[1], z=6)
    ax.add_patch(FancyBboxPatch((x + 7, y + h - 15), w - 14, 10,
                                boxstyle="round,pad=0,rounding_size=5",
                                facecolor="white", edgecolor="none", alpha=0.20, zorder=7))
    ax.add_patch(FancyBboxPatch((x + 6, y + 5), w - 12, 9,
                                boxstyle="round,pad=0,rounding_size=4",
                                facecolor="black", edgecolor="none", alpha=0.20, zorder=7))
    ax.add_patch(FancyBboxPatch((x, y), w, h,
                                boxstyle=f"round,pad=0,rounding_size={r}",
                                facecolor="none", edgecolor="white", lw=0.8,
                                alpha=0.20, zorder=8))
    ax.text(x + w / 2, y + h / 2 - 1, label, fontsize=size, color="black",
            ha="center", va="center", fontweight="bold", alpha=0.30, zorder=20)
    ax.text(x + w / 2, y + h / 2, label, fontsize=size, color="white",
            ha="center", va="center", fontweight="bold", zorder=21)


def card(x, y, w, h, title):
    shadow(x, y, w, h, 18, layers=4, spread=6, dy=-4, alpha=0.35)
    grad_round(x, y, w, h, 18, "#17253d", "#0e1828", z=4)
    ax.add_patch(FancyBboxPatch((x, y), w, h,
                                boxstyle="round,pad=0,rounding_size=18",
                                facecolor="none", edgecolor="#2b3d5c", lw=1.0, zorder=5))
    txt(x + 16, y + h - 20, title, 11, MUTE)


def lamp(cx, cy, r, color, on):
    if on:
        for k, a in ((3.4, 0.10), (2.3, 0.16), (1.5, 0.26)):
            ax.add_patch(Circle((cx, cy), r * k, facecolor=color, edgecolor="none",
                                alpha=a, zorder=6))
        grad_round(cx - r, cy - r, 2 * r, 2 * r, r, mix(color, "white", 0.55), color, z=8)
    else:
        grad_round(cx - r, cy - r, 2 * r, 2 * r, r, "#34445f", "#1c2941", z=8)
    ax.add_patch(Circle((cx, cy), r, facecolor="none", edgecolor="black",
                        alpha=0.25, lw=1, zorder=9))


# ---------------- 背景 ----------------
grad = np.zeros((256, 1, 3))
top, bot = np.array(to_rgb("#0a1424")), np.array(to_rgb("#1c2f4d"))
for i in range(256):
    grad[i, 0] = top + (bot - top) * (i / 255) ** 1.25
ax.imshow(grad, extent=(0, W, 0, H), aspect="auto", zorder=0, origin="lower")
for (gx, gy, gr, gc, ga) in ((640, 440, 250, "#1f5fd0", 0.12),
                             (110, 40, 210, "#12a35c", 0.07)):
    for k in range(26, 0, -1):
        ax.add_patch(Circle((gx, gy), gr * k / 26, facecolor=gc, edgecolor="none",
                            alpha=ga * (1 - k / 26) ** 2, zorder=1))

# ---------------- 标题栏 ----------------
ax.add_patch(Rectangle((0, 414), W, 66, facecolor="#0b1728", edgecolor="none",
                       alpha=0.90, zorder=3))
ax.plot([0, W], [414, 414], color="#2b4a72", lw=1, zorder=6)
txt(22, 452, "LX311  电机正反转控制", 19, "#ffffff", weight="bold")
txt(22, 430, "Hollysys LX-CU311   ·   Modbus TCP   ·   800×480", 9, "#7f9dc0")

lamp(586, 452, 4.5, "#3ee08a", True)
txt(597, 452, "通讯正常", 9.5, "#a9d6bf")
txt(690, 452, "2026-10-20  12:06", 9.5, "#7f9dc0")

# ---------------- 卡片 ----------------
card(14, 118, 232, 284, "电机状态")
card(254, 118, 268, 284, "运行数据")
card(530, 118, 256, 284, "操作按钮")

# 电机
cx, cy = 82, 306
ax.add_patch(Circle((cx, cy), 40, facecolor="#1b2b45", edgecolor="none", alpha=0.45, zorder=4))
ax.add_patch(Circle((cx, cy), 36, facecolor="#0c1626", edgecolor="none", zorder=5))
ax.add_patch(Circle((cx, cy), 30, facecolor="#12203a", edgecolor="none", zorder=6))
ax.add_patch(Circle((cx, cy), 36, facecolor="none", edgecolor="#3c5a86", lw=1.6, zorder=9))
txt(cx, cy + 9, "M", 21, "#cfe0f5", ha="center", weight="bold")
txt(cx, cy - 13, "3~", 11, "#7f9dc0", ha="center")

# 转向箭头
ax.annotate("", xy=(cx + 63, cy + 3), xytext=(cx + 44, cy + 3),
            arrowprops=dict(arrowstyle="-|>", color="#3ee08a", lw=2.8,
                            mutation_scale=16), zorder=10)
ax.annotate("", xy=(cx - 63, cy + 3), xytext=(cx - 44, cy + 3),
            arrowprops=dict(arrowstyle="-|>", color="#3d4f6c", lw=2.8,
                            mutation_scale=16), zorder=10)



# 状态灯
for name, col, y, on in (("正转运行", "#3ee08a", 370, True),
                         ("反转运行", "#5aa9ff", 334, False),
                         ("故障", "#ff6b5e", 298, False),
                         ("运行中", "#39d2c0", 262, True)):
    lamp(158, y, 7, col, on)
    txt(172, y, name, 10.5, INK if on else "#65789a")

txt(30, 194, "当前方向", 9.5, MUTE)
txt(30, 170, "正转", 19, "#6ff0aa", weight="bold")

# ---------------- 数据 ----------------
rows = [("本次运行", "00 : 12 : 36", "#e8eef7"),
        ("累计运行", "08 : 42", "#e8eef7"),
        ("累计启动", "128  次", "#e8eef7"),
        ("故障代码", "E00   无故障", "#6ff0aa")]
yy = 358
for lab, val, col in rows:
    txt(270, yy, lab, 9.5, MUTE)
    txt(270, yy - 24, val, 16, col, weight="bold")
    yy -= 62

# ---------------- 按钮 ----------------
btn3d(546, 272, 108, 100, "正转启动", GREEN, glow=True, size=13.5)
btn3d(664, 272, 108, 100, "反转启动", BLUE, size=13.5)
btn3d(546, 150, 108, 100, "停止", RED, size=15)
btn3d(664, 150, 108, 100, "故障复位", AMBER, size=13.5)

# ---------------- 报警条 ----------------
shadow(14, 62, 772, 46, 12, layers=3, spread=5, dy=-3, alpha=0.28)
grad_round(14, 62, 772, 46, 12, "#16253c", "#101a2c", z=5)
ax.add_patch(FancyBboxPatch((14, 62), 772, 46, boxstyle="round,pad=0,rounding_size=12",
                            facecolor="none", edgecolor="#2b3d5c", lw=1, zorder=6))
ax.add_patch(Circle((40, 85), 9, facecolor="#12a35c", edgecolor="none", alpha=0.25, zorder=7))
ax.plot([35, 38.5, 45], [84, 81, 89], color="#3ee08a", lw=2.0, solid_capstyle="round", zorder=21)
txt(58, 85, "系统正常 · 无未确认报警", 11, "#cfe0f5")
txt(770, 85, "报警记录 ›", 9.5, "#7f9dc0", ha="right")

# ---------------- 底部导航 ----------------
navs = [("主画面", True), ("手动/自动", False), ("报警记录", False), ("参数", False)]
x = 14
for name, active in navs:
    w = 116
    if active:
        btn3d(x, 10, w, 40, name, ("#2f5688", "#172c4b"), size=10.5)
    else:
        shadow(x, 10, w, 40, 10, layers=3, spread=4, dy=-3, alpha=0.25)
        grad_round(x, 10, w, 40, 10, "#1b2942", "#121d30", z=6)
        ax.add_patch(FancyBboxPatch((x, 10), w, 40, boxstyle="round,pad=0,rounding_size=10",
                                    facecolor="none", edgecolor="#2b3d5c", lw=1, zorder=7))
        txt(x + w / 2, 30, name, 10.5, "#a9bcd6", ha="center")
    x += w + 10

txt(778, 30, "MT8071iE", 9, "#5d7191", ha="right")

plt.savefig(r"E:\deepseek\LX311_电机正反转\图4_HMI组态画面.png", dpi=250,
            facecolor="#0a1424", bbox_inches="tight", pad_inches=0.0)
print("ok")



