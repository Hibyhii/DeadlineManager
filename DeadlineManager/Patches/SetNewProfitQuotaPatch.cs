using DynamicDeadlineMod.Core;
using HarmonyLib;

namespace DynamicDeadlineMod.Patches
{
    [HarmonyPatch(typeof(TimeOfDay), nameof(TimeOfDay.SetNewProfitQuota))]
    internal static class SetNewProfitQuotaPatch
    {
        internal sealed class RolloverState
        {
            public RolloverState(
                int quotasCompletedBefore,
                QuotaPerformanceSample completedQuota)
            {
                QuotasCompletedBefore = quotasCompletedBefore;
                CompletedQuota = completedQuota;
            }

            public int QuotasCompletedBefore { get; }

            public QuotaPerformanceSample CompletedQuota { get; }
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(
            TimeOfDay __instance,
            out RolloverState __state)
        {
            __state = null;

            var runtime = DeadlineManager.Runtime;
            if (runtime == null || !runtime.ShouldHandle(__instance))
            {
                return;
            }

            __state = new RolloverState(
                __instance.timesFulfilledQuota,
                runtime.CaptureCompletedQuota(__instance));
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyAfter("SoftDiamond.BrutalCompanyMinusExtraReborn")]
        private static void Postfix(
            TimeOfDay __instance,
            RolloverState __state)
        {
            var runtime = DeadlineManager.Runtime;
            if (runtime == null ||
                __state == null ||
                !runtime.ShouldHandle(__instance) ||
                __instance.timesFulfilledQuota <= __state.QuotasCompletedBefore)
            {
                return;
            }

            runtime.HandleQuotaRollover(__instance, __state.CompletedQuota);
        }
    }
}
