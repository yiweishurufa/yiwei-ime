# 一维输入法 · 安卓版

基于 [同文输入法 Trime](https://github.com/osfans/trime)（GPL-3.0）。CI 拉取固定版本的 Trime，运行 `prepare.py` 套上一维：

- 词库：雾凇拼音 + 一维科技词库 + 中英自动空格（默认关闭），与 Windows 版用同一个 `scripts/customize_data.py` 处理
- 配色：「墨线」五色（浅/深色成对，夜间模式自动切深色），默认青碧
- 包名 `cc.yiwei.ime`，可以和同文输入法同时安装
- 词库预编译：CI 用 Trime 自带的同一版 librime（+ librime-lua，`rime_host/build.sh` 从 Trime 子模块编译）生成 `shared/build`，再用 `rime_host/rime_gate.c` 像手机首次启动一样测试（用户目录里只放 App 自己写的 `default.custom.yaml`）：部署前（只有预编译数据）和部署后，26 键 `nihao`、九键 `64426` 都必须出「你好」，九键输入框必须显示 `ni hao`，词库大表不能在手机上重编，否则构建失败。安装包做完后再解开 arm64 APK 测一遍。（手机上会按文件时间重编几个小的 prism，几秒钟，期间预编译数据照常可用。）不要换回 apt 的 `librime-bin`（版本不同，手机会重新编译词库）
- 同文首次启动写的 `default.custom.yaml` 默认把方案列表改成 luna_pinyin（本应用没有），一维改为不改列表，并把旧版写下的 luna 列表删掉
- 工具栏和按键只能引用 `preset_keys` 里定义过的动作（`prepare.py` 的 `check_keys` 会检查）；同文遇到未定义的动作会把名字当文字打出来，一维已改成忽略

测试版用 `signing/yiwei-test.p12` 签名（公开的测试密钥）。正式发布前在仓库 Secrets 里设置 `ANDROID_KEYSTORE_BASE64` 和 `ANDROID_KEYSTORE_PASSWORD`（别名 `yiwei`），CI 会自动改用正式签名。
