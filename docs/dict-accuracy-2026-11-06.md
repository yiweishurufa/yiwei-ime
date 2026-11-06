# 词库整句准确率评测（2026-11-06）

目的：决定要不要把雾凇词频换成白霜（rime-frost，GPL-3.0）。结论：**这次不换**，换后整句准确率没有提高。

## 方法

- 脚本：`scripts/rime_test/accuracy.py`（思路同 gaboolic/rime-schema-compare）。
- 语料：rime-schema-compare 公开的知乎热榜语料（2026-08-30）按标点切开，取前 2000 个 2–20 字的纯中文短句；
  拼音由 pypinyin 生成（无调全拼，多音字可能注错，两边同样受影响）。语料不随仓库分发，脚本运行时下载。
- 引擎：Linux librime 1.13.1 + librime-lua（与 CI 的 rime_test 相同），方案为安装包同款 rime_ice（含一维 Lua，含本次的拼音纠错）。
- 判定：逐键打完整句全拼，首选与原句完全一致记为正确；另算文字正确率（编辑距离）。不上屏、不学习。
- 不含语法模型（默认安装没有模型）。

## 结果

| 词库 | 句子正确率（2000 句） | 文字正确率 | 前 700 句 |
|---|---|---|---|
| **现状：雾凇（含腾讯词向量，默认「完整」）** | **55.95%** | **86.95%** | 58.71% |
| 白霜 cn_dicts（8105/base/ext/41448，不挂腾讯；others 保留雾凇） | 55.80% | 86.71% | 57.29% |
| 白霜 + 白霜的腾讯词库 | — | — | 57.57% |
| 雾凇不挂腾讯（「标准」） | — | — | 53.29% |
| 雾凇 + grammar non_collocation_penalty -4（无模型文件） | — | — | 59.71% |
| 雾凇 / 白霜 + 白霜方案的 zh-moqi 小模型 + penalty -4 | — | — | 59.86% / 59.86% |

（“—” 为时间所限只跑了前 700 句。）

## 解读

- 在雾凇方案里直接换白霜词频，不升反降（-0.15 个点，2000 句）。公开评测里白霜领先（61.2% vs 53.3%），
  主要来自白霜方案自带的 `grammar/non_collocation_penalty: -4`（组词惩罚），把同样的设置给雾凇词库，两者打平。
- 去掉腾讯词向量（「标准」）会掉约 5 个点，所以「默认不挂腾讯」这次也不改；「标准」仍只建议老电脑用。
- 下一步更值得做的是语法模型（万象 LTS）：公开评测 +5~6 个点；本次已让首次引导推荐下载。

## 复现

```bash
python3 scripts/rime_test/run.py prepare <rime-ice full.zip> /tmp/rt/shared
python3 scripts/rime_test/accuracy.py corpus /tmp/corpus.tsv --limit 2000
python3 scripts/rime_test/accuracy.py run /tmp/rt/shared /tmp/acc_user /tmp/corpus.tsv /tmp/out.tsv --start 0 --count 800   # 分段跑
python3 scripts/rime_test/accuracy.py summary /tmp/out.tsv
```
白霜对比：把白霜 master（2026-11-04）的 `cn_dicts/{8105,41448,base,ext,tencent}.dict.yaml` 覆盖到共享目录 `cn_dicts/`，按需注释 `rime_ice.dict.yaml` 里的 tencent 行后重跑。
