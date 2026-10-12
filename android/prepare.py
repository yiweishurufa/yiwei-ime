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
        # preedit 小浮条（对标搜狗：键盘左上角、候选栏上方的圆角小条，不占输入框）
        for key, val in (("top_start_radius", "10"), ("top_end_radius", "10"), ("horizontal_padding", "10")):
            t, n = re.subn(r"(\npreedit:[^\n]*\n(?:[ \t#][^\n]*\n|\n)*?  %s:)[ \t]*[^\n#]*" % key, lambda m: m.group(1) + " " + val + " ", t, count=1)
            if n != 1:
                raise SystemExit("trime.yaml: preedit/%s not found" % key)
        t, n = re.subn(r"\n#  alpha: 0\.8[^\n]*", "\n  alpha: 1.0", t, count=1)
        if n != 1:
            raise SystemExit("trime.yaml: preedit alpha not found")
        t, n = re.subn(r"\npreset_keys:\n", lambda m: m.group(0) + layouts.PRESET_KEYS.lstrip("\n"), t, count=1)
        if n != 1:
            raise SystemExit("trime.yaml: preset_keys not found")
        t, n = re.subn(r"\npreset_keyboards:\n", lambda m: m.group(0) + layouts.keyboards(), t, count=1)
        if n != 1:
            raise SystemExit("trime.yaml: preset_keyboards not found")
        # stock boards that our aliases replace
        for a in layouts.QWERTY_ALIASES + layouts.OTHER_ALIASES:
            t = re.sub(r"\n  %s:\n(?:    [^\n]*\n|      [^\n]*\n|\n)*?(?=  \w+:\n|\Z)" % re.escape(a),
                       lambda m: "\n" if "import_preset: yw_" not in m.group(0) else m.group(0), t)
        # liquid keyboard: 「常用语」 tab first
        t, n = re.subn(r"(\n  keyboards: \[)emoji,", r"\1phrases, emoji,", t, count=1)
        t, n2 = re.subn(r"(\n  tabs:\n    name: 更多\n    type: TABS\n)", lambda m: m.group(1) + layouts.LIQUID_PHRASES, t, count=1)
        if (n, n2) != (1, 1):
            raise SystemExit("trime.yaml: liquid_keyboard keyboards/tabs not found %r" % ((n, n2),))
        # the stock tool_bar is replaced by ours
        m = re.search(r"^tool_bar:\n(?:[ \t#][^\n]*\n|\n)*", t, re.M)
        if m:
            t = t[:m.start()] + t[m.end():]
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
        # t9_preedit must be the FIRST filter: behind simplifier/uniquifier its preedit change does not reach the
        # composition and the input box keeps showing digits (verified with librime 1.17 + librime-lua)
        t, n = re.subn(r"(\n  filters:\n)", r"\1    - lua_filter@*t9_preedit  # 一维输入法：输入框显示拼音而不是数字\n", t, count=1)
        if n != 1:
            raise SystemExit("t9.schema.yaml: filters not found")
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
            60,
            160,
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
        # 一维输入法：主题高度 × 用户比例（设置滑块 / 键盘上拖动；拖动时用预览值），横竖屏各自封顶
        new = old.replace("        intArrayOf(", "        YiweiResize.scaled(\n            context,\n            intArrayOf(").replace(
            ").firstOrNull { it > 0 } ?: 0",
            ").firstOrNull { it > 0 } ?: 0,\n        )")
        return t.replace(old, new)
    edit(os.path.join(J, "ime", "keyboard", "Keyboard.kt"), kb)

    def svc(t):
        old = "        prefs.keyboard.expandKeypressArea,\n"
        if old not in t:
            raise SystemExit("TrimeInputMethodService: recreateInputViewPrefs not found")
        return t.replace(old, old + "        prefs.keyboard.keyboardHeightPercent,\n", 1)
    edit(os.path.join(J, "ime", "core", "TrimeInputMethodService.kt"), svc)

    # 拖动调整高度：覆盖层代码 + KeyboardWindow 暴露容器/当前高度 + keyboard_resize 命令
    shutil.copy(os.path.join(HERE, "kotlin", "YiweiResize.kt"), os.path.join(J, "ime", "keyboard", "YiweiResize.kt"))

    def kw(t):
        old = "    private lateinit var keyboardView: FrameLayout\n"
        old2 = "                add(it, lParams(matchParent, matchParent))\n"
        if old not in t or old2 not in t:
            raise SystemExit("KeyboardWindow: keyboardView / add not found")
        t = t.replace(old, old + """
    // 一维输入法：拖动调整高度（YiweiResize）用
    val yiweiContainer: FrameLayout get() = keyboardView
    val yiweiKeyboardHeight: Int get() = currentKeyboard?.keyboardHeight ?: 0
""", 1)
        return t.replace(old2, old2 + "                YiweiResize.raise()\n", 1)
    edit(os.path.join(J, "ime", "keyboard", "KeyboardWindow.kt"), kw)

    # 九键按「中/英」：以前只切 ascii_mode，键盘还是九键，按键打出来全是数字。
    # 现在九键方案下切到英文就换成 26 键字母键盘（保持英文），切回中文回到九键。
    def kw_t9(t):
        old = "            val targetMode = if (it.resetAsciiMode) it.asciiMode else it.lastAsciiMode\n"
        old2 = "    override fun onRimeOptionUpdated(value: RimeMessage.OptionMessage.Data) {\n        val option = value.option\n"
        if old not in t or old2 not in t:
            raise SystemExit("KeyboardWindow: targetMode / onRimeOptionUpdated not found")
        t = t.replace(old, "            val targetMode = yiweiForceAscii.also { yiweiForceAscii = null }\n"
                           "                ?: (if (it.resetAsciiMode) it.asciiMode else it.lastAsciiMode)\n", 1)
        return t.replace(old2, """    // 一维输入法：九键方案的英文用 26 键字母键盘
    private var yiweiForceAscii: Boolean? = null

    private fun yiweiT9Ascii(on: Boolean) {
        if (rime.run { statusCached }.schemaId != "t9") return
        val target = when {
            on && currentKeyboardId == "t9" -> "yw_qwerty"
            !on && currentKeyboardId == "yw_qwerty" -> "t9"
            else -> return
        }
        ContextCompat.getMainExecutor(service).execute {
            if (currentKeyboardId == target) return@execute
            detachCurrentView()
            yiweiForceAscii = on
            attachKeyboard(target)
        }
    }

""" + old2 + "        if (option == \"ascii_mode\") yiweiT9Ascii(value.value)\n", 1)
    edit(os.path.join(J, "ime", "keyboard", "KeyboardWindow.kt"), kw_t9)

    def ckal(t):
        t, n = re.subn(r'(\n(\s+)"menu_keyboard" -> )', r'\n\2"keyboard_resize" -> YiweiResize.toggle(keyboardWindow)\n\2"clear_composition" -> rime.launchOnReady { api -> service.lifecycleScope.launch { if (api.statusCached.isComposing) api.clearComposition() } }\1', t, count=1)
        if n != 1:
            raise SystemExit("CommonKeyboardActionListener: menu_keyboard not found")
        return t
    edit(os.path.join(J, "ime", "keyboard", "CommonKeyboardActionListener.kt"), ckal)

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


