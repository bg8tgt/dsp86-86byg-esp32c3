/*
 * ESP32 + 上海四宏 DSP86 + 86BYG450-79-A50-J5
 * ------------------------------------------------------------
 * 接线（共阳接法）：
 *   ESP32 5V   -> DSP86 PUL+ / DIR+
 *   ESP32 G18  -> DSP86 PUL-
 *   ESP32 G19  -> DSP86 DIR-
 *   ESP32 G23  -> DSP86 ENA-   （可选，悬空 = 默认使能）
 *   ESP32 GND  -> 不接（光耦隔离）
 *
 * 拨码（推荐）：
 *   电流 4.86A : SW1=off SW2=on  SW3=off
 *   半流待机   : SW4=off
 *   3200 步/转 : SW5=off SW6=off SW7=on  SW8=on
 *
 * 依赖库：FastAccelStepper（Arduino 库管理器安装）
 * 串口命令（115200，回车结束）：
 *   m3200   相对移动 3200 步（1 圈）
 *   s1600   设置速度 1600 步/秒
 *   a6400   设置加速度 6400 步/秒²
 *   z       当前位置清零
 *   ?       查询位置与运行状态
 */

#include <FastAccelStepper.h>

#define PIN_STEP 18
#define PIN_DIR  19
#define PIN_ENA  23          // 可选；不需要脱机控制时可悬空

// 共阳接法下若方向与预期相反，把 true 改为 false
#define DIR_INVERT true

// 是否使用 ENA（多数场合建议 false，让驱动器默认使能）
#define USE_ENA false

FastAccelStepperEngine engine = FastAccelStepperEngine();
FastAccelStepper *stepper = nullptr;

void setup() {
  Serial.begin(115200);
  delay(200);

  if (USE_ENA) {
    pinMode(PIN_ENA, OUTPUT);
    digitalWrite(PIN_ENA, LOW);   // 低电平 = 光耦导通（共阳）
  }

  engine.init();
  stepper = engine.stepperConnectToPin(PIN_STEP);
  if (!stepper) {
    Serial.println("STEP 引脚初始化失败");
    while (true) delay(1000);
  }

  stepper->setDirectionPin(PIN_DIR, DIR_INVERT);
  stepper->setSpeedInHz(3200);      // 3200 步/秒 = 1 转/秒
  stepper->setAcceleration(6400);   // 步/秒²
  stepper->setCurrentPosition(0);

  if (USE_ENA) {
    stepper->setEnablePin(PIN_ENA);
    stepper->setAutoEnable(true);
  }

  Serial.println();
  Serial.println("DSP86 ready.  m<steps>  s<hz>  a<hz2>  z  ?");
}

void loop() {
  if (!Serial.available()) return;

  String line = Serial.readStringUntil('\n');
  line.trim();
  if (line.length() == 0) return;

  char cmd = line[0];
  long val = line.substring(1).toInt();

  switch (cmd) {
    case 'm':
    case 'M':
      stepper->move(val);
      Serial.printf("move %ld steps\n", val);
      break;

    case 's':
    case 'S':
      if (val > 0 && val <= 200000) {
        stepper->setSpeedInHz(val);
        Serial.printf("speed %ld Hz\n", val);
      } else {
        Serial.println("速度范围 1..200000 Hz");
      }
      break;

    case 'a':
    case 'A':
      if (val > 0) {
        stepper->setAcceleration(val);
        Serial.printf("accel %ld\n", val);
      }
      break;

    case 'z':
    case 'Z':
      stepper->setCurrentPosition(0);
      Serial.println("position = 0");
      break;

    case '?':
      Serial.printf("pos=%ld  running=%d\n",
                    stepper->getCurrentPosition(),
                    stepper->isRunning());
      break;

    default:
      Serial.println("命令: m<steps> s<hz> a<hz2> z ?");
  }
}
