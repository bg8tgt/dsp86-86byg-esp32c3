# -*- coding: utf-8 -*-
"""world-atlas land-110m.topojson -> 简化陆地轮廓（每行一条环：lon,lat;lon,lat;...）"""
import json, math

src = r"C:/Users/Administrator/AppData/Local/Temp/land110.json"
import os
if not os.path.exists(src):
    src = "/tmp/land110.json"
d = json.load(open(src, encoding="utf-8"))
tf = d.get("transform", {"scale": [1, 1], "translate": [0, 0]})
sx, sy = tf["scale"]; tx, ty = tf["translate"]
arcs = []
for arc in d["arcs"]:
    x = y = 0; pts = []
    for dx, dy in arc:
        x += dx; y += dy
        pts.append((x * sx + tx, y * sy + ty))
    arcs.append(pts)

def ring(idxs):
    out = []
    for i in idxs:
        a = arcs[~i][::-1] if i < 0 else arcs[i]
        out.extend(a if not out else a[1:])
    return out

rings = []
for g in d["objects"]["land"]["geometries"]:
    t = g["type"]
    if t == "Polygon":
        for r in g["arcs"]:
            rings.append(ring(r))
    elif t == "MultiPolygon":
        for poly in g["arcs"]:
            for r in poly:
                rings.append(ring(r))

def dp(pts, eps):
    if len(pts) < 3: return pts
    # 简化（闭合环按首点连线做道格拉斯-普克）
    def rec(a, b):
        idx, dmax = -1, 0.0
        ax, ay = pts[a]; bx, by = pts[b]
        for i in range(a + 1, b):
            px, py = pts[i]
            if ax == bx and ay == by:
                dd = math.hypot(px - ax, py - ay)
            else:
                dd = abs((bx - ax) * (ay - py) - (ax - px) * (by - ay)) / math.hypot(bx - ax, by - ay)
            if dd > dmax: idx, dmax = i, dd
        if dmax > eps:
            return rec(a, idx) + rec(idx, b)[1:]
        return [pts[a], pts[b]]
    return rec(0, len(pts) - 1)

out = []
tot = 0
for r in rings:
    if len(r) < 4: continue
    s = dp(r, 0.45)
    if len(s) < 4: continue
    line = ";".join("%.2f,%.2f" % (x, y) for x, y in s)
    if len(line) < 40: continue
    out.append(line); tot += len(s)
print("rings=%d pts=%d chars=%d" % (len(out), tot, sum(len(l) for l in out)))
dst = r"E:/deepseek/YzControl/YzControl/Sat/world_land.txt"
open(dst, "w", encoding="ascii").write("\n".join(out))
print("->", dst)
