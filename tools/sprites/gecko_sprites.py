"""Generates the leopard gecko animation frames and effect sprites (pure Python, no deps).

The gecko is drawn procedurally from a pose (spine + legs + face) at 128x128 and upscaled
2x with nearest-neighbour, so every frame shares one pixel grid, one palette, and one
ground line: the outlined toes always end on y = 168 in the 256x256 output (jump frames
excepted), which is TerrariumArtLayout.Pet.BodyBottom. Re-measure it if you change GROUND
or the leg drawing.

usage: python3 tools/sprites/gecko_sprites.py [output_root]   (default: Assets/Resources)
"""
import math
import os
import struct
import sys
import zlib

W = H = 128
SCALE = 2
GROUND = 82  # feet line in the 128 canvas -> 164 in the 256 output

# Walk cycle, modelled on leopard gecko / sprawling-lizard locomotion:
# - diagonal couplets (trot-like): near fore + far hind step together, then near hind + far fore
# - duty factor ~0.7 (Eublepharis): each foot is planted 8 of 12 frames, swings for 4
# - no foot slip: the body advances WALK_STEP px per frame and a planted foot slides back
#   exactly WALK_STEP px per frame relative to the body, so it stays still on the ground.
#   The game advances walk frames by distance moved (TerrariumArtLayout.PetWalkStride*).
# - lateral trunk undulation: shoulder and hip girdles rotate in anti-phase each cycle
# - inverted-pendulum vaulting: tiny body rise twice per cycle, head held level
# - the tail is carried off the ground (not dragged) and swings with a lag
WALK_FRAMES = 12
WALK_STANCE_FRAMES = 8
WALK_STEP = 1.0  # px per frame in the 128 canvas -> stride 12 px (24 px in the 256 output)

OUTLINE = (58, 36, 26)
BASE = (246, 178, 58)
LIGHT = (253, 206, 104)
SHADE = (222, 142, 46)
BELLY = (251, 238, 214)
BELLY_SHADE = (232, 212, 180)
SPOT = (70, 44, 30)
BAND = (84, 52, 34)
EYE = (30, 22, 18)
EYE_HI = (255, 255, 255)
MOUTH = (200, 70, 80)
TONGUE = (240, 110, 120)
LEG_FAR = (206, 132, 44)

# Leopard spots in (s along spine, v across the body: -1 top .. +1 bottom, radius px),
# scattered deterministically over the head, back and flanks.
def _make_spots():
    seed = 12345
    spots = []
    while len(spots) < 30:
        seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF
        a = seed / 0x7FFFFFFF
        seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF
        b = seed / 0x7FFFFFFF
        seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF
        c = seed / 0x7FFFFFFF
        s = 0.03 + a * 0.58
        v = -0.95 + b * 1.2
        if s < 0.12 and v > -0.1:
            continue  # keep the face (eye/mouth area) clean
        spots.append((s, v, 0.8 + c * 0.5))
    return spots


SPOTS = _make_spots()


def lerp(a, b, t):
    return a + (b - a) * t


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def radius_at(s):
    """Body half-thickness profile from snout (s=0) to tail tip (s=1)."""
    keys = [(0.0, 3.2), (0.03, 6.6), (0.09, 9.2), (0.15, 8.0), (0.21, 8.8), (0.32, 10.4),
            (0.44, 10.4), (0.53, 8.8), (0.59, 8.6), (0.69, 8.8), (0.80, 6.6), (0.92, 3.6), (1.0, 2.0)]
    for (s0, r0), (s1, r1) in zip(keys, keys[1:]):
        if s <= s1:
            return lerp(r0, r1, smooth((s - s0) / (s1 - s0)))
    return keys[-1][1]


class Pose:
    def __init__(self, **kw):
        self.lift = 2.5          # belly clearance above the ground
        self.body_dy = 0.0       # whole-body vertical offset (negative = up; used for jumps)
        self.breath = 0.0        # -1..1 radius swell of the torso
        self.head_dx = 0.0       # head lunge (negative = forward/left)
        self.head_dy = 0.0       # head raise (negative = up)
        self.arch = 0.0          # back arch height at mid-body
        self.tail_lift = 0.0     # raise of the tail (negative = up)
        self.tail_wave = 0.0     # amplitude of a vertical tail wave
        self.tail_phase = 0.0
        self.walk_frame = None   # None = standing; 0..WALK_FRAMES-1 for the walk cycle
        self.feet_spread = 0.0   # extra stance width (threat)
        self.mouth = 0.0         # 0 closed .. 1 wide open
        self.eye = "open"        # open | closed | happy | half
        self.tongue = False
        self.__dict__.update(kw)


