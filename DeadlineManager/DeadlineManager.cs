using BepInEx;
using DeadlineManager.Configuration;
using DeadlineManager.Persistence;
using DeadlineManager.Runtime;
using HarmonyLib;

namespace DeadlineManager
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class DeadlineManager : BaseUnityPlugin
    {
        public const string ModGuid = "Hibyhii.DeadlineManager";
        public const string ModName = "DeadlineManager";
        public const string ModVersion = "2.0.6";

        private readonly Harmony _harmony = new(ModGuid);
        private ModConfiguration _configuration;

        internal static DeadlineRuntime Runtime { get; private set; }

        internal void Awake()
        {
            _configuration = ModConfiguration.Load(Config, Logger);
            var stateRepository = new DeadlineStateRepository(Logger);
            Runtime = new DeadlineRuntime(
                _configuration.DeadlineDays,
                stateRepository,
                Logger);
            _configuration.DeadlineDaysChanged += Runtime.ApplySettings;

            _harmony.PatchAll();

            Logger.LogInfo(
                $"{ModName} {ModVersion} initialized. Baseline={_configuration.DeadlineDays.BaselineMode}, Dynamic={_configuration.DeadlineDays.DynamicAdjustmentEnabled}, Floor={_configuration.DeadlineDays.InitialDeadlineDaysFloor}, Clamp={(_configuration.DeadlineDays.UpperClampEnabled ? _configuration.DeadlineDays.DeadlineDaysUpperClamp.ToString() : "Off")}.");
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
