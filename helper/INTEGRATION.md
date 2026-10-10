# 一维助手 (YiweiHelper.exe) — integration notes

## Build / ship
- `dotnet build helper\YiweiHelper.csproj -c Release -o helper-out` → ship `helper-out\YiweiHelper.exe` + `YiweiHelper.exe.config`.
- NuGet: WPF-UI 3.0.5 (MIT, net462+ build used), Fody 6.9.2 + Costura.Fody 6.0.0 (build-time only).
  Costura embeds Wpf.Ui.dll into the exe, so **no extra DLLs need to be redistributed** (no REDISTRIBUTE.txt).

## Command line / pipe (`\\.\pipe\YiweiHelper.Commands`, UTF-8, one command per connection)
| command | effect |
|---|---|
| *(none)* | start tray; a second launch opens settings |
| `/background` | start tray only (autostart); first ever start still shows the wizard |
| `/wizard` | open the first-run wizard (starts tray if not running, else forwarded over the pipe) |
| `/settings [页面]` | open settings, optionally on a page: 常规 输入方案 快捷输入 外观 快捷键 常用语 AI 词库 应用规则 统计 关于 |
| `/deploy` | rewrite weasel.custom.yaml / default.custom.yaml and redeploy in the background |
| `/diagnostics` | export the diagnostics zip to the desktop (also tray 「导出诊断信息」) |
| `/quit` | exit |
| `toast:中` / `toast:A` | show the 中/英 bubble at the caret (~600 ms). Also accepts `toast:zh` / `toast:cn` for 中 |
| `yiwei-ime://theme?...` | import a theme link |

## 中/英 toast
Weasel is a TSF text service and does not reliably mirror ascii_mode into IMM, so the reliable trigger is the
IME side: when `ascii_mode` changes (e.g. in WeaselServer's option-update / a librime notification handler, or a Lua
processor), write `toast:中` or `toast:A` to the pipe above (connect with a short timeout; ignore failures).
There is also an experimental IMM poller (设置 → 常规 → 从输入法状态检测切换, off by default).

## Paths
- Deployer: `YiweiDeployer.exe`, fallback `WeaselDeployer.exe`; Server: `YiweiServer.exe`, fallback `WeaselServer.exe`.
- Install root: HKLM\Software\Yiwei\YiweiIME `YiweiRoot`, fallback `WeaselRoot` (64- then 32-bit view).

## Files the helper writes in the RIME user folder
- `weasel.custom.yaml` (owned, user copy backed up once): style, preset_color_schemes `yiwei_{pill,moblue,qingbi,zhusha,dianzi,shimo}[_dark]` (default `yiwei_pill` 蓝色胶囊: `style/layout/round_corner: 99` + `hilite_padding_x: 8`), `yiwei_auto_dark` (generated dark version of an imported / classic scheme)
  (colors in Weasel abgr `0xAABBGGRR`), imported themes, app_options.
- `default.custom.yaml` (owned, a pre-existing user file is backed up to `.bak-<time>` first): `schema_list` with the chosen schema first
  (rime_ice / double_pinyin_flypy / double_pinyin / double_pinyin_mspy) and `menu/page_size`.
- `<schema>.custom.yaml` for rime_ice / double_pinyin_flypy / double_pinyin / double_pinyin_mspy (owned unless a user file without our
  marker exists): 简繁 default, 快捷输入 switches (disabled triggers are neutralised: empty trigger keys / recognizer pattern `^$`),
  大字表 (`translator/dictionary: yiwei_ice_big`, `translator/user_dict: rime_ice`), 模糊音 (`speller/algebra/+`, full pinyin only).
- `yiwei_ice_big.dict.yaml` (only when 大字表 is on): rime_ice tables + `cn_dicts/41448`.
- `user.yaml` `var/previously_selected_schema`.
- `sync/yiwei-import/rime_ice.userdb.txt`: words imported from 搜狗/微软拼音 text exports, merged with `WeaselDeployer /sync`.

## Downloads / mirrors
- Every GitHub request (appcast + installer, rime-ice release API + full.zip, RIME-LMDG grammar model) goes through `Mirrors`
  (Features/Mirrors.cs): last good source first, then `ghfast.top` → `gh-proxy.com` (also proxies api.github.com) → jsDelivr
  (repo files only) → GitHub. The last good source is kept in `<user>/yiwei/mirror.json`. Integrity: expected size +
  SHA-256 (release asset digest; the appcast `<enclosure sha256="…">` written by CI).

## Offline voice input (Features/Voice.cs, Wpf/VoiceCapsule.cs, Wpf/VoiceUi.cs)
- Hold **right Ctrl** (or right Alt; 设置 → 语音) ≥ 350 ms (right Alt: max(600, HoldMs+250)) → recording capsule at the caret;
  release → recognise → `TextOut.Type`. Esc cancels (swallowed); any other key cancels and passes through. Not active until the
  model is installed, nor in exclusive full screen / the Alt-gesture blocklist.
- Engine: `sherpa-onnx-offline.exe` 1.13.8 (win-x64 or win-arm64, shared-MT-Release-no-tts: exe + onnxruntime.dll, static CRT)
  + SenseVoice-Small int8 2024-07-17 (`model.int8.onnx`, `tokens.txt`), run with `--sense-voice-use-itn=1` (punctuation).
  Both pinned by size + SHA-256, downloaded through `Mirrors`, unpacked with the system `tar.exe` (Win10 1803+) into
  `<user>/yiwei/voice/`. The recogniser is started per utterance with relative ASCII paths (cwd = voice folder) so a
  non-ASCII user folder does not break its narrow argv. Output: one JSON line on stdout, field `text`.
- Recording: winmm waveIn 16 kHz/16-bit mono, polled buffers; max 60 s. Post-processing: fillers (嗯 呃 额 啊 哦 at sentence
  start / after punctuation) and CJK–Latin spacing, both switchable.
- If focus moves while recognising, the text goes to the clipboard with a balloon instead of being typed.
