# -*- coding: utf-8 -*-
"""生成 Word 版：ESP32-C3 + DSP86 + 86BYG450（AC24V / 4.86A / 400ppr）。"""
import os
from docx import Document
from docx.shared import Pt, Cm, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

BASE = r"E:\deepseek\DSP86_86BYG_ESP32C3"
FONT_CN = "微软雅黑"
FONT_MONO = "Consolas"
BLUE = RGBColor(0x1F, 0x3B, 0x63)
ACCENT = RGBColor(0xC0, 0x39, 0x2B)
GRAY = RGBColor(0x59, 0x59, 0x59)

doc = Document()
sec = doc.sections[0]
sec.page_width = Cm(21.0)
sec.page_height = Cm(29.7)
sec.top_margin = Cm(2.2)
sec.bottom_margin = Cm(2.0)
sec.left_margin = Cm(2.0)
sec.right_margin = Cm(2.0)


def set_cn(run, name=FONT_CN):
    run.font.name = name
    rPr = run._element.get_or_add_rPr()
    rFonts = rPr.find(qn("w:rFonts"))
    if rFonts is None:
        rFonts = OxmlElement("w:rFonts")
        rPr.append(rFonts)
    rFonts.set(qn("w:eastAsia"), name)


normal = doc.styles["Normal"]
normal.font.name = FONT_CN
normal.font.size = Pt(10.5)
normal.element.rPr.rFonts.set(qn("w:eastAsia"), FONT_CN)
normal.paragraph_format.line_spacing = 1.3
normal.paragraph_format.space_after = Pt(4)

for name, size in [("Heading 1", 18), ("Heading 2", 13.5), ("Heading 3", 11.5)]:
    st = doc.styles[name]
    st.font.name = FONT_CN
    st.font.size = Pt(size)
    st.font.bold = True
    st.font.color.rgb = BLUE
    st.element.rPr.rFonts.set(qn("w:eastAsia"), FONT_CN)
    st.paragraph_format.space_before = Pt(16 if name == "Heading 1" else 10)
    st.paragraph_format.space_after = Pt(6)
    st.paragraph_format.keep_with_next = True


def shade(cell, color):
    tcPr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), color)
    tcPr.append(shd)


def cell_text(cell, s, bold=False, size=9, color=None, align=WD_ALIGN_PARAGRAPH.LEFT):
    cell.text = ""
    p = cell.paragraphs[0]
    p.alignment = align
    p.paragraph_format.space_after = Pt(0)
    p.paragraph_format.line_spacing = 1.0
    r = p.add_run(str(s))
    r.bold = bold
    r.font.size = Pt(size)
    if color is not None:
        r.font.color.rgb = color
    set_cn(r)


def table(headers, rows, widths=None, size=9, header_fill="1F3B63", zebra=True):
    t = doc.add_table(rows=1, cols=len(headers))
    t.style = "Table Grid"
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    t.autofit = False
    for i, h in enumerate(headers):
        cell_text(t.rows[0].cells[i], h, bold=True, size=size,
                  color=RGBColor(0xFF, 0xFF, 0xFF), align=WD_ALIGN_PARAGRAPH.CENTER)
        shade(t.rows[0].cells[i], header_fill)
    for ri, row in enumerate(rows):
        cells = t.add_row().cells
        for i, v in enumerate(row):
            al = WD_ALIGN_PARAGRAPH.CENTER if i > 0 and len(str(v)) <= 12 else WD_ALIGN_PARAGRAPH.LEFT
            cell_text(cells[i], v, size=size, align=al)
            if zebra and ri % 2 == 1:
                shade(cells[i], "F2F5F9")
    if widths:
        for i, w in enumerate(widths):
            t.columns[i].width = Cm(w)
        for row in t.rows:
            for i, w in enumerate(widths):
                row.cells[i].width = Cm(w)
    trPr = t.rows[0]._tr.get_or_add_trPr()
    th = OxmlElement("w:tblHeader")
    th.set(qn("w:val"), "true")
    trPr.append(th)
    for row in t.rows:
        row._tr.get_or_add_trPr().append(OxmlElement("w:cantSplit"))
    doc.add_paragraph().paragraph_format.space_after = Pt(2)
    return t


def para(s, bold=False, size=10.5, color=None, align=None, space_after=4):
    p = doc.add_paragraph()
    if align is not None:
        p.alignment = align
    p.paragraph_format.space_after = Pt(space_after)
    r = p.add_run(s)
    r.bold = bold
    r.font.size = Pt(size)
    if color is not None:
        r.font.color.rgb = color
    set_cn(r)
    return p


def label(s, size=9.5):
    p = para(s, size=size, color=GRAY)
    p.paragraph_format.keep_with_next = True
    return p


def bullet(s, size=10.5):
    p = doc.add_paragraph(style="List Bullet")
    p.paragraph_format.space_after = Pt(2)
    p.paragraph_format.line_spacing = 1.25
    r = p.add_run(s)
    r.font.size = Pt(size)
    set_cn(r)


