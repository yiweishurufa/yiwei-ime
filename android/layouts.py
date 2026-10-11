"""一维输入法（安卓）键盘布局：26 键（对标 iOS / Gboard）与九键（对标搜狗经典九键），写成 Trime 主题 YAML。"""


def q(s):
    return "'" + s.replace("'", "''") + "'"


def commit(ch):
    return "{commit: %s, label: %s}" % (q(ch), q(ch))


PRESET_KEYS = """
  yw_shift: {label: "ic@apple_keyboard_shift", send: Shift_L, shift_lock: ascii_long, functional: true}
  yw_backspace: {label: "ic@backspace_outline", repeatable: true, slide_delete: true, functional: true, send: BackSpace}
  yw_space: {label: "ic@microphone_outline", preview: " ", slide_cursor: true, functional: false, send: space}
  yw_space_t9: {label: "ic@microphone_outline", preview: " ", slide_cursor: true, functional: false, send: space}
  yw_space_plain: {label: "空格", preview: " ", slide_cursor: true, functional: false, send: space}
  yw_return: {label: enter_labels, functional: true, send: Return}
  yw_mode: {toggle: ascii_mode, send: SWITCH_CHARSET, states: ["中", "英"], functional: true}
  yw_num: {label: "123", send: Eisu_toggle, select: yw_number, functional: true}
  yw_sym: {label: "符", send: FUNCTION, command: liquid_keyboard, option: "中文", functional: true}
  yw_emoji: {label: "ic@emoticon_outline", send: FUNCTION, command: liquid_keyboard, option: "emoji", functional: true}
  yw_layout: {label: "九键", send: FUNCTION, command: select_schema, option: "t9|rime_ice", functional: true}
  yw_layout26: {label: "26键", send: FUNCTION, command: select_schema, option: "t9|rime_ice", functional: true}
  yw_reinput: {label: "重输", send: FUNCTION, command: clear_composition, functional: true}
  yw_split: {label: "分词", send: "1"}
  yw_menu: {label: "菜单", send: FUNCTION, command: menu_keyboard, functional: true}
  yw_hide: {label: "收起", send: BACK, functional: true}
  yw_clipboard: {label: "剪贴板", send: FUNCTION, command: clipboard_window, option: "0", functional: true}
  yw_settings: {label: "设置", send: SETTINGS, functional: true}
  yw_resize: {label: "调整高度", send: FUNCTION, command: keyboard_resize, functional: true}
  yw_keepalive: {label: "保活", send: FUNCTION, command: yiwei_keepalive, functional: true}
  yw_kbd_chooser: {label: "键盘选择", send: FUNCTION, command: yiwei_kbdchooser, functional: true}
  yw_s_t9: {label: "九键", send: FUNCTION, command: select_schema, option: "t9", functional: true}
  yw_s_qp: {label: "26键", send: FUNCTION, command: select_schema, option: "rime_ice", functional: true}
  yw_s_flypy: {label: "小鹤", send: FUNCTION, command: select_schema, option: "double_pinyin_flypy", functional: true}
  yw_toolbox: {label: "工具箱", send: FUNCTION, command: yiwei_toolbox, functional: true}
  yw_phrases: {label: "常用语", send: FUNCTION, command: liquid_keyboard, option: "常用语", functional: true}
  yw_edit: {label: "编辑", send: Eisu_toggle, select: yw_edit_board, functional: true}
  yw_voice: {label: "语音", send: VOICE_ASSIST, functional: true}
  yw_trad: {label: "繁体", toggle: traditionalization, send: SWITCH_CHARSET, states: ["简", "繁"], functional: true}
  yw_fullshape: {label: "全角", toggle: full_shape, send: SWITCH_CHARSET, states: ["半角", "全角"], functional: true}
  yw_color: {label: "配色", send: SETTINGS, option: "color", functional: true}
  yw_sound: {label: "按键音", send: SETTINGS, option: "sound", functional: true}
  yw_schema: {label: "方案", send: MENU, functional: true}
  yw_e_all: {label: "全选", send: Control+a, functional: true}
  yw_e_copy: {label: "复制", send: Control+c, functional: true}
  yw_e_cut: {label: "剪切", send: Control+x, functional: true}
  yw_e_paste: {label: "粘贴", send: Control+v, functional: true}
  yw_e_undo: {label: "撤销", send: Control+z, functional: true}
  yw_e_redo: {label: "重做", send: Control+Shift+z, functional: true}
  yw_e_home: {label: "行首", send: Home, functional: true}
  yw_e_end: {label: "行尾", send: End, functional: true}
  yw_e_left: {label: "ic@arrow_left", send: Left, repeatable: true, functional: true}
  yw_e_right: {label: "ic@arrow_right", send: Right, repeatable: true, functional: true}
  yw_e_up: {label: "ic@arrow_up", send: Up, repeatable: true, functional: true}
  yw_e_down: {label: "ic@arrow_down", send: Down, repeatable: true, functional: true}
  yw_e_del: {label: "删除", send: Delete, repeatable: true, functional: true}
  yw_e_clear: {label: "清空", text: "{Control+a}{BackSpace}", functional: true}
  yw_e_copyall: {label: "全部复制", text: "{Control+a}{Control+c}", functional: true}
  yw_back: {label: "返回", send: Eisu_toggle, select: .default, functional: true}
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
    k.append("      - {click: yw_sym, long_click: yw_emoji, width: 10}")
    k.append("      - {click: yw_num, width: 10}")
    k.append("      - {click: \"'\", label: \"'\", width: 8}")
    k.append("      - {click: yw_space, long_click: yw_voice, width: 34}")
    k.append("      - {click: '.', label: '。', swipe_up: ',', width: 8}")
    k.append("      - {click: yw_mode, long_click: yw_layout, width: 10}")
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
        # 左列由 YiweiSideColumn 覆盖层负责（标点 / 拼音选择），这里只占位
        return "      - {width: 16}"
    k = []
    k += [left(0), digit(0), digit(1), digit(2), "      - {click: yw_backspace, width: 18}"]
    k += [left(1), digit(3), digit(4), digit(5), "      - {click: yw_reinput, width: 18}"]
    k += [left(2), digit(6), digit(7), digit(8), "      - {click: %s, label: '@', swipe_up: %s, width: 18,"
          " key_back_color: yw_func_back, key_text_color: yw_func_text}" % (commit("@"), commit("0"))]
    k += ["      - {click: yw_sym, long_click: yw_emoji, width: 16}",
          "      - {click: yw_num, width: 16}",
          "      - {click: yw_space_t9, long_click: yw_voice, width: 36}",
          "      - {click: yw_mode, long_click: yw_layout, width: 16}",
          "      - {click: yw_return, key_back_color: yw_enter_back, key_text_color: yw_enter_text,"
          " hilited_key_back_color: yw_enter_back, hilited_key_text_color: yw_enter_text, width: 16}"]
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


def number_board():
    """数字键盘（对标搜狗）：左列 / - :，3×3 数字，右列 ⌫ . @，底行 符 | 返回 | 0 | 空格 | ↵。"""
    def side(ch, up=None):
        extra = (", swipe_up: %s" % commit(up)) if up else ""
        return "      - {click: %s, label: %s%s, width: 16, key_back_color: yw_func_back, key_text_color: yw_func_text}" % (
            q(ch), q(ch), extra)

    def d(n):
        return "      - {click: KP_%d, label: %s, width: 22}" % (n, q(str(n)))
    k = [side("/", "+"), d(1), d(2), d(3), "      - {click: yw_backspace, width: 18}",
         side("-", "_"), d(4), d(5), d(6), "      - {click: '.', label: '.', width: 18, key_back_color: yw_func_back, key_text_color: yw_func_text}",
         side(":", ";"), d(7), d(8), d(9), "      - {click: '@', label: '@', width: 18, key_back_color: yw_func_back, key_text_color: yw_func_text}",
         "      - {click: yw_sym, width: 16}", "      - {click: yw_back, width: 22}", "      - {click: KP_0, label: '0', width: 22}",
         "      - {click: yw_space_plain, width: 22}",
         "      - {click: yw_return, key_back_color: yw_enter_back, key_text_color: yw_enter_text,"
         " hilited_key_back_color: yw_enter_back, hilited_key_text_color: yw_enter_text, width: 18}"]
    return """  yw_number:
    name: 一维数字
    author: 一维输入法
    width: 22
    height: 50
    lock: true
    keys:
""" + "\n".join(k) + "\n"


def edit_board():
    k = ["      - {click: yw_e_all, width: 20}", "      - {click: yw_e_copy, width: 20}",
         "      - {click: yw_e_cut, width: 20}", "      - {click: yw_e_paste, width: 20}",
         "      - {click: yw_backspace, width: 20}",
         "      - {click: yw_e_undo, width: 20}", "      - {click: yw_e_home, width: 20}",
         "      - {click: yw_e_up, width: 20}", "      - {click: yw_e_end, width: 20}",
         "      - {click: yw_e_redo, width: 20}",
         "      - {click: yw_e_del, width: 20}", "      - {click: yw_e_left, width: 20}",
         "      - {click: yw_e_down, width: 20}", "      - {click: yw_e_right, width: 20}",
         "      - {click: yw_e_copyall, width: 20}",
         "      - {click: yw_back, width: 20}", "      - {click: yw_e_clear, width: 20}",
         "      - {click: yw_space, width: 40}",
         "      - {click: yw_return, key_back_color: yw_enter_back, key_text_color: yw_enter_text,"
         " hilited_key_back_color: yw_enter_back, hilited_key_text_color: yw_enter_text, width: 20}"]
    return """  yw_edit_board:
    name: 一维编辑面板
    author: 一维输入法
    width: 20
    height: 50
    lock: true
    keys:
""" + "\n".join(k) + "\n"


# schemas that should use the 26-key board (Trime picks the board named like the schema id)
QWERTY_ALIASES = ["rime_ice", "double_pinyin", "double_pinyin_abc", "double_pinyin_flypy", "double_pinyin_jiajia",
                  "double_pinyin_mspy", "double_pinyin_sogou", "double_pinyin_ziguang", "melt_eng", "qwerty", "qwerty_", "qwerty0"]


# 同文在数字/电话输入框里会切到名为 number 的键盘：换成一维数字键盘
OTHER_ALIASES = ["number"]
ALIAS_TARGET = {"number": "yw_number"}

OUR_KEYBOARDS = ["yw_qwerty", "t9", "yw_edit_board", "yw_number"]


def keyboards():
    out = qwerty() + t9() + edit_board() + number_board()
    for a in QWERTY_ALIASES:
        out += "  %s:\n    import_preset: yw_qwerty\n" % a
    for a in OTHER_ALIASES:
        out += "  %s:\n    import_preset: %s\n" % (a, ALIAS_TARGET[a])
    return out


PHRASES = ["好的，收到。", "谢谢！", "不客气", "稍等，我马上看一下。", "在路上，马上到。", "抱歉，刚才没看到消息。",
           "方便的话请回电话。", "辛苦了！", "明白了，我来处理。", "晚点联系你。", "我现在不方便，稍后回复。", "请问您方便吗？"]

# liquid_keyboard 里的「常用语」标签页（工具栏/工具箱的常用语按钮打开它）
LIQUID_PHRASES = "  phrases:\n    name: 常用语\n    type: SINGLE\n    keys:\n" + "".join(
    "      - {click: %s}\n" % q(x) for x in PHRASES)


# 工具箱里的功能：(显示名, MDI 图标名, preset_keys 里的动作)。新增功能时在这里加，CI 会检查图标名和动作都存在。
TOOLBOX = [
    ("常用语", "message_text_outline", "yw_phrases"),
    ("编辑面板", "text_box_edit_outline", "yw_edit"),
    ("键盘选择", "keyboard_outline", "yw_kbd_chooser"),
    ("剪贴板", "clipboard_outline", "yw_clipboard"),
    ("语音输入", "microphone_outline", "yw_voice"),
    ("表情", "emoticon_outline", "yw_emoji"),
    ("符号", "ideogram_cjk", "yw_sym"),
    ("数字键盘", "numeric", "yw_num"),
    ("键盘调节", "arrow_expand_vertical", "yw_resize"),
    ("换配色", "palette_outline", "yw_color"),
    ("按键音", "volume_high", "yw_sound"),
    ("繁体输入", "translate", "yw_trad"),
    ("全角半角", "format_letter_case", "yw_fullshape"),
    ("输入方案", "view_list_outline", "yw_schema"),
    ("后台保活", "shield_check_outline", "yw_keepalive"),
    ("设置", "cog_outline", "yw_settings"),
]

# 键盘选择页：(显示名, 图标, 动作)。只放真的能用的键盘；手写 / 笔画 / 五笔做好后再加。
KEYBOARDS = [
    ("拼音 9 键", "dialpad", "yw_s_t9"),
    ("拼音 26 键", "keyboard_outline", "yw_s_qp"),
    ("小鹤双拼", "keyboard_variant", "yw_s_flypy"),
    ("数字键盘", "numeric", "yw_num"),
    ("编辑面板", "text_box_edit_outline", "yw_edit"),
]

# 工具栏：左 Logo(点开工具箱) | 可左右滑动的功能按钮 | 右 收起。buttons[0] 固定在最右（收起），其余从左到右按顺序排列。
_TB = [("keyboard_close", "yw_hide"), ("dialpad", "yw_kbd_chooser"), ("message_text_outline", "yw_phrases"),
       ("clipboard_outline", "yw_clipboard"), ("microphone_outline", "yw_voice"), ("emoticon_outline", "yw_emoji"),
       ("text_box_edit_outline", "yw_edit"), ("arrow_expand_vertical", "yw_resize"), ("translate", "yw_trad"),
       ("palette_outline", "yw_color"), ("cog_outline", "yw_settings")]

TOOL_BAR = """
tool_bar:
  button_spacing: 2
  back_style: "ic@arrow_left"
  primary_button:
    background: {type: circle, corner_radius: 10, normal: 0x00000000, highlight: hilited_candidate_button_color, vertical_inset: 4, horizontal_inset: 4}
    foreground: {style: "一", normal: hilited_candidate_text_color, font_size: 22, padding: 8}
    action: yw_toolbox
    long_press_action: yw_settings
  buttons:
""" + "\n".join('    - {foreground: {style: "ic@%s", normal: candidate_text_color}, action: %s}' % t for t in _TB) + "\n"

STYLE = {
    "key_height": "50", "keyboard_height": "236", "keyboard_height_land": "180",
    "horizontal_gap": "5", "vertical_gap": "9", "round_corner": "9", "key_border": "0",
    "key_text_size": "21", "key_long_text_size": "15", "symbol_text_size": "9", "label_text_size": "15",
    "candidate_text_size": "20", "comment_text_size": "11", "candidate_view_height": "40", "comment_height": "12",
    "candidate_padding": "8", "candidate_spacing": "0.0", "candidate_corner_radius": "8",
    "preview_text_size": "34", "preview_height": "58",
}