def spine(p):
    """Centerline points (x, y, r, s) from snout to tail tip."""
    pts = []
    n = 240
    length = 92.0
    x0 = 16.0
    for i in range(n + 1):
        s = i / n
        r = radius_at(s)
        if 0.18 < s < 0.58:
            r *= 1.0 + 0.05 * p.breath
        x = x0 + s * length
        # Belly sits `lift` above the ground; the torso is thickest so it defines the height.
        y = GROUND - p.lift - 10.4 + p.body_dy
        # Head: slightly raised, plus pose offsets fading out along the neck.
        head_w = 1.0 - smooth((s - 0.12) / 0.14)
        y += -1.5 * head_w + p.head_dy * head_w
        x += p.head_dx * head_w
        # Back arch (threat) peaks mid-body.
        y -= p.arch * math.sin(math.pi * smooth((s - 0.12) / 0.5)) if 0.12 < s < 0.62 else 0.0
        # Tail: droops toward the ground, lifts/waves with the pose.
        if s > 0.52:
            t = (s - 0.52) / 0.48
            droop = 7.0 * t * t
            y += droop + p.tail_lift * t * t
            # The fat tail's tip curls down and back.
            curl = smooth((t - 0.62) / 0.38)
            x -= 9.0 * curl * curl
            y += 5.0 * curl
            y += p.tail_wave * t * math.sin(p.tail_phase + t * 4.0)
            # Tail stays above ground.
            y = min(y, GROUND - r - 0.5)
        pts.append((x, y, r, s))
    return pts


