using System.Collections.Generic;
using ChestDisplay.Visuals;
using UnityEngine;

namespace ChestDisplay
{
    /// <summary>
    /// Число на табличке: строка из клеток атласа DigitArt — по квадрату на символ, в плоскости таблички лицом к +Z,
    /// по центру строки. Материал — как у иконки (шейдер построек с вырезом по альфе), поэтому цифры освещаются как доска.
    /// Меши кешируются по тексту числа.
    /// </summary>
    internal static class NumberArt
    {
        /// <summary>Высота символа в атласе, пикселей (на табличке символ ~6 см — с запасом).</summary>
        private const int Unit = 96;

        private static readonly Dictionary<string, Mesh> s_meshes = new Dictionary<string, Mesh>();
        private static Material s_material;

        /// <summary>Показать число (или ничего, если текст пуст) на рендерере.</summary>
        public static void Apply(MeshFilter filter, MeshRenderer renderer, string text)
        {
            if (filter == null || renderer == null)
            {
                return;
            }
            Mesh mesh = string.IsNullOrEmpty(text) ? null : MeshFor(text);
            Material material = mesh != null ? Material() : null;
            if (mesh == null || material == null)
            {
                renderer.enabled = false;
                filter.sharedMesh = null;
                return;
            }
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.enabled = true;
        }

        private static Material Material()
        {
            if (s_material != null)
            {
                return s_material;
            }
            byte[] rgba = DigitArt.Atlas(Unit, out int w, out int h);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = "chestdisplay_digits",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            // Не LoadRawTextureData: при текстуре с mip-уровнями он ждёт данные сразу для всех уровней.
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            s_material = IconArt.MaterialFor(tex);
            return s_material;
        }

        /// <summary>
        /// Квадраты символов высотой SignPiece.NumberHeight, строка по центру. Как и у иконки, X зеркалится: табличку
        /// смотрят со стороны +Z, и правая для смотрящего сторона — это -X.
        /// </summary>
        private static Mesh MeshFor(string text)
        {
            if (s_meshes.TryGetValue(text, out Mesh cached) && cached != null)
            {
                return cached;
            }
            List<GlyphPlace> places = DigitArt.Layout(text, out float total);
            if (places.Count == 0)
            {
                return null;
            }
            float h = SignPiece.NumberHeight;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            foreach (GlyphPlace p in places)
            {
                DigitArt.AtlasCell(p.Glyph, out float u0, out float u1);
                // Клетка в долях высоты: от X - Pad до X + Width + Pad, по высоте от -Pad до 1 + Pad; центр строки — 0.
                float left = (p.X - DigitArt.Pad - total * 0.5f) * h;
                float right = (p.X + p.Width + DigitArt.Pad - total * 0.5f) * h;
                float bottom = (-DigitArt.Pad - 0.5f) * h;
                float top = (1f + DigitArt.Pad - 0.5f) * h;
                int b = vertices.Count;
                // Углы с точки зрения смотрящего: левый низ, левый верх, правый верх, правый низ (x смотрящего = -X).
                vertices.Add(new Vector3(-left, bottom, 0f));
                vertices.Add(new Vector3(-left, top, 0f));
                vertices.Add(new Vector3(-right, top, 0f));
                vertices.Add(new Vector3(-right, bottom, 0f));
                uv.Add(new Vector2(u0, 0f));
                uv.Add(new Vector2(u0, 1f));
                uv.Add(new Vector2(u1, 1f));
                uv.Add(new Vector2(u1, 0f));
                // По часовой стрелке для смотрящего — лицевая сторона.
                triangles.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            var normals = new Vector3[vertices.Count];
            var tangents = new Vector4[vertices.Count];
            var colors = new Color32[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                normals[i] = Vector3.forward;
                tangents[i] = new Vector4(-1f, 0f, 0f, -1f);
                colors[i] = new Color32(255, 255, 255, 255);
            }
            var mesh = new Mesh { name = "chestdisplay_number_" + text };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.colors32 = colors;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            s_meshes[text] = mesh;
            return mesh;
        }
    }
}
