using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;

namespace ChestDisplay
{
    internal static class SignLocalization
    {
        public static void Register()
        {
            CustomLocalization loc = LocalizationManager.Instance.GetLocalization();

            loc.AddTranslation("English", new Dictionary<string, string>
            {
                { "chestdisplay_piece", "Chest Sign" },
                { "chestdisplay_desc", "A small wooden plaque. Fix it to the side of a shared chest and it shows the icon of the " +
                                       "first item inside (the top-left occupied slot). Using the sign opens the chest." },
                { "chestdisplay_hover", "Sign: {0}" },

                { "chestdisplay_msg_nochest", "The sign can only be fixed to a chest" },
                { "chestdisplay_msg_private", "A personal chest cannot carry a sign" },
                { "chestdisplay_msg_occupied", "This side of the chest already has a sign" },
            });

            loc.AddTranslation("Russian", new Dictionary<string, string>
            {
                { "chestdisplay_piece", "Табличка для сундука" },
                { "chestdisplay_desc", "Небольшая деревянная табличка. Крепится сбоку к общему сундуку и показывает иконку " +
                                       "первого предмета в нём (верхняя левая занятая ячейка). Действие на табличке открывает сундук." },
                { "chestdisplay_hover", "Табличка: {0}" },

                { "chestdisplay_msg_nochest", "Табличку можно прикрепить только к сундуку" },
                { "chestdisplay_msg_private", "На личный сундук табличку не повесить" },
                { "chestdisplay_msg_occupied", "С этой стороны сундука табличка уже есть" },
            });
        }

        /// <summary>Перевести токен ($chestdisplay_...) и подставить значения.</summary>
        public static string Format(string token, params object[] args)
        {
            string text = Localization.instance != null ? Localization.instance.Localize(token) : token;
            return args.Length == 0 ? text : string.Format(text, args);
        }
    }
}
