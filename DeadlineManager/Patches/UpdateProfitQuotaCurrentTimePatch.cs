using HarmonyLib;

namespace DeadlineManager.Patches
{
    [HarmonyPatch(typeof(TimeOfDay), nameof(TimeOfDay.UpdateProfitQuotaCurrentTime))]
    internal static class UpdateProfitQuotaCurrentTimePatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(
            TimeOfDay __instance,
            out bool __state)
        {
            __state =
                DeadlineManager.Runtime?.PrepareInitialDeadline(__instance) ==
                true;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(
            TimeOfDay __instance,
            bool __state)
        {
            if (!__state)
            {
                return;
            }

            DeadlineManager.Runtime?.FinalizeDeadlineRefresh(__instance);
        }
    }
}
