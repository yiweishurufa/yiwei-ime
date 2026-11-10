#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""一维分类词库生成器（可重复生成）。

  python3 scripts/data_gen/lexicon/build.py <雾凇数据目录（含 cn_dicts/）> [--cache DIR]

输出（都提交进仓库）：
  overlay/data/yiwei_dicts/yiwei.<领域>.dict.yaml   每个领域一个词典，由 rime_ice.dict.yaml 的 import_tables 挂载
  overlay/data/lua/yiwei_poetry_data.lua            诗句联想表（上句 → 下句、作者、题目）
  scripts/rime_test/lexicon_coverage.tsv            领域命中率测试清单（领域、词、拼音）

数据来源与许可证见 overlay/data/yiwei_dicts/README.md（同目录 SOURCES 常量）。
只用可再分发的开源数据；搜狗/百度/讯飞等商业细胞词库只参考了分类方式，没有复制任何词条。

权重校准（避免「全部写 100」和抢常用词首选）：
  * 有语料频次的（THUOCL 的 DF 值，与雾凇 base 词库同一量纲——base 本身就用了 THUOCL 的 DF）直接用，
    按词长截到雾凇 base 同长度词权重的 90 分位以内；
  * 手工整理、没有频次的按类别给一档固定权重（在 base 同长度词的 50–75 分位之间）；
  * 一维词库挂在 base 前面（同字同音以上面的为准），所以只收：雾凇没有的词；ext/腾讯里只有 100 的词；
    以及 base 里明显偏低、需要校准的词（盐城 1、长治 2：地名/国名/手工类别按目标权重，其它领域只改 base < 50 的，
    且新权重至少是旧的 4 倍）。其余已在 8105/base 的词不收，照用雾凇的人工调频；
  * 不抢首选：没有语料频次的词，同一个完整拼音下 base 已有别的词时，权重压到那个词的一半以下
    （袁鹰 113 曾压过 base 的 元婴 111）；有 THUOCL 频次的地名、术语按频次。
