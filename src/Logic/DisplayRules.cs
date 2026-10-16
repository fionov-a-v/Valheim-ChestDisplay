using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChestDisplay.Logic
{
    /// <summary>Грань сундука в его собственных координатах: четыре боковые и верх (крышка).</summary>
    public enum Face
    {
        PosX = 0,
        NegX = 1,
        PosZ = 2,
        NegZ = 3,
        Top = 4,
    }

    /// <summary>Как вписать иконку предмета в квадрат таблички: масштаб и центр в единицах спрайта.</summary>
    public struct IconFit
    {
        /// <summary>Во сколько раз уменьшить/увеличить вершины спрайта.</summary>
        public float Scale;

        /// <summary>Центр прямоугольника спрайта относительно его опорной точки (pivot), в единицах спрайта.</summary>
        public float CenterX;
        public float CenterY;
    }

    /// <summary>Правила таблички — без Unity, чтобы их можно было проверить тестами.</summary>
    public static class DisplayRules
    {
        /// <summary>
        /// Раньше ли ячейка (x1, y1), чем (x2, y2), в порядке, в каком игрок читает сундук: сначала строка, потом столбец.
        /// </summary>
        public static bool Precedes(int x1, int y1, int x2, int y2) => y1 < y2 || (y1 == y2 && x1 < x2);

        /// <summary>Индекс первой занятой ячейки (верхняя левая) в списке координат или -1, если список пуст.</summary>
        public static int FirstSlot(IList<int> xs, IList<int> ys)
        {
            int best = -1;
            for (int i = 0; i < xs.Count; i++)
            {
                if (best < 0 || Precedes(xs[i], ys[i], xs[best], ys[best]))
                {
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// На какую грань вешать табличку — по нормали поверхности, куда смотрит игрок (в координатах сундука).
        /// Нормаль почти вертикальна и смотрит вверх — на крышку; вниз (дно) — на боковую грань, обращённую к игроку
        /// (vx, vz — направление от центра сундука к игроку).
        /// </summary>
        public static Face PickFace(float nx, float ny, float nz, float vx, float vz)
        {
            float x = nx, z = nz;
            if (x * x + z * z < 0.25f)
            {
                if (ny > 0f)
                {
                    return Face.Top;
                }
                x = vx;
                z = vz;
            }
            if (Math.Abs(x) >= Math.Abs(z))
            {
                return x >= 0f ? Face.PosX : Face.NegX;
            }
            return z >= 0f ? Face.PosZ : Face.NegZ;
        }

        /// <summary>
        /// Табличка на крышке: куда (по осям сундука) смотрит верх иконки — от игрока (vx, vz — направление от центра
        /// сундука к игроку), чтобы она читалась с того места, откуда её поставили.
        /// </summary>
        public static Face TopUp(float vx, float vz) => Math.Abs(vx) >= Math.Abs(vz)
            ? (vx > 0f ? Face.NegX : Face.PosX)
            : (vz > 0f ? Face.NegZ : Face.PosZ);

        /// <summary>
        /// Сторона квадратной таблички: при 0 % — стандартная, при 100 % — наименьший размер поверхности, на которой она
        /// висит (если он больше стандартной; меньше стандартной табличка не становится), между ними — линейно.
        /// </summary>
        public static float SignSize(float standard, float surfaceMin, float percent)
        {
            float p = Math.Max(0f, Math.Min(100f, percent)) / 100f;
            float max = Math.Max(standard, surfaceMin);
            return standard + (max - standard) * p;
        }

        /// <summary>
        /// Число на табличке — не длиннее 4 знаков, чтобы всегда помещалось: до 999 — как есть, до 9999 — тысячи с одной
        /// десятой (1.2k, ровно — 1k), до 99 999 — целые тысячи (12k), больше — «99k+». Округление вниз: табличка
        /// не обещает больше, чем лежит.
        /// </summary>
        public static string FormatCount(int count)
        {
            if (count < 1000)
            {
                return Math.Max(0, count).ToString(CultureInfo.InvariantCulture);
            }
            if (count < 10000)
            {
                int tenths = count / 100;
                return tenths % 10 == 0
                    ? (tenths / 10).ToString(CultureInfo.InvariantCulture) + "k"
                    : (tenths / 10).ToString(CultureInfo.InvariantCulture) + "." + (tenths % 10).ToString(CultureInfo.InvariantCulture) + "k";
            }
            if (count < 100000)
            {
                return (count / 1000).ToString(CultureInfo.InvariantCulture) + "k";
            }
            return "99k+";
        }

        /// <summary>Наружная нормаль боковой грани: (x, z).</summary>
        public static void FaceNormal(Face face, out float x, out float z)
        {
            x = face == Face.PosX ? 1f : (face == Face.NegX ? -1f : 0f);
            z = face == Face.PosZ ? 1f : (face == Face.NegZ ? -1f : 0f);
        }

        /// <summary>
        /// Вписать спрайт в квадрат со стороной <paramref name="size"/> по его полному прямоугольнику (вместе с прозрачными
        /// полями, как в инвентаре): rect — размер в пикселях, pivot — опорная точка в пикселях от левого нижнего угла.
        /// </summary>
        public static IconFit Fit(float rectWidth, float rectHeight, float pivotX, float pivotY, float pixelsPerUnit, float size)
        {
            float ppu = pixelsPerUnit > 0f ? pixelsPerUnit : 100f;
            float longest = Math.Max(rectWidth, rectHeight) / ppu;
            return new IconFit
            {
                Scale = longest > 0f ? size / longest : 0f,
                CenterX = (rectWidth * 0.5f - pivotX) / ppu,
                CenterY = (rectHeight * 0.5f - pivotY) / ppu,
            };
        }

        /// <summary>Хранилища, которые не сундуки: бочки и шкафы (в названиях префабов и сундуков).</summary>
        private static readonly string[] s_notChests = { "barrel", "warderobe", "wardrobe" };

        /// <summary>
        /// Хранилище — не сундук (табличка вешается только на сундуки): бочка (ванильная piece_chest_barrel,
        /// $piece_chestbarrel) или шкаф (piece_chest_warderobe, $piece_chestwarderobe), в том числе модовые —
        /// по имени префаба или сундука.
        /// </summary>
        public static bool IsNotChest(string prefabName, string containerName)
        {
            foreach (string word in s_notChests)
            {
                if (Contains(prefabName, word) || Contains(containerName, word))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Contains(string text, string part) =>
            !string.IsNullOrEmpty(text) && text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Помещается ли станок в панель постройки: ресурсы и станок делят одни и те же ячейки (у ванильной панели их 6),
        /// станок выводится в ячейку сразу после последнего ресурса.
        /// </summary>
        public static bool StationFits(int resources, int slots) => resources < slots;

        /// <summary>
        /// Самый длинный ровный участок профиля боковины: подряд идущие замеры глубины (float.NaN — луч не попал), соседние
        /// отличаются не больше чем на maxStep. Ступеньки — плинтус, обод под крышкой — участок разрывают. При равной длине —
        /// тот, что ближе к середине. false — ни одного замера.
        /// </summary>
        public static bool LongestSmoothRun(IList<float> depths, float maxStep, out int from, out int to)
        {
            from = to = -1;
            int bestLen = 0;
            float mid = (depths.Count - 1) * 0.5f;
            int start = -1;
            for (int i = 0; i <= depths.Count; i++)
            {
                bool ok = i < depths.Count && !float.IsNaN(depths[i]);
                bool joins = ok && start >= 0 && Math.Abs(depths[i] - depths[i - 1]) <= maxStep;
                if (ok && start < 0)
                {
                    start = i;
                    continue;
                }
                if (joins)
                {
                    continue;
                }
                if (start >= 0)
                {
                    int end = i - 1, len = end - start + 1;
                    float center = (start + end) * 0.5f;
                    if (len > bestLen || (len == bestLen && Math.Abs(center - mid) < Math.Abs((from + to) * 0.5f - mid)))
                    {
                        bestLen = len;
                        from = start;
                        to = end;
                    }
                }
                start = ok ? i : -1;
            }
            return bestLen > 0;
        }

        /// <summary>
        /// Прямая depth = offset + slope · y по участку [from, to] (наименьшие квадраты), сдвинутая наружу так, чтобы
        /// все замеры были не выше неё: она касается самых выступающих точек — табличка ляжет на них и нигде не утонет.
        /// Наклон ограничен maxSlope (больше — не стенка, а что-то иное).
        /// </summary>
        public static void FitOuterLine(IList<float> ys, IList<float> depths, int from, int to, float maxSlope,
            out float slope, out float offset)
        {
            int n = to - from + 1;
            double sy = 0, sd = 0, syy = 0, syd = 0;
            for (int i = from; i <= to; i++)
            {
                sy += ys[i];
                sd += depths[i];
                syy += ys[i] * ys[i];
                syd += ys[i] * depths[i];
            }
            double den = n * syy - sy * sy;
            slope = n > 1 && Math.Abs(den) > 1e-12 ? (float)((n * syd - sy * sd) / den) : 0f;
            slope = Math.Max(-maxSlope, Math.Min(maxSlope, slope));
            offset = float.NegativeInfinity;
            for (int i = from; i <= to; i++)
            {
                offset = Math.Max(offset, depths[i] - slope * ys[i]);
            }
        }

        /// <summary>
        /// «Предмет:Кол-во, Предмет:Кол-во» → список пар. Количество по умолчанию 1; неверные части пропускаются
        /// (их текст попадает в <paramref name="errors"/>).
        /// </summary>
        public static List<KeyValuePair<string, int>> ParseCost(string cost, List<string> errors = null)
        {
            var result = new List<KeyValuePair<string, int>>();
            if (string.IsNullOrWhiteSpace(cost))
            {
                return result;
            }
            foreach (string part in cost.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split(':');
                string item = kv[0].Trim();
                int amount = 1;
                if (item.Length == 0 || kv.Length > 2 || (kv.Length == 2 && !int.TryParse(kv[1].Trim(), out amount)) || amount <= 0)
                {
                    errors?.Add(part.Trim());
                    continue;
                }
                result.Add(new KeyValuePair<string, int>(item, amount));
            }
            return result;
        }
    }
}
