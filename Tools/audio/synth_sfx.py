"""Original combat SFX for Adventure King, synthesised from scratch (no samples, no third-party audio).
Run with any Python 3 + numpy (e.g. Blender's bundled interpreter):
    python -I synth_sfx.py <out_dir>
Writes 44.1 kHz 16-bit mono WAVs, peak-normalised to -1 dBFS, with short fades (click-free).
Each class has its own sonic identity:
  Swordsman  airy steel swing, bright metallic clash with low body thump, heavy charged slash
  Mage       soft magical swish, crystalline bell pop, rising FM charge, deep burst + shimmer tail
  Assassin   short razor swish, twin quick blade ticks, dark swell, rapid slice
  Archer     bow creak, wooden arrow thunk, string tension, triple string-snap volley
"""
import os
import sys
import wave

import numpy as np

SR = 44100
rng = np.random.default_rng(7)


def t_axis(sec):
    return np.arange(int(sec * SR)) / SR


def noise(sec):
    return rng.standard_normal(int(sec * SR))


def env(sec, attack=0.005, decay=0.2, curve=4.0):
    t = t_axis(sec)
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    d = np.exp(-np.maximum(t - attack, 0) * curve / max(decay, 1e-4))
    return a * d


def biquad_bp(x, f0, q):
    """Band-pass with optionally time-varying centre frequency (array or scalar)."""
    f0 = np.broadcast_to(np.asarray(f0, dtype=float), x.shape)
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for n in range(len(x)):
        w0 = 2 * np.pi * min(f0[n], SR * 0.45) / SR
        alpha = np.sin(w0) / (2 * q)
        b0, b2 = alpha, -alpha
        a0, a1, a2 = 1 + alpha, -2 * np.cos(w0), 1 - alpha
        yn = (b0 * x[n] + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1 = x1, x[n]
        y2, y1 = y1, yn
        y[n] = yn
    return y


def lowpass(x, fc):
    a = np.exp(-2 * np.pi * fc / SR)
    y = np.zeros_like(x)
    acc = 0.0
    for n in range(len(x)):
        acc = (1 - a) * x[n] + a * acc
        y[n] = acc
    return y


def sine(freq, sec, phase=0.0):
    t = t_axis(sec)
    f = np.broadcast_to(np.asarray(freq, dtype=float), t.shape)
    return np.sin(2 * np.pi * np.cumsum(f) / SR + phase)


def metal(base, sec, decay, partials=(1.0, 2.76, 5.40, 8.93, 13.34), detune=0.003):
    out = np.zeros(int(sec * SR))
    for i, p in enumerate(partials):
        for d in (-detune, detune):
            out += sine(base * p * (1 + d), sec) * env(sec, 0.001, decay / (1 + i * 0.6), 5) / (1 + i * 0.7)
    return out


def pluck(freq, sec, damp=0.996):
    n = int(sec * SR)
    period = int(SR / freq)
    buf = rng.uniform(-1, 1, period)
    out = np.zeros(n)
    for i in range(n):
        out[i] = buf[i % period]
        buf[i % period] = damp * 0.5 * (buf[i % period] + buf[(i + 1) % period])
    return out


def reverb(x, mix=0.18, room=0.78):
    delays = [1557, 1617, 1491, 1422]
    y = np.zeros(len(x) + SR // 2)
    xp = np.concatenate([x, np.zeros(SR // 2)])
    for d in delays:
        buf = np.zeros(len(xp))
        for n in range(len(xp)):
            buf[n] = xp[n] + (room * buf[n - d] if n >= d else 0.0)
        y += buf
    y = lowpass(y / len(delays), 6000)
    return xp * (1 - mix) + y * mix


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def at(x, sec):
    return np.concatenate([np.zeros(int(sec * SR)), x])


def finish(x, path, peak_db=-1.0):
    x = x - np.mean(x)
    fade = int(0.004 * SR)
    x[:fade] *= np.linspace(0, 1, fade)
    x[-fade * 4:] *= np.linspace(1, 0, fade * 4)
    x = x / (np.max(np.abs(x)) + 1e-9) * (10 ** (peak_db / 20))
    data = (x * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print("wrote", os.path.basename(path), f"{len(x) / SR:.2f}s")


def swish(sec, f_start, f_end, q, shape=1.6):
    t = t_axis(sec)
    sweep = f_start * (f_end / f_start) ** (t / sec)
    e = np.sin(np.pi * np.clip(t / sec, 0, 1)) ** shape
    return biquad_bp(noise(sec), sweep, q) * e


def build(out):
    os.makedirs(out, exist_ok=True)
    P = lambda n: os.path.join(out, n + ".wav")
    # ---------------- Swordsman
    finish(reverb(swish(0.26, 700, 2600, 1.6), 0.08), P("Swordsman_AttackWhoosh"))
    clash = mix(metal(1150, 0.7, 0.35), biquad_bp(noise(0.08), 3500, 0.8) * env(0.08, 0.001, 0.03) * 2.5,
                sine(np.linspace(140, 70, int(0.25 * SR)), 0.25) * env(0.25, 0.002, 0.12) * 1.2)
    finish(reverb(clash, 0.15), P("Swordsman_AttackImpact"))
    charge = mix(swish(0.55, 300, 1800, 2.2, 1.0) * 0.8, metal(620, 0.6, 0.5) * np.linspace(0, 1, int(0.6 * SR)) * 0.5)
    finish(reverb(charge, 0.2), P("Swordsman_SkillCast"))
    heavy = mix(sine(np.linspace(95, 38, int(0.5 * SR)), 0.5) * env(0.5, 0.002, 0.3) * 1.6, metal(820, 0.8, 0.45) * 0.9,
                biquad_bp(noise(0.2), 1800, 0.6) * env(0.2, 0.001, 0.08) * 2.0)
    finish(reverb(heavy, 0.2), P("Swordsman_SkillImpact"))
    # ---------------- Mage
    t = t_axis(0.35)
    finish(reverb(mix(swish(0.35, 1200, 4200, 2.5) * 0.6, sine(np.linspace(600, 1400, len(t)), 0.35) * env(0.35, 0.05, 0.25) * 0.25), 0.3), P("Mage_AttackWhoosh"))
    bell = mix(*[sine(f, 0.9) * env(0.9, 0.001, 0.5 / (i + 1), 4) / (i + 1) for i, f in enumerate([1318, 1976, 2637, 3951])],
               biquad_bp(noise(0.25), 6000, 1.5) * env(0.25, 0.001, 0.12) * 0.6)
    finish(reverb(bell, 0.3), P("Mage_AttackImpact"))
    tt = t_axis(1.1)
    car = 220 * (1 + 1.6 * tt / 1.1)
    fm = np.sin(2 * np.pi * np.cumsum(car) / SR + 2.5 * np.sin(2 * np.pi * np.cumsum(car * 1.5) / SR))
    arp = mix(*[at(sine(f, 0.22) * env(0.22, 0.002, 0.15), i * 0.18) for i, f in enumerate([523, 659, 784, 1047, 1319])])
    finish(reverb(mix(fm * np.linspace(0.1, 0.8, len(tt)) * 0.5, arp * 0.5, swish(1.1, 400, 5000, 3.0, 0.6) * 0.4), 0.35), P("Mage_SkillCast"))
    boom = mix(sine(np.linspace(110, 32, int(0.9 * SR)), 0.9) * env(0.9, 0.003, 0.6) * 1.7,
               lowpass(noise(0.6), 1800) * env(0.6, 0.002, 0.35) * 2.2, at(bell * 0.6, 0.05))
    finish(reverb(boom, 0.3), P("Mage_SkillImpact"))
    # ---------------- Assassin
    finish(swish(0.13, 2200, 7000, 3.0, 1.2), P("Assassin_AttackWhoosh"))
    tick = mix(metal(2400, 0.25, 0.08, (1.0, 2.4, 4.1)), biquad_bp(noise(0.03), 7000, 1.0) * env(0.03, 0.0005, 0.012) * 2)
    finish(reverb(mix(tick, at(tick * 0.85, 0.075)), 0.08), P("Assassin_AttackImpact"))
    dark = mix(swish(0.6, 150, 900, 1.2, 0.8), sine(np.linspace(80, 55, int(0.6 * SR)), 0.6) * np.linspace(0, 1, int(0.6 * SR)) ** 2 * 0.7)
    finish(reverb(dark, 0.25), P("Assassin_SkillCast"))
    slice_ = mix(swish(0.1, 3000, 8000, 2.5, 1.0), at(tick * 0.9, 0.04))
    finish(reverb(slice_, 0.1), P("Assassin_SkillImpact"))
    # ---------------- Archer
    tc = t_axis(0.32)
    creak = lowpass(noise(0.32), 900) * (0.6 + 0.4 * np.sign(np.sin(2 * np.pi * 38 * tc))) * env(0.32, 0.08, 0.3, 2)
    finish(reverb(mix(creak, sine(np.linspace(180, 260, len(tc)), 0.32) * 0.08), 0.1), P("Archer_AttackWhoosh"))
    thunk = mix(pluck(170, 0.35, 0.985) * 0.8, lowpass(noise(0.06), 2500) * env(0.06, 0.0005, 0.03) * 2.0,
                sine(np.linspace(200, 90, int(0.12 * SR)), 0.12) * env(0.12, 0.001, 0.06))
    finish(reverb(thunk, 0.12), P("Archer_AttackImpact"))
    tension = mix(lowpass(noise(0.7), 700) * np.linspace(0.2, 1, int(0.7 * SR)) * 0.8, sine(np.linspace(140, 320, int(0.7 * SR)), 0.7) * 0.12)
    finish(reverb(tension, 0.12), P("Archer_SkillCast"))
    snap = mix(pluck(392, 0.4, 0.99), swish(0.18, 1500, 4500, 2.0) * 0.5)
    finish(reverb(mix(snap, at(snap * 0.9, 0.07), at(snap * 0.8, 0.14)), 0.15), P("Archer_SkillImpact"))
    # ---------------- shared footstep (soft earth)
    step = mix(lowpass(noise(0.09), 700) * env(0.09, 0.002, 0.04) * 1.5, sine(np.linspace(120, 70, int(0.06 * SR)), 0.06) * env(0.06, 0.001, 0.03))
    finish(step, P("Footstep_Earth"), -6.0)


if __name__ == "__main__":
    build(sys.argv[1] if len(sys.argv) > 1 else "Assets/Audio/SFX")