def install_toolbox():
    """工具栏可左右滑动 + 点 Logo 打开工具箱（YiweiToolboxWindow，条目由 layouts.TOOLBOX 生成）。"""
    J = os.path.join(MAIN, "java", "com", "osfans", "trime", "ime")
    src = open(os.path.join(HERE, "kotlin", "YiweiToolbox.kt"), encoding="utf-8").read()
    def gen(lst):
        return ",\n".join('        YiweiGridWindow.Item("%s", "%s", "%s")' % t for t in lst)
    for marker in ("/*YIWEI_TOOLBOX*/", "/*YIWEI_KEYBOARDS*/"):
        if marker not in src:
            raise SystemExit("YiweiToolbox.kt: %s missing" % marker)
    src = src.replace("/*YIWEI_TOOLBOX*/", gen(layouts.TOOLBOX)).replace("/*YIWEI_KEYBOARDS*/", gen(layouts.KEYBOARDS))
    open(os.path.join(J, "keyboard", "YiweiToolbox.kt"), "w", encoding="utf-8", newline="\n").write(src)

    def ckal(t):
        old = '"menu_keyboard" -> windowManager.attachWindow(SwitchOptionWindow())'
        if old not in t:
            raise SystemExit("CommonKeyboardActionListener: menu_keyboard handler not found")
        return t.replace(old, '"yiwei_toolbox" -> windowManager.attachWindow(YiweiGridWindow.toolbox())\n'
                         '                    "yiwei_kbdchooser" -> windowManager.attachWindow(YiweiGridWindow.keyboards())\n                    ' + old, 1)
    edit(os.path.join(J, "keyboard", "CommonKeyboardActionListener.kt"), ckal)

    shutil.copy(os.path.join(HERE, "kotlin", "YiweiToolbarOrder.kt"), os.path.join(J, "keyboard", "YiweiToolbarOrder.kt"))

    def svc(t):
        a = "    private fun replaceCandidateView(theme: Theme): CandidatesView {\n"
        if a not in t:
            raise SystemExit("TrimeInputMethodService: replaceCandidateView not found")
        return t.replace(a, "    // 一维输入法：工具栏顺序改了以后重建键盘界面\n"
                            "    fun yiweiRebuildInputView() {\n        replaceInputView(ThemeManager.activeTheme)\n    }\n\n" + a, 1)
    edit(os.path.join(J, "core", "TrimeInputMethodService.kt"), svc)

    def bar(t):
        a = "        val buttons = theme.toolBar.buttons\n"
        if a not in t:
            raise SystemExit("ButtonsBarUi: buttons list not found")
        t = t.replace(a, "        val buttons = com.osfans.trime.ime.keyboard.YiweiToolbarOrder.arrange(ctx, theme.toolBar.buttons)\n", 1)
        # buttons in natural left-to-right order, inside a horizontal scroller
        t = t.replace("flexDirection = FlexDirection.ROW_REVERSE", "flexDirection = FlexDirection.ROW", 1)
        old = "    val firstButton: ToolButton?\n"
        if old not in t:
            raise SystemExit("ButtonsBarUi: firstButton not found")
        t = t.replace(old, """    // 一维输入法：工具栏可以左右滑动
    val scroller = android.widget.HorizontalScrollView(ctx).apply {
        isHorizontalScrollBarEnabled = false
        overScrollMode = android.view.View.OVER_SCROLL_NEVER
        isFillViewport = true
        addView(
            root,
            android.view.ViewGroup.LayoutParams(
                android.view.ViewGroup.LayoutParams.WRAP_CONTENT,
                android.view.ViewGroup.LayoutParams.MATCH_PARENT,
            ),
        )
    }

""" + old, 1)
        return t
    edit(os.path.join(J, "bar", "ui", "ButtonsBarUi.kt"), bar)

    def always(t):
        old = "            add(buttonsUi.root, lParams(matchParent, matchParent))\n"
        if old not in t:
            raise SystemExit("AlwaysUi: buttonsUi.root add not found")
        return t.replace(old, "            add(buttonsUi.scroller, lParams(matchParent, matchParent))\n", 1)
    edit(os.path.join(J, "bar", "ui", "AlwaysUi.kt"), always)


