#!/usr/bin/env python3
"""一维输入法 · 整句准确率评测（词库/语法模型改动前后对比用）。

思路同 gaboolic/rime-schema-compare：把语料按标点切成短句，用 pypinyin 转成无调全拼，逐键打进
与安装包相同的 rime_ice 方案（含一维 Lua），看首选是否和原句完全一致（句子正确率），
以及首选与原句的编辑距离（文字正确率）。不上屏、不学习，每句之间清空。

  python scripts/rime_test/accuracy.py corpus <out.tsv> [--limit 2000]
      下载 rime-schema-compare 的公开语料（知乎热榜 2026-08-30，评测用，不随仓库分发），
      只留 2–20 个汉字的纯中文短句，生成「原句\\t全拼」。
  python scripts/rime_test/accuracy.py run <shared_dir> <user_dir> <in.tsv> <out.tsv> [--start N --count M]
      分段跑（每段一个进程，便于在 2 分钟限时的环境里跑完），结果追加到 out.tsv。
  python scripts/rime_test/accuracy.py summary <out.tsv> [<out2.tsv> ...]

需要：librime + librime-lua（同 run.py）；pypinyin（pip install pypinyin，仅生成语料时用）。
同一份语料、同一个 librime 下比较两份词库才有意义；拼音由 pypinyin 生成，多音字可能注错，两边同样受影响。
"""
import argparse
import os
import re
import sys
import time
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
CORPUS_URL = "https://raw.githubusercontent.com/gaboolic/rime-schema-compare/main/data/corpus/zhihu_hotlist_2026-08-30.txt"
HAN = re.compile(r"^[\u4e00-\u9fff]+$")


def make_corpus(out, limit, url=CORPUS_URL):
    from pypinyin import lazy_pinyin, Style
    text = urllib.request.urlopen(url, timeout=60).read().decode("utf-8")
    rows, seen = [], set()
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith("链接："):
            continue
        for part in re.split(r"[^\u4e00-\u9fff]+", line):
            if 2 <= len(part) <= 20 and HAN.match(part) and part not in seen:
                seen.add(part)
                code = "".join(lazy_pinyin(part, style=Style.NORMAL, v_to_u=False, neutral_tone_with_five=False))
                code = code.replace("ü", "v")
                if re.match(r"^[a-z]+$", code):
                    rows.append((part, code))
    rows = rows[:limit]
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        for s, c in rows:
            f.write("%s\t%s\n" % (s, c))
    print("corpus: %d sentences -> %s" % (len(rows), out))


def lev(a, b):
    prev = list(range(len(b) + 1))
    for i, x in enumerate(a, 1):
        cur = [i]
        for j, y in enumerate(b, 1):
            cur.append(min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (x != y)))
        prev = cur
    return prev[-1]


def run(shared, user, src, out, start, count):
    from rime import Rime, default_lib
    rows = [l.rstrip("\n").split("\t") for l in open(src, encoding="utf-8") if "\t" in l]
    rows = rows[start:start + count] if count else rows[start:]
    os.makedirs(user, exist_ok=True)
    os.chdir(user)
    t0 = time.time()
    r = Rime(default_lib(), shared, user)
    r.deploy(True)
    r.open("rime_ice")
    t1 = time.time()
    with open(out, "a", encoding="utf-8", newline="\n") as f:
        for s, code in rows:
            r.type(code)
            page = r.context()["page"]
            r.clear()
            got = page[0] if page else ""
            f.write("%s\t%s\t%s\n" % (s, code, got))
    r.close()
    print("ran %d sentences (deploy %.0fs, typing %.0fs)" % (len(rows), t1 - t0, time.time() - t1))


def summary(paths):
    for p in paths:
        n = ok = chars = dist = 0
        for line in open(p, encoding="utf-8"):
            parts = line.rstrip("\n").split("\t")
            if len(parts) < 3:
                continue
            s, _, got = parts[:3]
            n += 1
            ok += s == got
            chars += len(s)
            dist += min(len(s), lev(s, got))
        if n:
            print("%s: 句子正确率 %.2f%% (%d/%d)，文字正确率 %.2f%%" % (p, 100.0 * ok / n, ok, n, 100.0 * (chars - dist) / chars))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("corpus"); p.add_argument("out"); p.add_argument("--limit", type=int, default=2000); p.add_argument("--url", default=CORPUS_URL)
    p = sub.add_parser("run"); p.add_argument("shared"); p.add_argument("user"); p.add_argument("src"); p.add_argument("out")
    p.add_argument("--start", type=int, default=0); p.add_argument("--count", type=int, default=0)
    p = sub.add_parser("summary"); p.add_argument("paths", nargs="+")
    a = ap.parse_args()
    if a.cmd == "corpus":
        make_corpus(a.out, a.limit, a.url)
    elif a.cmd == "run":
        run(a.shared, a.user, a.src, a.out, a.start, a.count)
    else:
        summary(a.paths)
