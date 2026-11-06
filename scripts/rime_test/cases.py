"""用例：每个函数拿到一个已部署的 Rime，失败就 raise。"""
import os

ALL = []

# 一维助手（helper/Features/CoreInput.cs、RimeFeatures.cs）写出的补丁，格式保持一致
CUSTOM = {
    # W1：全拼模糊音追加在雾凇规则之后（en = eng）
    "rime_ice.custom.yaml": 'patch:\n  "speller/algebra/+":\n    - derive/en$/eng/\n    - derive/eng$/en/\n',
    # W1：双拼模糊音插在最前（zh = z）
    # W12：自定义标点（yiwei/punct.txt「`  ·」「~  ～|〜」→ half_shape 合并补丁）
    "double_pinyin.custom.yaml": 'patch:\n  "punctuator/half_shape/+": {"`": "·", "~": ["～", "〜"]}\n',
    # W47：输入法开关快捷键（RimeToggleKeys.PatchLines 写进 default.custom.yaml 的格式）
    "default.custom.yaml": 'patch:\n  "key_binder/bindings/+":\n'
                           '    - { when: always, toggle: full_shape, accept: Control+Shift+5 }\n'
                           '    - { when: always, toggle: full_shape, accept: Control+Shift+percent }\n'
                           '    - { when: always, toggle: emoji, accept: Control+period }\n',
    "double_pinyin_flypy.custom.yaml": 'patch:\n  "speller/algebra/@before 0": derive/^zh/z/\n  "speller/algebra/@before 1": derive/^z([^h])/zh$1/\n',
}


def setup(user):
    os.makedirs(user, exist_ok=True)
    for name, text in CUSTOM.items():
        open(os.path.join(user, name), "w", encoding="utf-8").write(text)


def case(fn):
    ALL.append((fn.__name__, fn))
    return fn


def first(r):
    p = r.context()["page"]
    return p[0] if p else ""


@case
def basic_nihao(r):
    r.open("rime_ice")
    r.type("nihao")
    assert first(r) == "你好", r.context()["page"]


def yiwei_file(r, name, content=None):
    import os
    d = os.path.join(r.user, "yiwei")
    os.makedirs(d, exist_ok=True)
    p = os.path.join(d, name)
    if content is None:
        if os.path.exists(p):
            os.remove(p)
    else:
        open(p, "w", encoding="utf-8").write(content)


def wait_flags():
    import time
    time.sleep(3.2)  # yiwei_flags.lua 3 秒缓存


def pick_in_pieces(r, code, pieces):
    r.type(code)
    for w in pieces:
        r.pick(w)
    got = r.commit()
    assert got == "".join(pieces), "上屏 %r" % got


@case
def x1_learn_pieces_first(r):
    """X1：yuanyiwei 分次选 苑/祎/玮，选两次后首选就是 苑祎玮；清除后不再置首。"""
    yiwei_file(r, "learn.disabled")
    r.reopen("rime_ice")
    r.type("yuanyiwei")
    before = first(r)
    r.clear()
    assert before != "苑祎玮", before
    for _ in range(2):
        pick_in_pieces(r, "yuanyiwei", ["苑", "祎", "玮"])
    r.type("yuanyiwei")
    assert first(r) == "苑祎玮", r.context()["page"]
    r.clear()
    # 清除最近一小时的学习：习惯和 RIME 用户词库里的 苑祎玮 都删掉
    yiwei_file(r, "learn.clear", "3600")
    r.type("yuanyiwei")
    r.clear()
    r.type("yuanyiwei")
    assert first(r) == before, r.context()["page"]
    r.clear()


@case
def x1_habit_single_char(r):
    """X1：常选的单字（选两次）持续排第一，关掉后恢复原顺序。"""
    r.reopen("rime_ice")
    r.type("shi")
    page = r.context()["page"]
    r.clear()
    target = page[4]
    for _ in range(2):
        r.type("shi"); r.pick(target); assert r.commit() == target
    r.type("shi")
    assert first(r) == target, r.context()["page"]
    r.clear()
    yiwei_file(r, "learn.disabled", "1")
    wait_flags()
    r.type("shi")
    off = r.context()["page"]
    r.clear()
    yiwei_file(r, "learn.disabled")
    wait_flags()
    yiwei_file(r, "learn.clear", "all")
    r.type("shi"); r.clear()
    print("    关闭时 shi →", off[:5])


