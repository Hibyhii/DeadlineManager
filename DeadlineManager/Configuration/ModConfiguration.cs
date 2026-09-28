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
        private const double DefaultLinearDaysPerQuota = 0.5d;
        private const double DefaultQuadraticGrowth = 9d;
        private const double DefaultDynamicUpwardPressure = 1d;
        private const double DefaultDynamicDownwardPressure = 1d;
        private ModConfiguration(DeadlineDaysSettings deadlineDays)
        {
            DeadlineDays = deadlineDays;
        }

        public DeadlineDaysSettings DeadlineDays { get; }

        public static ModConfiguration Load(ConfigFile config, ManualLogSource logger)
        {
            var baselineMode = config.Bind(
                "Deadline Days",
                "Baseline Mode",
                DeadlineBaselineMode.Quadratic,
                "Deadline growth curve. Static ignores the shared floor/clamp. Changes require a game restart.");

            var dynamicAdjustmentEnabled = config.Bind(
                "Deadline Days",
                "Enable Dynamic Adjustment",
                true,
                "Adjust Linear or Quadratic deadlines using crew performance history. Ignored by Static. Changes require a game restart.");

            var initialFloor = config.Bind(
                "Deadline Days",
                "Initial Deadline Days Floor",
                DefaultInitialDeadlineDaysFloor,
                "Starting deadline and lower clamp for Linear/Quadratic scaling. Valid range: 1-999. Changes require a game restart.");

            var upperClamp = config.Bind(
                "Deadline Days",
                "Deadline Days Upper Clamp",
                DefaultDeadlineDaysUpperClamp,
                "Maximum deadline for Linear/Quadratic scaling. Valid range: 1-999. If lower than the floor, it is raised to the floor. Changes require a game restart.");

            var upperClampEnabled = config.Bind(
                "Deadline Days",
                "Enable Deadline Days Upper Clamp",
                false,
                "When disabled, Linear/Quadratic/Dynamic scaling has no user-configured upper deadline limit. The game/runtime numeric limit still applies. Changes require a game restart.");

            var staticDays = config.Bind(
                "Static",
                "Static Deadline Days",
                DefaultStaticDeadlineDays,
                "Deadline used by Static mode. Valid range: 1-999. Changes require a game restart.");

            var linearDaysPerQuota = config.Bind(
                "Linear",
                "Days Added Per Quota",
                DefaultLinearDaysPerQuota,
                "Days added per completed quota. Fractional values are evaluated from total quotas completed, so rounding does not accumulate. Valid range: 0-999. Changes require a game restart.");

            var quadraticGrowth = config.Bind(
                "Quadratic",
                "Quadratic Growth",
                DefaultQuadraticGrowth,
                "Quadratic growth coefficient for floor + growth * (quotasCompleted^2 / 16). Valid range: 0-999. Changes require a game restart.");

            var dynamicUpwardPressure = config.Bind(
                "Dynamic",
                "Upward Pressure",
                DefaultDynamicUpwardPressure,
                "How strongly under-performance grants additional deadline time. 0 disables upward adjustment. Valid range: 0-999. Changes require a game restart.");

            var dynamicDownwardPressure = config.Bind(
                "Dynamic",
                "Downward Pressure",
                DefaultDynamicDownwardPressure,
                "How strongly over-performance removes deadline time. 0 disables downward adjustment. Valid range: 0-999. Changes require a game restart.");

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

            return new ModConfiguration(
                new DeadlineDaysSettings(
                    baselineMode.Value,
                    dynamicAdjustmentEnabled.Value,
                    floor,
                    upperClampEnabled.Value,
                    clamp,
                    staticDeadline,
                    linearGrowth,
                    quadratic,
                    upwardPressure,
                    downwardPressure));
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
