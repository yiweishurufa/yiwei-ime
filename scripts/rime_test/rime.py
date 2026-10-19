"""一维输入法 · Rime 输入测试用的最小 librime 绑定（ctypes，Linux / Windows 通用）。

只用 RimeApi 结构里排在前面、librime 1.x 一直不变的函数（按下标取函数指针）。
"""
import ctypes as C
import os
import sys

# RimeApi 里函数的下标（rime_api.h，data_size 之后按顺序；1.x 只追加不改顺序）
API = dict(setup=0, initialize=2, finalize=3, start_maintenance=4, join_maintenance_thread=6,
           deploy_config_file=11, sync_user_data=12, create_session=13, destroy_session=15,
           process_key=18, commit_composition=19, clear_composition=20, get_commit=21, free_commit=22,
           get_context=23, free_context=24, set_option=27, get_option=28, select_schema=34,
           get_input=69, select_candidate=71, candidate_list_begin=75, candidate_list_next=76,
           candidate_list_end=77, set_caret_pos=73, get_caret_pos=70)


class Traits(C.Structure):
    _fields_ = [("data_size", C.c_int), ("shared_data_dir", C.c_char_p), ("user_data_dir", C.c_char_p),
                ("distribution_name", C.c_char_p), ("distribution_code_name", C.c_char_p),
                ("distribution_version", C.c_char_p), ("app_name", C.c_char_p), ("modules", C.c_void_p),
                ("min_log_level", C.c_int), ("log_dir", C.c_char_p), ("prebuilt_data_dir", C.c_char_p),
                ("staging_dir", C.c_char_p)]


class Composition(C.Structure):
    _fields_ = [("length", C.c_int), ("cursor_pos", C.c_int), ("sel_start", C.c_int), ("sel_end", C.c_int),
                ("preedit", C.c_char_p)]


class Candidate(C.Structure):
    _fields_ = [("text", C.c_char_p), ("comment", C.c_char_p), ("reserved", C.c_void_p)]


class Menu(C.Structure):
    _fields_ = [("page_size", C.c_int), ("page_no", C.c_int), ("is_last_page", C.c_int), ("highlighted", C.c_int),
                ("num_candidates", C.c_int), ("candidates", C.POINTER(Candidate)), ("select_keys", C.c_char_p)]


class Context(C.Structure):
    _fields_ = [("data_size", C.c_int), ("composition", Composition), ("menu", Menu),
                ("commit_text_preview", C.c_char_p), ("select_labels", C.c_void_p)]


class Commit(C.Structure):
    _fields_ = [("data_size", C.c_int), ("text", C.c_char_p)]


class Iterator(C.Structure):
    _fields_ = [("ptr", C.c_void_p), ("index", C.c_int), ("candidate", Candidate)]


def _sz(s):
    o = s()
    o.data_size = C.sizeof(s) - C.sizeof(C.c_int)
    return o


def _d(b):
    return b.decode("utf-8", "replace") if b else ""


KEYS = {" ": 0x20, "\r": 0xff0d, "\b": 0xff08, "\x1b": 0xff1b, "LEFT": 0xff51, "RIGHT": 0xff53,
        "UP": 0xff52, "DOWN": 0xff54, "HOME": 0xff50, "END": 0xff57, "PGUP": 0xff55, "PGDN": 0xff56,
        "TAB": 0xff09, "DEL": 0xffff, "BS": 0xff08, "RET": 0xff0d, "ESC": 0xff1b,
        "Shift_L": 0xffe1, "Shift_R": 0xffe2, "Control_L": 0xffe3}
MODS = {"Shift": 1, "Control": 4, "Alt": 8, "Release": 1 << 30}


