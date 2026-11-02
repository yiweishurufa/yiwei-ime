#!/usr/bin/env python3
"""Rebrand an upstream Weasel (小狼毫) source tree as 一维输入法 (Yiwei IME).

Run from the repository root:  python scripts/rebrand.py weasel
Changes every user-visible name, plus every identity Windows uses to tell two
input methods apart (TSF CLSID/profile GUIDs, registry keys, IPC pipe/window
names, mutexes, install folder, user folder), so 一维输入法 can be installed
side by side with 小狼毫.
"""
import os
import re
import sys
import uuid

ROOT = sys.argv[1] if len(sys.argv) > 1 else "weasel"
REPO = "https://github.com/yiweishurufa/yiwei-ime"
APPCAST = "https://raw.githubusercontent.com/yiweishurufa/yiwei-ime/main/update/appcast.xml"
NAME = "一维输入法"
EN_NAME = "Yiwei IME"

SKIP_DIRS = {".git", "librime", "plum", "boost", "winsparkle", "deps", "test"}
TEXT_EXT = {".cpp", ".h", ".hpp", ".c", ".rc", ".nsi", ".bat", ".txt", ".js",
            ".lua", ".props", ".ps1", ".template", ".md", ".xml", ".def", ".vcxproj"}

GUIDS = {
    # old -> new
    "A3F4CDED-B1E9-41EE-9CA6-7B4D0DE6CB0A": "54C92C2A-5FE9-43A0-A9EC-D05B42C996D4",  # text service CLSID
    "3D02CAB6-2B8E-4781-BA20-1C9267529467": "B7D08A98-C73D-4BBF-8938-F1164EC9AC7F",  # language profile
    "341F9E3A-B8AD-499D-936C-48701E329FB2": "C3F2E896-E075-4442-A2D8-08F25B04E5BE",  # lang bar button
    "2AC87E79-3260-4B32-9DEA-F8390976C20B": "4FA6E9B1-9D26-4C9C-9352-EBDEC900CB81",  # display attribute
}

