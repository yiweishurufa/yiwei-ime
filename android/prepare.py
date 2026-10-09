#!/usr/bin/env python3
"""Turn a Trime (同文输入法) checkout into 一维输入法 for Android.

usage: python android/prepare.py <trime_dir> <rime-ice full.zip> <version>

* 雾凇拼音 + 一维 overlay (科技词库, 中英自动空格) replace Trime's bundled luna_pinyin data,
  patched by the same scripts/customize_data.py the Windows build uses, so both platforms type alike.
* 「墨线」 colour schemes (scripts/brand_schemes.py) become Trime's colour schemes; 青碧 is the default,
  with a dark twin used automatically in night mode.
* App id cc.yiwei.ime (installs beside Trime), name 一维输入法, ink-and-seal launcher icon.
"""
import os
import re
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(REPO, "scripts"))
sys.path.insert(0, os.path.join(REPO, "scripts", "design"))

import customize_data as cd  # noqa: E402
from brand_schemes import BRANDS, scheme, mix  # noqa: E402
sys.path.insert(0, HERE)
import skins  # noqa: E402
import layouts  # noqa: E402

T, ICE_ZIP, VERSION = sys.argv[1], sys.argv[2], sys.argv[3]
MAIN = os.path.join(T, "app", "src", "main")
SHARED = os.path.join(MAIN, "assets", "shared")
APP_ID = "cc.yiwei.ime"


def edit(path, fn):
    text = open(path, encoding="utf-8").read()
    new = fn(text)
    if new == text:
        raise SystemExit("%s: nothing changed" % path)
    open(path, "w", encoding="utf-8", newline="\n").write(new)


# ---------- data ----------

def install_data():
    keep = {"trime.yaml", "tongwenfeng.trime.yaml"}
    for fn in os.listdir(SHARED):
        if fn in keep:
            continue
        p = os.path.join(SHARED, fn)
        if os.path.isdir(p) and not os.path.islink(p):
            shutil.rmtree(p)
        else:
            os.remove(p)  # Trime ships these as symlinks into app/data/rime
    cd.DATA, cd.ICE_ZIP, cd.ANDROID = SHARED, ICE_ZIP, True
    cd.install_rime_ice()
    cd.customize_default_yaml()
    cd.mount_into_schemas()
    for fn in ("yiwei_stats.lua", "yiwei_toast.lua", "yiwei_autopair.lua"):  # Windows-helper features
        p = os.path.join(SHARED, "lua", fn)
        if os.path.exists(p):
            os.remove(p)
    shutil.rmtree(os.path.join(SHARED, "preview"), ignore_errors=True)


# ---------- colours, keyboards ----------

def hx(argb):
    """Trime colours: 0xRRGGBB, or 0xAARRGGBB when not opaque."""
    a = (argb >> 24) & 255
    return "0x%08x" % argb if a != 255 else "0x%06x" % (argb & 0xFFFFFF)


DEFAULT_SKIN = "yiwei_moblue"  # 墨蓝


def colour_block():
    lines = []
    for sid, name, c, dark in skins.all_skins():
        body = ["    name: \"%s\"" % name, "    author: 一维输入法"]
        body += ["    %s: %s" % (k, hx(v)) for k, v in c.items()]
        if dark:
            body.append("    dark_scheme: %s" % dark)
        lines.append("  %s:" % sid)
        lines += body
        if sid == DEFAULT_SKIN:  # Trime falls back to `default`
            lines.append("  default:")
            lines += body
    return "\n".join(lines) + "\n"


def set_style(t, key, value):
    pat = re.compile(r"^(  %s:)[ \t]*[^\n#]*" % re.escape(key), re.M)
    if pat.search(t):
        return pat.sub(lambda m: m.group(1) + " " + value + " ", t, count=1)
    return t.replace("\nstyle:\n", "\nstyle:\n  %s: %s\n" % (key, value), 1)


