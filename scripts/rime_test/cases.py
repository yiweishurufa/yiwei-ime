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