# Ordered literal replacements applied to every text file.
REPLACEMENTS = [
    ("小狼毫輸入法", NAME), ("小狼毫输入法", NAME), ("小狼毫", NAME),
    # registry (C string escaping and NSIS single backslash forms)
    ("Software\\\\WOW6432Node\\\\Rime\\\\Weasel", "Software\\\\WOW6432Node\\\\Yiwei\\\\YiweiIME"),
    ("Software\\\\Rime\\\\Weasel", "Software\\\\Yiwei\\\\YiweiIME"),
    ("Software\\\\Rime\\\\weasel", "Software\\\\Yiwei\\\\YiweiIME"),
    ('L"Software\\\\Rime"', 'L"Software\\\\Yiwei"'),
    ("Software\\Rime\\Weasel", "Software\\Yiwei\\YiweiIME"),
    ("SOFTWARE\\Rime\\Weasel", "SOFTWARE\\Yiwei\\YiweiIME"),
    ("DeleteRegKey HKLM SOFTWARE\\Rime", "DeleteRegKey HKLM SOFTWARE\\Yiwei"),
    ("CurrentVersion\\Uninstall\\Weasel", "CurrentVersion\\Uninstall\\YiweiIME"),
    ('"Software\\Microsoft\\Windows\\CurrentVersion\\Run" "WeaselServer"',
     '"Software\\Microsoft\\Windows\\CurrentVersion\\Run" "YiweiIME"'),
    # folders
    ("%AppData%\\\\Rime", "%AppData%\\\\YiweiIME"),
    ("$PROGRAMFILES64\\Rime", "$PROGRAMFILES64\\Yiwei"),
    ("$PROGRAMFILES\\Rime", "$PROGRAMFILES\\Yiwei"),
    ("$INSTDIR\\weasel-${WEASEL_VERSION}", "$INSTDIR\\yiwei-${WEASEL_VERSION}"),
    ("$TEMP\\weasel-backup", "$TEMP\\yiwei-backup"),
    ("rime.weasel", "rime.yiwei"),
    # IPC and synchronisation objects
    ("WeaselIPCWindow_1.0", "YiweiIPCWindow_1.0"),
    ("WeaselNamedPipe", "YiweiNamedPipe"),
    ("WeaselDeployerExclusiveMutex", "YiweiDeployerExclusiveMutex"),
    ("WeaselDeployerMutex", "YiweiDeployerMutex"),
    ('L"WeaselInputService"', 'L"YiweiInputService"'),
    ('L"Weasel Display Attribute Input"', 'L"Yiwei Display Attribute Input"'),
    ('L"WeaselTSF Button"', 'L"YiweiTSF Button"'),
    ('L"Weasel Deployer"', 'L"' + NAME + '"'),
    # links
    ("https://rime.im/docs/", REPO + "#readme"),
    ("https://rime.im/discuss/", REPO + "/issues"),
    ("https://rime.im/", REPO),
    # installer
    ('archives\\weasel-${PRODUCT_VERSION}-installer.exe', 'archives\\yiwei-ime-${PRODUCT_VERSION}-installer.exe'),
    ('"Publisher" "式恕堂"', '"Publisher" "' + NAME + '"'),
    ('"CheckForUpdates" "1"', '"CheckForUpdates" "0"'),
    # executable / module file names (so Task Manager, the install folder and
    # every runtime lookup say Yiwei, not Weasel)
    ("WeaselServer.exe", "YiweiServer.exe"), ("weaselserver.exe", "yiweiserver.exe"),
    ("WeaselDeployer.exe", "YiweiDeployer.exe"), ("WeaselDeployer.pdb", "YiweiDeployer.pdb"),
    ("WeaselServer.pdb", "YiweiServer.pdb"),
    ("WeaselSetupx64.exe", "YiweiSetupx64.exe"), ("WeaselSetup.exe", "YiweiSetup.exe"),
    ("WeaselSetup.pdb", "YiweiSetup.pdb"),
    ("weaselARM64X.dll", "yiweiARM64X.dll"), ("weaselARM64.dll", "yiweiARM64.dll"),
    ("weaselARM.dll", "yiweiARM.dll"), ("weaselx64.dll", "yiweix64.dll"),
    ("weaselx64.pdb", "yiweix64.pdb"), ("weasel.dll", "yiwei.dll"), ("weasel.ime", "yiwei.ime"),
    ('L"weaselARM64X"', 'L"yiweiARM64X"'), ('L"\\\\weasel"', 'L"\\\\yiwei"'),
    ('srcFileName = L"weasel"', 'srcFileName = L"yiwei"'),
    ("<TargetName>weasel</TargetName>", "<TargetName>yiwei</TargetName>"),
    ("<TargetName>weasel$(Platform)</TargetName>", "<TargetName>yiwei$(Platform)</TargetName>"),
    ("output\\weasel$(TargetExt)", "output\\yiwei$(TargetExt)"),
    ("output\\weasel$(Platform)$(TargetExt)", "output\\yiwei$(Platform)$(TargetExt)"),
    ("      ShowBalloon(info, get_weasel_ime_name().c_str());",
     "      (void)info;  // 一维输入法：换皮肤等重新部署时不弹「维护中」系统通知"),
    ("      !state.disabled)  // display notification when deploying",
     "      true)  // 一维：托盘只保留一维助手的一个图标（中/英状态也显示在那里）"),
    ('"WeaselRoot"', '"YiweiRoot"'), ('L"WeaselRoot"', 'L"YiweiRoot"'),
    ('#define WEASEL_CODE_NAME "Weasel"', '#define WEASEL_CODE_NAME "Yiwei"'),
    ('L"WeaselSetup"', 'L"' + NAME + '"'),
    ('"Software\\Rime\\Weasel\\Updates"', '"Software\\Yiwei\\YiweiIME\\Updates"'),
]

# exe projects whose output file name is their project name: pin a Yiwei name
EXE_TARGETS = {"WeaselServer.vcxproj": "YiweiServer",
               "WeaselDeployer.vcxproj": "YiweiDeployer",
               "WeaselSetup.vcxproj": "YiweiSetup"}