def install_theme():
    def patch(t):
        # our skins replace every stock colour scheme
        i = t.index("\npreset_color_schemes:\n") + len("\npreset_color_schemes:\n")
        m = re.compile(r"^\S", re.M).search(t, i)
        t = t[:i] + colour_block() + "\n" + t[m.start():]
        for k, v in layouts.STYLE.items():
            t = set_style(t, k, v)
        t, n = re.subn(r"\npreset_keys:\n", lambda m: m.group(0) + layouts.PRESET_KEYS.lstrip("\n"), t, count=1)
        if n != 1:
            raise SystemExit("trime.yaml: preset_keys not found")
        t, n = re.subn(r"\npreset_keyboards:\n", lambda m: m.group(0) + layouts.keyboards(), t, count=1)
        if n != 1:
            raise SystemExit("trime.yaml: preset_keyboards not found")
        # stock boards that our aliases replace
        for a in layouts.QWERTY_ALIASES:
            t = re.sub(r"\n  %s:\n(?:    [^\n]*\n|      [^\n]*\n|\n)*?(?=  \w+:\n|\Z)" % re.escape(a),
                       lambda m: "\n" if "import_preset: yw_qwerty" not in m.group(0) else m.group(0), t)
        t = t.rstrip("\n") + "\n" + layouts.TOOL_BAR
        t = re.sub(r"^name: .*$", "name: 一维", t, count=1, flags=re.M)
        t = re.sub(r"^author: .*$", "author: 一维输入法（基于同文 Trime 默认主题）", t, count=1, flags=re.M)
        return t
    edit(os.path.join(SHARED, "trime.yaml"), patch)


def install_reset_chinese():
    """像主流输入法一样：每次弹出键盘都是中文模式。"""
    import glob
    n = 0
    for f in glob.glob(os.path.join(SHARED, "*.schema.yaml")):
        s = open(f, encoding="utf-8").read()
        s2 = re.sub(r"(\n  - name: ascii_mode\n    states: [^\n]*\n)(?!    reset:)", r"\1    reset: 0\n", s, count=1)
        if s2 != s:
            open(f, "w", encoding="utf-8", newline="\n").write(s2); n += 1
    print("ascii_mode reset in %d schemas" % n)


def install_t9():
    """rime-ice's t9 schema targets Hamster / Yuanshu: drop their private processor, show pinyin instead of digits."""
    def patch(t):
        t, n = re.subn(r"\n    - t9_processor[^\n]*", "", t, count=1)
        if n != 1:
            raise SystemExit("t9.schema.yaml: t9_processor not found")
        t, n = re.subn(r"(\n    - uniquifier[^\n]*\n)", r"\1    - lua_filter@*t9_preedit  # 一维输入法：输入框显示拼音而不是数字\n", t, count=1)
        if n != 1:
            raise SystemExit("t9.schema.yaml: uniquifier not found")
        return t
    edit(os.path.join(SHARED, "t9.schema.yaml"), patch)


