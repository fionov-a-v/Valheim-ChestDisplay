using System;
using System.Collections.Generic;
using ChestDisplay.Logic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;
using Object = UnityEngine.Object;

namespace ChestDisplay
{
    /// <summary>
    /// Префаб таблички — клон ванильной таблички с надписью (sign): берутся её постройка, прочность, звуки и деревянный
    /// материал, а надпись убирается. Вид: тёмная рамка, светлая доска поверх, иконка предмета на ней и (если включено)
    /// число внизу доски — тогда иконка чуть меньше и выше. Корень префаба — центр доски, лицевая сторона смотрит в +Z;
    /// всё видимое и коллайдер — в дочернем объекте ChestSign.VisualName (его масштабирует размер таблички).
    /// </summary>
    internal static class SignPiece
    {
        public const string PrefabName = "piece_chestdisplay";
        private const string BaseName = "sign";
        /// <summary>Для иконки в меню молота: сундук, к которому прикреплена табличка, и предмет на ней.</summary>
        private const string IconChest = "piece_chest_wood";
        private const string IconItem = "Iron";

        /// <summary>Во сколько раз табличка на иконке крупнее настоящей (иначе в ячейке меню её не разглядеть).</summary>
        private const float IconSignScale = 1.5f;

        /// <summary>Сторона рамки, м (влезает на любую грань ванильных сундуков ниже крышки).</summary>
        public const float BoardSize = 0.34f;

        /// <summary>Сторона светлой доски внутри рамки, м.</summary>
        public const float PanelSize = 0.29f;

        /// <summary>Сторона квадрата, в который вписана иконка, м.</summary>
        public const float IconSize = 0.25f;

        /// <summary>Толщина таблички, м: задняя сторона — z = -Thickness/2, лицевая — +Thickness/2.</summary>
        public const float Thickness = 0.03f;

        private const float FrameDepth = 0.02f;

        /// <summary>
        /// Число внизу доски: высота символа и центр строки, м (по превью tests/sign_preview.py: «99k+» помещается
        /// с запасом, между иконкой и числом ~2 см). С числом иконка уменьшается и поднимается, чтобы не наезжать на него.
        /// </summary>
        public const float NumberHeight = 0.064f;
        public const float NumberCenterY = -PanelSize * 0.5f + 0.012f + NumberHeight * 0.5f;
        public const float IconScaleWithCount = 0.76f;
        public const float IconShiftWithCount = 0.045f;

        /// <summary>Иконка и число лежат чуть впереди лицевой стороны доски (щель против мерцания).</summary>
        public const float FrontZ = Thickness * 0.5f + 0.003f;

        /// <summary>На сколько центр таблички отстоит от поверхности сундука: половина толщины и щель против мерцания.</summary>
        public const float BackOffset = Thickness * 0.5f + 0.002f;

        /// <summary>Ячеек под ресурсы и станок в панели постройки ванильного Hud (m_requirementItems).</summary>
        private const int DefaultHudSlots = 6;

        private static GameObject s_prefab;
        private static string s_stationWarning;

        public static void Register()
        {
            GameObject basePrefab = PrefabManager.Instance.GetPrefab(BaseName);
            if (basePrefab == null || basePrefab.GetComponent<Piece>() == null || basePrefab.GetComponent<WearNTear>() == null)
            {
                Logger.LogError($"Chest Display: ванильная табличка '{BaseName}' не найдена — табличка для сундука не добавлена");
                return;
            }
            GameObject prefab = PrefabManager.Instance.CreateClonedPrefab(PrefabName, basePrefab);

            // Текст ванильной таблички не нужен: её компонент (он же перехватывал бы «Использовать») и холст с надписью.
            Sign sign = prefab.GetComponent<Sign>();
            if (sign != null)
            {
                Object.DestroyImmediate(sign);
            }
            foreach (Canvas canvas in prefab.GetComponentsInChildren<Canvas>(true))
            {
                Object.DestroyImmediate(canvas.gameObject);
            }

            WearNTear wear = prefab.GetComponent<WearNTear>();
            // Сундуки не держат на себе постройки (у них m_supports = false), поэтому табличка не должна
            // разрушаться «без опоры». Её саму тоже нельзя делать опорой: на неё ничего не построить.
            wear.m_noSupportWear = false;
            wear.m_supports = false;
            prefab.GetComponent<Piece>().m_canRotate = false;
            prefab.AddComponent<ChestSign>();

            MeshFilter icon = null;
            MeshRenderer iconRenderer = null;
            try
            {
                BuildVisual(prefab, out icon, out iconRenderer);
            }
            catch (Exception e)
            {
                Logger.LogError($"Chest Display: не удалось собрать вид таблички: {e}");
            }

            List<KeyValuePair<string, int>> cost = Cost();
            var config = new PieceConfig
            {
                Name = "$chestdisplay_piece",
                Description = "$chestdisplay_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Furniture,
                CraftingStation = StationName(cost.Count),
                Enabled = SignConfig.Enabled.Value,
                Icon = RenderIcon(prefab, icon, iconRenderer) ?? basePrefab.GetComponent<Piece>().m_icon,
                Requirements = cost.ConvertAll(r => new RequirementConfig(r.Key, r.Value, 0, true)).ToArray(),
            };
            PieceManager.Instance.AddPiece(new CustomPiece(prefab, false, config));
            s_prefab = prefab;
        }