NSI_EN = [
    ('"Weasel"', '"' + EN_NAME + '"'),
    ("Weasel Manual", EN_NAME + " Manual"), ("Weasel Settings", EN_NAME + " Settings"),
    ("Weasel Dictionary Manager", EN_NAME + " Dictionary Manager"),
    ("Weasel Sync User Profile", EN_NAME + " Sync User Profile"),
    ("Weasel Deploy", EN_NAME + " Deploy"), ("Weasel Server", EN_NAME + " Server"),
    ("Weasel User Folder", EN_NAME + " User Folder"), ("Weasel App Folder", EN_NAME + " App Folder"),
    ("Weasel Check for Updates", EN_NAME + " Check for Updates"),
    ("Weasel Installation Preference", EN_NAME + " Installation Preference"),
    ("Uninstall Weasel", "Uninstall " + EN_NAME),
    ("old version of Weasel", "old version of " + EN_NAME),
]


# ---------------------------------------------------------------------------
# 一维：任务栏只留一个状态图标。小狼毫 TSF 的语言栏「输入模式」按钮（GUID_LBI_INPUTMODE，
# Windows 输入指示器旁的 中/英）是唯一常驻图标；一维助手的托盘图标默认隐藏，
# 它的菜单功能挪到这个按钮的右键菜单里（菜单项交给 YiweiHelper.exe 执行）。
HELPER_IDS = {  # 菜单 id → YiweiHelper.exe 命令行
    "ID_WEASELTRAY_SETTINGS": "/settings", "ID_WEASELTRAY_CHECKUPDATE": "/update",
    "ID_WEASELTRAY_DEPLOY": "/deploy",
    "40101": "/handwrite", "40102": "/symbols", "40103": "/ocr", "40104": "/addword",
    "40105": "/share", "40106": "/tray toggle", "40107": "/diagnostics",
}
LANGBAR_MENUS = {
    "IDR_MENU_POPUP_HANS": [
        ("一维输入法设置 (&S)", "ID_WEASELTRAY_SETTINGS"), None,
        ("手写输入 (&H)", "40101"), ("符号与表情 (&F)", "40102"), ("截图识字 (&O)", "40103"),
        ("加词… (&A)", "40104"), ("局域网互传 (&L)", "40105"), None,
        ("用户词典管理 (&D)", "ID_WEASELTRAY_DICT_MANAGEMENT"), ("用户资料同步 (&N)", "ID_WEASELTRAY_SYNC"),
        ("用户文件夹 (&C)", "ID_WEASELTRAY_USERCONFIG"), ("程序文件夹 (&P)", "ID_WEASELTRAY_INSTALLDIR"),
        ("日志文件夹 (&G)", "ID_WEASELTRAY_LOGDIR"), None,
        ("显示/隐藏通知区域的一维图标 (&T)", "40106"), ("检查更新 (&U)", "ID_WEASELTRAY_CHECKUPDATE"),
        ("重新部署 (&R)", "ID_WEASELTRAY_DEPLOY"), ("导出诊断信息 (&I)", "40107"),
        ("帮助 (&W)", "ID_WEASELTRAY_WIKI"), None,
        ("重启算法服务 (&E)", "ID_WEASELTRAY_RERUN_SERVICE"), ("退出算法服务 (&Q)", "ID_WEASELTRAY_QUIT"),
    ],
    "IDR_MENU_POPUP_HANT": [
        ("一維輸入法設定 (&S)", "ID_WEASELTRAY_SETTINGS"), None,
        ("手寫輸入 (&H)", "40101"), ("符號與表情 (&F)", "40102"), ("截圖識字 (&O)", "40103"),
        ("加詞… (&A)", "40104"), ("區域網路互傳 (&L)", "40105"), None,
        ("用戶詞典管理 (&D)", "ID_WEASELTRAY_DICT_MANAGEMENT"), ("用戶資料同步 (&N)", "ID_WEASELTRAY_SYNC"),
        ("用戶文件夾 (&C)", "ID_WEASELTRAY_USERCONFIG"), ("程序文件夾 (&P)", "ID_WEASELTRAY_INSTALLDIR"),
        ("日誌文件夾 (&G)", "ID_WEASELTRAY_LOGDIR"), None,
        ("顯示/隱藏通知區域的一維圖示 (&T)", "40106"), ("檢查更新 (&U)", "ID_WEASELTRAY_CHECKUPDATE"),
        ("重新部署 (&R)", "ID_WEASELTRAY_DEPLOY"), ("匯出診斷資訊 (&I)", "40107"),
        ("說明 (&W)", "ID_WEASELTRAY_WIKI"), None,
        ("重啓算法服務 (&E)", "ID_WEASELTRAY_RERUN_SERVICE"), ("退出算法服務 (&Q)", "ID_WEASELTRAY_QUIT"),
    ],
    "IDR_MENU_POPUP": [
        ("Yiwei IME settings (&S)", "ID_WEASELTRAY_SETTINGS"), None,
        ("Handwriting (&H)", "40101"), ("Symbols && emoji (&F)", "40102"), ("Text from screenshot (&O)", "40103"),
        ("Add word… (&A)", "40104"), ("LAN share (&L)", "40105"), None,
        ("Dictionary manager (&D)", "ID_WEASELTRAY_DICT_MANAGEMENT"), ("User data sync (&N)", "ID_WEASELTRAY_SYNC"),
        ("User folder (&C)", "ID_WEASELTRAY_USERCONFIG"), ("Program folder (&P)", "ID_WEASELTRAY_INSTALLDIR"),
        ("Log folder (&G)", "ID_WEASELTRAY_LOGDIR"), None,
        ("Show/hide tray icon (&T)", "40106"), ("Check for updates (&U)", "ID_WEASELTRAY_CHECKUPDATE"),
        ("Deploy (&R)", "ID_WEASELTRAY_DEPLOY"), ("Export diagnostics (&I)", "40107"),
        ("Help (&W)", "ID_WEASELTRAY_WIKI"), None,
        ("Restart (&E)", "ID_WEASELTRAY_RERUN_SERVICE"), ("Exit (&Q)", "ID_WEASELTRAY_QUIT"),
    ],
}