def render(p):
    canvas = [[None] * W for _ in range(H)]
    pts = spine(p)

    def put(x, y, c):
        if 0 <= x < W and 0 <= y < H:
            canvas[y][x] = c

    def disc(cx, cy, r, c):
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                    put(x, y, c)

    def line(x0, y0, x1, y1, width, c):
        steps = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
        for i in range(steps + 1):
            t = i / steps
            disc(lerp(x0, x1, t), lerp(y0, y1, t), width / 2, c)

    def body_at(s):
        i = min(len(pts) - 1, max(0, int(round(s * (len(pts) - 1)))))
        return pts[i]

    def leg(s_anchor, foot_dx, lift_foot, far, girdle_dx=0.0):
        x, y, r, _ = body_at(s_anchor)
        col = LEG_FAR if far else BASE
        # The girdle swings with the trunk's lateral bend; the planted foot does not.
        hip = (x + girdle_dx, y + r * 0.45)
        # Feet leave the ground only for a real jump, not for the small walk bob.
        jump = p.body_dy if p.body_dy < -1.0 else 0.0
        foot = (x + foot_dx, GROUND - 1 - lift_foot + jump)
        # Knee bends outward (up) for a lizard sprawl.
        knee = ((hip[0] + foot[0]) / 2 + (2.5 if foot_dx >= 0 else -2.5), max(hip[1] + 1.0, foot[1] - 3.0))
        line(hip[0], hip[1], knee[0], knee[1], 4.6, col)
        line(knee[0], knee[1], foot[0], foot[1], 3.6, col)
        # Splayed toes
        for tdx in (-3, -1, 1, 3):
            put(int(foot[0] + tdx), int(foot[1]), col)
        put(int(foot[0] - 1), int(foot[1] - 1), col)
        put(int(foot[0] + 1), int(foot[1] - 1), col)

    def walk_foot(frame, offset):
        """Foot offset from its rest spot and lift, for a leg `offset` frames into the cycle."""
        j = (frame + offset) % WALK_FRAMES
        half = WALK_STANCE_FRAMES * WALK_STEP / 2.0
        if j < WALK_STANCE_FRAMES:
            # Planted: slides back relative to the body exactly as fast as the body moves on.
            return -half + j * WALK_STEP, 0.0
        u = (j - WALK_STANCE_FRAMES + 1) / (WALK_FRAMES - WALK_STANCE_FRAMES + 1)
        return half - 2.0 * half * smooth(u), 2.6 * math.sin(math.pi * u)

    front, hind = 0.22, 0.50
    half_cycle = WALK_FRAMES // 2
    legs = []
    for s_a in (front, hind):
        for far in (True, False):
            rest = (-1.0 if s_a == front else 1.0) + (1.5 if far else -1.0)
            if p.walk_frame is None:
                dx = (-2.0 if s_a == front else 2.0) * (1.0 + p.feet_spread) + (1.5 if far else -1.0)
                lift_f = 0.0
                girdle = 0.0
            else:
                # Diagonal couplets: near fore + far hind together, the other pair half a cycle later.
                in_first_pair = (s_a == front) != far
                off, lift_f = walk_foot(p.walk_frame, 0 if in_first_pair else half_cycle)
                dx = rest + off
                # Shoulders and hips rotate in anti-phase with the trunk's lateral bend.
                bend = math.cos(2 * math.pi * p.walk_frame / WALK_FRAMES)
                girdle = 1.0 * bend * (1 if s_a == front else -1) * (-1 if far else 1)
            legs.append((s_a, dx, lift_f, far, girdle))

    # Far legs behind the body.
    for s_a, dx, lf, far, girdle in legs:
        if far:
            leg(s_a, dx, lf, True, girdle)

    # Body: nearest-spine coloring.
    best = [[None] * W for _ in range(H)]
    for (x, y, r, s) in pts:
        for py in range(int(y - r - 1), int(y + r + 2)):
            for px in range(int(x - r - 1), int(x + r + 2)):
                if not (0 <= px < W and 0 <= py < H):
                    continue
                d2 = (px + 0.5 - x) ** 2 + (py + 0.5 - y) ** 2
                if d2 <= r * r:
                    v = (py + 0.5 - y) / r
                    cur = best[py][px]
                    if cur is None or d2 / (r * r) < cur[0]:
                        best[py][px] = (d2 / (r * r), s, v, r)
    for py in range(H):
        for px in range(W):
            b = best[py][px]
            if b is None:
                continue
            _, s, v, rad = b
            if v > 0.42:
                c = BELLY if v < 0.8 else BELLY_SHADE
            elif v < -0.55:
                c = LIGHT
            elif v > 0.2:
                c = SHADE
            else:
                c = BASE
            # Tail bands.
            if s > 0.6 and v < 0.55:
                band = ((s - 0.6) * 11.0) % 1.0
                if band < 0.32:
                    c = BAND
            if s > 0.97:
                c = BELLY
            # Spots.
            for (ss, vv, rr) in SPOTS:
                if ((s - ss) * 92.0) ** 2 + ((v - vv) * rad) ** 2 <= rr * rr:
                    c = SPOT
                    break
            canvas[py][px] = c

    # Near legs in front of the body.
    for s_a, dx, lf, far, girdle in legs:
        if not far:
            leg(s_a, dx, lf, False, girdle)

    # Face.
    hx, hy, hr, _ = body_at(0.085)
    ex, ey = int(round(hx)), int(round(hy - hr * 0.22))
    if p.eye == "open":
        for dy in range(-2, 2):
            for dx in range(-2, 2):
                if (dx, dy) not in ((-2, -2), (1, -2), (-2, 1), (1, 1)):
                    put(ex + dx, ey + dy, EYE)
        put(ex - 1, ey - 1, EYE_HI)
        put(ex - 1, ey - 2, EYE_HI)
    elif p.eye == "half":
        for dx in range(-1, 2):
            put(ex + dx, ey, EYE)
            put(ex + dx, ey + 1, EYE)
        put(ex - 1, ey - 1, OUTLINE)
        put(ex, ey - 1, OUTLINE)
        put(ex + 1, ey - 1, OUTLINE)
    elif p.eye == "happy":
        put(ex - 1, ey + 1, EYE)
        put(ex, ey, EYE)
        put(ex + 1, ey + 1, EYE)
        put(ex - 2, ey + 1, EYE)
        put(ex + 2, ey + 1, EYE)
    else:  # closed: a gentle downward arc
        for dx in range(-2, 3):
            put(ex + dx, ey + (1 if abs(dx) < 2 else 0), EYE)

    # Mouth: a line from the snout back to the jaw corner, or an open wedge.
    sx, sy, sr, _ = body_at(0.0)
    cx, cy, cr, _ = body_at(0.12)
    mouth_y0 = sy + sr * 0.35
    corner = (cx - 1, cy + cr * 0.28)
    if p.mouth <= 0.05:
        line(sx + 1, mouth_y0, corner[0], corner[1], 1.0, OUTLINE)
    else:
        gap = 1.5 + 6.0 * p.mouth
        tip_upper = (sx - 0.5, mouth_y0 - gap * 0.35)
        tip_lower = (sx + 1.0, mouth_y0 + gap * 0.65)
        # Lower jaw drops: redraw it below the wedge.
        line(tip_lower[0] + 1, tip_lower[1] + 1, corner[0], corner[1] + 1.5, 2.5, BELLY)
        tri = (tip_upper, tip_lower, corner)
        minx, maxx = int(min(t[0] for t in tri)) - 1, int(max(t[0] for t in tri)) + 1
        miny, maxy = int(min(t[1] for t in tri)) - 1, int(max(t[1] for t in tri)) + 1

        def inside(px, py):
            (x1, y1), (x2, y2), (x3, y3) = tri
            d1 = (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2)
            d2 = (px - x3) * (y2 - y3) - (x2 - x3) * (py - y3)
            d3 = (px - x1) * (y3 - y1) - (x3 - x1) * (py - y1)
            neg = d1 < 0 or d2 < 0 or d3 < 0
            pos = d1 > 0 or d2 > 0 or d3 > 0
            return not (neg and pos)

        for py in range(miny, maxy + 1):
            for px in range(minx, maxx + 1):
                if inside(px + 0.5, py + 0.5):
                    put(px, py, MOUTH)
        # Clear anything above the upper lip that belonged to the head silhouette's snout tip
        # is unnecessary; the outline pass will frame the wedge.
        if p.tongue:
            put(int(tip_lower[0] + 2), int(tip_lower[1] - 1), TONGUE)
            put(int(tip_lower[0] + 3), int(tip_lower[1] - 1), TONGUE)
            put(int(tip_lower[0] + 2), int(tip_lower[1] - 2), TONGUE)

    # Outline: any empty pixel touching the silhouette.
    out = [row[:] for row in canvas]
    for py in range(H):
        for px in range(W):
            if canvas[py][px] is not None:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = px + dx, py + dy
                if 0 <= nx < W and 0 <= ny < H and canvas[ny][nx] is not None:
                    out[py][px] = OUTLINE
                    break
    # Inner edge of the open mouth also gets an outline.
    for py in range(H):
        for px in range(W):
            if out[py][px] == MOUTH:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = px + dx, py + dy
                    if 0 <= nx < W and 0 <= ny < H and out[ny][nx] not in (MOUTH, TONGUE, None, OUTLINE):
                        if canvas[ny][nx] in (BASE, SHADE, LIGHT, SPOT) and dy != 0:
                            out[ny][nx] = OUTLINE
    return out


