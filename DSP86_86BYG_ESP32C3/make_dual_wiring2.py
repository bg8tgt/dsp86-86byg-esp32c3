# -*- coding: utf-8 -*-
"""双轴接线图：上方控制接线（ESP32→2×DSP86→2×电机），下方线号表。"""
from PIL import Image, ImageDraw, ImageFont

F = r"C:\Windows\Fonts\msyh.ttc"; FB = r"C:\Windows\Fonts\msyhbd.ttc"
def f(s,b=False): return ImageFont.truetype(FB if b else F,s)

W,H=1800,2140
img=Image.new("RGB",(W,H),"#ffffff")
top=Image.open(r"E:\deepseek\DSP86_86BYG_ESP32C3\10_control_schematic.png").convert("RGB")
top=top.resize((1800,1260),Image.LANCZOS)
img.paste(top,(0,0))
d=ImageDraw.Draw(img)
BLK="#1b2430"; RED="#c62828"; GRN="#2e7d32"; BLU="#1565c0"; ORG="#ef6c00"; PUR="#6a1b9a"

def t(xy,s,size=15,c=BLK,a="la",b=False): d.text(xy,s,font=f(size,b),fill=c,anchor=a)
def box(r,fill,out="#cbd5e1",w=1): d.rectangle(r,fill=fill,outline=out,width=w)

d.line([(0,1262),(W,1262)],fill="#94a3b8",width=2)
t((60,1282),"双轴接线线号表 W1–W29（与上方控制图、电源图对应）",24,"#111",b=True)

rows=[
("W1","220V L","QF1 L","棕","1.5","一次侧"),
("W2","220V N","QF1 N","蓝","1.5","一次侧"),
("W3","220V PE","PE 排","黄绿","1.5","接地"),
("W4","QF1 L","FU1","棕","1.5",""),
("W5","FU1","SB1","棕","1.5",""),
("W6","QF1 N","SB1","蓝","1.5",""),
("W7","SB1","T1 L","棕","1.5",""),
("W8","SB1","T1 N","蓝","1.5",""),
("W9","T1 二次","FU2","红","4.0","24VAC"),
("W10","FU2","FU3/FU4 母线","红","4.0","24VAC"),
("W11","FU3","DRV1 AC1","红","2.5",""),
("W12","T1 二次","DRV1 AC2","黑","2.5",""),
("W13","FU4","DRV2 AC1","红","2.5",""),
("W14","T1 二次","DRV2 AC2","黑","2.5",""),
("W15","C3 5V","X2:1","红","0.5","公共 +5V"),
("W16","X2:1","DRV1/2 PUL+/DIR+/ENA+","红","0.5","6 根并接"),
("W17","C3 GPIO4","DRV1 PUL−","橙","0.2","AZ_PUL"),
("W18","C3 GPIO5","DRV1 DIR−","紫","0.2","AZ_DIR"),
("W19","C3 GPIO6","DRV1 ENA−","灰","0.2","AZ_ENA"),
("W20","C3 GPIO7","DRV2 PUL−","橙","0.2","EL_PUL"),
("W21","C3 GPIO10","DRV2 DIR−","紫","0.2","EL_DIR"),
("W22","C3 GPIO0","DRV2 ENA−","灰","0.2","EL_ENA"),
("W23","C3 GPIO1","AZ HOME","绿","0.2","到 GND"),
("W24","C3 GPIO20","EL LIMIT","绿","0.2","到 GND"),
("W25","C3 3V3","传感器 VCC","红","0.2",""),
("W26","C3 GND","传感器 GND","黑","0.2",""),
("W27","传感器 OUT","C3 GPIO3","蓝","0.2","1k/100nF"),
("W28","DRV1 A+/A−/B+/B−","M1","蓝/黄/红/绿","1.0","4 芯屏蔽"),
("W29","DRV2 A+/A−/B+/B−","M2","蓝/黄/红/绿","1.0","4 芯屏蔽"),
]

x0=60; y0=1330
widths=[80,230,420,150,80,600]
headers=["线号","起点","终点","线色","mm²","备注"]
x=x0
for i,h in enumerate(headers):
    box((x,y0,x+widths[i],y0+34),"#1f3b63","#1f3b63")
    t((x+widths[i]/2,y0+17),h,15,"#fff","mm",True)
    x+=widths[i]
for ri,row in enumerate(rows):
    y=y0+34+ri*22
    x=x0
    fill="#f2f5f9" if ri%2 else "#ffffff"
    for i,v in enumerate(row):
        box((x,y,x+widths[i],y+22),fill)
        c=BLK
        if i==3:
            c={"棕":"#8d6e63","蓝":BLU,"黄绿":GRN,"红":RED,"黑":BLK,"橙":ORG,"紫":PUR,"灰":"#757575","绿":GRN,"黄":"#f9a825"}.get(v,BLK)
        t((x+6,y+11),v,13,c,"lm")
        x+=widths[i]

t((60,2050),"注：1) 驱动器信号侧与 C3 不共地；2) 5V 公共约 60mA；3) 电机线屏蔽层驱动器侧单端接 PE。",13,"#37474f")
t((60,2078),"4) 两台驱动器 AC 不分极性；5) 限位/急停触发时两台停机并脱机；6) 线号与原理图一致。",13,"#37474f")

img.save(r"E:\deepseek\DSP86_86BYG_ESP32C3\11_wiring_diagram.png")
print("saved")
