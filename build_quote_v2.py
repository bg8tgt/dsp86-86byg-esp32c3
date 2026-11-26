# -*- coding: utf-8 -*-
"""Build a quotation workbook (single-manufacturer, 30% markup) from the inquiry sheet."""
from openpyxl import Workbook
from openpyxl.styles import Font, Alignment, Border, Side, PatternFill
from openpyxl.worksheet.properties import PageSetupProperties


def num_to_cny(n):
    n = round(float(n), 2)
    if n == 0:
        return '零元整'
    int_part = int(n)
    frac = int(round((n - int_part) * 100))
    jiao, fen = divmod(frac, 10)
    digit = '零壹贰叁肆伍陆柒捌玖'

    def four(x):
        s = ''
        zero = False
        for i, ch in enumerate(reversed(str(x))):
            d = int(ch)
            if d == 0:
                zero = True
            else:
                if zero and s:
                    s = '零' + s
                zero = False
                s = digit[d] + ['', '拾', '佰', '仟'][i] + s
        return s

    groups = []
    x = int_part
    while x > 0:
        groups.append(x % 10000)
        x //= 10000
    unit_big = ['', '万', '亿', '万亿']
    s = ''
    for idx in range(len(groups) - 1, -1, -1):
        g = groups[idx]
        if g == 0:
            if s and not s.endswith('零'):
                s += '零'
            continue
        part = four(g)
        if s and g < 1000 and not s.endswith('零'):
            s += '零'
        s += part + unit_big[idx]
    s += '元'
    if jiao == 0 and fen == 0:
        s += '整'
    else:
        if jiao:
            s += digit[jiao] + '角'
        elif fen:
            s += '零'
        if fen:
            s += digit[fen] + '分'
    return s


MAKER = '江苏华海测控技术有限公司'
MARKUP = 1.30

# base = 询价基准单价（市场参考价 + 非标配置折算），报价 = base × 1.30
ITEMS = [
    dict(no=1, name='不锈钢隔膜耐蚀压力真空表',
         spec='Y-100 测量范围：-0.1～2.4MPa，DN25 PN40凸面（M）法兰316L材质，精度1.6级',
         mat='316L', unit='块', qty=10, base=860.00,
         remark=''),
    dict(no=2, name='防爆耐震电接点压力表',
         spec='YTX-100B，1.6级精度，0～0.6MPa，介质温度范围0～300℃，ExdⅡCT6，电接点磁助式双接点，'
              '接点容量AC 380V/30VA，M20×1.5，配防爆电缆引入装置（格兰头），充油耐震，不锈钢材质，'
              '随货文件含防爆证、CPA、合格证、检定证书',
         mat='不锈钢', unit='台', qty=25, base=960.00,
         remark='导热油电加热器'),
    dict(no=3, name='万向型双金属温度计',
         spec='WSS-481/TH02，-40～80℃，过流介质：氯甲烷；量程-40～80℃；材质不锈钢；表盘直径100mm；'
              '测温杆长2000mm；安装方式：法兰连接，法兰规格HG/T20592-2009 WN40（B）-25FM（凹面）316L',
         mat='不锈钢', unit='个', qty=10, base=780.00,
         remark=''),
    dict(no=4, name='磁性翻板液位计（远传）',
         spec='HH-UHZ-500BEECADN50TL。过流介质：密度1.0～1.5g/cm³强酸性水溶液，易挥发结晶；'
              '材质304+烧结四氟；量程0～500mm；连接形式DN50 PN16-RF，中心距500mm；'
              '电气接口1/2NPT，两线制DC24V/4-20mA，IP65；准确度±10mm；压力-0.1～1.6MPa；防爆等级ExdⅡCT6 Gb',
         mat='304+烧结四氟', unit='个', qty=6, base=3600.00,
         remark=''),
]

for it in ITEMS:
    it['price'] = round(it['base'] * MARKUP, 2)

