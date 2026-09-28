using System;

namespace DeadlineManager.Core
{
    public sealed class DeadlineDaysSettings
    {
        public const int MaximumSupportedDeadlineDays = 999;
        public const double MaximumSupportedTuningValue = 999d;
        public const int MaximumCalculatedDeadlineDays = int.MaxValue;

        public DeadlineDaysSettings(
            DeadlineBaselineMode baselineMode,
            bool dynamicAdjustmentEnabled,
            int initialDeadlineDaysFloor,
            bool upperClampEnabled,
            int deadlineDaysUpperClamp,
            int staticDeadlineDays,
            double linearDaysPerQuota,
            double quadraticGrowth,
            double dynamicUpwardPressure,
            double dynamicDownwardPressure)
        {
            ValidateDayValue(initialDeadlineDaysFloor, nameof(initialDeadlineDaysFloor));
            ValidateDayValue(deadlineDaysUpperClamp, nameof(deadlineDaysUpperClamp));
            ValidateDayValue(staticDeadlineDays, nameof(staticDeadlineDays));

            if (upperClampEnabled &&
                deadlineDaysUpperClamp < initialDeadlineDaysFloor)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deadlineDaysUpperClamp),
                    "Deadline upper clamp cannot be lower than the initial deadline floor.");
            }

            ValidateNonNegativeFinite(linearDaysPerQuota, nameof(linearDaysPerQuota));
            ValidateNonNegativeFinite(quadraticGrowth, nameof(quadraticGrowth));
            ValidateNonNegativeFinite(dynamicUpwardPressure, nameof(dynamicUpwardPressure));
            ValidateNonNegativeFinite(dynamicDownwardPressure, nameof(dynamicDownwardPressure));

            BaselineMode = baselineMode;
            DynamicAdjustmentEnabled = dynamicAdjustmentEnabled;
            InitialDeadlineDaysFloor = initialDeadlineDaysFloor;
            UpperClampEnabled = upperClampEnabled;
            DeadlineDaysUpperClamp = deadlineDaysUpperClamp;
            StaticDeadlineDays = staticDeadlineDays;
            LinearDaysPerQuota = linearDaysPerQuota;
            QuadraticGrowth = quadraticGrowth;
            DynamicUpwardPressure = dynamicUpwardPressure;
            DynamicDownwardPressure = dynamicDownwardPressure;
        }

        public DeadlineBaselineMode BaselineMode { get; }

        public bool DynamicAdjustmentEnabled { get; }

        public int InitialDeadlineDaysFloor { get; }

        public bool UpperClampEnabled { get; }

        public int DeadlineDaysUpperClamp { get; }

        public int StaticDeadlineDays { get; }

        public double LinearDaysPerQuota { get; }

        public double QuadraticGrowth { get; }

        public double DynamicUpwardPressure { get; }

        public double DynamicDownwardPressure { get; }

        internal static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void ValidateDayValue(int value, string parameterName)
        {
            if (value < 1 || value > MaximumSupportedDeadlineDays)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Deadline days must be between 1 and {MaximumSupportedDeadlineDays}.");
            }
        }

        private static void ValidateNonNegativeFinite(double value, string parameterName)
        {
            if (!IsFinite(value) ||
                value < 0d ||
                value > MaximumSupportedTuningValue)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Value must be finite and between 0 and {MaximumSupportedTuningValue}.");
            }
        }
    }
}
