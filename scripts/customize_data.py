#!/usr/bin/env python3
"""Shape the shared data and the installer of a (rebranded) Weasel tree into 一维输入法.

usage: python scripts/customize_data.py <weasel_dir> <rime-ice full.zip>

* 雾凇拼音 (rime-ice) becomes the bundled default schema set.
* The AIME colour themes (renamed) become preset colour schemes, with a modern default style.
* Common developer apps start in English (per-app ascii_mode).
* The 2026 tech vocabulary and the opt-in typing-stats Lua processor are mounted into every rime-ice schema.
* The installer ships the extra data folders and the 一维输入法 helper (YiweiHelper.exe).
"""
import os
import re
import shutil
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
# android/prepare.py imports this module and sets DATA / ICE_ZIP / ANDROID itself
W = sys.argv[1] if __name__ == "__main__" else None
ICE_ZIP = sys.argv[2] if __name__ == "__main__" else None
DATA = os.path.join(W, "output", "data") if W else None
ANDROID = False  # Android (Trime): no Windows helper, so no stats / toast / caret-moving autopair

THEMES = [
    # id, name, 13 colours (argb) in AIME order
    ("yiwei_light", "一维 · 浅色", "F5FCFCFB 14242321 FF6B6A67 FF242321 14242321 FF242321 FF6B6A67 FF8A8986 FFB64032 FFFFFFFF E6FFFFFF E6FFFFFF"),
    ("yiwei_dark", "一维 · 深色", "F21E1E20 1FFFFFFF FFA3A3A0 FFF2F2F0 29FFFFFF FFF2F2F0 FFA3A3A0 FF8E8E8B FFC4503F FFFFFFFF E6FFFFFF E6FFFFFF"),
    ("graphite", "石墨", "F5F5F5F4 1A000000 FF57534E FF1C1917 14000000 FF292524 FFA8A29E FFA8A29E FF292524 FFFAFAF9 FFD6D3D1 FFD6D3D1"),
    ("midnight", "午夜", "F20B1120 2638BDF8 FF94A3B8 FFE2E8F0 3338BDF8 FFE2E8F0 FF64748B FF64748B FF0EA5E9 FF0B1120 FF0C4A6E FF0C4A6E"),
    ("sakura", "樱花", "F5FFF5F7 1FDB2777 FF9D174D FF831843 1FEC4899 FF500724 FFF472B6 FFF9A8D4 FFEC4899 FFFFFFFF FFFCE7F3 FFFCE7F3"),
    ("matcha", "抹茶", "F5F4F9F1 1F4D7C0F FF4D7C0F FF365314 1F65A30D FF1A2E05 FF84A36B FF9CB88A FF4D7C0F FFF7FEE7 FFD9F99D FFD9F99D"),
    ("nord", "北境", "F22E3440 1FD8DEE9 FFD8DEE9 FFECEFF4 3388C0D0 FFE5E9F0 FF81A1C1 FF81A1C1 FF88C0D0 FF2E3440 FF3B4252 FF3B4252"),
    ("paper", "纸墨（竖排适用）", "FAFAF7F0 26000000 FF78716C FF1C1917 1A92400E FF1C1917 FFA8A29E FFB45309 FFB91C1C FFFFFBEB FFFDE68A FFFDE68A"),
]
KEYS = ["back_color", "border_color", "text_color", "hilited_text_color", "hilited_back_color",
        "candidate_text_color", "comment_text_color", "label_color", "hilited_candidate_back_color",
        "hilited_candidate_text_color", "hilited_comment_text_color", "hilited_label_color"]

ASCII_APPS = ["cmd.exe", "conhost.exe", "WindowsTerminal.exe", "OpenConsole.exe", "powershell.exe",
              "pwsh.exe", "wsl.exe", "Code.exe", "Cursor.exe", "Windsurf.exe", "idea64.exe",
              "pycharm64.exe", "webstorm64.exe", "goland64.exe", "clion64.exe", "rider64.exe",
              "devenv.exe", "WindowsPowerShell_ISE.exe", "PowerToys.PowerLauncher.exe", "Everything.exe"]

STYLE = {
    "color_scheme": "yiwei_pill", "color_scheme_dark": "yiwei_pill_dark",
    "font_face": '"Microsoft YaHei UI"', "label_font_face": '"Segoe UI"', "comment_font_face": '"Microsoft YaHei UI"',
    "font_point": "12", "label_font_point": "9", "comment_font_point": "10",
    "horizontal": "true", "inline_preedit": "true", "label_format": '"%s"', "display_tray_icon": "false",
}
# 小狼毫状态提示：不含 ascii_mode（中/英提示由一维助手显示，避免光标旁同时弹出两个「中」）
SHOW_NOTIFICATIONS = "[schema, ascii_punct, full_shape, traditionalization, emoji, search_single_char]"
LAYOUT = {"border_width": "1", "margin_x": "10", "margin_y": "8", "spacing": "8", "candidate_spacing": "10",
          "hilite_spacing": "4", "hilite_padding": "4", "round_corner": "99", "corner_radius": "10",
          "shadow_radius": "6", "shadow_offset_x": "0", "shadow_offset_y": "2", "min_width": "120"}


def set_key(text, indent, key, value):
    pat = re.compile(r"^(%s%s:)[ \t]*[^\n#]*" % (re.escape(indent), re.escape(key)), re.M)
    if pat.search(text):
        return pat.sub(lambda m: m.group(1) + " " + value + " ", text, count=1)
    raise SystemExit("weasel.yaml: key %s%s not found" % (indent, key))


