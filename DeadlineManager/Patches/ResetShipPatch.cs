using HarmonyLib;

namespace DynamicDeadlineMod.Patches
{
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ResetShip))]
    internal static class ResetShipPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            DeadlineManager.Runtime?.ApplyResetShipDeadline(TimeOfDay.Instance);
        }
    }
}
