"""Generates the event booth and visitor sprites (pure Python, no deps).

Drawn on a small logical canvas and upscaled 2x with nearest neighbour, like the gecko sprites.
Same dark-brown outline (58,36,26), warm palette, light from the upper left (shade lower right).
Files written to <output_root>/Event/: booth_bg (old single-booth backdrop, no longer used by the day scene),
aisle_bg (the aisle seen from inside our booth), counter_fg (back of our counter, foreground strip),
booth_case, sold_tag, visitor_{a,b,c}_{0,1} (walk frames, facing right), visitor_{a,b,c}_front (standing, faces us),
bubble (9-slice, border 8 px at output scale), bubble_clerk (tail-less 9-slice, border 8 px) + bubble_tail_down (small down-pointing tail, drawn under it) for our clerk's lines, other_booth.

usage: python3 tools/sprites/event_sprites.py [output_root]   (default: Assets/Resources)
"""
import os
import struct
import sys
import zlib

SCALE = 2
OUTLINE = (58, 36, 26)
WOOD = (196, 140, 84)
WOOD_LIGHT = (222, 170, 108)
WOOD_SHADE = (150, 98, 58)
CLOTH = (244, 232, 206)
CLOTH_SHADE = (222, 206, 170)
WALL = (252, 240, 214)
WALL_SHADE = (240, 222, 188)
FLOOR = (214, 180, 132)
FLOOR_SHADE = (190, 154, 108)
GLASS = (190, 226, 232)
GLASS_LIGHT = (232, 248, 250)
GLASS_SHADE = (150, 196, 206)
RED = (214, 72, 62)
RED_LIGHT = (240, 112, 94)
RED_SHADE = (166, 48, 46)
GREEN = (96, 164, 96)
GREEN_SHADE = (66, 126, 78)
BLUE = (88, 134, 190)
BLUE_SHADE = (60, 98, 150)
YELLOW = (246, 178, 58)
YELLOW_SHADE = (222, 142, 46)
WHITE = (255, 255, 255)
SKIN = (246, 208, 170)
SKIN_SHADE = (222, 176, 138)
EYE = (30, 22, 18)


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = [[None] * w for _ in range(h)]

    def put(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[y][x] = c

    def rect(self, x0, y0, x1, y1, c):  # inclusive
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, c)

    def box(self, x0, y0, x1, y1, base, light, shade):
        """Filled rect with a light top/left edge and shaded bottom/right edge."""
        self.rect(x0, y0, x1, y1, base)
        self.rect(x0, y0, x1, y0, light)
        self.rect(x0, y0, x0, y1, light)
        self.rect(x0, y1, x1, y1, shade)
        self.rect(x1, y1 - (y1 - y0), x1, y1, shade)

    def outline(self):
        """One-pixel outline around every opaque region (outside only)."""
        edge = []
        for y in range(self.h):
            for x in range(self.w):
                if self.px[y][x] is None:
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < self.w and 0 <= ny < self.h and self.px[ny][nx] not in (None, OUTLINE):
                            edge.append((x, y))
                            break
        for x, y in edge:
            self.px[y][x] = OUTLINE

    def rows(self):
        out = []
        for y in range(self.h):
            row = bytearray()
            for x in range(self.w):
                c = self.px[y][x]
                px = bytes((c[0], c[1], c[2], 255)) if c else b"\0\0\0\0"
                row += px * SCALE
            out.extend([bytes(row)] * SCALE)
        return out


def write_png(path, w, h, rows):
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)

    raw = b"".join(b"\x00" + r for r in rows)
    with open(path, "wb") as fh:
        fh.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
                 + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def save(root, name, cv):
    write_png(os.path.join(root, name + ".png"), cv.w * SCALE, cv.h * SCALE, cv.rows())


# ---- 3x5 pixel font for the tag
FONT = {
    "S": ["111", "100", "111", "001", "111"],
    "O": ["111", "101", "101", "101", "111"],
    "L": ["100", "100", "100", "100", "111"],
    "D": ["110", "101", "101", "101", "110"],
}


def text(cv, x, y, s, c):
    for ch in s:
        for r, line in enumerate(FONT[ch]):
            for k, b in enumerate(line):
                if b == "1":
                    cv.put(x + k, y + r, c)
        x += 4


