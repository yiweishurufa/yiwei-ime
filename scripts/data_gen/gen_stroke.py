#!/usr/bin/env python3
"""生成 overlay/data/yiwei_stroke.dict.yaml（W4 笔画输入）。

  python scripts/data_gen/gen_stroke.py <rime-stroke 的 stroke.dict.yaml> <雾凇 cn_dicts 目录>

只留单字：雾凇 8105 字表（带字频）+ 41448 大字表（字频 0，生僻字正是笔画输入的主要用途）。
笔画数据来自 rime/rime-stroke（LGPL-3.0），h 横 s 竖 p 撇 n 点/捺 z 折。
"""
import os
import re
import sys

src, cn = sys.argv[1], sys.argv[2]
weight = {}
for name in ("8105", "41448"):
    for line in open(os.path.join(cn, name + ".dict.yaml"), encoding="utf-8"):
        m = re.match(r"^(\S)\t[a-z]+(?:\t(\d+))?", line)
        if m:
            w = int(m.group(2) or 0)
            weight[m.group(1)] = max(weight.get(m.group(1), 0), w)
rows, seen = [], set()
body = False
for line in open(src, encoding="utf-8"):
    if line.startswith("..."):
        body = True
        continue
    if not body:
        continue
    p = line.rstrip("\n").split("\t")
    if len(p) < 2 or len(p[0]) != 1 or p[0] not in weight or not re.fullmatch(r"[hspnz]+", p[1]):
        continue
    if (p[0], p[1]) in seen:
        continue
    seen.add((p[0], p[1]))
    rows.append((p[0], p[1], weight[p[0]]))
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "overlay", "data", "yiwei_stroke.dict.yaml")
with open(out, "w", encoding="utf-8", newline="\n") as f:
    f.write("# 一维输入法 · 笔画输入（W4）：u + 笔画，h 横 s 竖 p 撇 n 点 z 折\n")
    f.write("# 由 scripts/data_gen/gen_stroke.py 生成；笔画数据 rime/rime-stroke（LGPL-3.0），字频来自雾凇拼音\n")
    f.write("---\nname: yiwei_stroke\nversion: \"1\"\nsort: by_weight\nuse_preset_vocabulary: false\n...\n")
    for c, code, w in rows:
        f.write("%s\t%s\t%d\n" % (c, code, w))
print(len(rows), "rows ->", os.path.normpath(out))
