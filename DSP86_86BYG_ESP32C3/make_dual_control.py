# -*- coding: utf-8 -*-
"""双轴系统——控制原理图（网络标号法）。"""
from PIL import Image, ImageDraw, ImageFont

F = r"C:\Windows\Fonts\msyh.ttc"; FB = r"C:\Windows\Fonts\msyhbd.ttc"
def f(s,b=False): return ImageFont.truetype(FB if b else F,s)

W,H=1800,1150
img=Image.new("RGB",(W,H),"#ffffff"); d=ImageDraw.Draw(img)
RED="#c62828"; BLK="#1b2430"; GRN="#2e7d32"; BLU="#1565c0"; ORG="#ef6c00"; PUR="#6a1b9a"

def t(xy,s,size=18,c=BLK,a="la",b=False): d.text(xy,s,font=f(size,b),fill=c,anchor=a)
def box(r,fill,out=BLK,w=2,rad=8): d.rounded_rectangle(r,radius=rad,fill=fill,outline=out,width=w)
def line(pts,c,w=3): d.line(pts,fill=c,width=w,joint="curve")
def tag(x,y,s,c=BLK,w=110):
    box((x,y-13,x+w,y+13),"#ffffff",c,1,6)
    t((x+w/2,y),s,14,c,"mm",True)
def pin(x,y,name,c=BLK):
    d.rectangle((x,y-11,x+18,y+11),fill="#fff",outline="#90a4ae",width=2)
    t((x-6,y),name,17,c,"rm",True)