# ---- pieces
def banner(cv, x, top, w, h, cloth, shade):
    """A hanging nobori flag: cloth strip with a pole on top and a stripe."""
    cv.rect(x - 1, top, x + w, top, WOOD_SHADE)
    cv.rect(x, top + 1, x + w - 1, top + h, cloth)
    cv.rect(x + w - 2, top + 1, x + w - 1, top + h, shade)
    cv.rect(x + 2, top + 4, x + w - 3, top + 5, CLOTH)
    cv.rect(x + 2, top + 9, x + w - 3, top + 10, CLOTH)


def table(cv, x0, x1, top, bottom):
    cv.rect(x0, top, x1, top + 3, WOOD_LIGHT)
    cv.rect(x0, top + 3, x1, top + 3, WOOD)
    cv.rect(x0, top + 4, x1, top + 12, CLOTH)
    cv.rect(x0, top + 12, x1, top + 12, CLOTH_SHADE)
    for x in range(x0 + 4, x1, 8):  # cloth folds
        cv.rect(x, top + 5, x, top + 11, CLOTH_SHADE)
    cv.rect(x0 + 1, top + 13, x0 + 3, bottom, WOOD)
    cv.rect(x1 - 3, top + 13, x1 - 1, bottom, WOOD_SHADE)


def booth_bg():
    cv = Canvas(160, 64)
    cv.rect(0, 0, 159, 63, WALL)
    cv.rect(0, 0, 159, 3, WALL_SHADE)
    cv.rect(0, 52, 159, 63, FLOOR)
    cv.rect(0, 52, 159, 53, FLOOR_SHADE)
    # poles and awning
    cv.rect(6, 8, 8, 55, WOOD)
    cv.rect(8, 8, 8, 55, WOOD_SHADE)
    cv.rect(151, 8, 153, 55, WOOD)
    cv.rect(153, 8, 153, 55, WOOD_SHADE)
    for i, x in enumerate(range(4, 156, 8)):
        c = RED if i % 2 == 0 else CLOTH
        cv.rect(x, 4, x + 7, 10, c)
        cv.rect(x, 10, x + 7, 10, RED_SHADE if i % 2 == 0 else CLOTH_SHADE)
        cv.rect(x + 2, 11, x + 5, 12, c)
    cv.rect(4, 4, 155, 4, RED_LIGHT)
    # nobori flags at both sides
    banner(cv, 12, 14, 9, 20, BLUE, BLUE_SHADE)
    banner(cv, 139, 14, 9, 20, GREEN, GREEN_SHADE)
    # sign board
    cv.box(58, 16, 101, 29, CLOTH, WHITE, CLOTH_SHADE)
    cv.rect(62, 20, 97, 21, YELLOW)
    cv.rect(62, 24, 97, 25, YELLOW_SHADE)
    # long table with cloth
    table(cv, 20, 139, 40, 56)
    cv.outline()
    return cv


def booth_case():
    cv = Canvas(40, 32)
    cv.box(2, 4, 37, 24, GLASS, GLASS_LIGHT, GLASS_SHADE)
    cv.rect(6, 6, 8, 14, GLASS_LIGHT)  # glass glint
    cv.rect(3, 20, 36, 24, FLOOR)  # substrate
    cv.rect(3, 20, 36, 20, FLOOR_SHADE)
    cv.rect(2, 2, 37, 3, WOOD_LIGHT)  # lid
    cv.rect(2, 3, 37, 3, WOOD)
    cv.rect(2, 25, 37, 29, WOOD)  # base
    cv.rect(2, 25, 37, 25, WOOD_LIGHT)
    cv.rect(2, 29, 37, 29, WOOD_SHADE)
    cv.rect(14, 16, 19, 16, GREEN)  # small plant
    cv.rect(15, 14, 15, 16, GREEN_SHADE)
    cv.rect(17, 13, 17, 16, GREEN_SHADE)
    cv.rect(33, 21, 36, 24, WOOD_SHADE)  # hide
    cv.outline()
    return cv


def sold_tag():
    cv = Canvas(24, 14)
    cv.box(1, 3, 22, 12, RED, RED_LIGHT, RED_SHADE)
    cv.rect(10, 0, 13, 0, WOOD_SHADE)  # string
    cv.rect(11, 1, 12, 2, WOOD_SHADE)
    cv.put(3, 5, WHITE)
    text(cv, 4, 5, "SOLD", WHITE)
    cv.outline()
    return cv


# Visitors: 24x40 logical, feet on y=37 (outline row 38). Facing right.
LOOKS = {
    "a": dict(hair=(90, 56, 38), top=BLUE, top_shade=BLUE_SHADE, legs=(88, 78, 110), hat=None),
    "b": dict(hair=(214, 150, 54), top=RED, top_shade=RED_SHADE, legs=(70, 70, 84), hat=GREEN),
    "c": dict(hair=(60, 60, 70), top=GREEN, top_shade=GREEN_SHADE, legs=(130, 100, 70), hat=None),
}


