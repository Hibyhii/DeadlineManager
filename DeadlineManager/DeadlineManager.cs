using BepInEx;
using DynamicDeadlineMod.Configuration;
using DynamicDeadlineMod.Persistence;
using DynamicDeadlineMod.Runtime;
using HarmonyLib;

namespace DynamicDeadlineMod
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class DeadlineManager : BaseUnityPlugin
    {
        public const string ModGuid = "Haha.DynamicDeadline";
        public const string ModName = "DeadlineManager";
        public const string ModVersion = "2.0.0";

        private readonly Harmony _harmony = new(ModGuid);

        internal static DeadlineRuntime Runtime { get; private set; }

        internal void Awake()
        {
            var configuration = ModConfiguration.Load(Config, Logger);
            var stateRepository = new DeadlineStateRepository(Logger);
            Runtime = new DeadlineRuntime(
                configuration.DeadlineDays,
                stateRepository,
                Logger);

            _harmony.PatchAll();

            Logger.LogInfo(
                $"{ModName} {ModVersion} initialized. Baseline={configuration.DeadlineDays.BaselineMode}, Dynamic={configuration.DeadlineDays.DynamicAdjustmentEnabled}, Floor={configuration.DeadlineDays.InitialDeadlineDaysFloor}, Clamp={(configuration.DeadlineDays.UpperClampEnabled ? configuration.DeadlineDays.DeadlineDaysUpperClamp.ToString() : "Off")}.");
            Logger.LogDebug($"Harmony initialized with ID '{_harmony.Id}'.");
            LogPatchRegistration(
                typeof(TimeOfDay),
                nameof(TimeOfDay.UpdateProfitQuotaCurrentTime));
            LogPatchRegistration(
                typeof(TimeOfDay),
                nameof(TimeOfDay.SetNewProfitQuota));
            LogPatchRegistration(
                typeof(GameNetworkManager),
                "SaveGameValues");
        }

        private void LogPatchRegistration(System.Type type, string methodName)
        {
            var method = AccessTools.Method(type, methodName);
            var patchInfo = method == null ? null : Harmony.GetPatchInfo(method);
            var installed = patchInfo != null && patchInfo.Owners.Contains(ModGuid);

            Logger.LogDebug(
                $"Patch registration {type.Name}.{methodName}: {(installed ? "installed" : "MISSING")}.");
        }
    }

}
