using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using ChestDisplay.Logic;
using ChestDisplay.Visuals;

namespace ChestDisplay.Tests
{
    /// <summary>
    /// Проверки правил таблички и рисунка цифр (dotnet run --project tests/LogicTests -- &lt;папка для png&gt;):
    /// с папкой — выгружает атлас цифр и числа, как их рисует мод, для превью (tests/sign_preview.py).
    /// </summary>
    internal static class Program
    {
        private static int s_failed;
        private static int s_passed;

        /// <summary>Числа для превью: от одной цифры до самых длинных сокращений.</summary>
        private static readonly string[] s_samples = { "7", "48", "999", "1.2k", "12k", "99k+" };

        private static int Main(string[] args)
        {
            FirstSlot();
            Faces();
            Barrels();
            Sizes();
            Digits();
            Fitting();
            Parsing();

            string outDir = args.Length > 0 ? args[0] : null;
            if (outDir != null)
            {
                Directory.CreateDirectory(outDir);
                byte[] atlas = DigitArt.Atlas(128, out int w, out int h);
                WritePng(Path.Combine(outDir, "digits_atlas.png"), atlas, w, h);
                foreach (string s in s_samples)
                {
                    byte[] text = RenderText(s, 128, out int tw, out int th);
                    WritePng(Path.Combine(outDir, "num_" + s.Replace("+", "plus") + ".png"), text, tw, th);
                }
                Console.WriteLine($"PNG: {outDir}");
            }
            Console.WriteLine($"passed {s_passed}, failed {s_failed}");
            return s_failed == 0 ? 0 : 1;
        }

        private static void FirstSlot()
        {
            Check("пустой сундук — нет первой ячейки", DisplayRules.FirstSlot(new int[0], new int[0]) == -1);
            Check("одна вещь — она и первая", DisplayRules.FirstSlot(new[] { 4 }, new[] { 3 }) == 0);
            // (3,0) (1,1) (0,2) (2,0): первая строка важнее столбца → (2,0).
            Check("сначала строка, потом столбец",
                DisplayRules.FirstSlot(new[] { 3, 1, 0, 2 }, new[] { 0, 1, 2, 0 }) == 3);
            Check("левее в той же строке — раньше", DisplayRules.Precedes(0, 1, 5, 1));
            Check("выше — раньше, даже если правее", DisplayRules.Precedes(5, 0, 0, 1));
            Check("та же ячейка — не раньше", !DisplayRules.Precedes(2, 2, 2, 2));
        }

        private static void Faces()
        {
            Check("нормаль +X → грань +X", DisplayRules.PickFace(1f, 0f, 0f, -1f, 0f) == Face.PosX);
            Check("нормаль -Z → грань -Z", DisplayRules.PickFace(0f, 0f, -1f, 1f, 0f) == Face.NegZ);
            Check("наклонная нормаль — по большей составляющей", DisplayRules.PickFace(0.3f, 0.5f, 0.8f, 0f, 0f) == Face.PosZ);
            Check("смотрит на крышку → на крышку", DisplayRules.PickFace(0.05f, 0.99f, 0.02f, -2f, 0.5f) == Face.Top);
            Check("скос крышки (нормаль чуть вбок, вверх) → на крышку", DisplayRules.PickFace(0.45f, 0.89f, 0f, 0f, -1f) == Face.Top);
            Check("заметно вбок — уже по нормали", DisplayRules.PickFace(0.6f, 0.8f, 0f, 0f, -1f) == Face.PosX);
            Check("дно (нормаль вниз) → грань к игроку", DisplayRules.PickFace(0f, -1f, 0f, 0.4f, 3f) == Face.PosZ);

            Check("на крышке: игрок спереди (+Z) → верх иконки к -Z", DisplayRules.TopUp(0.2f, 2f) == Face.NegZ);
            Check("на крышке: игрок слева (-X) → верх иконки к +X", DisplayRules.TopUp(-2f, 0.5f) == Face.PosX);

            DisplayRules.FaceNormal(Face.NegX, out float x, out float z);
            Check("нормаль грани -X", x == -1f && z == 0f);
            DisplayRules.FaceNormal(Face.PosZ, out x, out z);
            Check("нормаль грани +Z", x == 0f && z == 1f);
        }

        private static void Barrels()
        {
            Check("ванильная бочка — не сундук", DisplayRules.IsNotChest("piece_chest_barrel(Clone)", "$piece_chestbarrel"));
            Check("ванильный шкаф — не сундук", DisplayRules.IsNotChest("piece_chest_warderobe(Clone)", "$piece_chestwarderobe"));
            Check("модовая бочка по имени сундука", DisplayRules.IsNotChest("mod_storage", "$mod_Barrel_big"));
            Check("модовый шкаф (wardrobe)", DisplayRules.IsNotChest("piece_wardrobe_oak", "$mod_wardrobe"));
            Check("деревянный сундук — сундук", !DisplayRules.IsNotChest("piece_chest_wood(Clone)", "$piece_chestwood"));
            Check("армированный сундук — сундук", !DisplayRules.IsNotChest("piece_chest(Clone)", "$piece_chest"));
            Check("сундук из чёрного металла — сундук", !DisplayRules.IsNotChest("piece_chest_blackmetal(Clone)", "$piece_chestblackmetal"));
            Check("пустые имена — сундук", !DisplayRules.IsNotChest(null, ""));
        }