def visitor(kind, frame):
    L = LOOKS[kind]
    cv = Canvas(24, 40)
    bob = 1 if frame == 1 else 0
    cx = 11
    # legs: frame 0 right foot forward, frame 1 left foot forward
    far = (cx - 2 + (-1 if frame == 0 else 1), cx - 1 + (-1 if frame == 0 else 1))
    near = (cx + (1 if frame == 0 else -1), cx + 1 + (1 if frame == 0 else -1))
    shade_leg = tuple(max(0, v - 28) for v in L["legs"])
    cv.rect(far[0], 28 - bob, far[1], 36, shade_leg)
    cv.rect(far[0], 36, far[1] + 1, 37, OUTLINE)
    cv.rect(near[0], 28, near[1], 36, L["legs"])
    cv.rect(near[0], 36, near[1] + 1, 37, EYE)
    # torso
    ty = 17 - bob
    cv.rect(cx - 4, ty, cx + 3, 28 - bob, L["top"])
    cv.rect(cx - 4, ty, cx - 4, 28 - bob, L["top"])
    cv.rect(cx + 2, ty, cx + 3, 28 - bob, L["top_shade"])
    # arms swing opposite to legs
    ax = cx + (-1 if frame == 0 else 1)
    cv.rect(ax - 1, ty + 1, ax, ty + 8, SKIN_SHADE)
    cv.rect(ax - 1, ty + 8, ax, ty + 9, SKIN)
    # head
    hy = 6 - bob
    cv.rect(cx - 4, hy, cx + 3, hy + 9, SKIN)
    cv.rect(cx + 3, hy + 1, cx + 3, hy + 9, SKIN_SHADE)
    cv.rect(cx - 4, hy - 1, cx + 3, hy + 2, L["hair"])
    cv.rect(cx - 5, hy, cx - 4, hy + 5, L["hair"])
    if L["hat"]:
        cv.rect(cx - 4, hy - 3, cx + 3, hy - 1, L["hat"])
        cv.rect(cx + 3, hy - 1, cx + 5, hy - 1, L["hat"])  # brim toward the front
    cv.rect(cx + 1, hy + 4, cx + 1, hy + 5, EYE)  # eye (facing right)
    cv.rect(cx + 2, hy + 7, cx + 3, hy + 7, SKIN_SHADE)
    cv.outline()
    return cv


def far_booth(cv, x0, awning, awning_shade):
    """A booth across the aisle, seen from the front and drawn small (46 px wide)."""
    x1 = x0 + 45
    cv.rect(x0 + 1, 18, x0 + 2, 50, WOOD_SHADE)
    cv.rect(x1 - 2, 18, x1 - 1, 50, WOOD_SHADE)
    cv.rect(x0 + 3, 20, x1 - 3, 40, WALL)  # booth back wall
    for i, x in enumerate(range(x0, x1 - 6, 6)):
        c = awning if i % 2 == 0 else CLOTH
        cv.rect(x, 14, x + 5, 19, c)
        cv.rect(x, 19, x + 5, 19, awning_shade if i % 2 == 0 else CLOTH_SHADE)
    cv.box(x0 + 14, 23, x0 + 31, 29, CLOTH, WHITE, CLOTH_SHADE)
    cv.rect(x0 + 17, 25, x0 + 28, 26, YELLOW)
    cv.rect(x0 + 3, 37, x1 - 3, 40, WOOD_LIGHT)  # table
    cv.rect(x0 + 3, 41, x1 - 3, 50, CLOTH)
    cv.rect(x0 + 3, 50, x1 - 3, 50, CLOTH_SHADE)
    for x in range(x0 + 7, x1 - 3, 8):
        cv.rect(x, 42, x, 49, CLOTH_SHADE)
    for x in (x0 + 8, x0 + 28):  # two small cases on the table
        cv.box(x, 32, x + 9, 37, GLASS, GLASS_LIGHT, GLASS_SHADE)
    for y in (14, 19):
        cv.rect(x0, y, x0, y, OUTLINE)


