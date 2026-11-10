# 一维分类词库（yiwei.*.dict.yaml）

由 `scripts/data_gen/lexicon/build.py` 从下列来源生成，不要手改；改了来源或规则就重新运行脚本：

```bash
pip install pypinyin opencc-python-reimplemented
python3 scripts/data_gen/lexicon/build.py <雾凇数据目录（含 cn_dicts/）>
```

挂载：安装包里的 `rime_ice.dict.yaml` 在 `cn_dicts/base` 前面 `import_tables` 这些词典（同字同音以上面的为准，词典里只有新词和有意校准的词）（`scripts/customize_data.py` 的
`mount_domain_dicts`；词库更新后由助手 `helper/Features/DomainDicts.cs` 补回）。词典主名仍是 `rime_ice`，用户词库不受影响。
设置 → 词库 →「分类词库」可整体或按领域关闭（用户文件夹放同名空词典）。

| 文件 | 领域 | 来源 |
|---|---|---|
| yiwei.spoken | 口语短句 | 一维手写 |
| yiwei.geo | 中国地名（省市县、山川河流、名胜） | modood/Administrative-divisions-of-China（WTFPL）；THUOCL 地名词频；一维整理的山川名胜与多音字地名读音 |
| yiwei.world | 国家、首都、世界城市 | 一维整理（事实名称）；THUOCL 地名词频 |
| yiwei.poetry | 诗词名句 | chinese-poetry（MIT）：唐诗三百首、宋词三百首、千家诗、诗经；THUOCL 诗词（MIT） |
| yiwei.idiom | 成语 | chinese-xinhua（MIT）；THUOCL 成语（MIT） |
| yiwei.history | 历史人文、节日节气 | 一维整理；THUOCL 历史名人（MIT） |
| yiwei.science | 化学元素、数理化天文 | 一维整理 |
| yiwei.medical / law / finance / it / food | 行业术语 | THUOCL 医学、法律、财经、IT、饮食（MIT） |
| yiwei.nature | 动植物 | THUOCL 动物（MIT）；一维整理的植物 |
| yiwei.brand | 品牌机构 | 一维整理；THUOCL 汽车（MIT） |
| ../lua/yiwei_poetry_data.lua | 诗句联想表 | chinese-poetry（MIT） |

许可证原文：THUOCL https://github.com/thunlp/THUOCL/blob/master/LICENSE （MIT，© 2018 THUNLP）；
chinese-poetry https://github.com/chinese-poetry/chinese-poetry/blob/master/LICENSE （MIT）；
chinese-xinhua https://github.com/pwxcoo/chinese-xinhua/blob/master/LICENSE （MIT，© 2018 PWXCOO）；
Administrative-divisions-of-China https://github.com/modood/Administrative-divisions-of-China （WTFPL）。

**没有使用**搜狗、百度、QQ、讯飞等商业输入法的细胞词库词条（各平台用户协议限制再分发），只参考了它们的分类方式。

权重规则（详见 build.py 文件头）：THUOCL 来源直接用 DF 词频（与雾凇 base 同一量纲），按词长截到 base 同长度词的 90 分位；
手工整理的按类别给 base 50–75 分位的一档；同字同音已在雾凇 8105/base 的一般不收，只校准明显偏低的（base 里 盐城 1、长治 2、株洲 2 → 按 THUOCL 词频；其它领域只改 base 权重不到 50 的）；同一完整拼音下 base 已有更常用的词时，
新词权重压到它的一半以下（不抢首选）。
