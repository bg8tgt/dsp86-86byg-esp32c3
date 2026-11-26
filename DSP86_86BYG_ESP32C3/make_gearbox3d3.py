# -*- coding: utf-8 -*-
"""双轴 3D 总装：底部水平电机 + 90°换向 + 顶部垂直电机 + 垂直齿轮箱双输出。"""
import math
from PIL import Image, ImageDraw, ImageFont
F=r"C:\Windows\Fonts\msyh.ttc"; FB=r"C:\Windows\Fonts\msyhbd.ttc"
def f(s,b=False): return ImageFont.truetype(FB if b else F,s)
W,H=1700,1150
img=Image.new("RGB",(W,H),"#ffffff"); d=ImageDraw.Draw(img)
BLK="#1b2430"; RED="#c62828"; GRN="#2e7d32"; BLU="#1565c0"; ORG="#ef6c00"
def t(xy,s,sz=16,c=BLK,a="la",b=False): d.text(xy,s,font=f(sz,b),fill=c,anchor=a)
OX,OY,S=650,800,1.0
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
def dash(p0,p1,c=RED,w=2,n=18):
    for i in range(n):
        if i%2: continue
        a=i/n; b=(i+1)/n
        d.line([(p0[0]+(p1[0]-p0[0])*a,p0[1]+(p1[1]-p0[1])*a),
                (p0[0]+(p1[0]-p0[0])*b,p0[1]+(p1[1]-p0[1])*b)],fill=c,width=w)

t((W//2,26),"双轴云台 3D 总装（水平电机在底部 + 垂直电机 + 垂直齿轮箱双输出）",32,"#111","ma",True)
t((W//2,72),"M1 水平卧式 → 90°换向 → 垂直方位轴；M2 垂直竖立 → 垂直齿轮箱 → 左右同步俯仰轴",16,"#5b6570","ma")

# 底座
box(0,0,8,300,220,16,(176,190,207))
# 水平电机（沿 X 卧式）
box(-120,0,62,130,80,74,(69,90,100),(96,118,128),(52,70,80))
# 90°齿轮箱
box(0,0,62,80,80,74,(198,120,60),(224,150,90),(160,86,38))
# 垂直输出轴
d.line([P(0,0,99),P(0,0,132)],fill=(176,190,207),width=16)
# 回转台
cylz(0,0,132,95,20,(120,144,156))
# 立柱
box(-95,0,220,16,110,176,(144,164,184),(170,188,205),(118,138,158))
box(95,0,220,16,110,176,(144,164,184),(170,188,205),(118,138,158))
# 顶部横梁
box(0,-70,308,200,16,14,(144,164,184),(170,188,205),(118,138,158))
# 垂直电机（竖立）
box(0,-70,405,76,76,100,(69,90,100),(96,118,128),(52,70,80))
# 垂直齿轮箱
box(0,-70,300,92,70,70,(198,120,60),(224,150,90),(160,86,38))
# 垂直电机轴
d.line([P(0,-70,355),P(0,-70,335)],fill=(176,190,207),width=14)
# 左右输出轴
d.line([P(-95,-70,300),P(95,-70,300)],fill=(176,190,207),width=18)
d.line([P(-95,-70,300),P(95,-70,300)],fill="#546e7a",width=2)
# 平台与吊臂
box(0,20,300,160,110,10,(255,183,77),(255,204,128),(230,150,45))
box(-70,-25,300,12,90,10,(255,183,77),(255,204,128),(230,150,45))
box(70,-25,300,12,90,10,(255,183,77),(255,204,128),(230,150,45))
# 传感器
box(110,-70,300,26,40,28,(102,187,106),(129,199,132),(76,155,80))

# 轴线
dash(P(0,0,0),P(0,0,470))
dash(P(-150,-70,300),P(150,-70,300),BLU)

# 标注
def leader(a,b,lab,pos,c=BLK):
    d.line([a,b],fill=c,width=2)
    t(pos,lab,15,c)
leader((250,690),(P(-120,45,70)),"水平电机 M1",(260,682))
leader((700,650),(P(45,45,70)),"90°换向齿轮箱",(710,642))
leader((1050,430),(P(0,-45,310)),"垂直齿轮箱（双输出）",(1060,422))
leader((1050,330),(P(0,-110,420)),"垂直电机 M2",(1060,322))
leader((1050,500),(P(0,60,305)),"负载平台",(1060,492))
leader((1050,600),(P(110,-55,300)),"俯仰角传感器",(1060,592))
leader((230,900),(P(-95,45,220)),"U 形立柱",(240,892))

# 轴向小图
AX,AY=1350,700
t((AX,150),"轴向关系",20,"#111","ma",True)
d.line([(AX,AY),(AX,AY-190)],fill=RED,width=5)
d.polygon([(AX,AY-200),(AX-9,AY-178),(AX+9,AY-178)],fill=RED)
t((AX+12,AY-200),"方位轴 Z",14,RED)
d.line([(AX,AY),(AX+180,AY)],fill=BLU,width=5)
d.polygon([(AX+190,AY),(AX+168,AY-9),(AX+168,AY+9)],fill=BLU)
t((AX+55,AY+16),"俯仰轴 X",14,BLU)
d.line([(AX,AY),(AX-120,AY-90)],fill=ORG,width=5)
d.polygon([(AX-128,AY-96),(AX-110,AY-78),(AX-102,AY-94)],fill=ORG)
t((AX-300,AY-70),"垂直电机轴",13,ORG)
t((AX-300,AY-46),"⊥ 方位轴",12,"#37474f")

img.save(r"E:\deepseek\DSP86_86BYG_ESP32C3\07_pitch_gearbox_3d.png")
print("saved")
