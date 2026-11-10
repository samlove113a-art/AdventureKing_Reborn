"""Original per-skill SFX for the 12 class skills (SkillVfxDirector), synthesised from scratch (no samples).
    python -I skill_sfx.py <out_dir>        (Blender's bundled Python has numpy)
44.1 kHz 16-bit mono WAV, peak -1 dBFS, click-free fades. Keys match SkillVfxDirector.Sfx():
  sw* wind & steel · mg* fire / ice / meteor · as* shadow blades · ar* bow, rain, phoenix
"""
import os
import sys
import wave

import numpy as np

SR = 44100
rng = np.random.default_rng(1110)


def t(sec):
    return np.arange(int(sec * SR)) / SR


def env(n, a=0.005, d=0.3, curve=4.0):
    x = np.arange(n) / SR
    att = np.clip(x / max(a, 1e-4), 0, 1)
    dec = np.exp(-np.maximum(x - a, 0) * curve / max(d, 1e-4))
    return att * dec


def noise(sec):
    return rng.standard_normal(int(sec * SR))


def lp(x, cut):
    """One-pole low-pass (cut in Hz, may be an array)."""
    cut = np.broadcast_to(np.asarray(cut, dtype=float), x.shape)
    a = np.exp(-2 * np.pi * cut / SR)
    y = np.zeros_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc = (1 - a[i]) * x[i] + a[i] * acc
        y[i] = acc
    return y


def hp(x, cut):
    return x - lp(x, cut)


def sweep(sec, f0, f1, shape="exp"):
    tt = t(sec)
    f = f0 * (f1 / f0) ** (tt / sec) if shape == "exp" else f0 + (f1 - f0) * tt / sec
    return np.sin(2 * np.pi * np.cumsum(f) / SR), f


def whoosh(sec, f0, f1, q=1.0):
    n = noise(sec)
    _, f = sweep(sec, f0, f1)
    return hp(lp(n, f * 1.6), f * 0.4 / q) * np.sin(np.linspace(0, np.pi, len(n))) ** 1.5


def ring(sec, freqs, decay=0.6, amps=None):
    tt = t(sec)
    amps = amps or [1.0] * len(freqs)
    return sum(a * np.sin(2 * np.pi * f * tt) * np.exp(-tt * (3 + i) / decay) for i, (f, a) in enumerate(zip(freqs, amps)))


def thump(sec, f0=90, f1=40, decay=0.25):
    s, _ = sweep(sec, f0, f1)
    return s * env(len(s), 0.002, decay, 5)


def crackle(sec, density=60):
    out = np.zeros(int(sec * SR))
    for _ in range(int(density * sec)):
        i = rng.integers(0, len(out) - 400)
        out[i:i + 300] += rng.standard_normal(300) * np.exp(-np.arange(300) / 40) * rng.uniform(0.3, 1)
    return hp(out, 900)


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def at(x, sec):
    return np.concatenate([np.zeros(int(sec * SR)), x])


def save(path, x, gain=1.0):
    x = np.asarray(x, dtype=float)
    x[:64] *= np.linspace(0, 1, 64)
    x[-400:] *= np.linspace(1, 0, 400)
    peak = np.max(np.abs(x)) or 1.0
    x = x / peak * 0.89 * gain
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype("<i2").tobytes())