def aisle_bg():
    """The aisle as seen from behind our own counter: the valance of our booth on top, booths across the aisle, floor."""
    cv = Canvas(160, 108)
    cv.rect(0, 0, 159, 107, WALL_SHADE)  # hall wall
    cv.rect(0, 0, 159, 57, WALL_SHADE)
    cv.rect(0, 10, 159, 13, WALL)
    # floor: bands get lighter and wider towards the viewer
    cv.rect(0, 58, 159, 107, FLOOR)
    cv.rect(0, 58, 159, 59, FLOOR_SHADE)
    for y in (66, 76, 88, 102):
        cv.rect(0, y, 159, y, FLOOR_SHADE)
    for y0, y1, step in ((60, 66, 12), (67, 76, 16), (77, 88, 22), (89, 102, 30)):
        off = 0
        for y in range(y0, y1 + 1):
            for x in range(off, 160, step):
                cv.put(x, y, FLOOR_SHADE)
        # tile joints stay vertical, spacing grows per band
    # three booths across the aisle
    far_booth(cv, 6, GREEN, GREEN_SHADE)
    far_booth(cv, 57, BLUE, BLUE_SHADE)
    far_booth(cv, 108, YELLOW, YELLOW_SHADE)
    cv.rect(0, 51, 159, 57, WALL_SHADE)
    for x in (6, 57, 108):
        cv.rect(x + 3, 50, x + 42, 51, FLOOR_SHADE)
    # our booth: valance and poles in front
    for i, x in enumerate(range(0, 160, 8)):
        c = RED if i % 2 == 0 else CLOTH
        cv.rect(x, 0, x + 7, 7, c)
        cv.rect(x, 7, x + 7, 7, RED_SHADE if i % 2 == 0 else CLOTH_SHADE)
        cv.rect(x + 2, 8, x + 5, 9, c)
    cv.rect(0, 0, 159, 0, RED_LIGHT)
    cv.rect(0, 10, 159, 10, OUTLINE)
    cv.rect(2, 10, 5, 107, WOOD)
    cv.rect(5, 10, 5, 107, WOOD_SHADE)
    cv.rect(154, 10, 157, 107, WOOD)
    cv.rect(157, 10, 157, 107, WOOD_SHADE)
    cv.rect(1, 10, 1, 107, OUTLINE)
    cv.rect(6, 10, 6, 107, OUTLINE)
    cv.rect(153, 10, 153, 107, OUTLINE)
    cv.rect(158, 10, 158, 107, OUTLINE)
    # nobori flags hanging from the valance, seen from behind
    banner(cv, 12, 12, 9, 20, BLUE, BLUE_SHADE)
    banner(cv, 139, 12, 9, 20, GREEN, GREEN_SHADE)
    return cv


def counter_fg():
    """Back of our display counter: board on top, plank panel below. Stretched over the bottom of the stage."""
    cv = Canvas(160, 24)
    cv.rect(0, 0, 159, 0, OUTLINE)
    cv.rect(0, 1, 159, 2, WOOD_LIGHT)
    cv.rect(0, 3, 159, 5, WOOD)
    cv.rect(0, 6, 159, 6, OUTLINE)
    cv.rect(0, 7, 159, 23, WOOD_SHADE)
    for x in range(0, 160, 20):  # planks
        cv.rect(x + 1, 8, x + 18, 23, WOOD)
        cv.rect(x + 1, 8, x + 1, 23, WOOD_LIGHT)
        cv.rect(x + 18, 8, x + 18, 23, WOOD_SHADE)
        cv.rect(x + 19, 7, x + 19, 23, OUTLINE)
    cv.rect(0, 23, 159, 23, OUTLINE)
    return cv


def visitor_front(kind):
    """Standing, facing us (24x40, feet on y=37); same look as the walking frames."""
    L = LOOKS[kind]
    cv = Canvas(24, 40)
    cx = 11
    shade_leg = tuple(max(0, v - 28) for v in L["legs"])
    cv.rect(cx - 3, 28, cx - 1, 36, L["legs"])
    cv.rect(cx + 1, 28, cx + 3, 36, shade_leg)
    cv.rect(cx - 3, 36, cx - 1, 37, EYE)
    cv.rect(cx + 1, 36, cx + 3, 37, EYE)
    cv.rect(cx - 5, 17, cx + 4, 28, L["top"])
    cv.rect(cx + 3, 17, cx + 4, 28, L["top_shade"])
    cv.rect(cx - 7, 18, cx - 6, 27, SKIN_SHADE)  # arms hang at the sides
    cv.rect(cx + 5, 18, cx + 6, 27, SKIN_SHADE)
    cv.rect(cx - 7, 26, cx - 6, 27, SKIN)
    cv.rect(cx + 5, 26, cx + 6, 27, SKIN)
    cv.rect(cx - 5, 6, cx + 4, 15, SKIN)
    cv.rect(cx + 4, 7, cx + 4, 15, SKIN_SHADE)
    cv.rect(cx - 5, 5, cx + 4, 8, L["hair"])
    cv.rect(cx - 6, 6, cx - 5, 11, L["hair"])
    cv.rect(cx + 5, 6, cx + 5, 11, L["hair"])
    if L["hat"]:
        cv.rect(cx - 5, 2, cx + 4, 4, L["hat"])
        cv.rect(cx - 6, 5, cx + 5, 5, L["hat"])
    cv.rect(cx - 3, 10, cx - 3, 11, EYE)
    cv.rect(cx + 2, 10, cx + 2, 11, EYE)
    cv.rect(cx - 1, 13, cx, 13, SKIN_SHADE)
    cv.outline()
    return cv


