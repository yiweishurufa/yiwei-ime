# 多个 Windows 账户各自的词库（W46 现状说明）

**结论：已经是各账户独立的，不需要额外开关。**

| 数据 | 位置 | 是否按账户隔离 |
|---|---|---|
| 用户词库（打字学到的词，`*.userdb`）、方案配置、`weasel.custom.yaml` | `%AppData%\YiweiIME`（每个账户自己的漫游 AppData） | 是 |
| 一维助手设置、常用语、剪贴板历史、统计、`tools.json` | `%AppData%\YiweiIME\yiwei` | 是（剪贴板历史另用 DPAPI 按账户加密） |
| 崩溃转储 | `%LocalAppData%\YiweiIME\CrashDumps` | 是 |
| 程序、共享词典（雾凇）、预编译数据 | `C:\Program Files\Rime\…` | 所有账户共用（只读） |
| 开机启动（`HKLM\…\Run\YiweiHelper`） | 全机 | 每个账户登录时各自启动一份助手；可在 设置 → 工具 里按账户关掉开机自启 |

- 输入法服务 `YiweiServer.exe` 按登录会话各跑一份，快速切换用户 / 远程桌面多会话时互不影响。
- 一个账户想用另一个账户的词库：在原账户 设置 → 词库 → 备份与恢复 导出 `.yiwei-backup`，到新账户里恢复（词库合并、不覆盖）；或者两个账户设同一个 WebDAV / 同步文件夹（设置 → 工具 → WebDAV），会自动互相合并。
- 卸载时「删除个人数据」只删执行卸载的那个账户的 `%AppData%\YiweiIME`，其它账户的数据保留。

已知风险（需真机确认）：单实例互斥体是 `Local\YiweiHelper.Instance`（按会话），但命令管道 `\\.\pipe\YiweiHelper.Commands` 是全机唯一的名字、且只允许 1 个实例。两个账户同时登录（快速切换用户 / 多会话远程桌面）时，后登录账户的助手建不了管道，lua 发来的 `toast:` / `regret:` / `pair:` 等命令可能被送到先登录账户的助手。修法：管道名加上会话号或用户 SID（例如 `YiweiHelper.Commands.<SessionId>`），并同步改 lua 和语言栏菜单的客户端；涉及 Program.cs 与 overlay/data 的 lua，留给负责这些文件的组。
