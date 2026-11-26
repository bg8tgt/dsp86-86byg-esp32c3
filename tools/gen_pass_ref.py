# -*- coding: utf-8 -*-
"""用 Skyfield（与 OrbitLink 同源）生成 AO-91/SO-50 的过境与观测角基准，供 C# 交叉验证。"""
import json, os, sys, datetime as dt
from skyfield.api import load, wgs84, EarthSatellite
from skyfield.toposlib import GeographicPosition

VENV = r"E:\ESP32\逆向文件夹\pc_upgrade\.venv\Lib\site-packages"
sys.path.insert(0, VENV)

CACHE = r"E:\deepseek\YzControl\YzControl\SatData\celestrak_omm_cache.json"
LAT, LON, ALT = 23 + 1/60 + 15/3600, 104 + 22/60 + 30/3600, 1234.0
TS = load.timescale()
QTH = wgs84.latlon(LAT, LON, elevation_m=ALT)

omm = json.load(open(CACHE, encoding="utf-8"))
targets = {"RADFXSAT (FOX-1B)": 43017, "SAUDISAT 1C (SO-50)": 27607}
out = {}
now = dt.datetime.now(dt.timezone.utc)

for item in omm:
    if item["NORAD_CAT_ID"] not in targets.values():
        continue
    sat = EarthSatellite.from_omm(TS, item)
    name = item["OBJECT_NAME"]
    # 采样：未来 6 小时，每 30 秒；求过境
    passes = []
    t0 = now
    minutes = 6 * 60
    step = 0.5  # 30s
    prev_el = None
    aos = None
    for m in range(0, minutes * 2 + 1):
        t = TS.from_datetime(t0 + dt.timedelta(minutes=m * step))
        alt, az, dist = (sat - QTH).at(t).altaz()
        el = alt.degrees
        if prev_el is not None:
            if prev_el < 0 <= el:
                aos = (t0 + dt.timedelta(minutes=m * step), az.degrees, el)
            elif prev_el >= 0 > el and aos is not None:
                passes.append({"aos": aos[0].isoformat(), "los": (t0 + dt.timedelta(minutes=m * step)).isoformat()})
                aos = None
        prev_el = el
    # 观测角 + 多普勒（固定三个时刻）
    samples = []
    for sec in (0, 120, 300, 600):
        t = TS.from_datetime(now + dt.timedelta(seconds=sec))
        top = (sat - QTH).at(t)
        alt, az, dist = top.altaz()
        rng = top.position.km
        # 距离率：±0.5 s 差分
        t1 = TS.from_datetime(now + dt.timedelta(seconds=sec + 0.5))
        t0_ = TS.from_datetime(now + dt.timedelta(seconds=sec - 0.5))
        d1 = (sat - QTH).at(t1).distance().km
        d0 = (sat - QTH).at(t0_).distance().km
        rr = (d1 - d0) / 1.0
        samples.append({"utc": t.utc_iso(), "az": az.degrees, "el": alt.degrees,
                        "range_km": dist.km, "range_rate": rr})
    out[name] = {"norad": item["NORAD_CAT_ID"], "passes": passes[:4], "samples": samples}

dst = r"E:\deepseek\YzControl\YzControl\Sat\pass_ref.json"
json.dump(out, open(dst, "w", encoding="utf-8"), indent=1)
print("OK ->", dst, "passes:", {k: len(v["passes"]) for k, v in out.items()})
