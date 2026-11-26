# -*- coding: utf-8 -*-
"""双轴云台 3D 总装示意：底部方位电机 + 水平俯仰电机 + 同步双向齿轮箱。"""
import math
from PIL import Image, ImageDraw, ImageFont

F = r"C:\Windows\Fonts\msyh.ttc"; FB = r"C:\Windows\Fonts\msyhbd.ttc"
def f(s,b=False): return ImageFont.truetype(FB if b else F,s)

W,H=1700,1150
img=Image.new("RGB",(W,H),"#ffffff"); d=ImageDraw.Draw(img)
BLK="#1b2430"; RED="#c62828"; GRN="#2e7d32"; BLU="#1565c0"; ORG="#ef6c00"

def t(xy,s,sz=17,c=BLK,a="la",b=False): d.text(xy,s,font=f(sz,b),fill=c,anchor=a)
OX,OY,S=620,780,1.05
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

def cyl_z(cx,cy,z,r,h,fill=(120,144,156)):
    pts=[P(cx+r*math.cos(2*math.pi*i/24),cy+r*math.sin(2*math.pi*i/24),z+h) for i in range(24)]
    d.polygon(pts,fill=tuple(min(255,c+15) for c in fill),outline="#37474f")

def shaft_x(x0,x1,y,z,r=10,c=(176,190,207)):
    p0=P(x0,y,z); p1=P(x1,y,z)
    d.line([p0,p1],fill=c,width=int(r*2*0.9))
    d.line([p0,p1],fill="#546e7a",width=2)

t((W//2,26),"双轴云台 3D 总装示意（底部方位电机 + 水平俯仰电机 + 同步双向齿轮箱）",34,"#111","ma",True)
t((W//2,76),"方位电机垂直布置在底部；俯仰电机水平布置，轴线垂直于方位轴；齿轮箱左右同步输出",17,"#5b6570","ma")

# 底座
box(0,0,7,270,210,14,(176,190,207))
# 方位电机（底部，垂直）
box(-60,60,62,64,64,90,(69,90,100),(96,118,128),(52,70,80))
shaft_x(-60,-60,60,0)  # placeholder
d.line([P(-60,60,107),P(-60,60,135)],fill=(176,190,207),width=16)
# 回转台
cyl_z(0,0,120,88,24,(120,144,156))
# 立柱
box(-84,0,225,16,110,190,(144,164,184),(170,188,205),(118,138,158))
box(84,0,225,16,110,190,(144,164,184),(170,188,205),(118,138,158))
# 齿轮箱（后侧中心）
box(0,-48,265,96,70,72,(198,120,60),(224,150,90),(160,86,38))
# 俯仰电机（水平，后侧，轴向 Y）
box(0,-118,265,76,76,76,(69,90,100),(96,118,128),(52,70,80))
d.line([P(0,-80,265),P(0,-48,265)],fill=(176,190,207),width=16)
# 左右同步输出轴
shaft_x(-84,84,0,265,12,(176,190,207))
# 负载平台
box(0,10,300,150,110,10,(255,183,77),(255,204,128),(230,150,45))
# 传感器（右侧输出端）
box(112,0,265,28,42,30,(102,187,106),(129,199,132),(76,155,80))

# 轴线
def dash(p0,p1,c=RED,w=3,n=18):
    for i in range(n):
        if i%2: continue
        a=i/n; b=(i+1)/n
        d.line([(p0[0]+(p1[0]-p0[0])*a,p0[1]+(p1[1]-p0[1])*a),
                (p0[0]+(p1[0]-p0[0])*b,p0[1]+(p1[1]-p0[1])*b)],fill=c,width=w)

dash(P(0,0,-40),P(0,0,340))           # 方位轴
dash(P(-140,0,265),P(150,0,265))      # 俯仰轴

# 标注
def leader(a,b,c=BLK,lab="",pos=None):
    d.line([a,b],fill=c,width=2)
    if lab: t(pos or b,lab,16,c)

leader((120,300),(P(-60,92,100)),BLK,"方位电机（底部，垂直）",(130,292))
leader((1030,330),(P(0,-78,290)),BLK,"俯仰电机（水平，⊥方位轴）",(1035,322))
leader((1000,520),(P(48,-48,270)),ORG,"同步双向齿轮箱",(1005,512))
leader((910,700),(P(0,70,300)),BLK,"负载平台",(915,692))
leader((880,860),(P(112,20,265)),GRN,"俯仰角传感器",(885,852))
leader((260,880),(P(-84,55,225)),BLK,"U 形立柱",(265,872))
leader((480,980),(P(0,0,7)),BLK,"底座 270×210",(485,972))

# 右侧轴向图
AX,AY=1320,560
t((AX,150),"轴向关系",22,"#111","ma",True)
d.line([(AX,AY),(AX,AY-230)],fill=RED,width=5)
d.polygon([(AX,AY-240),(AX-9,AY-218),(AX+9,AY-218)],fill=RED)
t((AX+16,AY-230),"方位轴（垂直）",16,RED)
d.line([(AX,AY),(AX+220,AY)],fill=BLU,width=5)
d.polygon([(AX+230,AY),(AX+208,AY-9),(AX+208,AY+9)],fill=BLU)
t((AX+80,AY+18),"俯仰轴（水平）",16,BLU)
d.line([(AX,AY),(AX-150,AY-110)],fill=ORG,width=5)
d.polygon([(AX-158,AY-116),(AX-140,AY-96),(AX-132,AY-112)],fill=ORG)
t((AX-360,AY-150),"俯仰电机轴",16,ORG)
t((AX-360,AY-126),"垂直于方位轴",14,"#37474f")
d.arc([AX-40,AY-40,AX+40,AY+40],200,340,fill="#607d8b",width=2)
t((AX+28,AY-52),"90°",15,"#37474f")

# 说明
notes=[
"1) 方位电机垂直安装在底座，经回转支承/齿轮驱动整个云台旋转。",
"2) 俯仰电机水平安装，轴线与方位轴成 90°；经同步双向齿轮箱左右输出。",
"3) 左右输出同速反向/同向由齿轮箱决定，保证两侧耳轴同步、无附加扭矩。",
"4) 俯仰传感器装在任一输出端，用于 -30°～+70° 闭环。",
]
for i,s in enumerate(notes):
    d.text((70,1010+i*28),s,font=f(15),fill="#37474f")

img.save(r"E:\deepseek\DSP86_86BYG_ESP32C3\07_pitch_gearbox_3d.png")
print("saved")

