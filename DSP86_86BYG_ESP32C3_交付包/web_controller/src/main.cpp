/*
 * ESP32-C3 + DSP86 + 86BYG450 Web 控制器
 * ------------------------------------------------------------
 * 功能：
 *   - 步进电机点动 / 停止 / 清零 / 使能
 *   - 电机参数在线查看与设置（步数/转、最大速度、加速度、方向）
 *   - WiFi 信息展示、扫描、配网
 *   - WiFi 热点常开（AP+STA 共存）
 *   - 网页 OTA 固件升级
 *   - 上限位 / 急停输入，俯仰角 ADC 显示
 *
 * 默认 WiFi：<wifi-ssid> / <wifi-password>，开机自动连接
 * 热点：DSP86-C3 / dsp86c3ap，IP 192.168.4.1
 *
 * 串口：COM6 @115200
 */

#include <Arduino.h>
#include <WiFi.h>
#include <WebServer.h>
#include <ESPmDNS.h>
#include <Update.h>
#include <Preferences.h>
#include <ArduinoJson.h>
#include <FastAccelStepper.h>

#include "web_ui.h"

// ---------------- 版本与默认值 ----------------
static const char *FW_VERSION = "1.0.0";
static const char *AP_SSID = "DSP86-C3";
static const char *AP_PASS = "12345678";
static const char *DEF_STA_SSID = "<wifi-ssid>";
static const char *DEF_STA_PASS = "<wifi-password>";

// ---------------- 引脚（与接线文档一致） ----------------
static const int PIN_STEP = 4;
static const int PIN_DIR = 5;
static const int PIN_ENA = 6;
static const int PIN_LIMIT = 1;
static const int PIN_PITCH = 3;

// ---------------- 全局 ----------------
WebServer server(80);
Preferences prefs;
FastAccelStepperEngine engine = FastAccelStepperEngine();
FastAccelStepper *stepper = nullptr;

struct MotorConfig {
  uint32_t stepsPerRev = 400;
  uint32_t maxSpeedHz = 2000;
  uint32_t accel = 4000;
  bool invertDir = true;
};

MotorConfig cfg;
String staSsid;
String staPass;

bool motorEnabled = false;
bool enaActiveLow = false;   // DSP86：false=使能时光耦断开(GPIO高)
bool limitActive = false;
float pitchAngle = 0.0f;
float currentSpeedHz = 0.0f;
float pitchVolt = 0.0f;

uint32_t lastWifiTry = 0;
uint32_t lastPitchRead = 0;
uint32_t rebootAt = 0;
bool otaSuccess = false;

// 俯仰云台：-30°（俯角）～ +70°（仰角）
struct PitchConfig {
  float minAngle = -30.0f;
  float maxAngle = 70.0f;
  float vMin = 0.50f;          // 下限对应的传感器电压
  float vMax = 3.00f;          // 上限对应的传感器电压
  float deadband = 0.30f;      // 死区（度）
  float kp = 3.0f;             // 比例增益（步/度）
  float maxStepPerCycle = 80;  // 每 50ms 最大步数
  bool enabled = false;
  float target = 0.0f;
};

PitchConfig pcfg;
bool pitchFault = false;
float pitchIntegral = 0.0f;
uint32_t lastPitchCtrl = 0;

// ---------------- 工具 ----------------
static const char *wifiStatusText(wl_status_t s) {
  switch (s) {
    case WL_IDLE_STATUS: return "IDLE";
    case WL_NO_SSID_AVAIL: return "NO_SSID";
    case WL_SCAN_COMPLETED: return "SCAN_DONE";
    case WL_CONNECTED: return "CONNECTED";
    case WL_CONNECT_FAILED: return "CONNECT_FAILED";
    case WL_CONNECTION_LOST: return "CONNECTION_LOST";
    case WL_DISCONNECTED: return "DISCONNECTED";
    default: return "UNKNOWN";
  }
}

void setMotorEnabled(bool on) {
  // DSP86 的 ENA 是“脱机”信号：光耦导通 = 电机自由，光耦断开 = 使能。
  // 默认 enaActiveLow=false：使能 -> GPIO 高（光耦断开），脱机 -> GPIO 低。
  digitalWrite(PIN_ENA, (on == enaActiveLow) ? LOW : HIGH);
  motorEnabled = on;
}