def numbered(s, n, size=10.5):
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(2)
    p.paragraph_format.line_spacing = 1.25
    p.paragraph_format.left_indent = Cm(0.9)
    p.paragraph_format.first_line_indent = Cm(-0.65)
    r = p.add_run(f"{n}.  " + s)
    r.font.size = Pt(size)
    set_cn(r)


def code(text, size=8.0):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(4)
    p.paragraph_format.space_after = Pt(8)
    p.paragraph_format.line_spacing = 1.0
    p.paragraph_format.left_indent = Cm(0.3)
    p.paragraph_format.keep_together = True
    pPr = p._p.get_or_add_pPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), "F4F6F8")
    pPr.append(shd)
    lines = text.strip("\n").split("\n")
    for i, line in enumerate(lines):
        r = p.add_run(line)
        r.font.name = FONT_MONO
        r.font.size = Pt(size)
        r._element.get_or_add_rPr().rFonts.set(qn("w:eastAsia"), FONT_CN)
        if i < len(lines) - 1:
            r.add_break()


def caption(s):
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_before = Pt(2)
    p.paragraph_format.space_after = Pt(10)
    r = p.add_run(s)
    r.italic = True
    r.font.size = Pt(9)
    r.font.color.rgb = GRAY
    set_cn(r)


def picture(name, width=17, cap=None):
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_before = Pt(4)
    p.paragraph_format.space_after = Pt(0)
    p.add_run().add_picture(os.path.join(BASE, name), width=Cm(width))
    if cap:
        caption(cap)


NEXT_BREAK = [False]


def page_break():
    NEXT_BREAK[0] = True


def h1(s):
    h = doc.add_heading(s, level=1)
    if NEXT_BREAK[0]:
        h.paragraph_format.page_break_before = True
        NEXT_BREAK[0] = False
    return h


# ---------------- 封面 ----------------
for _ in range(2):
    doc.add_paragraph()
p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
r = p.add_run("ESP32-C3 + DSP86 + 86BYG450")
r.bold = True
r.font.size = Pt(29)
r.font.color.rgb = BLUE
set_cn(r)

p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
r = p.add_run("步进电机接线与调试手册")
r.bold = True
r.font.size = Pt(22)
r.font.color.rgb = BLUE
set_cn(r)

p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_before = Pt(16)
r = p.add_run("AC 24 V · 4.86 A · 400 步/转（0.9°） · 共阳控制")
r.font.size = Pt(12)
r.font.color.rgb = ACCENT
set_cn(r)

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(8)
pPr = p._p.get_or_add_pPr()
pBdr = OxmlElement("w:pBdr")
bottom = OxmlElement("w:bottom")
bottom.set(qn("w:val"), "single")
bottom.set(qn("w:sz"), "12")
bottom.set(qn("w:space"), "1")
bottom.set(qn("w:color"), "1F3B63")
pBdr.append(bottom)
pPr.append(pBdr)

info = doc.add_table(rows=0, cols=2)
info.alignment = WD_TABLE_ALIGNMENT.CENTER
for k, v in [
    ("驱动器", "DSP86（上海四宏），AC 18–80 V，2.0–6.0 A"),
    ("电机", "86BYG450-79-A50-J5，1.8°，5.0 A，4 线（黄/蓝、红/绿）"),
    ("电源", "220 V / 24 VAC 隔离变压器，≥300 VA"),
    ("控制板", "ESP32-C3，STEP=G4 / DIR=G5 / ENA=G6"),
    ("拨码", "4.86 A · 400 步/转 · 0.9° · 半流待机"),
    ("I/O", "GPIO1=限位，GPIO3=俯仰角 ADC（-30°~+70°）"),
]:
    row = info.add_row().cells
    cell_text(row[0], k, bold=True, size=9.5, color=BLUE)
    cell_text(row[1], v, size=9.5)
    row[0].width = Cm(3.2)
    row[1].width = Cm(12.8)

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(12)
picture("01_system_wiring.png", width=16.0)

para("提示：端子排列与拨码位置以驱动器侧面铭牌为准；4.86 A 的适用性见 5.3 节。",
     size=9, color=ACCENT, align=WD_ALIGN_PARAGRAPH.CENTER)
page_break()

# ---------------- 1 ----------------
h1("1  先确认三件事")
doc.add_heading("① DSP86 是交流输入", level=2)
para("铭牌为 VAC: 18V~80V。本系统使用 220 V / 24 VAC 隔离变压器，不能接 24 V 直流开关电源，更不能把 220 V 直接接到 AC 端子。")
para("24 VAC 整流后母线约 32–34 VDC，处于允许范围下限，可正常工作，但高速转矩会明显下降（见 5.4 节）。", color=ACCENT)

doc.add_heading("② 4.86 A 是否合适，取决于电机接法", level=2)
table(
    ["电机接法", "每相额定", "4.86 A 占比", "结论"],
    [
        ["4 线（5.0 A，本机）", "5.0 A", "97%", "可用（当前）"],
        ["4 线（5.9 A）", "5.9 A", "82%", "可用"],
        ["8 线并联", "≈7.0 A", "69%", "可用"],
        ["8 线串联", "≈3.5 A", "139%", "禁止，改 3.71 A"],
    ],
    widths=[4.2, 3.6, 3.6, 5.6], size=9,
)