THIN = Side(style='thin', color='000000')
BORDER = Border(left=THIN, right=THIN, top=THIN, bottom=THIN)
CENTER = Alignment(horizontal='center', vertical='center', wrap_text=True)
LEFT = Alignment(horizontal='left', vertical='center', wrap_text=True)
HDR_FILL = PatternFill('solid', fgColor='DDEBF7')
TOTAL_FILL = PatternFill('solid', fgColor='FFF2CC')

wb = Workbook()

# ---------------------------------------------------------------- sheet 1
ws = wb.active
ws.title = '报价表'

widths = {'A': 5.5, 'B': 21, 'C': 52, 'D': 10.5, 'E': 6, 'F': 6.5,
          'G': 12, 'H': 13, 'I': 24, 'J': 7.5, 'K': 10.5, 'L': 16.5}
for col, w in widths.items():
    ws.column_dimensions[col].width = w

ws.merge_cells('A1:L1')
c = ws['A1']
c.value = '个旧锡都实业有限责任公司物资采购报价表'
c.font = Font(name='宋体', size=16, bold=True)
c.alignment = Alignment(horizontal='center', vertical='center')
ws.row_dimensions[1].height = 34

ws.merge_cells('A2:F2')
c = ws['A2']
c.value = '报价单位（公章）：'
c.font = Font(name='宋体', size=10)
c.alignment = Alignment(horizontal='left', vertical='center')
ws.merge_cells('G2:L2')
c = ws['G2']
c.value = ('项目名称：2025年-2026年度云南锡业锡化工材料有限责任公司\n'
           '机械维修服务合作协议（甲基锡）-仪表采购')
c.font = Font(name='宋体', size=10)
c.alignment = Alignment(horizontal='left', vertical='center', wrap_text=True)
ws.row_dimensions[2].height = 42

headers = ['序号', '物资名称', '规格型号', '材质', '单位', '数量',
           '含税单价\n（元）', '金额\n（元）', '生产厂家', '质量\n要求', '交货\n地点', '备注']
for i, h in enumerate(headers, start=1):
    c = ws.cell(row=3, column=i, value=h)
    c.font = Font(name='宋体', size=9.5, bold=True)
    c.alignment = CENTER
    c.border = BORDER
    c.fill = HDR_FILL
ws.row_dimensions[3].height = 32

row = 4
for it in ITEMS:
    ws.cell(row=row, column=1, value=it['no'])
    ws.cell(row=row, column=2, value=it['name'])
    ws.cell(row=row, column=3, value=it['spec'])
    ws.cell(row=row, column=4, value=it['mat'])
    ws.cell(row=row, column=5, value=it['unit'])
    ws.cell(row=row, column=6, value=it['qty'])
    ws.cell(row=row, column=7, value=it['price'])
    ws.cell(row=row, column=8, value='=F{0}*G{0}'.format(row))
    ws.cell(row=row, column=9, value=MAKER)
    ws.cell(row=row, column=12, value=it['remark'])
    for col in range(1, 13):
        c = ws.cell(row=row, column=col)
        c.border = BORDER
        c.font = Font(name='宋体', size=9)
        c.alignment = LEFT if col == 3 else CENTER
    ws.cell(row=row, column=7).number_format = '#,##0.00'
    ws.cell(row=row, column=8).number_format = '#,##0.00'
    ws.row_dimensions[row].height = {1: 46, 2: 76, 3: 58, 4: 74}[it['no']]
    row += 1

ws.merge_cells('J4:J7')
c = ws['J4']
c.value = '国标'
c.font = Font(name='宋体', size=9)
c.alignment = CENTER
ws.merge_cells('K4:K7')
c = ws['K4']
c.value = '大屯化工'
c.font = Font(name='宋体', size=9)
c.alignment = CENTER

total = sum(i['qty'] * i['price'] for i in ITEMS)