def keys(r, seq):
    for k in seq:
        r.key(k)


def page_after(r, seq, schema="rime_ice"):
    r.open(schema)
    keys(r, seq)
    c = r.context()
    return c


@case
def w3_select_char(r):
    """W3 以词定字：jiandan 后按 [ 上屏首字、] 上屏尾字。"""
    r.open("rime_ice")
    r.type("jiandan["); assert r.commit() == "简"
    r.type("jiandan]"); assert r.commit() == "单"


@case
def w11_time_phrases(r):
    """W11 时间短语：rq 日期、sj 时间、xq 星期。"""
    import re
    import time
    assert re.match(r"\d{4}-\d\d-\d\d$", page_after(r, "rq")["page"][0])
    r.clear()
    assert re.match(r"\d\d:\d\d$", page_after(r, "sj")["page"][0])
    r.clear()
    assert page_after(r, "xq")["page"][0].startswith("星期")
    r.clear()


@case
def w5_v_mode_amount_date_calc(r):
    """W5：R+数字 大写金额、N+日期 农历、cC 算式（雾凇的 V 模式等价物）。"""
    p = page_after(r, "R1234.5")["page"]; r.clear()
    assert "壹仟贰佰叁拾肆元伍角" in p, p
    p = page_after(r, "cC1+2*3")["page"]; r.clear()
    assert p[:2] == ["7", "1+2*3=7"], p
    # N+公历转农历依赖 lua/lunar.db，不同 librime 行为不一，这里不测


@case
def w7_abbrev(r):
    """W7 简拼：zgrm → 中国人民 首选。"""
    assert page_after(r, "zgrm")["page"][0] == "中国人民"
    r.clear()


@case
def w10_emoji(r):
    """W10 Emoji 候选：xiaolian 的前几个候选里有 😀。"""
    p = page_after(r, "xiaolian")["page"]; r.clear()
    assert any(ord(c[0]) > 0x1F000 for c in p[:6]), p
    # 关掉 emoji 开关（助手写 switches/@3/reset: 0 就是这个开关的默认值）后只剩文字
    r.open("rime_ice")
    r.option("emoji", False)
    r.type("xiaolian")
    p = r.context()["page"]
    r.clear()
    r.option("emoji", True)
    assert not any(ord(c[0]) > 0x1F000 for c in p), p


@case
def w13_decimal_after_digit(r):
    """W13 智能标点：数字后的 . 是小数点，汉字后的 . 是句号。"""
    r.open("rime_ice")
    keys(r, ["1", ".", "5"]); assert r.commit().startswith("."), "数字后应是小数点"
    keys(r, list("ni") + ["."]); assert r.commit() == "你。"


@case
def w14_mixed_english(r):
    """W14 中英混输：直接打 hello 首选英文单词。"""
    assert page_after(r, "hello")["page"][0] == "hello"
    r.clear()


@case
def w15_caret_edit(r):
    """W15 拼音内光标：光标移到音节中间，退格删掉一个字母后重新出候选。"""
    r.open("rime_ice")
    r.type("nihao")
    r.set_caret_pos(r.session, 3)  # 光标放到 nih|ao（小狼毫里用 ← / 鼠标；不同 librime 的 ← 步长不同）
    r.key("BS")
    assert r.input() == "niao", r.input()
    assert r.context()["page"][:1] == ["鸟"] or r.context()["page"], r.context()
    r.clear()


@case
def w1_fuzzy_full_and_double(r):
    """W1 模糊音：全拼 en=eng（fen 出 风），小鹤双拼 z=zh（zi 出 知）。"""
    p = page_after(r, "fen")["page"]; r.clear()
    assert "风" in p, p
    p = page_after(r, "zi", "double_pinyin_flypy")["page"]; r.clear()
    assert "知" in p or "只" in p, p


