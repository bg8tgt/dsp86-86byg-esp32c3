# -*- coding: utf-8 -*-
import json, sys, datetime as dt
sys.path.insert(0, r"E:/ESP32/逆向文件夹/pc_upgrade/.venv/Lib/site-packages")
from skyfield.api import load, wgs84, EarthSatellite
CACHE = r"E:/deepseek/YzControl/YzControl/SatData/celestrak_omm_cache.json"
TS = load.timescale(); Q = wgs84.latlon(23+1/60+15/3600, 104+22/60+30/3600, elevation_m=1234)
omm = json.load(open(CACHE, encoding="utf-8"))
now = dt.datetime.now(dt.timezone.utc)
anchor = now.replace(minute=(now.minute//10)*10, second=0, microsecond=0)
out = {"anchor": anchor.isoformat(), "sats": {}}
for item in omm:
    if item["NORAD_CAT_ID"] not in (43017, 27607, 25544): continue
    sat = EarthSatellite.from_omm(TS, item)
    passes = []; prev = None; aos = None
    for m in range(0, 12*60+1):
        t = TS.from_datetime(anchor + dt.timedelta(minutes=m))
        alt, az, _ = (sat-Q).at(t).altaz(); el = alt.degrees
        if prev is not None:
            if prev < 0 <= el: aos = (anchor+dt.timedelta(minutes=m), az.degrees)
            elif prev >= 0 > el and aos is not None:
                # 细采样 5 s 找最大仰角
                mx = (-90, 0, None)
                for k in range(0, int((anchor+dt.timedelta(minutes=m)-aos[0]).total_seconds())+1, 5):
                    tt = TS.from_datetime(aos[0]+dt.timedelta(seconds=k))
                    a2, z2, _ = (sat-Q).at(tt).altaz()
                    if a2.degrees > mx[0]: mx = (a2.degrees, z2.degrees, aos[0]+dt.timedelta(seconds=k))
                passes.append({"aos": aos[0].isoformat(), "aos_az": aos[1],
                               "los": (anchor+dt.timedelta(minutes=m)).isoformat(), "los_az": az.degrees,
                               "max_el": mx[0], "max_az": mx[1]})
                aos = None
        prev = el
    out["sats"][item["OBJECT_NAME"]] = {"norad": item["NORAD_CAT_ID"], "passes": passes[:6]}
json.dump(out, open(r"E:/deepseek/YzControl/YzControl/Sat/pass_list.json","w",encoding="utf-8"), indent=1)
print("anchor", anchor.isoformat(), {k: len(v["passes"]) for k,v in out["sats"].items()})