ws.merge_cells('A8:F8')
c = ws['A8']
c.value = '合计（人民币）'
c.font = Font(name='宋体', size=10, bold=True)
c.alignment = Alignment(horizontal='right', vertical='center')
c = ws['G8']
c.value = '含税总价'
c.font = Font(name='宋体', size=9, bold=True)
c.alignment = CENTER
c = ws['H8']
c.value = '=SUM(H4:H7)'
c.font = Font(name='宋体', size=11, bold=True)
c.alignment = CENTER
c.number_format = '#,##0.00'
ws.merge_cells('I8:L8')
c = ws['I8']
c.value = '大写：人民币' + num_to_cny(total)
c.font = Font(name='宋体', size=9, bold=True)
c.alignment = Alignment(horizontal='left', vertical='center', wrap_text=True)
for col in range(1, 13):
    ws.cell(row=8, column=col).border = BORDER
    ws.cell(row=8, column=col).fill = TOTAL_FILL
ws.row_dimensions[8].height = 26

ws.merge_cells('A9:F9')
c = ws['A9']
c.value = '报价日期：2026年10月20日'
c.font = Font(name='宋体', size=10)
c.alignment = Alignment(horizontal='left', vertical='center')
ws.merge_cells('G9:L9')
c = ws['G9']
c.value = '报价有效期：自报价之日起30天'
c.font = Font(name='宋体', size=10)
c.alignment = Alignment(horizontal='left', vertical='center')
ws.row_dimensions[9].height = 20

notes = [
    '报价说明：',
    '1. 上列单价为含13%增值税专用发票价格，含运输至交货地点费用；不含现场卸车、安装及调试费用。',
    '2. 交货期：合同生效并收到预付款后25～30天内交货；第2、4项防爆仪表需配套防爆合格证及检定证书，交货期30～35天。',
    '3. 付款方式：合同签订后预付30%；货到验收合格并开具13%增值税专用发票后付60%；余款10%作为质保金，质保期满无质量问题后付清。',
    '4. 质保期：自验收合格之日起12个月；质保期内因产品自身质量问题免费维修或更换。',
    '5. 随货文件：产品合格证、出厂检定证书、材质证明、装箱单；第2项附防爆合格证、CPA计量器具型式批准证书、检定证书，第4项附防爆合格证。',
    '6. 报价有效期30天；逾期未签订合同，价格需重新确认。',
    '7. 本报价所列4项产品均由江苏华海测控技术有限公司生产或配套供货，随货提供该厂产品合格证、检定证书、防爆合格证及材质证明。',
]
r = 10
for i, text in enumerate(notes):
    ws.merge_cells(start_row=r, start_column=1, end_row=r, end_column=12)
    c = ws.cell(row=r, column=1, value=text)
    c.font = Font(name='宋体', size=9, bold=(i == 0))
    c.alignment = Alignment(horizontal='left', vertical='center', wrap_text=True)
    ws.row_dimensions[r].height = 17
    r += 1

ws.print_area = 'A1:L17'
ws.page_setup.orientation = 'landscape'
ws.page_setup.paperSize = ws.PAPERSIZE_A4
ws.page_setup.fitToWidth = 1
ws.page_setup.fitToHeight = 1
ws.sheet_properties.pageSetUpPr = PageSetupProperties(fitToPage=True)
ws.print_options.horizontalCentered = True
ws.page_margins.left = 0.4
ws.page_margins.right = 0.4
ws.page_margins.top = 0.5
ws.page_margins.bottom = 0.5
ws.page_margins.header = 0.2
ws.page_margins.footer = 0.2
ws.print_title_rows = '1:3'
ws.oddFooter.center.text = '第 &P 页 / 共 &N 页'
ws.oddFooter.center.size = 9

# ---------------------------------------------------------------- sheet 2
ws2 = wb.create_sheet('报价依据')
for col, w in {'A': 5.5, 'B': 20, 'C': 32, 'D': 22, 'E': 24,
               'F': 13, 'G': 14, 'H': 30}.items():
    ws2.column_dimensions[col].width = w

