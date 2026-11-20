"""ORIGINAL procedural audio for the 180-second cinematic wedding (Adventure King Reborn).

Everything is synthesised from scratch with numpy (no samples, no soundfonts, no existing melodies), so the
project owns it outright for commercial use. Run with any Python 3 + numpy, e.g. Blender's bundled interpreter:
    python -I wedding_ceremony_audio.py <out_dir>

Score: a waltz in D major, 72 bpm (one bar = 2.5 s), 72 bars = 180 s, sections aligned with the ceremony scenes:
  0-20   entrance   processional theme (harp arpeggios, string pad, celesta melody)
  20-45  opening    soft underscore under the officiant's words
  45-70  vows       tender flute theme
  70-100 rings      theme returns, music-box sparkles, building
  100-120 kiss      swell and climax
  120-145 cake      playful pizzicato
  145-165 toast     bright bells
  165-180 finale    grand cadence
Effects: carpet footstep, ring chime, kiss sparkle, cake-knife cut, glass clink, applause, cheer (no voices).
"""
import os
import sys
import wave

import numpy as np

SR = 32000
rng = np.random.default_rng(20261110)
BEAT = 60.0 / 72.0
BAR = BEAT * 3


def hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12.0)


def t_axis(sec):
    return np.arange(int(sec * SR)) / SR


def place(buf, start, snd, gain=1.0):
    i = int(start * SR)
    if i >= len(buf):
        return
    n = min(len(snd), len(buf) - i)
    buf[i:i + n] += snd[:n] * gain


def bandpass(x, lo, hi):
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    m = np.clip((f - lo) / (lo * 0.3 + 1), 0, 1) * np.clip((hi - f) / (hi * 0.3 + 1), 0, 1)
    return np.fft.irfft(X * m, len(x))


# ------------------------------------------------------------------ instruments
def harp(freq, dur, bright=1.0):
    t = t_axis(dur + 1.6)
    out = np.zeros_like(t)
    for k in range(1, 8):
        inh = 1 + 0.0006 * k * k
        out += np.sin(2 * np.pi * freq * k * inh * t) * (bright / k ** 1.25) * np.exp(-t * (0.9 + 0.75 * k))
    return out * np.minimum(1, t / 0.002)


def pizz(freq):
    t = t_axis(0.45)
    out = sum(np.sin(2 * np.pi * freq * k * t) / k ** 1.4 * np.exp(-t * (9 + 4 * k)) for k in range(1, 6))
    return out * np.minimum(1, t / 0.003)


def celesta(freq, dur):
    t = t_axis(dur + 1.2)
    out = np.sin(2 * np.pi * freq * t) * np.exp(-t * 2.2) + 0.35 * np.sin(2 * np.pi * freq * 2 * t) * np.exp(-t * 4.5) \
        + 0.12 * np.sin(2 * np.pi * freq * 3.0 * t) * np.exp(-t * 7)
    return out * np.minimum(1, t / 0.002)


def bell(freq, dur):
    t = t_axis(dur)
    parts = [(1, 1.0, 1.6), (2.0, 0.5, 2.6), (2.76, 0.32, 3.4), (5.4, 0.18, 6), (8.93, 0.08, 9)]
    out = sum(a * np.sin(2 * np.pi * freq * p * t) * np.exp(-t * d) for p, a, d in parts)
    return out * np.minimum(1, t / 0.001)


def flute(freq, dur):
    t = t_axis(dur)
    vib = 0.006 * np.sin(2 * np.pi * 5.2 * t) * np.clip((t - 0.25) / 0.3, 0, 1)
    ph = 2 * np.pi * np.cumsum(freq * (1 + vib)) / SR
    tone = np.sin(ph) + 0.18 * np.sin(2 * ph) + 0.05 * np.sin(3 * ph)
    breath = bandpass(rng.standard_normal(len(t)), freq * 0.8, freq * 3.5) * 0.04
    att = np.clip(t / 0.09, 0, 1)
    rel = np.clip((dur - t) / 0.18, 0, 1)
    return (tone + breath) * att * rel


