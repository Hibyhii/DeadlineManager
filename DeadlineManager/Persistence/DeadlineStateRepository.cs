using System;
using System.Collections.Generic;
using BepInEx.Logging;
using DynamicDeadlineMod.Core;

namespace DynamicDeadlineMod.Persistence
{
    internal sealed class DeadlineStateRepository
    {
        private const int CurrentVersion = 1;
        private const string Prefix = DeadlineManager.ModGuid + ".DeadlineState.";
        private const string VersionKey = Prefix + "Version";
        private const string InitialFullDeadlineTimeKey = Prefix + "InitialFullDeadlineTime";
        private const string CalibrationCountKey = Prefix + "Calibration.Count";
        private const string RecentCountKey = Prefix + "Recent.Count";

        private readonly ManualLogSource _logger;

        public DeadlineStateRepository(ManualLogSource logger)
        {
            _logger = logger;
        }

        public DeadlineManagerState Load(string saveFile)
        {
            if (string.IsNullOrWhiteSpace(saveFile) ||
                !ES3.KeyExists(VersionKey, saveFile))
            {
                return DeadlineManagerState.Empty;
            }

            try
            {
                var version = ES3.Load(VersionKey, saveFile, 0);
                if (version != CurrentVersion)
                {
                    _logger.LogWarning(
                        $"Unsupported DeadlineManager save-state version {version}. Starting a new calibration period.");
                    Initialize(saveFile);
                    return DeadlineManagerState.Empty;
                }

                var calibrationCount = ES3.Load(CalibrationCountKey, saveFile, 0);
                var recentCount = ES3.Load(RecentCountKey, saveFile, 0);

                if (calibrationCount < 0 ||
                    calibrationCount > DeadlineManagerState.CalibrationSampleTarget ||
                    recentCount < 0 ||
                    recentCount > DeadlineManagerState.RecentSampleCapacity ||
                    (calibrationCount < DeadlineManagerState.CalibrationSampleTarget &&
                     recentCount != 0))
                {
                    throw new InvalidOperationException("Saved performance-history counts are invalid.");
                }

                var calibration = LoadSamples(saveFile, "Calibration", calibrationCount);
                var recent = LoadSamples(saveFile, "Recent", recentCount);

                return new DeadlineManagerState(calibration, recent);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    $"Could not load DeadlineManager performance history from '{saveFile}'. Starting a new calibration period. {exception.Message}");
                Initialize(saveFile);
                return DeadlineManagerState.Empty;
            }
        }

        public bool IsInitialized(string saveFile)
        {
            return !string.IsNullOrWhiteSpace(saveFile) &&
                   ES3.KeyExists(VersionKey, saveFile);
        }

        public int LoadInitialFullDeadlineTime(string saveFile)
        {
            if (string.IsNullOrWhiteSpace(saveFile))
            {
                return 0;
            }

            return ES3.Load(InitialFullDeadlineTimeKey, saveFile, 0);
        }