float readPitch() {
  int mv = analogReadMilliVolts(PIN_PITCH);
  pitchVolt = mv / 1000.0f;

  // 传感器失效：断线、短路、超量程
  pitchFault = (pitchVolt < 0.05f || pitchVolt > 3.25f);

  float span = pcfg.vMax - pcfg.vMin;
  if (span < 0.2f) span = 0.2f;
  float a = pcfg.minAngle +
            (pitchVolt - pcfg.vMin) *
            (pcfg.maxAngle - pcfg.minAngle) / span;
  if (a < pcfg.minAngle) a = pcfg.minAngle;
  if (a > pcfg.maxAngle) a = pcfg.maxAngle;
  pitchAngle = a;
  return a;
}

// 俯仰闭环：每 50ms 调一次，误差 -> 步数增量
void pitchControl() {
  if (!pcfg.enabled) return;

  if (pitchFault) {
    pcfg.enabled = false;
    if (stepper) stepper->forceStop();
    Serial.println("Pitch sensor fault, loop disabled");
    return;
  }
  if (pitchAngle < pcfg.minAngle - 2.0f || pitchAngle > pcfg.maxAngle + 2.0f) {
    pcfg.enabled = false;
    if (stepper) stepper->forceStop();
    Serial.println("Pitch soft limit, loop disabled");
    return;
  }

  float err = pcfg.target - pitchAngle;
  if (fabsf(err) <= pcfg.deadband) {
    pitchIntegral = 0;
    return;
  }

  pitchIntegral += err;
  if (pitchIntegral > 20.0f) pitchIntegral = 20.0f;
  if (pitchIntegral < -20.0f) pitchIntegral = -20.0f;

  float cmd = pcfg.kp * err + 0.6f * pitchIntegral;
  if (cmd > pcfg.maxStepPerCycle) cmd = pcfg.maxStepPerCycle;
  if (cmd < -pcfg.maxStepPerCycle) cmd = -pcfg.maxStepPerCycle;
  if (fabsf(cmd) < 1.0f) return;

  if (stepper && !stepper->isRunning()) {
    if (!motorEnabled) setMotorEnabled(true);
    stepper->move((int32_t)cmd);
  }
}

void applyMotorConfig() {
  if (!stepper) return;
  stepper->setSpeedInHz(cfg.maxSpeedHz);
  stepper->setAcceleration(cfg.accel);
  stepper->setDirectionPin(PIN_DIR, cfg.invertDir);
}

void loadConfig() {
  prefs.begin("dsp86", false);
  staSsid = prefs.getString("ssid", DEF_STA_SSID);
  staPass = prefs.getString("pass", DEF_STA_PASS);
  cfg.stepsPerRev = prefs.getUInt("spr", 400);
  cfg.maxSpeedHz = prefs.getUInt("speed", 2000);
  cfg.accel = prefs.getUInt("accel", 4000);
  cfg.invertDir = prefs.getBool("invert", true);
  enaActiveLow = prefs.getBool("enaLow", false);

  pcfg.minAngle = prefs.getFloat("pmin", -30.0f);
  pcfg.maxAngle = prefs.getFloat("pmax", 70.0f);
  pcfg.vMin = prefs.getFloat("pvmin", 0.50f);
  pcfg.vMax = prefs.getFloat("pvmax", 3.00f);
  pcfg.deadband = prefs.getFloat("pdb", 0.30f);
  pcfg.kp = prefs.getFloat("pkp", 3.0f);
  pcfg.target = prefs.getFloat("ptarget", 0.0f);

  if (pcfg.minAngle > -5.0f || pcfg.minAngle < -60.0f) pcfg.minAngle = -30.0f;
  if (pcfg.maxAngle < 30.0f || pcfg.maxAngle > 90.0f) pcfg.maxAngle = 70.0f;
  if (pcfg.deadband < 0.05f || pcfg.deadband > 3.0f) pcfg.deadband = 0.30f;
  if (pcfg.kp < 0.5f || pcfg.kp > 20.0f) pcfg.kp = 3.0f;
  if (pcfg.vMax - pcfg.vMin < 0.3f) { pcfg.vMin = 0.50f; pcfg.vMax = 3.00f; }
  pcfg.target = constrain(pcfg.target, pcfg.minAngle, pcfg.maxAngle);

  if (cfg.stepsPerRev < 200 || cfg.stepsPerRev > 51200) cfg.stepsPerRev = 400;
  if (cfg.maxSpeedHz < 100 || cfg.maxSpeedHz > 200000) cfg.maxSpeedHz = 2000;
  if (cfg.accel < 100 || cfg.accel > 1000000) cfg.accel = 4000;

  Serial.printf("WiFi: %s\n", staSsid.c_str());
}