        /// <summary>
        /// Доска ванильной таблички (куб 1 × 0.5 × 0.1) становится светлой доской, за ней — тёмная рамка из того же куба,
        /// перед ней — иконка и число. Коллайдер — по рамке. Всё это — внутри одного узла (VisualName).
        /// </summary>
        private static void BuildVisual(GameObject prefab, out MeshFilter icon, out MeshRenderer iconRenderer)
        {
            icon = null;
            iconRenderer = null;
            MeshFilter board = null;
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh != null && mf.GetComponent<MeshRenderer>() != null)
                {
                    board = mf;
                    break;
                }
            }
            if (board == null)
            {
                throw new InvalidOperationException("у ванильной таблички нет доски");
            }
            MeshRenderer boardRenderer = board.GetComponent<MeshRenderer>();
            Material wood = boardRenderer.sharedMaterial;
            IconArt.Init(wood);

            Transform panel = board.transform;
            panel.localPosition = Vector3.zero;
            panel.localRotation = Quaternion.identity;
            panel.localScale = new Vector3(PanelSize, PanelSize, Thickness);

            var frame = new GameObject("frame");
            frame.layer = board.gameObject.layer;
            frame.transform.SetParent(panel.parent, false);
            frame.transform.localPosition = new Vector3(0f, 0f, -Thickness * 0.5f + FrameDepth * 0.5f);
            frame.transform.localScale = new Vector3(BoardSize, BoardSize, FrameDepth);
            frame.AddComponent<MeshFilter>().sharedMesh = board.sharedMesh;
            MeshRenderer frameRenderer = frame.AddComponent<MeshRenderer>();
            var darkWood = new Material(wood) { name = "chestdisplay_frame" };
            darkWood.color = wood.color * new Color(0.45f, 0.4f, 0.36f, 1f);
            frameRenderer.sharedMaterial = darkWood;

            // Узел для всего видимого: дочерние объекты корня (коллайдер, «New» с доской и рамкой) переезжают в него.
            var visual = new GameObject(ChestSign.VisualName);
            visual.layer = board.gameObject.layer;
            visual.transform.SetParent(prefab.transform, false);
            var children = new List<Transform>();
            foreach (Transform child in prefab.transform)
            {
                if (child != visual.transform)
                {
                    children.Add(child);
                }
            }
            foreach (Transform child in children)
            {
                child.SetParent(visual.transform, false);
            }

            // Иконка — в узле, а не в «New»: при износе (смене вида постройки) она не должна пропадать.
            var iconGo = new GameObject(ChestSign.IconName);
            iconGo.layer = board.gameObject.layer;
            iconGo.transform.SetParent(visual.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, 0f, FrontZ);
            icon = iconGo.AddComponent<MeshFilter>();
            iconRenderer = iconGo.AddComponent<MeshRenderer>();
            iconRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            iconRenderer.enabled = false;

            foreach (BoxCollider box in prefab.GetComponentsInChildren<BoxCollider>(true))
            {
                box.center = Vector3.zero;
                box.size = new Vector3(BoardSize, BoardSize, Thickness);
            }

            Renderer[] countRenderers = BuildCount(visual.transform, board.gameObject.layer);