def pad(freqs, dur, warmth=1.0):
    t = t_axis(dur + 1.0)
    out = np.zeros_like(t)
    for f in freqs:
        for det in (-0.004, 0.0, 0.0045):
            ff = f * (1 + det)
            out += (np.sin(2 * np.pi * ff * t) + 0.32 * warmth * np.sin(4 * np.pi * ff * t) + 0.12 * warmth * np.sin(6 * np.pi * ff * t)) / 3
    att = np.clip(t / 0.7, 0, 1) ** 1.5
    rel = np.clip((dur + 0.9 - t) / 1.0, 0, 1)
    return out * att * rel / max(1, len(freqs))


def bass(freq, dur):
    t = t_axis(dur + 0.5)
    out = np.sin(2 * np.pi * freq * t) + 0.3 * np.sin(4 * np.pi * freq * t)
    return out * np.minimum(1, t / 0.01) * np.exp(-t * 1.1)


def shimmer(dur):
    t = t_axis(dur)
    n = bandpass(rng.standard_normal(len(t)), 5000, 12000)
    return n * (t / dur) ** 2.2 * np.clip((dur - t) / 0.08, 0, 1)


def reverb(x, seconds=2.2, mix=0.28):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = rng.standard_normal(n) * np.exp(-t * 6.9 / seconds)
    ir = bandpass(ir, 120, 7000)
    ir[0] += 0  # direct sound kept in the dry path
    size = 1 << int(np.ceil(np.log2(len(x) + n)))
    wet = np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)[:len(x) + n]
    wet /= np.max(np.abs(wet)) + 1e-9
    dry = np.concatenate([x, np.zeros(n)])
    dry /= np.max(np.abs(dry)) + 1e-9
    return dry * (1 - mix) + wet * mix


def write(path, x, peak_db=-1.0, mono_len=None):
    x = x - np.mean(x)
    if mono_len is not None:
        x = x[:int(mono_len * SR)]
    fade = int(0.01 * SR)
    x[:fade] *= np.linspace(0, 1, fade)
    x[-fade * 8:] *= np.linspace(1, 0, fade * 8)
    x = x / (np.max(np.abs(x)) + 1e-9) * (10 ** (peak_db / 20))
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    print("wrote", os.path.basename(path), f"{len(x) / SR:.1f}s")


# ------------------------------------------------------------------ the score
# chords per bar as (root midi, quality); D major: D Bm G A, Em A D D, ...
D, Bm, G, A, Em, Fsm, A7, Gm = (50, "M"), (47, "m"), (43, "M"), (45, "M"), (40, "m"), (42, "m"), (45, "7"), (43, "m")
QUAL = {"M": (0, 4, 7), "m": (0, 3, 7), "7": (0, 4, 7, 10)}


def triad(ch, octave=0):
    r, q = ch
    return [r + 12 * octave + i for i in QUAL[q]]


SECTIONS = [  # (name, bars, progression, style)
    ("entrance", 8, [D, Bm, G, A, D, Bm, Em, A7], "proc"),
    ("opening", 10, [D, G, D, A, Bm, G, Em, A, D, D], "soft"),
    ("vows", 10, [G, A, Fsm, Bm, Em, A, D, Bm, G, A7], "tender"),
    ("rings", 12, [D, Bm, G, A, D, Bm, Em, A7, G, A, D, D], "build"),
    ("kiss", 8, [G, A, Fsm, Bm, G, Em, A7, D], "climax"),
    ("cake", 10, [D, A, Bm, Fsm, G, D, Em, A, D, D], "pizz"),
    ("toast", 8, [G, A, D, Bm, G, A, D, D], "bells"),
    ("finale", 6, [G, A, Bm, G, A7, D], "grand"),
]

# original melodic cells (scale degrees in D major, relative to the chord root's register)
SCALE = [0, 2, 4, 5, 7, 9, 11]


def degree(d, base=74):  # base = D5
    o, s = divmod(d, 7)
    return base + 12 * o + SCALE[s]


THEME_A = [[2, 1, 0], [4, 2, 4], [3, 2, 1], [1, -1, 0], [2, 4, 5], [4, 2, 1], [0, 1, 2], [1, 0, -1]]
THEME_B = [[4, 4, 5], [4, 2, 1], [2, 2, 3], [2, 0, -1], [0, 1, 2], [4, 3, 2], [1, 2, 0], [0, -1, 0]]