void saveConfig() {
  prefs.putUInt("spr", cfg.stepsPerRev);
  prefs.putUInt("speed", cfg.maxSpeedHz);
  prefs.putUInt("accel", cfg.accel);
  prefs.putBool("invert", cfg.invertDir);
  prefs.putBool("enaLow", enaActiveLow);
}

void savePitchConfig() {
  prefs.putFloat("pmin", pcfg.minAngle);
  prefs.putFloat("pmax", pcfg.maxAngle);
  prefs.putFloat("pvmin", pcfg.vMin);
  prefs.putFloat("pvmax", pcfg.vMax);
  prefs.putFloat("pdb", pcfg.deadband);
  prefs.putFloat("pkp", pcfg.kp);
  prefs.putFloat("ptarget", pcfg.target);
}

void startSTA() {
  if (staSsid.isEmpty()) return;
  Serial.printf("Connecting WiFi: %s\n", staSsid.c_str());
  WiFi.begin(staSsid.c_str(), staPass.c_str());
  lastWifiTry = millis();
}

// ---------------- JSON 状态 ----------------
String buildStatusJson() {
  JsonDocument doc;
  doc["fw"] = FW_VERSION;
  doc["uptime"] = millis() / 1000;
  doc["heap"] = ESP.getFreeHeap();

  JsonObject w = doc["wifi"].to<JsonObject>();
  w["connected"] = (WiFi.status() == WL_CONNECTED);
  w["status"] = wifiStatusText(WiFi.status());
  w["ssid"] = WiFi.SSID();
  w["ip"] = WiFi.localIP().toString();
  w["rssi"] = WiFi.RSSI();
  w["mac"] = WiFi.macAddress();
  w["ap_ssid"] = AP_SSID;
  w["ap_ip"] = WiFi.softAPIP().toString();
  w["ap_clients"] = WiFi.softAPgetStationNum();
  w["hostname"] = "dsp86";

  JsonObject m = doc["motor"].to<JsonObject>();
  m["pos"] = stepper ? stepper->getCurrentPosition() : 0;
  m["speed"] = currentSpeedHz;
  m["running"] = stepper ? stepper->isRunning() : false;
  m["enabled"] = motorEnabled;
  m["limit"] = limitActive;
  m["spr"] = cfg.stepsPerRev;
  m["max_speed"] = cfg.maxSpeedHz;
  m["accel"] = cfg.accel;
  m["invert"] = cfg.invertDir;
  m["ena_low"] = enaActiveLow;
  m["pitch"] = pitchAngle;
  m["pitch_v"] = pitchVolt;

  JsonObject pt = doc["pitch"].to<JsonObject>();
  pt["angle"] = pitchAngle;
  pt["volt"] = pitchVolt;
  pt["target"] = pcfg.target;
  pt["enabled"] = pcfg.enabled;
  pt["error"] = pcfg.target - pitchAngle;
  pt["min"] = pcfg.minAngle;
  pt["max"] = pcfg.maxAngle;
  pt["deadband"] = pcfg.deadband;
  pt["kp"] = pcfg.kp;
  pt["vmin"] = pcfg.vMin;
  pt["vmax"] = pcfg.vMax;
  pt["fault"] = pitchFault;
  pt["running"] = stepper ? stepper->isRunning() : false;

  JsonObject p = doc["pins"].to<JsonObject>();
  p["step"] = PIN_STEP;
  p["dir"] = PIN_DIR;
  p["ena"] = PIN_ENA;
  p["limit"] = PIN_LIMIT;
  p["pitch"] = PIN_PITCH;

  String out;
  serializeJson(doc, out);
  return out;
}

// ---------------- HTTP ----------------
void handleRoot() {
  server.sendHeader("Cache-Control", "no-store");
  server.send(200, "text/html; charset=utf-8", String(INDEX_HTML));
}

void handleStatus() {
  server.sendHeader("Cache-Control", "no-store");
  server.send(200, "application/json", buildStatusJson());
}

