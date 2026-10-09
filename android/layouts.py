"""一维输入法（安卓）键盘布局：26 键（对标 iOS / Gboard）与九键（对标搜狗经典九键），写成 Trime 主题 YAML。"""


def q(s):
    return "'" + s.replace("'", "''") + "'"


def commit(ch):
    return "{commit: %s, label: %s}" % (q(ch), q(ch))


PRESET_KEYS = """
  yw_shift: {label: "ic@apple_keyboard_shift", send: Shift_L, shift_lock: ascii_long, functional: true}
  yw_backspace: {label: "ic@backspace_outline", repeatable: true, slide_delete: true, functional: true, send: BackSpace}
  yw_space: {label: "一维拼音", preview: " ", slide_cursor: true, functional: false, send: space}
  yw_space_t9: {label: "空格", preview: " ", slide_cursor: true, functional: false, send: space}
  yw_return: {label: enter_labels, functional: true, send: Return}
  yw_mode: {toggle: ascii_mode, send: SWITCH_CHARSET, states: ["中", "英"], functional: true}
  yw_num: {label: "123", send: Eisu_toggle, select: number, functional: true}
  yw_sym: {label: "符", send: Eisu_toggle, select: symbols, functional: true}
  yw_emoji: {label: "ic@emoticon_outline", send: FUNCTION, command: liquid_keyboard, option: "emoji", functional: true}
  yw_layout: {label: "九键", send: FUNCTION, command: select_schema, option: "t9|rime_ice", functional: true}
  yw_reinput: {label: "重输", send: Escape, functional: true}
  yw_split: {label: "分词", send: "1"}
"""

# letter, swipe-up/long-press symbol (Gboard-style)
ROW1 = list(zip("qwertyuiop", "1234567890"))
ROW2 = list(zip("asdfghjkl", "@#$_&-+()"))
ROW3 = list(zip("zxcvbnm", "*\"':;!?"))


def letter(ch, sym):
    return "      - {click: %s, long_click: %s, swipe_up: %s}" % (ch, commit(sym), commit(sym))


def qwerty():
    k = []
    k += [letter(c, s) for c, s in ROW1]
    k.append("      - {width: 5}")
    k += [letter(c, s) for c, s in ROW2]
    k.append("      - {width: 5}")
    k.append("      - {click: yw_shift, width: 14}")
    k.append("      - {width: 1}")
    k += [letter(c, s) for c, s in ROW3]
    k.append("      - {width: 1}")
    k.append("      - {click: yw_backspace, width: 14}")
    k.append("      - {click: yw_num, long_click: yw_sym, width: 12}")
    k.append("      - {click: yw_emoji, long_click: yw_layout, width: 10}")
    k.append("      - {click: yw_mode, width: 10}")
    k.append("      - {click: yw_space, width: 38}")
    k.append("      - {click: ',', long_click: '.', swipe_up: '.', label: '，', label_symbol: '。', width: 10}")
    k.append("      - {click: yw_return, key_back_color: yw_enter_back, key_text_color: yw_enter_text,"
             " hilited_key_back_color: yw_enter_back, hilited_key_text_color: yw_enter_text, width: 20}")
    return """  yw_qwerty:
    name: 一维 26 键
    author: 一维输入法
    ascii_mode: 0
    width: 10
    height: 50
    lock: true
    keys:
""" + "\n".join(k) + "\n"


T9 = [("1", "分词", "1"), ("2", "ABC", "2"), ("3", "DEF", "3"), ("4", "GHI", "4"), ("5", "JKL", "5"),
      ("6", "MNO", "6"), ("7", "PQRS", "7"), ("8", "TUV", "8"), ("9", "WXYZ", "9")]


def t9():
    side = ["，", "。", "？", "！"]
    side_send = [",", ".", "?", "!"]

    def digit(i):
        code, label, d = T9[i]
        return ("      - {click: %s, label: %s, label_symbol: %s, swipe_up: %s, long_click: %s, width: 22}"
                % (q(code), q(label), q(d), commit(d), commit(d)))

    def left(r):
        return "      - {click: %s, label: %s, width: 16, key_back_color: yw_func_back, key_text_color: yw_func_text}" % (
            q(side_send[r]), q(side[r]))
    k = []
    k += [left(0), digit(0), digit(1), digit(2), "      - {click: yw_backspace, width: 18}"]
    k += [left(1), digit(3), digit(4), digit(5), "      - {click: yw_reinput, width: 18}"]
    k += [left(2), digit(6), digit(7), digit(8), "      - {click: '@', label: '@', swipe_up: %s, width: 18,"
          " key_back_color: yw_func_back, key_text_color: yw_func_text}" % commit("0")]
    k += ["      - {click: yw_num, long_click: yw_sym, width: 16}",
          "      - {click: yw_mode, width: 11}",
          "      - {click: yw_space_t9, swipe_up: %s, width: 44}" % commit("0"),
          "      - {click: yw_layout, long_click: yw_emoji, width: 11}",
          "      - {click: yw_return, key_back_color: yw_enter_back, key_text_color: yw_enter_text,"
          " hilited_key_back_color: yw_enter_back, hilited_key_text_color: yw_enter_text, width: 18}"]
    return """  t9:
    name: 一维九键
    author: 一维输入法
    ascii_mode: 0
    ascii_keyboard: yw_qwerty
    width: 22
    height: 50
    lock: true
    keys:
""" + "\n".join(k) + "\n"


# schemas that should use the 26-key board (Trime picks the board named like the schema id)
QWERTY_ALIASES = ["rime_ice", "double_pinyin", "double_pinyin_abc", "double_pinyin_flypy", "double_pinyin_jiajia",
                  "double_pinyin_mspy", "double_pinyin_sogou", "double_pinyin_ziguang", "melt_eng", "qwerty", "qwerty_", "qwerty0"]


def keyboards():
    out = qwerty() + t9()
    for a in QWERTY_ALIASES:
        out += "  %s:\n    import_preset: yw_qwerty\n" % a
    return out


TOOL_BAR = """
tool_bar:
  button_spacing: 4
  back_style: "ic@arrow-left"
  primary_button:
    background: {type: circle, corner_radius: 10, normal: 0x00000000, highlight: hilited_candidate_button_color, vertical_inset: 4, horizontal_inset: 4}
    foreground: {style: "ic@dots_horizontal", normal: candidate_text_color, padding: 10}
    action: menu_keyboard
    long_press_action: Settings
  buttons:
    - {foreground: {style: "ic@keyboard_close", normal: candidate_text_color}, action: Hide}
    - {foreground: {style: "ic@clipboard_outline", normal: candidate_text_color}, action: clipboard_window}
    - {foreground: {style: "ic@emoticon_outline", normal: candidate_text_color}, action: yw_emoji}
    - {foreground: {style: "ic@dialpad", normal: candidate_text_color}, action: yw_layout}
"""

STYLE = {
    "key_height": "50", "keyboard_height": "236", "keyboard_height_land": "180",
    "horizontal_gap": "5", "vertical_gap": "9", "round_corner": "6", "key_border": "0",
    "key_text_size": "21", "key_long_text_size": "15", "symbol_text_size": "9", "label_text_size": "15",
    "candidate_text_size": "20", "comment_text_size": "11", "candidate_view_height": "40", "comment_height": "12",
    "candidate_padding": "8", "candidate_spacing": "0.0", "candidate_corner_radius": "8",
    "preview_text_size": "34", "preview_height": "58",
}
