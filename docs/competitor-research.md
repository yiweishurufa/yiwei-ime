# 主流中文输入法 Windows 端功能调研（2025–2026）与一维输入法改进建议

> 调研日期：2026-10-08。对象：搜狗输入法、百度输入法、微信输入法（WeType）、QQ 输入法、讯飞输入法、Windows 微软拼音，以及 macOS 端 AIME（艾么，一维的设计参照）。补充一个 2026 年新进者：豆包输入法 Windows 版。
> 方法：以官方更新日志 / 官方帮助页为主，评测与新闻为辅；未打开浏览器。搜索结果里大量 `xxx-sogou.com.cn`、`xunfei-xxx.com.cn` 之类的**仿冒「官网」站**，内容不可信，本文一律未采用。
> 图例：✅ 有　◐ 部分 / 依赖系统 / 需手动配置　❌ 无　**?** 本次未能从可靠来源核实（不代表没有）

---

## 0. 一页结论

- **竞品 2025–26 年的主线是「AI 全家桶 + 账号云端化」**：搜狗 16.x / 20.0 AI 版（7 种 AI 帮写、Word/WPS 光标助手、输入后按「=」调 AI、跨设备剪贴板、口语转书面语），百度「超会写」，微信输入法「=」问 AI（DeepSeek / 混元）、语音实时转写、隔空传送，讯飞主打语音 + 方言，豆包 2026-09 上 Windows 也主打语音。
- **传统效率功能大家早已齐平**：快捷短语、rq/sj 日期、V 模式计算器 / 大写数字、U 模式拆字、模糊音、皮肤、词库同步。**这些在 RIME 生态（雾凇 / 万象 Lua）里几乎都有现成方案**，一维的主要工作是「开好、配好、做 UI」，不是从零写。
- **差异化机会在隐私**：主流输入法都依赖云端联想（等于把按键发给服务器）。Citizen Lab 2023–24 年的研究发现多家厂商云端传输加密有漏洞，QQ 拼音 Windows 版截至 2024-04 仍可被解密；搜狗 PC 版 2026-02 才加上「关闭商业广告」按钮。一维「全本地 + AI 自带 Key、默认关闭 + 无广告、无遥测」本身就是卖点，要在官网和首次启动向导里明说。
- **一维最值得补的短板**（按性价比）：词库自动更新、把雾凇现有 Lua 功能做成可见的开关和速查卡、语法模型、按应用中英自动推荐、游戏 / 全屏免打扰、本地剪贴板历史、模糊音设置界面、librime 自带同步、细胞词库导入、本地 OCR，最后才是离线语音。

---

## 1. 功能对比表

