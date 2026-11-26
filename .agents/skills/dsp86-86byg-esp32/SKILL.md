---
name: dsp86-86byg-esp32
description: 配置、接线与排查 ESP32-C3 + 上海四宏 DSP86 + 86BYG450 步进系统（AC 18–80 V 输入、SW1–SW8 电流/细分拨码、共阳 STEP/DIR、FastAccelStepper）。
---

# DSP86 + 86BYG450 + ESP32-C3 步进系统

用于处理这套硬件的接线、拨码、程序与故障排查。核心目标：**不烧驱动器、不烧电机、一次转对方向**。

## 何时使用

- 用户提到 DSP86、86BYG450、86BYG、四宏步进驱动器；
- 需要根据电机铭牌/接法计算 SW1–SW8 拨码；
- ESP32-C3 驱动步进电机，需要选安全 GPIO；
- 电机只振动、丢步、方向反、过热等排查。

## 不可违反的事实

1. **DSP86 是 AC 18–80 V 输入**（铭牌 `VAC: 18V~80V`）。必须经 220 V/24 VAC 隔离变压器（本配置），**不能接直流开关电源，不能直连 220 V**。
   24 VAC 属允许范围低端，母线约 34 VDC，带载建议 **≤200–300 rpm**；需要更高转速换 48 VAC 变压器（拨码不变）。
2. **电机线禁止带电插拔**；改拨码、接线前断电并等电容放电 ≥1 分钟。
3. **电流设定不得超过电机在该接法下的额定值**：
   - 8 线串联：额定 = 铭牌 × 0.7
   - 8 线并联：额定 = 铭牌 × 1.4
   - 4 线：额定 = 铭牌
4. **ENA 默认悬空**（驱动器使能）。不同批次有效电平可能相反，首次上电必须单步验证。
5. **共阳接法**下 GPIO 低电平 = 光耦导通；方向极性需实测。
6. **ENA 是“脱机”信号**：光耦导通（GPIO 低）= 电机自由，光耦断开（GPIO 高）= 使能；默认 `enaActiveLow=false`。
7. **热点固定 `DSP86-C3` / `12345678`**；WiFi 需 `setSleep(false)`、`setTxPower(19.5dBm)`、`setAutoReconnect(true)`。
8. **主板固定 COM6（CH340）**；可用参考工程：`C:\Users\Administrator\Documents\aily-project\C3BUJINTEST`。

## 标准流程

### 第 1 步：确认电机接法与铭牌

问清（或让用户看铭牌）：

- 电机是 4 线还是 8 线？4 线常见黄/蓝、红/绿（以实测为准）；接法：蓝 A+、黄 A−、红 B+、绿 B−；8 线要问串联还是并联。
- 铭牌电流是多少（5.0 A / 5.9 A）？
- 丝杠导程/减速比（用于脉冲当量）。

### 第 2 步：算电流档

用脚本：

```bash
python scripts/dip_calc.py --current 4.86 --ppr 400 --motor 5.0 --connection 4wire
```

脚本会输出 SW1–SW8、电流占比与告警。没有脚本时查 `references/params.md` 的电流表。

判定规则：

| 占比 | 结论 |
|---:|---|
| ≤ 100% | 可用 |
| 100–110% | 仅调试可短时用，长期降档 |
| > 110% | 禁止，必须降档或改接法 |

常见组合：

- 4 线 5.0 A 电机 → **4.86 A 为 97%**（off/on/off，当前配置）
- 5.0 A 电机 + 8 线串联（3.5 A 额定）→ **3.71 A**（off/off/on）
- 5.0 A 电机 + 8 线并联（7.0 A 额定）→ 4.86 A 可用
- 5.9 A 电机 → 4.86 A 可用（82%）

### 第 3 步：选细分

| 需求 | 脉冲/转 | SW5–SW8 | 步距角 |
|---|---:|---|---:|
| 低速大转矩、脉冲频率低 | 400 | on on on on | 0.9° |
| 兼顾平稳 | 1600 | on off on on | 0.225° |
| 精细定位 | 3200 | off off on on | 0.1125° |

400 步/转是半步模式，1–2 rps 附近易共振；振动大就升到 1600。

### 第 4 步：ESP32-C3 引脚

优先 **STEP=GPIO4、DIR=GPIO5、ENA=GPIO6**；
限位/急停用 **GPIO1**（内部上拉），俯仰角传感器用 **GPIO3**（ADC1_CH3）。

