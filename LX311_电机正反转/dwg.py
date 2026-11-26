# -*- coding: utf-8 -*-
"""公共绘图工具：电气图元"""
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib import font_manager as fm
from matplotlib.patches import Rectangle, Circle, FancyArrow, Polygon

for f in (r"C:\Windows\Fonts\simhei.ttf", r"C:\Windows\Fonts\msyh.ttc"):
    try:
        fm.fontManager.addfont(f)
    except Exception:
        pass
plt.rcParams["font.family"] = ["SimHei", "Microsoft YaHei"]

K = "#111111"
BLUE = "#0b4f9c"
GREY = "#5f5f5f"
RED = "#b3261e"
LW = 1.8


def line(ax, pts, lw=LW, c=K, ls="-"):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    ax.plot(xs, ys, color=c, lw=lw, ls=ls, solid_capstyle="round",
            solid_joinstyle="miter")


def dot(ax, x, y, r=0.055, c=K):
    ax.add_patch(Circle((x, y), r, color=c, zorder=5))


def term(ax, x, y, r=0.075, c=K):
    ax.add_patch(Circle((x, y), r, facecolor="white", edgecolor=c, lw=1.4, zorder=6))


def txt(ax, x, y, s, size=10.5, c=K, ha="center", va="center", rot=0, style="normal"):
    ax.text(x, y, s, fontsize=size, color=c, ha=ha, va=va, rotation=rot,
            style=style)


def box(ax, x, y, w, h, label="", size=11, lw=1.8, fc="white", ec=K):
    ax.add_patch(Rectangle((x, y), w, h, facecolor=fc, edgecolor=ec, lw=lw, zorder=3))
    if label:
        txt(ax, x + w / 2, y + h / 2, label, size)


def earth(ax, x, y, s=0.16):
    line(ax, [(x, y), (x, y - s * 0.45)])
    line(ax, [(x - s, y - s * 0.45), (x + s, y - s * 0.45)])
    line(ax, [(x - s * 0.62, y - s * 0.75), (x + s * 0.62, y - s * 0.75)])
    line(ax, [(x - s * 0.3, y - s * 1.05), (x + s * 0.3, y - s * 1.05)])


def contact_h(ax, x, y, w=0.46, kind="no", label="", lsize=10, h=0.17):
    """水平导线上的触点；kind: no / nc / sw（隔离开关）"""
    g = 0.16
    line(ax, [(x - w / 2, y), (x - g, y)])
    line(ax, [(x + g, y), (x + w / 2, y)])
    pass
    if kind == "no":
        line(ax, [(x - g, y - h), (x - g, y + h)])
        line(ax, [(x + g, y - h), (x + g, y + h)])
    elif kind == "nc":
        line(ax, [(x - g, y - h), (x - g, y + h)])
        line(ax, [(x + g, y - h), (x + g, y + h)])
        line(ax, [(x - g - 0.10, y + h + 0.07), (x + g + 0.10, y - h - 0.07)])
    else:  # 开关/断路器：斜线 + 转轴圆点
        line(ax, [(x - g, y), (x + g + 0.14, y + 0.30)])
        ax.add_patch(Circle((x - g, y), 0.035, color=K, zorder=6))
    if label:
        txt(ax, x, y + h + 0.16, label, lsize)


def coil_h(ax, x, y, w=0.55, h=0.34, label="", lsize=10):
    ax.add_patch(Rectangle((x - w / 2, y - h / 2), w, h, fill=False, ec=K, lw=LW, zorder=4))
    line(ax, [(x - w / 2 - 0.22, y), (x - w / 2, y)])
    line(ax, [(x + w / 2, y), (x + w / 2 + 0.22, y)])
    if label:
        txt(ax, x, y + h + 0.16, label, lsize)


def heater_h(ax, x, y, w=0.44, h=0.30):
    ax.add_patch(Rectangle((x - w / 2, y - h / 2), w, h, fill=False, ec=K, lw=LW, zorder=4))


def diode(ax, x, y, size=0.11, vert=True):
    """二极管（竖直放置，导通方向向上）"""
    if vert:
        ax.add_patch(Polygon([[x - size, y - size], [x + size, y - size],
                              [x, y + size * 0.7]], closed=True,
                             facecolor="white", edgecolor=K, lw=1.4, zorder=6))
        line(ax, [(x - size, y + size * 0.7), (x + size, y + size * 0.7)], lw=1.6)
    else:
        ax.add_patch(Polygon([[x + size, y - size], [x + size, y + size],
                              [x - size * 0.7, y]], closed=True,
                             facecolor="white", edgecolor=K, lw=1.4, zorder=6))
        line(ax, [(x - size * 0.7, y - size), (x - size * 0.7, y + size)], lw=1.6)


def title_block(ax, x, y, w, h, rows):
    ax.add_patch(Rectangle((x, y), w, h, fill=False, ec=K, lw=1.4))
    n = len(rows)
    for i, (k, v) in enumerate(rows):
        yy = y + h - (i + 0.5) * h / n
        line(ax, [(x, y + h - (i + 1) * h / n), (x + w, y + h - (i + 1) * h / n)],
             lw=0.8, c=GREY)
        txt(ax, x + 0.12, yy, k, 8.5, GREY, ha="left")
        txt(ax, x + 1.05, yy, v, 8.8, K, ha="left")


def new_fig(w, h):
    fig, ax = plt.subplots(figsize=(w, h))
    ax.set_xlim(0, w)
    ax.set_ylim(0, h)
    ax.axis("off")
    ax.set_aspect("equal")
    return fig, ax