def score():
    total = sum(b for _, b, _, _ in SECTIONS) * BAR
    music = np.zeros(int((total + 4) * SR))
    bar = 0
    for name, bars, prog, style in SECTIONS:
        for i in range(bars):
            t0 = (bar + i) * BAR
            ch = prog[i % len(prog)]
            notes = triad(ch, 1)                   # chord tones around D4
            root = ch[0]
            last = i == bars - 1 and name == "finale"
            vol = {"proc": 1.0, "soft": 0.62, "tender": 0.8, "build": 0.95, "climax": 1.15, "pizz": 0.85, "bells": 1.0, "grand": 1.2}[style]
            # bass on beat 1
            place(music, t0, bass(hz(root - 12 if root > 45 else root), BAR * 0.9), 0.55 * vol)
            # string pad
            pad_notes = [hz(n) for n in triad(ch, 1)]
            place(music, t0, pad(pad_notes, BAR, 1.2 if style in ("climax", "grand") else 0.8), (0.42 if style != "soft" else 0.34) * vol)
            # harp: waltz arpeggio (oom-pah-pah) or rolling sixths
            if style in ("proc", "soft", "tender", "build", "climax", "bells", "grand"):
                arp = [notes[0], notes[1], notes[2], notes[1] + 12, notes[2], notes[1]]
                for k, n in enumerate(arp):
                    place(music, t0 + k * BEAT / 2, harp(hz(n), 1.2), (0.22 if style == "soft" else 0.28) * vol)
            if style == "pizz":
                for k, n in enumerate([notes[0] - 12, notes[1], notes[2]]):
                    place(music, t0 + k * BEAT, pizz(hz(n)), 0.5 * vol)
                    place(music, t0 + k * BEAT + BEAT / 2, pizz(hz(notes[(k + 1) % 3] + 12)), 0.28 * vol)
            # melody
            cell = (THEME_A if style in ("proc", "build", "climax", "grand", "bells") else THEME_B)[i % 8]
            if style == "soft" and i % 2 == 1:
                cell = None
            if cell and not last:
                shift = 7 if style == "climax" else 0
                for k, d in enumerate(cell):
                    dur = BEAT * (1.4 if k == 2 else 0.95)
                    f = hz(degree(d + (2 if style == "climax" and i >= 4 else 0)) + (0 if style != "pizz" else -12))
                    if style in ("tender", "soft"):
                        place(music, t0 + k * BEAT, flute(f, dur), 0.34 * vol)
                    elif style == "pizz":
                        place(music, t0 + k * BEAT, celesta(f, 0.5), 0.3 * vol)
                    else:
                        place(music, t0 + k * BEAT, flute(f * (2 if shift else 1) if style == "climax" else f, dur), 0.26 * vol)
                        place(music, t0 + k * BEAT, celesta(f * 2, dur), 0.16 * vol)
            # music-box sparkles in the ring section, bells for the toast
            if style == "build" and i >= 4:
                for k in range(6):
                    place(music, t0 + k * BEAT / 2 + BEAT / 4, celesta(hz(notes[k % 3] + 24), 0.4), 0.09)
            if style == "bells":
                place(music, t0, bell(hz(notes[0] + 24), 2.2), 0.12)
                place(music, t0 + BEAT * 2, bell(hz(notes[2] + 24), 1.5), 0.08)
            if last:  # final cadence: rolled D major chord ringing out
                for k, n in enumerate([50, 57, 62, 66, 69, 74, 78, 81]):
                    place(music, t0 + k * 0.07, harp(hz(n), 4.0, 1.2), 0.3)
                place(music, t0, pad([hz(62), hz(66), hz(69), hz(74)], 4.5, 1.3), 0.5)
                place(music, t0 + 0.02, bell(hz(86), 4.0), 0.12)
        if style == "climax":  # swell into the kiss (scene starts at 100 s; the kiss lands ~107.4 s)
            place(music, bar * BAR + 4.6, shimmer(2.8), 0.08)
        bar += bars
    return reverb(music, 2.4, 0.3)


# ------------------------------------------------------------------ effects
def footstep():
    t = t_axis(0.14)
    thud = bandpass(rng.standard_normal(len(t)), 60, 600) * np.exp(-t * 38)
    rustle = bandpass(rng.standard_normal(len(t)), 1500, 5000) * np.exp(-t * 60) * 0.25
    return thud + rustle