def upscale_rgba(canvas, scale):
    w, h = len(canvas[0]), len(canvas)
    rows = []
    for y in range(h):
        row = bytearray()
        for x in range(w):
            c = canvas[y][x]
            px = bytes((c[0], c[1], c[2], 255)) if c is not None else b"\x00\x00\x00\x00"
            row += px * scale
        for _ in range(scale):
            rows.append(bytes(row))
    return w * scale, h * scale, rows


def write_png(path, w, h, rows):
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)

    raw = b"".join(b"\x00" + r for r in rows)
    with open(path, "wb") as fh:
        fh.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
                 + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def clip_frames():
    tau = 2 * math.pi
    clips = {}

    # Idle: slow breathing, a blink on frame 6, gentle tail curl.
    clips["idle"] = [Pose(breath=math.sin(i / 8 * tau), tail_wave=0.6, tail_phase=i / 8 * tau,
                          eye="closed" if i == 6 else "open") for i in range(8)]

    # Walk: diagonal leg cycle, slight body bob and tail sway.
    walk = []
    for i in range(WALK_FRAMES):
        t = i / WALK_FRAMES
        vault = -0.5 * (1 - math.cos(2 * tau * t)) / 2  # rises twice per cycle
        walk.append(Pose(walk_frame=i, body_dy=vault, head_dy=-vault,
                         tail_lift=-2.0, tail_wave=1.0, tail_phase=t * tau - math.pi / 2))
    clips["walk"] = walk

    # Eat: lunge, snap, then chew.
    eat = []
    for i, (hdx, hdy, m) in enumerate([(0, 0, 0), (-3, 1, 0.3), (-6, 2, 0.9), (-7, 2.5, 0.2),
                                       (-4, 1, 0.0), (-2, 0, 0.35), (-2, 0, 0.0), (-2, 0, 0.35)]):
        eat.append(Pose(head_dx=hdx, head_dy=hdy, mouth=m, tongue=(i == 7), eye="open" if i != 3 else "half"))
    clips["eat"] = eat

    # Sleep: belly down, head lowered, eyes shut, very slow breathing.
    clips["sleep"] = [Pose(lift=0.5, head_dy=2.5, eye="closed", breath=0.6 * math.sin(i / 4 * tau), tail_lift=1.0)
                      for i in range(4)]

    # Yawn: head tips up, mouth opens wide, eyes squint, then settles.
    yawn = []
    for i, k in enumerate([0.0, 0.3, 0.7, 1.0, 1.0, 0.8, 0.4, 0.0]):
        yawn.append(Pose(head_dy=-3.5 * k, mouth=k, eye="closed" if k > 0.6 else ("half" if k > 0 else "open"),
                         tongue=k > 0.9))
    clips["yawn"] = yawn

    # Threat: body high on straight legs, arched back, raised waving tail, mouth open.
    clips["threat"] = [Pose(lift=8.5, arch=2.5, tail_lift=-9.0, tail_wave=2.5, tail_phase=i / 6 * tau,
                            mouth=0.55 + 0.25 * math.sin(i / 6 * tau), feet_spread=0.6, head_dy=-1.0)
                       for i in range(6)]

    # Happy: little hops with happy eyes and a wagging tail.
    happy = []
    for i in range(8):
        t = i / 8
        jump = -4.0 * max(0.0, math.sin(t * tau))
        happy.append(Pose(body_dy=jump, eye="happy", tail_wave=2.2, tail_phase=t * tau * 2,
                          head_dy=-1.0, breath=0.5))
    clips["happy"] = happy

    # Tail wag: the stalking/excited tail sway (also played just before striking at food).
    clips["tailwag"] = [Pose(head_dy=0.8, tail_lift=-6.0, tail_wave=6.0, tail_phase=i / 8 * tau,
                             eye="open") for i in range(8)]
    return clips


