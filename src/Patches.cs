using HarmonyLib;

namespace ChestDisplay
{
    [HarmonyPatch]
    internal static class Patches
    {
        /// <summary>Призрак таблички — к сундуку, на который смотрит игрок.</summary>
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        [HarmonyPostfix]
        private static void UpdatePlacementGhost(Player __instance)
        {
            SignPlacement.AfterGhostUpdate(__instance);
        }

        /// <summary>Не поставилась — сообщение с причиной.</summary>
        [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
        [HarmonyPostfix]
        private static void TryPlacePiece(Player __instance, bool __result)
        {
            SignPlacement.AfterTryPlace(__instance, __result);
        }
    }
}
