# -*- coding: utf-8 -*-
"""双轴系统——电源原理图。"""
from PIL import Image, ImageDraw, ImageFont

F = r"C:\Windows\Fonts\msyh.ttc"
FB = r"C:\Windows\Fonts\msyhbd.ttc"
def f(s, b=False): return ImageFont.truetype(FB if b else F, s)

W, H = 1800, 1150
img = Image.new("RGB", (W, H), "#ffffff")
d = ImageDraw.Draw(img)

def t(xy, s, size=20, color="#1b2430", anchor="la", bold=False):
    d.text(xy, s, font=f(size, bold), fill=color, anchor=anchor)

def box(r, fill, outline="#37474f", w=2):
    d.rounded_rectangle(r, radius=8, fill=fill, outline=outline, width=w)

def line(pts, color, w=4):
    d.line(pts, fill=color, width=w, joint="curve")

def dot(p, color, r=7):
    d.ellipse([p[0]-r,p[1]-r,p[0]+r,p[1]+r], fill=color)

def fuse(r, label):
    box(r, "#fff8e1", "#ef6c00", 2)
    x0,y0,x1,y1 = r
    d.line([x0,(y0+y1)//2,x1,(y0+y1)//2], fill="#ef6c00", width=3)
    t(((x0+x1)//2, y0-8), label, 16, "#b45309", "mb", True)

def pe(p):
    d.line([p[0],p[1],p[0],p[1]+18], fill="#2e7d32", width=4)
    d.line([p[0]-10,p[1]+18,p[0]+10,p[1]+18], fill="#2e7d32", width=4)
    d.line([p[0]-6,p[1]+24,p[0]+6,p[1]+24], fill="#2e7d32", width=4)

RED="#c62828"; BLK="#1b2430"; GRN="#2e7d32"; BLU="#1565c0"

t((W//2, 28), "双轴云台 电源原理图（220VAC → 24VAC → 2×DSP86）", 38, "#111", "ma", True)
t((W//2, 82), "QF1 2P C10 · FU1 5A · SB1 急停 · T1 600VA · FU2 25A · FU3/FU4 10A", 20, "#5b6570", "ma")

# 输入
box((60,180,200,320), "#fdecea", RED)
t((130,210), "220 VAC", 20, RED, "ma", True)
t((130,245), "L / N / PE", 18, "#7f1d1d", "ma")
line([(200,210),(250,210)], RED)
line([(200,250),(250,250)], BLK)
line([(200,290),(200,940)], GRN)
pe((200,940))

# QF1
box((250,170,390,300), "#fff3e0", "#ef6c00")
t((320,195), "QF1", 20, "#b45309", "ma", True)
t((320,225), "2P C10", 17, "#7c4a03", "ma")
t((320,258), "断路器", 16, "#7c4a03", "ma")
line([(390,210),(430,210)], RED)
line([(390,250),(430,250)], BLK)

# FU1
fuse((430,188,510,232), "FU1 5A")
line([(510,210),(550,210)], RED)
line([(430,250),(550,250)], BLK)

# 急停
box((550,175,690,300), "#fdecea", RED)
t((620,200), "SB1", 20, RED, "ma", True)
t((620,232), "急停", 18, "#7f1d1d", "ma")
t((620,262), "常闭", 15, "#7f1d1d", "ma")
line([(690,210),(730,210)], RED)
line([(690,250),(730,250)], BLK)

# 变压器
box((730,150,980,380), "#e3f2fd", BLU)
t((855,175), "T1 隔离变压器", 20, "#0d47a1", "ma", True)
t((855,208), "220 V / 24 VAC", 18, "#0d47a1", "ma")
t((855,238), "≥600 VA（25 A）", 17, "#0d47a1", "ma")
t((855,285), "一次侧        二次侧", 15, "#37474f", "ma")
line([(760,210),(760,250)], RED, 3)
line([(950,210),(950,250)], RED, 3)
line([(950,330),(950,370)], BLK, 3)
pe((855,380))

# 二次侧
line([(980,200),(1030,200)], RED)
fuse((1030,178,1110,222), "FU2 25A")
line([(1110,200),(1160,200)], RED)
dot((1160,200), RED)

# 24V+ 母线
line([(1160,200),(1160,620)], RED, 5)
for y in (300,560):
    dot((1160,y), RED)
    line([(1160,y),(1190,y)], RED)
    fuse((1190,y-22,1270,y+22), "FU3 10A" if y==300 else "FU4 10A")
    line([(1270,y),(1310,y)], RED)

# 24V- 母线
line([(950,330),(1120,330)], BLK, 5)
line([(1120,330),(1120,640)], BLK, 5)
line([(1120,340),(1310,340)], BLK, 4)
line([(1120,600),(1310,600)], BLK, 4)
dot((1120,340), BLK); dot((1120,600), BLK)

# 驱动器
for i, (y0, name) in enumerate([(260,"DSP86 #1  方位 AZ"), (520,"DSP86 #2  俯仰 EL")], 1):
    box((1310,y0,1600,y0+160), "#f3f4f6", "#111", 3)
    t((1455,y0+22), name, 19, "#111", "ma", True)
    t((1330,y0+62), "AC  AC", 16, "#37474f")
    t((1330,y0+112), "A+ A− B+ B−", 16, "#37474f")
    t((1585,y0+140), f"DRV{i}", 15, "#607d8b", "ra")
    # 端子
    for yy in (y0+45, y0+75):
        d.rectangle((1310,yy-6,1326,yy+6), fill="#ef5350", outline="#111")
    for j,yy in enumerate((y0+100,y0+115,y0+130,y0+145)):
        d.rectangle((1584,yy-5,1600,yy+5), fill="#66bb6a", outline="#111")
    pe((1455,y0+160))

# 电机
for i,(y0,name) in enumerate([(280,"M1 方位电机"),(540,"M2 俯仰电机")],1):
    box((1660,y0,1780,y0+130), "#e9f7ee", GRN)
    t((1720,y0+22), name, 17, "#1b5e20", "ma", True)
    t((1720,y0+58), "86BYG450", 15, "#2e7d32", "ma")
    t((1720,y0+88), "4 线", 14, "#2e7d32", "ma")
    pe((1720,y0+130))

# 电机线（示意颜色）
mcolors=[BLU,"#f9a825",RED,GRN]
for i,(y0) in enumerate([280,540]):
    for j,yy in enumerate((y0+100,y0+115,y0+130,y0+145)):
        line([(1600,yy),(1660,yy)], mcolors[j], 4)

# PE 母线
line([(80,940),(1780,940)], GRN, 5)
pe((855,380)); pe((1455,420)); pe((1455,680)); pe((1720,410)); pe((1720,670))

t((80,980), "PE 保护地：变压器铁芯、两台驱动器外壳、两台电机外壳、电控箱体、PE 排", 17, "#2e7d32")

t((80,1040), "注：1) 二次侧 24VAC 不分极性；2) 两台驱动器各自 10A 慢断；3) 急停切一次侧 220V；4) 电机线禁止带电插拔。", 16, "#37474f")
t((80,1075), "5) 24VAC 母线建议 4 mm²，驱动器分支 2.5 mm²；6) 变压器容量按两台同时 4.86A 选 ≥600VA。", 16, "#37474f")
t((80,1112), "控制：ESP32-C3 的 GPIO4/5/6 接 #1、GPIO7/10/0 接 #2（含 5V/限位/传感器，详见图 10）。", 16, "#1565c0")

img.save(r"E:\deepseek\DSP86_86BYG_ESP32C3\09_power_schematic.png")
print("saved")

