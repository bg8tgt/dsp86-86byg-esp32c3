# -*- coding: utf-8 -*-
"""双轴云台 3D 总装：底部方位电机 + 水平俯仰电机 + 后置同步双向齿轮箱。"""
import math
from PIL import Image, ImageDraw, ImageFont

F = r"C:\Windows\Fonts\msyh.ttc"; FB = r"C:\Windows\Fonts\msyhbd.ttc"
def f(s,b=False): return ImageFont.truetype(FB if b else F,s)

W,H=1700,1150
img=Image.new("RGB",(W,H),"#ffffff"); d=ImageDraw.Draw(img)
BLK="#1b2430"; RED="#c62828"; GRN="#2e7d32"; BLU="#1565c0"; ORG="#ef6c00"; YEL="#f9a825"

def t(xy,s,sz=16,c=BLK,a="la",b=False): d.text(xy,s,font=f(sz,b),fill=c,anchor=a)
OX,OY,S=560,760,1.35
def P(x,y,z): return (OX+(x-y)*0.866*S, OY+((x+y)*0.5-z)*S)

def box(cx,cy,cz,w,dd,h,base=(150,170,190),top=None,side=None):
    x0,x1=cx-w/2,cx+w/2; y0,y1=cy-dd/2,cy+dd/2; z0,z1=cz-h/2,cz+h/2
    if top is None: top=tuple(min(255,c+20) for c in base)
    if side is None: side=tuple(max(0,c-25) for c in base)
    def poly(pts,fill):
        d.polygon([P(*p) for p in pts],fill=fill,outline="#37474f")
    poly([(x0,y1,z0),(x1,y1,z0),(x1,y1,z1),(x0,y1,z1)],base)
    poly([(x1,y0,z0),(x1,y1,z0),(x1,y1,z1),(x1,y0,z1)],side)
    poly([(x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)],top)

def cylz(cx,cy,z,r,h,fill=(120,144,156)):
    pts=[P(cx+r*math.cos(2*math.pi*i/24),cy+r*math.sin(2*math.pi*i/24),z+h) for i in range(24)]
    d.polygon(pts,fill=tuple(min(255,c+15) for c in fill),outline="#37474f")

def shaftx(x0,x1,y,z,r=9):
    d.line([P(x0,y,z),P(x1,y,z)],fill=(176,190,207),width=int(r*1.8))
    d.line([P(x0,y,z),P(x1,y,z)],fill="#546e7a",width=2)

def dash(p0,p1,c=RED,w=3,n=20):
    for i in range(n):
        if i%2: continue
        a=i/n; b=(i+1)/n
        d.line([(p0[0]+(p1[0]-p0[0])*a,p0[1]+(p1[1]-p0[1])*a),
                (p0[0]+(p1[0]-p0[0])*b,p0[1]+(p1[1]-p0[1])*b)],fill=c,width=w)

def leader(a,b,lab,pos,c=BLK):
    d.line([a,b],fill=c,width=2)
    t(pos,lab,15,c)

t((W//2,26),"双轴云台 3D 总装（底部方位电机 + 水平俯仰电机 + 同步双向齿轮箱）",32,"#111","ma",True)
t((W//2,74),"方位电机垂直在底部；俯仰电机水平，轴线 ⊥ 方位轴；齿轮箱后置，左右同步输出",16,"#5b6570","ma")

# 底座
box(0,0,7,270,210,14,(176,190,207))
# 方位电机
box(-70,55,62,64,64,88,(69,90,100),(96,118,128),(52,70,80))
d.line([P(-70,55,106),P(-70,55,120)],fill=(176,190,207),width=16)
# 回转台
cylz(0,0,110,92,22,(120,144,156))
# 立柱
box(-92,0,210,16,110,180,(144,164,184),(170,188,205),(118,138,158))
box(92,0,210,16,110,180,(144,164,184),(170,188,205),(118,138,158))
# 齿轮箱（后置）
box(0,-110,282,96,66,66,(198,120,60),(224,150,90),(160,86,38))
# 俯仰电机（水平，后侧）
box(0,-188,282,70,70,70,(69,90,100),(96,118,128),(52,70,80))
d.line([P(0,-153,282),P(0,-110,282)],fill=(176,190,207),width=15)
# 同步输出轴
shaftx(-92,92,-110,282,9)
# 托架：后横梁 + 两侧臂 + 平台
box(0,-110,282,180,12,12,(255,183,77),(255,204,128),(230,150,45))
box(-72,-50,282,12,120,10,(255,183,77),(255,204,128),(230,150,45))
box(72,-50,282,12,120,10,(255,183,77),(255,204,128),(230,150,45))
box(0,30,288,150,100,8,(255,183,77),(255,204,128),(230,150,45))
box(0,30,296,120,80,6,(120,144,156),(150,172,184),(96,118,130))
# 传感器
box(112,-110,282,26,40,28,(102,187,106),(129,199,132),(76,155,80))

# 轴线
dash(P(0,0,-30),P(0,0,330))
dash(P(-160,-110,282),P(160,-110,282))

# 标注
leader((480,330),(P(-70,95,100)),"方位电机（底部，垂直）",(490,322),BLK)
leader((960,300),(P(0,-200,315)),"俯仰电机（水平，⊥方位轴）",(970,292),BLK)
leader((960,390),(P(40,-110,292)),"同步双向齿轮箱",(960,450),ORG)
leader((960,480),(P(60,30,300)),"负载平台",(970,472),BLK)
leader((960,570),(P(112,-90,282)),"俯仰角传感器",(970,562),GRN)
leader((300,880),(P(-92,40,210)),"U 形立柱",(310,872),BLK)
leader((450,970),(P(0,0,7)),"底座 270×210",(460,962),BLK)

# 轴向图
AX,AY=1330,520
t((AX,140),"轴向关系",22,"#111","ma",True)
d.line([(AX,AY),(AX,AY-210)],fill=RED,width=5)
d.polygon([(AX,AY-220),(AX-9,AY-198),(AX+9,AY-198)],fill=RED)
t((AX+14,AY-210),"方位轴（垂直）",15,RED)
d.line([(AX,AY),(AX+190,AY)],fill=BLU,width=5)
d.polygon([(AX+200,AY),(AX+178,AY-9),(AX+178,AY+9)],fill=BLU)
t((AX+55,AY+16),"俯仰轴（水平）",15,BLU)
d.line([(AX,AY),(AX-130,AY-95)],fill=ORG,width=5)
d.polygon([(AX-138,AY-100),(AX-120,AY-82),(AX-112,AY-98)],fill=ORG)
t((AX-330,AY-130),"俯仰电机轴",15,ORG)
t((AX-330,AY-106),"⊥ 方位轴",14,"#37474f")
d.arc([AX-34,AY-34,AX+34,AY+34],200,340,fill="#607d8b",width=2)
t((AX+24,AY-44),"90°",14,"#37474f")

notes=[
"1) 方位电机垂直装在底座，经回转支承/齿轮驱动云台旋转。",
"2) 俯仰电机水平后置，轴线与方位轴成 90°。",
"3) 同步双向齿轮箱左右输出，两侧耳轴同速，避免单侧驱动扭转变形。",
"4) 传感器装在任一输出端，用于 -30°～+70° 闭环。",
]
for i,s in enumerate(notes):
    d.text((70,1000+i*28),s,font=f(15),fill="#37474f")

img.save(r"E:\deepseek\DSP86_86BYG_ESP32C3\07_pitch_gearbox_3d.png")
print("saved")


