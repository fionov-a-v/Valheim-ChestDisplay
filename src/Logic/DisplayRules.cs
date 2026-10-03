using System;
using System.Collections.Generic;

namespace ChestDisplay.Logic
{
    /// <summary>Боковая грань сундука в его собственных координатах.</summary>
    public enum Face
    {
        PosX = 0,
        NegX = 1,
        PosZ = 2,
        NegZ = 3,
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
        /// На какую боковую грань вешать табличку. По нормали поверхности, куда смотрит игрок (в координатах сундука);
        /// если он смотрит на крышку или дно (нормаль почти вертикальна) — на грань, обращённую к игроку
        /// (vx, vz — направление от центра сундука к игроку).
        /// </summary>
        public static Face PickFace(float nx, float nz, float vx, float vz)
        {
            float x = nx, z = nz;
            if (x * x + z * z < 0.25f)
            {
                x = vx;
                z = vz;
            }
            if (Math.Abs(x) >= Math.Abs(z))
            {
                return x >= 0f ? Face.PosX : Face.NegX;
            }
            return z >= 0f ? Face.PosZ : Face.NegZ;
        }

        /// <summary>Наружная нормаль грани: (x, z).</summary>
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