def install_oppo():
    """OPPO / ColorOS 适配 + 大屏适配（YiweiOppo.kt）：后台保活向导、字体放大上限、折叠屏/平板限宽、边缘手势排除、
    线性马达触感（系统键盘触感 / 轻 / 中 / 重 四档预设波形）。"""
    J = os.path.join(MAIN, "java", "com", "osfans", "trime")
    shutil.copy(os.path.join(HERE, "kotlin", "YiweiOppo.kt"), os.path.join(J, "ime", "keyboard", "YiweiOppo.kt"))
    Y = "com.osfans.trime.ime.keyboard.YiweiOppo"

    def ckal(t):
        old = '"yiwei_toolbox" -> windowManager.attachWindow(YiweiGridWindow.toolbox())'
        if old not in t:
            raise SystemExit("CommonKeyboardActionListener: yiwei_toolbox handler not found (install_toolbox first)")
        return t.replace(old, old + '\n                    "yiwei_keepalive" -> %s.keepAlive(service)' % Y, 1)
    edit(os.path.join(J, "ime", "keyboard", "CommonKeyboardActionListener.kt"), ckal)

    def svc(t):
        a = "    override fun onCreate() {\n        rime = RimeDaemon.createSession(javaClass.name)\n"
        b = "    override fun onConfigurationChanged(newConfig: Configuration) {\n"
        if a not in t or b not in t:
            raise SystemExit("TrimeInputMethodService: onCreate / onConfigurationChanged anchors not found")
        t = t.replace(a, "    override fun onCreate() {\n        %s.capFontScale(this)\n        rime = RimeDaemon.createSession(javaClass.name)\n" % Y, 1)
        return t.replace(b, b + "        %s.capFontScale(this)\n" % Y, 1)
    edit(os.path.join(J, "ime", "core", "TrimeInputMethodService.kt"), svc)

    def iv(t):
        a = "                if (context.isLandscapeMode()) keyboardSidePaddingLandscape else keyboardSidePadding\n            return dp(value)\n"
        if a not in t:
            raise SystemExit("InputView: keyboardSidePaddingPx not found")
        t = t.replace(a, "                if (context.isLandscapeMode()) keyboardSidePaddingLandscape else keyboardSidePadding\n"
                         "            return %s.extraSidePadding(context, dp(value))\n" % Y, 1)
        b = "        updateKeyboardSize()\n\n        add(\n            preedit.ui.root,"
        if b not in t:
            raise SystemExit("InputView: init updateKeyboardSize anchor not found")
        return t.replace(b, "        updateKeyboardSize()\n        %s.excludeEdgeGestures(keyboardView)\n\n        add(\n            preedit.ui.root," % Y, 1)
    edit(os.path.join(J, "ime", "core", "InputView.kt"), iv)

    def prefs(t):
        a = '            const val VIBRATION_AMPLITUDE = "vibration_amplitude"'
        b = "        val speakOnKeyPress = switch("
        if a not in t or b not in t:
            raise SystemExit("AppPrefs: vibration anchors not found")
        t = t.replace(a, a + '\n            const val YIWEI_HAPTIC_STYLE = "yiwei_haptic_style"', 1)
        return t.replace(b, """        // 一维输入法：线性马达触感（0 系统键盘触感，1 轻 TICK，2 中 CLICK，3 重 HEAVY_CLICK）
        val yiweiHapticStyle = int(
            R.string.yiwei_haptic_style,
            YIWEI_HAPTIC_STYLE,
            0,
            0,
            3,
            "",
            defaultLabel = R.string.system_default,
        ) { vibrateOnKeyPress.getValue() }

""" + b, 1)
    edit(os.path.join(J, "data", "prefs", "AppPrefs.kt"), prefs)

    def fb(t):
        a = "        if (duration != 0L) { // use vibrator\n"
        if a not in t:
            raise SystemExit("InputFeedbackManager: vibrator branch not found")
        return t.replace(a, """        // 一维输入法：系统预设波形（OPPO/一加线性马达按这些预设调过，比方波「嗡」一下清脆得多）
        val yiweiStyle = keyboardPrefs.yiweiHapticStyle.getValue()
        if (yiweiStyle != 0 && Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            val effect = when {
                longPress -> VibrationEffect.EFFECT_HEAVY_CLICK
                yiweiStyle == 1 -> VibrationEffect.EFFECT_TICK
                yiweiStyle == 2 -> VibrationEffect.EFFECT_CLICK
                else -> VibrationEffect.EFFECT_HEAVY_CLICK
            }
            vibrator.vibrate(VibrationEffect.createPredefined(effect))
            return
        }

""" + a, 1)
    edit(os.path.join(J, "ime", "keyboard", "InputFeedbackManager.kt"), fb)

    for d, s in (("values", "Key vibration feel (0 system, 1 light, 2 medium, 3 strong)"),
                 ("values-zh-rCN", "按键触感（0 系统，1 轻，2 中，3 重）"),
                 ("values-zh-rTW", "按鍵觸感（0 系統，1 輕，2 中，3 重）")):
        edit(os.path.join(MAIN, "res", d, "strings.xml"),
             lambda t, s=s: t.replace("</resources>", '    <string name="yiwei_haptic_style">%s</string>\n</resources>' % s, 1))


