import sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter
src_dir, out = sys.argv[1], sys.argv[2]
W = 512
off = Image.open(f"{src_dir}/mirror_off.png").convert("RGB")
on  = Image.open(f"{src_dir}/model_mirror_on.png").convert("RGB")

# 오리 중심으로 정사각 크롭 (1024 원본에서 오리는 대략 x200~830, y30~800)
def duck_crop(im):
    return im.crop((40, 0, 1024, 984))          # 984x984 (총까지 여유 있게)
panel_h = 380
pw = W // 2
def panel(im, dim):
    p = duck_crop(im).resize((pw - 8, pw - 8), Image.LANCZOS)  # 248x248
    # 세로 380 패널에 맞게 가운데 배치
    bg = Image.new("RGB", (pw, panel_h), (24, 28, 36))
    bg.paste(p, ((pw - p.width)//2, (panel_h - p.height)//2 + 22))
    if dim:
        bg = Image.blend(bg, Image.new("RGB", bg.size, (24, 28, 36)), 0.35)
    return bg

canvas = Image.new("RGB", (W, W), (24, 28, 36))
canvas.paste(panel(off, True), (0, 0))
canvas.paste(panel(on, False), (pw, 0))

d = ImageDraw.Draw(canvas)
try:
    f_big = ImageFont.truetype("C:/Windows/Fonts/malgunbd.ttf", 34)
    f_mid = ImageFont.truetype("C:/Windows/Fonts/malgunbd.ttf", 22)
    f_sml = ImageFont.truetype("C:/Windows/Fonts/malgun.ttf", 17)
except Exception:
    f_big = f_mid = f_sml = ImageFont.load_default()

# 패널 라벨
def label(x, y, text, font, fill, anchor="mm", shadow=True):
    if shadow:
        d.text((x+2, y+2), text, font=font, fill=(0, 0, 0), anchor=anchor)
    d.text((x, y), text, font=font, fill=fill, anchor=anchor)
label(pw//2, 26, "BEFORE", f_mid, (170, 175, 185))
label(pw + pw//2, 26, "AFTER", f_mid, (120, 220, 140))

# 가운데 화살표 (원 + 삼각)
cx, cy = pw, panel_h//2 + 10
d.ellipse((cx-30, cy-30, cx+30, cy+30), fill=(255, 196, 40))
d.polygon([(cx-10, cy-16), (cx+16, cy), (cx-10, cy+16)], fill=(24, 28, 36))

# 아래 제목 띠
d.rectangle((0, panel_h, W, W), fill=(255, 196, 40))
d.rectangle((0, panel_h, W, panel_h+4), fill=(255, 230, 120))
label(W//2, panel_h + 40, "ShoulderTransition", f_big, (24, 28, 36), shadow=False)
label(W//2, panel_h + 82, "Left-handed Duck  ·  왼손 사격", f_mid, (60, 50, 20), shadow=False)
label(W//2, panel_h + 112, "Alt to toggle  ·  bullets really leave the left muzzle", f_sml, (90, 75, 30), shadow=False)

canvas.save(out, "PNG", optimize=True)
canvas.resize((256, 256), Image.LANCZOS).save(out.replace(".png", "_256.png"), "PNG", optimize=True)
print("saved", out, canvas.size)
