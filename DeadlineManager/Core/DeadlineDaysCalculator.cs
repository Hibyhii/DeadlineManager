using System;

namespace DynamicDeadlineMod.Core
{
    public static class DeadlineDaysCalculator
    {
        // Mirrors the t^2 / 16 shape discussed for quota progression while keeping
        // the user-facing growth coefficient in human-sized "days" units.
        private const double QuadraticNormalization = 16d;

        public static DeadlineDaysCalculationResult Calculate(
            DeadlineDaysSettings settings,
            int quotasCompleted,
            DeadlineManagerState dynamicState)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (dynamicState == null)
            {
                throw new ArgumentNullException(nameof(dynamicState));
            }

            if (quotasCompleted < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quotasCompleted),
                    "Completed quota count cannot be negative.");
            }

            var baseline = CalculateBaseline(settings, quotasCompleted);

            if (settings.BaselineMode == DeadlineBaselineMode.Static ||
                !settings.DynamicAdjustmentEnabled)
            {
                return BaselineOnly(settings, baseline);
            }

            return ApplyDynamicAdjustment(settings, baseline, dynamicState);
        }

        private static double CalculateBaseline(
            DeadlineDaysSettings settings,
            int quotasCompleted)
        {
            switch (settings.BaselineMode)
            {
                case DeadlineBaselineMode.Static:
                    return settings.StaticDeadlineDays;
                case DeadlineBaselineMode.Linear:
                    return ClampShared(
                        settings.InitialDeadlineDaysFloor +
                        settings.LinearDaysPerQuota * quotasCompleted,
                        settings);
                case DeadlineBaselineMode.Quadratic:
                    return CalculateQuadraticBaseline(settings, quotasCompleted);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(settings),
                        settings.BaselineMode,
                        "Unknown deadline baseline mode.");
            }
        }

        private static DeadlineDaysCalculationResult BaselineOnly(
            DeadlineDaysSettings settings,
            double baseline)
        {
            return new DeadlineDaysCalculationResult(
                settings.BaselineMode,
                RoundDays(baseline),
                baseline,
                baseline,
                null,
                null,
                null);
        }

        private static DeadlineDaysCalculationResult ApplyDynamicAdjustment(
            DeadlineDaysSettings settings,
            double baseline,
            DeadlineManagerState state)
        {
            var calibrationAverage = state.CalibrationAverage;
            var performanceAverage = state.AdjustmentPerformanceAverage;

            if (!calibrationAverage.HasValue || !performanceAverage.HasValue)
            {
                return new DeadlineDaysCalculationResult(
                    settings.BaselineMode,
                    RoundDays(baseline),
                    baseline,
                    baseline,
                    calibrationAverage,
                    performanceAverage,
                    null);
            }

            var performanceOffset = performanceAverage.Value - calibrationAverage.Value;
            var multiplier = performanceOffset >= 0d
                ? 1d - performanceOffset * settings.DynamicDownwardPressure
                : 1d + (-performanceOffset) * settings.DynamicUpwardPressure;

            multiplier = Math.Max(0d, multiplier);

            var adjustedDays = ClampShared(baseline * multiplier, settings);

            return new DeadlineDaysCalculationResult(
                settings.BaselineMode,
                RoundDays(adjustedDays),
                baseline,
                adjustedDays,
                calibrationAverage,
                performanceAverage,
                performanceOffset);
        }

        private static double CalculateQuadraticBaseline(
            DeadlineDaysSettings settings,
            int quotasCompleted)
        {
            var completed = (double)quotasCompleted;
            var rawDays =
                settings.InitialDeadlineDaysFloor +
                settings.QuadraticGrowth * (completed * completed / QuadraticNormalization);

            if (!DeadlineDaysSettings.IsFinite(rawDays))
            {
                return settings.UpperClampEnabled
                    ? settings.DeadlineDaysUpperClamp
                    : DeadlineDaysSettings.MaximumCalculatedDeadlineDays;
            }

            return ClampShared(rawDays, settings);
        }

        private static double ClampShared(double days, DeadlineDaysSettings settings)
        {
            var flooredDays = Math.Max(settings.InitialDeadlineDaysFloor, days);

            return settings.UpperClampEnabled
                ? Math.Min(settings.DeadlineDaysUpperClamp, flooredDays)
                : Math.Min(DeadlineDaysSettings.MaximumCalculatedDeadlineDays, flooredDays);
        }

        private static int RoundDays(double days)
        {
            return (int)Math.Floor(days + 0.5d);
        }
    }
}
