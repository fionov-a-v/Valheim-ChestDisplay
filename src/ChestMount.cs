using System.Collections.Generic;
using ChestDisplay.Logic;
using UnityEngine;

namespace ChestDisplay
{
    /// <summary>
    /// К каким сундукам крепится табличка и куда именно: по центру боковой грани, обращённой к игроку (ниже крышки),
    /// или сверху на крышку — вплотную к поверхности (с учётом выступов — оковки).
    /// </summary>
    internal static class ChestMount
    {
        public enum Problem
        {
            None,
            NotChest,
            Private,
            Occupied,
        }

        /// <summary>Откуда (вперёд от таблички, м) пускается луч, ищущий сундук за ней.</summary>
        private const float BehindStart = 0.4f;

        /// <summary>Таблички ближе этого к месту новой — место занято.</summary>
        private const float OccupiedDistance = 0.12f;

        private static int s_rayMask;
        private static readonly List<Collider> s_colliders = new List<Collider>();

        /// <summary>
        /// Видимые меши тела сундука (без крышки, снега и чужих украшений) — для рамки граней и лучей. Нужны, потому что
        /// у некоторых сундуков коллайдер заметно меньше модели: у каменного сундука Пепельных земель (piece_chest_grausten)
        /// это коробка 80 % размера — по одному коллайдеру табличка на боках уходила в камень на 20 см.
        /// </summary>
        private static readonly List<MeshFilter> s_bodyMeshes = new List<MeshFilter>();

        /// <summary>Треугольники меша для лучей (кеш по мешу; нечитаемый меш — только его рамка).</summary>
        private sealed class MeshData
        {
            public Vector3[] Vertices;
            public int[] Triangles;
        }

        private static readonly Dictionary<Mesh, MeshData> s_meshData = new Dictionary<Mesh, MeshData>();
        private static readonly RaycastHit[] s_hits = new RaycastHit[16];

        private static int RayMask => s_rayMask != 0
            ? s_rayMask
            : s_rayMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "vehicle");

        /// <summary>
        /// Можно ли повесить табличку на этот сундук: общий (не личный), построенный игроком как отдельная постройка,
        /// не бочка и не шкаф, не станок с хранилищем, не тележка и не корабль (табличка не ездит вместе с ними).
        /// </summary>
        public static Problem Check(Container chest)
        {
            if (chest == null)
            {
                return Problem.NotChest;
            }
            if (chest.m_privacy != Container.PrivacySetting.Public)
            {
                return Problem.Private;
            }
            ZNetView nview = chest.GetComponentInParent<ZNetView>();
            Piece piece = chest.GetComponentInParent<Piece>();
            if (piece == null || nview == null || !nview.IsValid() || chest.m_wagon != null ||
                chest.GetComponentInParent<Ship>() != null || DisplayRules.IsNotChest(piece.gameObject.name, chest.m_name) ||
                IsStation(piece, chest))
            {
                return Problem.NotChest;
            }
            Rigidbody body = chest.GetComponentInParent<Rigidbody>();
            return body != null && !body.isKinematic ? Problem.NotChest : Problem.None;
        }

