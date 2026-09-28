using System;
using BepInEx.Logging;
using DynamicDeadlineMod.Core;
using DynamicDeadlineMod.Persistence;

namespace DynamicDeadlineMod.Runtime
{
    internal sealed class DeadlineRuntime
    {
        private readonly DeadlineDaysSettings _settings;
        private readonly DeadlineStateRepository _stateRepository;
        private readonly ManualLogSource _logger;

        public DeadlineRuntime(
            DeadlineDaysSettings settings,
            DeadlineStateRepository stateRepository,
            ManualLogSource logger)
        {
            _settings = settings;
            _stateRepository = stateRepository;
            _logger = logger;
        }

        public bool ShouldHandle(TimeOfDay timeOfDay)
        {
            var startOfRoundIsServer =
                StartOfRound.Instance != null &&
                StartOfRound.Instance.IsServer;

            var gameIsHosting =
                GameNetworkManager.Instance != null &&
                GameNetworkManager.Instance.isHostingGame;

            return timeOfDay != null &&
                   (startOfRoundIsServer || gameIsHosting) &&
                   (StartOfRound.Instance == null || !StartOfRound.Instance.isChallengeFile);
        }

        public QuotaPerformanceSample CaptureCompletedQuota(TimeOfDay timeOfDay)
        {
            if (!ShouldHandle(timeOfDay))
            {
                return null;
            }

            try
            {
                return new QuotaPerformanceSample(
                    timeOfDay.profitQuota,
                    timeOfDay.quotaFulfilled);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                _logger.LogWarning(
                    $"Could not record completed quota performance: {exception.Message}");
                return null;
            }
        }

        public void HandleQuotaRollover(
            TimeOfDay timeOfDay,
            QuotaPerformanceSample completedQuota)
        {
            if (!ShouldHandle(timeOfDay))
            {
                return;
            }

            try
            {
                var saveFile = CurrentSaveFile;
                var state = _stateRepository.Load(saveFile);

                if (completedQuota != null)
                {
                    state = state.Record(completedQuota);
                    _stateRepository.Save(saveFile, state);
                }

                var result = DeadlineDaysCalculator.Calculate(
                    _settings,
                    timeOfDay.timesFulfilledQuota,
                    state);

                ApplyDeadline(timeOfDay, result, syncClients: true);
                LogDeadline(
                    "quota rollover",
                    timeOfDay.timesFulfilledQuota,
                    result,
                    state);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    $"DeadlineManager could not apply the quota-rollover deadline. Vanilla deadline state has been left in place where possible. {exception}");
            }
        }