            // Рамка, иконка и число видны и исчезают вдали вместе с доской.
            LODGroup lod = prefab.GetComponent<LODGroup>();
            if (lod != null)
            {
                LOD[] lods = lod.GetLODs();
                for (int i = 0; i < lods.Length; i++)
                {
                    if (Array.IndexOf(lods[i].renderers, boardRenderer) < 0)
                    {
                        continue;
                    }
                    var renderers = new List<Renderer>(lods[i].renderers) { frameRenderer, iconRenderer };
                    renderers.AddRange(countRenderers);
                    lods[i].renderers = renderers.ToArray();
                }
                lod.SetLODs(lods);
                lod.RecalculateBounds();
            }
        }

        /// <summary>
        /// Число внизу доски (NumberArt — цифры атласа DigitArt). По умолчанию выключено (объект неактивен) —
        /// включает ChestSign по настройке ShowItemCount.
        /// </summary>
        private static Renderer[] BuildCount(Transform visual, int layer)
        {
            var count = new GameObject(ChestSign.CountName);
            count.layer = layer;
            count.transform.SetParent(visual, false);
            count.transform.localPosition = new Vector3(0f, NumberCenterY, FrontZ);
            count.AddComponent<MeshFilter>();
            MeshRenderer renderer = count.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.enabled = false;
            count.SetActive(false);
            return new Renderer[] { renderer };
        }

        /// <summary>Применить рецепт, станок и «можно строить» из (синхронизированного) конфига к уже созданному префабу.</summary>
        public static void ApplyRecipe()
        {
            Piece piece = s_prefab != null ? s_prefab.GetComponent<Piece>() : null;
            if (piece == null || ObjectDB.instance == null || ObjectDB.instance.m_items.Count == 0)
            {
                return;
            }
            var requirements = new List<Piece.Requirement>();
            foreach (KeyValuePair<string, int> r in Cost())
            {
                GameObject item = ObjectDB.instance.GetItemPrefab(r.Key);
                ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    Logger.LogWarning($"Chest Display: предмет '{r.Key}' из рецепта не найден");
                    continue;
                }
                requirements.Add(new Piece.Requirement { m_resItem = drop, m_amount = r.Value, m_recover = true });
            }
            piece.m_resources = requirements.ToArray();
            piece.m_craftingStation = FindStation(StationName(requirements.Count));
            piece.m_enabled = SignConfig.Enabled.Value;
        }

        private static List<KeyValuePair<string, int>> Cost()
        {
            var errors = new List<string>();
            List<KeyValuePair<string, int>> cost = DisplayRules.ParseCost(SignConfig.Cost.Value, errors);
            foreach (string e in errors)
            {
                Logger.LogWarning($"Chest Display: неверная часть рецепта '{e}' (нужно Предмет:Кол-во)");
            }
            if (cost.Count == 0)
            {
                Logger.LogWarning("Chest Display: рецепт пуст — используется рецепт по умолчанию");
                cost = DisplayRules.ParseCost(SignConfig.DefaultCost);
            }
            return cost;
        }

        /// <summary>
        /// Станок из настроек, если он помещается в панель постройки вместе с ресурсами. В панели молота (Hud) всего
        /// <see cref="HudSlots"/> ячеек на ресурсы и станок вместе; лишний станок игра пытается вывести в несуществующую
        /// ячейку и падает каждый кадр (IndexOutOfRangeException в Hud.SetupPieceInfo) — тогда станок не требуется.
        /// </summary>
        private static string StationName(int resources)
        {
            string station = SignConfig.Station.Value;
            if (string.IsNullOrWhiteSpace(station))
            {
                return "";
            }
            int slots = HudSlots();
            if (!DisplayRules.StationFits(resources, slots))
            {
                string warning = $"{station.Trim()}|{resources}|{slots}";
                if (warning != s_stationWarning)
                {
                    s_stationWarning = warning;
                    Logger.LogWarning($"Chest Display: станок '{station.Trim()}' не помещается в панель постройки рядом с " +
                                      $"{resources} ресурсами (ячеек всего {slots}) — табличка строится без станка");
                }
                return "";
            }
            return station.Trim();
        }

        /// <summary>Ячеек под ресурсы и станок в панели постройки: у ванильного Hud их 6.</summary>
        private static int HudSlots()
        {
            Hud hud = Hud.instance;
            return hud != null && hud.m_requirementItems != null && hud.m_requirementItems.Length > 0
                ? hud.m_requirementItems.Length
                : DefaultHudSlots;
        }

        private static CraftingStation FindStation(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            name = CraftingStations.GetInternalName(name.Trim());
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (go == null)
            {
                go = PrefabManager.Instance.GetPrefab(name);
            }
            CraftingStation station = go != null ? go.GetComponent<CraftingStation>() : null;
            if (station == null)
            {
                Logger.LogWarning($"Chest Display: станок '{name}' не найден");
            }
            return station;
        }

        /// <summary>
        /// Иконка для меню молота: деревянный сундук, на его лицевой стороне — табличка со слитком железа, вид чуть сверху
        /// и сбоку. Табличка на иконке крупнее настоящей, чтобы её было видно в маленькой ячейке меню.
        /// Рисуется временная модель из одних мешей: сам префаб таблички лежит в неактивном контейнере Jötunn,
        /// а RenderManager рисует только активные объекты.
        /// </summary>
        private static Sprite RenderIcon(GameObject prefab, MeshFilter icon, MeshRenderer iconRenderer)
        {
            GameObject model = null;
            try
            {
                GameObject sample = PrefabManager.Instance.GetPrefab(IconItem);
                ItemDrop drop = sample != null ? sample.GetComponent<ItemDrop>() : null;
                Sprite[] icons = drop != null ? drop.m_itemData.m_shared.m_icons : null;
                IconArt.Apply(icon, iconRenderer, icons != null && icons.Length > 0 ? icons[0] : null);

                model = new GameObject("chestdisplay_render");
                model.transform.position = new Vector3(0f, -5000f, 0f);
                GameObject chest = PrefabManager.Instance.GetPrefab(IconChest);
                if (chest != null && PrefabBounds(chest, out Bounds bounds))
                {
                    // Лицевая сторона деревянного сундука (с замком) — его +Z; табличка смотрит туда же.
                    CopyVisuals(chest, model.transform, Vector3.zero, 1f);
                    var front = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + BackOffset * IconSignScale);
                    CopyVisuals(prefab, model.transform, front, IconSignScale);
                }
                else
                {
                    CopyVisuals(prefab, model.transform, Vector3.zero, 1f);
                }
                return RenderManager.Instance.Render(new RenderManager.RenderRequest(model)
                {
                    Width = 128,
                    Height = 128,
                    Rotation = Quaternion.Euler(14f, -28f, 0f),
                    UseCache = false,
                });
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Chest Display: иконка не отрисована, будет иконка ванильной таблички: {e.Message}");
                return null;
            }
            finally
            {
                if (model != null)
                {
                    Object.DestroyImmediate(model);
                }
                IconArt.Apply(icon, iconRenderer, null);
            }
        }

        /// <summary>
        /// Копии видимых мешей префаба (с теми же материалами и положением относительно его корня) внутрь parent:
        /// со сдвигом offset и масштабом scale.
        /// </summary>
        private static void CopyVisuals(GameObject prefab, Transform parent, Vector3 offset, float scale)
        {
            Transform root = prefab.transform;
            foreach (MeshRenderer r in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (!r.enabled || mf == null || mf.sharedMesh == null || !ActiveUnder(r.transform, root))
                {
                    continue;
                }
                var part = new GameObject(r.name);
                part.transform.SetParent(parent, false);
                part.transform.localPosition = offset + root.InverseTransformPoint(r.transform.position) * scale;
                part.transform.localRotation = Quaternion.Inverse(root.rotation) * r.transform.rotation;
                part.transform.localScale = Vector3.Scale(r.transform.lossyScale, Inverse(root.lossyScale)) * scale;
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
            }
        }

        /// <summary>
        /// Рамка твёрдых коллайдеров префаба в координатах его корня. Сам префаб в сцене не активен, поэтому коллайдеры
        /// берутся вместе с неактивными, а выключенные части (снег, открытая крышка) отсекаются по activeSelf.
        /// </summary>
        private static bool PrefabBounds(GameObject prefab, out Bounds bounds)
        {
            bounds = default;
            Transform root = prefab.transform;
            bool any = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            foreach (Collider c in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger || !ActiveUnder(c.transform, root) || !ChestMount.LocalBox(c, out Vector3 center, out Vector3 size))
                {
                    continue;
                }
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        center.x + ((i & 1) == 0 ? -0.5f : 0.5f) * size.x,
                        center.y + ((i & 2) == 0 ? -0.5f : 0.5f) * size.y,
                        center.z + ((i & 4) == 0 ? -0.5f : 0.5f) * size.z);
                    Vector3 p = root.InverseTransformPoint(c.transform.TransformPoint(corner));
                    min = any ? Vector3.Min(min, p) : p;
                    max = any ? Vector3.Max(max, p) : p;
                    any = true;
                }
            }
            if (any)
            {
                bounds.SetMinMax(min, max);
            }
            return any;
        }

        private static Vector3 Inverse(Vector3 v) => new Vector3(1f / v.x, 1f / v.y, 1f / v.z);

        private static bool ActiveUnder(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
            {
                if (!t.gameObject.activeSelf)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
