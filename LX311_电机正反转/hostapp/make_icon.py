from PIL import Image, ImageDraw, ImageFont
S = 256
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
# 圆角底
r = 52
d.rounded_rectangle([8, 8, S-8, S-8], radius=r, fill=(18, 30, 48), outline=(58, 200, 130), width=6)
# 电机圆
d.ellipse([70, 92, 186, 208], outline=(62, 224, 138), width=9)
try:
    f = ImageFont.truetype(r"C:\Windows\Fonts\simhei.ttf", 78)
    fs = ImageFont.truetype(r"C:\Windows\Fonts\simhei.ttf", 26)
except Exception:
    f = ImageFont.load_default(); fs = f
d.text((112, 118), "M", font=f, fill=(230, 245, 235), anchor="mm")
d.text((128, 246), "3~", font=fs, fill=(120, 200, 165), anchor="mm")
img.save(r"app.ico", sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
print("icon ok")
