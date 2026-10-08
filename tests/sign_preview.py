"""Превью таблички спереди — чтобы проверить раскладку иконки и числа без запуска игры.

Размеры — те же, что в моде (SignPiece: BoardSize, PanelSize, IconSize и раскладка числа); цифры — PNG, которые
выгружает тестовый проект ровно так, как их собирает мод (атлас DigitArt). Текстура доски и иконки предметов —
из игры (выгрузить UnityPy в папку game: itemstand_main.png, icon_<имя>.png); без неё доска рисуется ровным цветом.
Освещение не считается: картинка показывает доску при ровном дневном свете.

dotnet run --project tests/LogicTests -c Release -- out
python3 tests/sign_preview.py out <папка game или -> preview.png
"""
import os
import sys

from PIL import Image, ImageDraw

# Как в SignPiece (метры).
BOARD = 0.34
PANEL = 0.29
ICON = 0.25
NUMBER_HEIGHT = 0.064
NUMBER_CENTER_Y = -PANEL / 2 + 0.012 + NUMBER_HEIGHT / 2
ICON_SCALE_WITH_COUNT = 0.76
ICON_SHIFT_WITH_COUNT = 0.045
PAD = 0.12          # DigitArt.Pad — поля клетки символа в долях высоты
UNIT_PX = 128       # высота символа в PNG из тестов

# Как в IconDim: тусклая иконка опустевшего сундука.
DIM_DESATURATE = 0.75
DIM_FADE = 0.5
DIM_DARKEN = 0.8
DIM_WOOD = (74, 56, 34)

PX = 1000           # пикселей на метр в превью
WOOD_TINT = 0.83    # _Color материала itemstand
FRAME_TINT = (0.45, 0.40, 0.36)


def wood(size, tex, tint):
    """Квадрат дерева: текстура доски (как на кубе таблички — треть текстуры на сторону) или ровный цвет."""
    if tex is None:
        base = Image.new("RGB", (size, size), (170, 120, 70))
    else:
        crop = tex.crop((0, 0, tex.width // 3, tex.height // 3)).convert("RGB")
        base = crop.resize((size, size), Image.BICUBIC)
    r, g, b = base.split()
    return Image.merge("RGB", tuple(ch.point(lambda v, k=k: int(v * k)) for ch, k in zip((r, g, b), tint)))


def faded(icon, desaturate=DIM_DESATURATE, fade=DIM_FADE, wood_rgb=DIM_WOOD, darken=DIM_DARKEN):
    """Тусклая иконка — как IconDim в моде: цвет к серому, к цвету доски и темнее; альфа та же."""
    a = icon.split()[3]
    data = icon.convert("RGB").tobytes()
    px = []
    for i in range(0, len(data), 3):
        cr, cg, cb = data[i], data[i + 1], data[i + 2]
        l = 0.299 * cr + 0.587 * cg + 0.114 * cb
        out = []
        for c, w in zip((cr, cg, cb), wood_rgb):
            c = c + (l - c) * desaturate
            c = c + (w - c) * fade
            c *= darken
            out.append(int(max(0, min(255, round(c)))))
        px.append(tuple(out))
    rgb = Image.new("RGB", icon.size)
    rgb.putdata(px)
    rgb.putalpha(a)
    return rgb


def sign(game_dir, icon_name, number, out_dir, dim=None):
    tex = None
    if game_dir and os.path.exists(os.path.join(game_dir, "itemstand_main.png")):
        tex = Image.open(os.path.join(game_dir, "itemstand_main.png"))
    s = int(BOARD * PX)
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    frame_tint = tuple(WOOD_TINT * k for k in FRAME_TINT)
    img.paste(wood(s, tex, frame_tint), (0, 0))
    p = int(PANEL * PX)
    panel = wood(p, tex, (WOOD_TINT,) * 3)
    img.paste(panel, ((s - p) // 2, (s - p) // 2))
    # Тень рамки под доской (доска выступает на 1 см).
    d = ImageDraw.Draw(img)
    o = (s - p) // 2
    d.line((o, o + p, o + p, o + p), fill=(0, 0, 0, 90), width=3)
    d.line((o + p, o, o + p, o + p), fill=(0, 0, 0, 90), width=3)

    def at(x, y):
        """Метры от центра таблички (y вверх) → пиксели картинки."""
        return int(s / 2 + x * PX), int(s / 2 - y * PX)

    if icon_name:
        icon = Image.open(os.path.join(game_dir, "icon_" + icon_name + ".png")).convert("RGBA")
        scale = ICON_SCALE_WITH_COUNT if number else 1.0
        size = int(ICON * scale * PX)
        icon = icon.resize((size, size), Image.LANCZOS)
        if dim is not None:
            icon = dim(icon)
        # В игре иконку режет вырез по альфе (порог 0.5) — без полупрозрачных краёв.
        a = icon.split()[3].point(lambda v: 255 if v >= 128 else 0)
        icon.putalpha(a)
        cx, cy = at(0, ICON_SHIFT_WITH_COUNT if number else 0)
        img.alpha_composite(icon, (cx - size // 2, cy - size // 2))

    if number:
        num = Image.open(os.path.join(out_dir, "num_" + number.replace("+", "plus") + ".png")).convert("RGBA")
        k = NUMBER_HEIGHT * PX / UNIT_PX
        num = num.resize((max(1, int(num.width * k)), max(1, int(num.height * k))), Image.LANCZOS)
        a = num.split()[3].point(lambda v: 255 if v >= 128 else 0)
        num.putalpha(a)
        cx, cy = at(0, NUMBER_CENTER_Y)
        img.alpha_composite(num, (cx - num.width // 2, cy - num.height // 2))
    return img


def main():
    out_dir, game_dir, out = sys.argv[1], sys.argv[2], sys.argv[3]
    game_dir = None if game_dir == "-" else game_dir
    # Последние две — опустевший сундук: тусклая иконка (без числа и с «0»).
    cases = [("wood", None, None), ("wood", "7", None), ("coal", "48", None), ("stone", "999", None),
             ("resin", "1.2k", None), ("iron", "12k", None), ("coins", "99k+", None),
             ("coins", None, faded), ("resin", "0", faded)]
    signs = [sign(game_dir, icon, number, out_dir, dim) for icon, number, dim in cases]
    s = signs[0].width
    gap = 30
    sheet = Image.new("RGB", (len(signs) * (s + gap) + gap, s + 2 * gap + 150), (58, 52, 46))
    for i, img in enumerate(signs):
        sheet.paste(img, (gap + i * (s + gap), gap), img)
        # Та же табличка издали (~3–4 м на экране 1080p: около 90 пикселей).
        small = img.resize((90, 90), Image.LANCZOS)
        sheet.paste(small, (gap + i * (s + gap) + (s - 90) // 2, s + 2 * gap + 20), small)
    # Та же табличка на 100 % на боковине обычного сундука (меньшая сторона ниже крышки ~0.64 м): всё растёт вместе.
    big = sign(game_dir, "iron", "1.2k", out_dir)
    k = 0.64 / BOARD
    big = big.resize((int(big.width * k), int(big.height * k)), Image.LANCZOS)
    full = Image.new("RGB", (sheet.width, sheet.height + big.height + 2 * gap), (58, 52, 46))
    full.paste(sheet, (0, 0))
    full.paste(big, ((full.width - big.width) // 2, sheet.height + gap), big)
    full.save(out)
    print(out)


if __name__ == "__main__":
    main()
