#!/usr/bin/env python3
"""一维输入法 Windows 图标 v2「澄澈」（docs/design/DESIGN-v2.md §5）。

用法：
  python3 scripts/design/win_icons_v2.py            生成 assets/yiwei.ico、assets/yiwei-256.png、assets/zh.ico、assets/en.ico、helper/yiwei.ico
  python3 scripts/design/win_icons_v2.py --preview  另存一张浅底 / 深底、16–64px 的对比预览图到 /tmp/yiwei-icons-v2.png

造型：
  应用图标  圆角方块（圆角 22.5%），#3B82F6 → #2551D8 竖向渐变 + 上半部柔光 + 顶部 1px 高光边，中间白色「一」横加一个点（沿用 v1 品牌标）。
            16 / 20px 加粗横线、放大圆点、去掉柔光，避免糊。
  中        同一个渐变方块 + 白色「中」（实心）。
  英        白底方块 + 灰蓝描边 + 灰蓝「英」（空心）。实心 vs 空心，不靠颜色也能分清。
字体：思源黑体 / Noto Sans SC（SIL OFL 1.1），只在生成时用来画「中」「英」两个字，不随安装包分发。
     路径用环境变量 YIWEI_CJK_FONT 指定，默认 /workspace/work/fonts/NotoSansSC.ttf（可变字重版）。
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
FONT = os.environ.get("YIWEI_CJK_FONT", "/workspace/work/fonts/NotoSansSC.ttf")

TOP, BOTTOM = (0x3B, 0x82, 0xF6), (0x25, 0x51, 0xD8)   # 渐变（与 v2 默认强调色同一色相）
SLATE = (0x3E, 0x4C, 0x66)                             # 英：灰蓝描边与文字（白底上 9.0:1）
WHITE = (255, 255, 255)
APP_SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
STATE_SIZES = [16, 20, 24, 32, 40, 48, 64]
SS = 8  # 超采样倍数


def font(px, weight=700):
    f = ImageFont.truetype(FONT, px)
    try:
        f.set_variation_by_axes([weight])
    except Exception:
        pass
    return f


def squircle_mask(N, radius=0.225):
    m = Image.new("L", (N, N), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, N - 1, N - 1), int(N * radius), fill=255)
    return m


def gradient(N):
    g = Image.new("RGBA", (N, N))
    d = ImageDraw.Draw(g)
    for y in range(N):
        t = y / (N - 1)
        c = tuple(round(TOP[i] * (1 - t) + BOTTOM[i] * t) for i in range(3))
        d.line((0, y, N, y), fill=c + (255,))
    return g


def glass_tile(size, glow=True):
    """渐变方块 + 上半部柔光 + 顶部高光边（小尺寸不加柔光，避免发灰）。"""
    N = size * SS
    tile = gradient(N)
    if glow and size >= 24:
        hl = Image.new("RGBA", (N, N), (0, 0, 0, 0))
        hd = ImageDraw.Draw(hl)
        # 上半部一道很淡的白色柔光（液态玻璃的反光，但克制）
        for i in range(int(N * 0.48)):
            a = round(46 * (1 - i / (N * 0.48)) ** 1.6)
            hd.line((0, i, N, i), fill=(255, 255, 255, a))
        tile.alpha_composite(hl)
    if size >= 32:
        edge = Image.new("RGBA", (N, N), (0, 0, 0, 0))
        ImageDraw.Draw(edge).rounded_rectangle((0, 0, N - 1, N - 1), int(N * 0.225), outline=(255, 255, 255, 80), width=max(SS, N // 96))
        # 只留上半圈高光边，向下渐隐（不留硬切口）
        fade = Image.new("L", (N, N), 0)
        fd = ImageDraw.Draw(fade)
        for y in range(int(N * 0.45)):
            fd.line((0, y, N, y), fill=round(255 * (1 - y / (N * 0.45)) ** 1.2))
        from PIL import ImageChops
        edge.putalpha(ImageChops.multiply(edge.getchannel("A"), fade))
        tile.alpha_composite(edge)
    out = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    out.paste(tile, (0, 0), squircle_mask(N))
    return out


def app_icon(size):
    N = size * SS
    im = glass_tile(size)
    d = ImageDraw.Draw(im)
    small = size <= 20
    th = N * (0.13 if small else 0.085)          # 横线粗细
    x0, x1, cy = N * (0.22 if small else 0.24), N * (0.62 if small else 0.62), N * 0.5
    d.rounded_rectangle((x0, cy - th / 2, x1, cy + th / 2), th / 2, fill=WHITE + (255,))
    r = N * (0.075 if small else 0.056)           # 圆点
    cx = N * (0.76 if small else 0.735)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=WHITE + (225,))
    return im.resize((size, size), Image.LANCZOS)


def state_icon(size, chinese):
    N = size * SS
    if chinese:
        im = glass_tile(size, glow=False)
        fg = WHITE
    else:
        im = Image.new("RGBA", (N, N), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        bw = max(SS * 1.2, N * (0.1 if size <= 20 else 0.075))
        d.rounded_rectangle((0, 0, N - 1, N - 1), int(N * 0.225), fill=SLATE + (255,))
        d.rounded_rectangle((bw, bw, N - 1 - bw, N - 1 - bw), max(1, int(N * 0.225 - bw)), fill=WHITE + (255,))
        fg = SLATE
    d = ImageDraw.Draw(im)
    ch = "中" if chinese else "英"
    f = font(int(N * (0.66 if size <= 20 else 0.6)), 700 if size <= 24 else 600)
    bb = d.textbbox((0, 0), ch, font=f)
    w, h = bb[2] - bb[0], bb[3] - bb[1]
    d.text(((N - w) / 2 - bb[0], (N - h) / 2 - bb[1]), ch, font=f, fill=fg + (255,))
    return im.resize((size, size), Image.LANCZOS)


def save_ico(path, sizes, render):
    imgs = [render(s) for s in sizes]
    imgs[-1].save(path, format="ICO", sizes=[(s, s) for s in sizes], append_images=imgs[:-1])


def preview(path):
    sizes = [16, 20, 24, 32, 48, 64]
    W = sum(s + 12 for s in sizes) * 3 + 40
    out = Image.new("RGBA", (W, 2 * 90), (0, 0, 0, 0))
    for row, bg in enumerate([(243, 243, 243), (32, 32, 32)]):
        band = Image.new("RGBA", (W, 90), bg + (255,))
        x = 10
        for render in (app_icon, lambda s: state_icon(s, True), lambda s: state_icon(s, False)):
            for s in sizes:
                band.alpha_composite(render(s), (x, 45 - s // 2))
                x += s + 12
            x += 10
        out.alpha_composite(band, (0, row * 90))
    big = Image.new("RGBA", (3 * 276, 276), (243, 243, 243, 255))
    big.alpha_composite(app_icon(256), (10, 10))
    big.alpha_composite(state_icon(64, True).resize((256, 256), Image.NEAREST), (286, 10))
    big.alpha_composite(state_icon(64, False).resize((256, 256), Image.NEAREST), (562, 10))
    sheet = Image.new("RGBA", (max(W, big.width), 180 + 276), (255, 255, 255, 255))
    sheet.alpha_composite(out, (0, 0)); sheet.alpha_composite(big, (0, 180))
    sheet.save(path)


def main():
    assets = os.path.join(REPO, "assets")
    save_ico(os.path.join(assets, "yiwei.ico"), APP_SIZES, app_icon)
    save_ico(os.path.join(REPO, "helper", "yiwei.ico"), APP_SIZES, app_icon)
    app_icon(256).save(os.path.join(assets, "yiwei-256.png"))
    save_ico(os.path.join(assets, "zh.ico"), STATE_SIZES, lambda s: state_icon(s, True))
    save_ico(os.path.join(assets, "en.ico"), STATE_SIZES, lambda s: state_icon(s, False))
    if "--preview" in sys.argv:
        preview("/tmp/yiwei-icons-v2.png")
        print("预览：/tmp/yiwei-icons-v2.png")


if __name__ == "__main__":
    main()