| 功能 | 搜狗 | 百度 | 微信 WeType | QQ | 讯飞 | 微软拼音 | AIME (mac 参考) | **一维（现状）** |
|---|---|---|---|---|---|---|---|---|
| **候选与智能** | | | | | | | | |
| 云联想 / 云候选 | ✅ | ✅ | ? | ✅ | ✅ | ✅ 必应建议（可关） | ❌ 全本机 | ❌（有意不做） |
| 长句 / 整句 | ✅ 自研打字大模型 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ librime | ✅ librime（可加语法模型） |
| 纠错 | ✅ | ✅ 动态纠错 | ✅ 易错音 / 拼写检查 | ? | ✅ 智能纠错 | ◐ | ? | ◐ 雾凇自动纠错 + 错音提示 |
| 模糊音 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ◐ 改配置 | ◐ 改配置，无界面 |
| 简拼 / 超级简拼 | ✅ | ✅ | ? | ✅ | ? | ? | ✅ | ✅ 雾凇简拼 |
| AI 帮写 / 润色 / 翻译 | ✅ 7 种 + 光标助手 | ✅ 超会写 | ◐ 「=」问 AI | ❌ | ? PC 端未见 | ❌（只有系统 Copilot） | ✅ ⌥+空格 | ✅ Alt+空格（流式） |
| 打字中出 AI 候选 | ✅ 「=」AI 技能、续写补全 | ◐ AI 联想续写 | ✅ 「=」问 AI | ❌ | ? | ❌ | ❌ | ❌ |
| **效率** | | | | | | | | |
| 快捷短语 / 常用语 | ✅ | ✅ | ✅ 可设编码 | ✅ | ✅ | ✅ | ✅ 分类 + 编码 | ✅ Alt 面板 |
| 剪贴板历史 | ✅ 16.1 新增；16.5 跨设备 | ? | ✅ 跨设备复制粘贴 | ? | ? | ◐ 系统 Win+V | ? | ❌ |
| 表情 / 颜文字 / 符号大全 | ✅ 候选 emoji、斗图 | ✅ | ◐ 表情推荐 | ✅ | ✅ | ◐ 系统 Win+. | ◐ 符号按字母键 | ◐ 雾凇 emoji + v 符号 |
| rq/sj 日期、计算器 | ✅ rq/sj/xq、V 模式计算 | ✅ sj、V 模式计算器 | ? | ? | ? | ✅ V 模式 | ? | ✅ 雾凇 rq / nl / cC |
| 拆字 / 生僻字 | ✅ U 模式；GB18030-2022 97908 字 | ✅ 拆字（huohuohuo→焱） | ? | ? | ◐ 手写注音 | ✅ U 模式笔画 / 部件 | ? | ✅ 雾凇 uU 拆字反查 |
| 手写 | ✅ | ✅ | ? | ✅ | ✅ 触摸板叠写 | ◐ 系统手写面板 | ❌ | ❌ |
| 语音输入 | ✅ | ✅ | ✅ 实时转写（2.1.3） | ? | ✅ 离线 + 方言 + 跨屏 | ◐ 系统 Win+H | ❌ | ❌ |
| 截图 OCR | ✅ OCR 识图 | ◐ 截图扩展 | ? | ◐ 截屏 | ? | ❌ | ❌ | ❌ |
| 翻译 | ✅ 混元翻译，30+ 语种 | ✅ vf 翻译 / 超会写 | ◐ 问 AI | ? | ✅ 随声译 | ❌ | ✅ | ✅ AI |
| **个性化** | | | | | | | | |
| 皮肤商店 | ✅ 装扮商城 | ✅ | ❌ 极简 | ✅ | ✅ | ❌ | ✅ 官网主题 | ◐ 5 套内置 + 深色 |
| 状态栏自定义 | ✅ 可调透明度 | ✅ | ◐ 可隐藏 | ✅ | ✅ | ◐ IME 工具栏 | ❌ | ❌ 只有托盘图标 |
| 词库同步 | ✅ 账号 | ✅ 账号 | ✅ 账号 | ✅ 账号 | ✅ 账号 | ? | ❌ 本机 | ❌ |
| 细胞词库 / 分类词库 | ✅ | ✅ | ❌ | ✅ | ? | ? | ◐ 词库资源页 | ◐ 可从 Weasel / 文本导入 |
| 隐私模式 | ◐ 宣称有「本地模式」 | ? | ? | ? | ? | ✅ 云建议可关 | ✅ 默认本机、无遥测 | ✅ 本机；AI 自带 Key |
| **体验** | | | | | | | | |
| 状态栏悬浮条 | ✅ | ✅ | ◐ | ✅ | ✅ 另有语音悬浮框 | ◐ | ❌ | ❌ |
| 候选框跟随光标 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ Weasel |
| 游戏 / 全屏免打扰 | ◐ 游戏兼容修复多，未见独立模式 | ◐ 游戏场景自动关闭 I 模式（2016） | ? | ? | ? | ? | ❌ | ❌ |
| 按应用自动切换中英 | ? | ? | ? | ? | ? | ❌ | ✅ 首次扫描应用并推荐 | ◐ 手动黑名单（app_options ascii_mode） |
| 启动速度 / 内存 | 本次未实测 | 未实测 | 未实测 | 未实测 | 未实测 | 未实测 | 官方称单键 p99 0.61 ms（开发机） | 未实测 |
| 广告 / 商业内容 | ◐ 有，2026-02 起可关 | ◐ 历史上有积分、礼品、资讯 | ❌ 无 | 官方称无广告 | 官方称无广告 | ❌ 无 | ❌ 无 | ❌ 无 |
| 最近更新状态 | 活跃（16.9，2026-09-30） | 只修 bug（6.1.13.13，2026-07-01） | 活跃（2.1.4，2026-09-15） | 维护（6.7.6500，2026-06-24） | 维护（3.0.1750，2026-03） | 随系统更新 | 活跃（0.1.6） | nightly |

