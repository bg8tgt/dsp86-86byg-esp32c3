# -*- coding: utf-8 -*-
"""3D 总装：M1 直驱（无齿轮箱），M2 在齿轮箱下面。"""
import math
from PIL import Image, ImageDraw, ImageFont
F=r"C:\Windows\Fonts\msyh.ttc"; FB=r"C:\Windows\Fonts\msyhbd.ttc"
def f(s,b=False): return ImageFont.truetype(FB if b else F,s)
W,H=1700,1150
img=Image.new("RGB",(W,H),"#ffffff"); d=ImageDraw.Draw(img)
BLK="#1b2430"; RED="#c62828"; GRN="#2e7d32"; BLU="#1565c0"; ORG="#ef6c00"
def t(xy,s,sz=16,c=BLK,a="la",b=False): d.text(xy,s,font=f(sz,b),fill=c,anchor=a)
OX,OY,S=660,820,1.05
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

t((W//2,26),"双轴云台 3D 总装（M1 直驱 + M2 在齿轮箱下面）",32,"#111","ma",True)
t((W//2,72),"M1 直接驱动方位回转台；M2 竖立在俯仰齿轮箱下方；齿轮箱左右同步输出",16,"#5b6570","ma")

# 底座
box(0,0,8,300,220,16,(176,190,207))
box(-100,0,50,14,14,100,(144,164,184),(170,188,205),(118,138,158))
box(100,0,50,14,14,100,(144,164,184),(170,188,205),(118,138,158))
box(0,0,105,230,170,10,(176,190,207),(205,216,228),(150,164,183))
# M1 直驱
box(0,0,55,82,82,86,(69,90,100),(96,118,128),(52,70,80))
d.line([P(0,0,98),P(0,0,120)],fill=(176,190,207),width=16)
# 回转台
cylz(0,0,120,95,20,(120,144,156))
# 立柱
box(-95,0,190,16,110,130,(144,164,184),(170,188,205),(118,138,158))
box(95,0,190,16,110,130,(144,164,184),(170,188,205),(118,138,158))
# 顶部横梁（支撑齿轮箱）
box(0,-70,350,205,16,14,(144,164,184),(170,188,205),(118,138,158))
# 齿轮箱
box(0,-70,305,92,70,66,(198,120,60),(224,150,90),(160,86,38))
# M2 在齿轮箱下面
box(0,-70,215,72,72,80,(69,90,100),(96,118,128),(52,70,80))
d.line([P(0,-70,255),P(0,-70,272)],fill=(176,190,207),width=14)
# 左右输出
d.line([P(-95,-70,305),P(95,-70,305)],fill=(176,190,207),width=18)
d.line([P(-95,-70,305),P(95,-70,305)],fill="#546e7a",width=2)
# 平台
box(0,20,260,160,110,10,(255,183,77),(255,204,128),(230,150,45))
box(-70,-25,260,12,90,10,(255,183,77),(255,204,128),(230,150,45))
box(70,-25,260,12,90,10,(255,183,77),(255,204,128),(230,150,45))
# 传感器
box(110,-70,305,26,40,28,(102,187,106),(129,199,132),(76,155,80))

# 轴线
dash(P(0,0,0),P(0,0,460))
dash(P(-150,-70,305),P(150,-70,305),BLU)

# 标注
def leader(a,b,lab,pos,c=BLK):
    d.line([a,b],fill=c,width=2)
    t(pos,lab,15,c)
leader((250,690),(P(-30,30,70)),"水平电机 M1（直驱）",(260,682))
leader((1080,470),(P(45,-70,312)),"垂直齿轮箱（双输出）",(1090,462))
leader((1080,540),(P(-20,-70,240)),"垂直电机 M2（在齿轮箱下面）",(1090,532))
leader((1080,380),(P(0,80,265)),"负载平台",(1090,372))
leader((1080,650),(P(110,-55,305)),"俯仰角传感器",(1090,642))
leader((250,930),(P(-95,40,225)),"U 形立柱",(260,922))

# 轴向图
AX,AY=1360,720
t((AX,150),"轴向关系",20,"#111","ma",True)
d.line([(AX,AY),(AX,AY-190)],fill=RED,width=5)
d.polygon([(AX,AY-200),(AX-9,AY-178),(AX+9,AY-178)],fill=RED)
t((AX+12,AY-200),"方位轴 Z",14,RED)
d.line([(AX,AY),(AX+180,AY)],fill=BLU,width=5)
d.polygon([(AX+190,AY),(AX+168,AY-9),(AX+168,AY+9)],fill=BLU)
t((AX+55,AY+16),"俯仰轴 X",14,BLU)
d.line([(AX,AY),(AX-120,AY-90)],fill=ORG,width=5)
d.polygon([(AX-128,AY-96),(AX-110,AY-78),(AX-102,AY-94)],fill=ORG)
t((AX-250,AY+40),"M2 轴线 Z",13,ORG)

img.save(r"E:\deepseek\DSP86_86BYG_ESP32C3\07_pitch_gearbox_3d.png")
print("saved")
