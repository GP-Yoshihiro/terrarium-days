"""Generates the seven candling sprites (an egg lit from behind in a dark incubator).

64x64 transparent PNGs, pure Python (no deps), written to <output_root>/Candling/.
Brightness and shadow size change monotonically unknown -> hatching; dead is grey-brown.

usage: python3 tools/sprites/candling_sprites.py [output_root]   (default: Assets/Resources)
"""
import math
import os
import random
import struct
import sys
import zlib

SIZE = 64
CX, CY = 31.5, 32.5
RX, RY = 19.0, 25.0  # tall egg ellipse


def write_png(path, w, h, rows):
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)

    raw = b"".join(b"\x00" + r for r in rows)
    with open(path, "wb") as fh:
        fh.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
                 + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def lerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def smooth(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


VEIN = (176, 30, 24)
SHADOW = (58, 20, 14)

# name: (glow centre, glow edge, shadow radius factor k, shadow dark depth)
STAGES = {
    "unknown": ((255, 238, 150), (240, 150, 60), 0.0),
    "infertile": ((255, 224, 96), (232, 168, 48), 0.0),
    "early": ((252, 190, 84), (214, 104, 40), 0.0),
    "middle": ((240, 160, 70), (190, 84, 34), 0.58),
    "late": ((214, 120, 54), (150, 62, 28), 0.82),
    "hatching": ((190, 100, 44), (110, 46, 24), 0.97),
    "dead": ((150, 130, 104), (96, 82, 66), 0.0),
}


def veins(seed, origin, count, length, rng_len=0.4, start=0.0):
    """Wiggly vein polylines radiating from origin; returns pixel set with a radial start offset."""
    rnd = random.Random(seed)
    pts = []
    for n in range(count):
        ang = (n / count) * 2 * math.pi + rnd.uniform(-0.3, 0.3)
        ln = length * (1 - rng_len + rng_len * rnd.random())
        x, y = origin
        x += math.cos(ang) * start
        y += math.sin(ang) * start
        a = ang
        steps = int(ln * 2)
        for _ in range(steps):
            a += rnd.uniform(-0.18, 0.18)
            x += math.cos(a) * 0.5
            y += math.sin(a) * 0.5
            pts.append((int(round(x)), int(round(y))))
    return pts


def render(name):
    centre, edge, k = STAGES[name]
    px = [[None] * SIZE for _ in range(SIZE)]
    shadow_cy = CY + RY * (1 - k) * 0.75 + (RY * 0.1 if k else 0)

    def egg_d(x, y):
        return math.hypot((x - CX) / RX, (y - CY) / RY)

    for y in range(SIZE):
        for x in range(SIZE):
            d = egg_d(x, y)
            if d > 1.0:
                continue
            # light gradient, brightest slightly above centre
            g = egg_d(x, y + 4) ** 1.4
            col = lerp(centre, edge, min(1.0, g))
            if name == "dead":
                # mottled dull patches
                col = lerp(col, (120, 104, 78), 0.35 * (0.5 + 0.5 * math.sin(x * 0.9) * math.cos(y * 0.7)))
            if k:
                sd = math.hypot((x - CX) / (RX * k), (y - shadow_cy) / (RY * k))
                col = lerp(col, SHADOW, smooth(1.0, 0.75, sd) * 0.93)
            if d > 0.9:  # dark rim
                col = lerp(col, (40, 18, 10), smooth(0.9, 1.0, d) * 0.7)
            px[y][x] = col

    def plot(pts, colour, keep=lambda d: True):
        for (x, y) in pts:
            if 0 <= x < SIZE and 0 <= y < SIZE and px[y][x] is not None and keep(egg_d(x, y)):
                px[y][x] = colour

    if name == "early":
        plot(veins(3, (CX, CY + 2), 6, 9, 0.5), VEIN, keep=lambda d: d < 0.8)
        plot([(int(CX) + dx, int(CY) + 2 + dy) for dx in (0, 1) for dy in (0, 1)], (150, 14, 14))
    elif name == "middle":
        plot(veins(5, (CX, shadow_cy - 4), 12, 15, 0.4, start=8), VEIN, keep=lambda d: d < 0.9)
    elif name == "late":
        # veins hug the rim: short strokes along the edge, tangent-ish
        rim = []
        for n in range(26):
            ang = n * 2 * math.pi / 26 + 0.1
            for r in (0.84, 0.88):
                rim.append((int(round(CX + math.cos(ang) * RX * r)), int(round(CY + math.sin(ang) * RY * r))))
        plot(rim, VEIN)
        plot(veins(8, (CX, CY - 6), 7, 7, 0.4, start=10), VEIN, keep=lambda d: d > 0.55)
    elif name == "hatching":
        # air cell: bright band at the blunt (top) end
        for y in range(SIZE):
            for x in range(SIZE):
                if px[y][x] is not None and y < CY - RY * 0.72:
                    t = smooth(0.0, 1.0, (CY - RY * 0.72 - y) / 6.0)
                    px[y][x] = lerp(px[y][x], (255, 232, 170), 0.2 + 0.8 * t)
    elif name == "dead":
        # blood ring + greenish spots
        ring = []
        for n in range(120):
            ang = n * 2 * math.pi / 120
            ring.append((int(round(CX + math.cos(ang) * RX * 0.66)), int(round(CY + 4 + math.sin(ang) * RY * 0.52))))
        plot(ring, (120, 36, 26))
        rnd = random.Random(11)
        for _ in range(9):
            sx, sy = CX + rnd.uniform(-11, 11), CY + rnd.uniform(-16, 18)
            r = rnd.uniform(1.5, 2.8)
            for yy in range(int(sy - 3), int(sy + 4)):
                for xx in range(int(sx - 3), int(sx + 4)):
                    if 0 <= xx < SIZE and 0 <= yy < SIZE and px[yy][xx] is not None \
                            and math.hypot(xx - sx, yy - sy) < r:
                        px[yy][xx] = (88, 106, 52)

    rows = []
    for y in range(SIZE):
        row = bytearray()
        for x in range(SIZE):
            c = px[y][x]
            row += bytes((0, 0, 0, 0)) if c is None else bytes((int(c[0]), int(c[1]), int(c[2]), 255))
        rows.append(bytes(row))
    return rows


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else "Assets/Resources"
    out = os.path.join(root, "Candling")
    os.makedirs(out, exist_ok=True)
    for name in STAGES:
        write_png(os.path.join(out, f"candling_{name}.png"), SIZE, SIZE, render(name))
    print("candling:", ", ".join(STAGES))


if __name__ == "__main__":
    main()
