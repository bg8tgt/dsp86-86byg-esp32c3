# -*- coding: utf-8 -*-
"""生成俯仰云台 3D 示意图（PIL 等轴测）。"""
import math
from PIL import Image, ImageDraw, ImageFont

FONT = r"C:\Windows\Fonts\msyh.ttc"
FONTB = r"C:\Windows\Fonts\msyhbd.ttc"

def f(sz, bold=False):
    return ImageFont.truetype(FONTB if bold else FONT, sz)

W, H = 1600, 1000
img = Image.new("RGB", (W, H), "#ffffff")
d = ImageDraw.Draw(img)

OX, OY, S = 420, 540, 1.32

def P(x, y, z):
    return (OX + (x - y) * 0.866 * S,
            OY + ((x + y) * 0.5 - z) * S)

def poly(pts, fill, outline="#37474f", width=2):
    d.polygon(pts, fill=fill, outline=outline)
    d.line(pts + [pts[0]], fill=outline, width=width, joint="curve")

def box(cx, cy, cz, w, dd, h, base, top=None, side=None):
    x0, x1 = cx - w/2, cx + w/2
    y0, y1 = cy - dd/2, cy + dd/2
    z0, z1 = cz - h/2, cz + h/2
    top = top or tuple(min(255, c + 18) for c in base)
    side = side or tuple(max(0, c - 22) for c in base)
    # front (y1)
    poly([P(x0,y1,z0), P(x1,y1,z0), P(x1,y1,z1), P(x0,y1,z1)], base)
    # right (x1)
    poly([P(x1,y0,z0), P(x1,y1,z0), P(x1,y1,z1), P(x1,y0,z1)], side)
    # top
    poly([P(x0,y0,z1), P(x1,y0,z1), P(x1,y1,z1), P(x0,y1,z1)], top)

def label(s, xy, color="#1b2430", size=18, bold=False, anchor="la"):
    d.text(xy, s, font=f(size, bold), fill=color, anchor=anchor)

def leader(p1, p2, color="#607d8b"):
    d.line([p1, p2], fill=color, width=2)

