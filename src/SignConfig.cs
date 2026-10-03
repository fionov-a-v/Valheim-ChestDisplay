using System;
using BepInEx.Configuration;

namespace ChestDisplay
{
    /// <summary>
    /// Настройки мода. Все синхронизируются с сервера (Jotunn, IsAdminOnly), поэтому у всех игроков на сервере
    /// табличка стоит одинаково.
    /// </summary>
    internal static class SignConfig
    {
        public const string DefaultCost = "GreydwarfEye:1,Wood:2,Coal:2,Resin:2";

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<string> Cost;
        public static ConfigEntry<string> Station;

        /// <summary>Поменялись рецепт, станок или «можно строить» — надо переприменить к префабу.</summary>
        public static event Action RecipeChanged;

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
        }

        /// <summary>Описание настройки на двух языках: в .cfg — строка по-английски и под ней по-русски.</summary>
        private static string Text(string en, string ru) => en + "\n" + ru;

        private static ConfigEntry<T> Synced<T>(ConfigFile cfg, string section, string key, T value, string description)
        {
            var attributes = new ConfigurationManagerAttributes { IsAdminOnly = true };
            return cfg.Bind(section, key, value, new ConfigDescription(description, null, attributes));
        }
    }
}