def install_feedback():
    """按键音（五套自合成音效）、震动默认开、键盘高度滑块。"""
    import sounds
    sounds.build(os.path.join(SHARED, "soundeffect"))
    J = os.path.join(MAIN, "java", "com", "osfans", "trime")

    # shipped sound packs live in the shared dir: copy them into the user dir where Trime looks
    def sem(t):
        old = 'return FileUtils.rename(old, dest.name).getOrDefault(dest.also { it.mkdirs() })'
        if old not in t:
            raise SystemExit("SoundEffectManager: userDir not found")
        return t.replace(old, """val dir = FileUtils.rename(old, dest.name).getOrDefault(dest.also { it.mkdirs() })
            // 一维输入法：把安装包自带的音效复制到用户目录（已存在的不覆盖）
            File(DataManager.sharedDataDir, "soundeffect").listFiles()?.forEach { src ->
                val target = File(dir, src.name)
                if (!target.exists()) src.copyRecursively(target, overwrite = false)
            }
            return dir""")
    edit(os.path.join(J, "data", "soundeffect", "SoundEffectManager.kt"), sem)

    def prefs(t):
        t = t.replace('const val SPLIT_SPACE_PERCENT = "keyboard_split_space"',
                      'const val SPLIT_SPACE_PERCENT = "keyboard_split_space"\n            const val KEYBOARD_HEIGHT_PERCENT = "yiwei_keyboard_height_percent"', 1)
        old = "        val useSoftCursor = switch("
        if old not in t:
            raise SystemExit("AppPrefs: useSoftCursor not found")
        t = t.replace(old, """        // 一维输入法：键盘高度（相对主题默认高度）
        val keyboardHeightPercent = int(
            R.string.yiwei_keyboard_height,
            KEYBOARD_HEIGHT_PERCENT,
            100,
            70,
            140,
            "%",
            5,
        )

""" + old, 1)
        # mainstream defaults: vibration on, custom sound pack selected (sound itself stays opt-in)
        t, n1 = re.subn(r"(val vibrateOnKeyPress = switch\(R\.string\.vibrate_on_key_press, VIBRATE_ON_KEY_PRESS, )false\)", r"\1true)", t)
        t, n2 = re.subn(r"(USE_CUSTOM_SOUND_EFFECT,\s*)false(,\s*\) \{ soundOnKeyPress)", r"\1true\2", t)
        t, n3 = re.subn(r'(CUSTOM_SOUND_EFFECT,\s*)""', r'\1"一维 · 清脆"', t)
        if (n1, n2, n3) != (1, 1, 1):
            raise SystemExit("AppPrefs: feedback defaults not found %r" % ((n1, n2, n3),))
        return t
    edit(os.path.join(J, "data", "prefs", "AppPrefs.kt"), prefs)

    def kb(t):
        old = """    val keyboardHeight: Int =
        intArrayOf(
            selfConfig?.let { getKeyboardHeightFromKeyboardConfig(it) } ?: 0,
            getKeyboardHeightFromTheme(theme),
        ).firstOrNull { it > 0 } ?: 0"""
        if old not in t:
            raise SystemExit("Keyboard.kt: keyboardHeight not found")
        new = old.replace("        intArrayOf(", "        (intArrayOf(").replace(
            ").firstOrNull { it > 0 } ?: 0",
            ").firstOrNull { it > 0 } ?: 0) *\n            // 一维输入法：用户在设置里调的高度比例\n"
            "            AppPrefs.defaultInstance().keyboard.keyboardHeightPercent.getValue() / 100")
        return t.replace(old, new)
    edit(os.path.join(J, "ime", "keyboard", "Keyboard.kt"), kb)

    def svc(t):
        old = "        prefs.keyboard.expandKeypressArea,\n"
        if old not in t:
            raise SystemExit("TrimeInputMethodService: recreateInputViewPrefs not found")
        return t.replace(old, old + "        prefs.keyboard.keyboardHeightPercent,\n", 1)
    edit(os.path.join(J, "ime", "core", "TrimeInputMethodService.kt"), svc)

    for d, s in (("values", "Keyboard height"), ("values-zh-rCN", "键盘高度"), ("values-zh-rTW", "鍵盤高度")):
        edit(os.path.join(MAIN, "res", d, "strings.xml"),
             lambda t, s=s: t.replace("</resources>", '    <string name="yiwei_keyboard_height">%s</string>\n</resources>' % s, 1))


