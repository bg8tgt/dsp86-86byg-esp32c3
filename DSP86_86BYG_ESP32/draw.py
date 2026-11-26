# -*- coding: utf-8 -*-
"""生成 DSP86 + 86BYG450 + ESP32 接线示意图（PIL 绘制）。"""
from PIL import Image, ImageDraw, ImageFont

FONT = r"C:\Windows\Fonts\msyh.ttc"
FONTB = r"C:\Windows\Fonts\msyhbd.ttc"

def f(size, bold=False):
    return ImageFont.truetype(FONTB if bold else FONT, size)

def text(d, xy, s, font, fill="#1b1f24", anchor="la", spacing=10):
    d.multiline_text(xy, s, font=font, fill=fill, anchor=anchor, spacing=spacing, align="center")

def box(d, rect, fill, outline, width=3, radius=18):
    d.rounded_rectangle(rect, radius=radius, fill=fill, outline=outline, width=width)

def wire(d, pts, color, w=5, casing=True):
    if casing:
        d.line(pts, fill="#ffffff", width=w + 6, joint="curve")
    d.line(pts, fill=color, width=w, joint="curve")

def dot(d, p, color, r=6):
    d.ellipse([p[0]-r, p[1]-r, p[0]+r, p[1]+r], fill=color)

def tab(d, rect):
    d.rounded_rectangle(rect, radius=4, fill="#66bb6a", outline="#1b1f24", width=2)

