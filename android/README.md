# 一维输入法 · 安卓版

基于 [同文输入法 Trime](https://github.com/osfans/trime)（GPL-3.0）。CI 拉取固定版本的 Trime，运行 `prepare.py` 套上一维：

- 词库：雾凇拼音 + 一维科技词库 + 中英自动空格（默认关闭），与 Windows 版用同一个 `scripts/customize_data.py` 处理
- 配色：「墨线」五色（浅/深色成对，夜间模式自动切深色），默认青碧
- 包名 `cc.yiwei.ime`，可以和同文输入法同时安装

测试版用 `signing/yiwei-test.p12` 签名（公开的测试密钥）。正式发布前在仓库 Secrets 里设置 `ANDROID_KEYSTORE_BASE64` 和 `ANDROID_KEYSTORE_PASSWORD`（别名 `yiwei`），CI 会自动改用正式签名。