def langbar_menu_block(name, items, nl):
    lines = ["%s MENU" % name, "BEGIN", '    POPUP "WeaselTray"', "    BEGIN"]
    for it in items:
        lines.append("        MENUITEM SEPARATOR" if it is None else '        MENUITEM "%s", %s' % it)
    lines += ["    END", "END"]
    return nl.join(lines) + nl


def patch_tsf_rc(text):
    nl = "\r\n" if "\r\n" in text else "\n"
    for name, items in LANGBAR_MENUS.items():
        pat = re.compile(r"^%s MENU\r?\nBEGIN\r?\n.*?^END\r?\n" % re.escape(name), re.S | re.M)
        text, n = pat.subn(lambda m: langbar_menu_block(name, items, nl), text, count=1)
        if n != 1:
            raise SystemExit("WeaselTSF.rc: menu %s not found" % name)
    return text


HELPER_CPP = r"""
// 一维输入法：语言栏菜单里的一维功能交给一维助手 YiweiHelper.exe（找不到助手时照原来的方式处理）
static const wchar_t* YiweiHelperCommand(UINT wID) {
  switch (wID) {
%s
  }
  return nullptr;
}

static bool YiweiRunHelper(const std::wstring& root, const wchar_t* args) {
  if (root.empty())
    return false;
  std::wstring exe = root + L"\\YiweiHelper.exe";
  if (GetFileAttributesW(exe.c_str()) == INVALID_FILE_ATTRIBUTES)
    return false;
  AllowSetForegroundWindow(ASFW_ANY);
  std::wstring a(args);
  // 直接 CreateProcess（不依赖 COM/外壳），失败再退回 ShellExecute
  std::wstring cmdline = L"\"" + exe + L"\" " + a;
  STARTUPINFOW si = {sizeof(si)};
  PROCESS_INFORMATION pi = {};
  if (CreateProcessW(exe.c_str(), &cmdline[0], NULL, NULL, FALSE, 0, NULL,
                     root.c_str(), &si, &pi)) {
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);
    return true;
  }
  std::thread th([exe, a, root]() {
    ShellExecuteW(NULL, L"open", exe.c_str(), a.c_str(), root.c_str(),
                  SW_SHOWNORMAL);
  });
  th.detach();
  return true;
}

void WeaselTSF::_HandleLangBarMenuSelect(UINT wID) {
  if (const wchar_t* yiwei_cmd = YiweiHelperCommand(wID)) {
    if (YiweiRunHelper(_GetRootDir(), yiwei_cmd))
      return;
  }
"""


def patch_langbar_cpp(text):
    if "YiweiHelperCommand" in text:
        return text
    cases = "\n".join('    case %s:\n      return L"%s";' % (k, v) for k, v in HELPER_IDS.items())
    block = (HELPER_CPP % cases).lstrip("\n")
    pat = re.compile(r"^void WeaselTSF::_HandleLangBarMenuSelect\(UINT wID\) \{\r?\n", re.M)
    text, n = pat.subn(lambda m: block, text, count=1)
    if n != 1:
        raise SystemExit("LanguageBar.cpp: _HandleLangBarMenuSelect not found")
    return text