        /// <summary>
        /// Станок со встроенным хранилищем, а не сундук: кроме Container у постройки есть ещё что-то, на что наводятся или
        /// с чем взаимодействуют (бродильня и плавильня BetterStations, стол зачарования EpicLoot и т.п.). У ванильных
        /// и обычных модовых сундуков такого нет — только сам Container.
        /// </summary>
        private static bool IsStation(Piece piece, Container chest)
        {
            foreach (MonoBehaviour mb in piece.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb != chest && !(mb is ChestSign) && (mb is Hoverable || mb is Interactable))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Сундук, в который смотрит игрок: сама постройка или сундук, на котором висит табличка.</summary>
        public static Container ChestOf(Piece piece)
        {
            if (piece == null)
            {
                return null;
            }
            ChestSign sign = piece.GetComponent<ChestSign>();
            if (sign != null)
            {
                return sign.Chest;
            }
            // У ванильных сундуков Container на корне постройки, у некоторых модовых — на дочернем объекте.
            Container chest = piece.GetComponent<Container>();
            return chest != null ? chest : piece.GetComponentInChildren<Container>();
        }

        /// <summary>Куда встаёт табличка на сундуке.</summary>
        public struct Mount
        {
            public Face Face;

            /// <summary>Центр корня таблички (её задняя сторона прижата к сундуку), мир.</summary>
            public Vector3 Position;

            /// <summary>Лицевая сторона (+Z) — наружу от сундука; у таблички на крышке верх иконки — от игрока.</summary>
            public Quaternion Rotation;

            /// <summary>Меньшая сторона поверхности под табличкой, м (боковина ниже крышки или верх крышки).</summary>
            public float SurfaceMin;
        }

        /// <summary>Поверхность для таблички в координатах корня сундука.</summary>
        private struct Surface
        {
            public Vector3 Center;
            public Vector3 Outward;
            public float Width;
            public float Height;

            /// <summary>Поверхность — верх крышки (у крышки нет коллайдера, её высоту даёт меш).</summary>
            public bool FromLid;

            /// <summary>
            /// Боковина: где поверхность относительно плоскости грани в центре (Center), м (минус — внутрь), и как она
            /// уходит внутрь с высотой (наклон глубины по y). Уже найдены по профилю — лучи для неё не нужны.
            /// </summary>
            public bool Profiled;
            public float Depth;
            public float Slope;

            /// <summary>На сколько коллайдер сундука выступает перед видимой стенкой (у каменного сундука спереди — 12 см).</summary>
            public float ColliderGap;
        }

        /// <summary>Боковины по типу сундука и грани (профиль считается лучами один раз — от позиции он не зависит).</summary>
        private static readonly Dictionary<string, Surface> s_sides = new Dictionary<string, Surface>();

        /// <summary>Шаг профиля по высоте, м; скачок больше MaxStep между соседними замерами — ступенька.</summary>
        private const float ProfileStep = 0.02f;
        private const float MaxStep = 0.035f;

        /// <summary>Наибольший наклон стенки, под который подстраивается табличка (глубина на метр высоты, ~35°).</summary>
        private const float MaxSlope = 0.7f;

        /// <summary>
        /// Доля меньшей стороны поверхности, которую табличка занимает при 100 %, чтобы рамка не свисала с кромки:
        /// на боковине почти вся, на крышке — 90 % (крышки скруглены и сужаются к верху, на 98 % табличка торчала).
        /// </summary>
        private const float SideFill = 0.98f;
        private const float TopFill = 0.90f;

        private static float Fill(Face face) => face == Face.Top ? TopFill : SideFill;

        /// <summary>
        /// Где встанет табличка: на грань, куда смотрит игрок (боковина — по центру её части ниже крышки, крышка — по центру
        /// сверху), вплотную к поверхности, размером по настройке SizePercent.
        /// </summary>
        public static bool TryMount(Container chest, Vector3 hitNormal, Vector3 viewer, out Mount mount)
        {
            mount = default;
            Transform root = PieceRoot(chest);
            if (!BodyBounds(chest, root, out Bounds bounds))
            {
                return false;
            }
            Vector3 n = root.InverseTransformDirection(hitNormal);
            Vector3 toViewer = root.InverseTransformPoint(viewer) - bounds.center;
            Face face = DisplayRules.PickFace(n.x, n.y, n.z, toViewer.x, toViewer.z);
            Surface surface = SurfaceOf(chest, root, bounds, face);

            mount.Face = face;
            mount.SurfaceMin = Mathf.Min(surface.Width, surface.Height) * Fill(face);
            float size = SignSize(mount.SurfaceMin);

            if (face != Face.Top)
            {
                Pose(root, surface, size, out mount.Position, out mount.Rotation);
                return true;
            }
            Vector3 outward = root.TransformDirection(surface.Outward).normalized;
            DisplayRules.FaceNormal(DisplayRules.TopUp(toViewer.x, toViewer.z), out float ux, out float uz);
            Vector3 up = root.TransformDirection(new Vector3(ux, 0f, uz)).normalized;
            Vector3 center = root.TransformPoint(surface.Center);
            float depth = surface.FromLid ? 0f : Depth(center, outward, up, size);
            mount.Position = center + outward * (depth + SignPiece.BackOffset);
            mount.Rotation = Quaternion.LookRotation(outward, up);
            return true;
        }

        /// <summary>
        /// Табличка на боковине: на найденной по профилю поверхности (Depth от плоскости грани), наклонённая вдоль стенки
        /// (Slope); без профиля — по лучам в центр и углы.
        /// </summary>
        private static void Pose(Transform root, Surface surface, float size, out Vector3 position, out Quaternion rotation)
        {
            Vector3 outward = root.TransformDirection(surface.Outward).normalized;
            Vector3 up = root.up;
            Vector3 center = root.TransformPoint(surface.Center);
            if (!surface.Profiled)
            {
                position = center + outward * (Depth(center, outward, up, size) + SignPiece.BackOffset);
                rotation = Quaternion.LookRotation(outward, up);
                return;
            }
            // Стенка уходит внутрь с высотой на Slope: касательная вверх — up + outward·Slope, нормаль — outward − up·Slope.
            Vector3 normal = (outward - up * surface.Slope).normalized;
            Vector3 along = (up + outward * surface.Slope).normalized;
            position = center + outward * surface.Depth + normal * SignPiece.BackOffset;
            rotation = Quaternion.LookRotation(normal, along);
        }

        /// <summary>
        /// Для висящей таблички: на какой грани сундука она висит (по тому, куда смотрит её лицевая сторона) и меньшая
        /// сторона этой поверхности — от неё считается размер.
        /// </summary>
        public static bool TryGetSurface(Container chest, Transform sign, out Face face, out float surfaceMin)
        {
            face = Face.PosZ;
            surfaceMin = 0f;
            Transform root = PieceRoot(chest);
            if (!BodyBounds(chest, root, out Bounds bounds))
            {
                return false;
            }
            Vector3 f = root.InverseTransformDirection(sign.forward);
            face = DisplayRules.PickFace(f.x, f.y, f.z, f.x, f.z);
            Surface surface = SurfaceOf(chest, root, bounds, face);
            surfaceMin = Mathf.Min(surface.Width, surface.Height) * Fill(face);
            return true;
        }

        /// <summary>
        /// Правильное место корня таблички на боковой грани face при размере size — для уже висящей таблички: её вид
        /// подтягивается сюда (таблички, поставленные до исправления, утоплены в модель или висят выше центра).
        /// </summary>
        public static bool TryIdealSidePose(Container chest, Face face, float size, out Vector3 position, out Quaternion rotation,
            out float colliderGap)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            colliderGap = 0f;
            Transform root = PieceRoot(chest);
            if (face == Face.Top || !BodyBounds(chest, root, out Bounds bounds))
            {
                return false;
            }
            Surface surface = SurfaceOf(chest, root, bounds, face);
            Pose(root, surface, size, out position, out rotation);
            colliderGap = surface.ColliderGap;
            return true;
        }

