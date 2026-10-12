# 安卓版：对标搜狗 + OPPO（ColorOS）适配 设计方案（2026-10-11）

参考截图：/workspace/work/sogou-ref/（6 张）；Trime v3.3.12 源码：/workspace/work/t312。
约束：没有本机安卓编译环境，只能靠 GitHub CI 编译，所以每一步改动小、可由 CI 断言验证；键盘布局/主题尽量写成 YAML（prepare.py 生成），Kotlin 补丁只做 YAML 做不到的。

## 一、Trime 现状（已读源码）
- 工具栏 = `InputBarDelegate`(ViewAnimator: AlwaysUi 工具栏 / CandidateUi 候选 / TabUi 子页标题)。打字出候选时 `QuickBarStateMachine` 自动把工具栏换成候选栏 → 「候选占工具栏区域」天然具备，不用改。
- 工具栏按钮 `ButtonsBarUi` 是 FlexboxLayout ROW_REVERSE（不能滑动），按钮来自 trime.yaml `tool_bar.buttons`（action 必须是 preset_keys）。
- preedit：`PreeditDelegate/PreeditUi` 在键盘上方（above(keyboardView)）已是浮条，可改样式（白底小圆角）。
- 候选展开：`FlexboxUnrolledCandidateWindow`（⌄ 展开网格）已有。
- 键盘高度：`Keyboard.keyboardHeight`，已加比例缩放 + 拖动调整（YiweiResize）。
- 导航栏：`NavigationBarManager` + `InputView.onApplyWindowInsets` 已把底部导航栏高度加到 bottomPaddingSpace；`ThemePrefs.navbarBackground`（NONE/COLOR_ONLY/FULL）。
- 九键左侧列：Trime 键盘是 YAML `keys` 平铺，按键 `composing` 可在打字中换成别的动作 → 左侧列「打字时拼音选择」需要 Kotlin：从 Rime 取当前输入码的可切分拼音（t9 方案 `lua_filter t9_preedit` 已能算出 preedit），做成可滚动竖列。
- 弹出键盘符号面板：`LiquidWindow`（分类标签 + 网格），数据在 trime.yaml `liquid_keyboard`，可改成左侧竖栏样式。
- 震动：`InputFeedbackManager`（见下）。

## 二、搜狗对标要点（来自 6 张截图）
见 PROGRESS.md「用户新需求」。落地方案：
1. 九键：左列 ，。？！(打字时变拼音列)；中间 3×3；右列 ⌫/重输/0；底行 符|123|空格(麦克风)|中/英|↵；按键深色圆角、数字小字在上、字母在下。
2. 26 键：按键上方小字为长按符号；第三行 ⇧…⌫；底行 符|123|，|空格(麦克风)|。|中/英|↵。
3. 数字键盘：左列 / - : + 可滚动；3×3；底行 符|返回|0|空格|↵；右列 ⌫ . @。
4. 符号面板：左侧分类竖栏 + 右侧网格 + 底部返回。
5. 键盘选择页、工具箱页：图标网格（4 列），顶部标题栏「← 键盘选择 / ← 工具箱」。
6. 候选栏：首选加粗，横滑，最右 ⋮ 展开；preedit 左上小浮条。
配色：一维蓝（#2B5BD7 为强调色，key 深色/浅色两套随系统深色自动切换）。

## 三、工具栏与工具箱
- 工具栏改 HorizontalScrollView，左 Logo(点开工具箱)、右 收起 ⌄ 固定，中间按钮可横滑。
- 工具箱页：BarBoardWindow + 4 列网格，项目：常用语、编辑面板、键盘选择、剪贴板、语音、表情、手写、单手/悬浮、夜间模式、繁体、长辈大字、翻译、文字扫描、计算器、换皮肤、键盘调节(高度)、按键反馈、设置。
- 用户可调顺序：设置里「工具栏顺序」(SharedPreferences 存逗号分隔 id)，工具箱里长按拖动排序；默认顺序由 yaml 给。
- 每个功能落地方式：能用 Trime 现成 command 的直接绑（clipboard_window、liquid_keyboard、keyboard_resize、switch 选项如 traditional/simplification、dark mode 切 color_scheme）；需要新代码的（编辑面板、计算器、翻译、OCR）分步做，不可用的先隐藏，不放空按钮（CI 断言每个工具箱项都有处理）。

