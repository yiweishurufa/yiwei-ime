"""用例：每个函数拿到一个已部署的 Rime，失败就 raise。"""
ALL = []


def case(fn):
    ALL.append((fn.__name__, fn))
    return fn


def first(r):
    p = r.context()["page"]
    return p[0] if p else ""


@case
def basic_nihao(r):
    r.open("rime_ice")
    r.type("nihao")
    assert first(r) == "你好", r.context()["page"]


def yiwei_file(r, name, content=None):
    import os
    d = os.path.join(r.user, "yiwei")
    os.makedirs(d, exist_ok=True)
    p = os.path.join(d, name)
    if content is None:
        if os.path.exists(p):
            os.remove(p)
    else:
        open(p, "w", encoding="utf-8").write(content)


def wait_flags():
    import time
    time.sleep(3.2)  # yiwei_flags.lua 3 秒缓存


def pick_in_pieces(r, code, pieces):
    r.type(code)
    for w in pieces:
        r.pick(w)
    got = r.commit()
    assert got == "".join(pieces), "上屏 %r" % got


@case
def x1_learn_pieces_first(r):
    """X1：yuanyiwei 分次选 苑/祎/玮，选两次后首选就是 苑祎玮；清除后不再置首。"""
    yiwei_file(r, "learn.disabled")
    r.reopen("rime_ice")
    r.type("yuanyiwei")
    before = first(r)
    r.clear()
    assert before != "苑祎玮", before
    for _ in range(2):
        pick_in_pieces(r, "yuanyiwei", ["苑", "祎", "玮"])
    r.type("yuanyiwei")
    assert first(r) == "苑祎玮", r.context()["page"]
    r.clear()
    # 清除最近一小时的学习：习惯和 RIME 用户词库里的 苑祎玮 都删掉
    yiwei_file(r, "learn.clear", "3600")
    r.type("yuanyiwei")
    r.clear()
    r.type("yuanyiwei")
    assert first(r) == before, r.context()["page"]
    r.clear()


@case
def x1_habit_single_char(r):
    """X1：常选的单字（选两次）持续排第一，关掉后恢复原顺序。"""
    r.reopen("rime_ice")
    r.type("shi")
    page = r.context()["page"]
    r.clear()
    target = page[4]
    for _ in range(2):
        r.type("shi"); r.pick(target); assert r.commit() == target
    r.type("shi")
    assert first(r) == target, r.context()["page"]
    r.clear()
    yiwei_file(r, "learn.disabled", "1")
    wait_flags()
    r.type("shi")
    off = r.context()["page"]
    r.clear()
    yiwei_file(r, "learn.disabled")
    wait_flags()
    yiwei_file(r, "learn.clear", "all")
    r.type("shi"); r.clear()
    print("    关闭时 shi →", off[:5])