        /// <summary>Сторона таблички по настройке SizePercent для поверхности с меньшей стороной surfaceMin.</summary>
        public static float SignSize(float surfaceMin) =>
            DisplayRules.SignSize(SignPiece.BoardSize, surfaceMin, SignConfig.SizePercent.Value);

        /// <summary>
        /// Поверхность грани. Боковина — от низа сундука до низа крышки (на крышке табличка мешала бы её открывать),
        /// во всю ширину грани. Верх — верх закрытой крышки (по её мешу; если крышки нет — верх коллайдеров).
        /// </summary>
        private static Surface SurfaceOf(Container chest, Transform root, Bounds bounds, Face face)
        {
            bool lid = LidBounds(chest, root, bounds, out Bounds lidBounds);
            if (face == Face.Top)
            {
                Bounds top = lid ? lidBounds : bounds;
                return new Surface
                {
                    Center = new Vector3(top.center.x, top.max.y, top.center.z),
                    Outward = Vector3.up,
                    Width = top.size.x,
                    Height = top.size.z,
                    FromLid = lid,
                };
            }
            string key = root.name.Replace("(Clone)", "").Trim() + "|" + (int)face;
            if (s_sides.TryGetValue(key, out Surface cached))
            {
                return cached;
            }
            DisplayRules.FaceNormal(face, out float fx, out float fz);
            var outward = new Vector3(fx, 0f, fz);
            float bottom = bounds.min.y;
            float topY = lid ? Mathf.Min(lidBounds.min.y, bounds.max.y) : bounds.max.y;
            Vector3 center = bounds.center + Vector3.Scale(outward, bounds.extents);
            center.y = (bottom + topY) * 0.5f;
            var surface = new Surface
            {
                Center = center,
                Outward = outward,
                Width = fx != 0f ? bounds.size.z : bounds.size.x,
                Height = topY - bottom,
            };
            Profile(root, ref surface, bottom, topY);
            s_sides[key] = surface;
            return surface;
        }