doc.add_heading("③ 400 步/转是半步，精度较粗", level=2)
para("400 步/转 = 0.9°/步，低速转矩大、脉冲频率低，但分辨率粗、1–2 rps 易共振。振动大或定位不够时改 1600 步/转（0.225°）。")

# ---------------- 2 ----------------
h1("2  安全须知")
for s in [
    "高压侧为 AC 24 V，虽属低压，但由 220 V 变压器供电，一次侧仍然危险；接线必须断电。",
    "改拨码、插拔电机线前断电，等待驱动器电容放电至少 1 分钟。",
    "电机线严禁带电插拔，反电动势可能击穿功率管。",
    "驱动器外壳、电机外壳、变压器铁芯、电控箱体必须接 PE。",
    "一次侧加 5 A 慢断保险丝，二次侧加 12 A 慢断保险丝。",
    "急停开关切断变压器一次侧 220 V，而不是只关控制信号。",
]:
    bullet(s)

# ---------------- 3 ----------------
h1("3  系统组成")
table(
    ["序号", "部件", "规格 / 型号", "数量", "备注"],
    [
        ["1", "控制板", "ESP32-C3 开发板", "1", "USB-CDC，3.3 V 逻辑"],
        ["2", "驱动器", "上海四宏 DSP86", "1", "AC 18–80 V，2.0–6.0 A"],
        ["3", "电机", "86BYG450-79-A50-J5", "1", "4 线（黄/蓝、红/绿），1.8°，5.0 A"],
        ["4", "隔离变压器", "220 V → 24 VAC，≥300 VA", "1", "24 V 侧约 12.5 A"],
        ["5", "断路器/保险丝", "2P + 5 A 慢断；二次侧 12 A", "1 套", "—"],
        ["6", "急停", "常闭 10 A", "1", "切断 220 V"],
        ["7", "电机电缆", "4 芯屏蔽 1.0 mm²", "若干", "屏蔽单端接 PE"],
        ["8", "信号电缆", "4 芯屏蔽 0.2 mm²", "若干", "与电机线分开"],
    ],
    widths=[1.2, 3.0, 6.2, 1.4, 5.2], size=9,
)

# ---------------- 4 ----------------
h1("4  DSP86 参数与拨码")
doc.add_heading("4.1  主要参数", level=2)
table(
    ["项目", "参数", "说明"],
    [
        ["输入电源", "AC 18–80 V（50/60 Hz）", "本系统 24 VAC"],
        ["输出电流", "2.0–6.0 A（REF），峰值 7.2 A", "8 档拨码"],
        ["控制方式", "脉冲 + 方向 + 使能", "差分光耦"],
        ["最大脉冲频率", "200 kHz", "厂家标称"],
        ["细分", "400–51200 步/转，16 档", "SW5–SW8"],
        ["信号输入", "5–15 mA，TTL 兼容", "3.3 V 可驱动"],
        ["体积", "151 × 97 × 48 mm", "不含端子"],
    ],
    widths=[3.2, 7.4, 6.4], size=9,
)

doc.add_heading("4.2  端子定义", level=2)
table(
    ["端子", "含义", "接法"],
    [
        ["PUL+ / PUL−", "脉冲输入", "+ 接 5 V，− 接 GPIO4"],
        ["DIR+ / DIR−", "方向输入", "+ 接 5 V，− 接 GPIO5"],
        ["ENA+ / ENA−", "使能输入", "可悬空；接入时 − 接 GPIO6"],
        ["AC / AC", "交流电源", "24 VAC"],
        ["A+ / A−", "电机 A 相", "接电机 A 相"],
        ["B+ / B−", "电机 B 相", "接电机 B 相"],
    ],
    widths=[4.0, 4.6, 8.4], size=9,
)

doc.add_heading("4.3  电流拨码（SW1–SW3）", level=2)
table(
    ["REF 电流", "峰值", "SW1", "SW2", "SW3", "用途"],
    [
        ["2.00 A", "2.40 A", "on", "on", "on", ""],
        ["2.57 A", "3.08 A", "off", "on", "on", ""],
        ["3.14 A", "3.77 A", "on", "off", "on", ""],
        ["3.71 A", "4.45 A", "off", "off", "on", "8 线串联"],
        ["4.28 A", "5.14 A", "on", "on", "off", ""],
        ["4.86 A", "5.83 A", "off", "on", "off", "当前设定"],
        ["5.43 A", "6.52 A", "on", "off", "off", ""],
        ["6.00 A", "7.20 A", "off", "off", "off", ""],
    ],
    widths=[3.0, 3.0, 2.4, 2.4, 2.4, 4.0], size=9,
)