void handleMotorSave() {
  JsonDocument doc;
  DeserializationError err = deserializeJson(doc, server.arg("plain"));
  if (err) {
    server.send(400, "application/json", "{\"ok\":false,\"msg\":\"bad json\"}");
    return;
  }
  if (stepper && stepper->isRunning()) stepper->forceStop();

  if (doc["steps_per_rev"].is<uint32_t>()) {
    uint32_t v = doc["steps_per_rev"].as<uint32_t>();
    if (v >= 200 && v <= 51200) cfg.stepsPerRev = v;
  }
  if (doc["max_speed"].is<uint32_t>()) {
    uint32_t v = doc["max_speed"].as<uint32_t>();
    if (v >= 100 && v <= 200000) cfg.maxSpeedHz = v;
  }
  if (doc["accel"].is<uint32_t>()) {
    uint32_t v = doc["accel"].as<uint32_t>();
    if (v >= 100 && v <= 1000000) cfg.accel = v;
  }
  if (doc["invert"].is<bool>()) cfg.invertDir = doc["invert"].as<bool>();

  applyMotorConfig();
  saveConfig();
  server.send(200, "application/json", "{\"ok\":true}");
}

void handleJog() {
  if (limitActive) {
    server.send(409, "application/json", "{\"ok\":false,\"msg\":\"limit active\"}");
    return;
  }
  JsonDocument doc;
  if (deserializeJson(doc, server.arg("plain"))) {
    server.send(400, "application/json", "{\"ok\":false,\"msg\":\"bad json\"}");
    return;
  }
  int32_t steps = doc["steps"] | 0;
  if (steps == 0) {
    server.send(400, "application/json", "{\"ok\":false,\"msg\":\"steps=0\"}");
    return;
  }
  if (steps > 200000) steps = 200000;
  if (steps < -200000) steps = -200000;

  if (!stepper) {
    server.send(500, "application/json", "{\"ok\":false,\"msg\":\"stepper not ready\"}");
    return;
  }
  if (!motorEnabled) setMotorEnabled(true);
  stepper->move(steps);
  server.send(200, "application/json", "{\"ok\":true}");
}

void handleStop() {
  if (stepper) stepper->forceStop();
  server.send(200, "application/json", "{\"ok\":true}");
}

void handleZero() {
  if (stepper) {
    stepper->forceStop();
    stepper->setCurrentPosition(0);
  }
  server.send(200, "application/json", "{\"ok\":true}");
}

void handleEnable() {
  JsonDocument doc;
  if (deserializeJson(doc, server.arg("plain"))) {
    server.send(400, "application/json", "{\"ok\":false}");
    return;
  }
  bool on = doc["on"] | false;
  setMotorEnabled(on);
  server.send(200, "application/json", on ? "{\"ok\":true,\"enabled\":true}"
                                          : "{\"ok\":true,\"enabled\":false}");
}


void handlePitchTarget() {
  JsonDocument doc;
  if (deserializeJson(doc, server.arg("plain"))) {
    server.send(400, "application/json", "{\"ok\":false,\"msg\":\"bad json\"}");
    return;
  }
  float a = doc["angle"] | 0.0f;
  a = constrain(a, pcfg.minAngle, pcfg.maxAngle);
  pcfg.target = a;
  savePitchConfig();
  server.send(200, "application/json", "{\"ok\":true}");
}

void handlePitchEnable() {
  JsonDocument doc;
  if (deserializeJson(doc, server.arg("plain"))) {
    server.send(400, "application/json", "{\"ok\":false}");
    return;
  }
  bool on = doc["on"] | false;
  if (on) {
    if (pitchFault) {
      server.send(409, "application/json", "{\"ok\":false,\"msg\":\"sensor fault\"}");
      return;
    }
    if (pitchAngle < pcfg.minAngle - 2.0f || pitchAngle > pcfg.maxAngle + 2.0f) {
      server.send(409, "application/json", "{\"ok\":false,\"msg\":\"out of range\"}");
      return;
    }
    setMotorEnabled(true);
    pitchIntegral = 0;
    pcfg.enabled = true;
  } else {
    pcfg.enabled = false;
    if (stepper) stepper->forceStop();
  }
  server.send(200, "application/json", pcfg.enabled
    ? "{\"ok\":true,\"enabled\":true}"
    : "{\"ok\":true,\"enabled\":false}");
}

void handlePitchStop() {
  pcfg.enabled = false;
  if (stepper) stepper->forceStop();
  server.send(200, "application/json", "{\"ok\":true}");
}

