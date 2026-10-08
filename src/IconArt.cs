using System.Collections.Generic;
using ChestDisplay.Logic;
using ChestDisplay.Visuals;
using UnityEngine;
using UnityEngine.Rendering;
using Logger = Jotunn.Logger;

namespace ChestDisplay
{
    /// <summary>
    /// Иконка предмета на табличке. Меш строится из геометрии самого спрайта (вершины и UV атласа — работает и для упакованных
    /// в атлас иконок), материал — на шейдере построек Custom/Piece с вырезом по альфе: иконка освещается как доска под ней
    /// (ночью темнеет, у огня светлеет), а не светится сама. Меши и материалы общие для всех табличек с той же иконкой.
    /// Для опустевшего сундука — тусклый вариант: та же текстура, выцветшая (IconDim) в копии на процессоре.
    /// </summary>
    internal static class IconArt
    {
        private static readonly Dictionary<Sprite, Mesh> s_meshes = new Dictionary<Sprite, Mesh>();
        private static readonly Dictionary<Texture, Material> s_materials = new Dictionary<Texture, Material>();
        private static readonly Dictionary<Texture, Material> s_dimMaterials = new Dictionary<Texture, Material>();
        private static Shader s_shader;
        private static bool s_lit;

        /// <summary>Шейдер берётся у материала доски (это Custom/Piece), запасной — встроенный Sprites/Default.</summary>
        public static void Init(Material boardMaterial)
        {
            s_shader = boardMaterial != null ? boardMaterial.shader : null;
            if (s_shader == null || s_shader.name != "Custom/Piece")
            {
                s_shader = Shader.Find("Custom/Piece");
            }
            s_lit = s_shader != null;
            if (!s_lit)
            {
                s_shader = Shader.Find("Sprites/Default");
                Logger.LogWarning("Chest Display: шейдер Custom/Piece не найден — иконки будут без освещения");
            }
        }

