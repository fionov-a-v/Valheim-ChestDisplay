using System;
using System.Collections.Generic;
using ChestDisplay.Logic;

namespace ChestDisplay.Tests
{
    /// <summary>Проверки правил таблички (dotnet run --project tests/LogicTests).</summary>
    internal static class Program
    {
        private static int s_failed;
        private static int s_passed;

        private static int Main()
        {
            FirstSlot();
            Faces();
            Barrels();
            Fitting();
            Parsing();
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
            Check("нормаль +X → грань +X", DisplayRules.PickFace(1f, 0f, -1f, 0f) == Face.PosX);
            Check("нормаль -Z → грань -Z", DisplayRules.PickFace(0f, -1f, 1f, 0f) == Face.NegZ);
            Check("наклонная нормаль — по большей составляющей", DisplayRules.PickFace(0.3f, 0.8f, 0f, 0f) == Face.PosZ);
            Check("смотрит на крышку → грань к игроку", DisplayRules.PickFace(0.05f, 0.02f, -2f, 0.5f) == Face.NegX);
            Check("на крышку, игрок спереди-справа → +Z", DisplayRules.PickFace(0f, 0f, 0.4f, 3f) == Face.PosZ);
            Check("скос крышки (нормаль чуть вбок) → всё ещё к игроку", DisplayRules.PickFace(0.45f, 0f, 0f, -1f) == Face.NegZ);
            Check("заметно вбок — уже по нормали", DisplayRules.PickFace(0.6f, 0f, 0f, -1f) == Face.PosX);

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