必须避开：

- 启动绑带：GPIO2、8、9
- USB：GPIO18、19
- Flash：GPIO11–17
- UART0：GPIO20、21

接线（共阳）：

```
5V    → PUL+ / DIR+
GPIO4 → PUL-
GPIO5 → DIR-
GPIO6 → ENA-（可悬空）
GPIO1 → 限位/急停（另一端接 GND）
GPIO3 → 俯仰角传感器（0–3.3 V）
```

俯仰角：0–5 V 传感器必须分压；OUT 串 1 kΩ，ADC 节点对 GND 接 100 nF；用 `analogReadMilliVolts()` 标定。
C3 只有 ADC1（GPIO0–4）；GPIO5 属 ADC2，WiFi 开启时受限。

### 第 5 步：写程序

用 FastAccelStepper（支持 C3），按实际 `STEPS_PER_REV` 设置：

```cpp
#define PIN_STEP 4
#define PIN_DIR  5
#define PIN_ENA  6
#define PIN_LIMIT 1
#define PIN_PITCH 3
#define STEPS_PER_REV 400
```

速度换算：`rpm = Hz / STEPS_PER_REV × 60`。400 步/转时 800 Hz = 120 rpm。

首次测试用 400–800 Hz，加速度 800–1600 步/s²，确认方向后再提速。
限位触发时 `forceStop()` 并脱机；俯仰角每 100 ms 采一次即可。

### 第 6 步：上电顺序

1. 断开负载，设拨码；
2. 只上电，确认 PWR 灯正常；
3. 断电接电机，上电确认有保持力矩；
4. 断电接信号，上电；
5. 低速单步，测方向；
6. 跑 10 分钟测温升（≤80 K）。

### 第 7 步：验收

- [ ] AC 端子电压在 18–80 V 之间（交流档）
- [ ] 电机同相导通、异相不通
- [ ] 拨码与计算表一致
- [ ] 方向、使能逻辑实测正确
- [ ] 连续运行温升合格
- [ ] 急停能切断 220 V

## 故障速查

| 现象 | 先查 |
|---|---|
| 上电无灯 | AC、保险丝、变压器 |
| 松轴 | ENA 极性、缺相 |
| 有力矩不转 | PUL 接线、脉冲输出 |
| 只振动 | A/B 相序、电流、加速度 |
| 方向反 | 交换 A+/A− 或 `DIR_INVERT` |
| 低速共振 | 400 半步共振，改 1600 |
| 高速丢步 | 加速度、母线电压、细分 |
| 过热 | 电流超过接法额定、串联用大电流 |
| 角度读数跳变 | 模拟线受干扰、未加 RC | 加 1 kΩ/100 nF，远离电机线 |

## 工具与资料

- `scripts/dip_calc.py`：拨码计算 + 电流校验 + 脉冲当量
- `references/params.md`：DSP86/电机/C3 引脚完整表格
- 详细手册：`../../README.md`、`../../ESP32C3_DSP86_86BYG450_接线与调试手册.docx`

## 输出要求

给用户的结论必须包含：

1. 电流档 + SW1–SW3（并说明是否超过接法额定）；
2. 细分 + SW5–SW8 + 步距角；
3. 三根信号线接法（含 GPIO 号）；
4. 一条安全提醒（AC 输入 / 禁止带电插拔 / 电流校验）。




## 已验证参考

- `C:\Users\Administrator\Documents\aily-project\C3BUJINTEST`：C3 + DSP86 实测固件，含电机/ WiFi / OTA 分页实现。
- 关键点：ENA 低=脱机；限位触发停机+脱机；WiFi 关 sleep；OTA 用 `getFreeSketchSpace()` 计算分区。

## 双轴扩展

- 方位 M1：STEP4 / DIR5 / ENA6 / HOME1。
- 俯仰 M2：STEP7 / DIR10 / ENA0 / LIMIT20；PITCH 仍为 GPIO3。
- C3 RMT 支持两路步进；串口改为 Serial.begin(115200, SERIAL_8N1, -1, 21) 释放 GPIO20。
- 电源：24VAC ≥600VA；两台驱动器各 10A 慢断。
- 图纸：09_power_schematic.png、10_control_schematic.png、11_wiring_diagram.png、06_dual_3d_variants.png。

