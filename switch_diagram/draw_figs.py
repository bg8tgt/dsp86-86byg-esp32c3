# -*- coding: utf-8 -*-
"""SW1 六脚双刀双掷开关 原理图 + 示意图 生成脚本"""
import math
from PIL import Image, ImageDraw, ImageFont

S = 3  # 超采样倍数
FONT = "C:/Windows/Fonts/msyh.ttc"
FONTB = "C:/Windows/Fonts/msyhbd.ttc"

GREEN = (26, 127, 55)
DGREEN = (16, 90, 40)
RED = (198, 40, 40)
GRAY = (150, 150, 150)
LGRAY = (225, 225, 225)
BLUE = (21, 101, 192)
TEXT = (33, 33, 33)
SOFT = (90, 96, 100)


def font(sz, bold=False):
    return ImageFont.truetype(FONTB if bold else FONT, int(sz * S))


class Canvas:
    def __init__(self, w, h, bg="white"):
        self.w, self.h = w, h
        self.img = Image.new("RGB", (w * S, h * S), bg)
        self.d = ImageDraw.Draw(self.img)

    def line(self, pts, color, w=3, dash=None, joint="curve"):
        p = [(x * S, y * S) for x, y in pts]
        if dash is None:
            self.d.line(p, fill=color, width=max(1, int(w * S)), joint=joint)
            return
        for i in range(len(p) - 1):
            self._dash(p[i], p[i + 1], color, w, dash)

    def _dash(self, a, b, color, w, dash):
        dash, gap = dash
        x1, y1 = a
        x2, y2 = b
        dist = math.hypot(x2 - x1, y2 - y1)
        if dist == 0:
            return
        n = int(dist // (dash + gap)) + 1
        for i in range(n):
            s = i * (dash + gap)
            e = min(s + dash, dist)
            if s >= dist:
                break
            pa = (x1 + (x2 - x1) * s / dist, y1 + (y2 - y1) * s / dist)
            pb = (x1 + (x2 - x1) * e / dist, y1 + (y2 - y1) * e / dist)
            self.d.line([pa, pb], fill=color, width=max(1, int(w * S)))

    def circle(self, c, r, fill=None, outline=None, w=2):
        x, y = c
        self.d.ellipse([(x - r) * S, (y - r) * S, (x + r) * S, (y + r) * S],
                       fill=fill, outline=outline, width=max(1, int(w * S)))

    def rrect(self, box, r, fill=None, outline=None, w=2):
        x1, y1, x2, y2 = box
        self.d.rounded_rectangle([x1 * S, y1 * S, x2 * S, y2 * S], radius=r * S,
                                 fill=fill, outline=outline, width=max(1, int(w * S)))

    def rect(self, box, fill=None, outline=None, w=2):
        x1, y1, x2, y2 = box
        self.d.rectangle([x1 * S, y1 * S, x2 * S, y2 * S], fill=fill,
                         outline=outline, width=max(1, int(w * S)))

    def text(self, xy, s, sz=22, color=TEXT, bold=False, anchor="la"):
        self.d.text((xy[0] * S, xy[1] * S), s, font=font(sz, bold), fill=color, anchor=anchor)

    def save(self, path):
        self.img.resize((self.w, self.h), Image.LANCZOS).save(path)
        print("saved", path)


def pin_dot(c, p, color=BLUE, r=8):
    c.circle(p, r, fill="white", outline=color, w=2.2)


# ---------------------------------------------------------------- 图 1 原理图
def figure1(path):
    W, H = 1600, 1045
    c = Canvas(W, H)

    c.text((W / 2, 38), "SW1 六脚双刀双掷开关（DPDT / 2P2T）内部导通原理",
           sz=33, bold=True, anchor="mm", color=DGREEN)
    c.text((W / 2, 88), "两列 = 两组独立触点：1-2-3 为第一组、4-5-6 为第二组；每列的中间脚 2 / 5 是公共端 COM",
           sz=19, anchor="mm", color=SOFT)
    c.text((W / 2, 118), "两刀由同一拨柄机械联动，永远同时切换（不会一刀在 A、另一刀在 B）",
           sz=19, anchor="mm", color=SOFT)

    def panel(x0, title, active_top):
        c.rrect((x0, 165, x0 + 740, 865), 14, fill=(250, 252, 250), outline=LGRAY, w=2)
        c.text((x0 + 26, 182), title, sz=25, bold=True, color=DGREEN)

        y1, y2, y3 = 320, 460, 600  # 1/4、2/5、3/6 三行
        top_y, bot_y = y1 + 45, y3 - 45

        for idx, px in enumerate((x0 + 120, x0 + 460)):
            xc = px + 170   # 触点列
            xp = px + 90    # 刀根（转轴）
            for yy, n in zip((y1, y2, y3), (1 + idx * 3, 2 + idx * 3, 3 + idx * 3)):
                pin_dot(c, (px, yy))
                c.text((px - 22, yy), str(n), sz=21, bold=True, anchor="rm", color=BLUE)
            c.line([(px, y1), (xc, y1), (xc, top_y)], GREEN, 3.5)
            c.line([(px, y3), (xc, y3), (xc, bot_y)], GREEN, 3.5)
            c.line([(px, y2), (xp, y2)], GREEN, 3.5)
            c.circle((xp, y2), 6, fill=GREEN)

            act_y = top_y if active_top else bot_y
            ina_y = bot_y if active_top else top_y
            c.circle((xc, ina_y), 5.5, fill="white", outline=GRAY, w=2)
            c.line([(xp, y2), (xc, ina_y)], GRAY, 2.2, dash=(7, 7))
            c.line([(xp, y2), (xc, act_y)], RED, 5.5)
            c.circle((xc, act_y), 6.5, fill=RED)

            c.text((px - 20, y2 + 24), "COM", sz=14, color=SOFT, anchor="rm")
            c.text((xc + 16, top_y), "A", sz=17, color=SOFT, anchor="lm")
            c.text((xc + 16, bot_y), "B", sz=17, color=SOFT, anchor="lm")

        box = (x0 + 40, 700, x0 + 700, 830)
        c.rrect(box, 10, fill="white", outline=LGRAY, w=2)
        c.text((x0 + 64, 716), "本状态下导通的脚（万用表蜂鸣档会响）：", sz=17, color=SOFT)
        c.text((x0 + 64, 752), "2 — 1　　" if active_top else "2 — 3　　", sz=27, bold=True, color=RED)
        c.text((x0 + 262, 752), "5 — 4" if active_top else "5 — 6", sz=27, bold=True, color=RED)
        c.text((x0 + 64, 792), "另外一对触点断开，对应引脚悬空（高阻）", sz=17, color=SOFT)

    panel(50, "状态 ①　按下 / 拨向 A 端", True)
    panel(810, "状态 ②　松开 / 拨向 B 端", False)

    notes = [
        "① 每一列在任意时刻只有一对触点闭合：6 个脚里始终有 2 个脚悬空，绝不会 3 个脚同时导通。",
        "② 两组触点彼此绝缘（通常 > 100 MΩ），可分别走两个互不相干的回路，也可并联使用。",
        "③ 开关本身没有固定的“常开/常闭”：COM 接电源、另一个脚接负载，接 A 端就是“按下接通”，接 B 端就是“按下断开”。",
    ]
    for i, s in enumerate(notes):
        c.text((60, 892 + i * 42), s, sz=18, color=TEXT)
    c.save(path)


# ---------------------------------------------------------------- 图 2 示意图
def figure2(path):
    W, H = 1600, 1150
    c = Canvas(W, H)

    c.text((W / 2, 40), "SW1 引脚分布与导通对照（示意图）", sz=33, bold=True, anchor="mm", color=DGREEN)
    c.text((W / 2, 88), "俯视元件面：同一列的 3 个脚是一组，中间脚为公共端 COM；拨柄换向时，同列上、下两脚交替与中间脚导通",
           sz=18, anchor="mm", color=SOFT)

    # ---- 上半：引脚分布
    bx1, by1, bx2, by2 = 640, 205, 960, 540
    c.rrect((bx1 - 6, by1 - 6, bx2 + 6, by2 + 6), 10, fill=(247, 251, 247), outline=GREEN, w=3)
    c.rrect((770, 300, 830, 430), 8, fill=(210, 225, 210), outline=DGREEN, w=2)
    for yy in range(320, 420, 14):
        c.line([(776, yy), (824, yy)], DGREEN, 1.4)
    c.text((800, 466), "拨柄", sz=16, color=SOFT, anchor="mm")

    # 拨动方向箭头
    c.line([(712, 300), (712, 240)], SOFT, 2)
    c.line([(712, 240), (701, 253)], SOFT, 2)
    c.line([(712, 240), (723, 253)], SOFT, 2)
    c.text((728, 224), "拨向 A", sz=16, color=SOFT, anchor="lm")
    c.line([(888, 430), (888, 490)], SOFT, 2)
    c.line([(888, 490), (877, 477)], SOFT, 2)
    c.line([(888, 490), (899, 477)], SOFT, 2)
    c.text((874, 512), "拨向 B", sz=16, color=SOFT, anchor="rm")

    rows = [280, 365, 450]
    for i, yy in enumerate(rows):
        c.rrect((600, yy - 12, 632, yy + 12), 4, fill=(235, 243, 255), outline=BLUE, w=2)
        c.text((586, yy), str(i + 1), sz=22, bold=True, anchor="rm", color=BLUE)
        c.rrect((968, yy - 12, 1000, yy + 12), 4, fill=(235, 243, 255), outline=BLUE, w=2)
        c.text((1014, yy), str(i + 4), sz=22, bold=True, anchor="lm", color=BLUE)

    c.line([(556, 262), (556, 468)], GREEN, 2)
    c.line([(556, 262), (568, 262)], GREEN, 2)
    c.line([(556, 468), (568, 468)], GREEN, 2)
    c.text((548, 365), "第一组", sz=17, bold=True, color=GREEN, anchor="rm")
    c.line([(1044, 262), (1044, 468)], GREEN, 2)
    c.line([(1044, 262), (1032, 262)], GREEN, 2)
    c.line([(1044, 468), (1032, 468)], GREEN, 2)
    c.text((1052, 365), "第二组", sz=17, bold=True, color=GREEN, anchor="lm")

    for xx in (616, 984):
        c.text((xx, 244), "A 端", sz=15, color=SOFT, anchor="mm")
        c.text((xx, 484), "B 端", sz=15, color=SOFT, anchor="mm")
    for xx, yy in ((616, 365), (984, 365)):
        c.circle((xx, yy), 22, outline=RED, w=2.2)
        c.text((xx, yy + 34), "COM", sz=15, bold=True, color=RED, anchor="mm")

    # ---- 下半：对照表
    tx1, ty1 = 120, 620
    colw = [190, 300, 300, 300, 190]
    rowh = 60
    header = ["拨柄位置", "2 脚（COM）与谁通", "5 脚（COM）与谁通", "哪两个脚悬空", "状态"]
    rows_data = [
        ["① 拨向 A 端（按下）", "1 脚", "4 脚", "3、6", "A 组接通"],
        ["② 拨向 B 端（松开）", "3 脚", "6 脚", "1、4", "B 组接通"],
    ]
    x = tx1
    for i, h in enumerate(header):
        c.rect((x, ty1, x + colw[i], ty1 + 54), fill=GREEN)
        c.text((x + colw[i] / 2, ty1 + 27), h, sz=18, bold=True, color="white", anchor="mm")
        x += colw[i]
    for r, rd in enumerate(rows_data):
        y = ty1 + 54 + r * rowh
        x = tx1
        for i, v in enumerate(rd):
            fill = (246, 250, 246) if r == 0 else "white"
            c.rect((x, y, x + colw[i], y + rowh), fill=fill, outline=(205, 215, 205), w=1.5)
            col = RED if i in (1, 2) else TEXT
            c.text((x + colw[i] / 2, y + rowh / 2), v, sz=17, bold=(i in (1, 2)), color=col, anchor="mm")
            x += colw[i]

    c.text((tx1, ty1 + 54 + 2 * rowh + 18),
           "结论：任意时刻只有 2 对（4 个脚）导通，另 2 个脚悬空；两组永远同方向（都通 A 或都通 B）。",
           sz=18, color=TEXT)

    # ---- 万用表三步确认
    panel_box = (60, 850, 1540, 1125)
    c.rrect(panel_box, 12, fill=(246, 249, 246), outline=LGRAY, w=2)
    c.text((88, 866), "万用表三步确认法（蜂鸣档 / 通断档；必须先断电，最好把开关拆下或至少断开回路）",
           sz=18, bold=True, color=DGREEN)
    steps = [
        ("①", ["任取一脚，来回拨动拨柄，", "若它与另外两个脚“一个通、", "一个断”交替变化，", "这个脚就是 COM 公共端。"]),
        ("②", ["拨到 A 端，响的两对脚", "记作 COM–A（本例 2-1、5-4）；", "拨到 B 端，另两对记作", "COM–B（本例 2-3、5-6）。"]),
        ("③", ["按实物丝印/焊盘顺序给", "6 个脚编号，与上表核对；", "拨动时两组必须同时动作，", "否则不是 DPDT 结构。"]),
    ]
    for i, (num, lines) in enumerate(steps):
        x0 = 100 + i * 480
        c.circle((x0 + 18, 936), 17, fill=GREEN)
        c.text((x0 + 18, 936), num, sz=18, bold=True, color="white", anchor="mm")
        for j, ln in enumerate(lines):
            c.text((x0 + 46, 910 + j * 28), ln, sz=16, color=TEXT)

    c.save(path)


if __name__ == "__main__":
    figure1(r"E:\deepseek\switch_diagram\SW1_原理图.png")
    figure2(r"E:\deepseek\switch_diagram\SW1_示意图.png")