doc.add_heading("4.4  细分拨码（SW5–SW8）", level=2)
table(
    ["脉冲/转", "SW5", "SW6", "SW7", "SW8", "步距角（1.8°）"],
    [
        ["400", "on", "on", "on", "on", "0.9°（当前）"],
        ["800", "off", "on", "on", "on", "0.45°"],
        ["1600", "on", "off", "on", "on", "0.225°"],
        ["3200", "off", "off", "on", "on", "0.1125°"],
        ["6400", "on", "on", "off", "on", "0.05625°"],
        ["12800", "off", "on", "off", "on", "0.028125°"],
        ["25600", "on", "off", "off", "on", "0.0140625°"],
        ["51200", "off", "off", "off", "on", "0.00703125°"],
        ["1000", "on", "on", "on", "off", "—"],
        ["2000", "off", "on", "on", "off", "—"],
        ["4000", "on", "off", "on", "off", "—"],
        ["5000", "off", "off", "on", "off", "—"],
        ["8000", "on", "on", "off", "off", "—"],
        ["10000", "off", "on", "off", "off", "—"],
        ["20000", "on", "off", "off", "off", "—"],
        ["40000", "off", "off", "off", "off", "—"],
    ],
    widths=[2.8, 2.2, 2.2, 2.2, 2.2, 4.2], size=9,
)
para("SW4：off = 半流，on = 全流。4.86 A 较大，建议 off 降低待机发热。", size=9.5)

doc.add_heading("4.5  当前拨码汇总", level=2)
table(
    ["SW1", "SW2", "SW3", "SW4", "SW5", "SW6", "SW7", "SW8"],
    [["off", "on", "off", "off", "on", "on", "on", "on"]],
    widths=[2.0] * 8, size=9,
)
para("→ 4.86 A（峰值 5.83 A）/ 400 步/转 / 0.9° / 半流待机。", bold=True)

# ---------------- 5 ----------------
h1("5  86BYG450 电机")
doc.add_heading("5.1  铭牌参数", level=2)
table(
    ["项目", "参数", "项目", "参数"],
    [
        ["机座", "86 mm（NEMA34）", "步距角", "1.8°（整步）"],
        ["机身长", "79 mm", "额定电流", "5.0 A"],
        ["保持转矩", "≈5.5 N·m", "引出线", "4 线（黄/蓝、红/绿）"],
        ["轴径", "12.7 / 14 mm", "重量", "≈3.5 kg"],
        ["绝缘", "B", "温升", "≤80 K"],
    ],
    widths=[3.0, 5.4, 3.2, 5.4], size=9,
)
para("400 步/转时，系统步距角为 0.9°（整步 1.8° 的半步模式）。", size=9.5)

doc.add_heading("5.2  线色、相序与接法", level=2)
para("4 线电机只有两相，没有串联/并联可选。先实测相组，再按示例接：", size=10)
table(
    ["线色", "示例接法", "判断依据"],
    [
        ["蓝", "A+", "与黄同相，实测 0.5 Ω"],
        ["黄", "A−", "与蓝同相"],
        ["红", "B+", "与绿同相，实测 0.2 Ω"],
        ["绿", "B−", "与红同相"],
    ],
    widths=[3.0, 3.4, 10.2], size=9,
)
for s in [
    "实测：黄-蓝 0.5 Ω，红-绿 0.2 Ω；其余组合不通。",
    "0.2 Ω 偏低，先短接表笔归零复测；若仍是 0.2 Ω，该相损耗更大，注意温升。",
    "上电只振动不转：交换 A+/A−（黄蓝对调）或 B+/B−（红绿对调）。",
    "若实际是 8 线（还有 4 根），按图 3 串联/并联接；串联电流取 3.71 A。",
]:
    bullet(s)
picture("03_motor_wiring.png", width=16.0, cap="图 3  4 线 / 8 线绕组接法")

doc.add_heading("5.3  4.86 A 的电流校验", level=2)
table(
    ["接法", "每相额定", "4.86 A 占比", "判定"],
    [
        ["4 线 5.0 A（本机）", "5.0 A", "97%", "可用（当前）"],
        ["4 线 5.9 A", "5.9 A", "82%", "可用"],
        ["8 线并联", "≈7.0 A", "69%", "可用"],
        ["8 线串联", "≈3.5 A", "139%", "禁止；改 3.71 A"],
    ],
    widths=[4.6, 3.4, 3.4, 5.6], size=9,
)
para("4.86 A 对 5.0 A 铭牌电机是 97%，可长期运行；仍建议先跑 10 分钟测温升。",
     size=9.5, color=ACCENT)

doc.add_heading("5.4  24 VAC 供电的影响", level=2)
para("24 VAC 是 DSP86 允许范围的低端，能用，但有两个限制：", size=10)
for s in [
    "高速转矩下降：母线约 34 VDC，转速越高实际电流越小。400 步/转时建议 ≤200–300 rpm（约 1300–2000 Hz）。",
    "加速能力变弱：加速度从 800–1600 步/s² 起步，不要直接给高频。",
]:
    bullet(s)
para("如需 400–600 rpm 带载，换 48 VAC / 300 VA 变压器即可（拨码与接线不变）；或改用闭环步进/伺服。",
     size=9.5)