        public bool PrepareInitialDeadline(TimeOfDay timeOfDay)
        {
            if (!ShouldHandle(timeOfDay))
            {
                return false;
            }

            try
            {
                var saveFile = CurrentSaveFile;
                if (string.IsNullOrWhiteSpace(saveFile))
                {
                    return false;
                }

                if (!_stateRepository.IsInitialized(saveFile))
                {
                    _stateRepository.Initialize(saveFile);
                }

                if (timeOfDay.timesFulfilledQuota > 0)
                {
                    return false;
                }

                var result = CalculateInitialDeadline();
                var desiredFullDeadline = CalculateDeadlineTime(
                    timeOfDay,
                    result.DeadlineDays);

                var previouslyAppliedFullDeadline =
                    _stateRepository.LoadInitialFullDeadlineTime(saveFile);

                var sourceFullDeadline = previouslyAppliedFullDeadline > 0
                    ? previouslyAppliedFullDeadline
                    : CalculateDeadlineTime(
                        timeOfDay,
                        timeOfDay.quotaVariables.deadlineDaysAmount);

                if (previouslyAppliedFullDeadline == desiredFullDeadline &&
                    timeOfDay.timeUntilDeadline <= desiredFullDeadline + 1f)
                {
                    return false;
                }

                var elapsedTime = Math.Max(
                    0f,
                    sourceFullDeadline - timeOfDay.timeUntilDeadline);

                var migratedRemainingTime = Math.Max(
                    0,
                    (int)Math.Min(
                        int.MaxValue,
                        desiredFullDeadline - elapsedTime));

                timeOfDay.quotaVariables.deadlineDaysAmount =
                    result.DeadlineDays;
                timeOfDay.timeUntilDeadline = migratedRemainingTime;
                ES3.Save("DeadlineTime", migratedRemainingTime, saveFile);
                _stateRepository.SaveInitialFullDeadlineTime(
                    saveFile,
                    desiredFullDeadline);

                LogDeadline(
                    "initial quota reconciliation",
                    0,
                    result,
                    DeadlineManagerState.Empty);

                _logger.LogDebug(
                    $"Initial deadline reconciled in UpdateProfitQuotaCurrentTime: sourceFull={sourceFullDeadline}, desiredFull={desiredFullDeadline}, elapsed={elapsedTime:0.##}, remaining={migratedRemainingTime}.");

                return true;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    $"DeadlineManager could not reconcile the initial deadline before UpdateProfitQuotaCurrentTime. Vanilla deadline state remains available. {exception}");
                return false;
            }
        }

        public void FinalizeDeadlineRefresh(TimeOfDay timeOfDay)
        {
            if (!ShouldHandle(timeOfDay))
            {
                return;
            }

            timeOfDay.SetBuyingRateForDay();

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.DisplayDaysLeft(
                    timeOfDay.daysUntilDeadline);
            }
        }

        public void InitializeResetSave(GameNetworkManager gameNetworkManager)
        {
            if (gameNetworkManager == null ||
                !gameNetworkManager.isHostingGame ||
                (StartOfRound.Instance != null && StartOfRound.Instance.isChallengeFile))
            {
                return;
            }

            try
            {
                var saveFile = gameNetworkManager.currentSaveFileName;
                _stateRepository.Initialize(saveFile);

                var timeOfDay = TimeOfDay.Instance;
                if (timeOfDay == null)
                {
                    return;
                }

                var result = CalculateInitialDeadline();
                var deadlineTime = CalculateDeadlineTime(timeOfDay, result.DeadlineDays);

                ES3.Save("DeadlineTime", deadlineTime, saveFile);
                timeOfDay.quotaVariables.deadlineDaysAmount =
                    result.DeadlineDays;
                timeOfDay.timeUntilDeadline = deadlineTime;
                _stateRepository.SaveInitialFullDeadlineTime(
                    saveFile,
                    deadlineTime);

                LogDeadline("save reset", 0, result, DeadlineManagerState.Empty);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    $"DeadlineManager could not initialize the reset save deadline. Vanilla reset values remain available. {exception}");
            }
        }

        public void ApplyResetShipDeadline(TimeOfDay timeOfDay)
        {
            if (!ShouldHandle(timeOfDay))
            {
                return;
            }

            try
            {
                var result = CalculateInitialDeadline();
                ApplyDeadline(timeOfDay, result, syncClients: true);
                LogDeadline("ship reset", 0, result, DeadlineManagerState.Empty);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    $"DeadlineManager could not apply the ship-reset deadline. Vanilla reset values remain in place. {exception}");
            }
        }

        public void PersistCurrentDeadline(GameNetworkManager gameNetworkManager)
        {
            var timeOfDay = TimeOfDay.Instance;
            if (gameNetworkManager == null ||
                !gameNetworkManager.isHostingGame ||
                !ShouldHandle(timeOfDay) ||
                string.IsNullOrWhiteSpace(gameNetworkManager.currentSaveFileName))
            {
                return;
            }

            try
            {
                var deadlineTime = Math.Max(
                    0,
                    (int)Math.Min(
                        int.MaxValue,
                        timeOfDay.timeUntilDeadline));

                ES3.Save(
                    "DeadlineTime",
                    deadlineTime,
                    gameNetworkManager.currentSaveFileName);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    $"Could not preserve the current DeadlineManager deadline while saving. Vanilla's deadline save remains available. {exception.Message}");
            }
        }

        private DeadlineDaysCalculationResult CalculateInitialDeadline()
        {
            return DeadlineDaysCalculator.Calculate(
                _settings,
                quotasCompleted: 0,
                DeadlineManagerState.Empty);
        }

        private void ApplyDeadline(
            TimeOfDay timeOfDay,
            DeadlineDaysCalculationResult result,
            bool syncClients)
        {
            var deadlineTime = CalculateDeadlineTime(timeOfDay, result.DeadlineDays);
            timeOfDay.quotaVariables.deadlineDaysAmount =
                result.DeadlineDays;
            timeOfDay.timeUntilDeadline = deadlineTime;
            timeOfDay.UpdateProfitQuotaCurrentTime();
            timeOfDay.SetBuyingRateForDay();

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.DisplayDaysLeft(
                    timeOfDay.daysUntilDeadline);
            }

            if (syncClients)
            {
                timeOfDay.SyncTimeClientRpc(timeOfDay.globalTime, deadlineTime);
            }
        }

        private static int CalculateDeadlineTime(TimeOfDay timeOfDay, int deadlineDays)
        {
            var deadlineTime = timeOfDay.totalTime * deadlineDays;

            if (deadlineTime >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return Math.Max(0, (int)deadlineTime);
        }

        private void LogDeadline(
            string reason,
            int quotasCompleted,
            DeadlineDaysCalculationResult result,
            DeadlineManagerState state)
        {
            var dynamicText = result.DynamicAdjustmentApplied
                ? $", dynamic offset {result.PerformanceOffset:+0.###;-0.###;0}, adjusted {result.AdjustedDays:0.###}"
                : _settings.DynamicAdjustmentEnabled &&
                  _settings.BaselineMode != DeadlineBaselineMode.Static
                    ? $", dynamic calibration {state.CalibrationSamples.Count}/{DeadlineManagerState.CalibrationSampleTarget}"
                    : string.Empty;

            _logger.LogInfo(
                $"Applied {result.DeadlineDays}-day deadline after {reason} (baseline {_settings.BaselineMode}, quotas completed {quotasCompleted}, baseline {result.BaselineDays:0.###}{dynamicText}).");
        }

        private static string CurrentSaveFile =>
            GameNetworkManager.Instance == null
                ? string.Empty
                : GameNetworkManager.Instance.currentSaveFileName;
    }
}
