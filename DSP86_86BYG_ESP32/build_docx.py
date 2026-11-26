# -*- coding: utf-8 -*-
"""生成 Word 版《ESP32 + DSP86 + 86BYG450 接线与调试手册》。"""
import os
from docx import Document
from docx.shared import Pt, Cm, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.section import WD_SECTION
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

BASE = r"E:\deepseek\DSP86_86BYG_ESP32"
FONT_CN = "微软雅黑"
FONT_MONO = "Consolas"
BLUE = RGBColor(0x1F, 0x3B, 0x63)
ACCENT = RGBColor(0xC0, 0x39, 0x2B)
GRAY = RGBColor(0x59, 0x59, 0x59)

doc = Document()

# ---------- 页面 ----------
sec = doc.sections[0]
sec.page_width = Cm(21.0)
sec.page_height = Cm(29.7)
sec.top_margin = Cm(2.2)
sec.bottom_margin = Cm(2.0)
sec.left_margin = Cm(2.0)
sec.right_margin = Cm(2.0)

# ---------- 样式 ----------
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

for name, size, color in [("Heading 1", 18, BLUE), ("Heading 2", 13.5, BLUE), ("Heading 3", 11.5, BLUE)]:
    st = doc.styles[name]
    st.font.name = FONT_CN
    st.font.size = Pt(size)
    st.font.bold = True
    st.font.color.rgb = color
    st.element.rPr.rFonts.set(qn("w:eastAsia"), FONT_CN)
    st.paragraph_format.space_before = Pt(16 if name == "Heading 1" else 10)
    st.paragraph_format.space_after = Pt(6)
    st.paragraph_format.keep_with_next = True

# ---------- 工具 ----------
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
    hdr = t.rows[0].cells
    for i, h in enumerate(headers):
        cell_text(hdr[i], h, bold=True, size=size, color=RGBColor(0xFF, 0xFF, 0xFF),
                  align=WD_ALIGN_PARAGRAPH.CENTER)
        shade(hdr[i], header_fill)
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
    # 跨页时重复表头，并禁止单行被拆开
    trPr = t.rows[0]._tr.get_or_add_trPr()
    th = OxmlElement("w:tblHeader")
    th.set(qn("w:val"), "true")
    trPr.append(th)
    for row in t.rows:
        trPr = row._tr.get_or_add_trPr()
        trPr.append(OxmlElement("w:cantSplit"))
    doc.add_paragraph().paragraph_format.space_after = Pt(2)
    return t

def para(s, bold=False, size=10.5, color=None, align=None, space_after=4, indent=0):
    p = doc.add_paragraph()
    if align is not None:
        p.alignment = align
    p.paragraph_format.space_after = Pt(space_after)
    if indent:
        p.paragraph_format.left_indent = Cm(indent)
    r = p.add_run(s)
    r.bold = bold
    r.font.size = Pt(size)
    if color is not None:
        r.font.color.rgb = color
    set_cn(r)
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

def code(text, size=8.5):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(4)
    p.paragraph_format.space_after = Pt(8)
    p.paragraph_format.line_spacing = 1.05
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
    return p

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
    path = os.path.join(BASE, name)
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_before = Pt(4)
    p.paragraph_format.space_after = Pt(0)
    p.add_run().add_picture(path, width=Cm(width))
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

# ---------- 封面 ----------
for _ in range(2):
    doc.add_paragraph()
