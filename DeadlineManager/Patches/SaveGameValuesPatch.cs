using HarmonyLib;

namespace DynamicDeadlineMod.Patches
{
    [HarmonyPatch(typeof(GameNetworkManager), "SaveGameValues")]
    internal static class SaveGameValuesPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(GameNetworkManager __instance)
        {
            DeadlineManager.Runtime?.PersistCurrentDeadline(__instance);
        }
    }
}
