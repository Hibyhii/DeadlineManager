namespace DeadlineManager.Core
{
    public sealed class DeadlineDaysCalculationResult
    {
        internal DeadlineDaysCalculationResult(
            DeadlineBaselineMode baselineMode,
            int deadlineDays,
            double unroundedBaselineDays,
            double unroundedAdjustedDays,
            double? calibrationAverage,
            double? performanceAverage,
            double? performanceOffset)
        {
            BaselineMode = baselineMode;
            DeadlineDays = deadlineDays;
            BaselineDays = unroundedBaselineDays;
            AdjustedDays = unroundedAdjustedDays;
            CalibrationAverage = calibrationAverage;
            PerformanceAverage = performanceAverage;
            PerformanceOffset = performanceOffset;
        }

        public DeadlineBaselineMode BaselineMode { get; }

        public int DeadlineDays { get; }

        public double BaselineDays { get; }

        public double AdjustedDays { get; }

        public double? CalibrationAverage { get; }

        public double? PerformanceAverage { get; }

        public double? PerformanceOffset { get; }

        public bool DynamicAdjustmentApplied => PerformanceOffset.HasValue;
    }
}