table(
    ["项目", "建议"],
    [
        ["变压器", "220 V / 24 VAC，≥300 VA（24 V 侧约 12.5 A）"],
        ["二次侧保险丝", "12 A 慢断"],
        ["二次侧导线", "1.5 mm² 以上，尽量短"],
        ["母线电压", "空载约 34 VDC，加载 30–32 VDC 正常"],
    ],
    widths=[4.4, 12.2], size=9,
)

# ---------------- 6 ----------------
h1("6  接线步骤")
doc.add_heading("6.1  系统接线总图", level=2)
picture("01_system_wiring.png", width=17, cap="图 1  ESP32-C3 + DSP86 + 86BYG450 系统接线")

doc.add_heading("6.2  电源侧（AC 24 V）", level=2)
code("""220 VAC (L/N/PE)
   │
   ├─ 断路器 2P ── 5 A 慢断保险丝 ── 急停
   │
   └─ 隔离变压器 220 V / 24 VAC（≥300 VA，12.5 A）
            │
            └── DSP86  AC ── AC""")
for s in [
    "变压器次级不分极性，两根线接 DSP86 的两个 AC 端子。",
    "必须用隔离变压器；自耦调压器不能替代。",
    "二次侧 12 A 慢断保险丝，导线 ≥1.5 mm²。",
    "PE 接驱动器外壳、电机外壳、变压器铁芯、电控箱体。",
]:
    bullet(s)

doc.add_heading("6.3  电机侧", level=2)
for s in [
    "4 线：蓝 A+、黄 A−、红 B+、绿 B−。",
    "8 线串联：A+ → A1 → A2 → A−，B+ → B1 → B2 → B−。",
    "8 线并联：A1/A2 首端并接、尾端并接。",
    "相序错会“嗡嗡响、只振动不转”；交换 A+ / A−。",
]:
    bullet(s)

doc.add_heading("6.4  控制信号（共阳）", level=2)
picture("02_signal_wiring.png", width=17, cap="图 2  ESP32-C3 共阳控制信号接线")
table(
    ["ESP32-C3", "DSP86", "说明"],
    [
        ["5V", "PUL+、DIR+", "共阳，+ 端并接 5 V"],
        ["GPIO4", "PUL−", "脉冲"],
        ["GPIO5", "DIR−", "方向"],
        ["GPIO6", "ENA−", "可悬空"],
        ["GPIO1", "—", "限位/急停输入"],
        ["GPIO3", "—", "俯仰角 ADC（0–3.3 V）"],
        ["GND", "不接", "光耦隔离"],
    ],
    widths=[3.6, 4.4, 9.0], size=9,
)
para("GPIO 低电平 → 光耦导通 → 通道有效。方向相反时交换 A+/A− 或修改 DIR_INVERT。", size=9.5)

doc.add_heading("6.5  接线检查表", level=2)
for s in [
    "AC 两端不短路（断电测量）。",
    "电机同相导通、异相不通。",
    "A/B 相未接反。",
    "+5 V 未接到 PUL− / DIR−。",
    "驱动器、电机、变压器、箱体接 PE。",
    "电机线与信号线分开走。",
    "拨码方向与丝印一致（on 侧）。",
]:
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(2)
    p.paragraph_format.left_indent = Cm(0.4)
    r = p.add_run("☐  " + s)
    r.font.size = Pt(10)
    set_cn(r)

# ---------------- 7 ----------------
h1("7  ESP32-C3 引脚与程序")
doc.add_heading("7.1  引脚规划", level=2)
table(
    ["类别", "GPIO", "说明"],
    [
        ["推荐普通 IO", "0、1、3、4、5、6、7、10、20、21", "可自由使用"],
        ["启动绑带", "2、8、9", "上电电平影响启动"],
        ["USB", "18、19", "USB D−/D+，勿占用"],
        ["Flash", "11–17", "模组内部占用"],
        ["UART0", "20、21", "板载串口日志"],
    ],
    widths=[3.6, 7.2, 5.8], size=8.5,
)
table(
    ["功能", "GPIO", "备注"],
    [
        ["STEP / PUL", "4", "安全 IO"],
        ["DIR", "5", "安全 IO"],
        ["ENA", "6", "可选"],
        ["LIMIT / 急停", "1", "输入，内部上拉"],
        ["PITCH 俯仰角", "3", "ADC1_CH3，0–3.3 V"],
    ],
    widths=[4.4, 2.6, 9.6], size=9,
)
for s in [
    "3.3 V 逻辑，灌电流约 10 mA/路，可直接驱动光耦。",
    "使用原生 USB 串口时，Arduino IDE 勾选 USB CDC On Boot: Enabled。",
    "GPIO18/19 留给 USB；GPIO11–17 被 Flash 占用。"
    "GPIO2/8/9 是启动绑带脚，不要接会拉低的外部电路。",
]:
    bullet(s)

