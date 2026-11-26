#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""DSP86 拨码计算器：电流 / 细分 / 电流校验 / 脉冲当量。

示例：
  python dip_calc.py --current 5.43 --ppr 400 --motor 5.0 --connection parallel --lead 5
  python dip_calc.py --ppr 1600 --motor 5.0 --connection series
"""
import argparse
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

# REF 电流, 峰值, SW1, SW2, SW3
CURRENTS = [
    (2.00, 2.40, "on", "on", "on"),
    (2.57, 3.08, "off", "on", "on"),
    (3.14, 3.77, "on", "off", "on"),
    (3.71, 4.45, "off", "off", "on"),
    (4.28, 5.14, "on", "on", "off"),
    (4.86, 5.83, "off", "on", "off"),
    (5.43, 6.52, "on", "off", "off"),
    (6.00, 7.20, "off", "off", "off"),
]

# 脉冲/转: (SW5, SW6, SW7, SW8)
PPR = {
    400: ("on", "on", "on", "on"),
    800: ("off", "on", "on", "on"),
    1600: ("on", "off", "on", "on"),
    3200: ("off", "off", "on", "on"),
    6400: ("on", "on", "off", "on"),
    12800: ("off", "on", "off", "on"),
    25600: ("on", "off", "off", "on"),
    51200: ("off", "off", "off", "on"),
    1000: ("on", "on", "on", "off"),
    2000: ("off", "on", "on", "off"),
    4000: ("on", "off", "on", "off"),
    5000: ("off", "off", "on", "off"),
    8000: ("on", "on", "off", "off"),
    10000: ("off", "on", "off", "off"),
    20000: ("on", "off", "off", "off"),
    40000: ("off", "off", "off", "off"),
}

FACTOR = {"series": 0.707, "parallel": 1.414, "4wire": 1.0}


def pick_current(rated, limit=1.06):
    """自动选择不超过额定值的最大档。"""
    ok = [c for c in CURRENTS if c[0] <= rated * limit]
    return ok[-1] if ok else CURRENTS[0]


def nearest_current(value):
    return min(CURRENTS, key=lambda c: abs(c[0] - value))


def main():
    ap = argparse.ArgumentParser(description="DSP86 DIP calculator")
    ap.add_argument("--current", type=float, help="目标 REF 电流 (A)，省略则自动选择")
    ap.add_argument("--ppr", type=int, default=400, help="脉冲/转，默认 400")
    ap.add_argument("--motor", type=float, default=5.0, help="电机铭牌相电流 (A)，默认 5.0")
    ap.add_argument("--connection", choices=FACTOR, default="parallel",
                    help="series / parallel / 4wire，默认 parallel")
    ap.add_argument("--lead", type=float, default=5.0, help="丝杠导程 mm，默认 5")
    ap.add_argument("--sw4", choices=["on", "off"], default="off", help="半流/全流，默认 off")
    args = ap.parse_args()

    if args.ppr not in PPR:
        print(f"[错误] 不支持的脉冲/转: {args.ppr}")
        print("可选:", ", ".join(str(k) for k in sorted(PPR)))
        return 2

    rated = args.motor * FACTOR[args.connection]
    if args.current is None:
        ref, peak, sw1, sw2, sw3 = pick_current(rated)
        auto = True
    else:
        ref, peak, sw1, sw2, sw3 = nearest_current(args.current)
        auto = False
        if abs(ref - args.current) > 0.01:
            print(f"[提示] 表中无 {args.current:.2f} A，已选最接近的 {ref:.2f} A")

    ratio = ref / rated * 100
    if ratio <= 100.5:
        verdict = "OK"
    elif ratio <= 110:
        verdict = "CAUTION"
    else:
        verdict = "OVER"

    sw5, sw6, sw7, sw8 = PPR[args.ppr]
    angle = 360.0 / args.ppr
    step_mm = args.lead / args.ppr

    print("=" * 52)
    print("DSP86 拨码配置")
    print("=" * 52)
    print(f"电机接法      : {args.connection}  (系数 x{FACTOR[args.connection]:.3f})")
    print(f"每相额定      : {rated:.2f} A  (铭牌 {args.motor:.2f} A)")
    print(f"设定电流      : {ref:.2f} A REF / {peak:.2f} A PK"
          + ("  [自动]" if auto else ""))
    print(f"占比          : {ratio:.1f}%  -> {verdict}")
    print()
    print("拨码:")
    print(f"  SW1={sw1:<3} SW2={sw2:<3} SW3={sw3:<3}   (电流 {ref:.2f} A)")
    print(f"  SW4={args.sw4:<3}                    (半流/全流)")
    print(f"  SW5={sw5:<3} SW6={sw6:<3} SW7={sw7:<3} SW8={sw8:<3}   ({args.ppr} 步/转)")
    print()
    print(f"步距角        : {angle:.4f}°")
    print(f"脉冲当量      : {step_mm * 1000:.4f} um/step  (导程 {args.lead} mm)")
    print("转速:")
    for hz in (400, 800, 2000, 4000):
        print(f"  {hz:>5} Hz -> {hz / args.ppr * 60:6.1f} rpm")
    print()

    if verdict == "OVER":
        print("[警告] 电流超过该接法额定值 10% 以上，长期运行会过热。")
        sug = pick_current(rated)
        print(f"       建议改 {sug[0]:.2f} A: SW1={sug[2]} SW2={sug[3]} SW3={sug[4]}")
        return 1
    if verdict == "CAUTION":
        print("[注意] 电流略高于额定（<110%），可运行，但需监测温升；过热则降一档。")
    if args.ppr == 400:
        print("[提示] 400 步/转 = 0.9°，半步模式，1–2 rps 易共振；"
              "振动大可改 1600 步/转。")
    return 0


if __name__ == "__main__":
    sys.exit(main())