void handlePitchCal() {
  JsonDocument doc;
  if (deserializeJson(doc, server.arg("plain"))) {
    server.send(400, "application/json", "{\"ok\":false}");
    return;
  }
  String point = doc["point"] | "";
  if (pitchFault) {
    server.send(409, "application/json", "{\"ok\":false,\"msg\":\"sensor fault\"}");
    return;
  }

  float oldMin = pcfg.vMin, oldMax = pcfg.vMax;
  if (point == "min") pcfg.vMin = pitchVolt;
  else if (point == "max") pcfg.vMax = pitchVolt;
  else {
    server.send(400, "application/json", "{\"ok\":false,\"msg\":\"point?\"}");
    return;
  }

  if (pcfg.vMax - pcfg.vMin < 0.30f) {
    pcfg.vMin = oldMin;
    pcfg.vMax = oldMax;
    server.send(409, "application/json", "{\"ok\":false,\"msg\":\"voltage span too small\"}");
    return;
  }
  savePitchConfig();
  server.send(200, "application/json", "{\"ok\":true}");
}

void handleScan() {
  int n = WiFi.scanNetworks(false, true);
  JsonDocument doc;
  JsonArray arr = doc.to<JsonArray>();
  for (int i = 0; i < n; i++) {
    JsonObject o = arr.add<JsonObject>();
    o["ssid"] = WiFi.SSID(i);
    o["rssi"] = WiFi.RSSI(i);
    o["ch"] = WiFi.channel(i);
    o["enc"] = (WiFi.encryptionType(i) == WIFI_AUTH_OPEN) ? "OPEN" : "WPA";
  }
  WiFi.scanDelete();

  String out;
  serializeJson(doc, out);
  server.send(200, "application/json", out);
}

void handleWifiSave() {
  JsonDocument doc;
  if (deserializeJson(doc, server.arg("plain"))) {
    server.send(400, "application/json", "{\"ok\":false,\"msg\":\"bad json\"}");
    return;
  }
  String ssid = doc["ssid"] | "";
  String pass = doc["password"] | "";
  ssid.trim();
  if (ssid.isEmpty()) {
    server.send(400, "application/json", "{\"ok\":false,\"msg\":\"ssid empty\"}");
    return;
  }

  staSsid = ssid;
  staPass = pass;
  prefs.putString("ssid", staSsid);
  prefs.putString("pass", staPass);

  WiFi.disconnect();
  delay(100);
  startSTA();

  server.send(200, "application/json", "{\"ok\":true}");
}

// ---- OTA ----
void motorStopSafe() {
  pcfg.enabled = false;
  if (stepper) stepper->forceStop();
  setMotorEnabled(false);
}

void handleOtaUpload() {
  HTTPUpload &up = server.upload();
  if (up.status == UPLOAD_FILE_START) {
    otaSuccess = false;
    Serial.printf("OTA start: %s\n", up.filename.c_str());
    if (!up.filename.endsWith(".bin")) {
      Serial.println("OTA: not a .bin file");
      return;
    }
    motorStopSafe();
    uint32_t maxSketch = (ESP.getFreeSketchSpace() - 0x1000) & 0xFFFFF000;
    if (!Update.begin(maxSketch)) {
      Update.printError(Serial);
    }
  } else if (up.status == UPLOAD_FILE_WRITE) {
    if (Update.write(up.buf, up.currentSize) != up.currentSize) {
      Update.printError(Serial);
    }
  } else if (up.status == UPLOAD_FILE_END) {
    if (Update.end(true)) {
      otaSuccess = true;
      rebootAt = millis() + 1500;
      Serial.printf("OTA done: %u bytes\n", up.totalSize);
    } else {
      Update.printError(Serial);
    }
  }
}

void handleOtaDone() {
  if (otaSuccess) {
    server.send(200, "application/json", "{\"ok\":true,\"reboot\":true}");
  } else {
    server.send(500, "application/json", "{\"ok\":false,\"msg\":\"update failed\"}");
  }
}

