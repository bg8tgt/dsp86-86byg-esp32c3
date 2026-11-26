# DSP86 步进电机 Web 控制器（ESP32-C3）

一套跑在 ESP32-C3 上的步进电机测试/控制固件：网页里看状态、改参数、点动、扫描配网、OTA 升级；WiFi 热点常开，STA 开机自动连接。

## 功能

- **概览**：STA/AP 状态、IP、RSSI、MAC、运行时间、内存、电机位置/速度/限位/俯仰角
- **电机**：在线查看/设置脉冲每转、最大速度、加速度、方向；使能/失能、点动、停止、位置清零
- **俯仰**：-30°（俯）～ +70°（仰）闭环定位、目标角度、两点校准、传感器故障/超程保护
- **WiFi**：扫描附近网络、选择并保存 SSID/密码、自动重连；热点始终开启
- **OTA**：浏览器上传 `.bin` 固件，带进度条，完成后自动重启
- **保护**：上限位/急停输入触发立即停机并禁止点动

## 默认网络

| 项目 | 值 |
|---|---|
| STA SSID | `<wifi-ssid>` |
| STA 密码 | `<wifi-password>` |
| 热点 SSID | `DSP86-C3` |
| 热点密码 | `12345678` |
| 热点地址 | `http://192.168.4.1` |
| mDNS | `http://dsp86.local`（STA 已联网时） |

首次上电会自动连接默认 WiFi；连不上时热点仍然可用，用手机连 `DSP86-C3` 后打开 `192.168.4.1` 重新配网。配置保存在 NVS，掉电不丢。

> **注意**：ESP32-C3 只支持 **2.4 GHz**。如果 `<wifi-ssid>` 是 5 GHz 专用 SSID，会一直连不上——请改用路由器的 2.4 GHz SSID。

## 引脚

| 功能 | GPIO | 说明 |
|---|---|---|
| STEP | 4 | 接 DSP86 PUL− |
| DIR | 5 | 接 DSP86 DIR− |
| ENA | 6 | 接 DSP86 ENA−，低电平使能 |
| LIMIT | 1 | 内部上拉，闭合到 GND 触发 |
| PITCH | 3 | ADC1_CH3，0–3.3 V 俯仰角传感器 |

5V/GND 按共阳接法接 DSP86 的 PUL+/DIR+；具体见接线手册。

## 烧录（COM6）

### PlatformIO（推荐）

```powershell
cd E:\deepseek\DSP86_C3_Web
pio run                 # 编译
pio run -t upload       # 按 platformio.ini 的 upload_port = COM6 烧录
pio device monitor      # COM6 @115200
```

`platformio.ini` 已固定：

```ini
upload_port = COM6
monitor_port = COM6
```

如果换口，只改这两行，别在命令里临时写错端口。

### Arduino IDE

1. 开发板选 **ESP32C3 Dev Module**
2. 端口选 **COM6**
3. 库：FastAccelStepper、ArduinoJson
4. 分区表选默认（含 OTA）
5. 若用板载 USB 口而不是 UART 口，把 `ARDUINO_USB_CDC_ON_BOOT` 改为 1

### 一键脚本

```powershell
.\flash_com6.ps1
```

## 网页

烧录后：

- 手机/电脑连 `DSP86-C3`（密码 `12345678`）→ 打开 `http://192.168.4.1`
- 或与设备在同一路由器下 → 打开设备 IP / `http://dsp86.local`

五个标签页：**概览 / 电机 / 俯仰 / WiFi / OTA 升级**。

### 电机参数说明

| 参数 | 作用 |
|---|---|
| 脉冲/转 | 仅用于换算圈数与角度，需与 DSP86 拨码一致（当前 400） |
| 最大速度 | FastAccelStepper 的 `setSpeedInHz`，400 步/转时 2000 Hz = 300 rpm |
| 加速度 | 步/s²，24 VAC 下建议 2000–6000 |
| 反向 | 等价于交换 DIR 逻辑 |

电流和细分仍由 DSP86 的 SW1–SW8 拨码决定，软件不能改。

## OTA

1. 编译后固件在 `.pio\build\esp32-c3-devkitm-1\firmware.bin`
2. 打开网页 **OTA 升级** → 选择 `.bin` → 开始升级
3. 进度到 100% 后设备自动重启

升级失败常见原因：选错文件（不是本项目固件）、分区表不含 OTA、上传中断电。

## API（可选）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/status` | 全部状态 JSON |
| POST | `/api/motor` | `{steps_per_rev,max_speed,accel,invert}` |
| POST | `/api/jog` | `{steps}`，相对移动 |
| POST | `/api/stop` | 急停 |
| POST | `/api/pitch/target` | `{angle}`，-30～+70 |
| POST | `/api/pitch/enable` | `{on:true/false}` |
| POST | `/api/pitch/stop` | 停止俯仰闭环 |
| POST | `/api/pitch/cal` | `{point:"min"|"max"}` 两点校准 |
| POST | `/api/zero` | 位置清零 |
| POST | `/api/enable` | `{on:true/false}` |
| GET | `/api/scan` | 扫描结果数组 |
| POST | `/api/wifi` | `{ssid,password}` |
| POST | `/api/ota` | multipart 上传固件 |

## 故障排查

| 现象 | 处理 |
|---|---|
| 打不开网页 | 确认连的是 `DSP86-C3`；AP 密码 `12345678` |
| STA 一直连不上 | 确认 SSID 是 2.4 GHz、密码正确；看概览页状态码 |
| 电机不转 | 检查 ENA、限位是否触发、DSP86 AC 电源与拨码 |
| 点动报 limit | 限位/急停闭合，排查 GPIO1 回路 |
| OTA 500 | 固件不是本项目生成的、分区表不对 |
| COM6 打不开 | 装 CH340/CP210x 驱动，确认设备管理器端口号 |

## 目录

```
DSP86_C3_Web/
├─ platformio.ini
├─ flash_com6.ps1
├─ README.md
└─ src/
   ├─ main.cpp
   └─ web_ui.h
```

