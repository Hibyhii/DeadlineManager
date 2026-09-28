using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using DeadlineManager.Core;

namespace DeadlineManager.Configuration
{
    internal sealed class ModConfiguration
    {
        private const int DefaultInitialDeadlineDaysFloor = 1;
        private const int DefaultDeadlineDaysUpperClamp = 10;
        private const int DefaultStaticDeadlineDays = 3;
        private const double DefaultLinearDaysPerQuota = 1d;
        private const double DefaultQuadraticGrowth = 1d;
        private const double DefaultDynamicUpwardPressure = 1d;
        private const double DefaultDynamicDownwardPressure = 1d;
        private ModConfiguration(DeadlineDaysSettings deadlineDays)
        {
            DeadlineDays = deadlineDays;
        }

        public DeadlineDaysSettings DeadlineDays { get; private set; }

        public event Action<DeadlineDaysSettings> DeadlineDaysChanged;

        public static ModConfiguration Load(ConfigFile config, ManualLogSource logger)
        {
            var baselineMode = config.Bind(
                "Deadline Days",
                "Baseline Mode",
                DeadlineBaselineMode.Quadratic,
                "Deadline growth curve. Static ignores the shared floor/clamp. Changes apply immediately.");

            var dynamicAdjustmentEnabled = config.Bind(
                "Deadline Days",
                "Enable Dynamic Adjustment",
                true,
                "Adjust Linear or Quadratic deadlines using crew performance history. Ignored by Static. Changes apply immediately.");

            var initialFloor = config.Bind(
                "Deadline Days",
                "Initial Deadline Days Floor",
                DefaultInitialDeadlineDaysFloor,
                "Starting deadline and lower clamp for Linear/Quadratic scaling. Valid range: 1-999; whole numbers only. Changes apply immediately.");

            var upperClamp = config.Bind(
                "Deadline Days",
                "Deadline Days Upper Clamp",
                DefaultDeadlineDaysUpperClamp,
                "Maximum deadline for Linear/Quadratic scaling. Valid range: 1-999; whole numbers only. If lower than the floor, it is raised to the floor. Changes apply immediately.");

            var upperClampEnabled = config.Bind(
                "Deadline Days",
                "Enable Deadline Days Upper Clamp",
                false,
                "When disabled, Linear/Quadratic/Dynamic scaling has no user-configured upper deadline limit. The game/runtime numeric limit still applies. Changes apply immediately.");

            var staticDays = config.Bind(
                "Static",
                "Static Deadline Days",
                DefaultStaticDeadlineDays,
                "Deadline used by Static mode. Valid range: 1-999; whole numbers only. Changes apply immediately.");

            var linearDaysPerQuota = config.Bind(
                "Linear",
                "Days Added Per Quota",
                DefaultLinearDaysPerQuota,
                "Days added per completed quota. Valid range: 0-999; fractional values are allowed. Fractional values are evaluated from total quotas completed, so rounding does not accumulate. Changes apply immediately.");

            var quadraticGrowth = config.Bind(
                "Quadratic",
                "Quadratic Growth",
                DefaultQuadraticGrowth,
                "Quadratic growth coefficient for floor + growth * (quotasCompleted^2 / 16). Increasing this will make things easier over time. Valid range: 0-999; fractional values are allowed. Changes apply immediately.");

            var dynamicUpwardPressure = config.Bind(
                "Dynamic",
                "Upward Pressure",
                DefaultDynamicUpwardPressure,
                "How strongly under-performance grants additional deadline time. 0 disables upward adjustment. Increasing this can make things easier. Valid range: 0-999; fractional values are allowed. Changes apply immediately.");

            var dynamicDownwardPressure = config.Bind(
                "Dynamic",
                "Downward Pressure",
                DefaultDynamicDownwardPressure,
                "How strongly over-performance removes deadline time. 0 disables downward adjustment. Increasing this can make things harder. Valid range: 0-999; fractional values are allowed. Changes apply immediately.");

            DeadlineDaysSettings BuildSettings()
            {
                var repaired = false;

                if (!Enum.IsDefined(typeof(DeadlineBaselineMode), baselineMode.Value))
                {
                    logger.LogWarning(
                        $"Invalid Baseline Mode '{baselineMode.Value}'. Resetting to {DeadlineBaselineMode.Quadratic}.");
                    baselineMode.Value = DeadlineBaselineMode.Quadratic;
                    repaired = true;
                }

                var floor = RepairDayValue(
                    initialFloor,
                    DefaultInitialDeadlineDaysFloor,
                    logger,
                    ref repaired);

                var clamp = RepairDayValue(
                    upperClamp,
                    DefaultDeadlineDaysUpperClamp,
                    logger,
                    ref repaired);

                var staticDeadline = RepairDayValue(
                    staticDays,
                    DefaultStaticDeadlineDays,
                    logger,
                    ref repaired);

                if (upperClampEnabled.Value &&
                    clamp < floor)
                {
                    logger.LogWarning(
                        $"Deadline Days Upper Clamp ({clamp}) is lower than Initial Deadline Days Floor ({floor}). Raising the upper clamp to {floor}.");
                    clamp = floor;
                    upperClamp.Value = floor;
                    repaired = true;
                }

                var linearGrowth = RepairTuningValue(
                    linearDaysPerQuota,
                    DefaultLinearDaysPerQuota,
                    logger,
                    ref repaired);

                var quadratic = RepairTuningValue(
                    quadraticGrowth,
                    DefaultQuadraticGrowth,
                    logger,
                    ref repaired);

                var upwardPressure = RepairTuningValue(
                    dynamicUpwardPressure,
                    DefaultDynamicUpwardPressure,
                    logger,
                    ref repaired);

                var downwardPressure = RepairTuningValue(
                    dynamicDownwardPressure,
                    DefaultDynamicDownwardPressure,
                    logger,
                    ref repaired);

                if (repaired)
                {
                    config.Save();
                }

                return new DeadlineDaysSettings(
                    baselineMode.Value,
                    dynamicAdjustmentEnabled.Value,
                    floor,
                    upperClampEnabled.Value,
                    clamp,
                    staticDeadline,
                    linearGrowth,
                    quadratic,
                    upwardPressure,
                    downwardPressure);
            }

            var configuration = new ModConfiguration(BuildSettings());
            var handlingSettingChange = false;

            void ApplyChangedSettings()
            {
                if (handlingSettingChange)
                {
                    return;
                }

                handlingSettingChange = true;
                try
                {
                    var settings = BuildSettings();
                    configuration.DeadlineDays = settings;
                    configuration.DeadlineDaysChanged?.Invoke(settings);
                }
                finally
                {
                    handlingSettingChange = false;
                }
            }

            baselineMode.SettingChanged += (_, _) => ApplyChangedSettings();
            dynamicAdjustmentEnabled.SettingChanged += (_, _) => ApplyChangedSettings();
            initialFloor.SettingChanged += (_, _) => ApplyChangedSettings();
            upperClamp.SettingChanged += (_, _) => ApplyChangedSettings();
            upperClampEnabled.SettingChanged += (_, _) => ApplyChangedSettings();
            staticDays.SettingChanged += (_, _) => ApplyChangedSettings();
            linearDaysPerQuota.SettingChanged += (_, _) => ApplyChangedSettings();
            quadraticGrowth.SettingChanged += (_, _) => ApplyChangedSettings();
            dynamicUpwardPressure.SettingChanged += (_, _) => ApplyChangedSettings();
            dynamicDownwardPressure.SettingChanged += (_, _) => ApplyChangedSettings();

            return configuration;
        }