def customize_weasel_yaml():
    path = os.path.join(DATA, "weasel.yaml")
    text = open(path, encoding="utf-8").read().replace("\t#", "  #")
    for k, v in STYLE.items():
        try:
            text = set_key(text, "  ", k, v)
        except SystemExit:
            text = text.replace("\nstyle:\n", "\nstyle:\n  %s: %s\n" % (k, v), 1)
    for k, v in LAYOUT.items():
        text = set_key(text, "    ", k, v)
    apps = "".join("  %s:\n    ascii_mode: true\n" % a.lower() for a in ASCII_APPS if ("  %s:" % a.lower()) not in text)
    text = text.replace("app_options:\n", "app_options:\n" + apps, 1)
    schemes = ""
    for sid, name, cols in THEMES:
        c = cols.split()
        schemes += '  %s:\n    name: "%s"\n    author: "一维输入法"\n    color_format: argb\n' % (sid, name)
        schemes += "".join("    %s: 0x%s\n" % (k, v) for k, v in zip(KEYS, c))
        schemes += "    shadow_color: 0x1A000000\n"
    # 五个品牌色及深色版（默认青碧）
    sys.path.insert(0, HERE)
    from brand_schemes import all_schemes
    for sid, name, cols in all_schemes():
        schemes += '  %s:\n    name: "%s"\n    author: "一维输入法"\n    color_format: argb\n' % (sid, name)
        schemes += "".join("    %s: 0x%08X\n" % (k, v) for k, v in cols.items())
    if "preset_color_schemes:" not in text:
        raise SystemExit("weasel.yaml: preset_color_schemes not found")
    text = text.replace("preset_color_schemes:\n", "preset_color_schemes:\n" + schemes, 1)
    # 中/英切换只弹一维助手的「中/A」提示（设置 → 常规 → 中/英切换提示）：小狼毫自己的
    # ascii_mode 状态提示关掉，其余开关（简繁、全半角、emoji…）和切换方案照常提示
    text, n = re.subn(r"^show_notifications:[ \t]*[^\n#]*", "show_notifications: " + SHOW_NOTIFICATIONS + " ", text, count=1, flags=re.M)
    if n != 1:
        raise SystemExit("weasel.yaml: show_notifications not found")
    text = text.replace("# Weasel settings", "# 一维输入法 前端设置（基于小狼毫 Weasel）", 1)
    open(path, "w", encoding="utf-8", newline="\n").write(text)


def customize_default_yaml():
    """Shift (left or right) commits the typed letters as English and switches to English."""
    path = os.path.join(DATA, "default.yaml")
    text = open(path, encoding="utf-8").read()
    new, n = re.subn(r"^(    Shift_R:)[ \t]*\w+", r"\1 commit_code", text, count=1, flags=re.M)
    if n != 1 or not re.search(r"^    Shift_L:[ \t]*commit_code", new, re.M):
        raise SystemExit("default.yaml: ascii_composer/switch_key not as expected")
    if not ANDROID:
        # 和微软拼音、搜狗一样：中文状态下 / 键也打顿号「、」（\ 本来就是）；
        # 数字后面的 / 照旧是斜杠（1/2、10/11），双击 / 恢复成「、」
        new, n1 = re.subn(r"^(  half_shape:\n(?:    .*\n)*?    '/' : )'/'", r"\1'、'", new, count=1, flags=re.M)
        new, n2 = re.subn(r'^(  digit_separators: ",\.:)"', r'\1/"', new, count=1, flags=re.M)
        if (n1, n2) != (1, 1):
            raise SystemExit("default.yaml: punctuator '/' not as expected %r" % ((n1, n2),))
    open(path, "w", encoding="utf-8", newline="\n").write(new)


def install_rime_ice():
    keep_ours = {"weasel.yaml", "squirrel.yaml", "README.md", "LICENSE"}
    with zipfile.ZipFile(ICE_ZIP) as z:
        for info in z.infolist():
            name = info.filename
            if name.endswith("/") or os.path.basename(name) in keep_ours or name.startswith("others/"):
                continue
            dest = os.path.join(DATA, *name.split("/"))
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            with z.open(info) as src, open(dest, "wb") as dst:
                shutil.copyfileobj(src, dst)
    # our overlay (lua, tech vocabulary)
    src_root = os.path.join(REPO, "overlay", "data")
    for dirpath, _, files in os.walk(src_root):
        for fn in files:
            src = os.path.join(dirpath, fn)
            dest = os.path.join(DATA, os.path.relpath(src, src_root))
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            shutil.copyfile(src, dest)


TECH_BLOCK = """
# 一维输入法：科技与互联网补充词库
yiwei_tech:
  dictionary: ""
  user_dict: yiwei_tech
  db_class: stabledb
  enable_completion: false
  enable_sentence: false
  initial_quality: 1
"""


STROKE_BLOCK = """
# 一维输入法：笔画输入（W4），u + hspnz（横竖撇点折），至少两笔；候选注释是拼音
yiwei_stroke:
  tag: yiwei_stroke
  dictionary: yiwei_stroke
  enable_user_dict: false
  enable_completion: true
  prefix: "u"
  tips: "  〔笔画 h横 s竖 p撇 n点 z折〕"
  preedit_format:
    - xlit/hspnz/一丨丿丶乙/
yiwei_stroke_reverse:
  tags: [ yiwei_stroke ]
  dictionary: rime_ice
"""


def mount_stroke(text):
    """W4 笔画：雾凇全拼里 u + 笔画（u 不是拼音声母；至少两笔，避免吞掉英文 up / us）。"""
    steps = [
        (r"(\n  dependencies:\n)", r"\1    - yiwei_stroke  # 一维输入法：笔画输入词典\n"),
        (r"(\n(\s+)- affix_segmentor@radical_lookup[^\n]*\n)", r"\1\2- affix_segmentor@yiwei_stroke  # 一维输入法：u + 笔画\n"),
        (r"(\n(\s+)- table_translator@radical_lookup[^\n]*\n)", r"\1\2- table_translator@yiwei_stroke  # 一维输入法：笔画输入\n"),
        (r"(\n(\s+)- reverse_lookup_filter@radical_reverse_lookup[^\n]*\n)", r"\1\2- reverse_lookup_filter@yiwei_stroke_reverse  # 一维输入法：笔画候选注拼音\n"),
        (r"(\nrecognizer:\n(?:  [^\n]*\n)*?  patterns:[^\n]*\n)", r'\1    yiwei_stroke: "^u[hspnz]{2,}$"\n'),
    ]
    for pat, rep in steps:
        text, n = re.subn(pat, rep, text, count=1)
        if n != 1:
            raise SystemExit("rime_ice.schema.yaml: stroke mount failed at %r" % pat)
    return text + STROKE_BLOCK


