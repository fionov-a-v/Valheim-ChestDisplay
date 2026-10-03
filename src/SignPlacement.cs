using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ChestDisplay
{
    /// <summary>
    /// Установка таблички молотом. После того как игра расставила призрак постройки, табличка «примагничивается» к сундуку,
    /// на который смотрит игрок: встаёт по центру его боковой грани, обращённой к игроку, вплотную к поверхности.
    /// Ставить можно только на общий сундук и только на свободную грань. Не на сундук — призрака нет, на занятую грань —
    /// он красный; при попытке поставить — сообщение с причиной. (Сама игра на сундуки ничего ставить не даёт — у них m_supports = false; для таблички этот
    /// запрет снимается здесь.)
    /// </summary>
    internal static class SignPlacement
    {
        private static readonly AccessTools.FieldRef<Player, GameObject> s_ghost =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        private static readonly AccessTools.FieldRef<Player, Player.PlacementStatus> s_status =
            AccessTools.FieldRefAccess<Player, Player.PlacementStatus>("m_placementStatus");

        private static readonly MethodInfo s_pieceRayTest = AccessTools.Method(typeof(Player), "PieceRayTest");

        /// <summary>Почему табличку нельзя поставить туда, куда смотрит игрок (для сообщения при попытке).</summary>
        private static ChestMount.Problem s_problem;

        /// <summary>Из Player.UpdatePlacementGhost (postfix): призрак уже расставлен игрой, поправляем его для таблички.</summary>
        public static void AfterGhostUpdate(Player player)
        {
            if (player != Player.m_localPlayer)
            {
                return;
            }
            GameObject ghost = s_ghost(player);
            ChestSign sign = ghost != null ? ghost.GetComponent<ChestSign>() : null;
            if (sign == null)
            {
                s_problem = ChestMount.Problem.None;
                return;
            }
            Player.PlacementStatus status = s_status(player);
            if (status == Player.PlacementStatus.NoRayHits || !ghost.activeSelf ||
                !RayTest(player, out Vector3 point, out Vector3 normal, out Piece target))
            {
                sign.Preview(null);
                s_problem = ChestMount.Problem.None;
                return;
            }

            Container chest = ChestMount.ChestOf(target);
            ChestMount.Problem problem = ChestMount.Check(chest);
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            if (problem == ChestMount.Problem.None)
            {
                Vector3 viewer = GameCamera.instance != null ? GameCamera.instance.transform.position : player.transform.position;
                if (!ChestMount.TryPose(chest, point, normal, viewer, out position, out rotation))
                {
                    problem = ChestMount.Problem.NotChest;
                }
                else if (ChestMount.Occupied(position))
                {
                    problem = ChestMount.Problem.Occupied;
                }
            }

            // Остальные запреты игры (охранный камень, зона без строительства, мешает игрок) остаются в силе;
            // «Invalid» для таблички означает только «смотрит не на сундук» — его решаем сами.
            bool ours = status == Player.PlacementStatus.Valid || status == Player.PlacementStatus.Invalid;
            if (problem == ChestMount.Problem.None)
            {
                ghost.transform.SetPositionAndRotation(position, rotation);
                sign.Preview(chest);
                if (ours)
                {
                    status = Player.PlacementStatus.Valid;
                    if (Location.IsInsideNoBuildLocation(position))
                    {
                        status = Player.PlacementStatus.NoBuildZone;
                    }
                    else if (!PrivateArea.CheckAccess(position, 0f, false))
                    {
                        status = Player.PlacementStatus.PrivateZone;
                    }
                }
            }
            else
            {
                sign.Preview(null);
                if (problem == ChestMount.Problem.Occupied)
                {
                    // Красный призрак прямо поверх уже висящей таблички — видно, что место занято.
                    ghost.transform.SetPositionAndRotation(position, rotation);
                }
                else
                {
                    // Не сундук (земля, стена, бочка, шкаф, личный сундук): призрака нет вовсе, иначе игра ставит его
                    // в точку прицела на любой поверхности и кажется, что табличка цепляется и туда.
                    ghost.SetActive(false);
                }
                if (ours)
                {
                    status = Player.PlacementStatus.Invalid;
                }
            }
            s_problem = status == Player.PlacementStatus.Invalid ? problem : ChestMount.Problem.None;
            s_status(player) = status;
            ghost.GetComponent<Piece>()?.SetInvalidPlacementHeightlight(status != Player.PlacementStatus.Valid);
        }

        /// <summary>Из Player.TryPlacePiece (postfix): табличку не поставили — объяснить почему (вместо «Здесь нельзя»).</summary>
        public static void AfterTryPlace(Player player, bool placed)
        {
            if (placed || player != Player.m_localPlayer || s_problem == ChestMount.Problem.None)
            {
                return;
            }
            GameObject ghost = s_ghost(player);
            if (ghost == null || ghost.GetComponent<ChestSign>() == null || s_status(player) != Player.PlacementStatus.Invalid)
            {
                return;
            }
            string message = s_problem == ChestMount.Problem.Private ? "$chestdisplay_msg_private"
                : s_problem == ChestMount.Problem.Occupied ? "$chestdisplay_msg_occupied"
                : "$chestdisplay_msg_nochest";
            player.Message(MessageHud.MessageType.Center, message);
        }

        /// <summary>Тот же луч, что у игры при расстановке построек (Player.PieceRayTest).</summary>
        private static bool RayTest(Player player, out Vector3 point, out Vector3 normal, out Piece piece)
        {
            point = Vector3.zero;
            normal = Vector3.up;
            piece = null;
            if (s_pieceRayTest == null)
            {
                return false;
            }
            var args = new object[] { null, null, null, null, null, false };
            if (!(bool)s_pieceRayTest.Invoke(player, args))
            {
                return false;
            }
            point = (Vector3)args[0];
            normal = (Vector3)args[1];
            piece = args[2] as Piece;
            return true;
        }
    }
}
