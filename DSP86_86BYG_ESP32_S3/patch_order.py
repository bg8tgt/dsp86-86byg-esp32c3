# -*- coding: utf-8 -*-
import io
import re

p = r"E:\deepseek\DSP86_86BYG_ESP32_S3\draw.py"
s = io.open(p, encoding="utf-8").read()
miss = []


def rep(a, b):
    global s
    if a not in s:
        miss.append(a[:50])
    s = s.replace(a, b)


# ---- 图1：端子顺序 DIR / PUL / ENA，且上 − 下 + ----
rep('sig = [("PUL+", 380), ("PUL-", 430), ("DIR+", 520), ("DIR-", 570), ("ENA+", 660), ("ENA-", 710)]',
    'sig = [("DIR-", 380), ("DIR+", 430), ("PUL-", 520), ("PUL+", 570), ("ENA-", 660), ("ENA+", 710)]')
rep('wire(d, [(410, 380), (410, 660)], sv, 6)', 'wire(d, [(410, 430), (410, 710)], sv, 6)')
rep('for y in (380, 520, 660):', 'for y in (430, 570, 710):')
rep('text(d, (420, 366), "+5 V 共阳"', 'text(d, (420, 416), "+5 V 共阳"')
rep('wire(d, [(360, 560), (480, 560), (480, 430), (600, 430)], "#ef6c00", 5)',
    'wire(d, [(360, 560), (480, 560), (480, 520), (600, 520)], "#ef6c00", 5)')
rep('wire(d, [(360, 630), (510, 630), (510, 570), (600, 570)], "#6a1b9a", 5)',
    'wire(d, [(360, 630), (510, 630), (510, 380), (600, 380)], "#6a1b9a", 5)')
rep('wire(d, [(540, 700), (540, 710), (600, 710)], "#9e9e9e", 4)',
    'wire(d, [(540, 700), (540, 660), (600, 660)], "#9e9e9e", 4)')

# ---- 图2 整体重写 ----
new_fig2 = '''# ---------- 图 2：控制信号共阳接法 ----------
def fig_signal(path):
    W, H = 1600, 1080
    img = Image.new("RGB", (W, H), "#ffffff")
    d = ImageDraw.Draw(img)

    text(d, (W//2, 26), "ESP32-S3 控制信号接线（共阳接法）", f(40, True), "#111111", anchor="ma")
    text(d, (W//2, 84), "GPIO 低电平 → 光耦导通 → 走一步；高电平 → 截止 → 不走",
         f(22), "#5b6570", anchor="ma")

    bus = "#d32f2f"
    d.line((170, 150, 860, 150), fill=bus, width=7)
    text(d, (170, 126), "+5 V（ESP32-S3 的 5V 引脚）", f(19, True), bus, anchor="lb")

    box(d, (90, 240, 380, 840), "#e8f1fd", "#1565c0", 3)
    text(d, (235, 260), "ESP32-S3", f(26, True), "#0d47a1", anchor="ma")

    box(d, (920, 200, 1500, 860), "#f3f4f6", "#111111", 3)
    text(d, (1200, 220), "DSP86 信号输入（内部光耦）", f(24, True), "#111111", anchor="ma")
    text(d, (1200, 256), "每个通道 5–15 mA，差分输入", f(16), "#5b6570", anchor="ma")

    # 端子顺序与实物一致：DIR 在上，PUL 在下；每对上面是 −，下面是 +
    rows = [("DIR", 330, "#6a1b9a", "方向", False),
            ("PUL", 520, "#ef6c00", "脉冲", False),
            ("ENA", 710, "#9e9e9e", "使能（可选）", True)]

    d.line((860, 150, 860, 790), fill=bus, width=5)

    for name, y, c, cn, dashed in rows:
        tab(d, (920, y-17, 974, y+17))
        tab(d, (920, y+63, 974, y+97))
        d.text((1000, y-26), name + "−", font=f(18, True), fill=c, anchor="lb")
        d.text((1000, y+54), name + "+", font=f(18, True), fill=c, anchor="lb")

        # 内部光耦：阳极在下（+），阴极在上（−），箭头向上
        d.line((974, y, 1200, y), fill="#5b6570", width=3)
        d.line((1200, y, 1200, y+8), fill="#5b6570", width=3)
        d.line((1176, y+8, 1224, y+8), fill="#111111", width=5)
        d.polygon([(1180, y+40), (1220, y+40), (1200, y+8)], outline="#111111", fill="#ffcc80")
        d.line((1200, y+40, 1200, y+80), fill="#5b6570", width=3)
        d.line((1200, y+80, 974, y+80), fill="#5b6570", width=3)
        text(d, (1240, y+24), "光耦", f(14), "#5b6570", anchor="la")

        # +5V 接下端正端
        wire(d, [(860, y+80), (920, y+80)], bus, 5)

        # GPIO → 限流电阻 → 上端负端
        d.rectangle((470, y-14, 540, y+14), fill="#ffffff", outline=c, width=3)
        text(d, (505, y-24), "220Ω*", f(14, True), c, anchor="mb")
        if dashed:
            for x in range(380, 470, 16):
                d.line((x, y, min(x+8, 470), y), fill=c, width=4)
            for x in range(540, 920, 16):
                d.line((x, y, min(x+8, 920), y), fill=c, width=4)
        else:
            d.line((380, y, 470, y), fill=c, width=5)
            d.line((540, y, 920, y), fill=c, width=5)

        # ESP32 引脚
        d.rectangle((350, y-20, 380, y+20), fill="#ffffff", outline="#90a4ae", width=2)
        pin = {"DIR": "GPIO5", "PUL": "GPIO4", "ENA": "GPIO2"}[name]
        d.text((344, y), pin, font=f(19, True), fill="#111111", anchor="rm")

        d.text((400, y+16), name + "  " + cn, font=f(18, True), fill=c, anchor="lb")

    for y in (410, 600, 790):
        dot(d, (860, y), bus)

    text(d, (235, 870), "GPIO4 → PUL−（脉冲）", f(16), "#37474f", anchor="ma")
    text(d, (235, 896), "GPIO5 → DIR−（方向）", f(16), "#37474f", anchor="ma")
    text(d, (235, 922), "GPIO2 → ENA−（可选）", f(16), "#37474f", anchor="ma")

    notes = [
        ("说明", True),
        ("• 共阳接法：+5 V 接 PUL+/DIR+/ENA+，GPIO 接对应的 − 端，靠灌电流点亮光耦。", False),
        ("• 220Ω 为可选限流/保护电阻，驱动器内部已有限流，短接也能工作。", False),
        ("• ESP32-S3 为 3.3 V 逻辑，灌电流足够；干扰大时可加 74HCT245、ULN2003 或光耦模块。", False),
        ("• +5 V 与驱动器 AC 侧不共地；光耦本身已实现隔离。", False),
        ("• ENA 悬空 = 默认使能；不同批次有效电平可能相反，首次上电先单步测试。", False),
    ]
    for i, (t, bold) in enumerate(notes):
        d.text((560, 880 + i*30), t, font=f(17, True) if bold else f(15),
               fill="#111111" if bold else "#37474f", anchor="la")

    img.save(path)

# ---------- 图 3：电机绕组接法 ----------'''

s2, n = re.subn(r"# ---------- 图 2：控制信号共阳接法 ----------.*?# ---------- 图 3：电机绕组接法 ----------",
                new_fig2, s, count=1, flags=re.S)
if n != 1:
    miss.append("fig2 block")
s = s2

io.open(p, "w", encoding="utf-8").write(s)
print("miss:", miss)