def mount_into_schemas():
    count = 0
    for fn in os.listdir(DATA):
        if not fn.endswith(".schema.yaml"):
            continue
        path = os.path.join(DATA, fn)
        text = open(path, encoding="utf-8").read()
        if "table_translator@custom_phrase" not in text:
            continue  # only full pinyin / double pinyin schemas of rime-ice
        if not ANDROID:
            text = re.sub(r"(\n  processors:\n)", r"\1    - lua_processor@*yiwei_game  # 一维输入法：全屏游戏自动英文、Shift 交给游戏\n    - lua_processor@*yiwei_appmode  # 一维输入法：按程序记住中英状态（重启后也记得）\n    - lua_processor@*yiwei_stats  # 一维输入法：输入统计（默认关闭）\n    - lua_processor@*yiwei_toast  # 一维输入法：中/英切换提示\n    - lua_processor@*yiwei_regret  # 一维输入法：上屏后 Ctrl+Backspace 反悔重选\n    - lua_processor@*yiwei_private  # 一维输入法：无痕窗口里选词不进用户词库\n", text, count=1)
        # after ascii_composer, so a swallowed Shift+( never looks like a lone Shift tap
        extra = "" if ANDROID else r"\2- lua_processor@*yiwei_autopair  # 一维输入法：括号自动配对\n"
        text, n = re.subn(r"(\n(\s+)- ascii_composer[^\n]*\n)",
                          r"\1" + extra +
                          r"\2- lua_processor@*yiwei_pangu  # 一维输入法：中英之间自动加空格（默认关闭）\n"
                          r"\2- lua_processor@*yiwei_pin  # 一维输入法：Ctrl+T 置顶 / Ctrl+Delete 隐藏候选\n", text, count=1)
        if n != 1:
            raise SystemExit("%s: ascii_composer not found" % fn)
        text, n = re.subn(r"(\n  filters:\n(?:    [^\n]*\n)*)", r"\1    - lua_filter@*yiwei_learn  # 一维输入法：按习惯自动调候选（可关、可清除）\n    - lua_filter@*yiwei_pin_filter  # 一维输入法：置顶 / 隐藏候选\n    - lua_filter@*yiwei_pangu_filter  # 一维输入法：中英之间自动加空格\n", text, count=1)
        if n != 1:
            raise SystemExit("%s: filters not found" % fn)
        text = re.sub(r"(\n(\s+)- table_translator@custom_phrase[^\n]*\n)",
                      r"\1\2- table_translator@yiwei_tech     # 一维输入法：科技与互联网词库\n"
                      r"\2- lua_translator@*yiwei_convert   # 一维输入法：hs5km 单位 / 汇率换算\n", text, count=1)
        # hs + 数字 + 单位：让数字留在输入码里（和雾凇的 R、N 一样靠 recognizer）
        text, n = re.subn(r"(\nrecognizer:\n(?:  [^\n]*\n)*?  patterns:[^\n]*\n)", r'\1    yiwei_convert: "^hs[0-9][0-9.]*[a-z]*$"\n', text, count=1)
        if n != 1:
            raise SystemExit("%s: recognizer/patterns not found" % fn)
        if fn == "rime_ice.schema.yaml" and not ANDROID:
            text = mount_stroke(text)
        text = text.rstrip("\n") + "\n" + TECH_BLOCK
        open(path, "w", encoding="utf-8", newline="\n").write(text)
        count += 1
    if count == 0:
        raise SystemExit("no rime-ice schema patched")
    print("patched %d schemas" % count)
    if not ANDROID:
        write_shuangpin_keymaps()

CIRCLED = "ⒶⒷⒸⒹⒺⒻⒼⒽⒾⒿⓀⓁⓂⓃⓄⓅⓆⓇⓈⓉⓊⓋⓌⓍⓎⓏ"


def _expand(alt):
    """'[iu]ang' → iang, uang；'i?ong' → ong, iong；去掉 (.) 与锚点。v 显示成 ü。"""
    alt = alt.replace("(.)", "").replace("$", "")
    outs = [""]
    i = 0
    while i < len(alt):
        c = alt[i]
        if c == "[":
            j = alt.index("]", i)
            opts = list(alt[i + 1:j])
            i = j + 1
        else:
            opts = [c]
            i += 1
        if i < len(alt) and alt[i] == "?":
            opts = [""] + opts
            i += 1
        outs = [o + x for o in outs for x in opts]
    return [o.replace("v", "ü") for o in outs if o]


def shuangpin_keymap(schema_text):
    """从雾凇双拼方案的 speller/algebra（xform/韵母$/Ⓧ/ … xlit/Ⓧ…/x…/）推出键位：{键: [声母或韵母…]}。"""
    m = re.search(r"\n  algebra:\n((?:    [^\n]*\n)+)", schema_text)
    if not m:
        return None
    lines = [l.strip()[2:].strip() for l in m.group(1).splitlines() if l.strip().startswith("- ")]
    xl = [l for l in lines if l.startswith("xlit/") and any(c in l for c in CIRCLED)]
    if not xl:
        return None
    _, src, dst, _ = xl[-1].split("/")
    to_key = dict(zip(src, dst))
    by_mark = {}
    for l in lines:
        parts = l.split("/")
        if len(parts) < 4:
            continue
        op, pat, rep = parts[0], parts[1], parts[2]
        mark = rep[-1:] if rep else ""
        if op == "derive" and len(pat) == 2 and pat[0] in CIRCLED and pat[1] == "$" and mark in CIRCLED:
            by_mark.setdefault(mark, []).extend(x for x in by_mark.get(pat[0], []) if x not in by_mark.get(mark, []))
            continue
        if op != "xform" or not (mark in CIRCLED or rep == ";"):
            continue
        labels = []
        for alt in pat.split("|"):
            alt = alt.lstrip("^")
            labels += _expand(alt)
        by_mark.setdefault(mark, []).extend(x for x in labels if x not in by_mark.get(mark, []))
    keys = {}
    for mark, labels in by_mark.items():
        k = to_key.get(mark, mark)
        keys.setdefault(k, []).extend(labels)
    # 单个字母的韵母落在自己键上
    for v in "aoeiu":
        keys.setdefault(v, []).insert(0, v)
    keys.setdefault("v", []).insert(0, "ü")
    return keys


