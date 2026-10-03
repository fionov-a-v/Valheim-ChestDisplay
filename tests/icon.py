"""Иконка мода 256×256: деревянный сундук, на лицевой стороне — табличка в тёмной рамке со слитком железа
(тот же предмет, что на иконке постройки в меню молота). Баннер — кладовая: ряд таких сундуков, на табличках разные
предметы. Рисуется целиком кодом (без картинок из игры):

python3 tests/icon.py package/icon.png                 иконка 256×256
python3 tests/icon.py media/banner.png --banner        баннер 1300×372 (шапка страницы мода)
"""
import math
import random
import sys

from PIL import Image, ImageDraw, ImageFilter

S = 1024  # рисуем крупно и уменьшаем — сглаживание


def radial_background():
    bg = Image.new("RGB", (S, S))
    px = bg.load()
    for y in range(S):
        for x in range(S):
            d = math.hypot(x - S * 0.5, y - S * 0.52) / (S * 0.72)
            t = max(0.0, 1.0 - d)
            px[x, y] = (int(14 + 40 * t), int(11 + 28 * t), int(9 + 16 * t))
    return bg


def planks(d, box, color, dark, count, horizontal=True):
    x0, y0, x1, y1 = box
    d.rectangle(box, fill=color)
    for i in range(1, count):
        if horizontal:
            y = y0 + (y1 - y0) * i / count
            d.line((x0, y, x1, y), fill=dark, width=7)
        else:
            x = x0 + (x1 - x0) * i / count
            d.line((x, y0, x, y1), fill=dark, width=7)


def chest():
    layer = Image.new("RGBA", (S, S))
    d = ImageDraw.Draw(layer)
    wood, wood_dark = (122, 82, 46, 255), (78, 51, 29, 255)
    iron, iron_hi = (52, 56, 62, 255), (126, 134, 146, 255)

    x0, x1 = 120, 904          # корпус
    body_top, body_bot = 400, 900
    lid_top = 236

    planks(d, (x0, body_top, x1, body_bot), wood, wood_dark, 5)
    d.rounded_rectangle((x0 - 14, lid_top, x1 + 14, body_top + 24), radius=90, fill=wood)
    for i in range(1, 3):
        y = lid_top + 60 + (body_top - lid_top - 60) * i / 3
        d.line((x0, y, x1, y), fill=wood_dark, width=7)
    # Оковка: по углам и по стыку крышки.
    for bx in (x0, x1 - 60):
        d.rectangle((bx, lid_top + 50, bx + 60, body_bot), fill=iron)
        d.line((bx + 8, lid_top + 58, bx + 8, body_bot - 8), fill=iron_hi, width=5)
    d.rectangle((x0 - 18, body_top - 8, x1 + 18, body_top + 34), fill=iron)
    d.line((x0 - 10, body_top, x1 + 10, body_top), fill=iron_hi, width=5)
    d.rectangle((x0 - 8, body_bot - 40, x1 + 8, body_bot), fill=iron)
    for bx in (x0 + 30, x1 - 30):
        for by in (body_top + 90, body_top + 250, body_top + 410):
            d.ellipse((bx - 11, by - 11, bx + 11, by + 11), fill=iron_hi)
    return layer


def sign():
    """Табличка: тёмная рамка, светлая доска с волокнами, четыре гвоздика."""
    layer = Image.new("RGBA", (S, S))
    d = ImageDraw.Draw(layer)
    cx, cy, half = 512, 672, 196
    d.rectangle((cx - half, cy - half, cx + half, cy + half), fill=(58, 38, 22, 255))
    inner = half - 30
    board = (cx - inner, cy - inner, cx + inner, cy + inner)
    d.rectangle(board, fill=(196, 152, 98, 255))
    rnd = random.Random(3)
    for _ in range(22):  # волокна дерева
        y = rnd.uniform(board[1] + 10, board[3] - 10)
        d.line((board[0] + 6, y, board[2] - 6, y + rnd.uniform(-6, 6)), fill=(176, 132, 82, 255), width=3)
    for nx in (-1, 1):
        for ny in (-1, 1):
            x, y = cx + nx * (half - 15), cy + ny * (half - 15)
            d.ellipse((x - 9, y - 9, x + 9, y + 9), fill=(40, 40, 44, 255))
    return layer