def install_sidecolumn():
    """九键左侧竖列：标点 / 打字时的拼音选择列（YiweiSideColumn.kt）。"""
    J = os.path.join(MAIN, "java", "com", "osfans", "trime", "ime")
    Y = "com.osfans.trime.ime.keyboard.YiweiSideColumn"
    shutil.copy(os.path.join(HERE, "kotlin", "YiweiSideColumn.kt"), os.path.join(J, "keyboard", "YiweiSideColumn.kt"))

    def cand(t):
        a = "        val (total, highlighted, candidates) = data\n"
        b = "rime.launchOnReady { it.selectCandidate(position, global = true) }"
        c = "inputView.showCandidateActionMenu(position, items[position].text, view, global = true)"
        if a not in t or b not in t or c not in t:
            raise SystemExit("CompactCandidateDelegate: anchors not found")
        t = t.replace(a, "        %s.remember(data)\n        %s.setRefreshHook { onCandidateListUpdate(it) }\n"
                         "        val (total, highlighted, candidates) = %s.process(data)\n" % (Y, Y, Y), 1)
        t = t.replace(b, "val real = %s.realIndex(position)\n                rime.launchOnReady { it.selectCandidate(real, global = true) }" % Y, 1)
        return t.replace(c, "inputView.showCandidateActionMenu(%s.peekIndex(position), items[position].text, view, global = true)" % Y, 1)
    edit(os.path.join(J, "candidates", "compact", "CompactCandidateDelegate.kt"), cand)

    def kw(t):
        a = "                add(it, lParams(matchParent, matchParent))\n                YiweiResize.raise()\n"
        if a not in t:
            raise SystemExit("KeyboardWindow: attach anchor not found (install_feedback first)")
        return t.replace(a, a + "                YiweiSideColumn.onKeyboard(this, target, keyboard.keyboardHeight) { p -> service.commitText(p) }\n", 1)
    edit(os.path.join(J, "keyboard", "KeyboardWindow.kt"), kw)

    def ckal(t):
        a = "            private fun handleDefaultKeyAction(action: KeyAction) {\n"
        if a not in t:
            raise SystemExit("CommonKeyboardActionListener: handleDefaultKeyAction not found")
        return t.replace(a, a + """                // 一维输入法：选了拼音（九键左列）后，空格上屏过滤后的第一个候选
                if (action.code == KeyEvent.KEYCODE_SPACE) {
                    val idx = %s.filteredFirst()
                    if (idx != null) {
                        rime.launchOnReady { it.selectCandidate(idx, global = true) }
                        return
                    }
                }
""" % Y, 1)
    edit(os.path.join(J, "keyboard", "CommonKeyboardActionListener.kt"), ckal)