# ---------------------------------------------------------------------------
# 一维：「界面风格设定」对话框（WeaselDeployer）。小狼毫原版预览是繁体图片「RIME shu ru fa / 輸入法」、
# 列表混着小狼毫配色、确定按钮写着「中」、设定字体灰掉。换成 scripts/weasel_patches 里的一维版本：
# 一维配色排最前、原版配色隐藏，按配色实时画「yi wei / 一维输入法」预览，可设字体；
# 装了一维助手时选择交给助手保存（设置 → 外观 同一份设置），不带参数打开部署程序直接进助手的外观页。
PATCH_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "weasel_patches")
UPSTREAM_SCHEMES_FALLBACK = [
    "aqua", "azure", "luna", "ink", "lost_temple", "dark_temple", "starcraft", "google", "solarized_rock",
    "tintin", "dota_2", "brasil", "doraemon", "espagna", "gholabok", "kuma_shuzboz", "kuon", "macau", "nba",
    "ps4", "skype", "xbox_silver", "youtube", "so_young", "smurfs", "wii", "android", "cool_breeze",
    "google_plus", "modern_warfare", "brisk", "starcraft_ii", "steam", "flypy", "metroblue", "psionics"]

LEVERS_HELPERS = """  RimeCustomSettings* settings() { return settings_; }
  // 一维输入法：预览配色、设定字体
  bool GetConfig(RimeConfig* config) {
    return !!api_->settings_get_config(settings_, config);
  }
  bool CustomizeString(const char* key, const char* value) {
    return !!api_->customize_string(settings_, key, value);
  }
  bool CustomizeInt(const char* key, int value) {
    return !!api_->customize_int(settings_, key, value);
  }
"""

DEPLOYER_RUN_OLD = """  bool installing = IsOption(args.front(), L"/install") && args.size() == 1;
  return configurator.Run(installing);"""
DEPLOYER_RUN_NEW = """  bool installing = IsOption(args.front(), L"/install") && args.size() == 1;
  // 一维输入法：「输入法设定」「界面风格」直接打开一维助手的设置窗口（外观页）；
  // /classic 或找不到助手时才用Weasel 原来的对话框
  bool classic = IsOption(args.front(), L"/classic");
  if (!installing && !classic && YiweiHelperRun(L"/settings \\u5916\\u89c2"))
    return 0;
  return configurator.Run(installing);"""


def upstream_scheme_ids():
    path = os.path.join(ROOT, "output", "data", "weasel.yaml")
    try:
        text = open(path, encoding="utf-8").read()
        ids = re.findall(r"^  ([A-Za-z0-9_]+):[ \t]*$", text[text.index("preset_color_schemes:"):], re.M)
        ids = [i for i in ids if not i.startswith("yiwei")]
        if len(ids) >= 10:
            return ids
    except (OSError, ValueError):
        pass
    return UPSTREAM_SCHEMES_FALLBACK


def patch_deployer_sources(text, base):
    nl = "\r\n" if "\r\n" in text else "\n"
    if base == "WeaselDeployer.rc":
        n = text.count('"中",IDOK')
        if n == 0 and '"确定",IDOK' not in text:
            raise SystemExit("WeaselDeployer.rc: IDOK button text not found")
        text = text.replace('"中",IDOK', '"确定",IDOK')
    elif base == "UIStyleSettings.h" and "CustomizeString" not in text:
        old = "  RimeCustomSettings* settings() { return settings_; }" + nl
        if old not in text:
            raise SystemExit("UIStyleSettings.h: settings() accessor not found")
        text = text.replace(old, LEVERS_HELPERS.replace("\n", nl), 1)
    elif base == "WeaselDeployer.cpp" and "YiweiHelperRun" not in text:
        old = DEPLOYER_RUN_OLD.replace("\n", nl)
        if old not in text:
            raise SystemExit("WeaselDeployer.cpp: Run() tail not found")
        text = text.replace(old, DEPLOYER_RUN_NEW.replace("\n", nl), 1)
        decl = "static int Run(int argc, wchar_t* argv[]) {"
        if decl not in text:
            raise SystemExit("WeaselDeployer.cpp: Run() not found")
        text = text.replace(decl, "bool YiweiHelperRun(const wchar_t* args);  // UIStyleSettingsDialog.cpp" + nl + nl + decl, 1)
    return text