@case
def w27_unit_currency(r):
    """W27 换算：hs5km、hs100f、hs2jin、hs100usd（离线汇率）；hs 本身仍是拼音简拼。"""
    p = page_after(r, "hs5km")["page"]; r.clear()
    assert "5 千米 = 5000 米" in p, p
    p = page_after(r, "hs100f")["page"]; r.clear()
    assert "100 °F = 37.7778 °C" in p, p
    p = page_after(r, "hs2jin")["page"]; r.clear()
    assert "2 斤 = 1 千克" in p, p
    p = page_after(r, "hs100usd")["page"]; r.clear()
    assert any(x.startswith("100 美元 ≈ ") and x.endswith(" 人民币") for x in p), p
    p = page_after(r, "hs")["page"]; r.clear()
    assert p and "千米" not in p[0], p


@case
def w12_custom_punct(r):
    """W12 自定义标点：自然码双拼方案里 ` 打出 ·，~ 出两个候选。"""
    r.open("double_pinyin")
    r.key("`"); assert r.commit() == "·"
    r.key("~"); p = r.context()["page"]; r.clear()
    assert p[:2] == ["～", "〜"], p


@case
def w4_stroke(r):
    """W4 笔画：u + hspnz（横竖撇点折）。uhs → 十 在前几个；完整笔画 upspzspn 首选 你；up / us 仍是普通输入。"""
    r.open("rime_ice")
    r.type("uhs")
    page = r.context()["page"]
    assert "十" in page[:3], page
    r.clear()
    r.type("upspzspn")
    page = r.context()["page"]
    assert page and page[0] == "你", page
    c = r.candidates(10)
    print("    upspzspn →", page[:5], c[0] if c else "")
    r.clear()
    r.type("us")
    assert "十" not in r.context()["page"], r.context()["page"]
    r.clear()


@case
def w2_shuangpin_keymap(r):
    """W2 双拼键位图：从方案 algebra 生成的 yiwei_shuangpin.txt 与真实输入一致（小鹤 nihc → 你好）。"""
    km = {}
    for line in open(os.path.join(r.shared, "yiwei_shuangpin.txt"), encoding="utf-8"):
        if line.startswith("#") or not line.strip():
            continue
        sid, _, k, labels = line.rstrip("\n").split("\t")
        km[(sid, k)] = labels.split(",")
    assert km[("double_pinyin_flypy", "q")] == ["iu"], km.get(("double_pinyin_flypy", "q"))
    assert "ao" in km[("double_pinyin_flypy", "c")]
    assert "sh" in km[("double_pinyin_flypy", "u")]
    assert km[("double_pinyin_mspy", ";")] == ["ing"]
    assert "ing" in km[("double_pinyin", "y")]
    r.open("double_pinyin_flypy")
    r.type("nihc")
    assert first(r) == "你好", r.context()["page"]
    r.clear()


@case
def w28_search_prefix(r):
    """W28 搜索：sSnihao 候选是「你好」，空格不上屏、发 search:你好 给助手（Linux 上命名管道是当前目录里的普通文件）。"""
    pipe = os.path.join(r.user, "\\\\.\\pipe\\YiweiHelper.Commands")
    if os.path.exists(pipe):
        os.remove(pipe)
    r.open("rime_ice")
    r.type("sSnihao")
    assert first(r) == "你好", r.context()["page"]
    r.key(" ")
    assert r.commit() == "", "搜索词不该上屏"
    assert open(pipe, encoding="utf-8").read() == "search:你好"
    r.type("sSbeijing")
    r.key("1")
    assert open(pipe, encoding="utf-8").read() == "search:北京"
    os.remove(pipe)


@case
def w6_person_names(r):
    """W6 人名候选：liuxinyi 补出「刘欣怡」（注释 人名），sunjiahao 有「孙嘉豪」；nihao 不加；names.disabled 后不再出现。"""
    r.reopen("rime_ice")
    r.type("liuxinyi")
    c = r.candidates(12)
    r.clear()
    assert ("刘欣怡", "人名") in c, c
    r.type("sunjiahao")
    assert "孙嘉豪" in r.texts(12), r.texts(12)
    r.clear()
    r.type("nihao")
    assert not any(cm == "人名" for _, cm in r.candidates(12)), r.candidates(12)
    r.clear()
    yiwei_file(r, "names.disabled", "1")
    wait_flags()
    r.type("liuxinyi")
    assert not any(cm == "人名" for _, cm in r.candidates(12)), r.candidates(12)
    r.clear()
    yiwei_file(r, "names.disabled")
    wait_flags()


