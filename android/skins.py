"""一维输入法（安卓）键盘皮肤：每套皮肤一对浅色/深色配色，跟随系统深色模式自动切换。

颜色是 Trime 配色方案的字段；另外定义了几个一维自己的颜色名，布局里引用：
  yw_enter_back / yw_enter_text   回车键（品牌强调色）
  yw_func_back / yw_func_text     功能键（与 off_key_* 相同，给不走 functional 的键用）
仿 Mac / 搜狗 / 微信 / Gboard 只是「风格」，颜色按公开截图与开源复刻实现取近似值，不使用它们的商标和图标。
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "scripts"))
from brand_schemes import BRANDS, scheme, mix  # noqa: E402


def rgb(v):
    return 0xFF000000 | v if v <= 0xFFFFFF else v


def palette(kb, key, func, text, sub, pressed, func_pressed, accent, accent_text, cand_hl_back, cand_hl_text,
            border=None, func_text=None, cand_text=None, comment=None):
    """A complete Trime colour scheme from a handful of design colours."""
    kb, key, func, text, sub = map(rgb, (kb, key, func, text, sub))
    pressed, func_pressed, accent, accent_text = map(rgb, (pressed, func_pressed, accent, accent_text))
    cand_hl_back, cand_hl_text = rgb(cand_hl_back), rgb(cand_hl_text)
    border = rgb(border) if border is not None else kb
    func_text = rgb(func_text) if func_text is not None else text
    cand_text = rgb(cand_text) if cand_text is not None else text
    comment = rgb(comment) if comment is not None else sub
    return dict(
        # candidate bar / floating window
        back_color=kb, border_color=border, candidate_separator_color=kb,
        text_color=sub, text_back_color=kb, hilited_text_color=text, hilited_back_color=kb,
        candidate_text_color=cand_text, comment_text_color=comment, label_color=sub,
        hilited_candidate_back_color=cand_hl_back, hilited_candidate_text_color=cand_hl_text,
        hilited_comment_text_color=cand_hl_text, hilited_label_color=cand_hl_text,
        hilited_candidate_button_color=func_pressed,
        candidate_background=kb, root_background=kb, keyboard_back_color=kb, liquid_keyboard_background=kb,
        # letter keys
        key_back_color=key, key_text_color=text, key_symbol_color=sub, key_border_color=key,
        hilited_key_back_color=pressed, hilited_key_text_color=text, hilited_key_symbol_color=sub,
        # function keys (functional: true)
        off_key_back_color=func, off_key_text_color=func_text, off_key_symbol_color=sub,
        hilited_off_key_back_color=func_pressed, hilited_off_key_text_color=func_text,
        # toggles that are "on" (Shift lock, 英): same face, accent text
        on_key_back_color=func, on_key_text_color=accent, hilited_on_key_back_color=func_pressed, hilited_on_key_text_color=accent,
        preview_back_color=key, preview_text_color=text, shadow_color=0x00000000, long_text_back_color=key,
        yw_enter_back=accent, yw_enter_text=accent_text, yw_func_back=func, yw_func_text=func_text,
    )


def moxian(bid, rgbv, dark):
    """「墨线」: the Windows candidate window colours on a paper (or ink) keyboard."""
    s = scheme(bid, rgbv, dark)
    acc = 0xFF000000 | rgbv
    if not dark:
        p = palette(kb=0xE9ECE8, key=0xFBFCFA, func=0xD5DAD6, text=0x1F2421, sub=0x6A736E, pressed=0xD5DAD6,
                    func_pressed=0xC2C9C4, accent=acc, accent_text=0xFFFFFF,
                    cand_hl_back=s["hilited_candidate_back_color"], cand_hl_text=s["hilited_candidate_text_color"])
    else:
        p = palette(kb=0x141716, key=0x2A302D, func=0x1E2321, text=0xE8ECE9, sub=0x8F9893, pressed=0x3A423E,
                    func_pressed=0x2E3532, accent=mix(0xFF1B1F1D, acc, .55) if bid != "shimo" else 0xFF4A524E,
                    accent_text=0xFFFFFF,
                    cand_hl_back=s["hilited_candidate_back_color"], cand_hl_text=s["hilited_candidate_text_color"])
    return p


# (id, 名称, light palette kwargs, dark palette kwargs)
STYLES = [
    ("mac", "Mac 风",
     dict(kb=0xD1D3D9, key=0xFFFFFF, func=0xABB0BC, text=0x1C1C1E, sub=0x8A8D93, pressed=0xBCC0C9,
          func_pressed=0xFFFFFF, accent=0x007AFF, accent_text=0xFFFFFF, cand_hl_back=0xFFFFFF, cand_hl_text=0x007AFF),
     dict(kb=0x2B2B2D, key=0x565658, func=0x3A3A3C, text=0xFFFFFF, sub=0xA1A1A6, pressed=0x6E6E72,
          func_pressed=0x565658, accent=0x0A84FF, accent_text=0xFFFFFF, cand_hl_back=0x3A3A3C, cand_hl_text=0x64B0FF)),
    ("sogou", "搜狗风",
     dict(kb=0xEDEFF2, key=0xFFFFFF, func=0xDCE0E6, text=0x222222, sub=0x8C9199, pressed=0xD9DDE3,
          func_pressed=0xC8CDD5, accent=0xFF6A1A, accent_text=0xFFFFFF, cand_hl_back=0xFFF0E6, cand_hl_text=0xF05A0A),
     dict(kb=0x1C1C1E, key=0x343438, func=0x26262A, text=0xEDEDED, sub=0x8E8E93, pressed=0x48484E,
          func_pressed=0x343438, accent=0xFF7A30, accent_text=0xFFFFFF, cand_hl_back=0x3A2A20, cand_hl_text=0xFF9A5C)),
    ("wechat", "微信风",
     dict(kb=0xEDEDED, key=0xFFFFFF, func=0xF7F7F7, text=0x191919, sub=0x8C8C8C, pressed=0xDADADA,
          func_pressed=0xE2E2E2, accent=0x07C160, accent_text=0xFFFFFF, cand_hl_back=0xEDEDED, cand_hl_text=0x06AE56),
     dict(kb=0x191919, key=0x2C2C2C, func=0x232323, text=0xD5D5D5, sub=0x7A7A7A, pressed=0x3E3E3E,
          func_pressed=0x2C2C2C, accent=0x07C160, accent_text=0xFFFFFF, cand_hl_back=0x1E2E24, cand_hl_text=0x2AD07A)),
    ("gboard", "Gboard 风",
     dict(kb=0xE8EAED, key=0xFFFFFF, func=0xD3D6DB, text=0x202124, sub=0x5F6368, pressed=0xDADCE0,
          func_pressed=0xC4C7CC, accent=0x1A73E8, accent_text=0xFFFFFF, cand_hl_back=0xE8F0FE, cand_hl_text=0x1967D2),
     dict(kb=0x202124, key=0x3C4043, func=0x2D2E31, text=0xE8EAED, sub=0x9AA0A6, pressed=0x5F6368,
          func_pressed=0x3C4043, accent=0x8AB4F8, accent_text=0x202124, cand_hl_back=0x2D3B52, cand_hl_text=0x8AB4F8)),
    ("sakura", "樱花",
     dict(kb=0xFBEFF2, key=0xFFFFFF, func=0xF4D9E0, text=0x4A2932, sub=0xA5808A, pressed=0xF4D9E0,
          func_pressed=0xEBC3CE, accent=0xE0607E, accent_text=0xFFFFFF, cand_hl_back=0xFCE4EA, cand_hl_text=0xC9446A),
     dict(kb=0x231A1D, key=0x3A2C31, func=0x2C2226, text=0xF3E3E8, sub=0xA88E96, pressed=0x4E3C43,
          func_pressed=0x3A2C31, accent=0xE0708C, accent_text=0xFFFFFF, cand_hl_back=0x48303A, cand_hl_text=0xF29AB0)),
    ("oled", "纯黑",
     dict(kb=0x000000, key=0x1C1C1E, func=0x111113, text=0xEEEEEE, sub=0x8E8E93, pressed=0x3A3A3C,
          func_pressed=0x2C2C2E, accent=0x0E8C7A, accent_text=0xFFFFFF, cand_hl_back=0x10302B, cand_hl_text=0x5FD3C0),
     None),
]


def all_skins():
    """Yields (scheme_id, display name, colours dict, dark twin id or None)."""
    for bid, name, rgbv in BRANDS:
        yield "yiwei_%s" % bid, "墨线 · %s" % name, moxian(bid, rgbv, False), "yiwei_%s_dark" % bid
        yield "yiwei_%s_dark" % bid, "墨线 · %s（深色）" % name, moxian(bid, rgbv, True), None
    for sid, name, light, dark in STYLES:
        if dark is None:  # dark-only skin
            yield "yiwei_%s" % sid, name, palette(**light), None
            continue
        yield "yiwei_%s" % sid, name, palette(**light), "yiwei_%s_dark" % sid
        yield "yiwei_%s_dark" % sid, name + "（深色）", palette(**dark), None
