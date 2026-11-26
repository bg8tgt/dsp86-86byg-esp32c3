# -*- coding: utf-8 -*-
"""双轴控制原理图：ESP32-C3 实际连线到两台 DSP86 + 两台电机。"""
from PIL import Image, ImageDraw, ImageFont

F = r"C:\Windows\Fonts\msyh.ttc"; FB = r"C:\Windows\Fonts\msyhbd.ttc"
def f(s,b=False): return ImageFont.truetype(FB if b else F,s)

W,H=2000,1400
img=Image.new("RGB",(W,H),"#ffffff"); d=ImageDraw.Draw(img)
RED="#c62828"; BLK="#1b2430"; GRN="#2e7d32"; BLU="#1565c0"; ORG="#ef6c00"; PUR="#6a1b9a"; GRY="#757575"

def t(xy,s,size=18,c=BLK,a="la",b=False): d.text(xy,s,font=f(size,b),fill=c,anchor=a)
def box(r,fill,out=BLK,w=2,rad=8): d.rounded_rectangle(r,radius=rad,fill=fill,outline=out,width=w)
def wire(pts,c,w=4,label=None,lx=None,ly=None,lc=None):
    d.line(pts,fill=c,width=w,joint="curve")
    if label:
        t((lx,ly),label,14,lc or c,"mm",True)

