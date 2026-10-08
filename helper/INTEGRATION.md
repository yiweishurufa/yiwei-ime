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
| `/settings [页面]` | open settings, optionally on a page: 常规 输入方案 外观 快捷键 常用语 AI 词库 应用规则 统计 关于 |
| `/deploy` | rewrite weasel.custom.yaml / default.custom.yaml and redeploy in the background |
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
- `weasel.custom.yaml` (owned, user copy backed up once): style, preset_color_schemes `yiwei_{moblue,qingbi,zhusha,dianzi,shimo}[_dark]`
  (colors in Weasel abgr `0xAABBGGRR`), imported themes, app_options.
- `default.custom.yaml` (owned, a pre-existing user file is backed up to `.bak-<time>` first): `schema_list` with the chosen schema first
  (rime_ice / double_pinyin_flypy / double_pinyin / double_pinyin_mspy) and `menu/page_size`.
- `user.yaml` `var/previously_selected_schema`.
- `sync/yiwei-import/rime_ice.userdb.txt`: words imported from 搜狗/微软拼音 text exports, merged with `WeaselDeployer /sync`.