// ---------------- setup ----------------
void setup() {
  Serial.begin(115200);
  delay(200);
  Serial.println("\nDSP86-C3 web controller");

  pinMode(PIN_ENA, OUTPUT);
  setMotorEnabled(false);       // 上电先脱机，初始化后再使能
  pinMode(PIN_LIMIT, INPUT_PULLUP);
  analogReadResolution(12);
  analogSetAttenuation(ADC_11db);

  loadConfig();

  engine.init();
  stepper = engine.stepperConnectToPin(PIN_STEP);
  if (stepper) {
    stepper->setDirectionPin(PIN_DIR, cfg.invertDir);
    stepper->setSpeedInHz(cfg.maxSpeedHz);
    stepper->setAcceleration(cfg.accel);
    stepper->setCurrentPosition(0);
  } else {
    Serial.println("Stepper init failed!");
  }
  setMotorEnabled(true);

  // WiFi：热点常开 + STA（参数取自可用测试固件 C3BUJINTEST）
  WiFi.persistent(false);
  WiFi.mode(WIFI_AP_STA);
  WiFi.setSleep(false);                  // 关闭 modem sleep，避免网页轮询断连
  WiFi.setTxPower(WIFI_POWER_19_5dBm);
  WiFi.setHostname("dsp86-c3");
  WiFi.setAutoReconnect(true);
  bool apOk = WiFi.softAP(AP_SSID, AP_PASS, 6, 0, 4);
  Serial.printf("AP %s: %s  IP=%s\n", AP_SSID, apOk ? "ok" : "fail",
                WiFi.softAPIP().toString().c_str());
  startSTA();

  if (MDNS.begin("dsp86")) {
    MDNS.addService("http", "tcp", 80);
    Serial.println("mDNS: http://dsp86.local");
  }

  server.on("/", handleRoot);
  server.on("/api/status", HTTP_GET, handleStatus);
  server.on("/api/motor", HTTP_POST, handleMotorSave);
  server.on("/api/jog", HTTP_POST, handleJog);
  server.on("/api/stop", HTTP_POST, handleStop);
  server.on("/api/zero", HTTP_POST, handleZero);
  server.on("/api/enable", HTTP_POST, handleEnable);
  server.on("/api/pitch/target", HTTP_POST, handlePitchTarget);
  server.on("/api/pitch/enable", HTTP_POST, handlePitchEnable);
  server.on("/api/pitch/stop", HTTP_POST, handlePitchStop);
  server.on("/api/pitch/cal", HTTP_POST, handlePitchCal);
  server.on("/api/scan", HTTP_GET, handleScan);
  server.on("/api/wifi", HTTP_POST, handleWifiSave);
  server.on("/api/ota", HTTP_POST, handleOtaDone, handleOtaUpload);
  server.on("/favicon.ico", HTTP_GET, []() { server.send(204); });
  server.onNotFound([]() {
    server.send(404, "application/json", "{\"ok\":false,\"msg\":\"not found\"}");
  });
  server.begin();

  Serial.printf("AP:  http://%s\n", WiFi.softAPIP().toString().c_str());
  Serial.println("Ready.");
}

// ---------------- loop ----------------
void loop() {
  server.handleClient();

  // STA 断线重连（非阻塞）
  if (WiFi.status() != WL_CONNECTED && millis() - lastWifiTry > 15000) {
    startSTA();
  }

  // 限位/急停
  bool la = (digitalRead(PIN_LIMIT) == LOW);
  if (la != limitActive) {
    limitActive = la;
    if (limitActive) {
      if (stepper) stepper->forceStop();
      pcfg.enabled = false;
      setMotorEnabled(false);          // 触发后脱机，需手动恢复
      Serial.println("Limit active: stop + disable.");
    }
  }

  // 速度估算（每 500 ms 采样位置）
  static uint32_t lastSpeedMs = 0;
  static int32_t lastSpeedPos = 0;
  if (millis() - lastSpeedMs >= 500) {
    uint32_t now = millis();
    float dt = (now - lastSpeedMs) / 1000.0f;
    int32_t p = stepper ? stepper->getCurrentPosition() : 0;
    currentSpeedHz = (dt > 0) ? (p - lastSpeedPos) / dt : 0;
    lastSpeedPos = p;
    lastSpeedMs = now;
  }

  // 俯仰闭环
  if (millis() - lastPitchCtrl >= 50) {
    lastPitchCtrl = millis();
    pitchControl();
  }

  // 俯仰角
  if (millis() - lastPitchRead > 100) {
    lastPitchRead = millis();
    readPitch();
  }

  // OTA 完成后重启
  if (rebootAt && millis() >= rebootAt) {
    Serial.println("Rebooting...");
    delay(100);
    ESP.restart();
  }

  delay(2);
}


