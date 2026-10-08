"""一维输入法五个品牌色的候选框配色（与 helper/Brand.cs 保持一致），颜色为 ARGB。"""
BRANDS = [("moblue", "墨蓝", 0x2F5BEA), ("qingbi", "青碧", 0x0F9D8A), ("zhusha", "朱砂", 0xE0483A),
          ("dianzi", "靛紫", 0x6B4EE6), ("shimo", "石墨", 0x2B2F36)]
W = 0xFFFFFFFF


def mix(a, b, t):
    c = lambda s: round(((a >> s) & 255) * (1 - t) + ((b >> s) & 255) * t)
    return 0xFF000000 | (c(16) << 16) | (c(8) << 8) | c(0)


def scheme(bid, rgb, dark):
    acc = 0xFF000000 | rgb
    d = {}
    if not dark:
        d.update(back_color=0xFFFFFFFF, border_color=mix(0xFFE4E6EA, acc, .18), shadow_color=0x26000000,
                 text_color=0xFF5B6270, hilited_text_color=0xFF2B2F36 if bid == "shimo" else acc,
                 hilited_back_color=mix(acc, W, .88), candidate_text_color=0xFF1F2328,
                 comment_text_color=0xFF8A9099, label_color=0xFF6B7280 if bid == "shimo" else acc,
                 hilited_candidate_back_color=acc, hilited_candidate_text_color=W,
                 hilited_comment_text_color=mix(W, acc, .22), hilited_label_color=mix(W, acc, .15),
                 preedit_back_color=mix(acc, W, .92))
    else:
        hi = 0xFF4A505A if bid == "shimo" else mix(acc, 0xFF000000, .08)
        soft = 0xFFB8BEC8 if bid == "shimo" else mix(acc, W, .38)
        d.update(back_color=0xFF202226, border_color=mix(0xFF3A3D44, acc, .15), shadow_color=0x4D000000,
                 text_color=0xFFA9AFB8, hilited_text_color=soft, hilited_back_color=mix(0xFF202226, acc, .22),
                 candidate_text_color=0xFFECEEF1, comment_text_color=0xFF8B919A, label_color=soft,
                 hilited_candidate_back_color=hi, hilited_candidate_text_color=W,
                 hilited_comment_text_color=mix(W, hi, .25), hilited_label_color=mix(W, hi, .18),
                 preedit_back_color=mix(0xFF202226, acc, .15))
    return d


def all_schemes():
    for bid, name, rgb in BRANDS:
        for dark in (False, True):
            yield ("yiwei_%s%s" % (bid, "_dark" if dark else ""),
                   "一维 · %s%s" % (name, "（深色）" if dark else ""), scheme(bid, rgb, dark))