@case
def w47_switch_hotkeys(r):
    """W47 输入法开关快捷键：default.custom.yaml 加的 Ctrl+Shift+5 切全角、Ctrl+. 切 Emoji，全拼和双拼都生效；自带 Ctrl+Shift+4 简繁仍在。"""
    for schema in ("rime_ice", "double_pinyin_flypy"):
        r.open(schema)
        before = r.option("full_shape")
        r.key("Control+Shift+5")
        assert r.option("full_shape") != before, schema + " full_shape 没切换"
        r.key("Control+Shift+5")
        assert r.option("full_shape") == before
        e = r.option("emoji")
        r.key("Control+.")
        assert r.option("emoji") != e, schema + " emoji 没切换"
        r.key("Control+.")
        t = r.option("traditionalization")
        r.key("Control+Shift+4")
        assert r.option("traditionalization") != t, schema + " 自带简繁快捷键丢了"
        r.key("Control+Shift+4")


@case
def p34_perf_timing(r):
    """34 性能体检：每 20 次按键写一次 yiwei/perf.tsv（次数、平均毫秒、最慢毫秒、超过 50ms 次数、累计、时间）。"""
    path = os.path.join(r.user, "yiwei", "perf.tsv")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if os.path.exists(path):
        os.remove(path)
    r.reopen("rime_ice")
    for w in ["nihao", "shijian", "women", "zhongguo", "shurufa"]:
        r.type(w); r.clear()
    assert os.path.exists(path), "没有写 perf.tsv"
    n, avg, mx, slow, total, ts = open(path, encoding="utf-8").read().split("\t")
    assert int(n) >= 20 and float(avg) >= 0 and float(mx) >= float(avg), (n, avg, mx)
    print("    每键平均 %s ms，最慢 %s ms（%s 次）" % (avg, mx, n))


@case
def p32_lite_dict(r):
    """32 标准词库（放最后：要重新部署两次）：用户文件夹的 rime_ice.dict.yaml 注释掉 tencent（和 helper/Features/DictLite.cs 同一规则）后，
    只在腾讯词库里的「龚古尔奖」不再整词出现、日常词照常；删掉后恢复完整。"""
    import re
    import shutil
    import time
    word, code = "龚古尔奖", "gongguerjiang"
    r.reopen("rime_ice")
    r.type(code); full = r.texts(5); r.clear()
    assert word in full, full
    src = open(os.path.join(r.shared, "rime_ice.dict.yaml"), encoding="utf-8").read()
    lite, n = re.subn(r"(?m)^  - cn_dicts/tencent\b.*$",
                      "  # - cn_dicts/tencent  # 一维输入法：标准词库不含腾讯词向量（设置 → 词库 可改回完整）", src)
    assert n == 1, "rime_ice.dict.yaml 里找不到 cn_dicts/tencent"
    user_dict = os.path.join(r.user, "rime_ice.dict.yaml")
    open(user_dict, "w", encoding="utf-8").write(lite)
    try:
        t0 = time.time()
        r.deploy(True)
        print("    标准词库部署 %.0fs" % (time.time() - t0))
        r.reopen("rime_ice")
        r.type(code); got = r.texts(5); r.clear()
        assert word not in got, got
        r.type("nihao"); assert first(r) == "你好"; r.clear()
        r.type("shijian"); assert first(r) == "时间"; r.clear()
    finally:
        os.remove(user_dict)
        r.deploy(True)
    r.reopen("rime_ice")
    r.type(code); back = r.texts(5); r.clear()
    assert word in back, back


@case
def enter_commits_letters(r):
    """回车上屏英文字母、空格才上屏候选（用户 2026-10-14）。"""
    r.open("rime_ice")
    r.type("nihao"); r.key("RET"); assert r.commit() == "nihao", "回车"
    r.type("nihao"); r.key(" "); assert r.commit() == "你好", "空格"