YIWEI_LAUNCHER = """// 一维输入法：打开一维助手（与部署程序在同一文件夹）；找不到助手时返回 false
bool YiweiHelperRun(const wchar_t* args) {
  WCHAR path[MAX_PATH] = {0};
  GetModuleFileNameW(NULL, path, _countof(path));
  std::wstring exe(path);
  size_t slash = exe.find_last_of(L"\\\\/");
  if (slash == std::wstring::npos)
    return false;
  exe = exe.substr(0, slash + 1) + L"YiweiHelper.exe";
  if (GetFileAttributesW(exe.c_str()) == INVALID_FILE_ATTRIBUTES)
    return false;
  AllowSetForegroundWindow(ASFW_ANY);
  HINSTANCE h = ShellExecuteW(NULL, L"open", exe.c_str(), args, NULL, SW_SHOWNORMAL);
  return (INT_PTR)h > 32;
}
"""


def install_style_dialog():
    dst = os.path.join(ROOT, "WeaselDeployer")
    if not os.path.isdir(dst):
        return
    old, enc0, bom0 = read(os.path.join(dst, "UIStyleSettingsDialog.cpp"))
    if old is not None and "preview_ui_" in old:
        # 上游 2026-10 重写了对话框（预览用真实候选窗、样式键列表）：保留上游对话框，
        # 只追加 YiweiHelperRun（打开一维助手设置）；「/classic」才会用到上游对话框。
        if "YiweiHelperRun" not in old:
            nl = "\r\n" if "\r\n" in old else "\n"
            add = YIWEI_LAUNCHER.replace("\n", nl)
            with open(os.path.join(dst, "UIStyleSettingsDialog.cpp"), "wb") as f:
                f.write(bom0 + (old.rstrip() + nl + nl + add).encode(enc0))
        return
    if old is not None and "YiweiHelperRun" not in old and "select_font_.EnableWindow(FALSE);" not in old:
        raise SystemExit("UIStyleSettingsDialog.cpp: upstream changed, review scripts/weasel_patches")
    ids = ", ".join('"%s"' % i for i in upstream_scheme_ids())
    for fn in ("UIStyleSettingsDialog.cpp", "UIStyleSettingsDialog.h"):
        src = open(os.path.join(PATCH_DIR, fn), encoding="utf-8").read().replace("/*UPSTREAM_IDS*/", ids)
        with open(os.path.join(dst, fn), "wb") as f:
            f.write(b"\xef\xbb\xbf" + src.replace("\r\n", "\n").replace("\n", "\r\n").encode("utf-8"))


# ---------------------------------------------------------------------------
# 一维：升级不需要重启。系统目录里的 yiwei*.dll 被各程序加载着，不能覆盖但能改名：Weasel 原来改名成
# .old.0 … .old.9 并登记开机删除，十次升级不重启就会把名字用完、复制失败。改成先清掉已经没人用的
# .old.*，再用时间戳起新名字；已开的程序继续用旧 DLL，新开的程序用新 DLL，不需要重启。
COPY_FILE_NEW = """// 一维输入法：先清掉以前改名留下、已经没人加载的 .old.*，正在用的改成带时间戳的新名字，
// 新文件复制到原名 —— 已开的程序继续用旧版，新开的程序用新版，升级不需要重启。
static void yiwei_cleanup_old(const std::wstring& dest) {
  size_t slash = dest.find_last_of(L"\\\\/");
  std::wstring dir = slash == std::wstring::npos ? L"" : dest.substr(0, slash + 1);
  WIN32_FIND_DATAW fd;
  HANDLE h = FindFirstFileW((dest + L".old.*").c_str(), &fd);
  if (h == INVALID_HANDLE_VALUE)
    return;
  do {
    DeleteFileW((dir + fd.cFileName).c_str());
  } while (FindNextFileW(h, &fd));
  FindClose(h);
}

BOOL copy_file(const std::wstring& src, const std::wstring& dest) {
  yiwei_cleanup_old(dest);
  BOOL ret = CopyFile(src.c_str(), dest.c_str(), FALSE);
  if (!ret) {
    for (int i = 0; i < 3 && !ret; ++i) {
      std::wstring old = dest + L".old." + std::to_wstring(GetTickCount64()) +
                         L"." + std::to_wstring(i);
      if (MoveFileEx(dest.c_str(), old.c_str(), MOVEFILE_REPLACE_EXISTING)) {
        MoveFileEx(old.c_str(), NULL, MOVEFILE_DELAY_UNTIL_REBOOT);
        ret = CopyFile(src.c_str(), dest.c_str(), FALSE);
      }
    }
  }
  return ret;
}
"""


