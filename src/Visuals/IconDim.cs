namespace ChestDisplay.Visuals
{
    /// <summary>
    /// Тусклая иконка — для опустевшего сундука: табличка помнит, что в нём лежало, и показывает это выцветшим.
    /// Цвет каждого пикселя уводится к серому, потом к цвету доски, и немного темнеет: так «гаснут» и яркие иконки
    /// (простое затемнение оставляет тёмные — уголь, железо — почти как есть). Альфа не меняется. Параметры подобраны
    /// на превью (tests/sign_preview.py): предмет узнаётся, но сразу видно, что его нет.
    /// </summary>
    public static class IconDim
    {
        /// <summary>Доля обесцвечивания (0 — цвет как есть, 1 — серый).</summary>
        public const float Desaturate = 0.75f;

        /// <summary>Доля смешения с цветом доски.</summary>
        public const float Fade = 0.5f;

        /// <summary>Затемнение после смешения.</summary>
        public const float Darken = 0.8f;

        /// <summary>Цвет доски в текстуре (sRGB, 0..255): средний цвет Planks5c × оттенок материала 0.83.</summary>
        public static readonly byte[] Wood = { 74, 56, 34 };

        /// <summary>Выцветить один пиксель (sRGB, как хранятся в текстуре).</summary>
        public static void Dim(ref byte r, ref byte g, ref byte b)
        {
            float l = 0.299f * r + 0.587f * g + 0.114f * b;
            r = One(r, l, Wood[0]);
            g = One(g, l, Wood[1]);
            b = One(b, l, Wood[2]);
        }

        private static byte One(byte c, float gray, byte wood)
        {
            float v = c + (gray - c) * Desaturate;
            v = v + (wood - v) * Fade;
            v *= Darken;
            int i = (int)(v + 0.5f);
            return (byte)(i < 0 ? 0 : (i > 255 ? 255 : i));
        }
    }
}