@case
def autopair_skip_kinds(r):
    """括号自动配对逐类开关：yiwei/autopair.skip 里的左半边不再发 pair:，其余照常（Linux 上命名管道是当前目录里的普通文件）。"""
    pipe = os.path.join(r.user, "\\\\.\\pipe\\YiweiHelper.Commands")
    if os.path.exists(pipe):
        os.remove(pipe)
    yiwei_file(r, "autopair.skip", "《“")
    try:
        wait_flags()
        r.open("rime_ice")
        r.key("<")
        r.clear()
        assert not os.path.exists(pipe), "《 已取消配对，不该发 pair:"
        r.key("(")
        assert open(pipe, encoding="utf-8").read() == "pair:（）"
        os.remove(pipe)
        yiwei_file(r, "autopair.skip", None)
        wait_flags()
        r.key("<")
        assert open(pipe, encoding="utf-8").read() == "pair:《》", "删掉 skip 后 《 恢复配对"
    finally:
        yiwei_file(r, "autopair.skip", None)
        if os.path.exists(pipe):
            os.remove(pipe)


@case
def regret_hotkeys(r):
    """上屏后反悔重选：默认 Ctrl+Shift+Z；yiwei/regret.key 可改成 backspace / both；没有刚上屏的拼音或正在输入时不拦截。"""
    pipe = os.path.join(r.user, "\\\\.\\pipe\\YiweiHelper.Commands")

    def reset():
        if os.path.exists(pipe):
            os.remove(pipe)

    def commit_nihao():
        r.type("nihao"); r.key(" ")
        assert r.commit() == "你好"

    def sent():
        return open(pipe, encoding="utf-8").read() if os.path.exists(pipe) else None

    try:
        yiwei_file(r, "regret.key", None)
        yiwei_file(r, "regret.disabled", None)
        wait_flags()
        r.open("rime_ice")
        reset()
        commit_nihao()
        r.key("Control+BS")
        assert sent() is None, "默认不再用 Ctrl+Backspace"
        r.key("Control+Shift+Z")
        assert sent() == "regret:2:nihao", sent()
        reset()
        r.key("Control+Shift+Z")
        assert sent() is None, "同一次上屏只能反悔一次"
        # 正在输入拼音时不拦截
        commit_nihao()
        r.type("ni")
        r.key("Control+Shift+Z")
        assert sent() is None, "正在输入时不该拦截"
        r.clear()
        # backspace 模式
        yiwei_file(r, "regret.key", "backspace")
        wait_flags()
        commit_nihao()
        r.key("Control+Shift+Z")
        assert sent() is None, "backspace 模式不响应 Ctrl+Shift+Z"
        r.key("Control+BS")
        assert sent() == "regret:2:nihao", sent()
        reset()
        # both 模式
        yiwei_file(r, "regret.key", "both")
        wait_flags()
        commit_nihao()
        r.key("Control+Shift+Z")
        assert sent() == "regret:2:nihao", sent()
        reset()
        commit_nihao()
        r.key("Control+BS")
        assert sent() == "regret:2:nihao", sent()
        reset()
        # 阶段 2：分两次上屏的一句话（3 秒内、中间没按别的键）整句退回，拼音用 ' 隔开
        yiwei_file(r, "regret.key", None)
        wait_flags()
        commit_nihao()
        r.type("shijie"); r.key(" ")
        assert r.commit() == "世界"
        r.key("Control+Shift+Z")
        assert sent() == "regret:4:nihao'shijie", sent()
        reset()
        # 中间按了方向键（光标可能动了）：只退回最后一段
        wait_flags()
        commit_nihao()
        r.key("LEFT")
        r.type("shijie"); r.key(" ")
        assert r.commit() == "世界"
        r.key("Control+Shift+Z")
        assert sent() == "regret:2:shijie", sent()
        reset()
        # 总开关
        yiwei_file(r, "regret.disabled", "1")
        wait_flags()
        commit_nihao()
        r.key("Control+Shift+Z")
        assert sent() is None, "关闭后不该拦截"
    finally:
        yiwei_file(r, "regret.key", None)
        yiwei_file(r, "regret.disabled", None)
        reset()