def patch_imesetup(text):
    if "yiwei_cleanup_old" in text:
        return text
    nl = "\r\n" if "\r\n" in text else "\n"
    t = text.replace("\r\n", "\n")
    start = t.find("BOOL copy_file(const std::wstring& src, const std::wstring& dest) {\n")
    end = t.find("  return ret;\n}\n", start)
    if start < 0 or end < 0 or "MOVEFILE_DELAY_UNTIL_REBOOT" not in t[start:end]:
        raise SystemExit("imesetup.cpp: copy_file not as expected")
    t = t[:start] + COPY_FILE_NEW + t[end + len("  return ret;\n}\n"):]
    return t.replace("\n", nl)


def guid_struct(g):
    p = g.split("-")
    b = p[3] + p[4]
    tail = ", ".join("0x%02x" % int(b[i:i + 2], 16) for i in range(0, 16, 2))
    return "0x%s, 0x%s, 0x%s, {%s}" % (p[0].lower(), p[1].lower(), p[2].lower(), tail)


def guid_struct_regex(g):
    p = g.split("-")
    b = p[3] + p[4]
    parts = [r"0x0*%s" % p[0].lstrip("0").lower() or "0",
             r"0x0*%s" % (p[1].lstrip("0").lower() or "0"),
             r"0x0*%s" % (p[2].lstrip("0").lower() or "0")]
    tail = [r"0x0*%s" % (b[i:i + 2].lstrip("0").lower() or "0") for i in range(0, 16, 2)]
    sep = r"\s*,\s*"
    return re.compile(sep.join(parts) + sep + r"\{\s*" + sep.join(tail) + r"\s*\}", re.I)


def read(path):
    raw = open(path, "rb").read()
    if raw[:2] == b"\xff\xfe":
        return raw[2:].decode("utf-16-le"), "utf-16-le", b"\xff\xfe"
    if raw[:3] == b"\xef\xbb\xbf":
        return raw[3:].decode("utf-8"), "utf-8", b"\xef\xbb\xbf"
    try:
        return raw.decode("utf-8"), "utf-8", b""
    except UnicodeDecodeError:
        return None, None, None


def appcast_block(name):
    return '%s APPCAST\nBEGIN\n    "%s"\nEND\n' % (name, APPCAST)


def transform(path, text):
    for a, b in REPLACEMENTS:
        text = text.replace(a, b)
    for old, new in GUIDS.items():
        text = text.replace(old, new).replace(old.lower(), new.lower())
        text = guid_struct_regex(old).sub(guid_struct(new), text)
    base = os.path.basename(path)
    if base == "WeaselServerApp.cpp" and "win_sparkle_set_automatic_check_for_updates(0)" not in text:
        # 一维：由一维助手统一检查更新，关闭 WinSparkle 自带的自动检查
        text = re.sub(r"(\r?\n)(\s*)win_sparkle_init\(\);",
                      r"\1\2win_sparkle_set_automatic_check_for_updates(0);\1\2win_sparkle_init();", text, count=1)
    if base == "WeaselUtility.h":
        text = text.replace('return L"Weasel";', 'return L"%s";' % NAME)
    if base.endswith(".nsi"):
        for a, b in NSI_EN:
            text = text.replace(a, b)
    if base == "WeaselServer.rc":
        text = re.sub(r"(FEEDURL|MANUALUPDATEFEEDURL|TESTINGFEEDURL|TESTINGMANUALUPDATEFEEDURL)\s+APPCAST\s*\r?\nBEGIN.*?END\r?\n",
                      lambda m: appcast_block(m.group(1)).replace("\n", "\r\n"), text, flags=re.S)
    if base in EXE_TARGETS:
        text = re.sub(r"(<OutputFile>[^<]*)\$\(ProjectName\)", r"\1$(TargetName)", text)
    if base in EXE_TARGETS and "<TargetName>%s</TargetName>" % EXE_TARGETS[base] not in text:
        imp = '<Import Project="$(VCTargetsPath)\\Microsoft.Cpp.targets" />'
        text = text.replace(imp, "<PropertyGroup>\r\n    <TargetName>%s</TargetName>\r\n  </PropertyGroup>\r\n  %s" % (EXE_TARGETS[base], imp), 1)
    if base == "WeaselTSF.rc":
        text = patch_tsf_rc(text)
    if base == "LanguageBar.cpp":
        text = patch_langbar_cpp(text)
    if base == "imesetup.cpp":
        text = patch_imesetup(text)
    if base in ("WeaselDeployer.rc", "UIStyleSettings.h", "WeaselDeployer.cpp"):
        text = patch_deployer_sources(text, base)
    if base.endswith(".rc"):
        text = text.replace('"Weasel Server"', '"%s"' % NAME)
        text = text.replace('VALUE "ProductName", "Weasel"', 'VALUE "ProductName", "%s"' % NAME)
    return text


