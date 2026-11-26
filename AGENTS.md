# AGENTS.md —— DSP86 / 86BYG450 / ESP32-C3 项目规则

本工作区围绕 **ESP32-C3 + 上海四宏 DSP86 + 86BYG450-79-A50-J5** 步进云台。
任何修改都要先读本文件，再读对应目录的 README/SKILL。

## 硬件事实（不要再猜）

| 项目 | 值 |
|---|---|
| 驱动器 | DSP86，**AC 18–80 V 输入**（本系统 24 VAC） |
| 电机 | 86BYG450-79-A50-J5，4 线，5.0 A，≈5.5 N·m |
| 相组（实测） | **蓝 A+、黄 A−（0.5 Ω）；红 B+、绿 B−（0.2 Ω）** |
| 电流 | **4.86 A**（97%），DIP `SW1=off SW2=on SW3=off` |
| 细分 | **400 步/转**，0.9°，`SW5–SW8=on` |
| 半流 | `SW4=off` |
| 俯仰行程 | **-30°（俯）～ +70°（仰）**，机械硬限位 -32°/+72° |
| 主控 | ESP32-C3，3.3 V |
| 单轴引脚 | STEP=4，DIR=5，ENA=6，LIMIT=1，PITCH=3（ADC1_CH3） |
| 双轴扩展 | 方位 4/5/6 + HOME1；俯仰 7/10/0 + LIMIT20；PITCH3；串口仅 TX21 |
| 串口 | **COM6 / CH340，115200**，禁止写成其他端口 |
| 双轴电源 | 24VAC **≥600VA**，两台 DSP86 各 10A 慢断 |
| 热点 | `DSP86-C3` / `12345678`，`192.168.4.1` |
| 默认 STA | `<wifi-ssid>` / `<wifi-password>`，开机自动连接 |

## 关键电气语义

1. **ENA 是“脱机”信号**：光耦导通（GPIO 低）= 电机自由；光耦断开（GPIO 高）= 使能。
   默认 `enaActiveLow=false`，使能时 GPIO6 输出高。
2. 限位/急停触发：**停机 + 脱机**，排除后手动恢复使能。
3. 电机线禁止带电插拔；AC 侧必须经隔离变压器。
4. 传感器只接 0–3.3 V；5 V 必须分压。

## 已验证参考（优先复用）

- `C:\Users\Administrator\Documents\aily-project\C3BUJINTEST`：实测可用固件（COM6）。
  - 分页：电机 / WiFi / OTA；异步扫描；延迟配网切换。
  - `WiFi.setSleep(false)`、`setTxPower(WIFI_POWER_19_5dBm)`、`setAutoReconnect(true)`。
  - OTA：`Update.begin((ESP.getFreeSketchSpace()-0x1000)&0xFFFFF000)`。
  - 未用脚：0/7/10 拉低；2/9 保持输入（启动绑带）。
  - LED GPIO8：常亮=联网，闪=未联网。

## 目录

| 路径 | 内容 |
|---|---|
| `DSP86_86BYG_ESP32C3\` | 接线手册（docx/md）、图、Skill 源、3D 云台图 |
| `DSP86_C3_Web\` | Web 控制器（PlatformIO，COM6，OTA/配网/俯仰闭环） |
| `DSP86_86BYG_ESP32C3_交付包\` | 统一交付包（文档 + web_controller + skill） |
| `.agents\skills\dsp86-86byg-esp32\` | 已安装 Skill |

## 修改流程

1. 改代码/参数前先读 `README.md` 与 `SKILL.md`。
2. 改完同步四处：README、docx（`build_docx_s3.py` 重新生成）、Skill、交付包。
3. 重新渲染 docx 检查分页（office-render），图片重新生成后再嵌入。
4. 变更电流/细分/线色/引脚时，同步更新图 1/3/4 与 Skill。
5. 不要改 COM6 为其他端口；不要在文档里写“低电平使能”。

## 构建/烧录

- Web 控制器：`cd DSP86_C3_Web && pio run && pio run -t upload`（`upload_port=COM6`）。
- 交付包内 `web_controller\flash_com6.ps1` 一键编译+烧录+串口。
- 当前机器未安装 ESP32-C3 工具链时，不要声称“已编译/已烧录”。

## 文档风格

- 中文、表格优先、给可执行步骤。
- 不确定的电气结论必须标注“实测为准”。
- 3D 图是示意图，不是加工图。

