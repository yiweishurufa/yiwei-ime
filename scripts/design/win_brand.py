"""Windows 版视觉（2026-10 改版）：图标、托盘中/英、安装界面位图，全部来自安卓版极简标志（logo_min.tile）。

用法：python scripts/design/win_brand.py assets helper
  assets/yiwei.ico、yiwei-256.png      程序 / 安装器图标（16–256 多尺寸）
  assets/zh.ico、en.ico                小狼毫托盘状态：中 = 蓝底白字，英 = 白底蓝框蓝字
  assets/installer-*.bmp               安装界面「纯蓝整窗」（A 版）用的位图
  helper/yiwei.ico                     一维助手图标（与 assets 相同）
"""
import os
import shutil
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import logo_min as L  # noqa: E402
from fonts import F  # noqa: E402

BLUE = L.BLUE
WHITE = (255, 255, 255)
PALE = (220, 230, 255)
ICO_SIZES = [16, 20, 24, 32, 48, 64, 128, 256]


def bare(size, col=WHITE, dot_alpha=170):
    """The line + dot alone (no tile), for drawing on the blue installer window."""
    S = 4; N = size * S
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    L.mark(d, N, line=col + (255,), dot=col + (dot_alpha,))
    return im.resize((size, size), Image.LANCZOS)


def save_ico(path, render):
    imgs = [render(s) for s in ICO_SIZES]
    imgs[-1].save(path, format="ICO", sizes=[(s, s) for s in ICO_SIZES], append_images=imgs[:-1])


def state_tile(size, ch, chinese):
    """托盘：中 = 蓝底白字；英 = 白底蓝框蓝字（圆角方块）。"""
    S = 8; N = size * S
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    r = int(N * 0.22)
    if chinese:
        d.rounded_rectangle((0, 0, N - 1, N - 1), r, fill=BLUE + (255,))
        fg = WHITE
    else:
        bw = max(S, int(N * (0.09 if size <= 20 else 0.07)))
        d.rounded_rectangle((0, 0, N - 1, N - 1), r, fill=BLUE + (255,))
        d.rounded_rectangle((bw, bw, N - 1 - bw, N - 1 - bw), max(1, r - bw), fill=WHITE + (255,))
        fg = BLUE
    f = F(int(N * 0.62), 700)
    bb = d.textbbox((0, 0), ch, font=f)
    w = bb[2] - bb[0]; h = bb[3] - bb[1]
    d.text(((N - w) / 2 - bb[0], (N - h) / 2 - bb[1]), ch, font=f, fill=fg + (255,))
    return im.resize((size, size), Image.LANCZOS)


def on_blue(im_rgba, size):
    bg = Image.new("RGBA", size, BLUE + (255,))
    bg.alpha_composite(im_rgba)
    return bg.convert("RGB")


def centered_text(d, y, text, font, fill, W):
    w = d.textlength(text, font=font)
    d.text(((W - w) / 2, y), text, font=font, fill=fill)


def installer_bitmaps(out):
    # 欢迎页（nsDialogs 自定义整窗）：标志 + 标题 + 副标题，一张图；按钮、链接、底部小字是真控件
    S = 3; W, H = 320 * S, 170 * S
    im = Image.new("RGBA", (W, H), BLUE + (255,)); d = ImageDraw.Draw(im)
    mk = bare(150 * S)
    im.alpha_composite(mk, ((W - 150 * S) // 2, -12 * S))
    centered_text(d, 96 * S, "一维输入法", F(26 * S, 600), WHITE, W)
    centered_text(d, 138 * S, "中文常新，自在表达", F(12 * S), PALE, W)
    im.resize((320, 170), Image.LANCZOS).convert("RGB").save(os.path.join(out, "installer-hero.bmp"))

    # 白色胶囊按钮「立即安装」（蓝字），画在蓝底上；按下态略灰
    for name, fill in (("installer-button.bmp", WHITE), ("installer-button-down.bmp", (226, 233, 250))):
        S = 4; W, H = 180 * S, 40 * S
        im = Image.new("RGBA", (W, H), BLUE + (255,)); d = ImageDraw.Draw(im)
        d.rounded_rectangle((0, 0, W - 1, H - 1), H // 2, fill=fill + (255,))
        f = F(14 * S, 600); t = "立即安装"
        bb = d.textbbox((0, 0), t, font=f)
        d.text(((W - (bb[2] - bb[0])) / 2 - bb[0], (H - (bb[3] - bb[1])) / 2 - bb[1]), t, font=f, fill=BLUE + (255,))
        im.resize((180, 40), Image.LANCZOS).convert("RGB").save(os.path.join(out, name))

    # 完成页 / 卸载页左侧位图（MUI 164×314）：纯蓝 + 白标
    S = 3; W, H = 164 * S, 314 * S
    im = Image.new("RGBA", (W, H), BLUE + (255,)); d = ImageDraw.Draw(im)
    im.alpha_composite(bare(120 * S), ((W - 120 * S) // 2, int(H * 0.30)))
    centered_text(d, int(H * 0.56), "一维输入法", F(18 * S, 600), WHITE, W)
    centered_text(d, int(H * 0.56) + 30 * S, "中文常新，自在表达", F(9 * S), PALE, W)
    im.resize((164, 314), Image.LANCZOS).convert("RGB").save(os.path.join(out, "installer-welcome.bmp"))

    # 进度页页眉右侧位图（MUI 150×57）：纯蓝 + 白标，和蓝色页眉连成一片
    S = 4; W, H = 150 * S, 57 * S
    im = Image.new("RGBA", (W, H), BLUE + (255,))
    im.alpha_composite(bare(64 * S), (W - 76 * S, (H - 64 * S) // 2))
    im.resize((150, 57), Image.LANCZOS).convert("RGB").save(os.path.join(out, "installer-header.bmp"))


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "assets"
    helper = sys.argv[2] if len(sys.argv) > 2 else None
    os.makedirs(out, exist_ok=True)
    save_ico(os.path.join(out, "yiwei.ico"), lambda s: L.tile(s))
    L.tile(256).save(os.path.join(out, "yiwei-256.png"))
    tray = [16, 20, 24, 32, 48, 64]
    for name, ch, zh in (("zh", "中", True), ("en", "英", False)):
        ims = [state_tile(s, ch, zh) for s in tray]
        ims[-1].save(os.path.join(out, name + ".ico"), format="ICO", sizes=[(s, s) for s in tray], append_images=ims[:-1])
    installer_bitmaps(out)
    if helper:
        shutil.copyfile(os.path.join(out, "yiwei.ico"), os.path.join(helper, "yiwei.ico"))
    print("ok")


if __name__ == "__main__":
    main()
