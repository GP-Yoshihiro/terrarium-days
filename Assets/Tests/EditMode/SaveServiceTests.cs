using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TerrariumDays.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace TerrariumDays.Tests
{
    public sealed class SaveServiceTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

        private SaveService saveService;
        private string filePath;

        [SetUp]
        public void SetUp()
        {
            saveService = new SaveService();
            filePath = Path.Combine(Path.GetTempPath(), $"terrarium-save-test-{Guid.NewGuid():N}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        [Test]
        public void LoadOrCreateDefault_WhenFileDoesNotExist_ReturnsDefaultsAndPersistsImmediately()
        {
            Assert.That(File.Exists(filePath), Is.False);

            var state = saveService.LoadOrCreateDefault(filePath, Now);

            Assert.That(state.Hunger, Is.EqualTo(80d));
            Assert.That(state.Hydration, Is.EqualTo(80d));
            Assert.That(state.Cleanliness, Is.EqualTo(80d));
            Assert.That(state.Health, Is.EqualTo(100d));
            Assert.That(state.Growth, Is.EqualTo(0d));
            Assert.That(state.SelectedDecorId, Is.EqualTo(PetState.DefaultDecorId));
            Assert.That(state.LastSavedAtUtc, Is.EqualTo(Now));
            Assert.That(File.Exists(filePath), Is.True);
        }

        [Test]
        public void SaveThenLoadOrCreateDefault_RoundTripsEveryProperty()
        {
            var original = new PetState
            {
                Hunger = 55d,
                Hydration = 42d,
                Cleanliness = 91d,
                Health = 73d,
                Growth = 65d,
                SelectedDecorId = "lamp_02",
                UnlockedDecorIds = new List<string> { "rock_01", "lamp_02", "vine_03" },
                LastSavedAtUtc = Now
            };

            saveService.Save(filePath, original);
            var loaded = saveService.LoadOrCreateDefault(filePath, Now + TimeSpan.FromHours(1));

            Assert.That(loaded.Hunger, Is.EqualTo(original.Hunger));
            Assert.That(loaded.Hydration, Is.EqualTo(original.Hydration));
            Assert.That(loaded.Cleanliness, Is.EqualTo(original.Cleanliness));
            Assert.That(loaded.Health, Is.EqualTo(original.Health));
            Assert.That(loaded.Growth, Is.EqualTo(original.Growth));
            Assert.That(loaded.GrowthStage, Is.EqualTo(GrowthStage.Juvenile));
            Assert.That(loaded.SelectedDecorId, Is.EqualTo(original.SelectedDecorId));
            Assert.That(loaded.UnlockedDecorIds, Is.EqualTo(original.UnlockedDecorIds));
            Assert.That(loaded.LastSavedAtUtc, Is.EqualTo(original.LastSavedAtUtc));
        }

        [Test]
        public void LoadOrCreateDefault_WhenFileIsCorrupt_ReturnsDefaultsAndLeavesFileUntouched()
        {
            const string corruptContent = "not valid json {{{";
            File.WriteAllText(filePath, corruptContent);
            LogAssert.Expect(LogType.Error, new Regex("^Save data at .* could not be loaded"));

            var state = saveService.LoadOrCreateDefault(filePath, Now);

            Assert.That(state.Hunger, Is.EqualTo(80d));
            Assert.That(state.SelectedDecorId, Is.EqualTo(PetState.DefaultDecorId));
            Assert.That(File.ReadAllText(filePath), Is.EqualTo(corruptContent));
        }

        [Test]
        public void LoadOrCreateDefault_WhenStoredValuesAreOutOfRange_ClampsOnLoad()
        {
            var outOfRangeData = new PetSaveData
            {
                schemaVersion = 1,
                lastSavedAtUtc = Now.ToString("o", CultureInfo.InvariantCulture),
                hunger = 150d,
                hydration = -20d,
                cleanliness = 50d,
                health = 100d,
                growth = 0d,
                growthStage = GrowthStage.Baby.ToString(),
                selectedDecorId = PetState.DefaultDecorId,
                unlockedDecorIds = new List<string> { PetState.DefaultDecorId }
            };
            File.WriteAllText(filePath, JsonUtility.ToJson(outOfRangeData));

            var state = saveService.LoadOrCreateDefault(filePath, Now);

            Assert.That(state.Hunger, Is.EqualTo(100d));
            Assert.That(state.Hydration, Is.EqualTo(0d));
        }
    }
}
