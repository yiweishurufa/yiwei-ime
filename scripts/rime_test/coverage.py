#!/usr/bin/env python3
"""一维输入法 · 领域词命中率（分类词库改动前后对比用）。

  python3 scripts/rime_test/coverage.py run <shared_dir> <user_dir> <out.tsv> [--list lexicon_coverage.tsv]
  python3 scripts/rime_test/coverage.py summary <before.tsv> [<after.tsv>]

逐个打 lexicon_coverage.tsv 里每个词的全拼（由 scripts/data_gen/lexicon/build.py 生成：国家首都、省市县、
诗词名句、成语、历史人物、术语……），记录首选和前 3 是否就是这个词。不上屏、不学习。
"""
import argparse, collections, os, sys, time
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


def run(shared, user, out, lst):
    from rime import Rime, default_lib
    rows = [l.rstrip("\n").split("\t") for l in open(lst, encoding="utf-8") if not l.startswith("#") and l.strip()]
    os.makedirs(user, exist_ok=True)
    os.chdir(user)
    r = Rime(default_lib(), shared, user)
    t0 = time.time()
    r.deploy(True)
    r.open("rime_ice")
    t1 = time.time()
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        for dom, word, code in rows:
            r.type(code)
            page = r.context()["page"][:3]
            r.clear()
            f.write("%s\t%s\t%s\t%d\t%d\n" % (dom, word, code, int(bool(page) and page[0] == word), int(word in page)))
    r.close()
    print("ran %d words (deploy %.0fs, typing %.0fs)" % (len(rows), t1 - t0, time.time() - t1))


def load(p):
    d = collections.defaultdict(lambda: [0, 0, 0])
    for l in open(p, encoding="utf-8"):
        dom, _, _, t1, t3 = l.rstrip("\n").split("\t")
        for k in (dom, "全部"):
            d[k][0] += 1; d[k][1] += int(t1); d[k][2] += int(t3)
    return d


def summary(paths):
    ds = [load(p) for p in paths]
    print("| 领域 | 词数 | " + " | ".join("首选 / 前3（%s）" % os.path.basename(p) for p in paths) + " |")
    print("|---|---|" + "---|" * len(paths))
    for k in sorted(ds[0], key=lambda k: (k == "全部", k)):
        n = ds[0][k][0]
        print("| %s | %d | " % (k, n) + " | ".join("%.1f%% / %.1f%%" % (100.0 * d[k][1] / n, 100.0 * d[k][2] / n) for d in ds) + " |")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("run"); p.add_argument("shared"); p.add_argument("user"); p.add_argument("out")
    p.add_argument("--list", default=os.path.join(HERE, "lexicon_coverage.tsv"))
    p = sub.add_parser("summary"); p.add_argument("paths", nargs="+")
    a = ap.parse_args()
    if a.cmd == "run":
        run(a.shared, a.user, a.out, a.list)
    else:
        summary(a.paths)