        /// <summary>Показать иконку (или ничего, если null) на рендерере таблички; dim — тусклую.</summary>
        public static void Apply(MeshFilter filter, MeshRenderer renderer, Sprite icon, bool dim = false)
        {
            if (filter == null || renderer == null)
            {
                return;
            }
            Mesh mesh = icon != null ? MeshFor(icon) : null;
            Material material = mesh == null ? null
                : (dim ? DimMaterialFor(icon.texture) : null) ?? MaterialFor(icon.texture);
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

        private static Mesh MeshFor(Sprite icon)
        {
            if (s_meshes.TryGetValue(icon, out Mesh cached) && cached != null)
            {
                return cached;
            }
            Mesh mesh = null;
            try
            {
                mesh = Build(icon);
            }
            catch (System.Exception e)
            {
                Logger.LogWarning($"Chest Display: не удалось построить иконку '{icon.name}': {e.Message}");
            }
            s_meshes[icon] = mesh;
            return mesh;
        }

        /// <summary>
        /// Меш спрайта в плоскости XY лицом к +Z (туда смотрит лицевая сторона таблички), вписанный в квадрат иконки.
        /// Спрайт в Unity смотрит на -Z, поэтому X зеркалится: так картинка не перевёрнута и треугольники видны спереди.
        /// </summary>
        private static Mesh Build(Sprite icon)
        {
            if (icon.texture == null)
            {
                return null;
            }
            Vector2[] v = icon.vertices;
            Vector2[] uv = icon.uv;
            ushort[] tri = icon.triangles;
            if (v == null || uv == null || tri == null || v.Length == 0 || tri.Length < 3)
            {
                return null;
            }
            Rect rect = icon.rect;
            IconFit fit = DisplayRules.Fit(rect.width, rect.height, icon.pivot.x, icon.pivot.y, icon.pixelsPerUnit, SignPiece.IconSize);

            var vertices = new Vector3[v.Length];
            var normals = new Vector3[v.Length];
            var tangents = new Vector4[v.Length];
            var colors = new Color32[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                vertices[i] = new Vector3(-(v[i].x - fit.CenterX) * fit.Scale, (v[i].y - fit.CenterY) * fit.Scale, 0f);
                normals[i] = Vector3.forward;
                // u растёт к -X, v — к +Y: касательная -X, бинормаль cross(N, T) * w = +Y при w = -1.
                tangents[i] = new Vector4(-1f, 0f, 0f, -1f);
                colors[i] = new Color32(255, 255, 255, 255);
            }
            var triangles = new int[tri.Length];
            for (int i = 0; i < tri.Length; i++)
            {
                triangles[i] = tri[i];
            }
            var mesh = new Mesh { name = "chestdisplay_icon_" + icon.name };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Материал для иконки или цифр: шейдер построек с вырезом по альфе (общий на текстуру).</summary>
        public static Material MaterialFor(Texture texture)
        {
            if (texture == null || s_shader == null)
            {
                return null;
            }
            if (s_materials.TryGetValue(texture, out Material cached) && cached != null)
            {
                return cached;
            }
            var m = new Material(s_shader) { name = "chestdisplay_icon_" + texture.name, mainTexture = texture };
            m.mainTextureScale = Vector2.one;
            m.mainTextureOffset = Vector2.zero;
            if (s_lit)
            {
                // Вырез по альфе: прозрачные поля иконки не рисуются.
                SetFloat(m, "_Cutoff", 0.5f);
                // Матовая краска на дереве: без блеска, металла, шума, снега и мокрости.
                SetFloat(m, "_Glossiness", 0.08f);
                SetFloat(m, "_Metallic", 0f);
                SetFloat(m, "_ValueNoise", 0f);
                SetFloat(m, "_TriplanarMap", 0f);
                SetFloat(m, "_AddRain", 0f);
                if (m.HasProperty("_AddSnow"))
                {
                    m.SetInteger("_AddSnow", 0);
                }
                if (m.HasProperty("_EmissionColor"))
                {
                    m.SetColor("_EmissionColor", Color.black);
                }
                m.color = Color.white;
            }
            else
            {
                m.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            }
            s_materials[texture] = m;
            return m;
        }

        /// <summary>
        /// Тусклый материал: выцветшая копия текстуры иконок (весь атлас целиком — UV спрайтов те же) с тем же шейдером.
        /// Текстуры иконок игры не читаются с процессора, поэтому копия снимается с видеокарты: Blit в RenderTexture
        /// и ReadPixels. Нет видеокарты (выделенный сервер) или копия не удалась — null, будет обычная иконка.
        /// </summary>
        private static Material DimMaterialFor(Texture texture)
        {
            if (texture == null)
            {
                return null;
            }
            if (s_dimMaterials.TryGetValue(texture, out Material cached))
            {
                return cached;
            }
            Material m = null;
            try
            {
                Texture2D dim = DimTexture(texture);
                m = dim != null ? MaterialFor(dim) : null;
            }
            catch (System.Exception e)
            {
                Logger.LogWarning($"Chest Display: не удалось сделать тусклую иконку из '{texture.name}': {e.Message}");
            }
            s_dimMaterials[texture] = m;
            return m;
        }

        private static Texture2D DimTexture(Texture source)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return null;
            }
            int w = source.width, h = source.height;
            // Default (sRGB) туда и обратно: байты копии — те же, что в исходной текстуре.
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            RenderTexture previous = RenderTexture.active;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = source.name + "_chestdisplay_dim",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = source.filterMode,
                anisoLevel = source.anisoLevel,
            };
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                copy.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
            Color32[] px = copy.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                IconDim.Dim(ref px[i].r, ref px[i].g, ref px[i].b);
            }
            copy.SetPixels32(px);
            copy.Apply(true, true);
            return copy;
        }

        private static void SetFloat(Material m, string name, float value)
        {
            if (m.HasProperty(name))
            {
                m.SetFloat(name, value);
            }
        }
    }
}