ws2.merge_cells('A1:H1')
c = ws2['A1']
c.value = '报价依据与价格测算（内部参考，不随报价表对外提供）'
c.font = Font(name='宋体', size=14, bold=True)
c.alignment = Alignment(horizontal='center', vertical='center')
ws2.row_dimensions[1].height = 30

ws2.merge_cells('A2:H2')
c = ws2['A2']
c.value = ('询价日期：2026年10月20日        报价厂家：江苏华海测控技术有限公司（淮安）        '
           '定价口径：询价基准单价 × 1.30（上浮30%）')
c.font = Font(name='宋体', size=9)
c.alignment = Alignment(horizontal='left', vertical='center')
ws2.row_dimensions[2].height = 20

hdr2 = ['序号', '物资名称', '关键配置差异（相对公开报价样机）', '市场参考价/起价（元）',
        '询价来源（公开报价）', '询价基准\n单价（元）', '报价单价\n（×1.30）', '说明']
for i, h in enumerate(hdr2, start=1):
    c = ws2.cell(row=3, column=i, value=h)
    c.font = Font(name='宋体', size=9.5, bold=True)
    c.alignment = CENTER
    c.border = BORDER
    c.fill = HDR_FILL
ws2.row_dimensions[3].height = 40

rows2 = [
    (1, '不锈钢隔膜耐蚀压力真空表',
     '公开报价样机为DN25隔膜法兰耐震压力表（0～0.6MPa、2.5级、304膜片）；本项要求1.6级、316L膜片与法兰、PN40凸面、真空复合量程，需加价约30%～50%。',
     '605（YTNPF-100B DN25）；525（TKYN-100B/F 不锈钢耐震隔膜）',
     '机电之家 安徽普利仪表 YTNPF-100B 报价 605元/块（2024-12-06）',
     '华海配套供货；基准价=605元样机按316L膜片+PN40凸面法兰+1.6级折算为860元'),
    (2, '防爆耐震电接点压力表',
     '公开报价为YTX-100B基本型（不锈钢、单/双接点）；本项追加充油耐震、磁助式双接点AC380V/30VA、介质0～300℃、M20×1.5及防爆格兰、防爆证+CPA+检定证书。',
     '480（YTX-100B 基本型）',
     '全球塑胶网 江苏英普瑞 YTX-100B 报价 480元/台',
     '华海配套供货；基准价=480元基本型加充油、双接点、耐300℃结构及全套证书折算为960元'),
    (3, '万向型双金属温度计',
     '公开报价为WSS-481 万向型、Φ100、304、L=75mm、0～50℃；本项测温杆2000mm（加长），法兰为HG/T20592 WN40(B)-25FM凹面316L（非标法兰），介质氯甲烷。',
     '372.90（上仪 WSS-481，Φ100，L=75mm）',
     '西域（ehsy.com）上仪 WSS-481 含税价 372.90元/个',
     '华海配套供货；基准价=372.90元样机加2000mm长杆+316L非标法兰折算为780元'),
    (4, '磁性翻板液位计（远传）',
     '公开报价为UHZ系列304基本型（500mm级、无远传）；本项要求304+烧结四氟（耐强酸、防结晶）、两线制4-20mA远传、ExdⅡCT6 Gb、DN50 PN16-RF、±10mm。',
     '1500（UHZ 304 基本型）；面议（带远传/防爆）',
     '机电之家 安徽天康 UHZ 磁翻板（耐高温型）1500元/台；仪表网同系列多为面议',
     '华海自产HH-UHZ系列；基准价=1500元基本型加烧结四氟内衬、远传变送器、隔爆接线盒折算为3600元'),
]

r = 4
for idx, item in enumerate(ITEMS):
    no, name, diff, ref, src, note = rows2[idx]
    vals = [no, name, diff, ref, src, item['base'], item['price'], note]
    for i, v in enumerate(vals, start=1):
        c = ws2.cell(row=r, column=i, value=v)
        c.border = BORDER
        c.font = Font(name='宋体', size=9)
        c.alignment = CENTER if i in (1, 6, 7) else LEFT
    ws2.cell(row=r, column=6).number_format = '#,##0.00'
    ws2.cell(row=r, column=7).number_format = '#,##0.00'
    ws2.row_dimensions[r].height = 62
    r += 1

