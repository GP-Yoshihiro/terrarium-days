using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;
using TerrariumDays.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace TerrariumDays.Tests
{
    public sealed class TerrariumViewTests
    {
        private GameObject gameObject;
        private TerrariumView view;

        private Label growthStageLabel;
        private VisualElement growthGaugeFill;
        private VisualElement hungerBarFill;
        private Label hungerValueLabel;
        private VisualElement hydrationBarFill;
        private Label hydrationValueLabel;
        private VisualElement cleanlinessBarFill;
        private Label cleanlinessValueLabel;
        private VisualElement healthBarFill;
        private Label healthValueLabel;
        private Label feedbackLabel;
        private Button feedButton;
        private Button waterButton;
        private Button cleanButton;
        private VisualElement milestoneModal;
        private Label milestoneStageLabel;
        private Label milestoneMessageLabel;
        private Button milestoneContinueButton;
        private VisualElement decorImageElement;
        private Button decorButton;
        private VisualElement decorDrawer;
        private Button decorDrawerCloseButton;
        private readonly Dictionary<string, Button> decorRowButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Label> decorStatusLabels = new Dictionary<string, Label>();
        private VisualElement topBar;
        private Button debugButton;
        private VisualElement debugPanel;
        private Label debugAppliedElapsedLabel;
        private Slider debugWeightSlider;
        private readonly List<string> tempSavePaths = new List<string>();

        private string CreateTempSavePath()
        {
            var path = Path.Combine(Path.GetTempPath(), $"terrarium-view-test-{Guid.NewGuid():N}.json");
            tempSavePaths.Add(path);
            return path;
        }

        private string SaveColonyWith(PetState pet, DateTimeOffset nowUtc)
        {
            var path = CreateTempSavePath();
            var colony = Colony.CreateNew(nowUtc, new EconomyTuning(), new CareTuning(), new System.Random(1));
            var cage = colony.Cages[0];
            colony.Animals.Clear();
            cage.AnimalId = -1;
            colony.NextAnimalId = 1;
            colony.AddAnimal(pet, cage);
            new ColonySaveService(new CareTuning(), new EconomyTuning()).Save(path, colony);
            return path;
        }

        [SetUp]
        public void SetUp()
        {
            // Keep the GameObject inactive so RequireComponent(UIDocument) does not fire
            // TerrariumView.Awake() against an unconfigured UIDocument; this test drives
            // BindElements/Render directly against a hand-built tree instead (see the
            // unity-playmode-integration-test skill: build the minimal graph from code).
            gameObject = new GameObject("TerrariumViewUnderTest");
            gameObject.SetActive(false);
            view = gameObject.AddComponent<TerrariumView>();

            var root = new VisualElement();

            growthStageLabel = new Label { name = "growth-stage-label" };
            growthGaugeFill = new VisualElement { name = "growth-gauge-fill" };
            hungerBarFill = new VisualElement { name = "hunger-bar-fill" };
            hungerValueLabel = new Label { name = "hunger-value-label" };
            hydrationBarFill = new VisualElement { name = "hydration-bar-fill" };
            hydrationValueLabel = new Label { name = "hydration-value-label" };
            cleanlinessBarFill = new VisualElement { name = "cleanliness-bar-fill" };
            cleanlinessValueLabel = new Label { name = "cleanliness-value-label" };
            healthBarFill = new VisualElement { name = "health-bar-fill" };
            healthValueLabel = new Label { name = "health-value-label" };
            feedbackLabel = new Label { name = "feedback-label" };
            feedButton = new Button { name = "feed-button" };
            waterButton = new Button { name = "water-button" };
            cleanButton = new Button { name = "clean-button" };
            milestoneModal = new VisualElement { name = "milestone-modal" };
            milestoneStageLabel = new Label { name = "milestone-stage-label" };
            milestoneMessageLabel = new Label { name = "milestone-message-label" };
            milestoneContinueButton = new Button { name = "milestone-continue-button" };
            milestoneModal.Add(milestoneStageLabel);
            milestoneModal.Add(milestoneMessageLabel);
            milestoneModal.Add(milestoneContinueButton);

            decorImageElement = new VisualElement { name = "decor-image" };
            decorButton = new Button { name = "decor-button" };
            decorDrawer = new VisualElement { name = "decor-drawer" };
            decorDrawerCloseButton = new Button { name = "decor-drawer-close-button" };
            decorRowButtons.Clear();
            decorStatusLabels.Clear();
            foreach (var decor in DecorCatalog.All)
            {
                var rowButton = new Button { name = $"decor-row-{decor.Id}" };
                var statusLabel = new Label { name = $"decor-status-{decor.Id}" };
                rowButton.Add(statusLabel);
                decorDrawer.Add(rowButton);
                decorRowButtons[decor.Id] = rowButton;
                decorStatusLabels[decor.Id] = statusLabel;
            }
            decorDrawer.Add(decorDrawerCloseButton);

            topBar = new VisualElement { name = "top-bar" };
            debugButton = new Button { name = "debug-button" };
            debugPanel = new VisualElement { name = "debug-panel" };
            debugAppliedElapsedLabel = new Label { name = "debug-applied-elapsed-label" };
            debugWeightSlider = new Slider { name = "debug-weight-slider", lowValue = 0f, highValue = 80f };
            debugPanel.Add(debugAppliedElapsedLabel);
            debugPanel.Add(debugWeightSlider);

            root.Add(growthStageLabel);
            root.Add(growthGaugeFill);
            root.Add(hungerBarFill);
            root.Add(hungerValueLabel);
            root.Add(hydrationBarFill);
            root.Add(hydrationValueLabel);
            root.Add(cleanlinessBarFill);
            root.Add(cleanlinessValueLabel);
            root.Add(healthBarFill);
            root.Add(healthValueLabel);
            root.Add(feedbackLabel);
            root.Add(feedButton);
            root.Add(waterButton);
            root.Add(cleanButton);
            root.Add(milestoneModal);
            root.Add(decorImageElement);
            root.Add(decorButton);
            root.Add(decorDrawer);
            root.Add(topBar);
            root.Add(debugButton);
            root.Add(debugPanel);

            view.BindElements(root);

            // Activate now that the hand-built tree is bound: TerrariumView.Awake() is
            // guarded to skip real-UIDocument rebinding once already initialized, so this
            // only enables StartCoroutine (used by the feedback auto-hide) to work like it
            // would in a real, active scene instead of erroring on an inactive object.
            view.Initialize(new PetState(), new CareTuning());
            gameObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(gameObject);

            foreach (var path in tempSavePaths)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            tempSavePaths.Clear();
        }

        [Test]
        public void Render_ShowsTheJapaneseStageWithWeightAndTheProgressToTheNextStage()
        {
            var state = new PetState { Stage = GrowthStage.Baby, WeightGrams = 9d };

            view.Render(state, new CareTuning());

            Assert.That(growthStageLabel.text, Is.EqualTo("ベビー 9.0g"));
            Assert.That(growthGaugeFill.style.width.value.value, Is.EqualTo(50f).Within(0.01f));
        }

        [Test]
        public void Render_DisplaysIntegerRoundedStatusValues()
        {
            var state = new PetState { Hunger = 67.6d };

            view.Render(state, new CareTuning());

            Assert.That(hungerValueLabel.text, Is.EqualTo("68"));
            Assert.That(hungerBarFill.style.width.value.value, Is.EqualTo(67.6f).Within(0.01f));
        }

        [Test]
        public void Render_AppliesGoodClassWhenStatusIsAtOrAboveHealthyThreshold()
        {
            var state = new PetState { Hunger = 40d };

            view.Render(state, new CareTuning());

            Assert.That(hungerBarFill.ClassListContains("status-good"), Is.True);
            Assert.That(hungerBarFill.ClassListContains("status-warning"), Is.False);
            Assert.That(hungerBarFill.ClassListContains("status-critical"), Is.False);
        }

        [Test]
        public void Render_AppliesWarningClassWhenStatusIsInTheMiddleBand()
        {
            var state = new PetState { Hydration = 25d };

            view.Render(state, new CareTuning());

            Assert.That(hydrationBarFill.ClassListContains("status-warning"), Is.True);
            Assert.That(hydrationBarFill.ClassListContains("status-good"), Is.False);
            Assert.That(hydrationBarFill.ClassListContains("status-critical"), Is.False);
        }

        [Test]
        public void Render_AppliesCriticalClassWhenStatusIsBelowLowThreshold()
        {
            var state = new PetState { Cleanliness = 10d };

            view.Render(state, new CareTuning());

            Assert.That(cleanlinessBarFill.ClassListContains("status-critical"), Is.True);
            Assert.That(cleanlinessBarFill.ClassListContains("status-good"), Is.False);
            Assert.That(cleanlinessBarFill.ClassListContains("status-warning"), Is.False);
        }

        [Test]
        public void Render_CalledTwice_SwapsTheStatusClassRatherThanStackingIt()
        {
            var healthyState = new PetState { Health = 90d };
            var criticalState = new PetState { Health = 5d };

            view.Render(healthyState, new CareTuning());
            view.Render(criticalState, new CareTuning());

            Assert.That(healthBarFill.ClassListContains("status-critical"), Is.True);
            Assert.That(healthBarFill.ClassListContains("status-good"), Is.False);
        }

        [Test]
        public void OnFeedClicked_IncreasesOnlyHungerAndShowsTheSuccessMessage()
        {
            view.Initialize(new PetState { Hunger = 50d, Hydration = 50d }, new CareTuning());

            view.OnFeedClicked();

            Assert.That(view.State.Hunger, Is.EqualTo(80d));
            Assert.That(view.State.Hydration, Is.EqualTo(50d));
            Assert.That(feedbackLabel.text, Is.EqualTo("ごはんを食べた！"));
            Assert.That(feedbackLabel.style.visibility.value, Is.EqualTo(Visibility.Visible));
            Assert.That(hungerValueLabel.text, Is.EqualTo("80"));
        }

        [Test]
        public void OnRefreshWaterClicked_WhenHydrationWasAlreadyAtMaximum_ShowsTheAlreadyFullMessage()
        {
            view.Initialize(new PetState { Hydration = 100d }, new CareTuning());

            view.OnRefreshWaterClicked();

            Assert.That(view.State.Hydration, Is.EqualTo(100d));
            Assert.That(feedbackLabel.text, Is.EqualTo(CareFeedbackMessage.AlreadyFullMessage));
        }

        [Test]
        public void OnCleanClicked_IncreasesOnlyCleanlinessAndShowsTheSuccessMessage()
        {
            view.Initialize(new PetState { Cleanliness = 40d, Health = 90d }, new CareTuning());

            view.OnCleanClicked();

            Assert.That(view.State.Cleanliness, Is.EqualTo(80d));
            Assert.That(view.State.Health, Is.EqualTo(90d));
            Assert.That(feedbackLabel.text, Is.EqualTo("テラリウムがきれい！"));
            Assert.That(cleanlinessValueLabel.text, Is.EqualTo("80"));
        }

        [Test]
        public void LoadColony_WhenNoSaveFileExists_LoadsDefaultsAndKeepsTheModalHidden()
        {
            var path = CreateTempSavePath();

            view.LoadColony(path, DateTimeOffset.UtcNow);

            Assert.That(view.State.Hunger, Is.EqualTo(80d));
            Assert.That(milestoneModal.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void LoadColony_WhenElapsedTimeCrossesAStageBoundary_ShowsTheMilestoneModal()
        {
            var care = new CareTuning();
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var savedState = new PetState
            {
                Hunger = 90d,
                Hydration = 90d,
                Cleanliness = 90d,
                WeightGrams = 15.5d,
                NextShedAtUtc = nowUtc.AddDays(3),
                LastSavedAtUtc = nowUtc - GameCalendar.RealTimeFor(care.PreGrowthFastGameDays) - TimeSpan.FromMinutes(5)
            };
            var path = SaveColonyWith(savedState, nowUtc);

            view.LoadColony(path, nowUtc);

            Assert.That(view.State.GrowthStage, Is.EqualTo(GrowthStage.Juvenile));
            Assert.That(milestoneModal.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(milestoneStageLabel.text, Is.EqualTo(GrowthModel.StageLabel(GrowthStage.Juvenile)));
        }

        [Test]
        public void OnMilestoneContinueClicked_HidesTheModal()
        {
            view.ShowMilestoneModal(GrowthStage.Adult);

            view.OnMilestoneContinueClicked();

            Assert.That(milestoneModal.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void OnFeedClicked_AfterLoadColony_PersistsTheUpdatedValue()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, DateTimeOffset.UtcNow);

            view.OnFeedClicked();

            var reloaded = new ColonySaveService(new CareTuning(), new EconomyTuning())
                .LoadOrCreate(path, DateTimeOffset.UtcNow, new System.Random(1)).Animals[0];
            Assert.That(reloaded.Hunger, Is.EqualTo(100d));
        }

        [Test]
        public void OnFeedClicked_ForAFullPet_ShowsAlreadyFullAndDoesNotChargeMoney()
        {
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var tuning = new CareTuning();
            var path = SaveColonyWith(new PetState
            {
                Hunger = tuning.FeedFullThreshold,
                LastSavedAtUtc = nowUtc,
                NextShedAtUtc = nowUtc.AddDays(3),
            }, nowUtc);
            view.LoadColony(path, nowUtc, new TimeService(() => nowUtc));
            var moneyBefore = view.Session.Colony.Wallet.Money;

            view.OnFeedClicked();

            Assert.That(feedbackLabel.text, Is.EqualTo(CareFeedbackMessage.AlreadyFullMessage));
            Assert.That(view.Session.Colony.Wallet.Money, Is.EqualTo(moneyBefore));
            Assert.That(view.State.Hunger, Is.EqualTo(tuning.FeedFullThreshold));
        }

        [Test]
        public void OnDecorButtonClicked_ShowsTheDrawer()
        {
            view.OnDecorButtonClicked();

            Assert.That(decorDrawer.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void OnDecorButtonClicked_ALockedRowIsDisabledAndShowsItsUnlockCondition()
        {
            view.OnDecorButtonClicked();

            var lockedRow = decorRowButtons["plant_01"];
            Assert.That(lockedRow.enabledSelf, Is.False);
            Assert.That(decorStatusLabels["plant_01"].text, Is.EqualTo($"{GrowthStage.Juvenile}で解放"));
        }

        [Test]
        public void OnDecorRowClicked_ForALockedDecor_DoesNothing()
        {
            view.OnDecorRowClicked("plant_01");

            Assert.That(view.State.SelectedDecorId, Is.EqualTo(PetState.DefaultDecorId));
        }

        [Test]
        public void OnDecorRowClicked_ForAnUnlockedDecor_SelectsItAndUpdatesTheDecorImage()
        {
            DecorUnlockService.GrantUnlocksForStage(view.State, GrowthStage.Juvenile);

            view.OnDecorRowClicked("plant_01");

            Assert.That(view.State.SelectedDecorId, Is.EqualTo("plant_01"));
            Assert.That(decorImageElement.ClassListContains("decor-icon-plant_01"), Is.True);
            Assert.That(decorImageElement.ClassListContains($"decor-icon-{PetState.DefaultDecorId}"), Is.False);
        }

        [Test]
        public void OnDecorDrawerCloseClicked_HidesTheDrawer()
        {
            view.OnDecorButtonClicked();

            view.OnDecorDrawerCloseClicked();

            Assert.That(decorDrawer.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ApplyDebugVisibility_TogglesTheDebugButton()
        {
            view.ApplyDebugVisibility(true);
            Assert.That(debugButton.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            view.ApplyDebugVisibility(false);
            Assert.That(debugButton.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ApplyDebugVisibility_TogglesTheTopBarDebugClass_SoTheNextCageButtonStaysClearOfIt()
        {
            // The debug button is absolutely positioned over the top bar; the
            // top-bar-with-debug class (Terrarium.uss) reserves room for it so it never
            // covers the next-cage button, but only when the debug button is actually shown.
            view.ApplyDebugVisibility(true);
            Assert.That(topBar.ClassListContains("top-bar-with-debug"), Is.True,
                "debug builds must reserve room for the debug button next to the cage-nav row");

            view.ApplyDebugVisibility(false);
            Assert.That(topBar.ClassListContains("top-bar-with-debug"), Is.False,
                "release builds must not reserve unused room since the debug button is hidden");
        }

        [Test]
        public void ApplyLiveTickDelta_AtSixtyTimesMultiplier_AppliesOneHourOfProgress()
        {
            var path = CreateTempSavePath();
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            view.LoadColony(path, nowUtc);
            view.OnDebugMultiplierClicked(60d);

            view.ApplyLiveTickDelta(TimeSpan.FromMinutes(1));

            var tuning = new CareTuning();
            Assert.That(view.State.Hunger, Is.EqualTo(80d - tuning.HungerDecayPerHour).Within(1e-6));
            Assert.That(view.State.WeightGrams, Is.EqualTo(3d).Within(1e-6), "weight only changes from feeding or fasting, neither of which applies here");
        }

        [Test]
        public void ApplyLiveTickDelta_AccumulatesSubMinuteRemaindersAcrossCalls()
        {
            var path = CreateTempSavePath();
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            view.LoadColony(path, nowUtc);
            view.OnDebugMultiplierClicked(40d);

            view.ApplyLiveTickDelta(TimeSpan.FromSeconds(1));
            Assert.That(view.State.Hunger, Is.EqualTo(80d), "a single 40-scaled-second tick must not yet apply a minute step");

            view.ApplyLiveTickDelta(TimeSpan.FromSeconds(1));

            var tuning = new CareTuning();
            var expectedHunger = 80d - tuning.HungerDecayPerHour / 60d;
            Assert.That(view.State.Hunger, Is.EqualTo(expectedHunger).Within(1e-6));
        }

        [Test]
        public void OnDebugSimulate12HoursClicked_AppliesExactlyTwelveHoursOfProgress()
        {
            var path = CreateTempSavePath();
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            view.LoadColony(path, nowUtc);

            view.OnDebugSimulate12HoursClicked();

            var tuning = new CareTuning();
            Assert.That(view.State.Hunger, Is.EqualTo(80d - tuning.HungerDecayPerHour * 12d).Within(1e-6));
            Assert.That(debugAppliedElapsedLabel.text, Is.EqualTo("前回反映: 12時間0分"));
        }

        [Test]
        public void OnCleanClicked_AfterTheDebugClockRanAhead_IsNotUndoneByTheNextTick()
        {
            // Regression: a care tap used to reset LastSavedAtUtc to the real clock while
            // the debug-accelerated clock was hours ahead, so the next tick replayed that
            // whole gap and immediately decayed the stat the player had just restored.
            var path = CreateTempSavePath();
            var realNow = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            view.LoadColony(path, realNow, new TimeService(() => realNow));
            view.OnDebugMultiplierClicked(600d);
            view.ApplyLiveTickDelta(TimeSpan.FromSeconds(6)); // one virtual hour ahead

            view.OnCleanClicked();
            Assert.That(view.State.Cleanliness, Is.EqualTo(100d));

            view.ApplyLiveTickDelta(TimeSpan.FromSeconds(0.1)); // one virtual minute

            var tuning = new CareTuning();
            Assert.That(view.State.Cleanliness, Is.EqualTo(100d - tuning.CleanlinessDecayPerHour / 60d).Within(1e-6));
        }

        [Test]
        public void OnApplicationPause_OnResume_AppliesTheRealTimeSpentInTheBackground()
        {
            var path = CreateTempSavePath();
            var realNow = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            view.LoadColony(path, realNow, new TimeService(() => realNow));

            view.OnApplicationPause(true);
            realNow += TimeSpan.FromHours(2);
            view.OnApplicationPause(false);

            var tuning = new CareTuning();
            Assert.That(view.State.Hunger, Is.EqualTo(80d - tuning.HungerDecayPerHour * 2d).Within(1e-6));
        }

        [Test]
        public void OnPetTapped_ShowsAHintAboutTheMostPressingNeed()
        {
            view.OnDebugHydrationChanged(10d);

            view.OnPetTapped();

            Assert.That(feedbackLabel.text, Is.EqualTo(PetMoodMessage.Thirsty));
            Assert.That(feedbackLabel.style.visibility.value, Is.EqualTo(Visibility.Visible));
        }

        [Test]
        public void OnFeedClicked_RightBeforeAShed_IsRefusedAndExplained()
        {
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var path = SaveColonyWith(new PetState
            {
                Hunger = 50d,
                LastSavedAtUtc = nowUtc,
                NextShedAtUtc = nowUtc + TimeSpan.FromMinutes(30),
            }, nowUtc);
            view.LoadColony(path, nowUtc, new TimeService(() => nowUtc));

            view.OnFeedClicked();

            Assert.That(view.State.Hunger, Is.EqualTo(50d));
            StringAssert.Contains("脱皮", feedbackLabel.text);
        }

        [Test]
        public void LoadColony_WhenAShedHappenedWhileAway_SaysSo()
        {
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var path = SaveColonyWith(new PetState
            {
                LastSavedAtUtc = nowUtc - TimeSpan.FromHours(3),
                NextShedAtUtc = nowUtc - TimeSpan.FromHours(1),
            }, nowUtc);

            view.LoadColony(path, nowUtc, new TimeService(() => nowUtc));

            StringAssert.Contains("脱皮した", feedbackLabel.text);
            Assert.That(view.State.NextShedAtUtc, Is.GreaterThan(nowUtc));
        }

        [Test]
        public void FeedbackLabel_KeepsItsLayoutSpaceWhetherShownOrHidden()
        {
            // Toggling display used to collapse the label, so every message shoved the
            // terrarium and buttons up and down. Only visibility may change.
            Assert.That(feedbackLabel.style.display.value, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(feedbackLabel.style.visibility.value, Is.EqualTo(Visibility.Hidden));

            view.OnFeedClicked();

            Assert.That(feedbackLabel.style.display.value, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(feedbackLabel.style.visibility.value, Is.EqualTo(Visibility.Visible));
        }

        [Test]
        public void OnDebugWeightChanged_UpdatesStateAndRerenders()
        {
            view.OnDebugWeightChanged(9d);

            Assert.That(view.State.WeightGrams, Is.EqualTo(9d));
            Assert.That(growthStageLabel.text, Is.EqualTo("ベビー 9.0g"));
        }

        [Test]
        public void OnDebugClearSaveClicked_ResetsStateToDefaultsAndOverwritesTheSaveFile()
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var nonDefaultState = new PetState { Hunger = 12d, WeightGrams = 60d };
            var path = SaveColonyWith(nonDefaultState, nowUtc);
            view.LoadColony(path, nowUtc);
            Assert.That(view.State.Hunger, Is.EqualTo(12d));

            view.OnDebugClearSaveClicked();

            Assert.That(view.State.Hunger, Is.EqualTo(80d));
            Assert.That(view.State.WeightGrams, Is.EqualTo(3d));

            var reloaded = new ColonySaveService(new CareTuning(), new EconomyTuning())
                .LoadOrCreate(path, nowUtc, new System.Random(1)).Animals[0];
            Assert.That(reloaded.Hunger, Is.EqualTo(80d));
        }

        [Test]
        public void OnFeedAllClicked_FeedsEveryPetAndSaysHowMany()
        {
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var path = SaveColonyWith(new PetState { Hunger = 20d, LastSavedAtUtc = nowUtc, NextShedAtUtc = nowUtc.AddDays(3) }, nowUtc);
            view.LoadColony(path, nowUtc, new TimeService(() => nowUtc));
            view.Session.Colony.AddAnimal(new PetState { Hunger = 20d, LastSavedAtUtc = nowUtc, NextShedAtUtc = nowUtc.AddDays(3) },
                view.Session.Colony.AddCage(CageSize.Standard));

            view.OnFeedAllClicked();

            Assert.That(feedbackLabel.text, Is.EqualTo("2匹が食べました"));
            Assert.That(view.Session.Colony.Wallet.Money, Is.EqualTo(50000 - 60));
        }

        [Test]
        public void SwipeStep_OnlyReportsADirectionPastTheThreshold()
        {
            Assert.That(TerrariumView.SwipeStep(-100f, 60f), Is.EqualTo(1), "swiping left steps forward to the next cage");
            Assert.That(TerrariumView.SwipeStep(100f, 60f), Is.EqualTo(-1), "swiping right steps back to the previous cage");
            Assert.That(TerrariumView.SwipeStep(30f, 60f), Is.EqualTo(0));
            Assert.That(TerrariumView.SwipeStep(-30f, 60f), Is.EqualTo(0));
            Assert.That(TerrariumView.SwipeStep(-60f, 60f), Is.EqualTo(1), "exactly at the threshold still counts as a swipe");
        }

        [Test]
        public void ApplySwipeDelta_BeyondThreshold_StepsTheCageAndSuppressesOnlyTheNextPetTap()
        {
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var path = SaveColonyWith(new PetState { LastSavedAtUtc = nowUtc, NextShedAtUtc = nowUtc.AddDays(3) }, nowUtc);
            view.LoadColony(path, nowUtc, new TimeService(() => nowUtc));
            var second = view.Session.Colony.AddAnimal(new PetState { Name = "ふたり目" }, view.Session.Colony.AddCage(CageSize.Standard));

            view.ApplySwipeDelta(-100f);
            Assert.That(view.State, Is.SameAs(second), "a recognised swipe still steps to the next cage");

            // The swipe started/ended on the gecko: the very next pet tap must be ignored.
            view.OnPetElementClicked(ClickEvent.GetPooled());
            Assert.That(feedbackLabel.style.visibility.value, Is.EqualTo(Visibility.Hidden),
                "a pet tap right after a recognised swipe must be ignored");

            // But only that one click — a normal tap afterwards must still register.
            view.OnPetElementClicked(ClickEvent.GetPooled());
            Assert.That(feedbackLabel.style.visibility.value, Is.EqualTo(Visibility.Visible),
                "a later, unrelated tap on the pet must still work");
        }

        [Test]
        public void BeginGestureTracking_AfterARecognisedSwipe_ClearsTheTapSuppressionForTheNewGesture()
        {
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var path = SaveColonyWith(new PetState { LastSavedAtUtc = nowUtc, NextShedAtUtc = nowUtc.AddDays(3) }, nowUtc);
            view.LoadColony(path, nowUtc, new TimeService(() => nowUtc));
            view.Session.Colony.AddAnimal(new PetState { Name = "ふたり目" }, view.Session.Colony.AddCage(CageSize.Standard));

            // A recognised swipe arms the suppression flag but a background swipe never
            // reaches a pet click to consume it.
            view.ApplySwipeDelta(-100f);

            // A new gesture starts (e.g. the player taps down on the pet later): the
            // PointerDown handler must clear the leftover suppression so this unrelated tap
            // is handled normally instead of being silently swallowed.
            view.BeginGestureTracking(Vector2.zero);

            view.OnPetElementClicked(ClickEvent.GetPooled());
            Assert.That(feedbackLabel.style.visibility.value, Is.EqualTo(Visibility.Visible),
                "a pet tap belonging to a new gesture must not be swallowed by a stale swipe suppression");
        }

        [Test]
        public void ApplySwipeDelta_BelowThreshold_DoesNotStepOrSuppressTheNextPetTap()
        {
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var path = SaveColonyWith(new PetState { LastSavedAtUtc = nowUtc, NextShedAtUtc = nowUtc.AddDays(3) }, nowUtc);
            view.LoadColony(path, nowUtc, new TimeService(() => nowUtc));
            var first = view.State;

            view.ApplySwipeDelta(20f);
            Assert.That(view.State, Is.SameAs(first), "a short drag is not a swipe");

            view.OnPetElementClicked(ClickEvent.GetPooled());
            Assert.That(feedbackLabel.style.visibility.value, Is.EqualTo(Visibility.Visible),
                "a tap not preceded by a recognised swipe must always register");
        }

        [Test]
        public void ShowCageStep_WrapsAroundAndSelectsThatCagesPet()
        {
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var path = SaveColonyWith(new PetState { LastSavedAtUtc = nowUtc, NextShedAtUtc = nowUtc.AddDays(3) }, nowUtc);
            view.LoadColony(path, nowUtc, new TimeService(() => nowUtc));
            var second = view.Session.Colony.AddAnimal(new PetState { Name = "ふたり目" }, view.Session.Colony.AddCage(CageSize.Standard));

            view.ShowCageStep(1);
            Assert.That(view.State, Is.SameAs(second));

            view.ShowCageStep(1);
            Assert.That(view.State, Is.SameAs(view.Session.Colony.Animals[0]));
        }
    }
}
