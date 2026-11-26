# -*- coding: utf-8 -*-
"""导出 HMI 按钮素材：4 个按钮 × 正常/按下，透明背景 PNG（2 倍尺寸 216×200）"""
import os
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, Rectangle
from matplotlib import font_manager as fm
from matplotlib.colors import to_rgb

for f in (r"C:\Windows\Fonts\simhei.ttf", r"C:\Windows\Fonts\msyh.ttc"):
    try:
        fm.fontManager.addfont(f)
    except Exception:
        pass
plt.rcParams["font.family"] = ["SimHei", "Microsoft YaHei"]

OUT = r"E:\deepseek\LX311_电机正反转\hmi_buttons"
os.makedirs(OUT, exist_ok=True)

GREEN = ("#3ee08a", "#12a35c")
BLUE = ("#5aa9ff", "#1f5fd0")
RED = ("#ff7b6e", "#c62828")
AMBER = ("#ffc65c", "#cf7d0a")


def mix(c1, c2, t):
    a, b = np.array(to_rgb(c1)), np.array(to_rgb(c2))
    return tuple(a + (b - a) * t)


def draw(path, label, colors, pressed=False, size=13.5):
    W, H = 120.0, 112.0
    fig, ax = plt.subplots(figsize=(W / 100, H / 100), dpi=200)
    ax.set_xlim(0, W)
    ax.set_ylim(0, H)
    ax.axis("off")
    fig.subplots_adjust(left=0, right=1, top=1, bottom=0)

    r = 14
    x, y, w, h = 8, 13, 104, 92
    if pressed:
        y += 1.5
        h -= 1.5
        colors = (mix(colors[0], "#000000", 0.16), mix(colors[1], "#000000", 0.16))

    # 投影
    drop = 3 if pressed else 5
    for i in range(4, 0, -1):
        k = i / 4
        ax.add_patch(FancyBboxPatch((x - 3 * k, y - drop - 4 * k), w + 6 * k, h + 6 * k,
                                    boxstyle=f"round,pad=0,rounding_size={r + 4 * k}",
                                    facecolor="black", edgecolor="none",
                                    alpha=0.10 * (1 - k) + 0.05, zorder=1))

    # 渐变主体
    clip = FancyBboxPatch((x, y), w, h, boxstyle=f"round,pad=0,rounding_size={r}",
                          facecolor="none", edgecolor="none", zorder=3)
    ax.add_patch(clip)
    n = 80
    for i in range(n):
        t = i / (n - 1)
        rr = Rectangle((x, y + h * t), w, h / n * 1.7,
                       facecolor=mix(colors[1], colors[0], t), edgecolor="none", zorder=3)
        rr.set_clip_path(clip)
        ax.add_patch(rr)

    if not pressed:
        ax.add_patch(FancyBboxPatch((x + 7, y + h - 15), w - 14, 10,
                                    boxstyle="round,pad=0,rounding_size=5",
                                    facecolor="white", edgecolor="none", alpha=0.20, zorder=4))
    ax.add_patch(FancyBboxPatch((x + 6, y + 5), w - 12, 9,
                                boxstyle="round,pad=0,rounding_size=4",
                                facecolor="black", edgecolor="none",
                                alpha=0.24 if pressed else 0.18, zorder=4))
    ax.add_patch(FancyBboxPatch((x, y), w, h, boxstyle=f"round,pad=0,rounding_size={r}",
                                facecolor="none", edgecolor="white", lw=0.8,
                                alpha=0.20, zorder=5))

    ax.text(x + w / 2, y + h / 2 - 2, label, fontsize=size, color="black",
            ha="center", va="center", fontweight="bold", alpha=0.30, zorder=7)
    ax.text(x + w / 2, y + h / 2 - 1, label, fontsize=size, color="white",
            ha="center", va="center", fontweight="bold", zorder=8)

    fig.savefig(path, dpi=200, transparent=True,
                bbox_inches=None, pad_inches=0, facecolor="none")
    plt.close(fig)


items = [("btn_fwd", "正转启动", GREEN, 13.5),
         ("btn_rev", "反转启动", BLUE, 13.5),
         ("btn_stop", "停止", RED, 15.0),
         ("btn_reset", "故障复位", AMBER, 13.5)]

for name, label, col, size in items:
    draw(os.path.join(OUT, f"{name}_normal.png"), label, col, False, size)
    draw(os.path.join(OUT, f"{name}_press.png"), label, col, True, size)

print("saved", len(items) * 2, "files to", OUT)