# ---------- 图 1：系统接线 ----------
def fig_system(path):
    W, H = 1800, 1200
    img = Image.new("RGB", (W, H), "#ffffff")
    d = ImageDraw.Draw(img)

    text(d, (W//2, 32), "ESP32 → DSP86 → 86BYG450 步进系统接线图", f(46, True), "#111111", anchor="ma")
    text(d, (W//2, 98), "AC 18–80 V 供电 · 共阳控制 · 86 系列两相混合式步进电机",
         f(24), "#5b6570", anchor="ma")

    box(d, (70, 340, 360, 800), "#e8f1fd", "#1565c0", 3)
    text(d, (215, 362), "ESP32 开发板", f(28, True), "#0d47a1", anchor="ma")
    text(d, (215, 400), "（GPIO 可自行调整）", f(17), "#5b6570", anchor="ma")
    pins = [("5V", 430, "#d32f2f"), ("GND", 490, "#5b6570"),
            ("GPIO18  STEP", 560, "#ef6c00"), ("GPIO19  DIR", 630, "#6a1b9a"),
            ("GPIO23  ENA", 700, "#9e9e9e")]
    for name, y, c in pins:
        d.rectangle((330, y-19, 360, y+19), fill="#ffffff", outline="#90a4ae", width=2)
        d.text((344, y), name, font=f(19, True), fill=c, anchor="rm")

    box(d, (600, 230, 1120, 900), "#f3f4f6", "#111111", 4)
    text(d, (860, 248), "DSP86 步进驱动器", f(30, True), "#111111", anchor="ma")
    text(d, (860, 290), "上海四宏 · AC 18–80 V · 2.0–6.0 A", f(18), "#5b6570", anchor="ma")
    d.line((640, 320, 1080, 320), fill="#9aa4ae", width=2)
    text(d, (690, 340), "信号输入（光耦隔离）", font=f(16, True), fill="#5b6570", anchor="lm")
    text(d, (1035, 340), "电机输出", font=f(16, True), fill="#5b6570", anchor="rm")

    sig = [("PUL+", 380), ("PUL-", 430), ("DIR+", 520), ("DIR-", 570), ("ENA+", 660), ("ENA-", 710)]
    for name, y in sig:
        tab(d, (600, y-17, 654, y+17))
        d.text((666, y), name, font=f(21, True), fill="#111111", anchor="lm")

    mot = [("A+", 400), ("A-", 450), ("B+", 540), ("B-", 590)]
    for name, y in mot:
        d.text((1046, y), name, font=f(22, True), fill="#111111", anchor="rm")
        tab(d, (1060, y-17, 1120, y+17))

    text(d, (750, 874), "AC 18–80 V", font=f(18, True), fill="#c62828", anchor="rm")
    d.rounded_rectangle((780, 898, 846, 932), radius=4, fill="#ef5350", outline="#1b1f24", width=2)
    d.rounded_rectangle((868, 898, 934, 932), radius=4, fill="#ef5350", outline="#1b1f24", width=2)
    text(d, (813, 915), "AC", f(18, True), "#ffffff", anchor="mm")
    text(d, (901, 915), "AC", f(18, True), "#ffffff", anchor="mm")

    box(d, (1360, 380, 1740, 900), "#e9f7ee", "#1b7f3b", 4)
    text(d, (1550, 402), "86BYG450-79-A50-J5", f(25, True), "#1b5e20", anchor="ma")
    text(d, (1550, 442), "两相混合式步进电机", f(18), "#2e7d32", anchor="ma")
    for name, y in [("A+", 500), ("A-", 550), ("B+", 620), ("B-", 670)]:
        tab(d, (1348, y-16, 1400, y+16))
        d.text((1408, y), name, font=f(18, True), fill="#1b5e20", anchor="lm")
    info = ["步距角      1.8°", "额定电流   5.0 A", "保持转矩   ≈ 5.5 N·m",
            "机身长度   79 mm", "引出线      8 线"]
    for i, s in enumerate(info):
        d.text((1392, 722 + i*32), s, font=f(19), fill="#1b3b24", anchor="la")

    # ---- 信号接线（共阳） ----
    sv = "#d32f2f"
    wire(d, [(360, 430), (410, 430)], sv, 6)
    wire(d, [(410, 380), (410, 660)], sv, 6)
    dot(d, (410, 430), sv)
    for y in (380, 520, 660):
        wire(d, [(410, y), (600, y)], sv, 5)
        dot(d, (410, y), sv)
    text(d, (420, 366), "+5 V 共阳", f(17, True), sv, anchor="lb")

    wire(d, [(360, 560), (480, 560), (480, 430), (600, 430)], "#ef6c00", 5)
    wire(d, [(360, 630), (510, 630), (510, 570), (600, 570)], "#6a1b9a", 5)
    for x in range(360, 540, 18):
        d.line((x, 700, min(x + 9, 540), 700), fill="#9e9e9e", width=4)
    wire(d, [(540, 700), (540, 710), (600, 710)], "#9e9e9e", 4)
    text(d, (470, 726), "可选：悬空即默认使能", f(14), "#757575", anchor="ma")
    text(d, (470, 752), "220Ω 限流电阻的接法见图 2", f(14), "#757575", anchor="ma")

    mw = [("#111111", 400, 500), ("#2e7d32", 450, 550), ("#c62828", 540, 620), ("#1565c0", 590, 670)]
    for i, (c, y1, y2) in enumerate(mw):
        lane = 1170 + i * 26
        wire(d, [(1120, y1), (lane, y1), (lane, y2), (1348, y2)], c, 5)

    pwr = "#c62828"
    box(d, (70, 955, 300, 1090), "#fdecea", pwr, 3)
    text(d, (185, 992), "220 VAC 输入", f(22, True), pwr, anchor="ma")
    text(d, (185, 1036), "L / N / PE", f(19), "#7f1d1d", anchor="ma")

    box(d, (340, 955, 570, 1090), "#fff8e1", "#ef6c00", 3)
    text(d, (455, 992), "断路器 + 保险丝", f(20, True), "#b45309", anchor="ma")
    text(d, (455, 1036), "3–5 A（慢断）", f(18), "#7c4a03", anchor="ma")

    box(d, (600, 950, 950, 1110), "#fdecea", pwr, 3)
    text(d, (775, 976), "隔离变压器", f(22, True), pwr, anchor="ma")
    text(d, (775, 1016), "220 V → 48 VAC", f(20, True), "#7f1d1d", anchor="ma")
    text(d, (775, 1056), "容量 ≥ 300 VA", f(18), "#7f1d1d", anchor="ma")

    d.line((300, 1022, 340, 1022), fill=pwr, width=4)
    d.line((570, 1022, 600, 1022), fill=pwr, width=4)
    wire(d, [(700, 950), (700, 930), (813, 930), (813, 934)], pwr, 4)
    wire(d, [(850, 950), (850, 930), (901, 930), (901, 934)], pwr, 4)

    notes = [
        ("接线要点", True),
        ("① 断电接线，上电前用万用表确认 AC 两端不短路", False),
        ("② 驱动器标称 AC 18–80 V，严禁直接接入 220 V", False),
        ("③ 电机线禁止带电插拔，否则易损坏驱动器", False),
        ("④ ENA 可悬空（默认使能）；脱机控制时再接入", False),
        ("⑤ 驱动器外壳、电机外壳与 PE 可靠接地", False),
        ("⑥ 信号线远离电机动力线，长距离用屏蔽线", False),
    ]
    for i, (s, bold) in enumerate(notes):
        d.text((1150, 966 + i*32), s, font=f(21, True) if bold else f(17),
               fill="#111111" if bold else "#37474f", anchor="la")

    img.save(path)

# ---------- 图 2：控制信号共阳接法 ----------
def fig_signal(path):
    W, H = 1600, 1080
    img = Image.new("RGB", (W, H), "#ffffff")
    d = ImageDraw.Draw(img)

    text(d, (W//2, 26), "控制信号接线（共阳接法，推荐）", f(40, True), "#111111", anchor="ma")
    text(d, (W//2, 84), "GPIO 低电平 → 光耦导通 → 走一步；高电平 → 截止 → 不走",
         f(22), "#5b6570", anchor="ma")

    bus = "#d32f2f"
    d.line((170, 150, 860, 150), fill=bus, width=7)
    text(d, (170, 126), "+5 V（ESP32 的 5V 引脚）", f(19, True), bus, anchor="lb")

    box(d, (90, 240, 380, 840), "#e8f1fd", "#1565c0", 3)
    text(d, (235, 260), "ESP32", f(26, True), "#0d47a1", anchor="ma")
    for n, y in [("GPIO18", 330), ("GPIO19", 520), ("GPIO23", 710)]:
        d.rectangle((350, y+60, 380, y+100), fill="#ffffff", outline="#90a4ae", width=2)
        d.text((344, y+80), n, font=f(19, True), fill="#111111", anchor="rm")

    box(d, (920, 200, 1500, 860), "#f3f4f6", "#111111", 3)
    text(d, (1200, 220), "DSP86 信号输入（内部光耦）", f(24, True), "#111111", anchor="ma")
    text(d, (1200, 256), "每个通道 5–15 mA，差分输入", f(16), "#5b6570", anchor="ma")

    rows = [("PUL", 330, "#ef6c00", "脉冲", False),
            ("DIR", 520, "#6a1b9a", "方向", False),
            ("ENA", 710, "#9e9e9e", "使能（可选）", True)]

    # 先画 +5V 垂直母线，信号线后画，交叉处形成跨线效果
    d.line((860, 150, 860, 710), fill=bus, width=5)

    for name, y, c, cn, dashed in rows:
        # 端子
        tab(d, (920, y-17, 974, y+17))
        tab(d, (920, y+63, 974, y+97))
        d.text((1000, y-26), name + "+", font=f(18, True), fill=c, anchor="lb")
        d.text((1000, y+54), name + "−", font=f(18, True), fill=c, anchor="lb")

        # 内部光耦：阳极在上，阴极在下
        d.line((974, y, 1200, y), fill="#5b6570", width=3)
        d.line((1200, y, 1200, y+8), fill="#5b6570", width=3)
        d.polygon([(1180, y+8), (1220, y+8), (1200, y+40)], outline="#111111", fill="#ffcc80")
        d.line((1176, y+40, 1224, y+40), fill="#111111", width=5)
        d.line((1200, y+40, 1200, y+80), fill="#5b6570", width=3)
        d.line((1200, y+80, 974, y+80), fill="#5b6570", width=3)
        text(d, (1240, y+22), "光耦", f(14), "#5b6570", anchor="la")

        # +5V 从上方接入正端
        wire(d, [(860, y), (920, y)], bus, 5)

        # GPIO → 限流电阻 → 负端
        d.rectangle((470, y+66, 540, y+94), fill="#ffffff", outline=c, width=3)
        text(d, (505, y+112), "220Ω*", f(14, True), c, anchor="ma")
        if dashed:
            for x in range(380, 470, 16):
                d.line((x, y+80, min(x+8, 470), y+80), fill=c, width=4)
            for x in range(540, 920, 16):
                d.line((x, y+80, min(x+8, 920), y+80), fill=c, width=4)
        else:
            d.line((380, y+80, 470, y+80), fill=c, width=5)
            d.line((540, y+80, 920, y+80), fill=c, width=5)

        d.text((400, y+16), name + "  " + cn, font=f(18, True), fill=c, anchor="lb")

    for y in (330, 520, 710):
        dot(d, (860, y), bus)

    text(d, (235, 870), "GPIO18 → PUL−（脉冲）", f(16), "#37474f", anchor="ma")
    text(d, (235, 896), "GPIO19 → DIR−（方向）", f(16), "#37474f", anchor="ma")
    text(d, (235, 922), "GPIO23 → ENA−（可选）", f(16), "#37474f", anchor="ma")

    notes = [
        ("说明", True),
        ("• 共阳接法：+5 V 接 PUL+/DIR+/ENA+，GPIO 接对应的 − 端，靠灌电流点亮光耦。", False),
        ("• 220Ω 为可选限流/保护电阻，驱动器内部已有限流，短接也能工作。", False),
        ("• ESP32 为 3.3 V 逻辑，灌电流足够；干扰大时可加 74HCT245、ULN2003 或光耦模块。", False),
        ("• +5 V 与驱动器 AC 侧不共地；光耦本身已实现隔离。", False),
        ("• ENA 悬空 = 默认使能；不同批次有效电平可能相反，首次上电先单步测试。", False),
    ]
    for i, (s, bold) in enumerate(notes):
        d.text((560, 880 + i*30), s, font=f(17, True) if bold else f(15),
               fill="#111111" if bold else "#37474f", anchor="la")

    img.save(path)

# ---------- 图 3：电机绕组接法 ----------
def coil(d, rect, label, color="#1565c0"):
    d.rounded_rectangle(rect, radius=24, outline=color, width=4, fill="#ffffff")
    text(d, ((rect[0]+rect[2])//2, (rect[1]+rect[3])//2), label, f(17, True), color, anchor="mm")

def fig_motor(path):
    W, H = 1600, 1120
    img = Image.new("RGB", (W, H), "#ffffff")
    d = ImageDraw.Draw(img)

    text(d, (W//2, 24), "86BYG450 电机绕组接线（4 线 / 8 线）", f(38, True), "#111111", anchor="ma")
    text(d, (W//2, 80), "同相两根线用万用表测量：阻值约 0.5–2 Ω；不同相之间不通",
         f(21), "#5b6570", anchor="ma")

    # 左：4 线
    box(d, (60, 150, 760, 620), "#f7fbff", "#1565c0", 3)
    text(d, (410, 170), "A. 4 线（双极）", f(24, True), "#0d47a1", anchor="ma")
    for n, y in [("A+", 300), ("A-", 360), ("B+", 450), ("B-", 510)]:
        d.rectangle((110, y-16, 165, y+16), fill="#66bb6a", outline="#1b1f24", width=2)
        d.text((137, y), n, font=f(18, True), fill="#ffffff", anchor="mm")

    box(d, (470, 240, 700, 570), "#e9f7ee", "#1b7f3b", 3)
    text(d, (585, 262), "86BYG450", f(22, True), "#1b5e20", anchor="ma")
    coil(d, (560, 290, 640, 370), "A 相", "#111111")
    coil(d, (560, 440, 640, 520), "B 相", "#c62828")
    for c, y in [("#111111", 300), ("#2e7d32", 360), ("#c62828", 450), ("#1565c0", 510)]:
        d.line((165, y, 470, y), fill=c, width=5)
        d.line((470, y, 560, y), fill=c, width=5)
        dot(d, (470, y), c, 5)
        dot(d, (560, y), c, 5)
    text(d, (410, 588), "常见色序：黑 A+ · 绿 A- · 红 B+ · 蓝 B-", f(15), "#37474f", anchor="ma")

    # 右上：8 线串联
    box(d, (820, 150, 1540, 620), "#fffdf5", "#ef6c00", 3)
    text(d, (1180, 170), "B. 8 线串联（低速大转矩）", f(23, True), "#b45309", anchor="ma")
    text(d, (1180, 204), "电流取铭牌 0.7 倍（本例约 3.5 A）", f(16), "#b45309", anchor="ma")

    d.rectangle((860, 250, 905, 285), fill="#66bb6a", outline="#1b1f24", width=2)
    d.text((882, 267), "A+", font=f(15, True), fill="#ffffff", anchor="mm")
    coil(d, (960, 246, 1130, 290), "A1", "#111111")
    coil(d, (1180, 246, 1350, 290), "A2", "#111111")
    d.line((905, 267, 960, 267), fill="#111111", width=4)
    d.line((1130, 267, 1180, 267), fill="#111111", width=4)
    d.line((1350, 267, 1440, 267), fill="#111111", width=4)
    d.text((1448, 267), "A-", font=f(17, True), fill="#111", anchor="lm")
    text(d, (1155, 306), "A1 尾端 → A2 首端", f(14), "#b45309", anchor="ma")

    d.rectangle((860, 380, 905, 415), fill="#66bb6a", outline="#1b1f24", width=2)
    d.text((882, 397), "B+", font=f(15, True), fill="#ffffff", anchor="mm")
    coil(d, (960, 376, 1130, 420), "B1", "#c62828")
    coil(d, (1180, 376, 1350, 420), "B2", "#c62828")
    d.line((905, 397, 960, 397), fill="#c62828", width=4)
    d.line((1130, 397, 1180, 397), fill="#c62828", width=4)
    d.line((1350, 397, 1440, 397), fill="#c62828", width=4)
    d.text((1448, 397), "B-", font=f(17, True), fill="#111", anchor="lm")
    text(d, (1155, 436), "B1 尾端 → B2 首端", f(14), "#b45309", anchor="ma")

    text(d, (880, 490), "A+ → A1 → A2 → A-；B+ → B1 → B2 → B-", f(17), "#37474f", anchor="la")
    text(d, (880, 524), "电感增大、高速转矩下降，适合低速重载。", f(17), "#37474f", anchor="la")
    text(d, (880, 558), "若电机为 4 线，则内部已串联，直接按 A+/A-/B+/B- 接。", f(15), "#7a4a00", anchor="la")

    # 下：8 线并联
    box(d, (60, 660, 1540, 1060), "#f7fbff", "#1565c0", 3)
    text(d, (800, 678), "C. 8 线并联（高速性能好）", f(24, True), "#0d47a1", anchor="ma")
    text(d, (800, 712), "电流取铭牌 1.4 倍：本例 5.0 A × 1.4 = 7.0 A，超过 DSP86 的 6.0 A 上限",
         f(14), "#c62828", anchor="ma")

    d.rectangle((100, 790, 145, 825), fill="#66bb6a", outline="#1b1f24", width=2)
    d.text((122, 807), "A+", font=f(15, True), fill="#ffffff", anchor="mm")
    coil(d, (280, 750, 500, 795), "A1", "#111111")
    coil(d, (280, 840, 500, 885), "A2", "#111111")
    d.line((145, 807, 200, 807), fill="#111111", width=4)
    d.line((200, 807, 200, 772, 280, 772), fill="#111111", width=4)
    d.line((200, 807, 200, 862, 280, 862), fill="#111111", width=4)
    dot(d, (200, 807), "#111111", 5)
    d.line((500, 772, 580, 772), fill="#111111", width=4)
    d.line((500, 862, 580, 862), fill="#111111", width=4)
    d.line((580, 772, 580, 862), fill="#111111", width=4)
    d.line((580, 817, 660, 817), fill="#111111", width=4)
    d.text((668, 817), "A-", font=f(17, True), fill="#111", anchor="lm")

    d.rectangle((840, 790, 885, 825), fill="#66bb6a", outline="#1b1f24", width=2)
    d.text((862, 807), "B+", font=f(15, True), fill="#ffffff", anchor="mm")
    coil(d, (1020, 750, 1240, 795), "B1", "#c62828")
    coil(d, (1020, 840, 1240, 885), "B2", "#c62828")
    d.line((885, 807, 940, 807), fill="#c62828", width=4)
    d.line((940, 807, 940, 772, 1020, 772), fill="#c62828", width=4)
    d.line((940, 807, 940, 862, 1020, 862), fill="#c62828", width=4)
    dot(d, (940, 807), "#c62828", 5)
    d.line((1240, 772, 1320, 772), fill="#c62828", width=4)
    d.line((1240, 862, 1320, 862), fill="#c62828", width=4)
    d.line((1320, 772, 1320, 862), fill="#c62828", width=4)
    d.line((1320, 817, 1400, 817), fill="#c62828", width=4)
    d.text((1408, 817), "B-", font=f(17, True), fill="#111", anchor="lm")

    text(d, (110, 940), "并联：A1、A2 首端并接为 A+，尾端并接为 A-；B 相同理。",
         f(17), "#37474f", anchor="la")
    text(d, (110, 976), "并联后相电阻减半、电流加倍；只有在驱动器电流上限允许时才可使用。",
         f(17), "#37474f", anchor="la")
    text(d, (110, 1012), "对 DSP86（峰值 6.0 A）+ 本例 5.0 A 电机，推荐串联接法，不要并联。",
         f(17, True), "#c62828", anchor="la")

    img.save(path)

if __name__ == "__main__":
    base = r"E:\deepseek\DSP86_86BYG_ESP32"
    fig_system(base + r"\01_system_wiring.png")
    fig_signal(base + r"\02_signal_wiring.png")
    fig_motor(base + r"\03_motor_wiring.png")
    print("done")