t((W//2,28),"双轴控制原理图：ESP32-C3 → 2×DSP86 → 2×86BYG450",36,"#111","ma",True)
t((W//2,82),"GPIO4/5/6 控制方位驱动器；GPIO7/10/0 控制俯仰驱动器；5V 并接 6 个 + 端；两台电机独立",18,"#5b6570","ma")

# ESP32
box((70,200,520,1300),"#e8f1fd","#1565c0",3)
t((295,225),"ESP32-C3",26,"#0d47a1","ma",True)
t((295,258),"3.3V 逻辑 · 光耦隔离输出",15,"#5b6570","ma")
pins=[("3V3",300,RED),("GND",340,BLK),("5V",400,RED),
      ("GPIO4",480,ORG),("GPIO5",560,PUR),("GPIO6",640,GRY),
      ("GPIO7",900,ORG),("GPIO10",980,PUR),("GPIO0",1060,GRY),
      ("GPIO1",1140,GRN),("GPIO20",1180,GRN),("GPIO3",1220,BLU)]
for n,y,c in pins:
    d.rectangle((500,y-11,518,y+11),fill="#fff",outline="#90a4ae",width=2)
    t((494,y),n,17,c,"rm",True)

# 驱动器
def drv(x,y,title,prefix):
    box((x,y,x+430,y+480),"#f3f4f6","#111",3)
    t((x+215,y+24),title,19,"#111","ma",True)
    t((x+18,y+58),"信号（光耦）",14,"#5b6570")
    rows=[("PUL+",y+80,"+5V"),("PUL−",y+130,prefix+"_PUL"),
          ("DIR+",y+200,"+5V"),("DIR−",y+250,prefix+"_DIR"),
          ("ENA+",y+320,"+5V"),("ENA−",y+370,prefix+"_ENA")]
    for nm,yy,net in rows:
        d.rectangle((x,yy-8,x+14,yy+8),fill="#66bb6a",outline="#111")
        t((x+22,yy),nm,15,"#111","lm",True)
        if net=="+5V":
            box((x+80,yy-12,x+140,yy+12),"#fff","#c62828",1,5)
            t((x+110,yy),"+5V",12,"#c62828","mm",True)
    t((x+300,y+58),"电机输出",14,"#5b6570","ra")
    for i,nm in enumerate(["A+","A−","B+","B−"]):
        yy=y+110+i*45
        d.rectangle((x+416,yy-8,x+430,yy+8),fill="#66bb6a",outline="#111")
        t((x+410,yy),nm,14,"#111","rm",True)

drv(1150,240,"DSP86 #1  方位 AZ","AZ")
drv(1150,800,"DSP86 #2  俯仰 EL","EL")

# 5V 标签（避免长线交叉）
box((548,388,610,412),"#fff","#c62828",1,5); t((579,400),"+5V",12,"#c62828","mm",True)

# 六路控制线
wire([(518,480),(700,480),(700,370),(1150,370)],ORG,4,"GPIO4 → PUL−",860,355,ORG)
wire([(518,560),(760,560),(760,490),(1150,490)],PUR,4,"GPIO5 → DIR−",860,475,PUR)
wire([(518,640),(820,640),(820,610),(1150,610)],GRY,4,"GPIO6 → ENA−",860,595,GRY)
wire([(518,900),(700,900),(700,930),(1150,930)],ORG,4,"GPIO7 → PUL−",860,915,ORG)
wire([(518,980),(760,980),(760,1050),(1150,1050)],PUR,4,"GPIO10 → DIR−",860,1035,PUR)
wire([(518,1060),(820,1060),(820,1170),(1150,1170)],GRY,4,"GPIO0 → ENA−",900,1105,GRY)

# 电机
def motor(x,y,name):
    box((x,y,x+180,y+220),"#e9f7ee",GRN)
    t((x+90,y+24),name,17,"#1b5e20","ma",True)
    t((x+90,y+54),"86BYG450",14,"#2e7d32","ma")
    t((x+90,y+84),"4 线",12,"#2e7d32","ma")
    for i,nm in enumerate(["A+","A−","B+","B−"]):
        t((x+10,y+120+i*22),nm,13,"#37474f","lm",True)
    t((x+90,y+205),"蓝/黄/红/绿",11,"#37474f","ma")

motor(1640,300,"M1 方位")
motor(1640,860,"M2 俯仰")
cols=[BLU,"#f9a825",RED,GRN]
for i in range(4):
    y1=350+i*45; y2=420+i*22
    d.line([(1550,y1),(1595,y1),(1595,y2),(1640,y2)],fill=cols[i],width=5)
for i in range(4):
    y1=910+i*45; y2=980+i*22
    d.line([(1550,y1),(1595,y1),(1595,y2),(1640,y2)],fill=cols[i],width=5)

# I/O 网络标号（限位/传感器细节见 04 图）
def tag(x,y,label,c,w=120):
    box((x,y-13,x+w,y+13),"#fff",c,1,6)
    t((x+w/2,y),label,13,c,"mm",True)

for y,lab,c in [(1140,"AZ_HOME",GRN),(1180,"EL_LIMIT",GRN),(1220,"PITCH_ADC",BLU)]:
    d.line([(518,y),(535,y)],c,3)
    tag(540,y,lab,c,115)

t((560,1250),"限位/急停：AZ_HOME=GPIO1，EL_LIMIT=GPIO20，均内部上拉、到 GND 触发",13,"#37474f")
t((560,1275),"俯仰角：PITCH_ADC=GPIO3（ADC1_CH3），0–3.3V，OUT 串 1kΩ、对 GND 100nF",13,"#37474f")
t((560,1300),"限位触发时两台驱动器同时 forceStop 并脱机；排除后网页手动恢复使能。",13,RED)

# 说明
t((70,1320),"5V 公共：两台 DSP86 的 PUL+/DIR+/ENA+ 共 6 个 + 端并接 C3 5V（约 60mA）；驱动器信号侧与 C3 不共地。",14,"#37474f")
t((70,1348),"限位/急停触发：两台 forceStop + 脱机；ENA 为脱机信号，使能时 GPIO 输出高（光耦断开）。",14,RED)
t((70,1376),"电机线色：蓝 A+、黄 A−、红 B+、绿 B−；两台电机分别接各自驱动器，禁止并联。",14,"#37474f")

img.save(r"E:\deepseek\DSP86_86BYG_ESP32C3\10_control_schematic.png")
print("saved")


