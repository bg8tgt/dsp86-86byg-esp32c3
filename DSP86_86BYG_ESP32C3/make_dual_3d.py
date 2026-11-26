# -*- coding: utf-8 -*-
"""双轴云台三种结构 3D 参考图（等轴测示意图）。"""
import math
from PIL import Image, ImageDraw, ImageFont

F = r"C:\Windows\Fonts\msyh.ttc"; FB = r"C:\Windows\Fonts\msyhbd.ttc"
def f(s,b=False): return ImageFont.truetype(FB if b else F,s)

W,H=2100,1200
img=Image.new("RGB",(W,H),"#ffffff"); d=ImageDraw.Draw(img)
t=lambda xy,s,sz=18,c="#1b2430",a="la",b=False: d.text(xy,s,font=f(sz,b),fill=c,anchor=a)

def make_proj(ox,oy,s):
    def P(x,y,z):
        return (ox+(x-y)*0.866*s, oy+((x+y)*0.5-z)*s)
    return P

def draw_box(P, cx,cy,cz,w,dd,h,base=(150,170,190)):
    x0,x1=cx-w/2,cx+w/2; y0,y1=cy-dd/2,cy+dd/2; z0,z1=cz-h/2,cz+h/2
    top=tuple(min(255,c+20) for c in base); side=tuple(max(0,c-25) for c in base)
    def poly(pts,fill):
        d.polygon([P(*p) for p in pts],fill=fill,outline="#37474f")
    poly([(x0,y1,z0),(x1,y1,z0),(x1,y1,z1),(x0,y1,z1)],base)
    poly([(x1,y0,z0),(x1,y1,z0),(x1,y1,z1),(x1,y0,z1)],side)
    poly([(x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)],top)

def cyl(P,cx,cy,z,r,h,fill=(120,144,156)):
    pts=[]
    for i in range(25):
        a=2*math.pi*i/24
        pts.append(P(cx+r*math.cos(a),cy+r*math.sin(a),z+h))
    d.polygon(pts,fill=tuple(min(255,c+15) for c in fill),outline="#37474f")
    # side
    left=P(cx-r,cy,z); right=P(cx+r,cy,z)
    d.line([left, P(cx-r,cy,z+h)], fill="#37474f", width=2)
    d.line([right, P(cx+r,cy,z+h)], fill="#37474f", width=2)
    d.line([P(cx-r,cy,z),P(cx+r,cy,z)], fill="#37474f", width=2)

def panel(ox, title, variant):
    P=make_proj(ox,720,0.8)
    t((ox,100),title,22,"#0d47a1","ma",True)
    # 底座
    draw_box(P,0,0,8,240,190,16,(176,190,207))
    # 方位电机 + 回转
    if variant=="A":
        draw_box(P,-95,55,55,64,64,70,(69,90,100))   # M1
        cyl(P,0,0,16,80,22)
        draw_box(P,-70,0,105,14,110,150,(144,164,184))
        draw_box(P,70,0,105,14,110,150,(144,164,184))
        draw_box(P,0,0,185,130,100,10,(255,183,77))
        draw_box(P,-92,0,190,60,72,72,(69,90,100))   # M2
        draw_box(P,92,0,190,28,40,30,(102,187,106))  # sensor
        draw_box(P,0,0,200,150,110,8,(120,144,156))
    elif variant=="B":
        draw_box(P,-80,55,55,64,64,70,(69,90,100))
        cyl(P,0,0,16,90,20)
        draw_box(P,-55,0,130,80,80,220,(144,164,184))
        draw_box(P,35,0,230,110,18,14,(144,164,184))
        draw_box(P,95,0,215,60,72,72,(69,90,100))
        draw_box(P,95,55,235,28,36,28,(102,187,106))
        draw_box(P,95,0,250,120,100,8,(255,183,77))
    else:
        draw_box(P,-95,55,50,64,64,60,(69,90,100))
        cyl(P,0,0,16,105,26,(120,144,156))
        draw_box(P,0,0,80,120,120,110,(144,164,184))
        draw_box(P,-105,0,105,52,64,52,(198,120,60))  # worm AZ
        draw_box(P,0,0,150,140,110,10,(255,183,77))
        draw_box(P,-92,0,175,60,72,72,(69,90,100))    # M2
        draw_box(P,90,0,170,28,40,30,(102,187,106))
        draw_box(P,0,0,165,150,110,8,(120,144,156))

t((W//2,26),"双轴云台 三种结构 3D 参考图（示意图，非加工图）",38,"#111","ma",True)
t((W//2,70),"A：U 形叉架　B：立柱 T 型　C：蜗轮重载；两台 86BYG450 + 两台 DSP86，行程俯仰 -30°～+70°",17,"#5b6570","ma")

panel(340,"A. U 形叉架（重心低、结构简单）","A")
panel(1030,"B. 立柱 T 型（方位 360°、视野好）","B")
panel(1720,"C. 蜗轮重载（自锁、抗风）","C")

# 图例
notes=[
"A：底座 + 回转支承 + 左右立柱 + 俯仰平台；M1 方位、M2 俯仰均 86 框。",
"B：单立柱 + 悬臂，俯仰轴在臂端；适合相机/测试仪，需配重。",
"C：方位/俯仰均用蜗轮蜗杆，断电自锁；适合大负载/户外。",
]
for i,s in enumerate(notes):
    d.text((60,900+i*30),s,font=f(15),fill="#37474f")

d.text((60,1000),"电气：ESP32-C3 + 2×DSP86，两台 86BYG450 独立驱动（原理图见图 10）；本图为结构参考，不是加工图。",font=f(15,True),fill="#c62828")

img.save(r"E:\deepseek\DSP86_86BYG_ESP32C3\06_dual_3d_variants.png")
print("saved")