        private static void Sizes()
        {
            Check("0 % — стандартный размер", Near(DisplayRules.SignSize(0.34f, 0.65f, 0f), 0.34f));
            Check("100 % — меньшая сторона поверхности", Near(DisplayRules.SignSize(0.34f, 0.65f, 100f), 0.65f));
            Check("50 % — посередине", Near(DisplayRules.SignSize(0.34f, 0.64f, 50f), 0.49f));
            Check("поверхность меньше стандартной — табличка не уменьшается", Near(DisplayRules.SignSize(0.34f, 0.2f, 100f), 0.34f));
            Check("проценты за пределами 0–100 обрезаются",
                Near(DisplayRules.SignSize(0.34f, 0.65f, 250f), 0.65f) && Near(DisplayRules.SignSize(0.34f, 0.65f, -5f), 0.34f));

            Check("количество до 999 — как есть", DisplayRules.FormatCount(0) == "0" && DisplayRules.FormatCount(999) == "999");
            Check("тысячи — с десятой вниз", DisplayRules.FormatCount(1250) == "1.2k" && DisplayRules.FormatCount(9999) == "9.9k");
            Check("ровные тысячи — без .0", DisplayRules.FormatCount(1000) == "1k" && DisplayRules.FormatCount(3050) == "3k");
            Check("десятки тысяч — целые k", DisplayRules.FormatCount(12345) == "12k" && DisplayRules.FormatCount(99999) == "99k");
            Check("больше — 99k+", DisplayRules.FormatCount(100000) == "99k+" && DisplayRules.FormatCount(int.MaxValue) == "99k+");
            foreach (int n in new[] { 0, 7, 42, 999, 1000, 1999, 9999, 10000, 54321, 99999, 100000, 5000000 })
            {
                string s = DisplayRules.FormatCount(n);
                Check($"{n} → «{s}» не длиннее 4 знаков и рисуется целиком",
                    s.Length <= 4 && DigitArt.Layout(s, out _).Count == s.Length);
            }
        }

        private static void Digits()
        {
            Check("все символы атласа на месте", DigitArt.Count == DigitArt.Chars.Length);
            List<GlyphPlace> places = DigitArt.Layout("1.2k", out float width);
            Check("раскладка: 4 символа слева направо",
                places.Count == 4 && places[0].X == 0f && places[1].X > places[0].X && places[3].X > places[2].X);
            Check("раскладка: ширина = символы + промежутки",
                Near(width, places[3].X + places[3].Width));
            Check("неизвестные символы пропускаются", DigitArt.Layout("1a2", out _).Count == 2);
            DigitArt.AtlasCell(0, out float a0, out float a1);
            DigitArt.AtlasCell(DigitArt.Count - 1, out float b0, out float b1);
            Check("клетки атласа: от 0 до 1 по порядку", a0 == 0f && a1 > a0 && b0 >= a1 && Near(b1, 1f));

            byte[] atlas = DigitArt.Atlas(64, out int w, out int h);
            bool allDrawn = true;
            for (int g = 0; g < DigitArt.Count; g++)
            {
                DigitArt.AtlasCell(g, out float u0, out float u1);
                int opaque = 0;
                for (int y = 0; y < h; y++)
                {
                    for (int x = (int)(u0 * w); x < (int)(u1 * w); x++)
                    {
                        opaque += atlas[(y * w + x) * 4 + 3] > 128 ? 1 : 0;
                    }
                }
                allDrawn &= opaque > 20;
            }
            Check("каждый символ в атласе нарисован", allDrawn);
        }

        /// <summary>Строка числа, как её собирает мод: клетки атласа одна за другой, со сдвигом по раскладке.</summary>
        private static byte[] RenderText(string text, int unit, out int width, out int height)
        {
            byte[] atlas = DigitArt.Atlas(unit, out int aw, out int ah);
            List<GlyphPlace> places = DigitArt.Layout(text, out float total);
            width = (int)Math.Ceiling((total + 2f * DigitArt.Pad) * unit);
            height = ah;
            var px = new byte[width * height * 4];
            foreach (GlyphPlace p in places)
            {
                DigitArt.AtlasCell(p.Glyph, out float u0, out float u1);
                int sx0 = (int)Math.Round(u0 * aw), sx1 = (int)Math.Round(u1 * aw);
                int dx0 = (int)Math.Round(p.X * unit);
                for (int y = 0; y < height; y++)
                {
                    for (int sx = sx0; sx < sx1; sx++)
                    {
                        int dx = dx0 + sx - sx0;
                        if (dx < 0 || dx >= width)
                        {
                            continue;
                        }
                        int s = (y * aw + sx) * 4, d = (y * width + dx) * 4;
                        if (atlas[s + 3] > px[d + 3])
                        {
                            Buffer.BlockCopy(atlas, s, px, d, 4);
                        }
                    }
                }
            }
            return px;
        }

