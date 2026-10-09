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


# ---------- colours ----------

def hx(argb):
    """Trime colours: 0xRRGGBB, or 0xAARRGGBB when not opaque."""
    a = (argb >> 24) & 255
    return "0x%08x" % argb if a != 255 else "0x%06x" % (argb & 0xFFFFFF)


def trime_scheme(bid, name, rgb, dark):
    s = scheme(bid, rgb, dark)
    acc = 0xFF000000 | rgb
    if dark:
        kb, key, keyfg, sym, keyhi = 0xFF141716, 0xFF262B28, 0xFFE8ECE9, 0xFF8F9893, 0xFF39403C
        on_bg, on_fg = mix(0xFF1B1F1D, acc, .45), 0xFFFFFFFF
    else:
        kb, key, keyfg, sym, keyhi = 0xFFE9ECE8, 0xFFFBFCFA, 0xFF1F2421, 0xFF6A736E, 0xFFD5DAD6
        on_bg, on_fg = acc, 0xFFFFFFFF
    c = dict(
        back_color=s["back_color"], border_color=s["border_color"], candidate_separator_color=s["border_color"],
        candidate_text_color=s["candidate_text_color"], comment_text_color=s["comment_text_color"],
        hilited_back_color=s["hilited_back_color"], hilited_candidate_back_color=s["hilited_candidate_back_color"],
        hilited_candidate_text_color=s["hilited_candidate_text_color"],
        hilited_comment_text_color=s["hilited_comment_text_color"], hilited_text_color=s["hilited_text_color"],
        text_color=s["text_color"], text_back_color=s["back_color"], label_color=s["label_color"],
        keyboard_back_color=kb, key_back_color=key, key_text_color=keyfg, key_symbol_color=sym,
        hilited_key_back_color=keyhi, hilited_key_text_color=keyfg, hilited_key_symbol_color=sym,
        off_key_back_color=key, off_key_text_color=keyfg, hilited_off_key_back_color=keyhi, hilited_off_key_text_color=keyfg,
        on_key_back_color=on_bg, on_key_text_color=on_fg, hilited_on_key_back_color=on_bg, hilited_on_key_text_color=on_fg,
        preview_back_color=key, preview_text_color=acc if not dark else 0xFFE8ECE9, shadow_color=0x00000000,
    )
    out = ["    name: \"一维 · %s%s\"" % (name, "（深色）" if dark else ""), "    author: 一维输入法"]
    out += ["    %s: %s" % (k, hx(v)) for k, v in c.items()]
    return out


def install_colours():
    lines = []
    for i, (bid, name, rgb) in enumerate(BRANDS):
        for dark in (False, True):
            sid = "yiwei_%s%s" % (bid, "_dark" if dark else "")
            body = trime_scheme(bid, name, rgb, dark)
            if not dark:
                body.append("    dark_scheme: yiwei_%s_dark" % bid)
            lines.append("  %s:" % sid)
            lines += body
            if bid == "qingbi" and not dark:  # Trime falls back to `default`: make that 青碧
                lines.append("  default:")
                lines += body
    block = "\n".join(lines) + "\n\n"

    def patch(t):
        # drop Trime's own `default` scheme (ours replaces it), keep the rest selectable
        t = re.sub(r"(\npreset_color_schemes:\n)  default:\n(?:    [^\n]*\n|\n)*?(?=  \w+:\n)", r"\1", t, count=1)
        t, n = re.subn(r"\npreset_color_schemes:\n", lambda m: m.group(0) + block, t, count=1)
        if n != 1:
            raise SystemExit("trime.yaml: preset_color_schemes not found")
        t = re.sub(r"^name: .*$", "name: 一维 · 墨线", t, count=1, flags=re.M)
        t = re.sub(r"^author: .*$", "author: 一维输入法（基于同文 Trime 默认主题）", t, count=1, flags=re.M)
        return t
    edit(os.path.join(SHARED, "trime.yaml"), patch)


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
    from PIL import Image, ImageDraw
    import icon
    res = os.path.join(MAIN, "res")
    for dpi, px in (("mdpi", 48), ("hdpi", 72), ("xhdpi", 96), ("xxhdpi", 144), ("xxxhdpi", 192)):
        im = icon.app_icon(px)
        for n in ("ic_app_icon.png", "ic_app_icon_round.png"):
            im.save(os.path.join(res, "mipmap-" + dpi, n))
    # adaptive icon: ink background colour + paper stroke and seal on a transparent 108dp layer
    edit(os.path.join(res, "values", "ic_app_icon_background.xml"),
         lambda t: re.sub(r"#[0-9A-Fa-f]{6}", "#%02X%02X%02X" % icon.INK, t, count=1))
    fg = os.path.join(res, "drawable", "ic_app_icon_foreground.xml")
    if os.path.exists(fg):
        os.remove(fg)
    S = 4; N = 432 * S
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0)); dr = ImageDraw.Draw(im)
    w = N * 0.42; sc = 1.4
    icon.PTS[:] = [(x, y * sc) for x, y in icon.BASE]
    icon.stroke(dr, S, ((N - w) / 2, N * 0.47 - w * 0.09 * sc, (N + w) / 2, 0), icon.PAPER + (255,))
    s = N * 0.07; x = N * 0.60; y = N * 0.60
    dr.rounded_rectangle((x, y, x + s, y + s), s * 0.18, fill=icon.SEAL + (255,))
    d = os.path.join(res, "drawable-xxxhdpi"); os.makedirs(d, exist_ok=True)
    im.resize((432, 432), Image.LANCZOS).save(os.path.join(d, "ic_app_icon_foreground.png"))


if __name__ == "__main__":
    install_data()
    install_colours()
    install_brand()
    print("trime → 一维输入法 %s" % VERSION)