p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
r = p.add_run("ESP32 + DSP86 + 86BYG450")
r.bold = True
r.font.size = Pt(30)
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
p.paragraph_format.space_before = Pt(18)
r = p.add_run("上海四宏 DSP86（AC 18–80 V / 5.0 A 级）  ·  86BYG450-79-A50-J5  ·  ESP32")
r.font.size = Pt(10.5)
r.font.color.rgb = GRAY
set_cn(r)

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(6)
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
    ("驱动器", "DSP86（上海四宏），AC 18–80 V，2.0–6.0 A 拨码"),
    ("电机", "86BYG450-79-A50-J5，1.8°，5.0 A，≈5.5 N·m，8 线"),
    ("控制板", "ESP32 / ESP32-S3 开发板"),
    ("控制方式", "脉冲 + 方向（共阳接法），3200 步/转"),
    ("文档版本", "v1.0"),
]:
    row = info.add_row().cells
    cell_text(row[0], k, bold=True, size=9.5, color=BLUE)
    cell_text(row[1], v, size=9.5)
    row[0].width = Cm(3.4)
    row[1].width = Cm(12.6)

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(14)
picture("01_system_wiring.png", width=16.5)

para("提示：端子排列与拨码位置请以驱动器侧面铭牌为准；不同批次可能有差异。",
     size=9, color=ACCENT, align=WD_ALIGN_PARAGRAPH.CENTER)
page_break()

# ---------- 1 安全 ----------
h1("1  安全须知")
for s in [
    "驱动器高压侧为 AC 18–80 V，由 220 V 隔离变压器降压后接入。严禁把 220 V 直接接到 AC 端子，否则会立即损坏驱动器。",
    "接线、改拨码、插拔电机线前必须断电，并等待驱动器内部电容放电（至少 1 分钟）。",
    "电机线严禁带电插拔，否则反电动势可能击穿驱动器功率管。",
    "驱动器外壳、电机外壳、变压器铁芯、电控箱体必须可靠接 PE 保护地。",
    "首次调试建议串接 5 A 慢断保险丝，并使用带漏电保护的插座。",
    "急停开关应切断变压器一次侧（220 V），而不是只关控制信号。",
]:
    bullet(s)

para("本手册的接线图与参数用于指导调试；正式设计前请核对厂家最新手册与实物铭牌。",
     size=9, color=GRAY)

# ---------- 2 系统组成 ----------
h1("2  系统组成")
table(
    ["序号", "部件", "规格 / 型号", "数量", "备注"],
    [
        ["1", "控制板", "ESP32 / ESP32-S3 开发板", "1", "任意带 GPIO 的型号"],
        ["2", "步进驱动器", "上海四宏 DSP86", "1", "AC 18–80 V，2.0–6.0 A"],
        ["3", "步进电机", "86BYG450-79-A50-J5", "1", "1.8°，5.0 A，8 线"],
        ["4", "隔离变压器", "220 V → 48 VAC，≥300 VA", "1", "建议 400 VA"],
        ["5", "断路器/保险丝", "2P 断路器 + 5 A 慢断", "1 套", "一次侧"],
        ["6", "急停开关", "常闭 10 A", "1", "切断 220 V"],
        ["7", "电机电缆", "4 芯屏蔽 1.0 mm²", "若干", "屏蔽层单端接 PE"],
        ["8", "信号电缆", "4 芯屏蔽 0.2 mm²", "若干", "与电机线分开走"],
        ["9", "5 V 电源", "ESP32 USB 或 5 V/1 A", "1", "信号共阳"],
    ],
    widths=[1.2, 3.0, 6.4, 1.4, 5.0], size=9,
)

# ---------- 3 DSP86 ----------
h1("3  DSP86 驱动器参数")
doc.add_heading("3.1  主要电气参数", level=2)
table(
    ["项目", "参数", "说明"],
    [
        ["输入电源", "AC 18–80 V（50/60 Hz）", "铭牌 VAC 18V~80V；推荐 48 VAC"],
        ["输出电流", "2.0–6.0 A（REF），峰值 7.2 A", "8 档拨码"],
        ["控制方式", "脉冲 + 方向 + 使能", "共阳 / 共阴 / 差分"],
        ["最大脉冲频率", "200 kHz", "厂家标称"],
        ["细分", "400–51200 步/转，16 档", "SW5–SW8"],
        ["信号输入", "差分光耦，5–15 mA，TTL 兼容", "3.3 V 可直接驱动"],
        ["待机电流", "半流 / 全流可选", "SW4"],
        ["体积", "151 × 97 × 48 mm", "不含端子"],
        ["指示", "PWR / FAULT 指示灯", "故障状态以手册为准"],
    ],
    widths=[3.4, 7.2, 6.4], size=9,
)

