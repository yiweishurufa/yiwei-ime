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
W = sys.argv[1]
ICE_ZIP = sys.argv[2]
DATA = os.path.join(W, "output", "data")

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
    "color_scheme": "yiwei_light", "color_scheme_dark": "yiwei_dark",
    "font_face": '"Microsoft YaHei UI"', "label_font_face": '"Segoe UI"', "comment_font_face": '"Microsoft YaHei UI"',
    "font_point": "14", "label_font_point": "11", "comment_font_point": "11",
    "horizontal": "true", "inline_preedit": "true", "label_format": '"%s"', "display_tray_icon": "true",
}
LAYOUT = {"border_width": "1", "margin_x": "10", "margin_y": "8", "spacing": "8", "candidate_spacing": "10",
          "hilite_spacing": "4", "hilite_padding": "4", "round_corner": "6", "corner_radius": "10",
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
    if "preset_color_schemes:" not in text:
        raise SystemExit("weasel.yaml: preset_color_schemes not found")
    text = text.replace("preset_color_schemes:\n", "preset_color_schemes:\n" + schemes, 1)
    text = text.replace("# Weasel settings", "# 一维输入法 前端设置（基于小狼毫 Weasel）", 1)
    open(path, "w", encoding="utf-8", newline="\n").write(text)


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


def mount_into_schemas():
    count = 0
    for fn in os.listdir(DATA):
        if not fn.endswith(".schema.yaml"):
            continue
        path = os.path.join(DATA, fn)
        text = open(path, encoding="utf-8").read()
        if "table_translator@custom_phrase" not in text:
            continue  # only full pinyin / double pinyin schemas of rime-ice
        text = re.sub(r"(\n  processors:\n)", r"\1    - lua_processor@*yiwei_stats  # 一维输入法：输入统计（默认关闭）\n    - lua_processor@*yiwei_toast  # 一维输入法：中/英切换提示\n", text, count=1)
        text = re.sub(r"(\n(\s+)- table_translator@custom_phrase[^\n]*\n)",
                      r"\1\2- table_translator@yiwei_tech     # 一维输入法：科技与互联网词库\n", text, count=1)
        text = text.rstrip("\n") + "\n" + TECH_BLOCK
        open(path, "w", encoding="utf-8", newline="\n").write(text)
        count += 1
    if count == 0:
        raise SystemExit("no rime-ice schema patched")
    print("patched %d schemas" % count)


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
'''

NSI_SHORTCUT = r'''  CreateShortCut "$SMPROGRAMS\$(DISPLAYNAME)\【一维输入法】设置.lnk" "$INSTDIR\YiweiHelper.exe" "/settings" "$INSTDIR\YiweiHelper.exe" 0
'''

NSI_UNINSTALL = r'''
  ExecWait 'taskkill /f /im YiweiHelper.exe'
  DeleteRegValue HKLM "Software\Microsoft\Windows\CurrentVersion\Run" "YiweiHelper"
  DeleteRegKey HKCR "yiwei-ime"
  RMDir /r "$INSTDIR\data\lua"
  RMDir /r "$INSTDIR\data\cn_dicts"
  RMDir /r "$INSTDIR\data\en_dicts"
  Delete "$INSTDIR\YiweiHelper.exe"
  Delete "$INSTDIR\YiweiHelper.exe.config"
'''


NSI_PAGES = r'''!define MUI_WELCOMEPAGE_TITLE "欢迎使用 一维输入法"
!define MUI_WELCOMEPAGE_TEXT "中文常新，自在表达。$\r$\n$\r$\n一维输入法基于 RIME / 小狼毫，词库默认用雾凇拼音，支持全拼与双拼、AI 翻译润色、常用语和多套配色。除非你主动使用 AI，所有输入都只在本机处理。$\r$\n$\r$\n点「下一步」开始安装，大约 1 分钟。本软件以 GPL-3.0 协议开源。"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE "装好了"
!define MUI_FINISHPAGE_TEXT "按 Win + 空格 切换到「一维输入法」就能打字。$\r$\n首次引导会帮你选拼音方案和外观，只要一分钟。"
!define MUI_FINISHPAGE_RUN "$INSTDIR\YiweiHelper.exe"
!define MUI_FINISHPAGE_RUN_PARAMETERS "/wizard"
!define MUI_FINISHPAGE_RUN_TEXT "立即体验（打开首次引导）"
!insertmacro MUI_PAGE_FINISH
'''


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
    t = t.replace("!define MUI_ICON ..\\resource\\weasel.ico\n",
                  "!define MUI_ICON ..\\resource\\weasel.ico\n!define MUI_UNICON ..\\resource\\weasel.ico\n"
                  "!define MUI_WELCOMEFINISHPAGE_BITMAP ..\\resource\\installer-welcome.bmp\n"
                  "!define MUI_UNWELCOMEFINISHPAGE_BITMAP ..\\resource\\installer-welcome.bmp\n"
                  "!define MUI_HEADERIMAGE\n!define MUI_HEADERIMAGE_RIGHT\n"
                  "!define MUI_HEADERIMAGE_BITMAP ..\\resource\\installer-header.bmp\n"
                  "!define MUI_ABORTWARNING\nBrandingText \"一维输入法\"\n", 1)
    # before upgrading, stop the running helper as well
    t = t.replace("  ExecWait '\"$R1\\YiweiServer.exe\" /quit'\n",
                  "  ExecWait '\"$R1\\YiweiServer.exe\" /quit'\n  ExecWait 'taskkill /f /im YiweiHelper.exe'\n", 1)
    out = t.replace("\n", nl)
    open(path, "wb").write((b"\xef\xbb\xbf" if bom else b"") + out.encode("utf-8"))


if __name__ == "__main__":
    customize_weasel_yaml()
    install_rime_ice()
    mount_into_schemas()
    patch_installer()
    print("data customised")