# 标题
label("俯仰云台 3D 示意图（-30° 俯 ～ +70° 仰）", (W//2, 34), "#111111", 40, True, "ma")
label("示意图，非加工图；尺寸单位 mm，86 步进电机直驱俯仰轴", (W//2, 92), "#5b6570", 20, False, "ma")

# 底座
box(0, 0, 6, 240, 170, 12, (176, 190, 207), (205, 216, 228), (150, 164, 183))
# 地脚孔
for hx, hy in [(-100,-65),(100,-65),(-100,65),(100,65)]:
    px, py = P(hx, hy, 12)
    d.ellipse([px-7, py-4, px+7, py+4], fill="#8fa3b8", outline="#5b6b7c")

# 立柱
box(-95, 0, 81, 16, 120, 138, (144, 164, 184), (170, 188, 205), (118, 138, 158))
box(95, 0, 81, 16, 120, 138, (144, 164, 184), (170, 188, 205), (118, 138, 158))

# 电机（86 框）
box(-128, 0, 150, 64, 86, 86, (69, 90, 100), (96, 118, 128), (52, 70, 80))
# 电机轴
d.line([P(-96,0,150), P(-88,0,150)], fill="#b0bec5", width=10)
# 电机端盖圆
cx, cy = P(-160, 0, 150)
d.ellipse([cx-16, cy-28, cx+16, cy+28], fill="#37474f", outline="#263238")

# 云台U形架（俯仰部分）
box(0, 0, 150, 116, 108, 10, (255, 183, 77), (255, 204, 128), (230, 150, 45))
box(-54, 0, 178, 8, 96, 56, (255, 183, 77), (255, 204, 128), (230, 150, 45))
box(54, 0, 178, 8, 96, 56, (255, 183, 77), (255, 204, 128), (230, 150, 45))
# 负载平台
box(0, 0, 160, 140, 120, 8, (120, 144, 156), (150, 172, 184), (96, 118, 130))

# 传感器
box(112, 0, 135, 28, 42, 30, (102, 187, 106), (129, 199, 132), (76, 155, 80))

# 俯仰轴虚线
ax1, ax2 = P(-170,0,150), P(150,0,150)
n = 22
for i in range(n):
    if i % 2: continue
    t1, t2 = i/n, (i+1)/n
    d.line([(ax1[0]+(ax2[0]-ax1[0])*t1, ax1[1]+(ax2[1]-ax1[1])*t1),
            (ax1[0]+(ax2[0]-ax1[0])*t2, ax1[1]+(ax2[1]-ax1[1])*t2)],
           fill="#c62828", width=3)

# 标注
label("86 步进电机", (95, 150), "#37474f", 18, True)
leader((190, 168), P(-128, -43, 190))
label("俯仰轴", (655, 400), "#c62828", 18, True)
leader((640, 418), P(40, 0, 150))
label("负载平台", (585, 235), "#37474f", 18, True)
leader((620, 252), P(0, 55, 164))
label("俯仰角传感器", (640, 560), "#2e7d32", 18, True)
leader((675, 545), P(112, 20, 135))
label("U 形云台架", (300, 690), "#b45309", 18, True)
leader((360, 672), P(-40, 50, 150))
label("底座 240×170", (315, 805), "#37474f", 16)
leader((430, 790), P(-70, 80, 6))
label("轴高 150", (610, 760), "#37474f", 16)
leader((650, 745), P(20, 0, 150))

# 尺寸线
d.line([P(-120,-85,0), P(-120,-85,0)], fill="#90a4ae", width=1)

# ---------- 右侧角度图 ----------
PX, PY, R = 1235, 600, 185
d.line([PX-250, PY, PX+250, PY], fill="#b0bec5", width=2)
d.line([PX, PY-240, PX, PY+140], fill="#b0bec5", width=2)
label("0° 基准", (PX+190, PY+16), "#607d8b", 15)


def ray(angle, length, color, width=4):
    a = math.radians(angle)
    x = PX + math.cos(a)*length
    y = PY - math.sin(a)*length
    d.line([PX, PY, x, y], fill=color, width=width)
    return x, y

# -30 与 +70
ray(-30, R, "#c62828", 5)
ray(70, R, "#2e7d32", 5)
ray(0, R*0.82, "#90a4ae", 2)

# 圆弧
pts = []
for i in range(101):
    ang = -30 + (70+30)*i/100
    a = math.radians(ang)
    pts.append((PX + math.cos(a)*(R+18), PY - math.sin(a)*(R+18)))
d.line(pts, fill="#546e7a", width=3)

label("-30° 最大俯角", (PX+15, PY+185), "#c62828", 17, True, "ma")
label("+70° 最大仰角", (PX-15, PY-240), "#2e7d32", 17, True, "ma")
label("行程 100°", (PX+120, PY-20), "#37474f", 16)

# 平台示意（两个角度）
for ang, col in [(-30, "#c62828"), (70, "#2e7d32")]:
    a = math.radians(ang)
    x1 = PX + math.cos(a)*35; y1 = PY - math.sin(a)*35
    x2 = PX + math.cos(a)*(R-10); y2 = PY - math.sin(a)*(R-10)
    d.line([x1,y1,x2,y2], fill=col, width=9)
    d.ellipse([x2-7,y2-7,x2+7,y2+7], fill=col)

# 说明
notes = [
    "• 俯仰轴由 86 步进电机直驱，建议加减速 800–1600 步/s²，最大速度 ≤2000 Hz。",
    "• 机械硬限位建议放在 -32° / +72°，软件限位 -30° / +70°。",
    "• 传感器 0–3.3 V 接 GPIO3，两点校准后角度线性换算。",
]
for i, s in enumerate(notes):
    d.text((100, 900 + i*30), s, font=f(16), fill="#37474f", anchor="la")

img.save(r"E:\deepseek\DSP86_86BYG_ESP32C3\05_gimbal_3d.png")
print("saved")