doc.add_heading("7.2  例程（FastAccelStepper）", level=2)
code("""#include <FastAccelStepper.h>

#define PIN_STEP   4
#define PIN_DIR    5
#define PIN_ENA    6
#define PIN_LIMIT  1
#define PIN_PITCH  3

#define DIR_INVERT     true
#define ENA_ACTIVE_LOW true
#define STEPS_PER_REV  400

const float PITCH_V_MIN = 0.50, PITCH_V_MAX = 3.00;
const float PITCH_ANGLE_MIN = -45.0, PITCH_ANGLE_MAX = 45.0;

FastAccelStepperEngine engine = FastAccelStepperEngine();
FastAccelStepper *stepper = nullptr;
float pitchAngle = 0, pitchVolt = 0;
bool limitHit = false;

void setEnabled(bool en) {
  digitalWrite(PIN_ENA, (en == ENA_ACTIVE_LOW) ? LOW : HIGH);
}

float readPitch() {
  pitchVolt = analogReadMilliVolts(PIN_PITCH) / 1000.0;
  float a = PITCH_ANGLE_MIN + (pitchVolt - PITCH_V_MIN) *
            (PITCH_ANGLE_MAX - PITCH_ANGLE_MIN) / (PITCH_V_MAX - PITCH_V_MIN);
  if (a < PITCH_ANGLE_MIN) a = PITCH_ANGLE_MIN;
  if (a > PITCH_ANGLE_MAX) a = PITCH_ANGLE_MAX;
  return pitchAngle = a;
}""")
label("初始化（setup）：")
code("""void setup() {
  Serial.begin(115200);
  pinMode(PIN_LIMIT, INPUT_PULLUP);
  pinMode(PIN_ENA, OUTPUT);
  setEnabled(true);
  analogReadResolution(12);
  analogSetAttenuation(ADC_11db);

  engine.init();
  stepper = engine.stepperConnectToPin(PIN_STEP);
  stepper->setDirectionPin(PIN_DIR, DIR_INVERT);
  stepper->setSpeedInHz(800);
  stepper->setAcceleration(1600);
  stepper->setCurrentPosition(0);
}""")
label("主循环与命令解析（loop）：")
code("""void loop() {
  static uint32_t t = 0;
  if (millis() - t > 100) { t = millis(); readPitch(); }

  limitHit = (digitalRead(PIN_LIMIT) == LOW);
  if (limitHit && stepper->isRunning()) {
    stepper->forceStop();
    setEnabled(false);
    Serial.println("LIMIT stop");
  }

  if (!Serial.available()) return;
  String line = Serial.readStringUntil('\\n');
  line.trim();
  if (!line.length()) return;

  char cmd = line[0];
  long val = line.substring(1).toInt();

  switch (cmd) {
    case 'm':
      if (limitHit) { Serial.println("limit active"); break; }
      stepper->move(val);
      break;
    case 's': if (val > 0) stepper->setSpeedInHz(val); break;
    case 'a': if (val > 0) stepper->setAcceleration(val); break;
    case 'z': stepper->setCurrentPosition(0); break;
    case 'p': Serial.printf("pitch %.2f deg (%.3f V)\\n", readPitch(), pitchVolt); break;
    case '?':
      Serial.printf("pos=%ld run=%d limit=%d pitch=%.2f\\n",
                    stepper->getCurrentPosition(), stepper->isRunning(),
                    limitHit, pitchAngle);
      break;
    default: Serial.println("m s a z p ?");
  }
}""")
para("测试：s800 → m400 应转 1 圈；m-400 反向；再试 s1600、s2000。", size=9.5)

doc.add_heading("7.3  限位/急停与俯仰角", level=2)
picture("04_io_wiring.png", width=17, cap="图 4  限位/急停与俯仰角传感器接线")
para("限位/急停（GPIO1）", bold=True, size=10)
for s in [
    "开关一端接 GPIO1，另一端接 GND；GPIO1 内部上拉。",
    "常开：闭合 = 低电平 = 触发；代码 limitHit = (digitalRead(PIN_LIMIT)==LOW)。",
    "安全场合建议用常闭触点：正常低、按下或断线变高；此时改判 HIGH。",
    "急停主回路仍必须切断 220 V，GPIO1 只做状态检测。",
]:
    bullet(s)
para("俯仰角（GPIO3 / ADC1_CH3）", bold=True, size=10)
for s in [
    "只接受 0–3.3 V；0–5 V 传感器用两个 10 kΩ 电阻 1:1 分压。",
    "OUT 串 1 kΩ，ADC 节点对 GND 接 100 nF，抑制电机干扰。",
    "analogReadMilliVolts() 直接读电压，按标定换算角度。",
    "传感器电源取 3.3 V，GND 与 ESP32-C3 共地；模拟线远离电机线。",
]:
    bullet(s)
para("标定：测传感器在最小/最大角度时的 OUT 电压，填入 PITCH_V_MIN/MAX 与角度范围。", size=9.5)