def install_sogou_ui():
    """搜狗式界面余项：候选栏首选加粗 + 横滑、符号面板左侧分类竖栏、夜间模式手动开关、单手模式、长辈模式（大字）、
    输入设置面板（模糊音 / Emoji / 自定义标点 / 学习强度与清除，移植 Windows 一维助手）。"""
    J = os.path.join(MAIN, "java", "com", "osfans", "trime")
    K = os.path.join(J, "ime", "keyboard")
    for f in ("YiweiModes.kt", "YiweiSymbolSide.kt", "YiweiInputPrefs.kt"):
        shutil.copy(os.path.join(HERE, "kotlin", f), os.path.join(K, f))
    Y = "com.osfans.trime.ime.keyboard.YiweiModes"

    def must(t, a, what):
        if a not in t:
            raise SystemExit("%s not found" % what)

    # --- 设置项 ---
    def prefs(t):
        a = "        val useSoftCursor = switch("
        must(t, a, "AppPrefs: useSoftCursor")
        return t.replace(a, """        // 一维输入法：夜间模式手动开关（0 跟随系统 1 深色 2 浅色）、单手模式（0 关 1 左 2 右）、长辈模式、候选栏横滑
        val yiweiNightMode = int("yiwei_night_mode", 0)
        val yiweiOneHand = int("yiwei_one_hand", 0)
        val yiweiElder = switch(R.string.yiwei_elder, "yiwei_elder", false)
        val yiweiCandidateScroll = switch(R.string.yiwei_candidate_scroll, "yiwei_candidate_scroll", true)

""" + a, 1)
    edit(os.path.join(J, "data", "prefs", "AppPrefs.kt"), prefs)
    for d, e, c in (("values", "Elder mode (large text)", "Scrollable candidate bar"),
                    ("values-zh-rCN", "长辈模式（大字）", "候选栏左右滑动"),
                    ("values-zh-rTW", "長輩模式（大字）", "候選欄左右滑動")):
        edit(os.path.join(MAIN, "res", d, "strings.xml"),
             lambda t, e=e, c=c: t.replace("</resources>", '    <string name="yiwei_elder">%s</string>\n'
                                           '    <string name="yiwei_candidate_scroll">%s</string>\n</resources>' % (e, c), 1))

    def svc(t):
        a = "        prefs.keyboard.keyboardHeightPercent,\n"
        must(t, a, "TrimeInputMethodService: keyboardHeightPercent in recreateInputViewPrefs (install_feedback first)")
        return t.replace(a, a + "        prefs.keyboard.yiweiOneHand,\n        prefs.keyboard.yiweiElder,\n"
                            "        prefs.keyboard.yiweiCandidateScroll,\n", 1)
    edit(os.path.join(J, "ime", "core", "TrimeInputMethodService.kt"), svc)

    # --- 工具箱命令 ---
    def ckal(t):
        a = '"yiwei_toolbox" -> windowManager.attachWindow(YiweiGridWindow.toolbox())'
        must(t, a, "CommonKeyboardActionListener: yiwei_toolbox (install_toolbox first)")
        return t.replace(a, a + '\n                    "yiwei_night" -> %s.toggleNight(service)'
                                '\n                    "yiwei_onehand" -> %s.toggleOneHand(service)'
                                '\n                    "yiwei_elder" -> %s.toggleElder(service)'
                                '\n                    "yiwei_inputprefs" -> windowManager.attachWindow(YiweiInputPrefsWindow())' % (Y, Y, Y), 1)
    edit(os.path.join(K, "CommonKeyboardActionListener.kt"), ckal)

    # --- 夜间模式：ColorManager 按手动开关选深/浅配色 ---
    def cm(t):
        a = "    private fun evaluateActiveColorScheme(): ColorScheme = when {\n"
        must(t, a, "ColorManager: evaluateActiveColorScheme")
        return t.replace(a, """    // 一维输入法：工具箱「夜间模式」手动开关
    fun yiweiRefresh() {
        activeColorScheme = evaluateActiveColorScheme()
    }

    private fun evaluateActiveColorScheme(): ColorScheme {
        val override = %s.nightOverride()
        return yiweiEvaluate(%s.effectiveNight(isNightMode), followSystemDayNight || override != 0)
    }

    private fun yiweiEvaluate(isNightMode: Boolean, followSystemDayNight: Boolean): ColorScheme = when {
""" % (Y, Y), 1)
    edit(os.path.join(J, "data", "theme", "ColorManager.kt"), cm)

    # --- 单手模式：键盘一侧留白 ---
    def iv(t):
        a = "        val sidePadding = keyboardSidePaddingPx\n        val unset = LayoutParams.UNSET\n        if (sidePadding == 0) {\n"
        b = "            leftPaddingSpace.updateLayoutParams {\n                width = sidePadding\n            }\n            rightPaddingSpace.updateLayoutParams {\n                width = sidePadding\n            }\n"
        must(t, a, "InputView: updateKeyboardSize head")
        must(t, b, "InputView: side widths")
        t = t.replace(a, "        val sidePadding = keyboardSidePaddingPx\n        val unset = LayoutParams.UNSET\n"
                         "        val ywOneHand = %s.oneHandExtra(context)\n"
                         "        if (sidePadding == 0 && ywOneHand.first == 0 && ywOneHand.second == 0) {\n" % Y, 1)
        return t.replace(b, "            leftPaddingSpace.updateLayoutParams {\n                width = (sidePadding + ywOneHand.first).coerceAtLeast(1)\n            }\n"
                            "            rightPaddingSpace.updateLayoutParams {\n                width = (sidePadding + ywOneHand.second).coerceAtLeast(1)\n            }\n"
                            "            %s.bindBlankSides(leftPaddingSpace, rightPaddingSpace)\n" % Y, 1)
    edit(os.path.join(J, "ime", "core", "InputView.kt"), iv)

    # --- 长辈模式：按键 / 候选文字放大，键盘加高 ---
    def kv(t):
        a = "    internal val keyTextSize = theme.generalStyle.keyTextSize\n"
        b = "    internal val keyLongTextSize = theme.generalStyle.keyLongTextSize.takeIf { it > 0 } ?: keyTextSize\n"
        must(t, a + b, "KeyboardView: text sizes")
        return t.replace(a + b, "    internal val keyTextSize = theme.generalStyle.keyTextSize * %s.textScale()\n"
                                "    internal val keyLongTextSize = (theme.generalStyle.keyLongTextSize.takeIf { it > 0 } ?: theme.generalStyle.keyTextSize) * %s.textScale()\n" % (Y, Y), 1)
    edit(os.path.join(K, "KeyboardView.kt"), kv)

    def rz(t):
        a = "        val h = base * percent() / 100\n"
        must(t, a, "YiweiResize: scaled")
        return t.replace(a, "        val h = (base * percent() / 100 * YiweiModes.heightScale()).toInt()\n", 1)
    edit(os.path.join(K, "YiweiResize.kt"), rz)

    # --- 候选栏：首选加粗（一维蓝由配色 hilited_candidate_text_color 给） ---
    def ci(t):
        a = "        text.text = item.text\n        text.setTextColor(tColor)\n"
        b = "    private val textFont = FontManager.getTypeface(\"candidate_font\")\n"
        c = "    private val textSize = theme.generalStyle.candidateTextSize\n"
        for x, w in ((a, "update"), (b, "textFont"), (c, "textSize")):
            must(t, x, "CandidateItemUi: " + w)
        t = t.replace(c, "    private val textSize = theme.generalStyle.candidateTextSize * %s.textScale()\n" % Y, 1)
        t = t.replace(b, b + "    private val yiweiBoldFont = android.graphics.Typeface.create(textFont, android.graphics.Typeface.BOLD)\n", 1)
        return t.replace(a, a + "        text.typeface = if (highlighted) yiweiBoldFont else textFont\n", 1)
    edit(os.path.join(J, "ime", "candidates", "CandidateItemUi.kt"), ci)

    # --- 候选栏横滑：一行排开 16 个候选可左右滑，⌄ 展开从第 1 个起的全部候选 ---
    def cd(t):
        a = "            layoutManager = this@CompactCandidateDelegate.layoutManager\n"
        b = "    private val separatorDrawable by lazy {\n"
        c = "        adapter.updateCandidates(candidates, total, highlighted)\n"
        for x, w in ((a, "layoutManager"), (b, "separatorDrawable"), (c, "updateCandidates")):
            must(t, x, "CompactCandidateDelegate: " + w)
        t = t.replace(a, "            layoutManager = if (yiweiScroll) yiweiLinear else this@CompactCandidateDelegate.layoutManager\n", 1)
        t = t.replace(b, """    // 一维输入法：候选栏左右滑动（像搜狗）
    private val yiweiScroll = AppPrefs.defaultInstance().keyboard.yiweiCandidateScroll.getValue()

    private val yiweiLinear by lazy {
        object : androidx.recyclerview.widget.LinearLayoutManager(context, RecyclerView.HORIZONTAL, false) {
            override fun onLayoutCompleted(state: RecyclerView.State?) {
                super.onLayoutCompleted(state)
                // 展开面板从第一个候选开始列全部；没有候选时收起按钮
                _unrolledCandidateOffset.tryEmit(0)
                bar.unrollButtonStateMachine.push(
                    UnrollButtonStateMachine.TransitionEvent.UnrolledCandidatesUpdated,
                    UnrollButtonStateMachine.BooleanKey.UnrolledCandidatesEmpty to (adapter.itemCount == 0),
                )
            }
        }
    }

""" + b, 1)
        return t.replace(c, c + "        if (yiweiScroll) view.scrollToPosition(0)\n", 1)
    edit(os.path.join(J, "ime", "candidates", "compact", "CompactCandidateDelegate.kt"), cd)

    def deco(t):
        a = "        val layoutManager = parent.layoutManager as FlexboxLayoutManager\n"
        b = "            val lp = view.layoutParams as FlexboxLayoutManager.LayoutParams\n"
        must(t, a + "        for", "FlexboxVerticalDecoration: layoutManager cast")
        must(t, b, "FlexboxVerticalDecoration: lp cast")
        return t.replace(a, "        val layoutManager = parent.layoutManager ?: return\n", 1).replace(
            b, "            val lp = view.layoutParams as android.view.ViewGroup.MarginLayoutParams\n", 1)
    edit(os.path.join(J, "ime", "candidates", "unrolled", "decoration", "FlexboxVerticalDecoration.kt"), deco)

    # --- 符号面板：左侧分类竖栏 + 底部返回，工具栏位置显示当前分类名 ---
    def lw(t):
        a = "    override fun onCreateView(): View = LiquidLayout(context, theme, commonKeyboardActionListener).apply {\n"
        b = "    }\n\n    override fun onCreateBarView() = liquidLayout.tabsUi.root\n"
        c = "        liquidLayout.tabsUi.activateTab(i)\n"
        d = "    private lateinit var liquidLayout: LiquidLayout\n"
        for x, w in ((a, "onCreateView"), (b, "onCreateBarView"), (c, "activateTab"), (d, "liquidLayout")):
            must(t, x, "LiquidWindow: " + w)
        t = t.replace(d, d + """
    // 一维输入法：左侧分类竖栏（YiweiSymbolSide）
    private val yiweiSide = com.osfans.trime.ime.keyboard.YiweiSymbolSide(
        onPick = { i -> setDataByIndex(i) },
        onBack = { windowManager.attachWindow(KeyboardWindow) },
    )
""", 1)
        t = t.replace(a, "    override fun onCreateView(): View = yiweiSide.wrap(\n        context,\n        LiquidData.getTagList().map { it.label },\n"
                         "        LiquidLayout(context, theme, commonKeyboardActionListener).apply {\n", 1)
        t = t.replace(b, "    },\n    )\n\n    override fun onCreateBarView() = yiweiSide.barView(context)\n", 1)
        return t.replace(c, c + "        yiweiSide.activate(i)\n", 1)
    edit(os.path.join(J, "ime", "symbol", "LiquidWindow.kt"), lw)


