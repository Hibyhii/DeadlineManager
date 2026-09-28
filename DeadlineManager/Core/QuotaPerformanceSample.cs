using System;

namespace DynamicDeadlineMod.Core
{
    public sealed class QuotaPerformanceSample
    {
        private readonly double _successOffset;

        public QuotaPerformanceSample(double quotaRequired, double quotaFulfilled)
        {
            if (!DeadlineDaysSettings.IsFinite(quotaRequired) || quotaRequired <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quotaRequired),
                    "Quota required must be finite and greater than zero.");
            }

            if (!DeadlineDaysSettings.IsFinite(quotaFulfilled) || quotaFulfilled < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quotaFulfilled),
                    "Quota fulfilled must be finite and non-negative.");
            }

            var successOffset = (quotaFulfilled - quotaRequired) / quotaRequired;
            if (!DeadlineDaysSettings.IsFinite(successOffset))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quotaFulfilled),
                    "Quota performance produced a non-finite success percentage.");
            }

            QuotaRequired = quotaRequired;
            QuotaFulfilled = quotaFulfilled;
            _successOffset = successOffset;
        }

        public double QuotaRequired { get; }

        public double QuotaFulfilled { get; }

        public double SuccessOffset => _successOffset;
    }
}