class Rime:
    def __init__(self, lib, shared, user, log_level=1):
        self.dll = C.CDLL(lib)
        self.dll.rime_get_api.restype = C.POINTER(C.c_void_p)
        api = self.dll.rime_get_api()
        P, I, S, B = C.c_void_p, C.c_int, C.c_size_t, C.c_char_p
        sig = dict(setup=(None, P), initialize=(None, P), finalize=(None,), start_maintenance=(I, I),
                   join_maintenance_thread=(None,), deploy_config_file=(I, B, B), sync_user_data=(I,),
                   create_session=(S,), destroy_session=(I, S), process_key=(I, S, I, I),
                   commit_composition=(I, S), clear_composition=(None, S), get_commit=(I, S, P),
                   free_commit=(I, P), get_context=(I, S, P), free_context=(I, P), set_option=(None, S, B, I),
                   get_option=(I, S, B), select_schema=(I, S, B), get_input=(B, S), select_candidate=(I, S, S),
                   candidate_list_begin=(I, S, P), candidate_list_next=(I, P), candidate_list_end=(None, P),
                   set_caret_pos=(None, S, S), get_caret_pos=(S, S))
        for name, i in API.items():
            res, *args = sig[name]
            setattr(self, name, C.CFUNCTYPE(res, *args)(api[1 + i]))
        os.makedirs(user, exist_ok=True)
        self.shared, self.user = os.path.abspath(shared), os.path.abspath(user)
        t = _sz(Traits)
        t.shared_data_dir = self.shared.encode()
        t.user_data_dir = self.user.encode()
        t.distribution_name = "一维输入法".encode()
        t.distribution_code_name = b"yiwei-test"
        t.distribution_version = b"0"
        t.app_name = b"rime.yiwei-test"
        t.min_log_level = log_level
        t.log_dir = self.user.encode()
        self._traits = t
        self.setup(C.cast(C.byref(t), C.c_void_p))
        self.initialize(C.cast(C.byref(t), C.c_void_p))
        self.session = 0

    def deploy(self, full=True):
        self.start_maintenance(1 if full else 0)
        self.join_maintenance_thread()

    def open(self, schema):
        if not self.session:
            self.session = self.create_session()
        if not self.select_schema(self.session, schema.encode()):
            raise RuntimeError("cannot select schema %s" % schema)
        self.clear()

    def reopen(self, schema):
        """新会话（lua 组件重新 init，相当于切换输入框）。"""
        if self.session:
            self.destroy_session(self.session)
        self.session = 0
        self.open(schema)

    def clear(self):
        self.clear_composition(self.session)
        self.commit()

    def key(self, k):
        mask = 0
        if "+" in k and len(k) > 1:
            *mods, k = k.split("+")
            for m in mods:
                mask |= MODS[m]
        code = KEYS.get(k, ord(k) if len(k) == 1 else None)
        if code is None:
            raise ValueError("unknown key %r" % k)
        return self.process_key(self.session, code, mask)

    def type(self, text):
        for ch in text:
            self.key(ch)

    def context(self):
        c = _sz(Context)
        if not self.get_context(self.session, C.cast(C.byref(c), C.c_void_p)):
            return {"preedit": "", "page": [], "comments": [], "highlighted": 0}
        m = c.menu
        r = {"preedit": _d(c.composition.preedit), "cursor": c.composition.cursor_pos,
             "page": [_d(m.candidates[i].text) for i in range(m.num_candidates)],
             "comments": [_d(m.candidates[i].comment) for i in range(m.num_candidates)],
             "highlighted": m.highlighted, "page_size": m.page_size}
        self.free_context(C.cast(C.byref(c), C.c_void_p))
        return r

    def candidates(self, limit=300):
        it = Iterator()
        out = []
        if not self.candidate_list_begin(self.session, C.cast(C.byref(it), C.c_void_p)):
            return out
        while len(out) < limit and self.candidate_list_next(C.cast(C.byref(it), C.c_void_p)):
            out.append((_d(it.candidate.text), _d(it.candidate.comment)))
        self.candidate_list_end(C.cast(C.byref(it), C.c_void_p))
        return out

    def texts(self, limit=300):
        return [t for t, _ in self.candidates(limit)]

    def pick(self, text, limit=300):
        """在整个候选列表里找到 text 并选中（像翻页后按数字键）。"""
        texts = self.texts(limit)
        if text not in texts:
            raise AssertionError("候选里没有 %r（前 12 个：%s）" % (text, texts[:12]))
        self.select_candidate(self.session, texts.index(text))

    def commit(self):
        c = _sz(Commit)
        if not self.get_commit(self.session, C.cast(C.byref(c), C.c_void_p)):
            return ""
        t = _d(c.text)
        self.free_commit(C.cast(C.byref(c), C.c_void_p))
        return t

    def input(self):
        return _d(self.get_input(self.session))

    def option(self, name, value=None):
        if value is None:
            return bool(self.get_option(self.session, name.encode()))
        self.set_option(self.session, name.encode(), 1 if value else 0)

    def close(self):
        if self.session:
            self.destroy_session(self.session)
            self.session = 0
        self.finalize()


def default_lib():
    if sys.platform == "win32":
        return "rime.dll"
    for p in ("/usr/lib/x86_64-linux-gnu/librime.so.1", "/usr/lib/aarch64-linux-gnu/librime.so.1",
              "/usr/lib/librime.so.1", "/usr/local/lib/librime.so.1"):
        if os.path.exists(p):
            return p
    return "librime.so.1"