        /// <summary>
        /// Профиль боковины: лучи снаружи внутрь через каждые ProfileStep по высоте (по центру грани и в двух полосах
        /// по бокам — берётся самое выступающее), по коллайдерам и видимым мешам. Самый длинный ровный участок — это
        /// стенка, на которую встанет табличка: плинтус каменного сундука и обод под крышкой его разрывают. По участку —
        /// прямая через самые выступающие точки: наклонные бока деревянного и армированного сундуков табличка повторяет.
        /// Не нашлось ничего — поверхность остаётся прежней (по лучам при установке).
        /// </summary>
        private static void Profile(Transform root, ref Surface surface, float bottom, float top)
        {
            Vector3 outward = surface.Outward;
            Vector3 across = Vector3.Cross(Vector3.up, outward);
            float column = Mathf.Min(0.15f, surface.Width * 0.3f);
            var ys = new List<float>();
            var meshDepths = new List<float>();
            var colliderDepths = new List<float>();
            bool anyMesh = false;
            const float start = 0.5f;
            for (float y = bottom + ProfileStep * 0.5f; y < top; y += ProfileStep)
            {
                float meshDepth = float.NaN, colliderDepth = float.NaN;
                for (int c = -1; c <= 1; c++)
                {
                    Vector3 local = surface.Center + across * (column * c) + outward * start;
                    local.y = y;
                    var ray = new Ray(root.TransformPoint(local), root.TransformDirection(-outward).normalized);
                    foreach (Collider col in s_colliders)
                    {
                        if (col.Raycast(ray, out RaycastHit hit, start * 3f))
                        {
                            colliderDepth = Outer(colliderDepth, start - hit.distance);
                        }
                    }
                    foreach (MeshFilter mf in s_bodyMeshes)
                    {
                        if (RaycastMesh(mf, ray, start * 3f, out float distance))
                        {
                            meshDepth = Outer(meshDepth, start - distance);
                        }
                    }
                }
                anyMesh |= !float.IsNaN(meshDepth);
                ys.Add(y);
                meshDepths.Add(meshDepth);
                colliderDepths.Add(colliderDepth);
            }
            // Табличка ложится на то, что видно, — на модель; коллайдер — только если модели нет (он бывает и меньше
            // модели, и больше: у каменного сундука спереди коробка коллайдера на 12 см впереди стенки).
            List<float> depths = anyMesh ? meshDepths : colliderDepths;
            if (!DisplayRules.LongestSmoothRun(depths, MaxStep, out int from, out int to) || to - from < 2)
            {
                return;
            }
            DisplayRules.FitOuterLine(ys, depths, from, to, MaxSlope, out float slope, out float offset);
            for (int i = from; i <= to; i++)
            {
                if (!float.IsNaN(colliderDepths[i]))
                {
                    surface.ColliderGap = Mathf.Max(surface.ColliderGap, colliderDepths[i] - (offset + slope * ys[i]));
                }
            }
            float y0 = ys[from] - ProfileStep * 0.5f, y1 = ys[to] + ProfileStep * 0.5f;
            float cy = (y0 + y1) * 0.5f;
            surface.Center.y = cy;
            surface.Height = y1 - y0;
            surface.Depth = offset + slope * cy;
            surface.Slope = slope;
            surface.Profiled = true;
        }