doc.add_heading("7.4  无库自检", level=2)
code("""#define PIN_STEP 4
#define PIN_DIR  5

void setup() {
  pinMode(PIN_DIR, OUTPUT);
  digitalWrite(PIN_DIR, HIGH);

  ledcSetup(0, 400, 10);      // 400 Hz = 60 rpm
  ledcAttachPin(PIN_STEP, 0);
  ledcWriteTone(0, 400);
}

void loop() {}""")

doc.add_heading("7.5  俯仰闭环控制（-30°～+70°）", level=2)
picture("05_gimbal_3d.png", width=17, cap="图 5  俯仰云台 3D 示意与角度行程")
para("俯仰角传感器（或俯仰角测试仪输出）接 GPIO3 / ADC1_CH3，GND 共地，VCC 取 3.3 V；输出必须 0–3.3 V，5 V 传感器需分压。")
table(
    ["项目", "参数", "说明"],
    [
        ["软件行程", "-30° ~ +70°", "俯角 30°，仰角 70°"],
        ["机械硬限位", "-32° / +72°", "建议加机械挡块"],
        ["Kp", "3 步/°", "误差 10° 对应 30 步/周期"],
        ["死区", "0.3°", "抑制传感器噪声抖动"],
        ["控制周期", "50 ms", "20 Hz 闭环"],
        ["单周期最大步数", "80 步", "约 1600 步/s 上限"],
        ["传感器故障", "<0.05 V 或 >3.25 V", "立即停机并禁止闭环"],
    ],
    widths=[4.0, 5.2, 7.4], size=9,
)
para("校准步骤", bold=True, size=10)
for s in [
    "手动把云台摇到 -30°，网页“俯仰”页点“记录下限 -30°”。",
    "手动摇到 +70°，点“记录上限 +70°”。",
    "两点电压差需大于 0.3 V，否则检查传感器或接线。",
]:
    bullet(s)
para("稳定性建议", bold=True, size=10)
for s in [
    "俯仰轴加配重，重心尽量过轴，减小保持力矩和超调。",
    "加速度 800–1600 步/s²，最大速度 ≤2000 Hz；背隙大时 Kp 降到 2–3、死区加到 0.5°。",
    "机械硬限位 + 软件限位双保险；传感器断线或超程立即停机。",
    "需要断电自锁时用蜗轮蜗杆/自锁减速机。",
]:
    bullet(s)

# ---------------- 8 ----------------
h1("8  上电与调试")
for i, s in enumerate([
    "断开负载，拨码按 4.5 节设置。",
    "只接电源上电：PWR 灯亮，不报故障。",
    "断电接电机，再上电：轴应有保持力矩。",
    "断电接信号线，上电。",
    "先 400 Hz（60 rpm）低速运行，确认方向。",
    "逐步升到 1600–2000 Hz，观察失步与共振。",
    "运行 10 分钟测电机温升，超 80 K 降电流档。",
], 1):
    numbered(s, i)

para("400 步/转的共振点通常在 1–2 转/秒（400–800 Hz）附近；振动明显时避开该区间，或改 1600 步/转（SW5=on SW6=off SW7=on SW8=on）。",
     size=9.5, color=ACCENT)

# ---------------- 9 ----------------
h1("9  脉冲当量与速度")
code("""脉冲当量 = 导程 / 脉冲每转          [mm/step]
转速     = 脉冲频率 / 脉冲每转 × 60  [rpm]""")
table(
    ["脉冲频率", "转速（400 步/转）", "24 VAC 带载参考"],
    [
        ["400 Hz", "60 rpm", "轻松"],
        ["800 Hz", "120 rpm", "正常"],
        ["1600 Hz", "240 rpm", "可用"],
        ["2000 Hz", "300 rpm", "接近上限"],
        ["4000 Hz", "600 rpm", "空载/轻载才可能"],
    ],
    widths=[4.0, 5.6, 7.0], size=9,
)
para("5 mm 导程、400 步/转：脉冲当量 = 5/400 = 12.5 µm/step；改 1600 步/转则为 3.125 µm。", size=9.5)

# ---------------- 10 ----------------
h1("10  故障排查")
table(
    ["现象", "可能原因", "处理"],
    [
        ["上电无指示", "无 AC、保险丝断、变压器无输出", "测 AC 端子电压"],
        ["电机松轴", "ENA 极性相反、缺相", "断开 ENA 试；查电机线"],
        ["有力矩不转", "无脉冲、PUL 接错", "自检程序发 400 Hz"],
        ["只振动、嗡嗡响", "相序错、电流小、加速太快", "交换 A+/A−；提电流；降加速度"],
        ["方向相反", "DIR 逻辑相反", "交换 A+/A− 或改 DIR_INVERT"],
        ["低速共振", "400 步/转半步模式", "避开共振区或改 1600"],
        ["高速失步", "加速太快、24 V 电压低", "降加速度；必要时换 48 VAC"],
        ["电机过热", "电流档过高、串联接法", "串联改 3.71 A；开半流"],
        ["随机丢步", "干扰、信号与电机线并行", "屏蔽、单端接地、分开走线"],
    ],
    widths=[3.8, 6.4, 6.8], size=8.5,
)

