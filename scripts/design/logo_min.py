"""一维输入法极简标志：墨蓝底上一道白色横线 + 一颗圆点（字已写下，光标在等下一个）。"""
from PIL import Image, ImageDraw

BLUE = (43, 91, 215)
WHITE = (255, 255, 255)
DOT = (255, 255, 255, 170)


def mark(draw, N, cx=0.5, scale=1.0, line=WHITE + (255,), dot=DOT):
    """Draws the line+dot centred at (cx*N, N/2); total width ≈ 0.58*N*scale."""
    w = N * 0.44 * scale; h = N * 0.075 * scale; gap = N * 0.08 * scale; r = h * 0.62
    total = w + gap + r
    x0 = N * cx - total / 2
    draw.rounded_rectangle((x0, (N - h) / 2, x0 + w, (N + h) / 2), h / 2, fill=line)
    c = x0 + w + gap
    draw.ellipse((c - r, N / 2 - r, c + r, N / 2 + r), fill=dot)


def tile(size, radius=0.225, bg=BLUE):
    S = 4; N = size * S
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    if radius >= 0.5:
        d.ellipse((0, 0, N - 1, N - 1), fill=bg + (255,))
    else:
        d.rounded_rectangle((0, 0, N - 1, N - 1), int(N * radius), fill=bg + (255,))
    mark(d, N)
    return im.resize((size, size), Image.LANCZOS)


def adaptive_foreground(size=432):
    """Adaptive-icon foreground (108dp canvas; the mask keeps the middle ~66dp)."""
    S = 4; N = size * S
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    mark(d, N, scale=0.62)
    return im.resize((size, size), Image.LANCZOS)


if __name__ == "__main__":
    import sys
    tile(512).save(sys.argv[1])