doc.add_heading("3.2  端子定义", level=2)
table(
    ["端子", "含义", "接法"],
    [
        ["PUL+ / PUL−", "脉冲输入", "+ 接 +5 V，− 接 ESP32 GPIO"],
        ["DIR+ / DIR−", "方向输入", "+ 接 +5 V，− 接 ESP32 GPIO"],
        ["ENA+ / ENA−", "使能输入", "可悬空（默认使能）"],
        ["AC / AC", "交流电源", "AC 18–80 V"],
        ["A+ / A−", "电机 A 相", "接电机 A 相"],
        ["B+ / B−", "电机 B 相", "接电机 B 相"],
    ],
    widths=[4.2, 4.6, 8.2], size=9,
)

doc.add_heading("3.3  电流拨码表（SW1–SW3）", level=2)
table(
    ["REF 电流", "峰值电流", "SW1", "SW2", "SW3"],
    [
        ["2.00 A", "2.40 A", "on", "on", "on"],
        ["2.57 A", "3.08 A", "off", "on", "on"],
        ["3.14 A", "3.77 A", "on", "off", "on"],
        ["3.71 A", "4.45 A", "off", "off", "on"],
        ["4.28 A", "5.14 A", "on", "on", "off"],
        ["4.86 A", "5.83 A", "off", "on", "off"],
        ["5.43 A", "6.52 A", "on", "off", "off"],
        ["6.00 A", "7.20 A", "off", "off", "off"],
    ],
    widths=[3.4, 3.4, 2.6, 2.6, 2.6], size=9,
)
para("电机铭牌 5.0 A → 选 4.86 A 档（约 97%，留余量、温升更低）；若铭牌 5.9 A，可选 5.43 A 档。",
     size=9.5, color=GRAY)