def ingot():
    """Слиток железа: трапеция с гранями (верх светлее, бока темнее) и бликом, с мягкой тенью."""
    layer = Image.new("RGBA", (S, S))
    d = ImageDraw.Draw(layer)
    cx, cy = 512, 690
    top_w, bot_w, top_h, side_h = 104, 150, 74, 60
    y_top, y_mid = cy - 62, cy - 62 + top_h
    top = [(cx - top_w, y_top), (cx + top_w, y_top), (cx + bot_w - 18, y_mid), (cx - bot_w + 18, y_mid)]
    front = [(cx - bot_w + 18, y_mid), (cx + bot_w - 18, y_mid), (cx + bot_w, y_mid + side_h), (cx - bot_w, y_mid + side_h)]
    d.polygon(front, fill=(62, 68, 78, 255))
    d.polygon(top, fill=(150, 160, 174, 255))
    d.line(top + [top[0]], fill=(42, 46, 54, 255), width=6)
    d.line([front[0], front[3], front[2], front[1]], fill=(42, 46, 54, 255), width=6)
    d.polygon([(cx - top_w + 22, y_top + 12), (cx - 10, y_top + 12), (cx - 30, y_mid - 14), (cx - bot_w + 52, y_mid - 14)],
              fill=(206, 214, 226, 200))
    return with_shadow(layer)


def with_shadow(layer):
    """Рисунок на табличке с мягкой тенью вниз-вправо."""
    shadow = layer.split()[3].filter(ImageFilter.GaussianBlur(10))
    out = Image.new("RGBA", (S, S))
    out.paste((40, 20, 10, 150), (6, 10), shadow)
    out.alpha_composite(layer)
    return out


def logs():
    """Древесина: три бревна пирамидкой, торцами к зрителю — кольца годовые и кора."""
    layer = Image.new("RGBA", (S, S))
    d = ImageDraw.Draw(layer)
    for x, y in ((452, 712), (572, 712), (512, 612)):
        r = 62
        d.ellipse((x - r, y - r, x + r, y + r), fill=(92, 58, 30, 255))
        d.ellipse((x - r + 12, y - r + 12, x + r - 12, y + r - 12), fill=(214, 172, 112, 255))
        for k in (34, 18):
            d.ellipse((x - k, y - k, x + k, y + k), outline=(170, 124, 72, 255), width=4)
        d.ellipse((x - 5, y - 5, x + 5, y + 5), fill=(150, 104, 58, 255))
    return with_shadow(layer)


