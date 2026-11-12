"""Génère app.svg et app.ico à partir d'une géométrie unique (viewBox 256x256).

Usage : python Icon/generate-ico.py   (nécessite Pillow)
"""
from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent
SHIELD = [("M", 128, 36), ("L", 204, 64), ("L", 204, 120),
          ("C", 204, 164, 172, 196, 128, 220),
          ("C", 84, 196, 52, 164, 52, 120), ("L", 52, 64), ("Z",)]
BARS = [(84, 90, 88, 14, "#93C5FD"), (84, 118, 88, 14, "#1E3A8A"), (84, 146, 56, 14, "#93C5FD")]

SVG = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width="256" height="256">
  <title>TextAnonymizer</title>
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#3B82F6"/>
      <stop offset="1" stop-color="#1E40AF"/>
    </linearGradient>
  </defs>
  <rect width="256" height="256" rx="56" fill="url(#bg)"/>
  <path d="M128 36 L204 64 V120 C204 164 172 196 128 220 C84 196 52 164 52 120 V64 Z" fill="#FFFFFF"/>
""" + "".join(f'  <rect x="{x}" y="{y}" width="{w}" height="{h}" rx="7" fill="{c}"/>\n' for x, y, w, h, c in BARS) + "</svg>\n"


def flatten():
    pts, cur = [], None
    for seg in SHIELD:
        if seg[0] in "ML":
            cur = (seg[1], seg[2]); pts.append(cur)
        elif seg[0] == "C":
            x1, y1, x2, y2, x3, y3 = seg[1:]
            x0, y0 = cur
            for i in range(1, 41):
                t = i / 40; u = 1 - t
                pts.append((u**3*x0 + 3*u*u*t*x1 + 3*u*t*t*x2 + t**3*x3,
                            u**3*y0 + 3*u*u*t*y1 + 3*u*t*t*y2 + t**3*y3))
            cur = (x3, y3)
    return pts


def hexrgb(h):
    return tuple(int(h[i:i+2], 16) for i in (1, 3, 5))


def render(size=1024):
    k = size / 256
    top, bot = hexrgb("#3B82F6"), hexrgb("#1E40AF")
    grad = Image.new("RGBA", (size, size))
    gd = ImageDraw.Draw(grad)
    for y in range(size):
        t = y / (size - 1)
        gd.line([(0, y), (size, y)], fill=tuple(round(a + (b - a) * t) for a, b in zip(top, bot)) + (255,))
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, size - 1, size - 1], radius=56 * k, fill=255)
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    img.paste(grad, mask=mask)
    d = ImageDraw.Draw(img)
    d.polygon([(x * k, y * k) for x, y in flatten()], fill="#FFFFFF")
    for x, y, w, h, c in BARS:
        d.rounded_rectangle([x * k, y * k, (x + w) * k, (y + h) * k], radius=7 * k, fill=c)
    return img


(OUT / "app.svg").write_text(SVG, encoding="utf-8")
big = render()
sizes = [16, 24, 32, 48, 64, 128, 256]
big.save(OUT / "app.ico", format="ICO", sizes=[(s, s) for s in sizes])
print("OK")
