using System;
using System.Collections.Generic;

namespace ChestDisplay.Visuals
{
    /// <summary>Символ в строке числа: какой глиф и где он стоит (в долях высоты символа, от левого края строки).</summary>
    public struct GlyphPlace
    {
        public int Glyph;
        public float X;
        public float Width;
    }

    /// <summary>
    /// Цифры для числа на табличке — нарисованы кодом (без Unity: массив RGBA32, строки снизу вверх — как ждёт Texture2D).
    /// Округлые штрихи постоянной толщины: светлая краска цвета текста интерфейса Valheim с тёмно-коричневой обводкой —
    /// читается на дереве и вблизи, и издали. Альфа — штрих с обводкой и сглаженным краем (в игре его режет вырез
    /// по альфе шейдера построек, поэтому цифры освещаются как доска, а не светятся).
    /// Все символы — в одном атласе-строке: «0»–«9», «k», «+», «.».
    /// </summary>
    public static class DigitArt
    {
        public const string Chars = "0123456789k+.";

        /// <summary>Поля клетки атласа вокруг символа, в долях высоты (чтобы сглаженный край не обрезался).</summary>
        public const float Pad = 0.12f;

        /// <summary>Промежуток между символами в строке, в долях высоты.</summary>
        public const float Spacing = 0.07f;

        /// <summary>Полутолщина светлого штриха и ширина тёмной обводки вокруг него, в долях высоты.</summary>
        private const float HalfStroke = 0.068f;
        private const float Outline = 0.034f;

        // Краска — как текст интерфейса игры (кремовый), обводка — тёмное дерево.
        private static readonly float[] s_paint = { 0.95f, 0.87f, 0.68f };
        private static readonly float[] s_outline = { 0.17f, 0.10f, 0.05f };

        private static float[] s_widths;
        private static List<float[]>[] s_strokes;

        /// <summary>Ширина символа в долях высоты.</summary>
        public static float Width(int glyph)
        {
            Init();
            return s_widths[glyph];
        }

        public static int Count
        {
            get
            {
                Init();
                return s_widths.Length;
            }
        }

        /// <summary>Расставить символы строки слева направо; неизвестные символы пропускаются.</summary>
        public static List<GlyphPlace> Layout(string text, out float totalWidth)
        {
            Init();
            var result = new List<GlyphPlace>();
            float x = 0f;
            foreach (char c in text ?? "")
            {
                int glyph = Chars.IndexOf(c);
                if (glyph < 0)
                {
                    continue;
                }
                if (result.Count > 0)
                {
                    x += Spacing;
                }
                result.Add(new GlyphPlace { Glyph = glyph, X = x, Width = s_widths[glyph] });
                x += s_widths[glyph];
            }
            totalWidth = x;
            return result;
        }

        /// <summary>Клетка символа в атласе: левый и правый край по u (0..1); по v клетка занимает всю высоту.</summary>
        public static void AtlasCell(int glyph, out float u0, out float u1)
        {
            Init();
            float total = 0f, start = 0f;
            for (int i = 0; i < s_widths.Length; i++)
            {
                if (i == glyph)
                {
                    start = total;
                }
                total += s_widths[i] + 2f * Pad;
            }
            u0 = start / total;
            u1 = (start + s_widths[glyph] + 2f * Pad) / total;
        }

        /// <summary>Атлас всех символов в одну строку: высота символа — <paramref name="unit"/> пикселей.</summary>
        public static byte[] Atlas(int unit, out int width, out int height)
        {
            Init();
            float total = 0f;
            foreach (float w in s_widths)
            {
                total += w + 2f * Pad;
            }
            width = (int)Math.Ceiling(total * unit);
            height = (int)Math.Ceiling((1f + 2f * Pad) * unit);
            var px = new byte[width * height * 4];
            float aa = 1.2f / unit; // сглаживание края — чуть больше пикселя
            float cellStart = 0f;
            for (int g = 0; g < s_widths.Length; g++)
            {
                int x0 = (int)Math.Floor(cellStart * unit);
                int x1 = Math.Min(width, (int)Math.Ceiling((cellStart + s_widths[g] + 2f * Pad) * unit));
                List<float[]> strokes = s_strokes[g];
                for (int y = 0; y < height; y++)
                {
                    float v = (y + 0.5f) / unit - Pad;
                    for (int x = x0; x < x1; x++)
                    {
                        float u = (x + 0.5f) / unit - cellStart - Pad;
                        float d = float.MaxValue;
                        foreach (float[] line in strokes)
                        {
                            d = Math.Min(d, PolylineDistance(u, v, line));
                        }
                        float a = SmoothStep(HalfStroke + Outline + aa, HalfStroke + Outline - aa, d);
                        if (a <= 0f)
                        {
                            continue;
                        }
                        float paint = SmoothStep(HalfStroke + aa, HalfStroke - aa, d);
                        int i = (y * width + x) * 4;
                        for (int c = 0; c < 3; c++)
                        {
                            px[i + c] = ToByte(s_outline[c] + (s_paint[c] - s_outline[c]) * paint);
                        }
                        px[i + 3] = ToByte(a);
                    }
                }
                cellStart += s_widths[g] + 2f * Pad;
            }
            return px;
        }

        // ================================================================== начертания