doc.add_heading("3.4  细分拨码表（SW5–SW8）", level=2)
table(
    ["脉冲/转", "SW5", "SW6", "SW7", "SW8", "步距角（1.8°）"],
    [
        ["400", "on", "on", "on", "on", "0.9°"],
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
    widths=[2.8, 2.2, 2.2, 2.2, 2.2, 4.0], size=9,
)
para("SW4：off = 半流（停机自动减流），on = 全流。建议先用 off，发热更低；若停机保持力矩不足再改 on。",
     size=9.5)

# ---------- 4 电机 ----------
h1("4  86BYG450 电机参数")
doc.add_heading("4.1  铭牌参数", level=2)
table(
    ["项目", "参数", "项目", "参数"],
    [
        ["机座 / 法兰", "86 mm（NEMA 34）", "步距角", "1.8°（整步）"],
        ["相数", "2 相", "机身长度", "79 mm"],
        ["额定相电流", "5.0 A", "保持转矩", "≈5.5 N·m"],
        ["引出线", "8 线（可串/并联）", "轴径", "12.7 / 14 mm"],
        ["重量", "≈3.5 kg", "绝缘等级", "B"],
        ["温升", "≤80 K", "环境温度", "−20 ~ +50 ℃"],
    ],
    widths=[3.4, 5.2, 3.2, 5.2], size=9,
)

doc.add_heading("4.2  绕组识别（万用表法）", level=2)
for s in [
    "电阻档找出导通的两两线对；同一相阻值通常 0.5–2 Ω，不同相之间不通（∞）。",
    "8 线电机可量出 4 组线圈：A1、A2、B1、B2，每组两根线。",
    "常见色序（仅参考，务必实测）：黑 A+、绿 A−、红 B+、蓝 B−；中间抽头为黄/白。",
]:
    bullet(s)

doc.add_heading("4.3  串联 / 并联怎么选", level=2)
table(
    ["接法", "特点", "电流设置", "本配置是否推荐"],
    [
        ["8 线串联", "低速转矩大、高速差、发热小", "铭牌×0.7 ≈ 3.5 A", "推荐"],
        ["8 线并联", "高速好、电流大、发热大", "铭牌×1.4 ≈ 7.0 A", "禁止（超 6 A）"],
        ["4 线", "内部已接好，直接 A+/A−/B+/B−", "按铭牌 5.0 A", "若是 4 线则用此"],
    ],
    widths=[2.8, 6.6, 4.4, 3.2], size=9,
)
para("本例 5.0 A 电机并联需约 7.0 A，超过 DSP86 的 6.0 A（峰值 7.2 A）能力，不要并联满载运行。推荐串联 + 3.5 A 档。",
     size=9.5, color=ACCENT)
picture("03_motor_wiring.png", width=16.5, cap="图 3  4 线与 8 线的串联 / 并联接法")

doc.add_heading("4.4  接线顺序（建议）", level=2)
for i, s in enumerate([
    "断电，确认变压器一次侧已断开、内部电容已放电。",
    "先接 PE：驱动器外壳、电机外壳、变压器铁芯、电控箱体。",
    "接电机：按 4 线或 8 线串联接法接好 A+/A−/B+/B−，拧紧端子。",
    "接驱动器 AC：变压器次级两根线接 DSP86 的 AC、AC。",
    "设置拨码：按第 6 节设置电流、细分与半流。",
    "接信号：+5 V → PUL+/DIR+，GPIO18 → PUL−，GPIO19 → DIR−。",
    "对照 5.5 检查表复查，确认 AC 与电机相间均无短路。",
    "上电：先低速单步测试方向与平稳性，再逐步提高速度。",
], 1):
    numbered(s, i, size=10)

# ---------- 5 接线 ----------
h1("5  接线步骤")
doc.add_heading("5.1  系统接线总图", level=2)
picture("01_system_wiring.png", width=17, cap="图 1  ESP32 + DSP86 + 86BYG450 系统接线")

doc.add_heading("5.2  电源侧（AC 18–80 V）", level=2)
code("""220 VAC (L/N/PE)
   │
   ├─ 断路器 2P ── 5 A 慢断保险丝 ── 急停开关
   │
   └─ 隔离变压器 220 V / 48 VAC（≥300 VA）
            │
            └── DSP86  AC ── AC""")
for s in [
    "变压器次级不分极性，两根线接 DSP86 的两个 AC 端子即可。",
    "必须使用隔离变压器；自耦调压器（如 TDGC2）不能代替隔离变压器。",
    "一次侧、二次侧均建议加保险丝；二次侧 6 A 慢断。",
    "PE 接到驱动器外壳接地螺钉、电机外壳、电控箱接地排。",
]:
    bullet(s)

doc.add_heading("5.3  电机侧", level=2)
for s in [
    "4 线：A+ / A− / B+ / B− 一一对应。",
    "8 线串联：A+ → A1 → A2 → A−，B+ → B1 → B2 → B−。",
    "相序错会表现为“嗡嗡响、只振动不转”，此时交换 A+ / A−。",
    "电机线使用屏蔽电缆，屏蔽层在驱动器侧单端接 PE。",
]:
    bullet(s)

doc.add_heading("5.4  控制信号侧（共阳接法，推荐）", level=2)
picture("02_signal_wiring.png", width=17, cap="图 2  共阳控制信号接线")
table(
    ["ESP32", "驱动器", "说明"],
    [
        ["5V", "PUL+、DIR+", "共阳，+ 端并接 +5 V"],
        ["GPIO18", "PUL−", "脉冲，每个低脉冲走一步"],
        ["GPIO19", "DIR−", "方向，可在代码中取反"],
        ["GPIO23（可选）", "ENA−", "不接 = 默认使能"],
        ["GND", "不接", "光耦隔离，无需共地"],
    ],
    widths=[3.6, 4.4, 9.0], size=9,
)
para("电平逻辑：GPIO 输出低电平 → 光耦导通 → 该通道有效。因此方向与使能的极性要在首次上电时确认。",
     size=9.5)
for s in [
    "干扰大、线长 > 1 m：加 74HCT245（5 V 供电）、ULN2003 或成品光耦隔离模块。",
    "需要真正差分：用 AM26LS31 等 RS-422 驱动器输出到 PUL± / DIR±。",
    "信号地与 AC 地不要短接；光耦本身已隔离。",
]:
    bullet(s)

doc.add_heading("5.5  接线检查表", level=2)
for s in [
    "AC 两根线之间不短路（断电用万用表测）。",
    "电机相间不短路，同相导通。",
    "A/B 相没有接反。",
    "+5 V 没有接到 PUL− / DIR−。",
    "驱动器外壳、电机、变压器、箱体均接 PE。",
    "电机线与信号线分开走线，避免长距离平行。",
    "拨码方向与丝印一致（on 侧）。",
]:
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(2)
    p.paragraph_format.left_indent = Cm(0.4)
    r = p.add_run("☐  " + s)
    r.font.size = Pt(10)
    set_cn(r)

# ---------- 6 拨码 ----------
h1("6  拨码设置（本配置推荐）")
table(
    ["拨码", "位置", "含义"],
    [
        ["SW1", "off", "电流档 bit0"],
        ["SW2", "on", "电流档 bit1"],
        ["SW3", "off", "电流档 bit2 → 4.86 A"],
        ["SW4", "off", "半流（停机减流）"],
        ["SW5", "off", "细分 bit0"],
        ["SW6", "off", "细分 bit1"],
        ["SW7", "on", "细分 bit2"],
        ["SW8", "on", "细分 bit3 → 3200 步/转"],
    ],
    widths=[2.6, 2.8, 9.6], size=9,
)
para("若采用 8 线串联并按 3.5 A 运行：SW1=off、SW2=off、SW3=on（REF 3.71 A / PK 4.45 A）。",
     size=9.5, color=GRAY)
para("推荐组合：off / on / off / off / off / off / on / on  →  4.86 A、3200 步/转、半流待机。",
     bold=True, size=10)

# ---------- 7 程序 ----------
h1("7  ESP32 控制程序")
doc.add_heading("7.1  引脚分配", level=2)
table(
    ["功能", "GPIO", "备注"],
    [
        ["STEP / PUL", "18", "避免 0/2/5/12/15 等启动绑带脚"],
        ["DIR", "19", "可软件取反"],
        ["ENA", "23", "可选，默认不接"],
    ],
    widths=[4.2, 2.4, 9.4], size=9,
)

doc.add_heading("7.2  例程（FastAccelStepper）", level=2)
para("在 Arduino IDE 的库管理器中搜索并安装 FastAccelStepper，然后上传以下程序。", size=9.5)
code("""#include <FastAccelStepper.h>

#define PIN_STEP 18
#define PIN_DIR  19

// 共阳接法下，方向如与实际相反，把 true 改成 false
#define DIR_INVERT true

FastAccelStepperEngine engine = FastAccelStepperEngine();
FastAccelStepper *stepper = nullptr;

void setup() {
  Serial.begin(115200);
  delay(200);

  engine.init();
  stepper = engine.stepperConnectToPin(PIN_STEP);
  if (!stepper) {
    Serial.println("step pin init failed");
    while (true) delay(1000);
  }

  stepper->setDirectionPin(PIN_DIR, DIR_INVERT);
  stepper->setSpeedInHz(3200);      // 3200 步/秒 = 1 转/秒
  stepper->setAcceleration(6400);   // 步/秒²
  stepper->setCurrentPosition(0);

  Serial.println("DSP86 ready. cmds: m<steps> s<hz> a<hz2> z ?");
}

void loop() {
  if (!Serial.available()) return;

  String line = Serial.readStringUntil('\\n');
  line.trim();
  if (line.length() == 0) return;

  char cmd = line[0];
  long val = line.substring(1).toInt();

  switch (cmd) {
    case 'm':                       // 相对移动，例：m3200
      stepper->move(val);
      Serial.printf("move %ld\\n", val);
      break;
    case 's':                       // 速度 Hz
      stepper->setSpeedInHz(val);
      Serial.printf("speed %ld Hz\\n", val);
      break;
    case 'a':                       // 加速度
      stepper->setAcceleration(val);
      Serial.printf("accel %ld\\n", val);
      break;
    case 'z':
      stepper->setCurrentPosition(0);
      Serial.println("zero");
      break;
    case '?':
      Serial.printf("pos=%ld running=%d\\n",
                    stepper->getCurrentPosition(), stepper->isRunning());
      break;
    default:
      Serial.println("?");
  }
}""")
para("测试：发送 s800 → m3200，电机应转 1 圈；m-3200 反向；再逐步提高 s3200、s6400 观察是否失步。",
     size=9.5)

doc.add_heading("7.3  无库自检（只验证接线）", level=2)
code("""#define PIN_STEP 18
#define PIN_DIR  19

void setup() {
  pinMode(PIN_DIR, OUTPUT);
  digitalWrite(PIN_DIR, HIGH);

  ledcSetup(0, 1000, 10);       // 通道 0，1 kHz
  ledcAttachPin(PIN_STEP, 0);
  ledcWriteTone(0, 1000);       // 1000 步/秒
}

void loop() {}""")
para("Arduino-ESP32 3.x 的 LEDC API 改为 ledcAttach(pin, freq, res)，请按核心版本调整。",
     size=9, color=GRAY)

# ---------- 8 调试 ----------
h1("8  上电与调试")
doc.add_heading("8.1  首次上电（不带负载）", level=2)
for i, s in enumerate([
    "断开电机轴上的联轴器或负载。",
    "拨码按第 6 节设置。",
    "先只接电源上电：PWR 灯亮、不报故障。",
    "断电后接电机；再上电，电机应有保持力矩（手拧不动）。",
    "断电接 ESP32 信号线，再上电。",
    "发低速脉冲，观察转向与平稳性。",
], 1):
    numbered(s, i)

doc.add_heading("8.2  电流整定", level=2)
for s in [
    "从 3.71 A 档起步，连续运行 10 分钟，摸电机外壳温升。",
    "温升 ≤80 K 且不失步即可，不必追求最大电流。",
    "SW4=off 时停机力矩会下降，属正常现象。",
]:
    bullet(s)

doc.add_heading("8.3  加减速", level=2)
for s in [
    "86 电机转子惯量大，必须设置加速度，建议 3000–10000 步/s² 起步。",
    "直接给高频脉冲会堵转/失步，表现为“响一下不走”。",
    "高速上不去时：提高母线电压、降低细分、延长加速时间。",
]:
    bullet(s)

# ---------- 9 计算 ----------
h1("9  脉冲当量与速度计算")
para("设细分为 N（本配置 3200 步/转）、丝杠导程 P（mm/转）、减速比 i：", size=10)
code("""脉冲当量 = P / (N × i)          [mm/step]
速度     = f / N × 60 / i        [r/min]   （f 为脉冲频率 Hz）""")
para("例：5 mm 导程丝杠、直连、3200 步/转：脉冲当量 = 5/3200 ≈ 1.56 µm；1 kHz 对应 18.75 r/min，32 kHz 对应 600 r/min（需按转矩曲线降额）。",
     size=10)

# ---------- 10 故障 ----------
h1("10  故障排查")
table(
    ["现象", "可能原因", "处理"],
    [
        ["上电无指示", "无 AC、保险丝断、变压器无输出", "测 AC 端子电压；查保险丝/急停"],
        ["电机松轴", "ENA 有效电平相反、缺相、驱动器故障", "断开 ENA 试；查电机线；看 FAULT 灯"],
        ["有力矩但不转", "无脉冲、PUL 接错", "用自检程序发 1 kHz；测 PUL−"],
        ["只振动、嗡嗡响", "A/B 相序错、电流过小、加速太快", "交换 A+/A−；提高电流；降加速度"],
        ["方向相反", "DIR 逻辑相反", "交换 A+/A−，或 DIR_INVERT 取反"],
        ["高速失步", "加速太快、电压低、细分太高", "降加速度；查 48 VAC；用 1600 细分"],
        ["随机丢步", "干扰、信号与电机线并行", "屏蔽、单端接地、分开走线、加磁环"],
        ["电机过热", "电流档过高、半流失效、散热差", "降一档电流；SW4=off；改善通风"],
        ["上电炸保险", "AC 短路、接错 220 V、驱动器损坏", "立即断电查线，勿盲目再上电"],
    ],
    widths=[4.0, 6.4, 6.6], size=8.5,
)

# ---------- 11 参考 ----------
h1("11  参考资料")
refs = [
    ("上海四宏 DSP86 产品页", "https://www.sihengmotor.com/product/dsp86/"),
    ("DSP86 驱动器资料与尺寸图", "https://www.shyl-technology.com/product/dsp86-stepper-motor-driver-for-nema34-motor/"),
    ("86BYG 系列电机参数（Mecheltron）", "https://mecheltron.eu/en/product/86byg-stepping-motor"),
    ("86BYG250 系列接线与参数", "http://www.zgbjdj.com/news2.asp?id=7401"),
    ("86BYGH450B 电机参数（5 A / 5.88 N·m）", "https://www.eaglecnctec.com/wap/products/155-stepping-motor-86bygh450b.html"),
    ("86BYGH450B-79-A50-J5 商品页（5.9 A / 5.5 N·m）", "https://www.phantomcncsystems.com/product-page/stepper-motor-86bygh450b-79-a50-j5-5-9a-5-5nm"),
]
for name, url in refs:
    p = doc.add_paragraph(style="List Bullet")
    p.paragraph_format.space_after = Pt(2)
    r = p.add_run(name + "：")
    r.font.size = Pt(9.5)
    set_cn(r)
    r2 = p.add_run(url)
    r2.font.size = Pt(9)
    r2.font.color.rgb = RGBColor(0x1F, 0x4E, 0x79)
    r2.font.name = FONT_MONO

para("免责声明：以上参数整理自公开资料与驱动器铭牌照片，不同批次可能存在差异；正式设计前请以厂家最新手册和实物铭牌为准。",
     size=8.5, color=GRAY)

# ---------- 附录 ----------
h1("附录 A  一分钟接线速查")
code("""220VAC ── 断路器 ── 保险丝 ── 急停 ── 变压器(48VAC) ── DSP86 AC/AC
DSP86 A+ ── 电机 A+
DSP86 A- ── 电机 A-
DSP86 B+ ── 电机 B+
DSP86 B- ── 电机 B-
ESP32 5V  ── PUL+ / DIR+（共阳）
ESP32 G18 ── PUL-
ESP32 G19 ── DIR-
ESP32 G23 ── ENA-（可不接）""")

h1("附录 B  推荐拨码")
table(
    ["SW1", "SW2", "SW3", "SW4", "SW5", "SW6", "SW7", "SW8"],
    [["off", "on", "off", "off", "off", "off", "on", "on"]],
    widths=[2.0] * 8, size=9,
)
para("→ 4.86 A / 3200 步/转 / 半流待机。", size=9.5, color=GRAY)

# ---------- 页脚页码 ----------
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

doc.core_properties.title = "ESP32 + DSP86 + 86BYG450 步进电机接线与调试手册"
doc.core_properties.subject = "DSP86 / 86BYG450 / ESP32 接线、拨码与调试"
doc.core_properties.author = "DSH"

out = os.path.join(BASE, "ESP32_DSP86_86BYG450_接线与调试手册.docx")
doc.save(out)
print("saved:", out, os.path.getsize(out), "bytes")


