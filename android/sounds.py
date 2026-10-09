"""一维输入法（安卓）按键音：五套音效全部用代码合成，不使用任何第三方录音素材。

每套音效生成 key1/key2（字母数字，随机交替）、space、del、enter 五个 wav，
外加 Trime 的 *.sound.yaml 描述文件。
"""
import math
import os
import random
import struct
import wave

SR = 44100


def env(n, attack=0.002, decay=0.03):
    a = max(1, int(attack * SR))
    out = []
    for i in range(n):
        t = i / SR
        e = (i / a) if i < a else math.exp(-(t - attack) / decay)
        out.append(e)
    return out


def tone(freqs, dur, decay, attack=0.001, sweep=None, noise=0.0, seed=1, gain=0.8):
    rnd = random.Random(seed)
    n = int(dur * SR)
    e = env(n, attack, decay)
    out = []
    ph = [0.0] * len(freqs)
    lp = 0.0
    for i in range(n):
        t = i / SR
        s = 0.0
        for k, (f, amp) in enumerate(freqs):
            if sweep:
                f = f * (1 + sweep * min(1.0, t / dur))
            ph[k] += 2 * math.pi * f / SR
            s += amp * math.sin(ph[k])
        if noise:
            lp = lp * 0.55 + rnd.uniform(-1, 1) * 0.45  # soft low-pass on the noise
            s += noise * lp * math.exp(-t / 0.006)
        out.append(s * e[i] * gain)
    peak = max(1e-6, max(abs(x) for x in out))
    return [x / peak * gain for x in out]


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, x in enumerate(p):
            out[i] += x
    peak = max(1e-6, max(abs(x) for x in out))
    return [x / peak * 0.8 for x in out]


def save(path, samples):
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        # 3 ms fade out to avoid clicks at the tail
        f = int(0.003 * SR)
        s = list(samples)
        for i in range(1, min(f, len(s)) + 1):
            s[-i] *= i / f
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, x)) * 32000)) for x in s))


def pack_click():
    return {
        "key1": tone([(2600, 1), (5200, .25)], .045, .010),
        "key2": tone([(2450, 1), (4900, .25)], .045, .010, seed=2),
        "space": tone([(1500, 1), (3000, .3)], .06, .016),
        "del": tone([(1900, 1), (3800, .2)], .05, .012),
        "enter": tone([(1200, 1), (2400, .3)], .08, .022),
    }


def pack_mech():
    def k(f, seed):
        return mix(tone([(f, 1), (f * 2.7, .3)], .09, .022, noise=2.5, seed=seed), tone([(4200, .4)], .02, .004, seed=seed))
    return {
        "key1": k(190, 3), "key2": k(210, 4),
        "space": mix(tone([(120, 1), (300, .4)], .13, .035, noise=3, seed=5)),
        "del": k(170, 6),
        "enter": mix(tone([(140, 1), (380, .4)], .12, .03, noise=3, seed=7), tone([(3600, .5)], .03, .006, seed=8)),
    }


def pack_typewriter():
    def k(f, seed):
        return mix(tone([(f, 1), (f * 1.52, .6), (f * 2.4, .4)], .07, .012, noise=1.5, seed=seed), tone([(220, .5)], .05, .02))
    return {
        "key1": k(3100, 9), "key2": k(2900, 10),
        "space": mix(tone([(900, .6), (180, 1)], .09, .025, noise=2, seed=11)),
        "del": k(2500, 12),
        "enter": tone([(2093, 1), (4186, .35), (6279, .15)], .55, .18),  # bell
    }


def pack_drop():
    return {
        "key1": tone([(700, 1)], .07, .018, sweep=1.1),
        "key2": tone([(780, 1)], .07, .018, sweep=1.0, seed=13),
        "space": tone([(480, 1)], .1, .028, sweep=0.9),
        "del": tone([(900, 1)], .06, .014, sweep=-0.35),
        "enter": tone([(520, 1), (1040, .2)], .14, .04, sweep=1.2),
    }


def pack_wood():
    def k(f):
        return tone([(f, 1), (f * 2.76, .45), (f * 5.4, .15)], .11, .025, attack=0.0005)
    return {"key1": k(820), "key2": k(880), "space": k(560), "del": k(700), "enter": k(440)}


PACKS = [
    ("yiwei_click", "一维 · 清脆", pack_click),
    ("yiwei_mech", "一维 · 机械键盘", pack_mech),
    ("yiwei_typewriter", "一维 · 打字机", pack_typewriter),
    ("yiwei_drop", "一维 · 水滴", pack_drop),
    ("yiwei_wood", "一维 · 木鱼", pack_wood),
]

ORDER = ["key1", "space", "del", "enter", "key2"]

YAML = """name: "{name}"
folder: {folder}
sound:
{sounds}
keyset:
  - {{keys: [KEYCODE_SPACE], inOrder: true, sounds: [1]}}
  - {{keys: [KEYCODE_DEL, KEYCODE_FORWARD_DEL], inOrder: true, sounds: [2]}}
  - {{keys: [KEYCODE_ENTER], inOrder: true, sounds: [3]}}
  - {{min: KEYCODE_0, max: KEYCODE_9, inOrder: false, sounds: [0, 4]}}
  - {{min: KEYCODE_A, max: KEYCODE_Z, inOrder: false, sounds: [0, 4]}}
"""


def build(dest):
    """Write every pack into <dest> (a `soundeffect` directory)."""
    os.makedirs(dest, exist_ok=True)
    for folder, name, fn in PACKS:
        d = os.path.join(dest, folder)
        os.makedirs(d, exist_ok=True)
        snd = fn()
        for k in ORDER:
            save(os.path.join(d, k + ".wav"), snd[k])
        with open(os.path.join(dest, folder + ".sound.yaml"), "w", encoding="utf-8") as f:
            f.write(YAML.format(name=name, folder=folder, sounds="\n".join("  - %s.wav" % k for k in ORDER)))


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
