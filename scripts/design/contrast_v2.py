#!/usr/bin/env python3
"""一维输入法 Windows v2「澄澈」颜色 token 的 WCAG 对比度自检（docs/design/DESIGN-v2.md §2）。

用法：python3 scripts/design/contrast_v2.py   —— 任一必须达标的配对不达标时退出码为 1。
数值与 helper/Wpf/DesignV2.cs（Tok / PanelPalette）、helper/Brand.cs V2Argb、scripts/brand_schemes.py v2() 保持一致。
"""
import sys


def lum(rgb):
    def ch(c):
        c /= 255
        return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255
    return 0.2126 * ch(r) + 0.7152 * ch(g) + 0.0722 * ch(b)


def ratio(a, b):
    la, lb = sorted((lum(a), lum(b)), reverse=True)
    return (la + 0.05) / (lb + 0.05)


def over(fg_argb, bg_rgb):
    """半透明前景叠在不透明底上。"""
    a = ((fg_argb >> 24) & 255) / 255
    out = 0
    for s in (16, 8, 0):
        out |= round(((fg_argb >> s) & 255) * a + ((bg_rgb >> s) & 255) * (1 - a)) << s
    return out


def mix(a, b, t):
    return sum(round(((a >> s) & 255) * (1 - t) + ((b >> s) & 255) * t) << s for s in (16, 8, 0))


# 浮动面板（功能面板、常用语、符号…）与候选窗的中性色
LIGHT = dict(back=0xF6F8FB, card=0xFFFFFF, hover=0xEEF2F8, text=0x161B26, dim=0x5A6478, faint=0x7A8398, line=0xE2E7EF)
DARK = dict(back=0x1D2129, card=0x272C36, hover=0x313744, text=0xEEF1F6, dim=0xA4ADBE, faint=0x7C8598, line=0x3A4150)
# 强调色：跟随系统；下面是常见系统强调色与回退色（Windows 默认蓝 #0067C0）
ACCENTS = {"默认蓝": 0x0067C0, "Win 蓝": 0x0078D4, "紫": 0x8764B8, "绿": 0x107C10, "橙": 0xCA5010, "红": 0xC42B1C, "黄": 0xFFB900, "青": 0x038387}
INK, WHITE = 0x0B0F17, 0xFFFFFF


def on_accent(acc):
    """强调色底上的文字：白或近黑，取对比度高的（与 Brand.OnAccent 一致）。"""
    return WHITE if ratio(WHITE, acc) >= ratio(INK, acc) else INK


def accent_text(acc, dark):
    """强调色作文字（页签、链接、选中序号）：浅色档向墨色压 25%，深色档向白提亮 45%（与 Tok.AccentText 一致）。"""
    back = DARK["back"] if dark else LIGHT["back"]
    t = 0.45 if dark else 0.25
    while True:
        c = mix(acc, WHITE, t) if dark else mix(acc, INK, t)
        if ratio(c, back) >= 4.5 or t >= 0.9:
            return c
        t += 0.05  # 黄、浅绿这类亮色系统强调色要压得更深才能当文字


def dark_accent(acc):
    """深色档的强调色：向白色提亮 30%（与 Brand.V2Argb 一致）。"""
    return mix(acc, WHITE, 0.30)


fails = 0


def check(name, a, b, need):
    global fails
    r = ratio(a, b)
    ok = r >= need
    fails += 0 if ok else 1
    print("%-34s %5.2f  %s (≥%.1f)" % (name, r, "✓" if ok else "✗", need))


for mode, P in (("浅色", LIGHT), ("深色", DARK)):
    print("== %s ==" % mode)
    check("正文 / 面板底", P["text"], P["back"], 4.5)
    check("正文 / 卡片", P["text"], P["card"], 4.5)
    check("正文 / 悬停卡片", P["text"], P["hover"], 4.5)
    check("次要文字 / 面板底", P["dim"], P["back"], 4.5)
    check("次要文字 / 卡片", P["dim"], P["card"], 4.5)
    check("弱文字（序号、提示）/ 面板底", P["faint"], P["back"], 3.0)
    for an, acc in ACCENTS.items():
        a = acc if mode == "浅色" else dark_accent(acc)
        check("高亮候选字 / 强调色 " + an, on_accent(a), a, 4.5)
        check("强调色文字 / 面板底 " + an, accent_text(acc, mode == "深色"), P["back"], 4.5)
print("\n全部达标" if not fails else "\n%d 项不达标" % fails)
sys.exit(1 if fails else 0)