def ring_chime():
    out = np.zeros(int(2.4 * SR))
    for k, (f, dly) in enumerate([(1568, 0.0), (2093, 0.06), (2637, 0.12), (3136, 0.2)]):
        place(out, dly, bell(f, 2.2), 0.5 / (1 + k * 0.3))
    place(out, 0, shimmer(0.5)[::-1] * 0.5, 0.4)
    return reverb(out, 1.4, 0.3)


def kiss_sparkle():
    out = np.zeros(int(3.2 * SR))
    for k in range(12):
        f = hz(86 + [0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24, 26][k])
        place(out, k * 0.07, celesta(f, 0.9), 0.35)
    place(out, 0, pad([hz(74), hz(78), hz(81)], 2.4, 0.6), 0.6)
    return reverb(out, 1.8, 0.35)


def knife_cut():
    t = t_axis(0.55)
    slide = bandpass(rng.standard_normal(len(t)), 1800, 6500) * np.sin(np.pi * np.clip(t / 0.42, 0, 1)) ** 1.5 * 0.5
    squish = bandpass(rng.standard_normal(len(t)), 150, 900) * np.exp(-np.maximum(t - 0.3, 0) * 25) * (t > 0.3) * 0.7
    tap = np.sin(2 * np.pi * 900 * t) * np.exp(-np.maximum(t - 0.42, 0) * 60) * (t > 0.42) * 0.25
    return slide + squish + tap


def glass_clink():
    t = t_axis(1.8)
    out = np.zeros_like(t)
    for p, a, d in [(1, 1.0, 2.8), (2.32, 0.55, 4.0), (4.25, 0.33, 6.5), (6.63, 0.18, 9.0)]:
        for det in (-0.002, 0.003):
            out += a * np.sin(2 * np.pi * 2350 * p * (1 + det) * t) * np.exp(-t * d)
    out *= np.minimum(1, t / 0.0006)
    return reverb(out, 1.2, 0.25)


def clap():
    t = t_axis(0.06)
    return bandpass(rng.standard_normal(len(t)), 700 + rng.uniform(0, 600), 4000) * np.exp(-t * (60 + rng.uniform(0, 40)))


def applause(sec=4.5, people=26, bright=1.0):
    out = np.zeros(int((sec + 0.5) * SR))
    for p in range(people):
        rate = rng.uniform(3.2, 4.6)
        start = rng.uniform(0, 0.5)
        stop = sec - rng.uniform(0, 1.2)
        tt = start
        while tt < stop:
            place(out, tt, clap(), rng.uniform(0.4, 1.0) * bright * min(1, (stop - tt) / 0.8))
            tt += 1 / rate * rng.uniform(0.85, 1.15)
    return reverb(out, 1.1, 0.25)


def cheer():
    out = applause(3.5, 18, 1.2)
    t = t_axis(0.9)
    whistle = np.sin(2 * np.pi * np.cumsum(np.linspace(1800, 2600, len(t))) / SR) * np.sin(np.pi * t / 0.9) * 0.35
    place(out, 0.4, whistle, 1)
    place(out, 1.6, whistle[::-1] * 0.8, 1)
    for k in range(6):
        place(out, 0.2 + k * 0.25, celesta(hz(88 + (k % 3) * 4), 0.6), 0.25)
    return out


def main(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    write(os.path.join(out_dir, "BGM_Wedding_Ceremony_180s.wav"), score(), -1.5, mono_len=183.0)
    write(os.path.join(out_dir, "SFX_Wedding_Footstep.wav"), footstep(), -6)
    write(os.path.join(out_dir, "SFX_Wedding_RingChime.wav"), ring_chime(), -2)
    write(os.path.join(out_dir, "SFX_Wedding_KissSparkle.wav"), kiss_sparkle(), -3)
    write(os.path.join(out_dir, "SFX_Wedding_KnifeCut.wav"), knife_cut(), -4)
    write(os.path.join(out_dir, "SFX_Wedding_GlassClink.wav"), glass_clink(), -2)
    write(os.path.join(out_dir, "SFX_Wedding_Applause.wav"), applause(), -3)
    write(os.path.join(out_dir, "SFX_Wedding_Cheer.wav"), cheer(), -3)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Assets/Audio/Wedding")
