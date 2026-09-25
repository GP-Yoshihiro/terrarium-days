using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace TerrariumDays.Core
{
    /// <summary>
    /// JSON persistence for PetState. Caller supplies the file path and current time so
    /// this stays deterministic and EditMode testable. See Terrarium_Days_仕様書.md section 8.
    /// </summary>
    public sealed class SaveService
    {
        private const int CurrentSchemaVersion = 2;
        private const string TimestampFormat = "o";

        public PetState LoadOrCreateDefault(string filePath, DateTimeOffset nowUtc)
        {
            if (!File.Exists(filePath))
            {
                var createdState = NewState(nowUtc);
                Save(filePath, createdState);
                return createdState;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                var data = JsonUtility.FromJson<PetSaveData>(json);
                return FromSaveData(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Save data at '{filePath}' could not be loaded ({ex.Message}); starting from a fresh state without overwriting the file.");
                return NewState(nowUtc);
            }
        }

        private static PetState NewState(DateTimeOffset nowUtc)
        {
            return new PetState
            {
                LastSavedAtUtc = nowUtc,
                LastShedAtUtc = nowUtc,
                NextShedAtUtc = nowUtc.AddDays(new CareTuning().ShedIntervalDays),
            };
        }

        public void Save(string filePath, PetState state)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonUtility.ToJson(ToSaveData(state));
            File.WriteAllText(filePath, json);
        }

        private static PetState FromSaveData(PetSaveData data)
        {
            if (data == null)
            {
                throw new InvalidDataException("Save data deserialized to null.");
            }

            var lastSaved = DateTimeOffset.ParseExact(data.lastSavedAtUtc, TimestampFormat, CultureInfo.InvariantCulture);
            // Schema-1 saves have no shedding schedule: start one interval after the last save.
            var lastShed = ParseOr(data.lastShedAtUtc, lastSaved);
            var nextShed = ParseOr(data.nextShedAtUtc, lastSaved.AddDays(new CareTuning().ShedIntervalDays));

            return new PetState
            {
                Hunger = data.hunger,
                Hydration = data.hydration,
                Cleanliness = data.cleanliness,
                Health = data.health,
                Growth = data.growth,
                SelectedDecorId = string.IsNullOrEmpty(data.selectedDecorId) ? PetState.DefaultDecorId : data.selectedDecorId,
                UnlockedDecorIds = data.unlockedDecorIds ?? new List<string> { PetState.DefaultDecorId },
                LastSavedAtUtc = lastSaved,
                LastShedAtUtc = lastShed,
                NextShedAtUtc = nextShed,
            };
        }

        private static DateTimeOffset ParseOr(string value, DateTimeOffset fallback)
        {
            return string.IsNullOrEmpty(value)
                ? fallback
                : DateTimeOffset.ParseExact(value, TimestampFormat, CultureInfo.InvariantCulture);
        }

        private static PetSaveData ToSaveData(PetState state)
        {
            return new PetSaveData
            {
                schemaVersion = CurrentSchemaVersion,
                lastSavedAtUtc = state.LastSavedAtUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture),
                hunger = state.Hunger,
                hydration = state.Hydration,
                cleanliness = state.Cleanliness,
                health = state.Health,
                growth = state.Growth,
                growthStage = state.GrowthStage.ToString(),
                selectedDecorId = state.SelectedDecorId,
                unlockedDecorIds = state.UnlockedDecorIds,
                lastShedAtUtc = state.LastShedAtUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture),
                nextShedAtUtc = state.NextShedAtUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture),
            };
        }
    }
}