        private static void Init()
        {
            if (s_widths != null)
            {
                return;
            }
            var widths = new float[Chars.Length];
            var strokes = new List<float[]>[Chars.Length];
            for (int i = 0; i < Chars.Length; i++)
            {
                strokes[i] = new List<float[]>();
            }

            Glyph(widths, strokes, '0', 0.58f, Arc(0.29f, 0.5f, 0.21f, 0.40f, 0f, 360f));
            Glyph(widths, strokes, '1', 0.46f, Line(0.10f, 0.76f, 0.27f, 0.92f, 0.27f, 0.08f), Line(0.10f, 0.08f, 0.42f, 0.08f));
            Glyph(widths, strokes, '2', 0.58f, Join(Arc(0.29f, 0.70f, 0.20f, 0.20f, 165f, -35f), Line(0.08f, 0.08f, 0.52f, 0.08f)));
            Glyph(widths, strokes, '3', 0.58f, Arc(0.28f, 0.71f, 0.19f, 0.19f, 155f, -90f), Arc(0.28f, 0.30f, 0.22f, 0.22f, 90f, -155f));
            Glyph(widths, strokes, '4', 0.60f, Line(0.44f, 0.08f, 0.44f, 0.92f, 0.06f, 0.34f, 0.56f, 0.34f));
            Glyph(widths, strokes, '5', 0.58f, Join(Line(0.50f, 0.92f, 0.14f, 0.92f, 0.11f, 0.56f), Arc(0.29f, 0.33f, 0.22f, 0.24f, 135f, -150f)));
            Glyph(widths, strokes, '6', 0.58f, Arc(0.30f, 0.30f, 0.21f, 0.22f, 0f, 360f), Bezier(0.09f, 0.32f, 0.08f, 0.90f, 0.46f, 0.92f));
            Glyph(widths, strokes, '7', 0.56f, Line(0.07f, 0.92f, 0.50f, 0.92f, 0.20f, 0.08f));
            Glyph(widths, strokes, '8', 0.58f, Arc(0.29f, 0.71f, 0.17f, 0.19f, 0f, 360f), Arc(0.29f, 0.29f, 0.21f, 0.21f, 0f, 360f));
            Glyph(widths, strokes, '9', 0.58f, Arc(0.28f, 0.70f, 0.21f, 0.22f, 0f, 360f), Bezier(0.49f, 0.68f, 0.50f, 0.10f, 0.12f, 0.08f));
            Glyph(widths, strokes, 'k', 0.52f, Line(0.10f, 0.92f, 0.10f, 0.08f), Line(0.44f, 0.60f, 0.11f, 0.30f), Line(0.22f, 0.40f, 0.46f, 0.08f));
            Glyph(widths, strokes, '+', 0.56f, Line(0.28f, 0.24f, 0.28f, 0.72f), Line(0.06f, 0.48f, 0.50f, 0.48f));
            Glyph(widths, strokes, '.', 0.24f, Line(0.12f, 0.11f, 0.12f, 0.11f));
            s_strokes = strokes;
            s_widths = widths;
        }

        private static void Glyph(float[] widths, List<float[]>[] strokes, char c, float width, params float[][] lines)
        {
            int i = Chars.IndexOf(c);
            widths[i] = width;
            strokes[i].AddRange(lines);
        }

        /// <summary>Ломаная по точкам x1, y1, x2, y2, ...</summary>
        private static float[] Line(params float[] points) => points;

        private static float[] Join(float[] a, float[] b)
        {
            var r = new float[a.Length + b.Length];
            Array.Copy(a, r, a.Length);
            Array.Copy(b, 0, r, a.Length, b.Length);
            return r;
        }

        /// <summary>Дуга эллипса от угла a0 до a1 (градусы, можно убывающие — тогда по часовой стрелке).</summary>
        private static float[] Arc(float cx, float cy, float rx, float ry, float a0, float a1)
        {
            const int n = 32;
            var r = new float[(n + 1) * 2];
            for (int i = 0; i <= n; i++)
            {
                double a = (a0 + (a1 - a0) * i / n) * Math.PI / 180.0;
                r[i * 2] = cx + rx * (float)Math.Cos(a);
                r[i * 2 + 1] = cy + ry * (float)Math.Sin(a);
            }
            return r;
        }

        /// <summary>Квадратичная кривая Безье p0 → p2 с опорной точкой p1.</summary>
        private static float[] Bezier(float x0, float y0, float x1, float y1, float x2, float y2)
        {
            const int n = 24;
            var r = new float[(n + 1) * 2];
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n, s = 1f - t;
                r[i * 2] = s * s * x0 + 2f * s * t * x1 + t * t * x2;
                r[i * 2 + 1] = s * s * y0 + 2f * s * t * y1 + t * t * y2;
            }
            return r;
        }

        private static float PolylineDistance(float px, float py, float[] p)
        {
            if (p.Length == 2)
            {
                return SegmentDistance(px, py, p[0], p[1], p[0], p[1]);
            }
            float d = float.MaxValue;
            for (int i = 0; i + 3 < p.Length; i += 2)
            {
                d = Math.Min(d, SegmentDistance(px, py, p[i], p[i + 1], p[i + 2], p[i + 3]));
            }
            return d;
        }

        private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float len2 = dx * dx + dy * dy;
            float t = len2 > 1e-12f ? ((px - ax) * dx + (py - ay) * dy) / len2 : 0f;
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            float cx = ax + t * dx - px, cy = ay + t * dy - py;
            return (float)Math.Sqrt(cx * cx + cy * cy);
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = (x - edge0) / (edge1 - edge0);
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return t * t * (3f - 2f * t);
        }

        private static byte ToByte(float f)
        {
            int v = (int)(f * 255f + 0.5f);
            return (byte)(v < 0 ? 0 : (v > 255 ? 255 : v));
        }
    }
}
