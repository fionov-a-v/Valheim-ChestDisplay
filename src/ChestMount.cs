using System.Collections.Generic;
using ChestDisplay.Logic;
using UnityEngine;

namespace ChestDisplay
{
    /// <summary>
    /// К каким сундукам крепится табличка и куда именно: по центру боковой грани, обращённой к игроку,
    /// вплотную к поверхности (с учётом выступов — оковки).
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

        /// <summary>
        /// Где встанет табличка: центр корня таблички (её задняя сторона прижата к сундуку) и поворот (лицом наружу).
        /// </summary>
        public static bool TryPose(Container chest, Vector3 hitPoint, Vector3 hitNormal, Vector3 viewer, out Vector3 position,
            out Quaternion rotation)
        {
            position = hitPoint;
            rotation = Quaternion.identity;
            Transform root = PieceRoot(chest);
            if (!LocalBounds(root, out Bounds bounds))
            {
                return false;
            }

            Vector3 n = root.InverseTransformDirection(hitNormal);
            Vector3 toViewer = root.InverseTransformPoint(viewer) - bounds.center;
            Face face = DisplayRules.PickFace(n.x, n.z, toViewer.x, toViewer.z);
            DisplayRules.FaceNormal(face, out float fx, out float fz);
            var outwardLocal = new Vector3(fx, 0f, fz);

            // Центр грани по рамке коллайдеров; по высоте — середина сундука (ниже крышки у всех ванильных сундуков).
            Vector3 faceCenterLocal = bounds.center + Vector3.Scale(outwardLocal, bounds.extents);
            Vector3 center = root.TransformPoint(faceCenterLocal);
            Vector3 outward = root.TransformDirection(outwardLocal).normalized;
            Vector3 up = root.up;
            Vector3 right = Vector3.Cross(up, outward).normalized;

            // Насколько поверхность ближе плоскости грани: лучи снаружи в центр и в углы таблички, берём самую
            // выступающую точку — табличка ляжет на выступы и не провалится в них.
            float half = SignPiece.BoardSize * 0.5f * 0.8f;
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
            if (float.IsNegativeInfinity(depth))
            {
                depth = 0f;
            }

            position = center + outward * (depth + SignPiece.BackOffset);
            rotation = Quaternion.LookRotation(outward, up);
            return true;
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