        /// <summary>
        /// Насколько поверхность ближе плоскости грани: лучи снаружи в центр и в углы таблички, берём самую
        /// выступающую точку — табличка ляжет на выступы (оковку) и не провалится в них.
        /// </summary>
        private static float Depth(Vector3 center, Vector3 outward, Vector3 up, float size)
        {
            Vector3 right = Vector3.Cross(up, outward).normalized;
            float half = size * 0.5f * 0.8f;
            float depth = float.NegativeInfinity;
            const float start = 0.5f;
            for (int i = 0; i < 5; i++)
            {
                float ox = i == 0 ? 0f : ((i & 1) == 0 ? half : -half);
                float oy = i == 0 ? 0f : (i < 3 ? half : -half);
                var ray = new Ray(center + right * ox + up * oy + outward * start, -outward);
                foreach (Collider c in s_colliders)
                {
                    if (c.Raycast(ray, out RaycastHit hit, start * 3f))
                    {
                        depth = Mathf.Max(depth, start - hit.distance);
                    }
                }
                foreach (MeshFilter mf in s_bodyMeshes)
                {
                    if (RaycastMesh(mf, ray, start * 3f, out float distance))
                    {
                        depth = Mathf.Max(depth, start - distance);
                    }
                }
            }
            return float.IsNegativeInfinity(depth) ? 0f : depth;
        }

        /// <summary>
        /// Рамка закрытой крышки (Container.m_closed) в координатах корня сундука. Крышкой считается, только если она
        /// в верхней части сундука: у некоторых модовых сундуков «закрытым видом» может быть весь сундук.
        /// </summary>
        private static bool LidBounds(Container chest, Transform root, Bounds body, out Bounds lid)
        {
            lid = default;
            if (chest.m_closed == null || !MeshBounds(chest.m_closed, root, out lid))
            {
                return false;
            }
            return lid.min.y > body.min.y + body.size.y * 0.3f && lid.size.x > 0.05f && lid.size.z > 0.05f;
        }

        /// <summary>Рамка всех мешей объекта (вместе с неактивными) в координатах root.</summary>
        private static bool MeshBounds(GameObject go, Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null)
                {
                    continue;
                }
                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
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

        /// <summary>
        /// Как сдвигается крышка при открытии: матрица из положения закрытой крышки в положение открытой (в мире).
        /// Закрытая и открытая крышки у ванильных сундуков — два объекта с одним и тем же мешем (Container.m_closed и
        /// m_open), поэтому табличка на крышке, сдвинутая той же матрицей, остаётся на ней. Сравниваются первые меши
        /// в каждом (у сундука из чёрного металла m_closed/m_open — группы с крышкой внутри).
        /// </summary>
        public static bool TryLidMotion(Container chest, out Matrix4x4 motion)
        {
            motion = Matrix4x4.identity;
            if (chest == null || chest.m_open == null || chest.m_closed == null)
            {
                return false;
            }
            MeshFilter closed = FirstMesh(chest.m_closed);
            MeshFilter open = FirstMesh(chest.m_open);
            if (closed == null || open == null || closed.sharedMesh.name != open.sharedMesh.name)
            {
                return false;
            }
            motion = open.transform.localToWorldMatrix * closed.transform.worldToLocalMatrix;
            return true;
        }

        /// <summary>Открыт ли сундук (виден открытый вид крышки) — так его показывает сама игра, и у других игроков тоже.</summary>
        public static bool IsOpen(Container chest) => chest != null && chest.m_open != null && chest.m_open.activeInHierarchy;