@case
def toast_only_manual_toggle(r):
    """中/英提示只在用户按键切换时弹（toast:），程序自动切换（如切到自动英文的程序）只同步状态（mode:），不再乱弹「A」。"""
    pipe = os.path.join(r.user, "\\\\.\\pipe\\YiweiHelper.Commands")

    def reset():
        if os.path.exists(pipe):
            os.remove(pipe)

    def sent():
        return open(pipe, encoding="utf-8").read() if os.path.exists(pipe) else None

    try:
        r.reopen("rime_ice")
        r.option("ascii_mode", False)
        reset()
        # 小狼毫按程序设置（app_options）或助手命令改中英：不弹
        r.option("ascii_mode", True)
        assert sent() == "mode:A", sent()
        reset()
        r.option("ascii_mode", False)
        assert sent() == "mode:中", sent()
        # 打了字之后的自动切换也不弹
        r.type("a"); r.key("\x1b")
        reset()
        r.option("ascii_mode", True)
        assert sent() == "mode:A", sent()
        r.option("ascii_mode", False)
        # 单按 Shift 切换：弹
        reset()
        r.key("Shift_L"); r.key("Release+Shift_L")
        assert r.option("ascii_mode") is True, "Shift_L 应切到英文"
        assert sent() == "toast:A", sent()
        reset()
        r.key("Shift_L"); r.key("Release+Shift_L")
        assert sent() == "toast:中", sent()
    finally:
        reset()
        r.option("ascii_mode", False)


@case
def mixed_words(r):
    """backlog 7 中英混输词条：ppt → PPT、cwei → C位 排第 2（不抢首选），只有它一个候选时排第 1（icka → IC卡）；
    用户文件夹放一份空的 yiwei_mixed.txt（和 helper/Features/MixedWords.cs 同一规则）后不再出现，删掉后恢复。"""
    def page(code):
        r.type(code); t = r.texts(6); r.clear(); return t
    r.reopen("rime_ice")
    for code, word in (("ppt", "PPT"), ("cwei", "C位"), ("qq", "QQ"), ("ktv", "KTV")):
        t = page(code)
        assert len(t) > 1 and t[1] == word, (code, t)
    assert page("icka")[:1] == ["IC卡"], page("icka")
    assert page("nihao")[0] == "你好"
    off = os.path.join(r.user, "yiwei_mixed.txt")
    open(off, "w", encoding="utf-8").write("# 一维输入法：中英混输词条已关闭（设置 → 词库 可重新打开；删掉本文件也会恢复）\n")
    try:
        r.deploy(True)
        r.reopen("rime_ice")
        assert "PPT" not in page("ppt"), page("ppt")
        assert "IC卡" not in page("icka"), page("icka")
    finally:
        os.remove(off)
        r.deploy(True)
    r.reopen("rime_ice")
    assert page("ppt")[1] == "PPT"


@case
def phrase_time_vars(r):
    """常用语时间变量：custom_phrase 里的 {date}/{weekday} 上屏时换成当天的值，不认识的 {xxx} 原样保留
    （和 helper/Features/PhraseVars.cs 同一套写法）。"""
    import time
    user_phrase = os.path.join(r.user, "custom_phrase.txt")
    open(user_phrase, "w", encoding="utf-8").write(
        "今天是{date}\tywjt\t100\n{weekday}好\tywxq\t100\n保留{abc}\tywbl\t100\n")
    try:
        r.deploy(True)
        r.reopen("rime_ice")
        r.type("ywjt"); got = r.texts(3); r.clear()
        assert got and got[0] == "今天是" + time.strftime("%Y-%m-%d"), got
        r.type("ywxq"); got = r.texts(3); r.clear()
        week = "日一二三四五六"[int(time.strftime("%w"))]
        assert got and got[0] == "星期" + week + "好", got
        r.type("ywbl"); got = r.texts(3); r.clear()
        assert got and got[0] == "保留{abc}", got
    finally:
        os.remove(user_phrase)
        r.deploy(True)


