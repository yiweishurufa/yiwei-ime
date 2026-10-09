# 一维输入法 · 下一步优化打磨方案（2026-10-08）

> 在 `competitor-research.md` 基础上，补充 2026 年 6–10 月的新动态。

## 一、行业新变化（2026 下半年）

| 动态 | 时间 | 对一维的含义 |
|---|---|---|
| 豆包输入法 Windows 版 v0.9.0 上线：长按右 Alt 语音、离线语音、大模型长句补全、随打随纠、跨设备「超级互传」 | 2026-09-08 | **语音输入成为 PC 输入法标配**，右 Alt 长按已成用户心智 |
| 豆包 Windows 版补齐双拼、五笔 | 2026-08 | 大厂 AI 输入法在追基础能力，基础稳定性仍是老用户迁移门槛 |
| 搜狗「重做」：AI 服务总入口，8 项 AI 能力集中；20.0 主打 AI 语音 / 翻译 / 打字模型 | 2026-01 / 08 | AI 入口集中化；但广告问题仍是最大槽点 |
| 腾讯 Chatterfly AI 输入法内测（Win / Mac） | 2026-09-18 | 智谱、千问、阿里、腾讯都在做 AI 输入法，**纯 AI 赛道已拥挤** |
| 微信输入法：语音去口水词、自动标点分段、跨端复制 | 2026 全年 | 「去口水词 + 自动标点」是语音输入的及格线 |
| 素言 SuYan（RIME 内核）走红：剪贴板历史、截图、快捷中英切换，主打本地 | 2026-04 | **与一维定位最接近的竞品**，剪贴板历史是其卖点 |
| WeaselTune 小狼毫图形配置工具、万象拼音 v17（Lite 版、超级注释、候选反查筛选、手动排序、tips） | 2026-08 / 10 | RIME 用户最痛的是「配置门槛」，图形化是刚需 |
| 小狼毫无障碍（NVDA）PR 已合并，仅 nightly 可用 | 2026-04 | 一维可率先发布读屏支持，几乎零竞争 |
| 开源离线语音工具涌现：VoiceSnap、SenseVox、VoxType 等，基于 sherpa-onnx + SenseVoice / Paraformer / FireRedASR2 | 2025-12 起 | 本地语音技术成熟：模型 150–240 MB，CPU 可实时 |

**结论：** 大厂在比 AI 云能力，一维不跟。一维的打法是「**本地 AI + 零打扰 + 开箱即用的 RIME**」，把大厂刚做成标配的语音、剪贴板、纠错，用离线方式做出来。

## 二、优先级方案

### P0 · 下个版本必做（1–2 周）

1. **Windows 真机验收**：托盘图标、静默升级 / UAC、模糊音、大字表部署时长、语法模型内存。建一份 `docs/qa-checklist.md`，每版发布前跑一遍。
2. **国内下载可用性**：安装包、词库、语法模型都托管在 GitHub。增加镜像链（ghfast / ghproxy 类代理 + jsDelivr + 自建对象存储任选），下载器按顺序回退并记录成功源。这是国内用户的第一道坎。
3. **首次部署体验**：部署时显示进度与预计时间，大字表 / 语法模型改为「首次空闲时后台部署」，避免装完卡住。
4. **崩溃与日志自助包**：托盘「导出诊断信息」一键打包 rime 日志、配置、版本号（不含词频），方便用户提 Issue。

### P1 · 差异化主力（2–6 周）

5. **离线语音输入（最重要）**
   - 引擎：sherpa-onnx C API + SenseVoice-Small int8（约 150–240 MB，按需下载，与语法模型同一下载器）。可选 Paraformer 中文、FireRedASR2。
   - 交互：长按**右 Ctrl**（或可选右 Alt，注意与一维 Alt 面板冲突）说话，松开上屏；Esc 取消；悬浮胶囊显示音量与时长。
   - 后处理：本地去口水词（嗯、啊、呃）、CT-Transformer 标点、中英之间自动空格；可选「AI 整理」走用户自己的接口。
   - 保护剪贴板：用 TSF 直接上屏，失败才回退剪贴板粘贴并恢复原内容。
   - 卖点：「豆包同款按住说话，但声音不出电脑。」