        private static MeshFilter FirstMesh(GameObject go)
        {
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh != null)
                {
                    return mf;
                }
            }
            return null;
        }

        /// <summary>Есть ли уже табличка на этом месте (по корню или по её виду — он мог быть подтянут на поверхность).</summary>
        public static bool Occupied(Vector3 position)
        {
            foreach (ChestSign sign in ChestSign.All)
            {
                if (sign != null && (Vector3.Distance(sign.transform.position, position) < OccupiedDistance ||
                                     Vector3.Distance(sign.VisualPosition, position) < OccupiedDistance))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Сундук за спиной висящей таблички: луч из её лицевой стороны назад.</summary>
        public static Container FindBehind(ChestSign sign)
        {
            Transform t = sign.transform;
            float half = SignPiece.BoardSize * 0.35f;
            for (int i = 0; i < 5; i++)
            {
                float ox = i == 0 ? 0f : ((i & 1) == 0 ? half : -half);
                float oy = i == 0 ? 0f : (i < 3 ? half : -half);
                // Луч — издалека: коллайдер сундука бывает впереди стенки, на которой висит табличка (каменный сундук —
                // на 12 см), а луч, начатый внутри коллайдера, его не видит. Сундук перед табличкой (дальше 20 см
                // от её задней стороны) — не её.
                Vector3 origin = t.position + t.right * ox + t.up * oy + t.forward * BehindStart;
                int count = Physics.RaycastNonAlloc(origin, -t.forward, s_hits, BehindStart + 0.6f, RayMask, QueryTriggerInteraction.Ignore);
                Container best = null;
                float bestDistance = float.MaxValue;
                for (int h = 0; h < count; h++)
                {
                    Collider c = s_hits[h].collider;
                    Piece piece = c != null ? c.GetComponentInParent<Piece>() : null;
                    if (piece == null || s_hits[h].distance >= bestDistance || s_hits[h].distance < BehindStart - 0.2f ||
                        piece.GetComponent<ChestSign>() != null)
                    {
                        continue; // своя или соседняя табличка, или вовсе не постройка
                    }
                    Container chest = ChestOf(piece);
                    if (chest != null && Check(chest) == Problem.None)
                    {
                        best = chest;
                        bestDistance = s_hits[h].distance;
                    }
                }
                if (best != null)
                {
                    return best;
                }
            }
            return null;
        }

        /// <summary>Корень постройки сундука (у ванильных — сам сундук): по нему считаются грани и коллайдеры.</summary>
        private static Transform PieceRoot(Container chest)
        {
            Piece piece = chest.GetComponentInParent<Piece>();
            return piece != null ? piece.transform : chest.transform;
        }

        /// <summary>
        /// Рамка тела сундука в координатах корня: твёрдые коллайдеры и видимые меши тела вместе (заодно запоминает их
        /// для лучей). Меши — активные, на слое самой постройки, не крышка (m_open/m_closed): так не попадают снег,
        /// украшения других модов (руны распределяющего сундука) и открытая крышка.
        /// </summary>
        private static bool BodyBounds(Container chest, Transform root, out Bounds bounds)
        {
            bool any = LocalBounds(root, out bounds);
            s_bodyMeshes.Clear();
            Piece piece = root.GetComponent<Piece>();
            int layer = root.gameObject.layer;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
            {
                MeshRenderer r = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || r == null || !r.enabled || mf.gameObject.layer != layer ||
                    (piece != null && mf.GetComponentInParent<Piece>() != piece) ||
                    Under(mf.transform, chest.m_open) || Under(mf.transform, chest.m_closed))
                {
                    continue;
                }
                s_bodyMeshes.Add(mf);
                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (any)
                    {
                        bounds.Encapsulate(p);
                    }
                    else
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                }
            }
            return any;
        }

        private static float Outer(float current, float depth) => float.IsNaN(current) || depth > current ? depth : current;

        private static bool Under(Transform t, GameObject parent) => parent != null && t.IsChildOf(parent.transform);

        /// <summary>
        /// Луч по видимому мешу: по треугольникам (если меш читаемый), иначе по его рамке. distance — в метрах мира.
        /// </summary>
        private static bool RaycastMesh(MeshFilter mf, Ray ray, float maxDistance, out float distance)
        {
            distance = 0f;
            Mesh mesh = mf.sharedMesh;
            Transform t = mf.transform;
            Vector3 o = t.InverseTransformPoint(ray.origin);
            Vector3 d = t.InverseTransformVector(ray.direction);
            float best = float.MaxValue;
            MeshData data = DataOf(mesh);
            if (data != null)
            {
                Vector3[] v = data.Vertices;
                int[] tri = data.Triangles;
                for (int i = 0; i + 2 < tri.Length; i += 3)
                {
                    if (RayTriangle(o, d, v[tri[i]], v[tri[i + 1]], v[tri[i + 2]], out float k) && k < best)
                    {
                        best = k;
                    }
                }
            }
            else if (RayBox(o, d, mesh.bounds, out float k))
            {
                best = k;
            }
            if (best == float.MaxValue)
            {
                return false;
            }
            distance = Vector3.Distance(ray.origin, t.TransformPoint(o + d * best));
            return distance <= maxDistance;
        }

        private static MeshData DataOf(Mesh mesh)
        {
            if (s_meshData.TryGetValue(mesh, out MeshData cached))
            {
                return cached;
            }
            MeshData data = null;
            if (mesh.isReadable)
            {
                data = new MeshData { Vertices = mesh.vertices, Triangles = mesh.triangles };
            }
            s_meshData[mesh] = data;
            return data;
        }

        /// <summary>Пересечение луча o + d·k (k ≥ 0) с треугольником с обеих сторон (Мёллер — Трумбор).</summary>
        private static bool RayTriangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float k)
        {
            k = 0f;
            Vector3 e1 = b - a, e2 = c - a;
            Vector3 p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p);
            if (det > -1e-9f && det < 1e-9f)
            {
                return false;
            }
            float inv = 1f / det;
            Vector3 s = o - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f)
            {
                return false;
            }
            Vector3 q = Vector3.Cross(s, e1);
            float w = Vector3.Dot(d, q) * inv;
            if (w < 0f || u + w > 1f)
            {
                return false;
            }
            k = Vector3.Dot(e2, q) * inv;
            return k >= 0f;
        }

        /// <summary>Пересечение луча o + d·k (k ≥ 0) с рамкой (slab-метод).</summary>
        private static bool RayBox(Vector3 o, Vector3 d, Bounds box, out float k)
        {
            float near = 0f, far = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                if (Mathf.Abs(d[i]) < 1e-9f)
                {
                    if (o[i] < box.min[i] || o[i] > box.max[i])
                    {
                        k = 0f;
                        return false;
                    }
                    continue;
                }
                float t1 = (box.min[i] - o[i]) / d[i], t2 = (box.max[i] - o[i]) / d[i];
                near = Mathf.Max(near, Mathf.Min(t1, t2));
                far = Mathf.Min(far, Mathf.Max(t1, t2));
            }
            k = near;
            return near <= far;
        }

        /// <summary>
        /// Рамка твёрдых коллайдеров постройки в её собственных координатах (заодно запоминает эти коллайдеры для лучей).
        /// </summary>
        private static bool LocalBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            s_colliders.Clear();
            Piece piece = root.GetComponent<Piece>();
            bool any = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            foreach (Collider c in root.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled || (piece != null && c.GetComponentInParent<Piece>() != piece))
                {
                    continue;
                }
                s_colliders.Add(c);
                if (!LocalBox(c, out Vector3 center, out Vector3 size))
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
            if (!any)
            {
                return false;
            }
            bounds.SetMinMax(min, max);
            return true;
        }

        /// <summary>Рамка коллайдера в его собственных координатах.</summary>
        public static bool LocalBox(Collider c, out Vector3 center, out Vector3 size)
        {
            switch (c)
            {
                case BoxCollider box:
                    center = box.center;
                    size = box.size;
                    return true;
                case MeshCollider mesh when mesh.sharedMesh != null:
                    center = mesh.sharedMesh.bounds.center;
                    size = mesh.sharedMesh.bounds.size;
                    return true;
                case SphereCollider sphere:
                    center = sphere.center;
                    size = Vector3.one * (sphere.radius * 2f);
                    return true;
                case CapsuleCollider capsule:
                    center = capsule.center;
                    float d = capsule.radius * 2f;
                    size = new Vector3(d, d, d);
                    size[capsule.direction] = Mathf.Max(d, capsule.height);
                    return true;
                default:
                    center = Vector3.zero;
                    size = Vector3.zero;
                    return false;
            }
        }
    }
}
