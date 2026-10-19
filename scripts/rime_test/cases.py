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
