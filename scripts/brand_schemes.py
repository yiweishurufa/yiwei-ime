"""一维输入法五个品牌色的候选框配色（与 helper/Brand.cs 保持一致），颜色为 ARGB。

「墨线」视觉：纸色底、墨色字，品牌色只用在高亮候选的浅色底、高亮文字和高亮序号上，
其余序号、注释、拼音都退成灰，候选窗安静不抢眼。
"""
BRANDS = [("moblue", "墨蓝", 0x2B5BD7), ("qingbi", "青碧", 0x0E8C7A), ("zhusha", "朱砂", 0xC8452F),
          ("dianzi", "靛紫", 0x6450D6), ("shimo", "石墨", 0x2E3431)]
W = 0xFFFFFFFF
PAPER, INK = 0xFFFBFCFA, 0xFF1F2421
NIGHT, MOON = 0xFF1B1F1D, 0xFFE8ECE9


def mix(a, b, t):
    c = lambda s: round(((a >> s) & 255) * (1 - t) + ((b >> s) & 255) * t)
    return 0xFF000000 | (c(16) << 16) | (c(8) << 8) | c(0)


def scheme(bid, rgb, dark):
    acc = 0xFF000000 | rgb
    g = bid == "shimo"
    if not dark:
        return dict(back_color=PAPER, border_color=mix(0xFFDDE3DF, acc, .10), shadow_color=0x1F1A2420,
                    text_color=0xFF6A736E, hilited_text_color=INK, hilited_back_color=PAPER,
                    preedit_back_color=PAPER, candidate_text_color=INK,
                    comment_text_color=0xFF8E9792, label_color=0xFF9AA39E,
                    hilited_candidate_back_color=mix(acc, PAPER, .90 if g else .86),
                    hilited_candidate_text_color=INK if g else mix(acc, INK, .35),
                    hilited_comment_text_color=mix(acc, PAPER, .30) if not g else 0xFF6A736E,
                    hilited_label_color=INK if g else acc)
    lift = 0xFF39403C if g else mix(NIGHT, acc, .30)
    glow = 0xFFE8ECE9 if g else mix(acc, W, .55)
    return dict(back_color=NIGHT, border_color=mix(0xFF323936, acc, .12), shadow_color=0x4D000000,
                text_color=0xFF8F9893, hilited_text_color=MOON, hilited_back_color=NIGHT,
                preedit_back_color=NIGHT, candidate_text_color=MOON,
                comment_text_color=0xFF7F8883, label_color=0xFF6F7873,
                hilited_candidate_back_color=lift, hilited_candidate_text_color=glow,
                hilited_comment_text_color=mix(glow, lift, .35), hilited_label_color=mix(glow, lift, .2))


def all_schemes():
    for bid, name, rgb in BRANDS:
        for dark in (False, True):
            yield ("yiwei_%s%s" % (bid, "_dark" if dark else ""),
                   "一维 · %s%s" % (name, "（深色）" if dark else ""), scheme(bid, rgb, dark))