        private static void Fitting()
        {
            // Иконка 64×64, pivot в центре, 100 px на единицу → 0.64 ед.; вписываем в 0.25.
            IconFit f = DisplayRules.Fit(64f, 64f, 32f, 32f, 100f, 0.25f);
            Check("квадратная иконка: масштаб", Near(f.Scale, 0.25f / 0.64f));
            Check("квадратная иконка: центр в опорной точке", Near(f.CenterX, 0f) && Near(f.CenterY, 0f));

            // Pivot в левом нижнем углу: центр прямоугольника — (+0.32, +0.32).
            f = DisplayRules.Fit(64f, 64f, 0f, 0f, 100f, 0.25f);
            Check("pivot в углу: центр сдвинут", Near(f.CenterX, 0.32f) && Near(f.CenterY, 0.32f));

            // Вытянутая 128×64: вписывается по длинной стороне.
            f = DisplayRules.Fit(128f, 64f, 64f, 32f, 100f, 0.25f);
            Check("вытянутая иконка — по длинной стороне", Near(f.Scale, 0.25f / 1.28f));

            f = DisplayRules.Fit(64f, 64f, 32f, 32f, 0f, 0.25f);
            Check("ppu = 0 не ломает расчёт", Near(f.Scale, 0.25f / 0.64f));
            f = DisplayRules.Fit(0f, 0f, 0f, 0f, 100f, 0.25f);
            Check("пустой спрайт — масштаб 0", f.Scale == 0f);
        }

        private static void Parsing()
        {
            List<KeyValuePair<string, int>> cost = DisplayRules.ParseCost("GreydwarfEye:1,Wood:2,Coal:2,Resin:2");
            Check("рецепт по умолчанию: 4 предмета", cost.Count == 4);
            Check("рецепт по умолчанию: порядок и количества",
                cost.Count == 4 && cost[0].Key == "GreydwarfEye" && cost[0].Value == 1 && cost[1].Key == "Wood" && cost[1].Value == 2 &&
                cost[2].Key == "Coal" && cost[2].Value == 2 && cost[3].Key == "Resin" && cost[3].Value == 2);
            Check("рецепт + станок помещаются в 6 ячеек", DisplayRules.StationFits(cost.Count, 6));
            Check("5 ресурсов + станок — ещё да", DisplayRules.StationFits(5, 6));
            Check("6 ресурсов + станок — уже нет", !DisplayRules.StationFits(6, 6));

            var errors = new List<string>();
            cost = DisplayRules.ParseCost(" Wood : 3 ; Stone ; Bad:x, :2, Neg:-1, A:1:2 ", errors);
            Check("пробелы и ; допустимы, без количества — 1",
                cost.Count == 2 && cost[0].Key == "Wood" && cost[0].Value == 3 && cost[1].Key == "Stone" && cost[1].Value == 1);
            Check("ошибки собраны", errors.Count == 4);
            Check("пустая строка — пустой рецепт", DisplayRules.ParseCost("  ").Count == 0);
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) < 1e-5f;

        /// <summary>RGBA32, строки снизу вверх (как в Texture2D) → PNG с прозрачностью.</summary>
        private static void WritePng(string path, byte[] rgba, int w, int h)
        {
            var raw = new byte[(w * 4 + 1) * h];
            for (int y = 0; y < h; y++)
            {
                raw[y * (w * 4 + 1)] = 0;
                Buffer.BlockCopy(rgba, (h - 1 - y) * w * 4, raw, y * (w * 4 + 1) + 1, w * 4);
            }
            using var fs = File.Create(path);
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var ihdr = new byte[13];
            BigEndian(ihdr, 0, w);
            BigEndian(ihdr, 4, h);
            ihdr[8] = 8;
            ihdr[9] = 6;
            Chunk(fs, "IHDR", ihdr);
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true))
                {
                    z.Write(raw, 0, raw.Length);
                }
                Chunk(fs, "IDAT", ms.ToArray());
            }
            Chunk(fs, "IEND", Array.Empty<byte>());
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            BigEndian(len, 0, data.Length);
            s.Write(len);
            byte[] t = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(t);
            s.Write(data);
            uint crc = Crc(t, 0xFFFFFFFFu);
            crc = Crc(data, crc) ^ 0xFFFFFFFFu;
            var c = new byte[4];
            BigEndian(c, 0, (int)crc);
            s.Write(c);
        }

        private static uint Crc(byte[] data, uint crc)
        {
            foreach (byte b in data)
            {
                crc ^= b;
                for (int k = 0; k < 8; k++)
                {
                    crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
                }
            }
            return crc;
        }

        private static void BigEndian(byte[] b, int o, int v)
        {
            b[o] = (byte)(v >> 24);
            b[o + 1] = (byte)(v >> 16);
            b[o + 2] = (byte)(v >> 8);
            b[o + 3] = (byte)v;
        }

        private static void Check(string name, bool ok)
        {
            if (ok)
            {
                s_passed++;
            }
            else
            {
                s_failed++;
                Console.WriteLine("FAIL: " + name);
            }
        }
    }
}