SHUANGPIN_NAMES = {"double_pinyin_flypy": "小鹤双拼", "double_pinyin": "自然码双拼", "double_pinyin_mspy": "微软双拼",
                   "double_pinyin_sogou": "搜狗双拼", "double_pinyin_ziguang": "紫光双拼", "double_pinyin_abc": "智能ABC双拼",
                   "double_pinyin_jiajia": "拼音加加双拼"}


def write_shuangpin_keymaps():
    """W2 双拼键位图：data/yiwei_shuangpin.txt（方案<TAB>名称<TAB>键<TAB>声母韵母,…），一维助手的键位图浮窗读它。"""
    out = []
    for fn in sorted(os.listdir(DATA)):
        sid = fn[:-len(".schema.yaml")] if fn.endswith(".schema.yaml") else ""
        if not sid.startswith("double_pinyin"):
            continue
        km = shuangpin_keymap(open(os.path.join(DATA, fn), encoding="utf-8").read())
        if not km:
            continue
        for k in sorted(km):
            out.append("%s\t%s\t%s\t%s" % (sid, SHUANGPIN_NAMES.get(sid, sid), k, ",".join(km[k])))
    if not out:
        raise SystemExit("no double pinyin keymap generated")
    with open(os.path.join(DATA, "yiwei_shuangpin.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("# 一维输入法 · 双拼键位图（由 customize_data.py 从各双拼方案的 speller/algebra 生成）\n")
        f.write("\n".join(out) + "\n")
    print("shuangpin keymaps: %d lines" % len(out))


NSI_DATA = r'''
  ; 一维输入法：雾凇拼音与扩展数据
  SetOutPath $INSTDIR\data\lua
  File /r "data\lua\*.*"
  SetOutPath $INSTDIR\data\cn_dicts
  File /r "data\cn_dicts\*.*"
  SetOutPath $INSTDIR\data\en_dicts
  File /r "data\en_dicts\*.*"
  SetOutPath $INSTDIR\data\opencc
  File /nonfatal "data\opencc\*.txt"
  ; 一维输入法助手（常用语、AI、设置、统计、词库更新）
  SetOutPath $INSTDIR
  File "YiweiHelper.exe"
  File /nonfatal "YiweiHelper.exe.config"
  File /nonfatal "helperlibs\*.dll"
'''

NSI_HELPER_RUN = r'''
  ; 一维输入法助手：开机启动、theme 链接协议
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Run" "YiweiHelper" '"$INSTDIR\YiweiHelper.exe" /background'
  WriteRegStr HKCR "yiwei-ime" "" "URL:一维输入法"
  WriteRegStr HKCR "yiwei-ime" "URL Protocol" ""
  WriteRegStr HKCR "yiwei-ime\DefaultIcon" "" '"$INSTDIR\YiweiHelper.exe",0'
  WriteRegStr HKCR "yiwei-ime\shell\open\command" "" '"$INSTDIR\YiweiHelper.exe" "%1"'
  Exec '"$INSTDIR\YiweiHelper.exe" /background'
  ; W45 崩溃时 Windows 错误报告自动保存 minidump（每个程序最多 5 个），「导出诊断信息」会带上最近 3 个
  SetRegView 64
  WriteRegExpandStr HKLM "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\YiweiServer.exe" "DumpFolder" "%LOCALAPPDATA%\YiweiIME\CrashDumps"
  WriteRegDWORD HKLM "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\YiweiServer.exe" "DumpCount" 5
  WriteRegDWORD HKLM "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\YiweiServer.exe" "DumpType" 1
  WriteRegExpandStr HKLM "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\YiweiDeployer.exe" "DumpFolder" "%LOCALAPPDATA%\YiweiIME\CrashDumps"
  WriteRegDWORD HKLM "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\YiweiDeployer.exe" "DumpCount" 5
  WriteRegDWORD HKLM "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\YiweiDeployer.exe" "DumpType" 1
  WriteRegExpandStr HKLM "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\YiweiHelper.exe" "DumpFolder" "%LOCALAPPDATA%\YiweiIME\CrashDumps"
  WriteRegDWORD HKLM "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\YiweiHelper.exe" "DumpCount" 5
  WriteRegDWORD HKLM "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\YiweiHelper.exe" "DumpType" 1
  SetRegView lastused
'''

NSI_SHORTCUT = r'''  CreateShortCut "$SMPROGRAMS\$(DISPLAYNAME)\【一维输入法】设置.lnk" "$INSTDIR\YiweiHelper.exe" "/settings" "$INSTDIR\YiweiHelper.exe" 0
'''

NSI_UNINSTALL = r'''
  ExecWait 'taskkill /f /im YiweiHelper.exe'
  DeleteRegValue HKLM "Software\Microsoft\Windows\CurrentVersion\Run" "YiweiHelper"
  DeleteRegKey HKCR "yiwei-ime"
  SetRegView 64
  DeleteRegKey HKLM "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\YiweiServer.exe"
  DeleteRegKey HKLM "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\YiweiDeployer.exe"
  DeleteRegKey HKLM "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\YiweiHelper.exe"
  SetRegView lastused
  RMDir /r "$INSTDIR\data\lua"
  RMDir /r "$INSTDIR\data\cn_dicts"
  RMDir /r "$INSTDIR\data\en_dicts"
  Delete "$INSTDIR\YiweiHelper.exe"
  Delete "$INSTDIR\YiweiHelper.exe.config"
  ; W42 卸载时可选清除个人数据（用户词库、常用语、设置、剪贴板历史）。默认保留；静默卸载（/S）保留。
  ; 覆盖安装/升级时安装程序会先静默卸载旧版，同样保留。
  IfSilent yw_keep_data
  SetShellVarContext current
  IfFileExists "$APPDATA\YiweiIME\*.*" 0 yw_keep_data
  MessageBox MB_YESNO|MB_ICONQUESTION|MB_DEFBUTTON2 "要同时删除你的个人数据吗？$\r$\n$\r$\n包括用户词库（打字学到的词）、常用语、设置、剪贴板历史和统计，位置：$\r$\n$APPDATA\YiweiIME$\r$\n$\r$\n选「否」保留，以后重新安装会自动用回这些数据。" IDYES yw_clean_data
  Goto yw_keep_data
yw_clean_data:
  RMDir /r "$APPDATA\YiweiIME"
yw_keep_data:
  SetShellVarContext all
'''


NSI_PAGES = r'''; ---- 一维输入法安装界面（A 版「纯蓝整窗」）----
; 欢迎页是 nsDialogs 自定义页，盖住整个窗口（按钮栏也隐藏）：白标 + 标题 + 白色胶囊「立即安装」+「自定义安装位置 ›」+ 底部小字。
; 进度页：蓝色页眉白字 + 白底 + 蓝色进度条；完成页：整页纯蓝白字。
!define MUI_BGCOLOR 2B5BD7
!define MUI_TEXTCOLOR FFFFFF
!define MUI_INSTFILESPAGE_COLORS "1F2421 FFFFFF"
!define MUI_INSTFILESPAGE_PROGRESSBAR "smooth"
!define MUI_FINISHPAGE_LINK_COLOR DCE6FF

Var YwDialog
Var YwCustomDir
Var YwHeroBmp
Var YwHeroHandle
Var YwBtn
Var YwBtnHandle
Var YwFontLink
Var YwFontFoot
!macro YwOuterVar ID
  Var YwVis${ID}
!macroend
!insertmacro YwOuterVar 1
!insertmacro YwOuterVar 2
!insertmacro YwOuterVar 3
!insertmacro YwOuterVar 1028
!insertmacro YwOuterVar 1034
!insertmacro YwOuterVar 1035
!insertmacro YwOuterVar 1036
!insertmacro YwOuterVar 1037
!insertmacro YwOuterVar 1038
!insertmacro YwOuterVar 1039
!insertmacro YwOuterVar 1045
!insertmacro YwOuterVar 1046
!insertmacro YwOuterVar 1256
; 记下控件自己的 WS_VISIBLE 位（欢迎页显示时主窗口还没显示，IsWindowVisible 会把所有按钮都当成隐藏，
; 离开欢迎页后「下一步/取消」就再也不出来了 —— 完成页只剩「< 上一步」就是这个原因）
!macro YwHideOuter ID
  GetDlgItem $0 $HWNDPARENT ${ID}
  StrCpy $YwVis${ID} 0
  ${If} $0 <> 0
    System::Call "user32::GetWindowLongW(p$0, i-16)i.r1"
    IntOp $YwVis${ID} $1 & 0x10000000
    ShowWindow $0 ${SW_HIDE}
  ${EndIf}
!macroend
!macro YwShowOuter ID
  GetDlgItem $0 $HWNDPARENT ${ID}
  ${If} $0 <> 0
  ${AndIf} $YwVis${ID} <> 0
    ShowWindow $0 ${SW_SHOW}
  ${EndIf}
!macroend

!macro YwFinishButtons
  GetDlgItem $0 $HWNDPARENT 3
  ShowWindow $0 ${SW_HIDE}
  GetDlgItem $0 $HWNDPARENT 2
  ShowWindow $0 ${SW_HIDE}
  GetDlgItem $0 $HWNDPARENT 1
  ShowWindow $0 ${SW_SHOW}
  EnableWindow $0 1
  SendMessage $0 ${WM_SETTEXT} 0 "STR:完成"
!macroend

Function YwWelcomeShow
  nsDialogs::Create 1044
  Pop $YwDialog
  ${If} $YwDialog == error
    Abort
  ${EndIf}
  SetCtlColors $YwDialog "" 2B5BD7

  ; 整窗：把页面铺满客户区，藏起按钮栏、页眉和品牌字
  LockWindow on
  !insertmacro YwHideOuter 1
  !insertmacro YwHideOuter 2
  !insertmacro YwHideOuter 3
  !insertmacro YwHideOuter 1028
  !insertmacro YwHideOuter 1034
  !insertmacro YwHideOuter 1035
  !insertmacro YwHideOuter 1036
  !insertmacro YwHideOuter 1037
  !insertmacro YwHideOuter 1038
  !insertmacro YwHideOuter 1039
  !insertmacro YwHideOuter 1045
  !insertmacro YwHideOuter 1046
  !insertmacro YwHideOuter 1256
  System::Call "*(i,i,i,i)p.r2"
  System::Call "user32::GetClientRect(p$HWNDPARENT, pr2)"
  System::Call "*$2(i,i,i.r3,i.r4)"
  System::Free $2
  System::Call "user32::SetWindowPos(p$YwDialog, p0, i0, i0, ir3, ir4, i0x0040)"
  LockWindow off

  ; 位图按 96 DPI 设计，按系统缩放换算成像素位置
  System::Call "user32::GetDC(p0)p.r5"
  System::Call "gdi32::GetDeviceCaps(p$5, i88)i.r6"
  System::Call "user32::ReleaseDC(p0, p$5)"
  ${If} $6 < 96
    StrCpy $6 96
  ${EndIf}

  ; 白标 + 一维输入法 + 中文常新，自在表达（一张位图）
  IntOp $7 $6 * 320
  IntOp $7 $7 / 96
  IntOp $8 $6 * 170
  IntOp $8 $8 / 96
  IntOp $1 $3 - $7
  IntOp $1 $1 / 2
  IntOp $9 $4 * 10
  IntOp $9 $9 / 100
  ${NSD_CreateBitmap} $1 $9 $7 $8 ""
  Pop $YwHeroBmp
  ${NSD_SetStretchedImage} $YwHeroBmp "$PLUGINSDIR\yiwei-hero.bmp" $YwHeroHandle

  ; 白色胶囊按钮「立即安装」
  IntOp $7 $6 * 180
  IntOp $7 $7 / 96
  IntOp $8 $6 * 40
  IntOp $8 $8 / 96
  IntOp $1 $3 - $7
  IntOp $1 $1 / 2
  IntOp $9 $4 * 62
  IntOp $9 $9 / 100
  ${NSD_CreateBitmap} $1 $9 $7 $8 ""
  Pop $YwBtn
  ${NSD_SetStretchedImage} $YwBtn "$PLUGINSDIR\yiwei-button.bmp" $YwBtnHandle
  ${NSD_OnClick} $YwBtn YwInstallNow

  ; 自定义安装位置 ›
  CreateFont $YwFontLink "Microsoft YaHei UI" 9 400
  CreateFont $YwFontFoot "Microsoft YaHei UI" 8 400
  IntOp $9 $4 * 76
  IntOp $9 $9 / 100
  nsDialogs::CreateControl STATIC ${WS_VISIBLE}|${WS_CHILD}|${WS_CLIPSIBLINGS}|${SS_NOTIFY}|${SS_CENTER} 0 0 $9 100% 20u "自定义安装位置  ›"
  Pop $0
  SetCtlColors $0 D2DEFF 2B5BD7
  SendMessage $0 ${WM_SETFONT} $YwFontLink 1
  ${NSD_OnClick} $0 YwCustomize

  ; 版本 · 开源免费 · 输入只在本机处理
  IntOp $9 $4 * 89
  IntOp $9 $9 / 100
  nsDialogs::CreateControl STATIC ${WS_VISIBLE}|${WS_CHILD}|${WS_CLIPSIBLINGS}|${SS_CENTER} 0 0 $9 100% 16u "v${WEASEL_VERSION}.${WEASEL_BUILD}  ·  开源免费  ·  输入只在本机处理"
  Pop $0
  SetCtlColors $0 BECDFA 2B5BD7
  SendMessage $0 ${WM_SETFONT} $YwFontFoot 1

  nsDialogs::Show
  ${NSD_FreeImage} $YwHeroHandle
  ${NSD_FreeImage} $YwBtnHandle
FunctionEnd

Function YwWelcomeLeave
  LockWindow on
  !insertmacro YwShowOuter 1
  !insertmacro YwShowOuter 2
  !insertmacro YwShowOuter 3
  !insertmacro YwShowOuter 1028
  !insertmacro YwShowOuter 1034
  !insertmacro YwShowOuter 1035
  !insertmacro YwShowOuter 1036
  !insertmacro YwShowOuter 1037
  !insertmacro YwShowOuter 1038
  !insertmacro YwShowOuter 1039
  !insertmacro YwShowOuter 1045
  !insertmacro YwShowOuter 1046
  !insertmacro YwShowOuter 1256
  ; 按钮栏的「下一步」「取消」无论如何都要回来（「上一步」由 NSIS 按页面自己管）
  GetDlgItem $0 $HWNDPARENT 1
  ShowWindow $0 ${SW_SHOW}
  GetDlgItem $0 $HWNDPARENT 2
  ShowWindow $0 ${SW_SHOW}
  LockWindow off
FunctionEnd

Function YwInstallNow
  Pop $0
  StrCpy $YwCustomDir 0
  SendMessage $HWNDPARENT 0x408 1 0
FunctionEnd

Function YwCustomize
  Pop $0
  StrCpy $YwCustomDir 1
  SendMessage $HWNDPARENT 0x408 1 0
FunctionEnd

Function YwDirectoryPre
  ${If} $YwCustomDir != 1
    Abort
  ${EndIf}
FunctionEnd

Function YwInstFilesShow
  FindWindow $0 "#32770" "" $HWNDPARENT
  SetCtlColors $0 "" FFFFFF
  GetDlgItem $1 $0 1006
  SetCtlColors $1 1F2421 FFFFFF
  GetDlgItem $1 $0 1004
  System::Call 'UXTHEME::SetWindowTheme(p$1,w" ",w" ")'
  SendMessage $1 0x2001 0 0xF7EBE6
  SendMessage $1 0x0409 0 0xD75B2B
FunctionEnd

Page custom YwWelcomeShow YwWelcomeLeave
!define MUI_PAGE_CUSTOMFUNCTION_PRE YwDirectoryPre
!insertmacro MUI_PAGE_DIRECTORY
!define MUI_PAGE_CUSTOMFUNCTION_SHOW YwInstFilesShow
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE "装好了"
!define MUI_FINISHPAGE_TEXT "按 Win + 空格 切换到「一维输入法」就能打字。$\r$\n首次引导会帮你选拼音方案和外观，只要一分钟。"
!define MUI_FINISHPAGE_RUN "$INSTDIR\YiweiHelper.exe"
!define MUI_FINISHPAGE_RUN_PARAMETERS "/wizard"
!define MUI_FINISHPAGE_RUN_TEXT "立即体验（打开首次引导）"
!define MUI_FINISHPAGE_BUTTON "完成"
!define MUI_FINISHPAGE_REBOOTLATER_DEFAULT
!define MUI_PAGE_CUSTOMFUNCTION_SHOW YwFinishShow
!insertmacro MUI_PAGE_FINISH

; 完成页的复选框默认主题会画成黑字，换成经典样式才能白字蓝底（变量由上面的 MUI 完成页声明）
; 结束页：只留一个明显的「完成」，「上一步」「取消」隐藏
Function YwFinishShow
  System::Call 'UXTHEME::SetWindowTheme(p$mui.FinishPage.Run,w" ",w" ")'
  SetCtlColors $mui.FinishPage.Run FFFFFF 2B5BD7
  !insertmacro YwFinishButtons
FunctionEnd

Function un.YwFinishShow
  !insertmacro YwFinishButtons
FunctionEnd

'''

# the welcome page bitmaps travel inside the installer ($PLUGINSDIR), not into the install folder
# W43 安装时检测原版小狼毫（Weasel）：同是 RIME，可共存，但 Ctrl+` / Shift 等快捷键和两个候选框会混淆。
# 在 customize_data（rebrand 之后）插入，所以这里的 Software\Rime\Weasel 不会被改名。静默安装不提示。
NSI_OLD_WEASEL = r'''  IfSilent yw_weasel_done
  StrCpy $R8 ""
  ReadRegStr $R8 HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Weasel" "UninstallString"
  StrCmp $R8 "" 0 yw_weasel_found
  SetRegView 64
  ReadRegStr $R8 HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Weasel" "UninstallString"
  SetRegView lastused
  StrCmp $R8 "" yw_weasel_done yw_weasel_found
yw_weasel_found:
  MessageBox MB_YESNOCANCEL|MB_ICONEXCLAMATION "检测到这台电脑装着「小狼毫」输入法。$\r$\n$\r$\n一维输入法基于小狼毫，两者可以同时安装，但同时启用时 Ctrl+`、Shift 切换等快捷键会互相抢，任务栏也会出现两个输入法。$\r$\n小狼毫的用户词库不会被改动；需要的话可以在一维设置 → 词库 → 导入里把它导进来。$\r$\n$\r$\n「是」：先打开小狼毫的卸载程序$\r$\n「否」：保留小狼毫，继续安装$\r$\n「取消」：退出安装" IDYES yw_weasel_uninst IDNO yw_weasel_done
  Quit
yw_weasel_uninst:
  ExecWait '$R8'
yw_weasel_done:
'''

NSI_ONINIT = r'''  ; 安装/卸载界面一律简体中文（按钮「下一步」「完成」「卸载」…）
  StrCpy $LANGUAGE ${LANG_SIMPCHINESE}
  InitPluginsDir
  File "/oname=$PLUGINSDIR\yiwei-hero.bmp" "..\resource\installer-hero.bmp"
  File "/oname=$PLUGINSDIR\yiwei-button.bmp" "..\resource\installer-button.bmp"
''' + NSI_OLD_WEASEL


# ---- 一维：升级不需要重启 ----
# Weasel 安装器要求重启的原因：①升级时先「卸载旧版」（YiweiSetup /u 注销输入法、删系统目录 DLL）并 SetRebootFlag；
# ②安装段末尾对升级无条件 SetRebootFlag；③卸载段也 SetRebootFlag。技术上并不需要：系统目录里的 yiwei*.dll
# 被各程序加载时不能覆盖，但可以改名 —— YiweiSetup 把它改名成 .old.<时间戳> 再复制新文件到原名，
# 已开的程序继续用旧 DLL，新开的程序用新 DLL（CLSID 指向的路径不变）；安装目录里的文件只被服务、助手、
# 部署程序占用，先停掉它们再替换，装完自动重启服务和助手。只有 YiweiSetup 注册失败（极端情况）才提示重启，默认「稍后」。
NSI_UPGRADE_FUNCS = r"""
; 停掉一维的进程（不弹黑窗）
!macro YwStopProcesses
  nsExec::Exec 'taskkill /f /im YiweiHelper.exe'
  Pop $9
  nsExec::Exec 'taskkill /f /im YiweiDeployer.exe'
  Pop $9
  nsExec::Exec 'taskkill /f /im YiweiServer.exe'
  Pop $9
!macroend

; 安装目录里删不掉（仍被占用）的 exe/dll 改名让位，下次升级再清掉；不用 /REBOOTOK，不要求重启
Function YwFreeLocked
  Delete "$INSTDIR\*.yiwei-old"
  System::Call "kernel32::GetTickCount()i.r8"
  FindFirst $6 $7 "$INSTDIR\*.*"
  ${DoWhile} $7 != ""
    StrCpy $5 $7 4 -4
    ${If} $5 == ".dll"
    ${OrIf} $5 == ".exe"
      Delete "$INSTDIR\$7"
      ${If} ${FileExists} "$INSTDIR\$7"
        Rename "$INSTDIR\$7" "$INSTDIR\$7.$8.yiwei-old"
      ${EndIf}
    ${EndIf}
    FindNext $6 $7
  ${Loop}
  FindClose $6
FunctionEnd

; 扩展名 .yiwei-old 的文件下次安装时清掉；安装失败需要重启时退出码 3010（CI 用来检查升级不需要重启）
Function .onInstSuccess
  IfRebootFlag 0 +2
  SetErrorLevel 3010
FunctionEnd
"""

def patch_installer():
    path = os.path.join(W, "output", "install.nsi")
    raw = open(path, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    nl = "\r\n" if "\r\n" in text else "\n"
    t = text.replace("\r\n", "\n")

    anchor = '  File "data\\preview\\*.png"\n'
    assert anchor in t, "nsi: preview anchor missing"
    t = t.replace(anchor, anchor + NSI_DATA, 1)

    anchor = '  Exec "$INSTDIR\\YiweiServer.exe"\n'
    assert anchor in t, "nsi: server exec anchor missing"
    t = t.replace(anchor, anchor + NSI_HELPER_RUN, 1)

    m = re.search(r'  CreateShortCut "\$SMPROGRAMS\\\$\(DISPLAYNAME\)\\\$\(LNKFORSETTING\)\.lnk"[^\n]*\n', t)
    assert m, "nsi: settings shortcut anchor missing"
    t = t[:m.start()] + NSI_SHORTCUT + t[m.start():]

    anchor = "  ExecWait '\"$INSTDIR\\YiweiSetup.exe\" /u'\n"
    idx = t.rfind(anchor)
    assert idx > 0, "nsi: uninstall anchor missing"
    t = t[:idx + len(anchor)] + NSI_UNINSTALL + t[idx + len(anchor):]

    # branded, near one-click installer: welcome -> install -> finish (launch the wizard)
    pages_old = ('!insertmacro MUI_PAGE_LICENSE "LICENSE.txt"\n'
                 '!insertmacro MUI_PAGE_DIRECTORY\n'
                 '!insertmacro MUI_PAGE_INSTFILES\n'
                 '!insertmacro MUI_PAGE_FINISH\n')
    assert pages_old in t, "nsi: pages anchor missing"
    t = t.replace(pages_old, NSI_PAGES, 1)
    anchor = "Function .onInit\n"
    assert anchor in t, "nsi: .onInit anchor missing"
    t = t.replace(anchor, anchor + NSI_ONINIT, 1)
    # 卸载完成页：同样只留「完成」
    anchor = "!insertmacro MUI_UNPAGE_FINISH\n"
    assert anchor in t, "nsi: uninstall finish page anchor missing"
    t = t.replace(anchor, "!define MUI_PAGE_CUSTOMFUNCTION_SHOW un.YwFinishShow\n!define MUI_FINISHPAGE_BUTTON \"完成\"\n" + anchor, 1)
    # 卸载程序：同样简体中文
    assert "Function un.onInit" not in t
    anchor = "; Uninstaller\n"
    assert anchor in t, "nsi: uninstaller anchor missing"
    t = t.replace(anchor, "Function un.onInit\n  StrCpy $LANGUAGE ${LANG_SIMPCHINESE}\nFunctionEnd\n\n" + anchor, 1)
    t = t.replace("!define MUI_ICON ..\\resource\\weasel.ico\n",
                  "!define MUI_ICON ..\\resource\\weasel.ico\n!define MUI_UNICON ..\\resource\\weasel.ico\n"
                  "!define MUI_WELCOMEFINISHPAGE_BITMAP ..\\resource\\installer-welcome.bmp\n"
                  "!define MUI_UNWELCOMEFINISHPAGE_BITMAP ..\\resource\\installer-welcome.bmp\n"
                  "!define MUI_HEADERIMAGE\n!define MUI_HEADERIMAGE_RIGHT\n"
                  "!define MUI_HEADERIMAGE_BITMAP ..\\resource\\installer-header.bmp\n"
                  "!define MUI_ABORTWARNING\nBrandingText \"一维输入法\"\n", 1)
    # 一维：安装时不弹 Weasel 的「方案选单 / 界面风格」对话框，方案和外观交给首次引导（完成页「立即体验」）
    old_inst = '  ExecWait "$INSTDIR\\YiweiDeployer.exe /install"\n'
    assert old_inst in t, "nsi: deployer /install anchor missing"
    t = t.replace(old_inst, '  ExecWait "$INSTDIR\\YiweiDeployer.exe /deploy"\n', 1)
    # before upgrading, stop the running helper as well
    t = t.replace("  ExecWait '\"$R1\\YiweiServer.exe\" /quit'\n",
                  "  ExecWait '\"$R1\\YiweiServer.exe\" /quit'\n  ExecWait 'taskkill /f /im YiweiHelper.exe'\n", 1)
    t = patch_upgrade(t)
    out = t.replace("\n", nl)
    open(path, "wb").write((b"\xef\xbb\xbf" if bom else b"") + out.encode("utf-8"))


def patch_upgrade(t):
    """升级不需要重启（见 NSI_UPGRADE_FUNCS 上的说明）。"""
    # .onInit 只判断是否升级；旧版的清理挪到真正安装时（YwUpgrade），不再弹「请先卸载旧版本」，取消安装也不会删掉旧版
    i = t.index('  StrCpy $0 "Upgrade"\n')
    j = t.index("uninst:\n", i)
    k = t.index("done:\nFunctionEnd\n", j)
    block = t[j + len("uninst:\n"):k]
    # 用专门的变量记「升级」：$0 会被安装界面各页的回调改掉
    t = t[:i] + '  StrCpy $YwUpgrading 1\n\n' + t[k:]
    t = t.replace("Function .onInit\n", "Var YwUpgrading\nFunction .onInit\n", 1)
    for old in ("  ExecWait '\"$R1\\YiweiSetup.exe\" /u'\n",  # 不注销输入法：Windows 不会把它从键盘列表里拿掉
                "  ; Prompt reboot\n  SetRebootFlag true\n",
                "  ExecWait 'taskkill /f /im YiweiHelper.exe'\n"):
        assert old in block, "nsi upgrade block: %r" % old
        block = block.replace(old, "", 1)
    block = block.replace("  ExecWait '\"$R1\\YiweiServer.exe\" /quit'\n",
                          "  ExecWait '\"$R1\\YiweiServer.exe\" /quit'\n  !insertmacro YwStopProcesses\n", 1)
    assert "YwStopProcesses" in block
    upgrade = "Function YwUpgrade\n" + block + "FunctionEnd\n"
    anchor = "; The stuff to install\n"
    assert anchor in t, "nsi: section anchor missing"
    t = t.replace(anchor, NSI_UPGRADE_FUNCS.lstrip("\n") + "\n" + upgrade + "\n" + anchor, 1)
    old = "  SectionIn RO\n"
    assert old in t
    t = t.replace(old, old + "\n  ; 一维：升级时先停掉旧版进程、清掉旧文件（不卸载输入法、不要求重启）\n  StrCmp $YwUpgrading 1 0 +2\n  Call YwUpgrade\n", 1)
    old = "  IfFileExists \"$INSTDIR\\YiweiServer.exe\" 0 +2\n  ExecWait '\"$INSTDIR\\YiweiServer.exe\" /quit'\n"
    assert old in t, "nsi: server quit anchor missing"
    t = t.replace(old, old + "  !insertmacro YwStopProcesses\n  Call YwFreeLocked\n", 1)
    # 注册输入法：/s 静默（不弹 Weasel 的安装选项框）；失败才要求重启（极端情况，完成页默认「稍后重启」）
    old = '  StrCpy $R2 "/i"\n'
    assert old in t
    t = t.replace(old, '  StrCpy $R2 "/s"\n', 1)
    old = "  ExecWait '\"$INSTDIR\\YiweiSetup.exe\" $R2'\n"
    assert old in t
    t = t.replace(old, "  ExecWait '\"$INSTDIR\\YiweiSetup.exe\" $R2' $R3\n  ${If} $R3 != 0\n    DetailPrint \"输入法注册未完成（$R3），重启电脑后生效\"\n    SetRebootFlag true\n  ${EndIf}\n", 1)
    old = '  ; Prompt reboot\n  StrCmp $0 "Upgrade" 0 +2\n  SetRebootFlag true\n'
    assert old in t, "nsi: upgrade reboot anchor missing"
    t = t.replace(old, "", 1)
    # 卸载：系统目录里仍被占用的 DLL 由 YiweiSetup 改名并在下次开机时删除，不需要立刻重启
    old = "  ; Prompt reboot\n  SetRebootFlag true\n\nSectionEnd"
    assert old in t, "nsi: uninstall reboot anchor missing"
    t = t.replace(old, "SectionEnd", 1)
    t = t.replace("  ExecWait 'taskkill /f /im YiweiHelper.exe'\n", "  nsExec::Exec 'taskkill /f /im YiweiHelper.exe'\n  Pop $9\n")
    assert "SetRebootFlag true\n" not in t.replace("    SetRebootFlag true\n  ${EndIf}", ""), "nsi: unexpected SetRebootFlag left"
    return t


if __name__ == "__main__":
    customize_weasel_yaml()
    install_rime_ice()
    customize_default_yaml()
    mount_into_schemas()
    patch_installer()
    print("data customised")
