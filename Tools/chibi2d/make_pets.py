"""Original 2D chibi pets (side view, facing right): cloud cat, chick, jade dragon, fire fox.
Analytic shapes, hard 3-tone cel shading, 1 px ink outline, 128 px pixel frames, hard alpha.
Run with Blender's python (numpy only):  python make_pets.py <out_dir>
Output: <out_dir>/<Pet>.png (idle, with open eye) and <Pet>_hungry.png (droopy eye, paler). Game animates hop/squash/flip."""
import os
import struct
import sys
import zlib

import numpy as np

PX, SS = 128, 4
N = PX * SS
_Y, _X = np.mgrid[0:N, 0:N].astype(np.float32)
X, Y = (_X + 0.5) / SS, (_Y + 0.5) / SS          # design space = 128 px, y down
INK = np.array([0.16, 0.08, 0.22], np.float32)


def rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], np.float32)


def ell(cx, cy, rx, ry, rot=0.0):
    c, s = np.cos(np.radians(rot)), np.sin(np.radians(rot))
    dx, dy = X - cx, Y - cy
    u, v = (c * dx + s * dy) / rx, (-s * dx + c * dy) / ry
    return (np.sqrt(u * u + v * v) - 1.0) * min(rx, ry)


def tri(a, b, c):
    """Signed-ish distance to a triangle (negative inside), good enough for ears / horns."""
    pts = [a, b, c]
    inside = np.ones_like(X, bool)
    dist = np.full_like(X, 1e9)
    sgn = 1 if ((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])) > 0 else -1
    for i in range(3):
        p, q = pts[i], pts[(i + 1) % 3]
        ex, ey = q[0] - p[0], q[1] - p[1]
        L = np.hypot(ex, ey)
        d = sgn * ((X - p[0]) * ey - (Y - p[1]) * ex) / L
        inside &= d <= 0
        t = np.clip(((X - p[0]) * ex + (Y - p[1]) * ey) / (L * L), 0, 1)
        dist = np.minimum(dist, np.hypot(X - p[0] - t * ex, Y - p[1] - t * ey))
    return np.where(inside, -dist, dist)


class Canvas:
    def __init__(self):
        self.col = np.zeros((N, N, 3), np.float32)
        self.cov = np.zeros((N, N), bool)

    def part(self, sd, base, shade=None, light=None, outline=True):
        m = sd < 0
        c = np.broadcast_to(base, (N, N, 3)).copy()
        if shade is not None:      # shadow on the lower-right, light on the upper-left (offset copy of the shape)
            off = np.roll(np.roll(sd, int(1.6 * SS), axis=0), int(-1.6 * SS), axis=1)
            c[m & (off >= 0)] = shade
        if light is not None:
            off = np.roll(np.roll(sd, int(-1.8 * SS), axis=0), int(1.8 * SS), axis=1)
            c[m & (off >= 0) & ~(m & (np.roll(np.roll(sd, int(1.6 * SS), 0), int(-1.6 * SS), 1) >= 0))] = light
        if outline:
            ring = (sd >= 0) & (sd < 0.9)
            self.col[ring] = INK
        self.col[m] = c[m]
        self.cov |= m | (outline & (sd >= 0) & (sd < 0.9))

    def eye(self, cx, cy, hungry):
        self.part(ell(cx, cy, 2.6, 3.4 if not hungry else 2.0), rgb("#241638"), outline=False)
        self.part(ell(cx - 0.8, cy - 1.2, 0.9, 1.0), rgb("#ffffff"), outline=False)

    def png(self, path):
        small = np.zeros((PX, PX, 4), np.float32)
        cov = self.cov.reshape(PX, SS, PX, SS).mean((1, 3))
        col = self.col.reshape(PX, SS, PX, SS, 3).mean((1, 3))
        small[..., :3] = col
        small[..., 3] = (cov > 0.5)
        # thin silhouette outline
        a = small[..., 3] > 0
        edge = a & ~(np.roll(a, 1, 0) & np.roll(a, -1, 0) & np.roll(a, 1, 1) & np.roll(a, -1, 1))
        small[edge, :3] = INK
        small[~a, :3] = 0
        write_png(path, (np.clip(small, 0, 1) * 255).astype(np.uint8))


def write_png(path, arr):
    h, w = arr.shape[:2]
    raw = b"".join(b"\x00" + arr[y].tobytes() for y in range(h))
    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def pale(c, hungry):
    return c * 0.82 + 0.1 if hungry else c


def cat(hungry):
    cv = Canvas()
    fur, sh, hi, pink = pale(rgb("#f6ece0"), hungry), pale(rgb("#d4c2b4"), hungry), rgb("#ffffff"), rgb("#ffb3c2")
    cloud, cloudsh = rgb("#cfe4ff"), rgb("#9cbbe8")
    cv.part(ell(36, 70, 12, 9, -30), fur, sh, hi)                                   # tail
    cv.part(ell(62, 82, 30, 22), fur, sh, hi)                                       # body
    for cx, cy, r in ((40, 106, 10), (66, 110, 12), (88, 106, 10)):
        cv.part(ell(cx, cy, r + 2, r * 0.55), cloud, cloudsh, hi)                   # cloud under the cat
    cv.part(tri((70, 38), (78, 12), (92, 36)), fur, sh)                             # far ear
    cv.part(ell(86, 52, 28, 24), fur, sh, hi)                                       # head
    cv.part(tri((78, 40), (88, 16), (100, 38)), fur, sh)                            # near ear
    cv.part(tri((83, 38), (89, 24), (95, 38)), pink, outline=False)
    cv.eye(96, 52, hungry)
    cv.part(ell(110, 58, 2.2, 1.6), pink, outline=False)                            # nose
    cv.part(ell(92, 62, 4.5, 2.6), pink * 0.9, outline=False)                       # blush
    cv.part(ell(84, 96, 9, 6), fur, sh, hi)                                         # front paw
    return cv