### 表格依据与说明
- **搜狗**：官方更新日志 https://pinyin.sogou.com/changelog.php （GBK 编码页，已转码阅读）
  - 15.9（2025-09）：续写补全（诗词、名言、邮箱、数学计算式）、候选 emoji、多时机表情推荐
  - 16.1（2026-01）：剪贴板、状态栏透明度
  - 16.1c（2026-02）：7 种 AI 帮写（灵感语录、润色、高情商回复、校对、结构化、翻译、写作）、Word / WPS 里的 AI 光标助手
  - 16.2（2026-02）：医生 / 律师模式、**新增商业广告关闭按钮**
  - 16.3b：按 ↓ 展开多行候选面板，Tab 切换服务
  - 16.5（2026-05）：跨设备复制粘贴
  - 16.8（2026-08）：GB 18030-2022 生僻字 97908 个
  - 更早：15.4d「=」唤起 AI 技能、AI 汪仔快捷搜索；14.8 AI 汪仔、智能体、AI 宠物；15.2 灵犀候选「应用直达 / 网站直达」；15.6 桌面壁纸装扮；10.0 OCR 识图；1.5Beta3 rq / sj / xq 与 V 模式。
  - 20.0 AI 大版本（央广网，2026-01-27）：混元翻译 30+ 语种、口语转书面语、「手机端一键关闭键盘广告，电脑端广告关闭功能预计 2 月上线」。https://tech.cnr.cn/techph/20260127/t20260127_527507799.shtml
  - 官方专题页宣称「本地模式 + 权限精细管理」：https://shurufa.sogou.com/specials
- **百度**：官方更新日志 https://shurufa.baidu.com/update/
  - 2024 年起只有「修复 / 基础优化」，6.1.13.13 发布于 2026-07-01。
  - 超会写：AI 帮写、划词、翻译、纠错、微信聊天高情商回复。
  - 更早的功能：V 模式计算器（1.8）、拆字（1.7.2）、动态纠错（2.3）、以词定字（2.2）、sj 时间（1.0.10）、vf 翻译、I 模式游戏场景自动关闭（3.4）。
  - 历史上有礼品中心、积分、资讯等商业内容（5.1 / 4.x）。
- **微信输入法**：官网 https://z.weixin.qq.com/ （「打字后按 = 获取 DeepSeek / Hunyuan 的 AI 回答」）；Windows 更新日志列表 https://z.weixin.qq.com/web/change-log/windows
  - 2.1.4（2026-09-15）：微信聊天框直接发表情
  - 2.1.3（2026-08-30）：语音输入实时转写到输入框
  - 2.0.0（2026-05）：隔空传送，见界面新闻 https://www.jiemian.com/article/14405782.html
  - 微软商店描述：跨设备复制粘贴、同步词库与常用语、符号配对、符号转换。https://apps.microsoft.com/detail/xpfffp686ndrdz
  - 单个版本的详情页由 JS 渲染，本次没能逐条读到。
- **QQ 输入法**：官网 https://qq.pinyin.cn/
  - 版本 6.7.6500.400，2026-06-24；官网支持系统只写到「XP / WIN7 / WIN8 / WIN10」。
  - 更新日志页是空的。6.6 的内容（截屏、手写快捷键、自定义短语、词库同步）来自转载站，可信度一般。
  - **本次未发现任何 AI 功能。**
- **讯飞**：官网 https://srf.xunfei.cn/ （Windows 3.0.1750）。PC 端功能（语音、23–26 种方言、离线语音、随声译、跨屏输入、触摸板叠写、语音悬浮框）来自 IT 之家 / 新浪的报道 https://finance.sina.com.cn/tech/2020-12-02/doc-iiznezxs4884743.shtml 和下载站说明。15.0 的「AI 键盘」是手机端的（中关村在线 2025-11）；**PC 端是否有 AI 帮写未能核实**。
- **微软拼音**：官方帮助 https://support.microsoft.com/zh-cn/windows/hardware/input-devices/microsoft-simplified-chinese-ime
  - 有：双拼、模糊拼音、「;R」人名输入、U 模式（笔画 / 部件）、V 模式（数字、日期、时间、公式、Unicode / GB18030 码位）、必应文本建议（Win11 需手动开启）、IME 工具栏。
  - 表情、剪贴板、语音、手写都是 Windows 系统级功能（Win+.、Win+V、Win+H、触摸键盘），不属于输入法本身。