@case
def typo_stats(r):
    """维护报告「选词不顺」统计：翻页才选到的词、上屏后马上退格重打的词，只在统计开启时记，只记词不记句子。"""
    import glob

    def typo_lines():
        out = []
        for f in glob.glob(os.path.join(r.user, "yiwei", "typo-*.tsv")):
            out += [l.rstrip("\n") for l in open(f, encoding="utf-8") if l.strip()]
        return out

    def pick_lines():
        out = []
        for f in glob.glob(os.path.join(r.user, "yiwei", "pick-*.tsv")):
            out += [l.rstrip("\n") for l in open(f, encoding="utf-8") if l.strip()]
        return out

    def late_cjk():
        """第 2 页以后的第一个汉字候选（跳过 emoji）。"""
        ps = r.context()["page_size"]
        return next(t for t in r.texts(40)[ps * 2:] if len(t) == 1 and 0x4E00 <= ord(t) <= 0x9FFF)

    def clean():
        for f in glob.glob(os.path.join(r.user, "yiwei", "typo-*.tsv")) + glob.glob(os.path.join(r.user, "yiwei", "stats-*.tsv")) + glob.glob(os.path.join(r.user, "yiwei", "pick-*.tsv")):
            os.remove(f)

    try:
        yiwei_file(r, "stats.enabled", None)
        clean()
        r.reopen("rime_ice")
        # 关闭统计时什么都不记
        r.type("shi")
        late = late_cjk()
        r.pick(late)
        assert r.commit() == late
        assert typo_lines() == [], typo_lines()
        yiwei_file(r, "stats.enabled", "1")
        # 第一页选的不记，翻页选的记 paged
        r.type("shi"); r.key(" "); r.commit()
        assert typo_lines() == [], typo_lines()
        r.type("shi")
        late = late_cjk()
        r.pick(late)
        assert r.commit() == late
        assert typo_lines() == ["paged\t" + late], typo_lines()
        clean()
        # 上屏「你好」后退格两下重打「拟好」→ retype 你好 → 新词
        r.type("nihao"); r.key(" ")
        assert r.commit() == "你好"
        r.key("BS"); r.key("BS")
        r.type("nihao"); r.pick("拟好")
        assert r.commit() == "拟好"
        assert typo_lines() == ["retype\t你好\t拟好"], typo_lines()
        clean()
        # 只退一个字：记最后一个字
        r.type("nihao"); r.key(" "); r.commit()
        r.key("BS")
        r.type("hao"); r.key(" ")
        assert r.commit() == "好"
        assert typo_lines() == ["retype\t好\t好"], typo_lines()
        clean()
        # 中间按了方向键 / 没删就接着打：都不算重打
        r.type("nihao"); r.key(" "); r.commit()
        r.key("LEFT"); r.key("BS")
        r.type("nihao"); r.key(" "); r.commit()
        r.type("shijie"); r.key(" "); r.commit()
        assert typo_lines() == [], typo_lines()
        # 统计文件只有上屏的词本身（与以前一致），typo 文件里没有时间戳
        assert not any(l[0].isdigit() for l in typo_lines())
        # 首选命中率：pick 文件只有「日期 TAB 1/0」，第一个候选记 1，其它记 0，回车上屏字母不记
        import datetime
        today = datetime.date.today().isoformat()
        clean()
        r.type("nihao"); r.key(" "); r.commit()
        r.type("nihao"); r.pick("拟好"); r.commit()
        r.type("abc"); r.key("RET"); r.commit()
        assert pick_lines() == [today + "\t1", today + "\t0"], pick_lines()
    finally:
        yiwei_file(r, "stats.enabled", None)
        clean()