"""
import argparse, json, os, random, re, sys, urllib.request, collections

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, HERE)
import curated as C  # noqa: E402

RAW = "https://raw.githubusercontent.com/"
SOURCES = {
    # 文件名: (URL, 许可证, 说明)
    "mo_provinces.json": (RAW + "modood/Administrative-divisions-of-China/master/dist/provinces.json", "WTFPL", "省级行政区"),
    "mo_cities.json": (RAW + "modood/Administrative-divisions-of-China/master/dist/cities.json", "WTFPL", "地级行政区"),
    "mo_areas.json": (RAW + "modood/Administrative-divisions-of-China/master/dist/areas.json", "WTFPL", "县级行政区"),
    "ts300.json": (RAW + "chinese-poetry/chinese-poetry/master/%E5%85%A8%E5%94%90%E8%AF%97/%E5%94%90%E8%AF%97%E4%B8%89%E7%99%BE%E9%A6%96.json", "MIT", "唐诗三百首"),
    "sc300.json": (RAW + "chinese-poetry/chinese-poetry/master/%E5%AE%8B%E8%AF%8D/%E5%AE%8B%E8%AF%8D%E4%B8%89%E7%99%BE%E9%A6%96.json", "MIT", "宋词三百首"),
    "qianjia.json": (RAW + "chinese-poetry/chinese-poetry/master/%E8%92%99%E5%AD%A6/qianjiashi.json", "MIT", "千家诗"),
    "shijing.json": (RAW + "chinese-poetry/chinese-poetry/master/%E8%AF%97%E7%BB%8F/shijing.json", "MIT", "诗经"),
    "idiom.json": (RAW + "pwxcoo/chinese-xinhua/master/data/idiom.json", "MIT", "成语（新华字典数据）"),
}
for _n in ("IT", "caijing", "chengyu", "diming", "lishimingren", "poem", "medical", "food", "law", "car", "animal"):
    SOURCES["THUOCL_%s.txt" % _n] = (RAW + "thunlp/THUOCL/master/data/THUOCL_%s.txt" % _n, "MIT", "THUOCL 清华大学开放中文词库")

DOMAINS = [
    # id, 名称, 说明
    ("spoken", "口语短句", "吃点饭、喝点水、到哪了这类聊天口语块"),
    ("geo", "中国地名", "省、地级市、县区、山川河流湖泊、名胜古迹"),
    ("world", "世界地名", "国家、首都、世界主要城市"),
    ("poetry", "诗词名句", "唐诗、宋词、千家诗、诗经的名句"),
    ("idiom", "成语", "成语与固定词组"),
    ("history", "历史人文", "朝代、帝王、历史人物与事件、节日节气、传统文化"),
    ("science", "自然科学", "化学元素、数理化与天文术语"),
    ("medical", "医学", "疾病、药品、检查与医学术语"),
    ("law", "法律", "法律法规与法律术语"),
    ("finance", "财经", "金融、证券、经济术语"),
    ("it", "计算机", "计算机与互联网术语"),
    ("food", "饮食", "菜名、食材、小吃"),
    ("nature", "动植物", "动物、植物、花卉"),
    ("brand", "品牌机构", "常见品牌、企业、高校与机构、汽车"),
]


def fetch(cache, name):
    p = os.path.join(cache, name)
    if not os.path.exists(p):
        os.makedirs(cache, exist_ok=True)
        data = urllib.request.urlopen(SOURCES[name][0], timeout=120).read()
        open(p, "wb").write(data)
    return p


def thuocl(cache, n):
    out = {}
    for line in open(fetch(cache, "THUOCL_%s.txt" % n), encoding="utf-8-sig").read().replace("\r", "\n").split("\n"):
        parts = line.strip().split("\t")
        if len(parts) == 2 and parts[1].strip().isdigit():
            out[parts[0].strip()] = int(parts[1])
    return out


ETHNIC = "|".join(sorted("""蒙古族 回族 藏族 维吾尔族 苗族 彝族 壮族 布依族 朝鲜族 满族 侗族 瑶族 白族 土家族 哈尼族 哈萨克族 傣族 黎族 傈僳族 佤族 畲族 拉祜族 水族 东乡族 纳西族 景颇族 柯尔克孜族 土族 达斡尔族 仫佬族 羌族 布朗族 撒拉族 毛南族 仡佬族 锡伯族 阿昌族 普米族 塔吉克族 怒族 乌孜别克族 俄罗斯族 鄂温克族 德昂族 保安族 裕固族 京族 塔塔尔族 独龙族 鄂伦春族 赫哲族 门巴族 珞巴族 基诺族""".split(), key=len, reverse=True))
HAN = re.compile(r"^[\u3400-\u9fff\U00020000-\U0003134f]+$")


class Ice:
    """雾凇现有词库：同字同音的词、各长度权重分位、每个完整拼音下 base 最常用的词。"""

    def __init__(self, root):
        self.have = collections.defaultdict(dict)  # text -> {code: weight}（8105/base/others，带注音）
        self.low = set()                            # ext/腾讯里只有 100 的词（text）
        self.top2 = {}                              # code -> base/8105 权重最高的两个 (权重, 词)
        self.syl = set()
        lens = collections.defaultdict(list)
        for fn in ("8105", "base", "others", "ext", "tencent"):
            p = os.path.join(root, "cn_dicts", fn + ".dict.yaml")
            if not os.path.exists(p):
                continue
            body = False
            for line in open(p, encoding="utf-8"):
                if not body:
                    body = line.startswith("...")
                    continue
                parts = line.rstrip("\n").split("\t")
                if not parts[0] or parts[0].startswith("#"):
                    continue
                text = parts[0]
                code = parts[1] if len(parts) > 1 and re.match(r"^[a-z ]+$", parts[1]) else None
                w = int(parts[-1]) if parts[-1].isdigit() else 0
                if fn in ("ext", "tencent"):
                    self.low.add(text)
                    continue
                if code is None:
                    continue
                if fn == "8105":
                    self.syl.add(code)
                self.have[text][code] = max(w, self.have[text].get(code, 0))
                if fn in ("8105", "base"):
                    tl = self.top2.setdefault(code, [])
                    tl.append((w, text))
                    tl.sort(reverse=True)
                    del tl[2:]
                    if fn == "base":
                        lens[len(text)].append(w)
        self.pct = {}
        for n, ws in lens.items():
            ws.sort()
            self.pct[n] = {q: ws[int(len(ws) * q / 100)] for q in (50, 60, 75, 90)}

    def p(self, n, q):
        return self.pct.get(min(max(n, 2), 7), self.pct[4])[q]


_pp = None


def pinyin(word):
    global _pp
    if _pp is None:
        from pypinyin import lazy_pinyin, Style
        _pp = (lazy_pinyin, Style)
    lazy_pinyin, Style = _pp
    if word in C.READING_FIX:
        return C.READING_FIX[word]
    # 最长的读音更正前缀（长子县 → 长子 + 县）
    for k in range(len(word) - 1, 1, -1):
        if word[:k] in C.READING_FIX:
            return C.READING_FIX[word[:k]] + " " + pinyin(word[k:])
    s = lazy_pinyin(word, style=Style.NORMAL, errors="ignore", v_to_u=False)
    return " ".join(x.replace("ü", "v") for x in s)


def t2s(text):
    try:
        import opencc
        return opencc.OpenCC("t2s").convert(text)
    except ImportError:
        raise SystemExit("需要 opencc：pip install opencc-python-reimplemented")


class Builder:
    def __init__(self, ice):
        self.ice = ice
        self.out = collections.defaultdict(dict)   # domain -> text -> (code, weight)
        self.tests = collections.defaultdict(list) # domain -> [(text, code)]
        self.seen = {}                              # text -> domain（一个词只进一个领域）
        self.stats = collections.Counter()

    def top_other(self, code, text):
        for w, x in self.ice.top2.get(code, []):
            if x != text:
                return w
        return 0

    def add(self, dom, text, weight, test=True, code=None, trust=False, calib=True):
        """trust：权重来自语料频次（THUOCL DF，与 base 同量纲），不再按同音词压低；
        calib：雾凇 base 里同字同音但权重不到目标 1/4 的（盐城 1、长治 2），用目标权重覆盖（一维词库挂在 base 前面）。"""
        text = text.strip()
        if not text or not HAN.match(text):
            return
        code = code or pinyin(text)
        sy = code.split()
        if len(sy) != len(text) or any(s not in self.ice.syl for s in sy):
            self.stats["bad_reading"] += 1
            return
        if test:
            self.tests[dom].append((text, code))
        if text in self.seen:
            return
        n = len(text)
        w = min(int(weight), self.ice.p(n, 90))
        other = self.top_other(code, text)
        if other > 0 and w >= other * 0.5 and not trust:  # 不抢首选（袁鹰 113 曾压过 base 的 元婴 111）
            w = int(other * 0.5)
            self.stats["capped"] += 1
        w = max(w, 10)
        old = self.ice.have.get(text, {}).get(code)
        if old is not None:
            # 只校准明显偏低的：地名/国名（雾凇 base 里 盐城 1、长治 2、株洲 2）、手工整理的类别，
            # 其它领域只改 base 权重不到 50 的（桔子 2），并截到 75 分位——雾凇 base 有人工调频，不大面积推翻
            ok = calib and n >= 2 and old * 4 <= w and w >= 200
            if ok and dom not in ("geo", "world", "poetry", "spoken", "history", "science"):
                ok = old < 50
                w = min(w, self.ice.p(n, 75))
            if not ok:
                self.stats["already"] += 1
                return
            self.stats["recalibrated"] += 1
        elif text in self.ice.low and w <= 100:
            self.stats["low_skip"] += 1
            return
        self.seen[text] = dom
        self.out[dom][text] = (code, w)


def poetry_lines(cache):
    poems = []  # (title, author, [clauses...])
    # 千家诗在前：同一句有不同署名时（白日依山尽：王之涣 / 朱斌）取通行的说法
    q = json.load(open(fetch(cache, "qianjia.json"), encoding="utf-8"))
    for sec in q.get("content", []):
        for p in sec.get("content", []):
            paras = [x for x in p.get("paragraphs", []) if isinstance(x, str)]
            author = re.sub(r"^[（(][^）)]*[）)]", "", p.get("author", "") or "")
            poems.append((t2s(p.get("chapter", "")), t2s(author), t2s("".join(paras))))
    for fn in ("ts300.json", "sc300.json"):
        for p in json.load(open(fetch(cache, fn), encoding="utf-8")):
            text = "".join(p.get("paragraphs") or [])
            title = p.get("title") or p.get("rhythmic") or ""
            poems.append((t2s(title), t2s(p.get("author", "")), t2s(text)))
    for p in json.load(open(fetch(cache, "shijing.json"), encoding="utf-8")):
        poems.append((p.get("title", ""), "诗经", "".join(p.get("content", []))))
    out = []
    for title, author, text in poems:
        text = re.sub(r"[（(][^）)]*[）)]", "", text)
        # 按句号类切成「联」，联内按逗号切成上下句
        for sent in re.split(r"[。！？；]", text):
            parts = [x for x in re.split(r"[，、：]", sent) if x]
            if len(parts) == 2 and all(4 <= len(x) <= 9 and HAN.match(x) for x in parts):
                out.append((parts[0], parts[1], title, author))
            for x in parts:
                if 4 <= len(x) <= 9 and HAN.match(x):
                    out.append((x, None, title, author))
    return out


def build(root, cache):
    ice = Ice(root)
    b = Builder(ice)
    P = lambda n, q: ice.p(n, q)
    th = {n: thuocl(cache, n) for n in ("diming", "poem", "chengyu", "lishimingren", "medical", "law", "caijing", "IT", "food", "animal", "car")}
    dm = th["diming"]

    # —— 口语
    for w in C.SPOKEN.split():
        b.add("spoken", w, P(len(w), 75))

    # —— 中国地名
    provs = json.load(open(fetch(cache, "mo_provinces.json"), encoding="utf-8"))
    cities = json.load(open(fetch(cache, "mo_cities.json"), encoding="utf-8"))
    areas = json.load(open(fetch(cache, "mo_areas.json"), encoding="utf-8"))
    skip = {"市辖区", "县", "省直辖县级行政区划", "自治区直辖县级行政区划"}
    sfx = ("特别行政区", "维吾尔自治区", "壮族自治区", "回族自治区", "自治区", "省", "市")
    for p in provs:
        name = p["name"]
        b.add("geo", name, dm.get(name, 20000), trust=name in dm)
        for s in sfx:
            if name.endswith(s) and len(name) - len(s) >= 2:
                short = name[:-len(s)]
                b.add("geo", short, dm.get(short, 20000), trust=short in dm)
                break
    for extra in ("台湾省", "香港特别行政区", "澳门特别行政区", "台湾", "香港", "澳门"):
        b.add("geo", extra, dm.get(extra, 20000), trust=extra in dm)
    for c in cities:
        name = c["name"]
        if name in skip:
            continue
        b.add("geo", name, dm.get(name, P(len(name), 75)), trust=name in dm)
        m = re.match(r"^(.{2,})(市|地区|盟)$", name) or re.match(r"^(.{2,}?)(?:%s)+自治州$" % ETHNIC, name)
        if m:
            b.add("geo", m.group(1), dm.get(m.group(1), P(len(m.group(1)), 75)), trust=m.group(1) in dm)
    for a in areas:
        name = a["name"]
        if name in skip:
            continue
        b.add("geo", name, dm.get(name, P(len(name), 60)), trust=name in dm)
        m = re.match(r"^(.{2,})(县|市|区|旗)$", name)
        if m and not re.search(r"(族|自治)", name):
            short = m.group(1)
            b.add("geo", short, dm.get(short, P(len(short), 50)), test=False, calib=False)
    for tok in C.CHINA_NATURE.split():
        w, _, code = tok.partition(":")
        b.add("geo", w, dm.get(w, P(len(w), 75)), code=code.replace("_", " ") or None)

    # —— 世界
    for tok in C.COUNTRIES.split():
        country, capital = tok.split(":")
        b.add("world", country, dm.get(country, P(len(country), 90)), trust=country in dm)
        b.add("world", capital, dm.get(capital, P(len(capital), 75)), trust=capital in dm)
    for w in C.WORLD_CITIES.split():
        b.add("world", w, dm.get(w, P(len(w), 75)), trust=w in dm)

    # —— 诗词：名句（THUOCL 诗词有频次）+ 各诗集全部句子；上下句联想表
    pf = th["poem"]
    couplets = {}
    for first, second, title, author in poetry_lines(cache):
        if second:
            couplets.setdefault(first, (second, title, author))
        else:
            b.add("poetry", first, max(pf.get(first, 0) * 3, P(len(first), 50)), test=False)
    for w, df in pf.items():
        if 4 <= len(w) <= 9 and df >= 1:
            b.add("poetry", w, max(df * 3, P(len(w), 50)), test=False)
    random.seed(7)
    famous = sorted([w for w, df in pf.items() if df >= 30 and 5 <= len(w) <= 7])
    for w in famous:
        b.tests["poetry"].append((w, pinyin(w)))

    # —— 成语
    cy = th["chengyu"]
    for it in json.load(open(fetch(cache, "idiom.json"), encoding="utf-8")):
        w = it.get("word", "")
        if 4 <= len(w) <= 8:
            b.add("idiom", w, cy.get(w, 30), test=w in cy and cy[w] >= 50)
    for w, df in cy.items():
        b.add("idiom", w, max(df, 30), test=df >= 50)

    # —— 历史人文
    for w in C.HISTORY.split() + C.CULTURE.split():
        b.add("history", w, P(len(w), 75))
    for w, df in th["lishimingren"].items():
        if df >= 20 and len(w) >= 2:
            b.add("history", w, df, test=df >= 2000)

    # —— 自然科学
    for w in C.ELEMENTS.split():
        b.add("science", w, 10)
    for w in C.ELEMENT_WORDS.split():
        b.add("science", w, P(len(w), 60))

    # —— 行业术语（THUOCL）
    for dom, src in (("medical", "medical"), ("law", "law"), ("finance", "caijing"), ("it", "IT"), ("food", "food")):
        for w, df in th[src].items():
            if df >= 5 and len(w) >= 2:
                b.add(dom, w, df, test=df >= 3000, trust=True)

    # —— 动植物
    for w, df in th["animal"].items():
        if df >= 5 and len(w) >= 2:
            b.add("nature", w, df, test=df >= 3000, trust=True)
    for w in C.PLANTS.split():
        b.add("nature", w, P(len(w), 60))

    # —— 品牌机构
    for w in C.BRANDS.split():
        w, _, code = w.partition(":")
        b.add("brand", w, P(len(w), 75), code=code.replace("_", " ") or None)
    for w, df in th["car"].items():
        if df >= 50 and len(w) >= 2:
            b.add("brand", w, df, test=False)
    return b, couplets


def write(b, couplets, root):
    out_dir = os.path.join(REPO, "overlay", "data", "yiwei_dicts")
    os.makedirs(out_dir, exist_ok=True)
    total = 0
    for dom, name, desc in DOMAINS:
        rows = sorted(b.out[dom].items(), key=lambda kv: (-kv[1][1], kv[1][0], kv[0]))
        total += len(rows)
        with open(os.path.join(out_dir, "yiwei.%s.dict.yaml" % dom), "w", encoding="utf-8", newline="\n") as f:
            f.write("# 一维输入法 · 分类词库「%s」：%s\n" % (name, desc))
            f.write("# 由 scripts/data_gen/lexicon/build.py 生成，不要手改；来源与许可证见同目录 README.md\n")
            f.write("# 设置 → 词库 →「分类词库」可整体或按领域关闭\n---\nname: yiwei.%s\nversion: \"2026.11.10\"\nsort: by_weight\n...\n" % dom)
            for text, (code, w) in rows:
                f.write("%s\t%s\t%d\n" % (text, code, w))
        print("yiwei.%s: %d" % (dom, len(rows)))
    print("total new/recalibrated entries: %d; stats %s" % (total, dict(b.stats)))
    # 诗句联想表：只收 4–9 字的上下句
    lua = os.path.join(REPO, "overlay", "data", "lua", "yiwei_poetry_data.lua")
    esc = lambda s: s.replace("\\", "\\\\").replace('"', '\\"')
    with open(lua, "w", encoding="utf-8", newline="\n") as f:
        f.write("-- 由 scripts/data_gen/lexicon/build.py 生成（诗句联想表：上句 → {下句, 作者, 题目, 下句首字母}），不要手改\n")
        f.write("-- 来源：chinese-poetry（MIT）唐诗三百首、宋词三百首、千家诗、诗经\nreturn {\n")
        for k in sorted(couplets):
            s, t, a = couplets[k]
            abbr = "".join(x[0] for x in pinyin(s).split())
            f.write('["%s"]={"%s","%s","%s","%s"},\n' % (esc(k), esc(s), esc(a), esc(t[:12]), abbr))
        f.write("}\n")
    print("poetry couplets: %d" % len(couplets))
    # 命中率清单
    with open(os.path.join(REPO, "scripts", "rime_test", "lexicon_coverage.tsv"), "w", encoding="utf-8", newline="\n") as f:
        f.write("# 领域\t词\t全拼（scripts/rime_test/coverage.py 用；由 build.py 生成）\n")
        for dom, _, _ in DOMAINS:
            seen = set()
            items = [x for x in b.tests[dom] if not (x[0] in seen or seen.add(x[0]))]
            random.Random(dom).shuffle(items)
            for text, code in sorted(items[:600]):
                f.write("%s\t%s\t%s\n" % (dom, text, code.replace(" ", "")))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("ice_root")
    ap.add_argument("--cache", default=os.path.join(os.environ.get("TMPDIR", "/tmp"), "yiwei-lexicon-src"))
    a = ap.parse_args()
    b, couplets = build(a.ice_root, a.cache)
    write(b, couplets, a.ice_root)
