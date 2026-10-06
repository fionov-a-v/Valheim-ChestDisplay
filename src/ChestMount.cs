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

        /// <summary>Таблички ближе этого к месту новой — место занято.</summary>
        private const float OccupiedDistance = 0.12f;

        private static int s_rayMask;
        private static readonly List<Collider> s_colliders = new List<Collider>();
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
        }

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
            if (!LocalBounds(root, out Bounds bounds))
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

            Vector3 outward = root.TransformDirection(surface.Outward).normalized;
            Vector3 up;
            if (face == Face.Top)
            {
                DisplayRules.FaceNormal(DisplayRules.TopUp(toViewer.x, toViewer.z), out float ux, out float uz);
                up = root.TransformDirection(new Vector3(ux, 0f, uz)).normalized;
            }
            else
            {
                up = root.up;
            }
            Vector3 center = root.TransformPoint(surface.Center);
            float depth = surface.FromLid ? 0f : Depth(center, outward, up, size);
            mount.Position = center + outward * (depth + SignPiece.BackOffset);
            mount.Rotation = Quaternion.LookRotation(outward, up);
            return true;
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
            if (!LocalBounds(root, out Bounds bounds))
            {
                return false;
            }
            Vector3 f = root.InverseTransformDirection(sign.forward);
            face = DisplayRules.PickFace(f.x, f.y, f.z, f.x, f.z);
            Surface surface = SurfaceOf(chest, root, bounds, face);
            surfaceMin = Mathf.Min(surface.Width, surface.Height) * Fill(face);
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
            DisplayRules.FaceNormal(face, out float fx, out float fz);
            var outward = new Vector3(fx, 0f, fz);
            float bottom = bounds.min.y;
            float topY = lid ? Mathf.Min(lidBounds.min.y, bounds.max.y) : bounds.max.y;
            Vector3 center = bounds.center + Vector3.Scale(outward, bounds.extents);
            center.y = (bottom + topY) * 0.5f;
            return new Surface
            {
                Center = center,
                Outward = outward,
                Width = fx != 0f ? bounds.size.z : bounds.size.x,
                Height = topY - bottom,
            };
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

        /// <summary>Есть ли уже табличка на этом месте.</summary>
        public static bool Occupied(Vector3 position)
        {
            foreach (ChestSign sign in ChestSign.All)
            {
                if (sign != null && Vector3.Distance(sign.transform.position, position) < OccupiedDistance)
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
                Vector3 origin = t.position + t.right * ox + t.up * oy + t.forward * 0.1f;
                int count = Physics.RaycastNonAlloc(origin, -t.forward, s_hits, 0.7f, RayMask, QueryTriggerInteraction.Ignore);
                Container best = null;
                float bestDistance = float.MaxValue;
                for (int h = 0; h < count; h++)
                {
                    Collider c = s_hits[h].collider;
                    Piece piece = c != null ? c.GetComponentInParent<Piece>() : null;
                    if (piece == null || s_hits[h].distance >= bestDistance || piece.GetComponent<ChestSign>() != null)
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