def main():
    changed = 0
    for dirpath, dirnames, filenames in os.walk(ROOT):
        rel = os.path.relpath(dirpath, ROOT)
        top = rel.split(os.sep)[0]
        if top in SKIP_DIRS:
            dirnames[:] = []
            continue
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in filenames:
            if os.path.splitext(fn)[1].lower() not in TEXT_EXT:
                continue
            path = os.path.join(dirpath, fn)
            text, enc, bom = read(path)
            if text is None:
                continue
            new = transform(path, text)
            if new != text:
                with open(path, "wb") as f:
                    f.write(bom + new.encode(enc))
                changed += 1
    if os.path.isdir(os.path.join(ROOT, "WeaselDeployer")):
        install_style_dialog()
    # verify nothing important was missed
    leftovers = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in filenames:
            if os.path.splitext(fn)[1].lower() not in TEXT_EXT or fn == "CHANGELOG.md" or fn.endswith(".md"):
                continue
            text, _, _ = read(os.path.join(dirpath, fn))
            if text and ("小狼毫" in text or "A3F4CDED" in text.upper() or "WeaselNamedPipe" in text or "ShowBalloon(info" in text or "display notification when deploying" in text
                         or re.search(r"Weasel(Server|Deployer|Setup)\.exe|weasel(x64|ARM64X?|ARM)?\.dll", text)):
                leftovers.append(os.path.join(dirpath, fn))
    tsf = os.path.join(ROOT, "WeaselTSF")
    if os.path.isdir(tsf):
        rc, _, _ = read(os.path.join(tsf, "WeaselTSF.rc"))
        lb, _, _ = read(os.path.join(tsf, "LanguageBar.cpp"))
        if '"40106"' in rc or "/tray toggle" not in lb or rc.count(", 40106") != 3:
            leftovers.append("WeaselTSF: 语言栏菜单未改成一维菜单")
    ims = os.path.join(ROOT, "WeaselSetup", "imesetup.cpp")
    if os.path.exists(ims) and "yiwei_cleanup_old" not in (read(ims)[0] or ""):
        leftovers.append("WeaselSetup: copy_file 未改成升级免重启版本")
    dep = os.path.join(ROOT, "WeaselDeployer")
    if os.path.isdir(dep):
        drc, _, _ = read(os.path.join(dep, "WeaselDeployer.rc"))
        dcpp, _, _ = read(os.path.join(dep, "WeaselDeployer.cpp"))
        dlg, _, _ = read(os.path.join(dep, "UIStyleSettingsDialog.cpp"))
        if '"中",IDOK' in drc or "YiweiHelperRun(L\"/settings" not in dcpp or ("kYiweiOrder" not in dlg and "YiweiHelperRun" not in dlg) or ("preview_ui_" not in dlg and '"steam"' not in dlg):
            leftovers.append("WeaselDeployer: 界面风格对话框未换成一维版本")
    print("rebranded %d files" % changed)
    if leftovers:
        print("LEFTOVER upstream identity in:", *leftovers, sep="\n  ")
        sys.exit(1)


if __name__ == "__main__":
    main()