        public void SaveInitialFullDeadlineTime(
            string saveFile,
            int fullDeadlineTime)
        {
            if (string.IsNullOrWhiteSpace(saveFile))
            {
                return;
            }

            try
            {
                ES3.Save(
                    InitialFullDeadlineTimeKey,
                    Math.Max(0, fullDeadlineTime),
                    saveFile);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    $"Could not save the initial DeadlineManager duration in '{saveFile}'. {exception.Message}");
            }
        }

        public void Save(string saveFile, DeadlineManagerState state)
        {
            if (string.IsNullOrWhiteSpace(saveFile))
            {
                return;
            }

            try
            {
                SaveCore(saveFile, state);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    $"Could not save DeadlineManager performance history to '{saveFile}'. The current session will continue, but this history may be lost after restart. {exception.Message}");
            }
        }

        public void Initialize(string saveFile)
        {
            if (string.IsNullOrWhiteSpace(saveFile))
            {
                return;
            }

            try
            {
                DeleteAllKeys(saveFile);
                SaveCore(saveFile, DeadlineManagerState.Empty);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    $"Could not initialize DeadlineManager performance history in '{saveFile}'. {exception.Message}");
            }
        }

        private static void SaveCore(
            string saveFile,
            DeadlineManagerState state)
        {
            ES3.Save(VersionKey, CurrentVersion, saveFile);
            if (!ES3.KeyExists(InitialFullDeadlineTimeKey, saveFile))
            {
                ES3.Save(InitialFullDeadlineTimeKey, 0, saveFile);
            }
            ES3.Save(CalibrationCountKey, state.CalibrationSamples.Count, saveFile);
            ES3.Save(RecentCountKey, state.RecentSamples.Count, saveFile);

            SaveSamples(
                saveFile,
                "Calibration",
                state.CalibrationSamples,
                DeadlineManagerState.CalibrationSampleTarget);

            SaveSamples(
                saveFile,
                "Recent",
                state.RecentSamples,
                DeadlineManagerState.RecentSampleCapacity);
        }

        private static List<QuotaPerformanceSample> LoadSamples(
            string saveFile,
            string group,
            int count)
        {
            var samples = new List<QuotaPerformanceSample>(count);

            for (var index = 0; index < count; index++)
            {
                var required = ES3.Load(
                    SampleKey(group, index, "Required"),
                    saveFile,
                    double.NaN);

                var fulfilled = ES3.Load(
                    SampleKey(group, index, "Fulfilled"),
                    saveFile,
                    double.NaN);

                samples.Add(new QuotaPerformanceSample(required, fulfilled));
            }

            return samples;
        }

        private static void SaveSamples(
            string saveFile,
            string group,
            IReadOnlyList<QuotaPerformanceSample> samples,
            int capacity)
        {
            for (var index = 0; index < samples.Count; index++)
            {
                ES3.Save(
                    SampleKey(group, index, "Required"),
                    samples[index].QuotaRequired,
                    saveFile);

                ES3.Save(
                    SampleKey(group, index, "Fulfilled"),
                    samples[index].QuotaFulfilled,
                    saveFile);
            }

            for (var index = samples.Count; index < capacity; index++)
            {
                DeleteKeyIfPresent(saveFile, SampleKey(group, index, "Required"));
                DeleteKeyIfPresent(saveFile, SampleKey(group, index, "Fulfilled"));
            }
        }

        private static void DeleteAllKeys(string saveFile)
        {
            DeleteKeyIfPresent(saveFile, VersionKey);
            DeleteKeyIfPresent(saveFile, InitialFullDeadlineTimeKey);
            DeleteKeyIfPresent(saveFile, Prefix + "InitialDeadlineApplied");
            DeleteKeyIfPresent(saveFile, CalibrationCountKey);
            DeleteKeyIfPresent(saveFile, RecentCountKey);

            for (var index = 0; index < DeadlineManagerState.CalibrationSampleTarget; index++)
            {
                DeleteKeyIfPresent(saveFile, SampleKey("Calibration", index, "Required"));
                DeleteKeyIfPresent(saveFile, SampleKey("Calibration", index, "Fulfilled"));
            }

            for (var index = 0; index < DeadlineManagerState.RecentSampleCapacity; index++)
            {
                DeleteKeyIfPresent(saveFile, SampleKey("Recent", index, "Required"));
                DeleteKeyIfPresent(saveFile, SampleKey("Recent", index, "Fulfilled"));
            }
        }

        private static string SampleKey(string group, int index, string valueName)
        {
            return $"{Prefix}{group}.{index}.{valueName}";
        }

        private static void DeleteKeyIfPresent(string saveFile, string key)
        {
            if (ES3.KeyExists(key, saveFile))
            {
                ES3.DeleteKey(key, saveFile);
            }
        }
    }
}