t((W//2,28),"双轴云台 控制原理图（ESP32-C3 + 2×DSP86）",38,"#111","ma",True)
t((W//2,80),"同名网络即相连；5V/GND 为公共网络；两台驱动器信号侧与 C3 光耦隔离",19,"#5b6570","ma")

# ESP32-C3
box((70,210,520,1000),"#e8f1fd","#1565c0",3)
t((295,235),"ESP32-C3",26,"#0d47a1","ma",True)
t((295,268),"3.3V / 5V / GND / GPIO",16,"#5b6570","ma")
pins=[("3V3",310),("5V",350),("GND",390),("GPIO4",445),("GPIO5",495),("GPIO6",545),
      ("GPIO7",595),("GPIO10",645),("GPIO0",695),("GPIO1",755),("GPIO20",805),("GPIO3",855)]
for n,y in pins: pin(500,y,n,"#111")
# 网络标号
for y,lab,c in [(310,"3V3",RED),(350,"+5V",RED),(390,"GND",BLK),
                (445,"AZ_PUL",ORG),(495,"AZ_DIR",PUR),(545,"AZ_ENA","#9e9e9e"),
                (595,"EL_PUL",ORG),(645,"EL_DIR",PUR),(695,"EL_ENA","#9e9e9e"),
                (755,"AZ_HOME",GRN),(805,"EL_LIMIT",GRN),(855,"PITCH_ADC",BLU)]:
    line([(518,y),(535,y)],c,3)
    tag(540,y,lab,c,110)

# 驱动器
def driver(x,y,title,nets):
    box((x,y,x+430,y+250),"#f3f4f6","#111",3)
    t((x+215,y+22),title,19,"#111","ma",True)
    t((x+20,y+55),"信号输入（光耦）",15,"#5b6570")
    rows=[("PUL+",y+80,"+5V"),("PUL−",y+110,nets[0]),("DIR+",y+145,"+5V"),
          ("DIR−",y+175,nets[1]),("ENA+",y+210,"+5V"),("ENA−",y+235,nets[2])]
    for nm,yy,net in rows:
        d.rectangle((x,yy-8,x+14,yy+8),fill="#66bb6a",outline="#111")
        t((x+22,yy),nm,15,"#111","lm",True)
        tag(x+80,yy,net,RED if net=="+5V" else BLK,105)
    t((x+300,y+55),"电机输出",15,"#5b6570","ra")
    for i,nm in enumerate(["A+","A−","B+","B−"]):
        yy=y+95+i*35
        d.rectangle((x+416,yy-7,x+430,yy+7),fill="#66bb6a",outline="#111")
        t((x+410,yy),nm,15,"#111","rm",True)

driver(1120,200,"DSP86 #1  方位 AZ",["AZ_PUL","AZ_DIR","AZ_ENA"])
driver(1120,540,"DSP86 #2  俯仰 EL",["EL_PUL","EL_DIR","EL_ENA"])

# 5V / GND 注记
t((700,150),"+5V 公共：两台 DSP86 的 PUL+/DIR+/ENA+ 并接 C3 5V（约 60mA）",16,RED)
t((700,175),"GND 公共：C3 GND、限位开关、传感器 GND（驱动器信号侧不共地）",15,BLK)

# 电机
for i,(y0,name) in enumerate([(250,"M1 方位"),(590,"M2 俯仰")],1):
    box((1620,y0,1770,y0+170),"#e9f7ee",GRN)
    t((1695,y0+20),name,17,"#1b5e20","ma",True)
    t((1695,y0+50),"86BYG450",15,"#2e7d32","ma")
    cols=[BLU,"#f9a825",RED,GRN]
    for j,nm in enumerate(["A+","A−","B+","B−"]):
        yy=y0+95+j*18
        line([(1550,yy),(1620,yy)],cols[j],5)
        t((1612,yy-8),nm,12,cols[j],"ra",True)
    t((1695,y0+150),"蓝/黄/红/绿",12,"#37474f","ma")

# 限位
box((620,880,860,980),"#fff8e1",ORG)
t((740,900),"AZ HOME 限位",16,"#b45309","ma",True)
t((740,928),"常开，闭合到 GND",14,"#7c4a03","ma")
t((640,958),"GND",13,BLK,"lm")
tag(680,845,"AZ_HOME",GRN,110)
line([(740,872),(740,880)],GRN,3)

box((900,880,1140,980),"#fff8e1",ORG)
t((1020,900),"EL 限位（-32°/+72°）",16,"#b45309","ma",True)
t((1020,928),"两只常开串联",14,"#7c4a03","ma")
t((920,958),"GND",13,BLK,"lm")
tag(960,845,"EL_LIMIT",GRN,110)
line([(1020,872),(1020,880)],GRN,3)

# 传感器
box((1200,880,1520,990),"#e8f5e9",GRN)
t((1360,900),"俯仰角传感器 / 测试仪",15,"#1b5e20","ma",True)
t((1360,926),"0–3.3V 输出",14,"#2e7d32","ma")
t((1360,952),"VCC=3V3  GND  OUT",13,"#2e7d32","ma")
tag(1220,845,"3V3",RED,90)
tag(1320,845,"GND",BLK,90)
tag(1420,845,"PITCH_ADC",BLU,120)
line([(1265,872),(1265,880)],RED,3)
line([(1365,872),(1365,880)],BLK,3)
line([(1480,872),(1480,880)],BLU,3)
t((1360,1010),"OUT 串 1kΩ，ADC 对 GND 接 100nF",12,"#37474f","ma")

# 网络表
t((70,1055),"网络表：AZ_PUL=GPIO4  AZ_DIR=GPIO5  AZ_ENA=GPIO6  EL_PUL=GPIO7  EL_DIR=GPIO10  EL_ENA=GPIO0",15,"#37474f")
t((70,1088),"AZ_HOME=GPIO1  EL_LIMIT=GPIO20  PITCH_ADC=GPIO3  +5V=C3 5V  GND=C3 GND",15,"#37474f")
t((70,1120),"注意：DSP86 的 ENA 为脱机信号，光耦导通=电机自由；软件默认使能输出高（光耦断开）。",15,RED)

img.save(r"E:\deepseek\DSP86_86BYG_ESP32C3\10_control_schematic.png")
print("saved")