def stone():
    """Камень: два серых угловатых валуна с бликами."""
    layer = Image.new("RGBA", (S, S))
    d = ImageDraw.Draw(layer)
    for pts, col, hi in (
            ([(380, 760), (402, 664), (470, 618), (548, 640), (580, 712), (556, 768)], (110, 112, 116, 255), (160, 162, 168, 255)),
            ([(520, 768), (540, 690), (606, 652), (662, 690), (650, 768)], (88, 90, 96, 255), (140, 142, 150, 255))):
        d.polygon(pts, fill=col)
        d.line(pts + [pts[0]], fill=(52, 54, 58, 255), width=6)
        d.polygon([pts[1], pts[2], ((pts[2][0] + pts[3][0]) // 2, pts[2][1] + 30), (pts[1][0] + 20, pts[1][1] + 40)], fill=hi)
    return with_shadow(layer)


def resin():
    """Смола: три янтарные капли с бликами."""
    layer = Image.new("RGBA", (S, S))
    d = ImageDraw.Draw(layer)
    for x, y, r in ((452, 720, 52), (574, 708, 60), (514, 612, 46)):
        d.ellipse((x - r, y - r, x + r, y + r), fill=(196, 104, 18, 255))
        d.polygon([(x - r * 0.72, y - r * 0.6), (x + r * 0.72, y - r * 0.6), (x, y - r * 1.9)], fill=(196, 104, 18, 255))
        d.ellipse((x - r + 10, y - r + 10, x + r - 6, y + r - 6), fill=(236, 156, 40, 255))
        d.ellipse((x - r * 0.45, y - r * 0.55, x - r * 0.05, y - r * 0.15), fill=(255, 226, 150, 230))
    return with_shadow(layer)


def coal():
    """Уголь: кучка чёрных угловатых кусков с синеватыми бликами."""
    layer = Image.new("RGBA", (S, S))
    d = ImageDraw.Draw(layer)
    for pts in (
            [(400, 770), (412, 700), (468, 676), (512, 712), (500, 770)],
            [(488, 772), (500, 708), (560, 680), (622, 716), (610, 772)],
            [(450, 690), (474, 628), (540, 608), (578, 652), (540, 700)]):
        d.polygon(pts, fill=(34, 34, 38, 255))
        d.line(pts + [pts[0]], fill=(14, 14, 16, 255), width=5)
        d.line((pts[1], pts[2]), fill=(118, 124, 140, 255), width=7)
    return with_shadow(layer)


def stocked_chest(item):
    """Сундук с табличкой и рисунком предмета на ней — слой 1024×1024, низ сундука на y=900."""
    layer = Image.new("RGBA", (S, S))
    layer.alpha_composite(chest())
    layer.alpha_composite(sign())
    layer.alpha_composite(item())
    return layer


def banner(out):
    """Кладовая: на дощатом полу у бревенчатой стены ряд сундуков с табличками, в центре — самый крупный, со слитком."""
    W, H = 2600, 744  # вдвое больше итоговых 1300×372
    floor_y = 590
    img = Image.new("RGB", (W, H))
    px = img.load()
    for y in range(H):
        for x in range(W):
            glow_c = max(0.0, 1.0 - math.hypot((x - W / 2) / (W * 0.42), (y - H * 0.62) / (H * 0.95)))
            if y < floor_y:
                px[x, y] = (int(22 + 70 * glow_c), int(15 + 44 * glow_c), int(10 + 22 * glow_c))
            else:
                px[x, y] = (int(30 + 64 * glow_c), int(20 + 40 * glow_c), int(12 + 20 * glow_c))
    img = img.convert("RGBA")

    # Стена из горизонтальных брёвен и пол из досок: тёмные стыки поверх градиента.
    seams = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(seams)
    for y in range(40, floor_y, 92):
        d.line((0, y, W, y), fill=(0, 0, 0, 90), width=8)
        d.line((0, y + 6, W, y + 6), fill=(255, 210, 150, 18), width=3)
    d.line((0, floor_y, W, floor_y), fill=(0, 0, 0, 140), width=10)
    rnd = random.Random(5)
    for row, y in enumerate(range(floor_y + 34, H, 46)):
        d.line((0, y, W, y), fill=(0, 0, 0, 70), width=5)
        for x in range(int(rnd.uniform(0, 300)), W, 420):
            d.line((x, y - 46, x, y), fill=(0, 0, 0, 60), width=5)
    img.alpha_composite(seams)

    def place(layer, scale, cx, base_y):
        """Слой 1024×1024 (сундук стоит низом на y=900) — уменьшить и поставить низом на base_y по центру cx."""
        size = int(S * scale)
        small = layer.resize((size, size), Image.LANCZOS)
        img.alpha_composite(small, (int(cx - size / 2), int(base_y - 900 * scale)))

    # Самые крупные — в центре, к краям сундуки меньше (и чуть дальше, выше по полу).
    row = [(W / 2 - 1100, 0.44, coal, floor_y + 40), (W / 2 - 600, 0.56, logs, floor_y + 62),
           (W / 2, 0.7, ingot, floor_y + 86),
           (W / 2 + 600, 0.56, stone, floor_y + 62), (W / 2 + 1100, 0.44, resin, floor_y + 40)]
    shadows = Image.new("RGBA", (W, H))
    for cx, scale, _, base_y in row:
        w = 430 * scale
        ImageDraw.Draw(shadows).ellipse((cx - w, base_y - 26 * scale, cx + w, base_y + 34 * scale), fill=(0, 0, 0, 160))
    img.alpha_composite(shadows.filter(ImageFilter.GaussianBlur(12)))
    for cx, scale, item, base_y in sorted(row, key=lambda r: r[1]):
        place(stocked_chest(item), scale, cx, base_y)

    img.convert("RGB").resize((W // 2, H // 2), Image.LANCZOS).save(out)
    print(out)


def main():
    out = sys.argv[1]
    if "--banner" in sys.argv[2:]:
        banner(out)
        return
    img = radial_background().convert("RGBA")
    body = chest()
    shadow = body.split()[3].filter(ImageFilter.GaussianBlur(28))
    dark = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    dark.paste((0, 0, 0, 140), (0, 18), shadow)
    img.alpha_composite(dark)
    img.alpha_composite(body)
    img.alpha_composite(sign())
    img.alpha_composite(ingot())
    img.convert("RGB").resize((256, 256), Image.LANCZOS).save(out)
    print(out)


if __name__ == "__main__":
    main()