- **AIME**：官网 https://aime.zool.app 。⌥+1 常用语面板、主题、本机输入统计（默认关闭）、按应用记住中英、⌥+空格 翻译 / 润色 / 粤语、词库每日自动更新并校验、「组字、候选与个人词频在本机处理，不含遥测」、AI 默认用 Apple 端侧模型，也可以接自己的 OpenAI 兼容接口。
- **豆包输入法 Windows 0.9.0**（2026-09-08，新进者，没放进表格）：右 Alt 语音（离线 / 弱网可用）、大模型联想与纠错、账号同步、跨端互传、无广告。https://news.mydrivers.com/1/1149/1149629.htm
- **启动和内存**：本次没有可信的实测数据，所以不填。建议自己测（见建议 18）。

---

## 2. 可借鉴的改进建议（按「价值 ÷ 难度」排序）

架构说明：**Lua** = librime-lua 脚本（processor / segmentor / translator / filter），放在 `lua/`，通过 schema patch 挂载；**插件** = librime C++ 插件（octagram、predict 等，Weasel 已内置 lua 和 octagram）；**Weasel** = 修改 Weasel 前端 / TSF 或 `weasel.yaml`；**Helper** = C# WPF 的 YiweiHelper；**词库** = dict.yaml 或数据文件。
标 🟢 的是 **RIME 生态已有现成方案**，可以直接用或改。