def install_safe_upgrade():
    """Upgrades: the APK ships dictionaries precompiled with Trime's own librime (assets/shared/build).
    Compiled files an older version left in the user build dir would shadow them (librime looks there first),
    so whenever the shipped data changes, drop user-dir copies of the shipped *.bin files."""
    f = os.path.join(MAIN, "java", "com", "osfans", "trime", "data", "base", "DataManager.kt")

    def patch(t):
        old = "        DataDiff.diff(oldChecksums, newChecksums).sortedByDescending { it.ordinal }.forEach {"
        if old not in t:
            raise SystemExit("DataManager: DataDiff loop not found")
        t = t.replace(old, "        val diffs = DataDiff.diff(oldChecksums, newChecksums).sortedByDescending { it.ordinal }\n"
                           "        diffs.forEach {", 1)
        old2 = "        ResourceUtils.copyFile(DATA_CHECKSUMS_NAME, dataDir.resolve(DATA_CHECKSUMS_NAME).absolutePath)"
        if old2 not in t:
            raise SystemExit("DataManager: checksums copy not found")
        return t.replace(old2, """        // 一维输入法：词库随安装包预编译好了；数据有更新时删掉用户目录里旧版本留下的同名编译文件，免得旧文件盖住新词库
        if (diffs.isNotEmpty()) {
            prebuiltDataDir.listFiles()?.filter { it.name.endsWith(".bin") }?.forEach { shipped ->
                val stale = File(stagingDir, shipped.name)
                if (stale.exists() && stale.delete()) Timber.i("removed stale ${stale.name}")
            }
        }
""" + old2, 1)
    edit(f, patch)


# What Trime writes to the user dir as default.custom.yaml on first start. Stock Trime writes a schema_list of
# luna_pinyin + luna_pinyin_simp, which this app does not ship: the start-up deployment then skips rime_ice and
# t9 entirely. Ours keeps default.yaml's list. The CI gate copies the same file into its user dir.
TRIME_DEFAULT_CUSTOM = """# 一维输入法：方案列表用 default.yaml 里的（雾凇拼音、九键、双拼）
patch:
  {}
"""


def install_default_custom():
    f = os.path.join(MAIN, "java", "com", "osfans", "trime", "data", "base", "DataManager.kt")

    def patch(t):
        t, n = re.subn(r'private const val SCHEMA_LIST_CUSTOM_PATCH = """.*?"""',
                       lambda m: 'private const val SCHEMA_LIST_CUSTOM_PATCH = """\n' + TRIME_DEFAULT_CUSTOM + '"""',
                       t, count=1, flags=re.S)
        if n != 1:
            raise SystemExit("DataManager: SCHEMA_LIST_CUSTOM_PATCH not found")
        old = "        if (!custom.exists()) {"
        if old not in t:
            raise SystemExit("DataManager: default.custom creation not found")
        t = t.replace(old, """        // 一维输入法：旧版本写过同文默认的 luna_pinyin 方案列表（本应用没有这些方案），改回一维的
        if (custom.exists() && custom.readText().let { it.contains("luna_pinyin") && !it.contains("rime_ice") }) {
            custom.delete()
        }
""" + old, 1)
        return t
    edit(f, patch)
    with open(os.path.join(T, "yiwei-default.custom.yaml"), "w", encoding="utf-8", newline="\n") as fp:
        fp.write(TRIME_DEFAULT_CUSTOM)


