"""GitHub 横幅（蓝色系）：个人主页、一维输入法、一维相册。

usage: python github_banners.py <out_dir>
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from PIL import Image, ImageDraw, ImageFilter  # noqa: E402
from fonts import F  # noqa: E402
import logo_min  # noqa: E402
from brand_schemes import scheme  # noqa: E402
from skins import card  # noqa: E402

S = 2
W, H = 1280 * S, 640 * S
BG = (247, 249, 253)
INK = (20, 26, 46)
MUTE = (96, 106, 128)
BLUE = logo_min.BLUE
BLUE_SOFT = (232, 239, 252)


def canvas():
    im = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(im)
    # one quiet horizontal rule: the "一" running across the page
    d.rounded_rectangle((0, H - 10 * S, W, H), 0, fill=BLUE + (255,))
    return im, d


def paste_tile(im, size, x, y, kind="ime"):
    t = logo_min.tile(size) if kind == "ime" else album_tile(size)
    sh = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle((x, y + 10 * S, x + size, y + size + 10 * S), int(size * 0.225), fill=(43, 91, 215, 70))
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(18 * S)))
    im.alpha_composite(t, (x, y))


def album_tile(size):
    N = size * 4
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, N - 1, N - 1), int(N * 0.225), fill=BLUE + (255,))
    # a photo frame drawn with the same single line, sun as the dot
    lw = int(N * 0.07)
    x0, y0, x1, y1 = N * 0.24, N * 0.28, N * 0.76, N * 0.72
    d.rounded_rectangle((x0, y0, x1, y1), int(N * 0.07), outline=(255, 255, 255, 255), width=lw)
    r = N * 0.055
    d.ellipse((N * 0.60 - r, N * 0.42 - r, N * 0.60 + r, N * 0.42 + r), fill=(255, 255, 255, 170))
    return im.resize((size, size), Image.LANCZOS)


def chip(d, x, y, text, fill=BLUE_SOFT, fg=BLUE):
    f = F(17 * S, 500)
    w = d.textlength(text, font=f) + 28 * S
    d.rounded_rectangle((x, y, x + w, y + 36 * S), 18 * S, fill=fill + (255,))
    d.text((x + 14 * S, y + 6 * S), text, font=f, fill=fg + (255,))
    return x + w + 10 * S


def save(im, path):
    im.resize((1280, 640), Image.LANCZOS).convert("RGB").save(path, optimize=True)


def profile(path):
    im, d = canvas()
    paste_tile(im, 132 * S, 110 * S, 150 * S)
    d.text((110 * S, 318 * S), "你好，我是 wei", font=F(60 * S, 650), fill=INK + (255,))
    d.text((112 * S, 404 * S), "独立开发者 · 中文优先，本地优先，无广告", font=F(26 * S), fill=MUTE + (255,))
    x = 112 * S
    for t in ("输入法", "相册客户端", "效率工具"):
        x = chip(d, x, 466 * S, t)
    # right: two product cards
    def product(y, kind, title, sub):
        d.rounded_rectangle((740 * S, y, 1170 * S, y + 150 * S), 22 * S, fill=(255, 255, 255, 255), outline=(225, 231, 243, 255), width=2 * S)
        paste_tile(im, 86 * S, 772 * S, y + 32 * S, kind)
        d.text((884 * S, y + 36 * S), title, font=F(30 * S, 600), fill=INK + (255,))
        d.text((886 * S, y + 86 * S), sub, font=F(19 * S), fill=MUTE + (255,))
    product(150 * S, "ime", "一维输入法", "Windows · Android 输入法")
    product(330 * S, "album", "一维相册", "飞牛 fnOS · NAS 与云盘")
    save(im, path)


def ime(path):
    im, d = canvas()
    paste_tile(im, 120 * S, 110 * S, 128 * S)
    d.text((110 * S, 290 * S), "一维输入法", font=F(68 * S, 650), fill=INK + (255,))
    d.text((112 * S, 386 * S), "中文常新，自在表达", font=F(28 * S), fill=MUTE + (255,))
    d.text((112 * S, 432 * S), "开源、本地、无广告 · 基于 RIME，词库雾凇拼音", font=F(20 * S), fill=MUTE + (255,))
    x = 112 * S
    for t in ("Windows 10 / 11", "Android", "GPL-3.0"):
        x = chip(d, x, 490 * S, t)
    c = card(scheme("moblue", 0x2B5BD7, False)); c = c.resize((int(c.width * 1.45), int(c.height * 1.45)), Image.LANCZOS)
    im.alpha_composite(c, (690 * S, 196 * S))
    c = card(scheme("moblue", 0x2B5BD7, True)); c = c.resize((int(c.width * 1.45), int(c.height * 1.45)), Image.LANCZOS)
    im.alpha_composite(c, (690 * S, 360 * S))
    save(im, path)


def album(path, shots=()):
    im, d = canvas()
    paste_tile(im, 120 * S, 110 * S, 128 * S, "album")
    d.text((110 * S, 290 * S), "一维相册", font=F(68 * S, 650), fill=INK + (255,))
    d.text((112 * S, 386 * S), "家里的 NAS、手头的云盘，一个 App 全收纳", font=F(28 * S), fill=MUTE + (255,))
    d.text((112 * S, 432 * S), "Android 原生飞牛 fnOS 相册客户端 · Jetpack Compose", font=F(20 * S), fill=MUTE + (255,))
    x = 112 * S
    for t in ("时间线", "多源聚合", "影视刮削", "本地优先"):
        x = chip(d, x, 490 * S, t)
    # right: an abstract timeline grid (no real photos)
    tones = [(214, 226, 250), (190, 208, 246), (167, 192, 242), (232, 239, 252), (143, 175, 238), (205, 219, 249)]
    gx, gy, cell, gap = 760 * S, 150 * S, 118 * S, 12 * S
    d.text((gx, gy - 46 * S), "2026 年 10 月", font=F(22 * S, 600), fill=INK + (255,))
    for r in range(3):
        for c in range(3):
            col = tones[(r * 3 + c * 2) % len(tones)]
            x0 = gx + c * (cell + gap); y0 = gy + r * (cell + gap)
            d.rounded_rectangle((x0, y0, x0 + cell, y0 + cell), 14 * S, fill=col + (255,))
    save(im, path)


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    profile(os.path.join(out, "profile-banner.png"))
    ime(os.path.join(out, "ime-banner.png"))
    album(os.path.join(out, "album-banner.png"))
