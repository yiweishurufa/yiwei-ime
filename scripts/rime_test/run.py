#!/usr/bin/env python3
"""一维输入法 · Rime 输入测试（输入核心：雾凇数据 + 一维 Lua + 方案补丁）。

  python scripts/rime_test/run.py prepare <rime-ice full.zip> <shared_dir>
      按安装包同样的方式（scripts/customize_data.py）摆好共享数据，不需要 Weasel 源码。
  python scripts/rime_test/run.py test <shared_dir> <user_dir> [--lib librime] [-k 名字片段]
      部署后逐个跑 cases.py 里的用例；任何一个失败退出码为 1。

Linux（CI 用 Debian 的 librime + librime-plugin-lua）和 Windows（安装目录的 rime.dll）都能跑。
"""
import argparse
import os
import shutil
import sys
import time
import traceback

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))


def prepare(ice_zip, shared):
    import customize_data as cd
    if os.path.isdir(shared):
        shutil.rmtree(shared)
    os.makedirs(shared)
    cd.DATA = shared
    cd.ICE_ZIP = ice_zip
    cd.install_rime_ice()
    cd.customize_default_yaml()
    cd.mount_into_schemas()
    print("prepared", shared)


def test(shared, user, lib, only):
    from rime import Rime, default_lib
    import cases
    t0 = time.time()
    os.makedirs(user, exist_ok=True)
    os.chdir(user)  # Windows 专用 Lua 在 Linux 上会把命名管道当普通文件写到当前目录
    cases.setup(user)  # 部署前写好助手会生成的 *.custom.yaml
    r = Rime(lib or default_lib(), shared, user)
    r.deploy(True)
    print("deployed in %.0fs" % (time.time() - t0))
    failed = []
    for name, fn in cases.ALL:
        if only and not any(o in name for o in only):
            continue
        try:
            fn(r)
            print("PASS", name)
        except Exception as e:  # noqa
            failed.append(name)
            print("FAIL", name, "-", e)
            traceback.print_exc(limit=3)
        finally:
            try:
                r.clear()
            except Exception:
                pass
    r.close()
    if failed:
        print("\n%d 个用例失败：%s" % (len(failed), ", ".join(failed)))
        sys.exit(1)
    print("\n全部通过")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("prepare")
    p.add_argument("zip")
    p.add_argument("shared")
    p = sub.add_parser("test")
    p.add_argument("shared")
    p.add_argument("user")
    p.add_argument("--lib")
    p.add_argument("-k", action="append")
    a = ap.parse_args()
    if a.cmd == "prepare":
        prepare(a.zip, a.shared)
    else:
        test(a.shared, a.user, a.lib, a.k)