6. **剪贴板历史**（对标素言、微信）：最近 50 条，只存本机、可设保留时长、默认排除密码管理器与「隐私」窗口；Alt 面板新增一页，按 A S D 上屏。
7. **图形化配置补全**（对标 WeaselTune）：
   - 自定义词条编辑器：挂载 `rime_ice.extended.dict.yaml`，不改原词库，上游更新永不冲突。
   - 候选手动排序 / 置顶 / 删词（右键候选或快捷键）。
   - 快捷键冲突检测（F4、Ctrl+`、Shift 等）。
8. **无障碍读屏**：合入上游 NVDA 无障碍 PR，发布说明里明确写「支持 NVDA / 讲述人」。

8.5 **括号自动配对**（用户提议，✅ 0.1.0.20 起已做基础版：`yiwei_autopair.lua` + 助手补发 ←，设置页可关；包裹选中文字、逐类开关待做）：输入 （ 【 《 “ ‘ 「 时自动补全右半边，光标停在中间；选中文字时输入左括号则包裹选中内容。实现：punctuator 输出成对符号 + 一维助手补发一次 ←；终端、编辑器、游戏默认关闭（按应用规则）。设置页可单独开关每种括号。

8.6 **Shift 直接提交英文**（用户提议）：拼音状态下打了一串字母，按 Shift 把原字母上屏并切到英文。写死 `ascii_composer/switch_key: {Shift_L: commit_code, Shift_R: commit_code}`，设置页可选「提交原文 / 提交首选汉字 / 清空」及左右 Shift 分别设置；真机验证与 Alt 面板、Ctrl+Shift 快捷键不冲突。

### P2 · 打磨与口碑（6 周以后）

9. **本地纠错**：利用万象语法模型做「声母 / 韵母相邻键」纠错候选（如 `shoujo` → 手机），全部本地。
10. **双拼辅助码**：提供万象 Pro 辅助码方案作为可选方案包，覆盖进阶用户。
11. **局域网互传**（替代大厂「跨端复制」）：同一 Wi-Fi 下与手机通过网页 / 二维码互传文字，不经云端；或与 Syncthing 同步目录联动。
12. **本地小模型 AI**：在 AI 设置中加「本机模式」一键对接 Ollama / llama.cpp（Qwen 1.5B 级），翻译润色完全离线。
13. **输入统计年报**：年底生成本机年度报告卡片，可分享（不上传），做口碑传播。
14. **主题市场**：主题导出为 `yiwei-ime://theme` 链接 + GitHub Discussions 主题帖，社区共建，不做付费皮肤。

## 三、不做的事

- 云联想、云输入、默认开启的云 AI。
- 账号体系、强制登录、厂商云同步。
- 广告、资讯、弹窗、桌面萌宠类功能。
- 和大厂比「AI 帮写入口数量」。

## 四、对外一句话

> **一维：按住说话、随手剪贴、AI 润色，样样都有；你的字和声音，一个都不出电脑。**

## 五、建议节奏

| 版本 | 内容 |
|---|---|
| 0.1.1 | P0 全部 + 剪贴板历史 |
| 0.2.0 | 离线语音输入 + 词条编辑器 + 无障碍 |
| 0.3.0 | 本地纠错、局域网互传、本机 AI 模式 |

## 参考来源

- 豆包输入法 Windows 版上线：https://news.qq.com/rain/a/20260908A0E01D00 ；https://www.chooseai.net/news/6745/
- 豆包补齐双拼五笔：https://post.smzdm.com/p/avgrk447/
- 搜狗重做：https://tech.ifeng.com/c/8w0PfNzU2oZ
- 腾讯 Chatterfly：https://www.thepaper.cn/newsDetail_forward_34172830
- 微信 / 豆包 / 千问语音输入：https://www.36kr.com/p/3866916399528838
- 素言等无广告输入法：https://www.toutiao.com/article/7630674708198949416/
- WeaselTune：https://mianao.info/weaseltune-gui-config-tool-for-rime-weasel/
- 万象拼音 v17：https://newreleases.io/project/github/amzxyz/rime-wanxiang/release/v17.7.0
- 小狼毫无障碍：https://nvdacn.com/index.php/archives/1517/
- 离线语音：https://github.com/vorojar/VoiceSnap ；https://github.com/dapanggougou/sensevox ；https://github.com/melody0709/VoxType ；https://github.com/k2-fsa/sherpa-onnx

## 六、为安卓版做准备（现在就要守的规矩）

- **底座选型**：同文 Trime（GPL-3，RIME 官方安卓前端）或 fcitx5-android（LGPL/GPL）；两者都能直接吃 RIME 方案，雾凇、语法模型、大字表可原样复用。倾向 fcitx5-android（键盘与插件体系更现代），需再评估。
- **数据层平台无关**：`overlay/data` 只放 RIME 标准 YAML / 词库 / Lua，不夹 Windows 专属字段；Windows 专属配置只进 `weasel.custom.yaml`。
- **皮肤单一来源**：`scripts/brand_schemes.py` 作为五色主题的唯一定义，后续同时生成小狼毫配色和 Trime/fcitx5 主题。
- **用户数据可互通**：常用语、AI 动作、应用规则改为平台无关的 JSON（带版本号），网盘同步目录结构与 RIME `sync_dir` 一致，手机端用同一网盘即可互通词频。
- **核心逻辑可移植**：常用语、AI 请求、词库更新/校验、语法模型下载的逻辑写清接口与测试用例，安卓端用 Kotlin 重写时照着测。
- **语音选型兼容**：离线语音统一选 sherpa-onnx（Windows/Android 都有官方支持），模型同一份。
- **品牌与标识**：提前占好包名（如 `cc.yiwei.ime`）与应用商店名称。