# Before a shed the old skin turns milky and pale. Same poses, washed-out palette.
# This used to be pre-rendered here into a GeckoPreShed/ set; the pre-shed colours
# are now derived at runtime from the normal frames by MorphAppearance.PreShed
# (see Assets/Scripts/UI/MorphRecolor.cs).


HEART = [
    "..XX...XX..",
    ".XHHX.XRRX.",
    "XHWHRXRRRRX",
    "XHHRRRRRRRX",
    "XRRRRRRRRRX",
    ".XRRRRRRRX.",
    "..XRRRRRX..",
    "...XRRRX...",
    "....XRX....",
    ".....X.....",
]

ZED = [
    "XXXXXX",
    "XWWWWX",
    "XXXWWX",
    "..XWX.",
    ".XWXXX",
    "XWWWWX",
    "XXXXXX",
]


def bitmap(rows, palette, scale):
    canvas = [[palette.get(ch) for ch in row] for row in rows]
    return upscale_rgba(canvas, scale)


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else "Assets/Resources"
    gecko_dir = os.path.join(root, "Gecko")
    fx_dir = os.path.join(root, "Effects")
    for d in (gecko_dir, fx_dir):
        os.makedirs(d, exist_ok=True)

    for name, poses in clip_frames().items():
        for i, pose in enumerate(poses):
            canvas = render(pose)
            w, h, rows = upscale_rgba(canvas, SCALE)
            write_png(os.path.join(gecko_dir, f"{name}_{i:02d}.png"), w, h, rows)
        print(f"{name}: {len(poses)} frames")

    heart_palette = {"X": (120, 20, 40), "R": (236, 64, 96), "H": (255, 150, 170), "W": (255, 255, 255)}
    w, h, rows = bitmap(HEART, heart_palette, 4)
    write_png(os.path.join(fx_dir, "heart.png"), w, h, rows)
    zed_palette = {"X": (70, 80, 130), "W": (225, 235, 255)}
    w, h, rows = bitmap(ZED, zed_palette, 4)
    write_png(os.path.join(fx_dir, "zzz.png"), w, h, rows)
    print("effects: heart, zzz")


if __name__ == "__main__":
    main()
