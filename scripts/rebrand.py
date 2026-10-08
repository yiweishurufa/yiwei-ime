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
    # verify nothing important was missed
    leftovers = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in filenames:
            if os.path.splitext(fn)[1].lower() not in TEXT_EXT or fn == "CHANGELOG.md" or fn.endswith(".md"):
                continue
            text, _, _ = read(os.path.join(dirpath, fn))
            if text and ("小狼毫" in text or "A3F4CDED" in text.upper() or "WeaselNamedPipe" in text
                         or re.search(r"Weasel(Server|Deployer|Setup)\.exe|weasel(x64|ARM64X?|ARM)?\.dll", text)):
                leftovers.append(os.path.join(dirpath, fn))
    print("rebranded %d files" % changed)
    if leftovers:
        print("LEFTOVER upstream identity in:", *leftovers, sep="\n  ")
        sys.exit(1)


if __name__ == "__main__":
    main()
