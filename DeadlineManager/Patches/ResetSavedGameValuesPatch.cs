using HarmonyLib;

namespace DynamicDeadlineMod.Patches
{
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.ResetSavedGameValues))]
    internal static class ResetSavedGameValuesPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(GameNetworkManager __instance)
        {
            DeadlineManager.Runtime?.InitializeResetSave(__instance);
        }
    }
}
