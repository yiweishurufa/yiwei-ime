# 一维输入法

**中文常新，自在表达。** AI 时代的 Windows 输入法。开源，注重隐私，基于 RIME。

一维输入法是 [AIME 艾么输入法](https://aime.zool.app/)（macOS）的 Windows 版复刻：内核基于 [小狼毫 Weasel](https://github.com/rime/weasel)，默认方案为 [雾凇拼音](https://github.com/iDvel/rime-ice)，产品体验、配色与科技词库来自 AIME（MIT）。可以和小狼毫同时安装，互不干扰。

## 下载

到 [Releases](https://github.com/yiweishurufa/yiwei-ime/releases) 下载 `yiwei-ime-*-installer.exe`，双击安装。支持 Windows 10 / 11。

## 功能

| 功能 | 用法 |
|---|---|
| 雾凇拼音 | 全拼与各家双拼，日期 `rq`、时间 `sj`、计算器 `V`、Unicode、农历、Emoji |
| 号码和邮箱，按一个键 | 长按 **Alt** 再按 **1–9** 打开常用语面板（手机号、邮箱、地址、符号…），按 **A S D…** 直接上屏 |
| 翻译和润色，在光标处完成 | 选中文字，长按 **Alt** 再按 **空格**：1 翻译、2 润色、3 粤语，可自定义动作；支持任何 OpenAI 兼容接口（OpenAI、DeepSeek、通义千问、Kimi、智谱、本机 Ollama） |
| 多套配色 | 一维浅色 / 深色、石墨、午夜、樱花、抹茶、北境、纸墨；横排、竖排、直书；浅色深色分别设置；`yiwei-ime://theme` 链接一键导入 |
| 配色、字号、圆角，边改边看 | 「一维输入法设置 → 外观」实时预览 |
| 终端里打英文，微信里打中文 | 每个应用记住自己的中英文状态，终端和编辑器默认英文，可自行勾选 |
| 这一年打了多少字 | 字数、时段、最常用的应用和词；只存本机，默认关闭，可随时清空 |
| 词库每天自动更新 | 每天检查雾凇拼音新词库，SHA-256 校验通过才安装 |
| 简繁转换 | 本机 OpenCC，不需要 AI |
| 从小狼毫搬家 | 「设置 → 导入」一键复制方案、补丁与词库 |

## 隐私

组字、候选与个人词频在本机处理，不含遥测。常用语、输入统计只存本机。AI 只在你按下快捷键时，把选中的那一段发给你自己配置的接口。

## 构建

GitHub Actions 自动构建（`.github/workflows/build.yml`）：拉取固定版本的小狼毫源码 → `scripts/rebrand.py` 改名并更换全部系统标识（TSF CLSID、注册表、管道、安装目录、用户目录）→ `scripts/customize_data.py` 装入雾凇拼音、配色与扩展 → 编译 `helper/`（一维助手，.NET Framework 4.8）→ NSIS 打包。

## 开源与致谢

- 小狼毫 Weasel — GPL-3.0
- librime — BSD-3-Clause
- 雾凇拼音 rime-ice — GPL-3.0
- OpenCC — Apache-2.0
- AIME 艾么输入法 — MIT, © 2026 ZOOL LLC

一维输入法整体以 GPL-3.0 发布，见 [LICENSE](LICENSE)。