def build():
    S = {}
    # ---- Kiếm Sĩ: wind & steel
    S["sw1_cast"] = mix(whoosh(0.32, 600, 2400) * 0.8, ring(0.3, [2200, 3300], 0.25) * 0.15)
    S["sw1_hit"] = mix(whoosh(0.25, 3000, 900) * 0.6, ring(0.5, [1850, 2770, 4100], 0.35, [0.6, 0.4, 0.25]), thump(0.25, 140, 60, 0.12) * 0.7)
    S["sw2_cast"] = mix(whoosh(0.9, 300, 1600, 0.6) * 0.9, at(whoosh(0.5, 500, 2000), 0.3) * 0.5)
    S["sw2_tick"] = mix(whoosh(0.18, 2400, 1200) * 0.7, ring(0.25, [2600, 3900], 0.2) * 0.3)
    S["sw2_hit"] = mix(whoosh(0.4, 2800, 500) * 0.8, ring(0.6, [1500, 2250, 3400], 0.4, [0.5, 0.35, 0.2]), thump(0.3, 120, 50, 0.18))
    s, _ = sweep(0.9, 220, 880)
    S["sw3_cast"] = mix(s * env(len(s), 0.4, 0.6, 1.5) * 0.35, ring(1.0, [880, 1320, 1760, 2640], 0.9, [0.3, 0.25, 0.2, 0.15]), whoosh(1.0, 200, 3000, 0.5) * 0.4)
    S["sw3_hit"] = mix(thump(0.9, 110, 32, 0.5) * 1.2, noise(0.8) * env(int(0.8 * SR), 0.002, 0.35, 4) * 0.5, ring(1.2, [660, 990, 1320, 1980], 1.0, [0.4, 0.3, 0.25, 0.15]), at(crackle(0.6, 80) * 0.4, 0.05))
    # ---- Pháp Sư: fire, ice, meteor
    S["mg1_cast"] = mix(lp(noise(0.5), 1800) * np.linspace(0.2, 1, int(0.5 * SR)) * 0.5, crackle(0.5, 50) * 0.5, sweep(0.5, 180, 420)[0] * 0.2)
    S["mg1_hit"] = mix(thump(0.6, 90, 35, 0.3), lp(noise(0.7), 2500) * env(int(0.7 * SR), 0.003, 0.3, 4) * 0.9, crackle(0.8, 90) * 0.6)
    S["mg2_cast"] = mix(ring(0.9, [1568, 2093, 2637, 3136], 0.9, [0.3, 0.3, 0.25, 0.2]), hp(noise(0.9), 4000) * np.sin(np.linspace(0, np.pi, int(0.9 * SR))) * 0.25)
    S["mg2_tick"] = mix(ring(0.35, [3520, 4699, 6272], 0.18, [0.5, 0.35, 0.2]), hp(noise(0.15), 3000) * env(int(0.15 * SR), 0.001, 0.05, 5) * 0.6)
    S["mg2_hit"] = mix(hp(noise(0.9), 1500) * env(int(0.9 * SR), 0.005, 0.5, 3) * 0.6, ring(1.0, [1047, 1568, 2093], 0.8, [0.4, 0.3, 0.2]), thump(0.4, 100, 50, 0.2) * 0.5)
    s, _ = sweep(1.3, 70, 260)
    S["mg3_cast"] = mix(s * env(len(s), 0.6, 0.9, 1.2) * 0.5, lp(noise(1.3), 700) * np.linspace(0.1, 1, int(1.3 * SR)) * 0.6, ring(1.3, [311, 466, 622], 1.2, [0.2, 0.15, 0.1]))
    S["mg3_hit"] = mix(thump(1.4, 80, 25, 0.8) * 1.4, lp(noise(1.4), 1800) * env(int(1.4 * SR), 0.003, 0.7, 3) * 1.0, crackle(1.3, 120) * 0.6, at(crackle(0.9, 60) * 0.3, 0.3))
    # ---- Sát Thủ: shadow blades
    S["as1_cast"] = mix(whoosh(0.22, 1500, 4500) * 0.8, ring(0.2, [3100, 4650], 0.12) * 0.2)
    S["as1_hit"] = mix(hp(noise(0.12), 2500) * env(int(0.12 * SR), 0.0008, 0.04, 6), ring(0.2, [4200, 5600], 0.1) * 0.4)
    S["as2_cast"] = mix(lp(noise(0.6), 900) * np.sin(np.linspace(0, np.pi, int(0.6 * SR))) * 0.7, sweep(0.6, 400, 120)[0] * env(int(0.6 * SR), 0.05, 0.4, 2) * 0.3)
    S["as2_tick"] = mix(whoosh(0.16, 3500, 1500) * 0.8, ring(0.15, [3800], 0.1) * 0.3)
    S["as2_hit"] = mix(whoosh(0.3, 4000, 800) * 0.8, lp(noise(0.5), 900) * env(int(0.5 * SR), 0.002, 0.3, 3) * 0.5, ring(0.4, [2900, 4350], 0.25) * 0.3)
    S["as3_cast"] = mix(whoosh(0.4, 800, 3500) * 0.7, lp(noise(0.5), 600) * env(int(0.5 * SR), 0.05, 0.4, 2) * 0.5)
    S["as3_tick"] = mix(hp(noise(0.1), 3000) * env(int(0.1 * SR), 0.0006, 0.03, 6), ring(0.12, [5000], 0.08) * 0.3)
    S["as3_hit"] = mix(whoosh(0.45, 5000, 600) * 0.9, thump(0.7, 100, 30, 0.4) * 0.9, lp(noise(0.8), 1000) * env(int(0.8 * SR), 0.002, 0.4, 3) * 0.5, ring(0.6, [2300, 3450], 0.4) * 0.25)
    # ---- Cung Thủ: bow, rain, phoenix
    creak = lp(noise(0.3), 500) * (1 + np.sin(2 * np.pi * 38 * t(0.3))) * np.linspace(0.2, 1, int(0.3 * SR))
    S["ar1_cast"] = mix(creak * 0.5, at(sweep(0.18, 180, 90)[0] * env(int(0.18 * SR), 0.001, 0.1, 5) * 0.8, 0.3), at(whoosh(0.3, 2000, 5000), 0.32) * 0.6)
    S["ar1_hit"] = mix(thump(0.25, 220, 90, 0.08), hp(noise(0.2), 1500) * env(int(0.2 * SR), 0.001, 0.08, 5) * 0.6, ring(0.3, [1300, 1950], 0.2) * 0.3)
    S["ar2_cast"] = mix(creak * 0.4, at(sweep(0.18, 200, 100)[0] * env(int(0.18 * SR), 0.001, 0.1, 5) * 0.7, 0.3), at(whoosh(0.5, 1500, 6000) * 0.6, 0.32))
    S["ar2_tick"] = mix(thump(0.12, 300, 140, 0.05) * 0.7, hp(noise(0.08), 2500) * env(int(0.08 * SR), 0.0005, 0.03, 5) * 0.5)
    rain = np.zeros(int(0.9 * SR))
    for k in range(14):
        i = int(rng.uniform(0, 0.75) * SR)
        b = thump(0.1, 320, 150, 0.04) * rng.uniform(0.4, 0.9)
        rain[i:i + len(b)] += b[:len(rain) - i]
    S["ar2_hit"] = mix(rain, whoosh(0.9, 3000, 1200) * 0.35)
    s, _ = sweep(1.2, 300, 900)
    S["ar3_cast"] = mix(s * env(len(s), 0.6, 0.8, 1.2) * 0.25, crackle(1.2, 70) * 0.5, lp(noise(1.2), 1500) * np.linspace(0.1, 0.8, int(1.2 * SR)) * 0.5, ring(1.2, [740, 1110, 1480], 1.1, [0.2, 0.15, 0.12]))
    S["ar3_hit"] = mix(thump(0.6, 130, 45, 0.3), lp(noise(0.7), 3000) * env(int(0.7 * SR), 0.002, 0.35, 3) * 0.8, crackle(0.8, 100) * 0.5, sweep(0.5, 1800, 900)[0] * env(int(0.5 * SR), 0.01, 0.3, 3) * 0.25)
    return S


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    sounds = build()
    for k, x in sounds.items():
        save(os.path.join(out, k + ".wav"), x)
    print("skill sfx written:", len(sounds))