def install_preedit():
    """preedit 浮条四角都圆（原版只有上面两个角）。"""
    f = os.path.join(MAIN, "java", "com", "osfans", "trime", "ime", "composition", "PreeditDelegate.kt")

    def patch(t):
        a = "floatArrayOf(startRadius, startRadius, endRadius, endRadius, 0f, 0f, 0f, 0f)"
        b = "floatArrayOf(endRadius, endRadius, startRadius, startRadius, 0f, 0f, 0f, 0f)"
        if a not in t or b not in t:
            raise SystemExit("PreeditDelegate: radii not found")
        return (t.replace(a, "floatArrayOf(startRadius, startRadius, endRadius, endRadius, endRadius, endRadius, startRadius, startRadius)", 1)
                 .replace(b, "floatArrayOf(endRadius, endRadius, startRadius, startRadius, startRadius, startRadius, endRadius, endRadius)", 1))
    edit(f, patch)


def install_safe_toolbar():
    """Tool-bar buttons: run only actions defined in preset_keys (or a real key name); an unknown action does
    nothing instead of being typed out as text (stock Trime turns an unknown token into a key sequence)."""
    f = os.path.join(MAIN, "java", "com", "osfans", "trime", "ime", "bar", "InputBarDelegate.kt")

    def patch(t):
        old = """            if (action.isNotEmpty()) {
                commonKeyboardActionListener.listener.onAction(KeyActionManager.getAction(action))
            } else {"""
        if old not in t:
            raise SystemExit("InputBarDelegate: tool bar action handler not found")
        return t.replace(old, """            if (action.isNotEmpty()) {
                // 一维输入法：只执行定义过的按键；没有定义的动作什么也不做，绝不把名字当文字上屏
                if (theme.presetKeys.containsKey(action) ||
                    com.osfans.trime.ime.keyboard.KeyCode.parse(action).first != 0
                ) {
                    commonKeyboardActionListener.listener.onAction(KeyActionManager.getAction(action))
                } else {
                    timber.log.Timber.w("tool bar: undefined action '%s' ignored", action)
                }
            } else {""", 1)
    edit(f, patch)


