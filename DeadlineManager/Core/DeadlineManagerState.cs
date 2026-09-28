using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicDeadlineMod.Core
{
    public sealed class DeadlineManagerState
    {
        public const int CalibrationSampleTarget = 3;
        public const int RecentSampleCapacity = 3;

        private readonly QuotaPerformanceSample[] _calibrationSamples;
        private readonly QuotaPerformanceSample[] _recentSamples;

        public DeadlineManagerState(
            IEnumerable<QuotaPerformanceSample> calibrationSamples,
            IEnumerable<QuotaPerformanceSample> recentSamples)
        {
            if (calibrationSamples == null)
            {
                throw new ArgumentNullException(nameof(calibrationSamples));
            }

            if (recentSamples == null)
            {
                throw new ArgumentNullException(nameof(recentSamples));
            }

            _calibrationSamples = calibrationSamples.ToArray();
            _recentSamples = recentSamples.ToArray();

            if (_calibrationSamples.Length > CalibrationSampleTarget)
            {
                throw new ArgumentException(
                    $"Calibration history cannot contain more than {CalibrationSampleTarget} samples.",
                    nameof(calibrationSamples));
            }

            if (_recentSamples.Length > RecentSampleCapacity)
            {
                throw new ArgumentException(
                    $"Recent history cannot contain more than {RecentSampleCapacity} samples.",
                    nameof(recentSamples));
            }

            if (_calibrationSamples.Length < CalibrationSampleTarget && _recentSamples.Length != 0)
            {
                throw new ArgumentException(
                    "Recent samples cannot exist until calibration is complete.",
                    nameof(recentSamples));
            }

            ValidateSamples(_calibrationSamples, nameof(calibrationSamples));
            ValidateSamples(_recentSamples, nameof(recentSamples));
        }

        public IReadOnlyList<QuotaPerformanceSample> CalibrationSamples => _calibrationSamples;

        public IReadOnlyList<QuotaPerformanceSample> RecentSamples => _recentSamples;

        public bool IsCalibrated => _calibrationSamples.Length == CalibrationSampleTarget;

        public bool HasDynamicObservation => _recentSamples.Length != 0;

        public double? CalibrationAverage =>
            IsCalibrated ? _calibrationSamples.Average(sample => sample.SuccessOffset) : (double?)null;

        public double? WeightedRecentAverage =>
            HasDynamicObservation ? CalculateWeightedAverage(_recentSamples) : (double?)null;

        public double? AdjustmentPerformanceAverage
        {
            get
            {
                if (!IsCalibrated)
                {
                    return null;
                }

                if (HasDynamicObservation)
                {
                    return WeightedRecentAverage;
                }

                // Quota 4 is the first adjusted deadline. At that point quota 3
                // is both part of calibration and the newest performance signal.
                return _calibrationSamples[CalibrationSampleTarget - 1].SuccessOffset;
            }
        }

        public static DeadlineManagerState Empty { get; } =
            new DeadlineManagerState(
                Array.Empty<QuotaPerformanceSample>(),
                Array.Empty<QuotaPerformanceSample>());

        public DeadlineManagerState Record(QuotaPerformanceSample sample)
        {
            if (sample == null)
            {
                throw new ArgumentNullException(nameof(sample));
            }

            if (!IsCalibrated)
            {
                var calibration = _calibrationSamples
                    .Concat(new[] { sample })
                    .ToArray();

                return new DeadlineManagerState(
                    calibration,
                    Array.Empty<QuotaPerformanceSample>());
            }

            var recent = _recentSamples
                .Concat(new[] { sample })
                .TakeLast(RecentSampleCapacity)
                .ToArray();

            return new DeadlineManagerState(_calibrationSamples, recent);
        }

        private static double CalculateWeightedAverage(IReadOnlyList<QuotaPerformanceSample> samples)
        {
            var weightedTotal = 0d;
            var totalWeight = 0d;

            for (var index = 0; index < samples.Count; index++)
            {
                var weight = index + 1;
                weightedTotal += samples[index].SuccessOffset * weight;
                totalWeight += weight;
            }

            return weightedTotal / totalWeight;
        }

        private static void ValidateSamples(
            IEnumerable<QuotaPerformanceSample> samples,
            string parameterName)
        {
            if (samples.Any(sample => sample == null))
            {
                throw new ArgumentException(
                    "Performance history cannot contain null samples.",
                    parameterName);
            }
        }
    }
}