def install_code():
    """Small Trime source patches: a key command to switch schema (九键 ⇄ 26 键)."""
    f = os.path.join(MAIN, "java", "com", "osfans", "trime", "ime", "keyboard", "CommonKeyboardActionListener.kt")

    def patch(t):
        t, n = re.subn(r'(\n(\s+)"select_candidate" -> handleSelectCandidate\(arg\)\n)',
                       r'\1\2"select_schema" -> handleSelectSchema(arg)\n', t, count=1)
        if n != 1:
            raise SystemExit("CommonKeyboardActionListener: select_candidate not found")
        fn = """
            // 一维输入法：select_schema 选项为 "a|b" 时在两个方案间来回切换（九键 ⇄ 26 键）
            private fun handleSelectSchema(arg: String) {
                val ids = arg.split('|').map { it.trim() }.filter { it.isNotEmpty() }
                if (ids.isEmpty()) return
                rime.launchOnReady { api ->
                    service.lifecycleScope.launch {
                        val current = api.statusCached.schemaId
                        val target = if (ids.size > 1 && current == ids[0]) ids[1] else ids[0]
                        api.selectSchema(target)
                    }
                }
            }
"""
        t, n = re.subn(r"(\n(\s+)private fun handleSelectCandidate\(arg: String\) \{)", fn.rstrip("\n") + r"\n\1", t, count=1)
        if n != 1:
            raise SystemExit("CommonKeyboardActionListener: handleSelectCandidate not found")
        return t
    edit(f, patch)


# ---------- brand ----------

def install_brand():
    gradle = os.path.join(T, "app", "build.gradle.kts")
    code = 20000000 + int(re.sub(r"\D", "", VERSION.split(".")[-1]) or 0)

    def g(t):
        t = t.replace('applicationId = "com.osfans.trime"', 'applicationId = "%s"' % APP_ID)
        t = re.sub(r'versionName = "[^"]*"', 'versionName = "%s"' % VERSION, t)
        t = re.sub(r"versionCode = \d+", "versionCode = %d" % code, t)
        t = t.replace('archivesName = "${android.defaultConfig.applicationId}-$buildVersionName"',
                      'archivesName = "yiwei-ime-android-%s"' % VERSION)
        return t
    edit(gradle, g)
    for d, rel, dbg in (("values", "Yiwei IME", "Yiwei IME (Debug)"), ("values-zh-rCN", "一维输入法", "一维输入法（调试）"),
                        ("values-zh-rTW", "一維輸入法", "一維輸入法（調試）")):
        p = os.path.join(MAIN, "res", d, "strings.xml")
        if os.path.exists(p):
            edit(p, lambda t: re.sub(r'(<string name="app_name_debug">)[^<]*', r"\g<1>" + dbg,
                                     re.sub(r'(<string name="app_name_release">)[^<]*', r"\g<1>" + rel, t)))
    icons()


def icons():
    import logo_min
    res = os.path.join(MAIN, "res")
    for dpi, px in (("mdpi", 48), ("hdpi", 72), ("xhdpi", 96), ("xxhdpi", 144), ("xxxhdpi", 192)):
        logo_min.tile(px).save(os.path.join(res, "mipmap-" + dpi, "ic_app_icon.png"))
        logo_min.tile(px, radius=0.5).save(os.path.join(res, "mipmap-" + dpi, "ic_app_icon_round.png"))
    edit(os.path.join(res, "values", "ic_app_icon_background.xml"),
         lambda t: re.sub(r"#[0-9A-Fa-f]{6}", "#%02X%02X%02X" % logo_min.BLUE, t, count=1))
    fg = os.path.join(res, "drawable", "ic_app_icon_foreground.xml")
    if os.path.exists(fg):
        os.remove(fg)
    d = os.path.join(res, "drawable-xxxhdpi"); os.makedirs(d, exist_ok=True)
    logo_min.adaptive_foreground(432).save(os.path.join(d, "ic_app_icon_foreground.png"))
    shutil.copyfile(os.path.join(res, "mipmap-xxxhdpi", "ic_app_icon.png"), os.path.join(MAIN, "ic_app_icon-playstore.png"))

if __name__ == "__main__":
    install_data()
    install_t9()
    install_reset_chinese()
    install_theme()
    install_code()
    install_feedback()
    install_brand()
    print("trime → 一维输入法 %s" % VERSION)
