using System;
using BepInEx.Configuration;

namespace ChestDisplay
{
    /// <summary>
    /// Настройки мода. Рецепт и размер синхронизируются с сервера (Jotunn, IsAdminOnly), поэтому у всех игроков на сервере
    /// табличка одинаковая; показ количества — у каждого игрока свой.
    /// </summary>
    internal static class SignConfig
    {
        public const string DefaultCost = "GreydwarfEye:1,Wood:2,Coal:2,Resin:2";

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<string> Cost;
        public static ConfigEntry<string> Station;

        public static ConfigEntry<int> SizePercent;
        public static ConfigEntry<bool> ShowCount;

        /// <summary>Поменялись рецепт, станок или «можно строить» — надо переприменить к префабу.</summary>
        public static event Action RecipeChanged;

        /// <summary>Поменялся вид табличек (размер, показ количества) — пересчитать уже висящие.</summary>
        public static event Action LookChanged;

        public static void Bind(ConfigFile cfg)
        {
            const string recipe = "1 - Recipe";
            Enabled = Synced(cfg, recipe, "Enabled", true,
                Text("Whether the chest sign can be built.", "Можно ли строить табличку для сундука."));
            Cost = Synced(cfg, recipe, "Cost", DefaultCost,
                Text("What the chest sign costs (Item:Amount separated by commas).",
                    "Сколько стоит табличка (Предмет:Кол-во через запятую)."));
            Station = Synced(cfg, recipe, "CraftingStation", "piece_workbench",
                Text("Crafting station the sign is built next to (piece_workbench, forge, ...); empty — none needed. " +
                     "The build panel has 6 slots for ingredients and the station together, so a station works only with 5 or fewer ingredients.",
                    "Станок, рядом с которым строится табличка (piece_workbench, forge, ...); пусто — станок не нужен. " +
                    "В панели постройки 6 ячеек на ресурсы и станок вместе, поэтому станок работает только при 5 ресурсах и меньше."));
            Enabled.SettingChanged += (_, _) => RecipeChanged?.Invoke();
            Cost.SettingChanged += (_, _) => RecipeChanged?.Invoke();
            Station.SettingChanged += (_, _) => RecipeChanged?.Invoke();

            const string sign = "2 - Sign";
            SizePercent = Synced(cfg, sign, "SizePercent", 50,
                Text("Size of the sign, %: 0 — the standard size (34 cm), 100 — as large as the surface it hangs on allows " +
                     "(the smaller side of the chest side below the lid, or of the lid top); the sign stays square.",
                    "Размер таблички, %: 0 — стандартный (34 см), 100 — сколько позволяет поверхность, на которой она висит " +
                    "(меньшая сторона боковины ниже крышки или верха крышки); табличка остаётся квадратной."),
                new AcceptableValueRange<int>(0, 100));
            SizePercent.SettingChanged += (_, _) => LookChanged?.Invoke();

            const string client = "3 - Client";
            ShowCount = cfg.Bind(client, "ShowItemCount", false,
                Text("Show at the bottom of the sign how many of its item the chest holds (all stacks together; " +
                     "large numbers are shortened: 1.2k, 12k, 99k+); this client only.",
                    "Показывать внизу таблички, сколько её предмета в сундуке (все стопки вместе; большие числа " +
                    "сокращаются: 1.2k, 12k, 99k+); только для этого клиента."));
            ShowCount.SettingChanged += (_, _) => LookChanged?.Invoke();
        }

        /// <summary>Описание настройки на двух языках: в .cfg — строка по-английски и под ней по-русски.</summary>
        private static string Text(string en, string ru) => en + "\n" + ru;

        private static ConfigEntry<T> Synced<T>(ConfigFile cfg, string section, string key, T value, string description,
            AcceptableValueBase range = null)
        {
            var attributes = new ConfigurationManagerAttributes { IsAdminOnly = true };
            return cfg.Bind(section, key, value, new ConfigDescription(description, range, attributes));
        }
    }
}