# ---------------- 11 ----------------
h1("11  参考资料")
for name, url in [
    ("上海四宏 DSP86 产品页", "https://www.sihengmotor.com/product/dsp86/"),
    ("DSP86 资料与尺寸图", "https://www.shyl-technology.com/product/dsp86-stepper-motor-driver-for-nema34-motor/"),
    ("86BYG 系列参数（Mecheltron）", "https://mecheltron.eu/en/product/86byg-stepping-motor"),
    ("86BYGH450B 参数（5 A / 5.88 N·m）", "https://www.eaglecnctec.com/wap/products/155-stepping-motor-86bygh450b.html"),
    ("ESP32-C3 datasheet（绑带引脚）", "https://www.espressif.com/sites/default/files/documentation/esp32-c3_datasheet_cn.pdf"),
    ("FastAccelStepper（支持 C3）", "https://github.com/gin66/FastAccelStepper"),
]:
    p = doc.add_paragraph(style="List Bullet")
    p.paragraph_format.space_after = Pt(2)
    r = p.add_run(name + "：")
    r.font.size = Pt(9.5)
    set_cn(r)
    r2 = p.add_run(url)
    r2.font.size = Pt(9)
    r2.font.color.rgb = RGBColor(0x1F, 0x4E, 0x79)
    r2.font.name = FONT_MONO

para("参数来自公开资料与铭牌照片，不同批次可能不同；以实物为准。", size=8.5, color=GRAY)

# ---------------- 附录 ----------------
h1("附录 A  一分钟速查")
code("""220VAC ── 断路器 ── 保险丝 ── 急停 ── 变压器(24VAC) ── DSP86 AC/AC
DSP86 A+ / A- / B+ / B-  →  电机对应相
ESP32-C3 5V  → PUL+ / DIR+（共阳）
ESP32-C3 G4  → PUL-
ESP32-C3 G5  → DIR-
ESP32-C3 G6  → ENA-（可悬空）
ESP32-C3 G1  → 限位/急停（到 GND）
ESP32-C3 G3  → 俯仰角传感器（0–3.3 V）

拨码：off on off | off | on on on on
     4.86A      半流   400 步/转（0.9°）""")

h1("附录 B  可用固件经验（C3BUJINTEST）")
para("参考工程：C:\\Users\\Administrator\\Documents\\aily-project\\C3BUJINTEST，已在 COM6 / CH340 上实测可用。", size=9.5)
para("ENA 是“脱机”信号（最容易接反）", bold=True, size=10)
for s in [
    "DSP86 光耦导通（GPIO 低）= 电机自由；光耦断开（GPIO 高）= 使能保持力矩。",
    "默认 enaActiveLow = false：使能时 GPIO6 输出高，脱机时输出低。",
    "限位/急停触发后停机 + 脱机，排除故障后手动恢复使能。",
]:
    bullet(s)
para("WiFi 稳定配置", bold=True, size=10)
for s in [
    "WiFi.setSleep(false)，避免网页高频轮询时间歇断连。",
    "WiFi.setTxPower(WIFI_POWER_19_5dBm)；WiFi.setAutoReconnect(true)。",
    "热点 DSP86-C3 / 12345678，信道 6，最多 4 个客户端。",
    "网页配网先回 HTTP 响应，再延迟切换新凭据。",
]:
    bullet(s)
para("OTA", bold=True, size=10)
for s in [
    "Update.begin((ESP.getFreeSketchSpace()-0x1000)&0xFFFFF000)。",
    "升级前停电机并脱机；响应头 Connection: close；成功后延时重启。",
]:
    bullet(s)
para("其他", bold=True, size=10)
for s in [
    "未用 GPIO0/7/10 拉低；GPIO2/9 为启动绑带脚，保持输入悬空。",
    "板载 LED GPIO8：常亮=STA 已联网，300 ms 闪烁=未联网。",
    "串口 115200：m<步> r<±1> x s<Hz> a<步/s²> n<步/转> z e d c p i ?。",
]:
    bullet(s)

# ---------------- 页脚 ----------------
footer = sec.footer
fp = footer.paragraphs[0]
fp.alignment = WD_ALIGN_PARAGRAPH.CENTER
run = fp.add_run()
fld1 = OxmlElement("w:fldChar"); fld1.set(qn("w:fldCharType"), "begin")
instr = OxmlElement("w:instrText"); instr.set(qn("xml:space"), "preserve"); instr.text = "PAGE"
fld2 = OxmlElement("w:fldChar"); fld2.set(qn("w:fldCharType"), "end")
run._r.append(fld1)
run._r.append(instr)
run._r.append(fld2)
run.font.size = Pt(9)
run.font.color.rgb = GRAY

doc.core_properties.title = "ESP32-C3 + DSP86 + 86BYG450 接线与调试手册"
doc.core_properties.subject = "ESP32-C3 / AC24V / 4.86A / 400ppr"
doc.core_properties.author = "DSH"

out = os.path.join(BASE, "ESP32C3_DSP86_86BYG450_接线与调试手册.docx")
doc.save(out)
print("saved:", out, os.path.getsize(out), "bytes")