def chick(hungry):
    cv = Canvas()
    yel, sh, hi = pale(rgb("#ffdc52"), hungry), pale(rgb("#e8a830"), hungry), rgb("#fff4b0")
    org = rgb("#ff8c28")
    cv.part(ell(50, 108, 9, 3.4), org, outline=False)                               # feet
    cv.part(ell(72, 108, 9, 3.4), org, outline=False)
    cv.part(ell(60, 82, 30, 27), yel, sh, hi)                                       # body
    cv.part(ell(40, 78, 9, 13, 25), yel * 0.92, sh)                                 # wing
    for dx in (-5, 0, 5):                                                           # head tuft
        cv.part(ell(86 + dx, 34 - abs(dx) * 0.3, 2.6, 6, dx * 4), yel, sh)
    cv.part(ell(84, 52, 22, 20), yel, sh, hi)                                       # head
    cv.part(tri((102, 46), (116, 54), (102, 61)), org, rgb("#d86a14"))              # beak
    cv.eye(92, 49, hungry)
    cv.part(ell(94, 60, 4, 2.4), rgb("#ff9a80"), outline=False)
    return cv


def dragon(hungry):
    cv = Canvas()
    jade, sh, hi = pale(rgb("#4cd29a"), hungry), pale(rgb("#2a9a70"), hungry), rgb("#b8ffe0")
    gold, belly, wing = rgb("#ffd24c"), rgb("#f6f2c0"), rgb("#9af2d4")
    cv.part(ell(30, 82, 20, 8, -25), jade, sh)                                      # tail
    cv.part(ell(15, 74, 5, 5), gold, rgb("#d89a20"))                                # tail tip
    cv.part(tri((48, 62), (30, 22), (64, 52)), wing, rgb("#62c8aa"))                # wing
    cv.part(ell(60, 82, 28, 24), jade, sh, hi)                                      # body
    cv.part(ell(66, 88, 15, 17), belly, rgb("#d8d49c"), outline=False)              # belly
    cv.part(ell(86, 52, 24, 21), jade, sh, hi)                                      # head
    cv.part(ell(104, 60, 13, 9), jade, sh)                                          # snout
    cv.part(tri((80, 36), (70, 14), (90, 32)), gold, rgb("#d89a20"))                # horn
    cv.eye(94, 49, hungry)
    cv.part(ell(96, 60, 4.2, 2.4), rgb("#9affd0"), outline=False)
    cv.part(ell(78, 106, 8, 4), jade, sh)                                           # foot
    return cv


def fox(hungry):
    cv = Canvas()
    fur, sh, hi = pale(rgb("#ff8230"), hungry), pale(rgb("#d4561a"), hungry), rgb("#ffc890")
    cream, dark, flame = rgb("#fff4dc"), rgb("#4a2418"), rgb("#ffd54a")
    for k, (cx, cy, rot) in enumerate(((26, 58, -50), (22, 72, -20), (28, 86, 10))):     # three tails with flame tips
        cv.part(ell(cx + 4, cy, 16, 7, rot), fur, sh, hi)
        cv.part(ell(cx - 8, cy - 8 + k * 2, 5, 6, rot), flame, rgb("#ff9a1a"))
    cv.part(ell(62, 84, 29, 21), fur, sh, hi)                                       # body
    cv.part(ell(72, 92, 14, 12), cream, rgb("#e0c8a0"), outline=False)              # chest
    cv.part(tri((70, 40), (76, 10), (90, 36)), fur, sh)                             # far ear
    cv.part(ell(86, 54, 25, 21), fur, sh, hi)                                       # head
    cv.part(tri((82, 40), (92, 8), (102, 38)), fur, sh)                             # near ear
    cv.part(tri((88, 38), (93, 20), (99, 38)), dark, outline=False)
    cv.part(ell(106, 62, 14, 9), cream, rgb("#e0c8a0"))                             # muzzle
    cv.part(ell(118, 59, 3, 2.6), dark, outline=False)                              # nose
    cv.eye(94, 50, hungry)
    cv.part(ell(94, 62, 4.2, 2.4), rgb("#ff9a80"), outline=False)
    cv.part(ell(80, 106, 8, 4), dark, outline=False)                                # paw
    return cv


MAKERS = {"PetCloudCat": cat, "PetChick": chick, "PetJadeDragon": dragon, "PetFireFox": fox}

if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    for name, fn in MAKERS.items():
        fn(False).png(os.path.join(out, name + ".png"))
        fn(True).png(os.path.join(out, name + "_hungry.png"))
        print("wrote", name)
