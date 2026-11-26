# -*- coding: utf-8 -*-
import sys, types, math
sys.path.insert(0, r"E:/ESP32/逆向文件夹/pc_upgrade/.venv/Lib/site-packages")
from sgp4.propagation import sgp4init, sgp4
from sgp4.earth_gravity import wgs72
from sgp4.ext import days2mdhms, jday

L1="1 29141U 85108AA  06170.26783845  .99999999  00000-0  13519-0 0   718"
L2="2 29141  82.4288 273.4882 0015848 277.2124  83.9133 15.93343074  6828"

def parse(l1,l2):
    s=types.SimpleNamespace()
    s.bstar=float("0."+l1[54:59].strip())*1e5 if False else None
    return s

# 手动解析
def sci(field):
    f=field.strip()
    if not f or f in '+-': return 0.0
    sign = -1.0 if f[0]=='-' else 1.0
    f2=f.lstrip('+-')
    mant, exp = f2.split('-') if '-' in f2 else (f2.split('+') if '+' in f2 else (f2,'0'))
    mant=mant.lstrip('0') or '0'
    return sign*float('0.'+mant)*10**int(exp)

bstar=sci(L1[53:61])
ndot=float(L1[33:43])
nddot=sci(L1[44:52])
epochyr=int(L1[18:20]); epochdays=float(L1[20:32])
inclo=math.radians(float(L2[8:16]))
nodeo=math.radians(float(L2[17:25]))
ecco=float('0.'+L2[26:33].strip())
argpo=math.radians(float(L2[34:42]))
mo=math.radians(float(L2[43:51]))
no_kozai=float(L2[52:63])*2*math.pi/1440.0
xpdotp=1440.0/(2*math.pi)
yr=epochyr+2000 if epochyr<57 else epochyr+1900
mon,day,hr,mn,sec=days2mdhms(yr,epochdays)
jd=jday(yr,mon,day,hr,mn,sec)

rec=types.SimpleNamespace()
sgp4init(wgs72,'i',29141,jd-2433281.5, bstar, ndot, nddot, ecco, argpo, inclo, mo, no_kozai, nodeo, rec)
keys=['isimp','aycof','con41','cc1','cc4','cc5','d2','d3','d4','delmo','eta','argpdot','omgcof','sinmao','t2cof','t3cof','t4cof','t5cof','x1mth2','x7thm1','mdot','nodedot','xlcof','xmcof','nodecf','no_unkozai','gsto','a','alta','altp']
for k in keys:
    print("%-10s = %.17g" % (k, getattr(rec,k)))
for t in (60.0,):
    ok,r,v = sgp4(rec, t, wgs72)
    print("t=%s ok=%s r=%s" % (t, ok, [round(x,4) for x in r]))