| # | 建议 | 为什么值得做 | 在我们架构里怎么做 | 难度 |
|---|---|---|---|---|
| 1 | 🟢 **词库每日自动更新，校验后再装** | AIME 的核心卖点之一；雾凇 / 万象词库更新很勤；用户最在意「新词打不出」。竞品靠云词库做到这一点，我们用离线增量做 | Helper 每天拉雾凇 GitHub release（加大陆镜像 mirror.nju.edu.cn），校验 sha256 / 签名，只替换 `cn_dicts/*`，不碰 `*.custom.yaml` 和 userdb。装完调 WeaselDeployer 重新部署，失败就回滚。复用现有 nightly 更新通道 | 低 |
| 2 | 🟢 **把雾凇现有 Lua 功能做成「看得见」的功能**：rq / sj 日期时间、nl 农历、cC 计算器、R+数字 转大写金额、U+码位、uuid、以词定字（`[` `]`）、辅码检字（`` ` ``+部首）、错字错音提示、英文自动大小写 | 竞品的 V / U 模式、日期快捷输入我们**其实已经有**，只是用户不知道。这是零成本补齐功能表 | Lua 已现成（雾凇 `lua/` 目录）。Helper 加「功能开关」页（生成 `rime_ice.custom.yaml` patch 增删 translator / filter）+ 首次启动向导里的「速查卡」+ 托盘菜单「快捷输入速查」。顺便确认 Weasel 自带 librime-lua 能正常加载（打 `rq` 能出日期就说明 Lua 正常） | 低 |
| 3 | 🟢 **语法模型（长句准确率）** | 竞品主打的「整句 / 长句」我们靠语法模型追；雾凇 README 官方推荐 | 插件 librime-octagram（Weasel 已内置）+ 万象 LMDG 模型 `wanxiang-lts-zh-hans.gram`（https://github.com/amzxyz/RIME-LMDG）。Helper 提供「下载增强模型」可选项，因为体积较大，不放进安装包。patch：`grammar/language`、`translator/contextual_suggestions` 等，按雾凇 `others/recipes/grammar` 配方 | 低–中 |
| 4 | **按应用自动切中英：扫描已装应用并推荐** | AIME 的亮点（「终端里打英文，微信里打中文」）。我们已经有手动黑名单，差的是「自动推荐 + 记住每个应用」 | Weasel 已支持 `app_options/<exe>: {ascii_mode, vim_mode, inline_preedit}`（0.16 新增 vim_mode / inline_preedit，应用名不区分大小写）。Helper 扫描开始菜单和 Uninstall 注册表，内置推荐表（Windows Terminal、VS Code、JetBrains、cmd、PowerShell → 英文 + vim_mode；微信、QQ、钉钉、飞书 → 中文），用户确认后写入 `weasel.custom.yaml` 并重新部署 | 低–中 |
| 5 | 🟢 **模糊音 / 纠错设置界面** | 竞品都有勾选式模糊音，RIME 用户却要手改 yaml，门槛很高 | 雾凇 schema 里已经有注释掉的模糊音 `derive` 规则和纠错规则。Helper 做勾选界面（z/zh、c/ch、s/sh、n/l、f/h、an/ang、en/eng、in/ing…），生成 `speller/algebra` patch，再部署 | 低 |
| 6 | 🟢 **生僻字与拆字：大字表、字体回退、拆分注释** | 搜狗 2026-08 拿 GB18030-2022 9.8 万字做宣传；我们的生僻字常常显示成「豆腐块」 | 词库：雾凇自带 41448 字的 Unihan 大字表（默认没开），做成开关。Weasel 的 `style/font_face` 支持按 Unicode 区间回退（0.15 起），预设「遍黑体 / MiSans L3 / 花园明朝」的回退链，Helper 检测是否已装这些字体并提示。拆字：雾凇 `uU` 拆字反查（现成），或者用万象的 `super_comment` 在候选旁显示「〔拆分〕」 | 低 |
| 7 | 🟢 **librime 自带的多设备同步（用户自己的网盘）** | 竞品都用账号云同步；我们不用账号，也能同步到用户自己的 OneDrive、坚果云、Syncthing 目录，**隐私友好又是差异点** | librime 内置：`installation.yaml` 里的 `sync_dir` + `installation_id`，用 Weasel 菜单「用户词典同步」或 `WeaselDeployer /sync` 触发。Helper 负责选目录、设定期同步、展示上次同步时间。常用语和统计数据由 Helper 自己合并到同一目录 | 低 |
| 8 | **游戏 / 全屏免打扰模式** | 输入法在游戏里抢 Shift、弹候选是高频吐槽（搜狗、百度日志里大量游戏兼容修复；社区也反馈微信输入法在 Steam 里不出候选） | Helper：用 `SHQueryUserNotificationState`（QUNS_RUNNING_D3D_FULL_SCREEN / BUSY）或「前台窗口铺满显示器」判断全屏，进入后用 `WeaselServer.exe /ascii` 强制英文（Weasel 0.16 起支持），同时停掉中 / 英切换 toast 和 Alt / Alt+空格 热键，退出全屏后恢复。另有「游戏进程列表」可手动加，写进 app_options | 中 |
| 9 | **本地剪贴板历史（默认关闭）** | 搜狗 16.1 刚加；微信、豆包、搜狗都在推跨设备剪贴板。我们做**只在本机**的版本，体验对齐，隐私更好 | Helper：`AddClipboardFormatListener`，复用 Alt 常用语面板的 UI（加一个「剪贴板」分组，热键如 Alt+V）。跳过带 `ExcludeClipboardContentFromMonitorProcessing` / `CanIncludeInClipboardHistory=0` 格式的内容（密码管理器会设这些格式），可设保留条数 / 时长，可一键清空，加密存在本机。默认关闭 | 中 |
| 10 | 🟢 **表情 / 颜文字 / 符号面板** | 竞品标配；雾凇已有 emoji 候选和 `v` 符号，但没有「面板」 | 雾凇已有：OpenCC emoji 滤镜（`emoji` 开关）+ `symbols_v.yaml`。万象已有：「超级符号库」（按名称输入数千个 Unicode 符号）。颜文字可参考雾凇 PR #920。Helper 加一个分类网格面板（复用常用语面板），数据从这些 yaml 生成 | 低–中 |
| 11 | **细胞词库导入（.scel / .bdict / .qpyd）** | 搜狗、百度、QQ 都有海量专业词库，用户迁移时最常问 | Helper 内置解析器：scel 格式公开，可参考 studyzy/imewlconverter（「深蓝词库转换」，**先确认其许可证**）。转成 `xxx.dict.yaml`，自动加进 `rime_ice.dict.yaml` 的 `import_tables`，再部署。现有「从 Weasel / 文本导入」入口直接扩展 | 中 |
| 12 | **AI 候选 / 续写（「=」触发）** | 搜狗、微信都用「输入后按 =」调 AI，已经成了用户习惯；我们的 AI 现在要先选中文本 | **Lua**：processor 在编码末尾检测「=」，把当前编码转成的首选句 / 上屏前文本交给 Helper。**Helper**：librime-lua 是同步的，不适合在里面跑网络请求，所以由 Helper 拿到文本后流式请求，在光标旁的浮层显示，Tab / 数字键上屏，Esc 取消。复用现有 Alt+空格 的流式管线和自定义指令。默认关闭，只用用户自己的 Key 或本地模型 | 中–高 |
| 13 | **本地截图 OCR 取字** | 搜狗、讯飞都有；图片里的字转文本是高频需求 | Helper：区域截图（WPF 遮罩）→ `Windows.Media.Ocr`（Win10+ 系统自带，离线，需要中文语言包；.NET Framework 4.8 可通过 Microsoft.Windows.SDK.Contracts 调 WinRT）→ 结果上屏、进剪贴板或交给 AI 翻译。**完全不上云** | 中 |
| 14 | **状态栏悬浮条（可选）** | 老用户习惯；搜狗 16.1 刚加透明度调节；可以放「中 / 英、全 / 半、简 / 繁、方案、AI、剪贴板」入口 | Helper：WPF 置顶小窗（WS_EX_NOACTIVATE + TOOLWINDOW），状态从 Weasel IPC 或托盘状态读取。点击切换走 `WeaselServer /ascii` 或 /nascii 和 Rime 选项。可拖动、可调透明度、默认隐藏。复用五套品牌皮肤 | 中 |
| 15 | **隐私 / 无痕模式** | 输错密码、私密聊天时不希望被学进词库或统计；这是我们差异化叙事的「看得见的开关」 | Helper：一键开关（托盘 + 快捷键），暂停输入统计、暂停剪贴板历史、AI 热键禁用。**不学词**：librime 不支持运行时关学习，可做一个 `enable_user_dict: false` 的影子方案，用 schema 切换实现（或 `translator/enable_user_dict` patch + 重新部署，但较慢）。密码框：确认 Weasel / TSF 在 password InputScope 下的行为，必要时 Helper 检测后强制英文 | 中 |
| 16 | 🟢 **候选右键：固顶 / 删词 / 以词定字提示** | 微信 1.4.1 刚加「右键固定到首位或删除」；RIME 用户常不知道怎么删错词 | librime 已支持删除用户词（Ctrl+Del / Shift+Del）；万象有「手动排序」Lua 可以实现固顶。先低成本做：在候选框 tips / 速查卡里告诉用户删词快捷键。再做右键菜单：需要改 Weasel UI 的鼠标事件，发对应按键或 Lua 指令 | 低（提示）/ 中（右键菜单） |
| 17 | **离线语音输入** | 讯飞、豆包、微信、搜狗 2025–26 都在押语音；「离线、不上传」是我们能做的差异化 | 第一步：热键直接调系统 Win+H（低成本）。第二步：Helper 集成 sherpa-onnx + 本地中文模型（如 SenseVoice / Paraformer），按住右 Alt 说话，流式上屏，可选 AI 把口语转成书面语（复用润色）。模型体积大，做成可选下载 | 高 |
| 18 | **性能基准与「轻」的证据** | AIME 公开了「单键 p99 0.61 ms」；我们宣传轻快需要数据，竞品不公开 | CI 或脚本：用 librime API 批量回放按键测延迟；测 WeaselServer、Helper 的常驻内存和冷启动时间；同机对比搜狗、微软拼音。结果写进官网和 README | 低–中 |

**排序理由**：1–7 基本是「打开现成能力 + 做一层 UI」，体验提升立竿见影；8–11 是 Helper 侧中等工程，补齐竞品的日常功能；12–15 需要新的交互或跨进程协作；16–18 是锦上添花或长线投入（语音价值很高，但工程量最大）。

### RIME 生态现成方案速查
- **雾凇 rime-ice**（https://github.com/iDvel/rime-ice ）：rq / sj 日期（双拼用 date）、nl / N 农历、cC 计算器、U Unicode、uuid、R 大写金额、uU 拆字反查（radical_pinyin）、以词定字、辅码检字、错字错音提示、英文自动大小写、emoji、模糊音、自动纠错、繁简、词汇别名、41448 字大字表、语法模型配方（`others/recipes/grammar`）。
- **万象 rime-wanxiang**（https://github.com/amzxyz/rime-wanxiang ）：计算器、超级注释（含拆分 `chaifen`、纠错提示）、符号包裹、动态时间戳、超级符号库、声调辅助筛选、Pro 版辅助码、手动排序、tips、快符；LMDG 语法模型；另有不带 Lua 的 Pure 版（兼容 Win7）。
- **librime 内置**：uniquifier（候选去重 filter）、simplifier（OpenCC，用于 emoji 和繁简）、用户词典同步（sync_dir）、简拼 / 模糊音（speller algebra）。
- **librime 插件**：octagram（语法模型，Weasel 已内置）、lua（已内置）、predict（联想预测，需确认 Weasel 发行版是否带）。
- **Weasel 0.15–0.17 可直接用的配置**：`app_options`（ascii_mode / vim_mode / inline_preedit）、`global_ascii`、`style/color_scheme_dark`（深色模式自动切）、`ascii_tip_follow_cursor`、`show_notifications(_time)`、按 Unicode 区间的字体回退、`WeaselServer.exe /ascii` 和 `/nascii`、`WeaselSetup /userdir:`。来源：https://github.com/rime/weasel/releases

---

## 3. 不建议做的

- **云输入 / 云联想（把按键上传）**：本质上是把键盘记录交给服务器。Citizen Lab 测了 9 家厂商，只有华为的传输没发现问题；QQ 拼音（含 Windows 版）截至 2024-04-01 仍能被解密，百度的加密仍有弱点；搜狗 2023 年被曝后才改用 TLS（Windows ≥ 13.7）。这和一维的定位直接冲突。
- **广告、资讯、弹窗、「灵犀候选」式应用直达 / 网站直达**：属于商业导流（搜狗 15.2；百度历史上的礼品中心和积分）。
- **桌面宠物、AI 汪仔式智能体、装扮商城（桌面壁纸 / 图标）、PDF 编辑器、AI 宠物**：臃肿，偏离输入法本职（搜狗 14.1 / 14.8 / 15.6）。
- **强制账号登录、遥测、把输入统计上传**：AIME 明确「不含遥测」，我们也应该如此；统计只放本机，可以清空。
- **在线斗图 / 表情包搜索、AI 表情生成**：需要把输入内容发给第三方，收益小，隐私代价大。
- **默认开启云端 AI**：AI 继续保持「默认关闭 + 用户自带 Key / 本地模型 + 只处理选中或明确触发的文本」。
- **自建跨设备云剪贴板 / 隔空传送**：除非做端到端加密，否则不做；用建议 7 的「用户自有网盘同步」替代。

---

## 4. 隐私差异化：可以对外讲的话（附出处）

1. 「**你打的每个字都留在本机。**」组字、候选、词频都由 librime 在本地计算，没有云联想。对照：主流输入法依靠云端联想，Citizen Lab 称这类功能「can function as vectors for surveillance and essentially behave as keyloggers」。https://citizenlab.ca/research/vulnerabilities-across-keyboard-apps-reveal-keystrokes-to-network-eavesdroppers/chinese-keyboard-app-vulnerabilities-explained/
2. 「**没有广告，从来没有。**」对照：搜狗 PC 版直到 16.2（2026-02-27）才「新增商业广告关闭按钮」。https://pinyin.sogou.com/changelog.php
3. 「**AI 只在你按下快捷键时工作，用你自己的 Key。**」对照：竞品的 AI 接的是厂商自己的云（混元、文心、DeepSeek）。
4. 「**同步走你自己的网盘，不需要账号。**」（建议 7）
5. 「**开源，可审计。**」基于 Weasel（GPL-3）、librime、雾凇（GPL-3）。
6. 可以引用的权威报道：Citizen Lab 2023 搜狗报告 https://citizenlab.ca/research/vulnerabilities-in-sogou-keyboard-encryption ；2024 九厂商报告 https://citizenlab.ca/research/vulnerabilities-across-keyboard-apps-reveal-keystrokes-to-network-eavesdroppers ；EFF 解读 https://www.eff.org/deeplinks/2023/08/vulnerability-tencents-sogou-chinese-keyboard-can-leak-text-input-real-time ；MIT Technology Review https://www.technologyreview.com/2024/04/24/1091740/chinese-keyboard-app-security-encryption 。注意：报告的结论是「传输加密有漏洞」，而且多数已经修复，对外表述要准确，不要说成「某某输入法在窃取数据」。

---

## 5. 未能核实 / 待补

- 表格里所有 **?** 项，尤其是：微信输入法 Windows 每个版本的详细日志（页面靠 JS 渲染）、QQ 输入法 PC 的完整功能、讯飞 PC 端的 AI 功能、各家的游戏模式和按应用中英切换。如果需要，可以用浏览器逐页核实，或者装机实测。
- 启动速度和内存：没有可信的公开数据，见建议 18。
- Weasel 在密码框（password InputScope）里的默认行为、librime-predict 是否随 Weasel 发行：需要在 Windows 上实测。
- imewlconverter 的许可证：需要确认后再决定是集成还是自己写解析器。