ws2.merge_cells('A8:F8')
c = ws2['A8']
c.value = '报价总额（含13%增值税、含运费）：'
c.font = Font(name='宋体', size=9, bold=True)
c.alignment = Alignment(horizontal='right', vertical='center')
c = ws2['G8']
c.value = "='报价表'!H8"
c.font = Font(name='宋体', size=10, bold=True)
c.alignment = CENTER
c.number_format = '#,##0.00'
c = ws2['H8']
c.value = '大写：' + num_to_cny(total)
c.font = Font(name='宋体', size=9, bold=True)
c.alignment = LEFT
for col in range(1, 9):
    ws2.cell(row=8, column=col).border = BORDER
    ws2.cell(row=8, column=col).fill = TOTAL_FILL
ws2.row_dimensions[8].height = 24

notes2 = [
    '报价口径与需确认事项：',
    '1. 上表“市场参考价”均为公开渠道可查的整机标价，仅作比价基准；实际成交价随批量、账期、证书要求浮动，签约前建议向厂家二次询价确认。',
    '2. 定价公式：报价单价 = 询价基准单价 × 1.30；合计 = Σ（数量 × 报价单价）。按此口径，四项合计为 80,600.00 元。',
    '3. 上浮30%对应毛利率约23.1%（1 − 1÷1.30）。若客户议价，建议让价区间控制在15%～25%，最低不低于基准单价的1.10倍，再低则售后与证书成本倒挂。',
    '4. 第1项“凸面（M）法兰”与第3项“WN40（B）-25FM凹面法兰”均为非标法兰，需在下单时提供法兰图纸或按HG/T20592-2009标准确认密封面形式，否则易出现到货无法安装。',
    '5. 第2项要求介质温度0～300℃，普通隔爆电接点表耐温≤70℃，需配散热器或隔膜隔离，基准价已按带散热结构考虑；若客户改为常温介质，基准价可下调约80～120元。',
    '6. 第4项“304+烧结四氟”为强酸防结晶工况，浮子与内衬材质须与介质（含氯甲烷/酸性水溶液）相容，建议签约前取得介质浓度与温度参数，必要时改为衬PTFE或PVDF。',
    '7. 全部4项按江苏华海测控技术有限公司单一厂家口径报价：第4项 HH-UHZ 为其自产型号；第1～3项由该厂配套供货，签约前请书面确认其配套件的品牌、材质及证书（防爆合格证、CPA、检定证书）能否随货提供。',
    '8. 修改“报价表”G列单价后，H列金额与合计自动重算；“报价表”I8 与本页 H8 的大写金额为静态文本，需同步手工更新。',
]
r = 9
for i, text in enumerate(notes2):
    ws2.merge_cells(start_row=r, start_column=1, end_row=r, end_column=8)
    c = ws2.cell(row=r, column=1, value=text)
    c.font = Font(name='宋体', size=9, bold=(i == 0))
    c.alignment = Alignment(horizontal='left', vertical='center', wrap_text=True)
    ws2.row_dimensions[r].height = 18
    r += 1

ws2.page_setup.orientation = 'landscape'
ws2.page_setup.paperSize = ws2.PAPERSIZE_A4
ws2.page_setup.fitToWidth = 1
ws2.page_setup.fitToHeight = 0
ws2.sheet_properties.pageSetUpPr = PageSetupProperties(fitToPage=True)
ws2.print_options.horizontalCentered = True

out = r'C:\Users\Administrator\Desktop\物资采购报价表-仪表（甲基锡）20261020.xlsx'
wb.save(out)
print('saved:', out)
print('total:', total, num_to_cny(total))
for it in ITEMS:
    print(it['no'], it['base'], '->', it['price'], '=', it['qty'] * it['price'])