def bubble():
    cv = Canvas(24, 20)
    cv.rect(0, 0, 23, 15, WHITE)
    for c in ((0, 0), (23, 0), (0, 15), (23, 15)):  # round the corners
        cv.px[c[1]][c[0]] = None
    cv.rect(1, 15, 22, 15, CLOTH_SHADE)
    cv.rect(22, 1, 22, 14, CLOTH_SHADE)
    # tail (below the stretchable middle: lives in the bottom border band)
    cv.rect(5, 16, 9, 16, WHITE)
    cv.rect(5, 17, 7, 17, WHITE)
    cv.rect(5, 18, 5, 18, WHITE)
    cv.px[16][9] = CLOTH_SHADE
    cv.px[17][7] = CLOTH_SHADE
    cv.outline()
    return cv


def bubble_clerk():
    cv = Canvas(26, 22)
    cv.rect(1, 1, 24, 20, WHITE)
    for c in ((1, 1), (24, 1), (1, 20), (24, 20)):  # round the corners
        cv.px[c[1]][c[0]] = None
    cv.rect(2, 20, 23, 20, CLOTH_SHADE)
    cv.rect(24, 2, 24, 19, CLOTH_SHADE)
    cv.outline()
    return cv


def bubble_tail_down():
    """The clerk bubble's tail: sits under the bubble's bottom edge and points down at the viewer."""
    cv = Canvas(10, 6)
    cv.rect(1, 0, 8, 0, WHITE)
    cv.rect(2, 1, 7, 1, WHITE)
    cv.rect(3, 2, 6, 2, WHITE)
    cv.rect(4, 3, 5, 3, WHITE)
    cv.outline()
    return cv


def other_booth():
    cv = Canvas(64, 48)
    cv.rect(0, 40, 63, 47, FLOOR)
    cv.rect(0, 40, 63, 40, FLOOR_SHADE)
    cv.rect(4, 6, 6, 40, WOOD)
    cv.rect(57, 6, 59, 40, WOOD)
    cv.rect(6, 6, 6, 40, WOOD_SHADE)
    cv.rect(59, 6, 59, 40, WOOD_SHADE)
    for i, x in enumerate(range(2, 62, 8)):  # green awning, differs from the home booth's red
        c = GREEN if i % 2 == 0 else CLOTH
        cv.rect(x, 2, x + 7, 8, c)
        cv.rect(x, 8, x + 7, 8, GREEN_SHADE if i % 2 == 0 else CLOTH_SHADE)
    cv.rect(2, 2, 61, 2, (140, 200, 132))
    cv.box(22, 12, 41, 20, CLOTH, WHITE, CLOTH_SHADE)
    cv.rect(25, 15, 38, 16, YELLOW)
    table(cv, 8, 55, 26, 46)
    # two small cases on the table
    for x in (14, 36):
        cv.box(x, 20, x + 13, 25, GLASS, GLASS_LIGHT, GLASS_SHADE)
    cv.outline()
    return cv


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else "Assets/Resources"
    out = os.path.join(root, "Event")
    os.makedirs(out, exist_ok=True)
    save(out, "booth_bg", booth_bg())
    save(out, "booth_case", booth_case())
    save(out, "sold_tag", sold_tag())
    for kind in LOOKS:
        for f in (0, 1):
            save(out, "visitor_%s_%d" % (kind, f), visitor(kind, f))
    save(out, "aisle_bg", aisle_bg())
    save(out, "counter_fg", counter_fg())
    for kind in LOOKS:
        save(out, "visitor_%s_front" % kind, visitor_front(kind))
    save(out, "bubble", bubble())
    save(out, "bubble_clerk", bubble_clerk())
    save(out, "bubble_tail_down", bubble_tail_down())
    save(out, "other_booth", other_booth())
    print("wrote", out)


if __name__ == "__main__":
    main()