## 四、OPPO / ColorOS 适配（调研结论）
1. **手势条/导航栏留白**：IME 窗口在 Android 15(SDK35) 强制 edge-to-edge；用 `WindowInsets.systemGestures/navigationBars` 底部 inset 加 padding，键盘背景延伸到屏幕底（不出现色块）。ColorOS 手势导航条约 20dp，三键导航约 48dp。Trime 已有 `getNavBarBottomInset`，需确认 FULL 模式并在 ColorOS 上默认用 FULL；底部加 `setImeExclusionRect`/系统手势排除区避免底行按键被左右返回手势吃掉。→ 步骤 OPPO-1。
2. **键盘高度上限**：横屏与折叠屏内屏封顶为屏幕高度的 ~45%/50%；已做横竖屏封顶（YiweiResize.scaled），补折叠屏。
3. **后台冻结/杀进程**：ColorOS 路径（ColorOS 13–16）：设置 → 应用 → 应用启动管理 → 关闭自动管理 → 打开 自启动/关联启动/后台活动；设置 → 电池 → 应用耗电管理 → 允许完全后台行为；任务栏下拉锁定；ColorOS 有固定白名单，系统「电池优化」选项会被重置。输入法被系统设为当前输入法时一般被保护，但冻结后首次弹出会冷启动慢（rime 重新载入）。做法：设置页加「后台保活向导」：`Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`（需要 REQUEST_IGNORE_BATTERY_OPTIMIZATIONS 权限）→ 再尝试跳 ColorOS 自启动页 `com.coloros.safecenter/.startupapp.StartupAppListActivity` / `com.oplus.battery/.../PowerConsumptionActivity`，失败回退应用详情页 `ACTION_APPLICATION_DETAILS_SETTINGS`。→ OPPO-2。
4. **线性马达震动**：OPPO 线性马达对 `View.performHapticFeedback(KEYBOARD_TAP/KEYBOARD_PRESS)` 有专门波形，对 `Vibrator.vibrate(ms)` 的方波很「嗡」。做法：震动方式增加「系统触感(推荐)」：API 27+ 用 `HapticFeedbackConstants.KEYBOARD_TAP`（长按 `LONG_PRESS`，退格 `KEYBOARD_RELEASE`），不设强度，跟随系统触感强度；备选 `VibrationEffect.createPredefined(EFFECT_TICK/CLICK)`(API29+)；老办法保留。注意：View 方式受系统「触感反馈」总开关控制，要用 `HapticFeedbackConstants.FLAG_IGNORE_GLOBAL_SETTING` 可选。→ OPPO-3。
5. **深色模式**：已有「夜间自动深色」，确认跟随系统 `Configuration.UI_MODE_NIGHT`，包括 ColorOS 的「定时深色」；补工具箱「夜间模式」手动覆盖。
6. **字体缩放**：按键文字用 dp 而非 sp，或限制 fontScale 至 1.15（IME 窗口 `Configuration.fontScale` 夹到 [0.85,1.15]），避免「分词/ABC」撑破。
7. **折叠屏/平板（Find N、OPPO Pad）**：内屏宽 ≥ 600dp 时：键盘不拉满全宽，最大宽度 ~ 560dp 居中（或拆分键盘，双侧各一半，中间留空），高度比例按宽度算。Find N 系统键盘本身默认拆分模式。→ OPPO-5。
8. **圆角风格**：ColorOS 16 偏大圆角/半透明；一维键盘按键圆角 8–10dp，候选栏/浮条 12dp，键盘顶部 16dp 圆角（浮动键盘）。
9. 权限：麦克风仅语音时请求；剪贴板在 Android 13+ 读取会弹系统提示，ColorOS 有「剪贴板读取提示」；设置里说明。

## 五、实现分步（每步一个提交，CI 绿再下一步）
- S1 副标题「一维输入法」（各语言）+ CI 断言。
- S2 工具栏可横滑 + 更多按钮 + Logo 点开工具箱（工具箱网格页）+ 顺序可调。
- S3 键盘布局对标：九键（左列、右列、底行、深色圆角）、26 键底行、数字键盘、符号面板。
- S4 打字时九键左列变拼音选择列、preedit 浮条样式。
- S5 键盘选择页。
- OPPO-1 底部安全区/手势排除；OPPO-2 后台保活向导；OPPO-3 线性马达触感；OPPO-4 字体缩放封顶；OPPO-5 折叠屏/平板；
- 收尾：整体检查 + CI 新增检查 + 下载链接告知用户。

## 六、搜狗界面余项 + 输入设置（2026-10-12，prepare.install_sogou_ui）
- 候选栏：首选（高亮项）加粗；默认一行排开前 16 个候选可左右滑（设置「候选栏左右滑动」可关回 Trime 原样），⌄ 展开从第 1 个起的全部候选。
- 符号面板：左侧分类竖栏（当前分类一维蓝加粗）+ 底部「返回」，工具栏位置显示当前分类名（YiweiSymbolSide.kt）。
- 工具箱新增：夜间模式（跟随系统 ⇄ 强制深/浅，ColorManager 按开关选配色的深色版）、单手模式（键盘 78% 宽靠右；空白处点一下换边，长按退出；横屏不生效）、长辈模式（按键/候选文字 ×1.25、键盘加高 10%）、输入设置（YiweiInputPrefs.kt）。
- 输入设置（移植 Windows 一维助手）：模糊音地区预设 + 11 组开关、Emoji 候选开关、自定义标点（/→、 \→、 `→· []→「」）、按习惯调候选（开关、学习强度、清除最近 1 小时/1 天/全部）。文件与 Windows 相同（用户目录 yiwei/ 下 learn.*、emoji.disabled、punct.txt、fuzzy.txt）；模糊音/Emoji/标点写进 rime_ice / double_pinyin_flypy / t9 的 *.custom.yaml 的 `# yiwei-begin … # yiwei-end` 段并重新部署（双拼、九键的模糊音规则用 `@before N` 插在最前）。
- 未做：悬浮键盘（IME 窗口要改成可拖动的浮窗，改动大，后续单独做）。