        private static int RepairDayValue(
            ConfigEntry<int> entry,
            int defaultValue,
            ManualLogSource logger,
            ref bool repaired)
        {
            if (entry.Value >= 1 &&
                entry.Value <= DeadlineDaysSettings.MaximumSupportedDeadlineDays)
            {
                return entry.Value;
            }

            logger.LogWarning(
                $"{entry.Definition.Key} value {entry.Value} is outside 1-{DeadlineDaysSettings.MaximumSupportedDeadlineDays}. Resetting it to {defaultValue}.");
            entry.Value = defaultValue;
            repaired = true;
            return defaultValue;
        }

        private static double RepairTuningValue(
            ConfigEntry<double> entry,
            double defaultValue,
            ManualLogSource logger,
            ref bool repaired)
        {
            if (DeadlineDaysSettings.IsFinite(entry.Value) &&
                entry.Value >= 0d &&
                entry.Value <= DeadlineDaysSettings.MaximumSupportedTuningValue)
            {
                return entry.Value;
            }

            logger.LogWarning(
                $"{entry.Definition.Key} value {entry.Value} is outside 0-{DeadlineDaysSettings.MaximumSupportedTuningValue} or is not finite. Resetting it to {defaultValue}.");
            entry.Value = defaultValue;
            repaired = true;
            return defaultValue;
        }
    }
}
