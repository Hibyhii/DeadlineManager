using DeadlineManager.Core;

namespace DeadlineManager.Tests;

public sealed class DeadlineDaysCalculatorTests
{
    [Fact]
    public void Static_IgnoresSharedFloorAndUpperClamp()
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Static,
            floor: 3,
            upperClamp: 10,
            staticDays: 42);

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 100,
            DeadlineManagerState.Empty);

        Assert.Equal(42, result.DeadlineDays);
        Assert.Equal(42d, result.BaselineDays);
    }

    [Fact]
    public void Static_SupportsMaximumConfiguredDeadline()
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Static,
            staticDays: DeadlineDaysSettings.MaximumSupportedDeadlineDays,
            upperClamp: 10);

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 0,
            DeadlineManagerState.Empty);

        Assert.Equal(999, result.DeadlineDays);
    }

    [Fact]
    public void Static_IgnoresDynamicAdjustmentEvenWhenEnabled()
    {
        var state = Record(
            DeadlineManagerState.Empty,
            successOffsets: new[] { 0.10d, 0.10d, 0.10d, 0.90d });
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Static,
            dynamicEnabled: true,
            staticDays: 7);

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 20,
            state);

        Assert.Equal(7, result.DeadlineDays);
        Assert.False(result.DynamicAdjustmentApplied);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 4)]
    [InlineData(2, 4)]
    [InlineData(3, 5)]
    [InlineData(4, 5)]
    public void Linear_EvaluatesFractionalGrowthFromQuotaCount(
        int quotasCompleted,
        int expectedDays)
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Linear,
            linearDaysPerQuota: 0.5d);

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted,
            DeadlineManagerState.Empty);

        Assert.Equal(expectedDays, result.DeadlineDays);
    }

    [Fact]
    public void Linear_UsesQuotaCountInsteadOfPreviousRoundedDeadline()
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Linear,
            linearDaysPerQuota: 0.25d);

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 6,
            DeadlineManagerState.Empty);

        Assert.Equal(5, result.DeadlineDays);
        Assert.Equal(4.5d, result.BaselineDays);
    }

    [Fact]
    public void Linear_RespectsUpperClamp()
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Linear,
            upperClamp: 7,
            linearDaysPerQuota: 2d);

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 10,
            DeadlineManagerState.Empty);

        Assert.Equal(7, result.DeadlineDays);
    }

    [Fact]
    public void Linear_CanExceedConfiguredClampWhenUpperClampIsDisabled()
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Linear,
            upperClampEnabled: false,
            upperClamp: 7,
            linearDaysPerQuota: 2d);

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 10,
            DeadlineManagerState.Empty);

        Assert.Equal(23, result.DeadlineDays);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(4, 4)]
    [InlineData(8, 7)]
    [InlineData(12, 12)]
    public void Quadratic_FollowsDirectQuadraticCurve(
        int quotasCompleted,
        int expectedDays)
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Quadratic,
            upperClamp: 999,
            quadraticGrowth: 1d);

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted,
            DeadlineManagerState.Empty);

        Assert.Equal(expectedDays, result.DeadlineDays);
    }

    [Fact]
    public void Quadratic_RespectsUpperClampBeforeRounding()
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Quadratic,
            upperClamp: 8,
            quadraticGrowth: 16d);

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 4,
            DeadlineManagerState.Empty);

        Assert.Equal(8d, result.BaselineDays);
        Assert.Equal(8, result.DeadlineDays);
    }

    [Fact]
    public void Quadratic_CanExceed999WhenUpperClampIsDisabled()
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Quadratic,
            upperClampEnabled: false,
            quadraticGrowth: 16d);

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 40,
            DeadlineManagerState.Empty);

        Assert.Equal(1603, result.DeadlineDays);
    }

    [Fact]
    public void PerformanceState_FirstThreeSamplesAreCalibrationOnly()
    {
        var state = Record(
            DeadlineManagerState.Empty,
            successOffsets: new[] { 0.10d, 0.20d, 0.30d });

        Assert.True(state.IsCalibrated);
        Assert.False(state.HasDynamicObservation);
        Assert.Equal(
            new[] { 0.10d, 0.20d, 0.30d },
            state.CalibrationSamples.Select(sample => sample.SuccessOffset));
        Assert.Empty(state.RecentSamples);
        Assert.Equal(0.20d, state.CalibrationAverage!.Value, precision: 10);
    }

    [Fact]
    public void PerformanceState_PostCalibrationSamplesUseThreeSampleMovingWindow()
    {
        var state = Record(
            DeadlineManagerState.Empty,
            successOffsets: new[]
            {
                0.10d, 0.10d, 0.10d,
                0.20d, 0.30d, 0.40d, 0.50d
            });

        Assert.Equal(
            new[] { 0.10d, 0.10d, 0.10d },
            state.CalibrationSamples.Select(sample => sample.SuccessOffset));
        Assert.Equal(
            new[] { 0.30d, 0.40d, 0.50d },
            state.RecentSamples.Select(sample => sample.SuccessOffset));
    }

    [Fact]
    public void PerformanceState_WeightsRecentSamplesTowardNewestQuota()
    {
        var state = Record(
            DeadlineManagerState.Empty,
            successOffsets: new[]
            {
                0d, 0d, 0d,
                0.10d, 0.20d, 0.40d
            });

        var expected = (0.10d * 1d + 0.20d * 2d + 0.40d * 3d) / 6d;

        Assert.Equal(expected, state.WeightedRecentAverage!.Value, precision: 10);
    }

    [Fact]
    public void Dynamic_QuotaFourUsesQuotaThreeAgainstCalibrationAverage()
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Quadratic,
            dynamicEnabled: true,
            upperClamp: 999,
            quadraticGrowth: 16d);
        var calibratedState = Record(
            DeadlineManagerState.Empty,
            successOffsets: new[] { 0.10d, 0.20d, 0.30d });

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 3,
            calibratedState);

        Assert.Equal(12d, result.BaselineDays);
        Assert.Equal(0.30d, result.PerformanceAverage!.Value, precision: 10);
        Assert.Equal(0.10d, result.PerformanceOffset!.Value, precision: 10);
        Assert.Equal(10.8d, result.AdjustedDays, precision: 10);
        Assert.Equal(11, result.DeadlineDays);
        Assert.True(result.DynamicAdjustmentApplied);
    }

    [Fact]
    public void Dynamic_MidRunWithoutHistoryUsesCurrentQuadraticCurveDuringRecovery()
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Quadratic,
            dynamicEnabled: true,
            upperClamp: 999,
            quadraticGrowth: 1d);

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 12,
            DeadlineManagerState.Empty);

        Assert.Equal(12, result.DeadlineDays);
        Assert.Equal(12d, result.BaselineDays);
        Assert.False(result.DynamicAdjustmentApplied);
    }

    [Fact]
    public void Dynamic_FirstPostCalibrationObservationCanAdjustFollowingDeadline()
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Quadratic,
            dynamicEnabled: true,
            floor: 10,
            upperClamp: 999,
            quadraticGrowth: 16d,
            downwardPressure: 1d);
        var state = Record(
            DeadlineManagerState.Empty,
            successOffsets: new[] { 0.10d, 0.10d, 0.10d, 0.30d });

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 4,
            state);

        Assert.Equal(26d, result.BaselineDays);
        Assert.Equal(0.20d, result.PerformanceOffset!.Value, precision: 10);
        Assert.Equal(20.8d, result.AdjustedDays, precision: 10);
        Assert.Equal(21, result.DeadlineDays);
    }

    [Fact]
    public void Dynamic_UnderPerformanceAppliesUpwardPressure()
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Quadratic,
            dynamicEnabled: true,
            floor: 10,
            upperClamp: 999,
            quadraticGrowth: 16d,
            upwardPressure: 0.5d);
        var state = Record(
            DeadlineManagerState.Empty,
            successOffsets: new[] { 0.30d, 0.30d, 0.30d, 0.10d });

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 4,
            state);

        Assert.Equal(-0.20d, result.PerformanceOffset!.Value, precision: 10);
        Assert.Equal(28.6d, result.AdjustedDays, precision: 10);
        Assert.Equal(29, result.DeadlineDays);
    }

    [Fact]
    public void Dynamic_UsesClampedQuadraticBaselineBeforeApplyingPressure()
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Quadratic,
            dynamicEnabled: true,
            floor: 3,
            upperClamp: 10,
            quadraticGrowth: 16d,
            downwardPressure: 1d);
        var state = Record(
            DeadlineManagerState.Empty,
            successOffsets: new[] { 0.10d, 0.10d, 0.10d, 0.30d });

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 4,
            state);

        Assert.Equal(10d, result.BaselineDays);
        Assert.Equal(8d, result.AdjustedDays, precision: 10);
        Assert.Equal(8, result.DeadlineDays);
    }

    [Fact]
    public void Dynamic_UsesUnclampedBaselineWhenUpperClampIsDisabled()
    {
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Quadratic,
            dynamicEnabled: true,
            floor: 3,
            upperClampEnabled: false,
            upperClamp: 10,
            quadraticGrowth: 16d,
            downwardPressure: 1d);
        var state = Record(
            DeadlineManagerState.Empty,
            successOffsets: new[] { 0.10d, 0.10d, 0.10d, 0.30d });

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 4,
            state);

        Assert.Equal(19d, result.BaselineDays);
        Assert.Equal(15.2d, result.AdjustedDays, precision: 10);
        Assert.Equal(15, result.DeadlineDays);
    }

    [Fact]
    public void Dynamic_PressureDirectionsCanBeTunedIndependently()
    {
        var noDownwardPressure = Settings(
            baselineMode: DeadlineBaselineMode.Quadratic,
            dynamicEnabled: true,
            floor: 10,
            upperClamp: 999,
            quadraticGrowth: 16d,
            upwardPressure: 2d,
            downwardPressure: 0d);
        var state = Record(
            DeadlineManagerState.Empty,
            successOffsets: new[] { 0.10d, 0.10d, 0.10d, 0.30d });

        var result = DeadlineDaysCalculator.Calculate(
            noDownwardPressure,
            quotasCompleted: 4,
            state);

        Assert.Equal(26, result.DeadlineDays);
        Assert.Equal(26d, result.AdjustedDays);
    }

    [Fact]
    public void PerformanceTracking_IsIndependentOfSelectedDeadlineMode()
    {
        var state = DeadlineManagerState.Empty;
        var sample = SampleFromOffset(0.25d);

        state = state.Record(sample);

        var staticResult = DeadlineDaysCalculator.Calculate(
            Settings(baselineMode: DeadlineBaselineMode.Static),
            quotasCompleted: 1,
            state);
        var linearResult = DeadlineDaysCalculator.Calculate(
            Settings(baselineMode: DeadlineBaselineMode.Linear),
            quotasCompleted: 1,
            state);

        Assert.Single(state.CalibrationSamples);
        Assert.Equal(1000d, state.CalibrationSamples[0].QuotaRequired);
        Assert.Equal(1250d, state.CalibrationSamples[0].QuotaFulfilled);
        Assert.Equal(0.25d, state.CalibrationSamples[0].SuccessOffset, precision: 10);
        Assert.Equal(3, staticResult.DeadlineDays);
        Assert.Equal(4, linearResult.DeadlineDays);
    }

    [Fact]
    public void Dynamic_CanUseHistoryCollectedWhileAnotherModeWasSelected()
    {
        var state = Record(
            DeadlineManagerState.Empty,
            successOffsets: new[] { 0.10d, 0.10d, 0.10d, 0.30d });
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Quadratic,
            dynamicEnabled: true,
            floor: 3,
            upperClamp: 20,
            quadraticGrowth: 4d,
            downwardPressure: 1d);

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 6,
            state);

        Assert.True(result.DynamicAdjustmentApplied);
        Assert.Equal(0.20d, result.PerformanceOffset!.Value, precision: 10);
        Assert.Equal(12d, result.BaselineDays);
        Assert.Equal(9.6d, result.AdjustedDays, precision: 10);
        Assert.Equal(10, result.DeadlineDays);
    }

    [Fact]
    public void Dynamic_CanUseLinearBaseline()
    {
        var state = Record(
            DeadlineManagerState.Empty,
            successOffsets: new[] { 0.10d, 0.10d, 0.10d, 0.30d });
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Linear,
            dynamicEnabled: true,
            floor: 3,
            upperClamp: 20,
            linearDaysPerQuota: 1d,
            downwardPressure: 1d);

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 4,
            state);

        Assert.Equal(7d, result.BaselineDays);
        Assert.Equal(5.6d, result.AdjustedDays, precision: 10);
        Assert.Equal(6, result.DeadlineDays);
    }

    [Fact]
    public void DynamicDisabled_UsesBaselineEvenWhenHistoryExists()
    {
        var state = Record(
            DeadlineManagerState.Empty,
            successOffsets: new[] { 0.10d, 0.10d, 0.10d, 0.90d });
        var settings = Settings(
            baselineMode: DeadlineBaselineMode.Quadratic,
            dynamicEnabled: false,
            floor: 3,
            upperClamp: 999,
            quadraticGrowth: 16d);

        var result = DeadlineDaysCalculator.Calculate(
            settings,
            quotasCompleted: 4,
            state);

        Assert.Equal(19, result.DeadlineDays);
        Assert.Equal(19d, result.BaselineDays);
        Assert.False(result.DynamicAdjustmentApplied);
    }

    [Fact]
    public void QuotaPerformanceSample_StoresPercentAboveOrBelowRequiredQuota()
    {
        Assert.Equal(0.25d, new QuotaPerformanceSample(1000d, 1250d).SuccessOffset, precision: 10);
        Assert.Equal(0d, new QuotaPerformanceSample(1000d, 1000d).SuccessOffset, precision: 10);
        Assert.Equal(-0.25d, new QuotaPerformanceSample(1000d, 750d).SuccessOffset, precision: 10);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void QuotaPerformanceSample_RejectsInvalidRequiredQuota(double quotaRequired)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new QuotaPerformanceSample(quotaRequired, 1000d));
    }

    [Fact]
    public void QuotaPerformanceSample_RejectsNonFiniteDerivedPercentage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new QuotaPerformanceSample(double.Epsilon, double.MaxValue));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Calculator_RejectsNegativeCompletedQuotaCount(int quotasCompleted)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DeadlineDaysCalculator.Calculate(
                Settings(),
                quotasCompleted,
                DeadlineManagerState.Empty));
    }

    [Fact]
    public void Settings_RejectUpperClampBelowFloor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Settings(floor: 10, upperClamp: 9));
    }

    [Fact]
    public void Settings_AllowUpperClampBelowFloorWhenClampIsDisabled()
    {
        var settings = Settings(
            floor: 10,
            upperClampEnabled: false,
            upperClamp: 9);

        Assert.False(settings.UpperClampEnabled);
        Assert.Equal(9, settings.DeadlineDaysUpperClamp);
    }

    [Fact]
    public void Settings_RejectDeadlineValuesAboveSupportedMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Settings(staticDays: DeadlineDaysSettings.MaximumSupportedDeadlineDays + 1));
    }

    [Fact]
    public void Settings_RejectTuningValuesAboveSupportedMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Settings(
                linearDaysPerQuota:
                DeadlineDaysSettings.MaximumSupportedTuningValue + 1d));
    }

    private static DeadlineManagerState Record(
        DeadlineManagerState state,
        IEnumerable<double> successOffsets)
    {
        foreach (var successOffset in successOffsets)
        {
            state = state.Record(SampleFromOffset(successOffset));
        }

        return state;
    }

    private static QuotaPerformanceSample SampleFromOffset(double successOffset)
    {
        const double quota = 1000d;
        return new QuotaPerformanceSample(quota, quota * (1d + successOffset));
    }

    private static DeadlineDaysSettings Settings(
        DeadlineBaselineMode baselineMode = DeadlineBaselineMode.Linear,
        bool dynamicEnabled = false,
        int floor = 3,
        bool upperClampEnabled = true,
        int upperClamp = 10,
        int staticDays = 3,
        double linearDaysPerQuota = 0.5d,
        double quadraticGrowth = 1d,
        double upwardPressure = 1d,
        double downwardPressure = 1d)
    {
        return new DeadlineDaysSettings(
            baselineMode,
            dynamicEnabled,
            floor,
            upperClampEnabled,
            upperClamp,
            staticDays,
            linearDaysPerQuota,
            quadraticGrowth,
            upwardPressure,
            downwardPressure);
    }
}
