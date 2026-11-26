/*
 * ESP32-C3 + 上海四宏 DSP86 + 86BYG450-79-A50-J5
 * ------------------------------------------------------------
 * 引脚：
 *   STEP = GPIO4      DIR = GPIO5      ENA = GPIO6
 *   LIMIT= GPIO1（限位/急停，内部上拉，低有效）
 *   PITCH= GPIO3（俯仰角传感器，ADC1_CH3，0–3.3 V）
 *
 * 拨码（当前配置）：
 *   4.86 A     : SW1=off SW2=on  SW3=off
 *   400 步/转  : SW5=on  SW6=on  SW7=on  SW8=on  → 0.9°/步
 *   半流待机   : SW4=off
 *
 * 电源：AC 24 V（隔离变压器），母线约 34 VDC。
 * 注意：4.86 A 适用于 4 线 5.0 A 电机（97%）；8 线串联请改 3.71 A。
 *
 * 串口命令（115200）：
 *   m400    移动 400 步（1 圈）
 *   s800    速度 800 步/秒（120 rpm）
 *   a1600   加速度
 *   z       位置清零
 *   p       读取俯仰角
 *   ?       状态
 */

#include <FastAccelStepper.h>

#define PIN_STEP   4
#define PIN_DIR    5
#define PIN_ENA    6
#define PIN_LIMIT  1
#define PIN_PITCH  3

#define DIR_INVERT        true
#define USE_ENA           true
#define ENA_ACTIVE_LOW    true    // 共阳：GPIO 低 = 光耦导通
#define LIMIT_ACTIVE_LOW  true    // 常开开关闭合到 GND = 触发
#define STEPS_PER_REV     400

// 俯仰角传感器标定（按实际传感器修改）
const float PITCH_V_MIN     = 0.50;   // 最小角度对应的电压 V
const float PITCH_V_MAX     = 3.00;   // 最大角度对应的电压 V
const float PITCH_ANGLE_MIN = -45.0;  // 最小角度
const float PITCH_ANGLE_MAX = 45.0;   // 最大角度

FastAccelStepperEngine engine = FastAccelStepperEngine();
FastAccelStepper *stepper = nullptr;

float pitchAngle = 0.0;
float pitchVolt  = 0.0;
bool  limitHit   = false;
bool  limitReported = false;

void setEnabled(bool en) {
  if (!USE_ENA) return;
  digitalWrite(PIN_ENA, (en == ENA_ACTIVE_LOW) ? LOW : HIGH);
}

float readPitch() {
  int mv = analogReadMilliVolts(PIN_PITCH);
  pitchVolt = mv / 1000.0;
  float a = PITCH_ANGLE_MIN +
            (pitchVolt - PITCH_V_MIN) *
            (PITCH_ANGLE_MAX - PITCH_ANGLE_MIN) /
            (PITCH_V_MAX - PITCH_V_MIN);
  if (a < PITCH_ANGLE_MIN) a = PITCH_ANGLE_MIN;
  if (a > PITCH_ANGLE_MAX) a = PITCH_ANGLE_MAX;
  pitchAngle = a;
  return a;
}

bool readLimit() {
  bool raw = (digitalRead(PIN_LIMIT) == LOW);
  if (LIMIT_ACTIVE_LOW) return raw;
  return !raw;
}

void setup() {
  Serial.begin(115200);
  delay(300);

  pinMode(PIN_LIMIT, INPUT_PULLUP);
  analogReadResolution(12);
  analogSetAttenuation(ADC_11db);   // 0–3.3 V 量程

  if (USE_ENA) {
    pinMode(PIN_ENA, OUTPUT);
    setEnabled(true);
  }

  engine.init();
  stepper = engine.stepperConnectToPin(PIN_STEP);
  if (!stepper) {
    Serial.println("STEP 引脚初始化失败");
    while (true) delay(1000);
  }

  stepper->setDirectionPin(PIN_DIR, DIR_INVERT);
  stepper->setSpeedInHz(800);
  stepper->setAcceleration(1600);
  stepper->setCurrentPosition(0);

  Serial.println();
  Serial.println("DSP86-S3 ready.");
  Serial.println("m<steps> s<hz> a<hz2> z p ?");
  Serial.printf("steps/rev=%d, 1 rev=%d steps\n", STEPS_PER_REV, STEPS_PER_REV);
}

void loop() {
  static uint32_t lastPitch = 0;
  if (millis() - lastPitch > 100) {
    lastPitch = millis();
    readPitch();
  }

  limitHit = readLimit();
  if (limitHit && stepper->isRunning()) {
    stepper->forceStop();
    setEnabled(false);
    if (!limitReported) {
      Serial.println("LIMIT/ESTOP 触发，已停止并脱机");
      limitReported = true;
    }
  }
  if (!limitHit) {
    if (limitReported) {
      limitReported = false;
      setEnabled(true);
      Serial.println("限位解除，恢复使能");
    }
  }

  if (!Serial.available()) return;

  String line = Serial.readStringUntil('\n');
  line.trim();
  if (line.length() == 0) return;

  char cmd = line[0];
  long val = line.substring(1).toInt();

  switch (cmd) {
    case 'm':
    case 'M':
      if (limitHit) {
        Serial.println("限位/急停有效，拒绝移动");
        break;
      }
      stepper->move(val);
      Serial.printf("move %ld (%.2f rev)\n", val, val / (float)STEPS_PER_REV);
      break;

    case 's':
    case 'S':
      if (val > 0 && val <= 200000) {
        stepper->setSpeedInHz(val);
        Serial.printf("speed %ld Hz\n", val);
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

    case 'p':
    case 'P':
      Serial.printf("pitch = %.2f deg  (%.3f V)\n", readPitch(), pitchVolt);
      break;

    case '?':
      Serial.printf("pos=%ld running=%d limit=%d pitch=%.2f deg (%.3f V)\n",
                    stepper->getCurrentPosition(),
                    stepper->isRunning(),
                    limitHit ? 1 : 0,
                    pitchAngle, pitchVolt);
      break;

    default:
      Serial.println("命令: m<steps> s<hz> a<hz2> z p ?");
  }
}