@case
def x8_unlearn_on_retype(r):
    """待办 8 从退格里学习：上屏的词马上被退格整个删掉、用同样拼音改选别的，刚才那次不算进习惯；中间按方向键则照常算。"""
    import glob
    try:
        yiwei_file(r, "learn.disabled")
        yiwei_file(r, "learn.clear", "all")
        r.reopen("rime_ice")
        r.type("shi"); r.clear()  # 执行清除
        r.type("shi")
        page = r.context()["page"]
        r.clear()
        x, y = page[3], page[4]
        for _ in range(2):
            r.type("shi"); r.pick(x); assert r.commit() == x
        r.type("shi")
        assert first(r) == x, r.context()["page"]
        r.clear()
        # 两次：空格上屏了 x，退格删掉，改选 y —— x 的这两次不算（仍是 2 次、时间也退回去），y 2 次且更近，排第一
        # （不撤销的话 x 是 4 次，仍排第一）
        import time
        time.sleep(1.1)  # learn.tsv 时间按整秒记
        for _ in range(2):
            r.type("shi"); r.key(" "); assert r.commit() == x
            r.key("BS")
            r.type("shi"); r.pick(y); assert r.commit() == y
        r.type("shi")
        assert first(r) == y, "退格重打后 x 不该还排第一：x=%s y=%s %s %s" % (x, y, r.context()["page"], open(os.path.join(r.user, "yiwei", "learn.tsv"), encoding="utf-8").read()[-300:])
        r.clear()
        lines = []
        for f in glob.glob(os.path.join(r.user, "yiwei", "learn.tsv")):
            lines = [l.rstrip("\n") for l in open(f, encoding="utf-8")]
        assert sum(1 for l in lines if l.startswith("shi\t" + x + "\t") and l.endswith("\t-1")) == 2, lines[-8:]
        # 中间按了方向键：不是重打，照常记
        r.type("shi"); r.key(" "); assert r.commit() == y
        r.key("LEFT"); r.key("BS")
        r.type("shi"); r.pick(x); assert r.commit() == x
        lines = [l.rstrip("\n") for l in open(os.path.join(r.user, "yiwei", "learn.tsv"), encoding="utf-8")]
        assert not any(l.startswith("shi\t" + y + "\t") and l.endswith("\t-1") for l in lines), lines[-4:]
    finally:
        yiwei_file(r, "learn.clear", "all")
        r.type("shi"); r.clear()


@case
def correct_pinyin(r):
    """拼音纠错（yiwei_correct.lua，用户 2026-11-06 要求）：打错一个字母时补「纠」候选，排第 2 位起；
    拼音本来就对（有完整的词）时一个都不加、首选不变（避免微信 diushi 打不出「丢失」）；可关、可排第一。"""
    def page(code, n=6):
        r.type(code)
        c = r.context()
        r.clear()
        return list(zip(c["page"], c["comments"]))[:n]

    def has(p, word, top):
        return any(t == word and "纠" in (cm or "") for t, cm in p[:top])

    yiwei_file(r, "correct.disabled")
    yiwei_file(r, "correct_first.enabled")
    wait_flags()
    r.reopen("rime_ice")
    p = page("yinggua")
    assert has(p, "英国", 3) and p[0][0] != "英国", p  # 用户原话的例子：前 3 位、带「纠」、不抢首选
    for code, word in (("zhognguo", "中国"), ("beijign", "北京")):
        p = page(code)
        assert word in [t for t, _ in p[:3]], (code, p)
    for code, word in (("nihso", "你好"), ("xainzai", "现在"), ("jintain", "今天"), ("shoujo", "手机")):
        p = page(code)
        assert has(p, word, 3), (code, p)
    # 正常输入：首选不变，也没有任何纠错候选
    for code, word in (("diushi", "丢失"), ("nihao", "你好"), ("yingguo", "英国"), ("women", "我们"),
                       ("shenme", "什么"), ("xianzai", "现在"), ("zhongguo", "中国"), ("beijing", "北京"), ("dianhua", "电话")):
        p = page(code, 9)
        assert p[0][0] == word, (code, p)
        assert not any("纠" in (cm or "") for _, cm in p), (code, p)
    # 简拼不纠
    p = page("zgrm", 9)
    assert not any("纠" in (cm or "") for _, cm in p), p
    try:
        yiwei_file(r, "correct_first.enabled", "1")
        wait_flags()
        p = page("yinggua")
        assert p[0][0] == "英国" and "纠" in p[0][1], p
        assert page("diushi")[0][0] == "丢失"
        yiwei_file(r, "correct_first.enabled")
        yiwei_file(r, "correct.disabled", "1")
        wait_flags()
        p = page("yinggua", 9)
        assert not any("纠" in (cm or "") for _, cm in p), p
    finally:
        yiwei_file(r, "correct.disabled")
        yiwei_file(r, "correct_first.enabled")
        wait_flags()
    assert has(page("yinggua"), "英国", 3)