def check_keys():
    """Fail the build if one of our keys or tool-bar buttons points at an action that is not defined:
    Trime would type its name out as text."""
    import yaml
    y = yaml.safe_load(open(os.path.join(SHARED, "trime.yaml"), encoding="utf-8"))
    presets = set((y.get("preset_keys") or {}).keys())
    bad = []
    tb = y.get("tool_bar") or {}
    for b in [tb.get("primary_button") or {}] + list(tb.get("buttons") or []):
        for k in ("action", "long_press_action"):
            a = b.get(k)
            if a and a not in presets:
                bad.append("tool_bar %s: %s" % (k, a))
    for kb in layouts.OUR_KEYBOARDS:
        for key in (y["preset_keyboards"][kb].get("keys") or []):
            for k in ("click", "long_click", "swipe_up", "swipe_down", "swipe_left", "swipe_right", "composing", "has_menu", "paging"):
                a = key.get(k)
                if isinstance(a, str) and len(a) > 1 and re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", a) and a not in presets \
                        and not re.fullmatch(r"KP_\d", a):  # keypad digits are real key names
                    bad.append("%s %s: %s" % (kb, k, a))
    # 每个键盘每一行的宽度加起来必须是 100（否则最后一键会掉到下一行/留空）
    for kb in layouts.OUR_KEYBOARDS:
        spec = y["preset_keyboards"][kb]
        row, w0 = 0.0, float(spec.get("width", 10))
        for key in spec.get("keys") or []:
            w = float(key.get("width", w0))
            if row + w > 100.001:
                bad.append("%s: row width %.1f != 100" % (kb, row))
                row = 0.0
            row += w
            if abs(row - 100) < 0.001:
                row = 0.0
        if row:
            bad.append("%s: last row width %.1f != 100" % (kb, row))
    # 拖动调整高度：工具栏必须有 yw_resize 按钮，且它是 keyboard_resize 命令，Kotlin 里有处理
    rz = (y.get("preset_keys") or {}).get("yw_resize") or {}
    if rz.get("command") != "keyboard_resize" or rz.get("send") != "FUNCTION":
        bad.append("preset_keys yw_resize: missing or not FUNCTION keyboard_resize")
    if not any(b.get("action") == "yw_resize" for b in tb.get("buttons") or []):
        bad.append("tool_bar: no yw_resize (调整高度) button")
    ck = open(os.path.join(MAIN, "java", "com", "osfans", "trime", "ime", "keyboard", "CommonKeyboardActionListener.kt"), encoding="utf-8").read()
    if '"keyboard_resize" ->' not in ck:
        bad.append("CommonKeyboardActionListener: keyboard_resize not handled")
    # 工具箱：每一项的动作都已定义、图标是 MDI 里真有的名字；工具栏按钮的图标同理
    names = set(open(os.path.join(HERE, "mdi_names.txt"), encoding="utf-8").read().split())
    for label, icon, action in layouts.TOOLBOX + layouts.KEYBOARDS:
        if icon not in names:
            bad.append("toolbox %s: unknown icon %s" % (label, icon))
        if action not in presets:
            bad.append("toolbox %s: undefined action %s" % (label, action))
    for b in [tb.get("primary_button") or {}] + list(tb.get("buttons") or []):
        st = (b.get("foreground") or {}).get("style") or ""
        if st.startswith("ic@") and st[3:].replace("-", "_") not in names:
            bad.append("tool_bar: unknown icon %s" % st)
    for k, v in (y.get("preset_keys") or {}).items():
        lab = v.get("label") if isinstance(v, dict) else None
        if isinstance(lab, str) and lab.startswith("ic@") and lab[3:].replace("-", "_") not in names:
            bad.append("preset_keys %s: unknown icon %s" % (k, lab))
    if not (y.get("tool_bar") or {}).get("primary_button", {}).get("action") == "yw_toolbox":
        bad.append("tool_bar: the Logo button must open the toolbox (yw_toolbox)")
    if not any(t[2] == "yw_edit" for t in layouts.TOOLBOX) or "yw_edit_board" not in (y.get("preset_keyboards") or {}):
        bad.append("edit panel keyboard missing")
    tbx = os.path.join(MAIN, "java", "com", "osfans", "trime", "ime", "keyboard", "YiweiToolbox.kt")
    if not os.path.exists(tbx) or '"yiwei_toolbox" ->' not in ck:
        bad.append("toolbox window or its command handler missing")
    for f, needles in (("ime/keyboard/InputFeedbackManager.kt", ("createPredefined",)),
                       ("ime/core/TrimeInputMethodService.kt", ("capFontScale",)),
                       ("ime/core/InputView.kt", ("excludeEdgeGestures", "extraSidePadding")),
                       ("ime/keyboard/CommonKeyboardActionListener.kt", ('"yiwei_keepalive" ->',))):
        src = open(os.path.join(MAIN, "java", "com", "osfans", "trime", *f.split("/")), encoding="utf-8").read()
        for n in needles:
            if n not in src:
                bad.append("OPPO patch missing in %s: %s" % (f, n))
    for f, needles in (("ime/candidates/compact/CompactCandidateDelegate.kt", ("YiweiSideColumn.process", "realIndex")),
                       ("ime/keyboard/KeyboardWindow.kt", ("YiweiSideColumn.onKeyboard",)),
                       ("ime/keyboard/CommonKeyboardActionListener.kt", ("filteredFirst",))):
        src = open(os.path.join(MAIN, "java", "com", "osfans", "trime", *f.split("/")), encoding="utf-8").read()
        for n in needles:
            if n not in src:
                bad.append("side column patch missing in %s: %s" % (f, n))
    if (y["preset_keyboards"].get("number") or {}).get("import_preset") != "yw_number":
        bad.append("preset_keyboards.number must import yw_number")
    if not any(t[2] == "yw_keepalive" for t in layouts.TOOLBOX):
        bad.append("toolbox: 后台保活 entry missing")
    if "phrases" not in ((y.get("liquid_keyboard") or {}).get("keyboards") or []):
        bad.append("liquid_keyboard: 常用语 tab missing")
    # 搜狗式界面余项（install_sogou_ui）都在
    for f, needles in (("ime/keyboard/CommonKeyboardActionListener.kt", ('"yiwei_night" ->', '"yiwei_onehand" ->', '"yiwei_elder" ->', '"yiwei_inputprefs" ->')),
                       ("data/theme/ColorManager.kt", ("YiweiModes.effectiveNight", "fun yiweiRefresh")),
                       ("ime/core/InputView.kt", ("YiweiModes.oneHandExtra", "bindBlankSides")),
                       ("ime/keyboard/KeyboardView.kt", ("YiweiModes.textScale",)),
                       ("ime/candidates/CandidateItemUi.kt", ("yiweiBoldFont",)),
                       ("ime/candidates/compact/CompactCandidateDelegate.kt", ("yiweiLinear",)),
                       ("ime/symbol/LiquidWindow.kt", ("yiweiSide.wrap", "yiweiSide.activate")),
                       ("ime/keyboard/YiweiInputPrefs.kt", ("class YiweiInputPrefsWindow",))):
        src = open(os.path.join(MAIN, "java", "com", "osfans", "trime", *f.split("/")), encoding="utf-8").read()
        for n in needles:
            if n not in src:
                bad.append("sogou-ui patch missing in %s: %s" % (f, n))
    for act in ("yw_night", "yw_onehand", "yw_elder", "yw_inputprefs"):
        if not any(t[2] == act for t in layouts.TOOLBOX):
            bad.append("toolbox: %s entry missing" % act)
    if bad:
        raise SystemExit("undefined key actions (would be typed as text):\n  " + "\n  ".join(bad))
    print("keys ok: %d preset keys, tool bar and %s checked" % (len(presets), "/".join(layouts.OUR_KEYBOARDS)))


def install_slogan():
    """系统「更改键盘」列表里输入法名字下面的副标题（IME subtype 标签，原为「击响中文之韵」）改成「一维输入法」。"""
    n = 0
    for d in sorted(os.listdir(os.path.join(MAIN, "res"))):
        p = os.path.join(MAIN, "res", d, "strings.xml")
        if not d.startswith("values") or not os.path.exists(p):
            continue
        txt = "一維輸入法" if d.endswith("zh-rTW") or d.endswith("zh-rHK") else "一维输入法"
        t = open(p, encoding="utf-8").read()
        t2 = re.sub(r'(<string name="trime_app_slogan">)[^<]*', r"\g<1>" + txt, t)
        if t2 != t:
            open(p, "w", encoding="utf-8", newline="\n").write(t2)
            n += 1
    if n < 3:
        raise SystemExit("trime_app_slogan patched in only %d resource files" % n)
    print("slogan set in %d string files" % n)


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
    install_safe_toolbar()
    install_safe_upgrade()
    install_default_custom()
    install_feedback()
    install_toolbox()
    install_oppo()
    install_preedit()
    install_sidecolumn()
    install_sogou_ui()
    check_keys()  # after install_feedback: also checks the keyboard_resize handler it patches in
    install_brand()
    install_slogan()
    print("trime → 一维输入法 %s" % VERSION)
