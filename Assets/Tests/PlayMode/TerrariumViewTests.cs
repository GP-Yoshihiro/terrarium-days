using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;
using TerrariumDays.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TerrariumDays.Tests
{
    public sealed class TerrariumViewTests
    {
        private GameObject gameObject;
        private TerrariumView view;

        private Label growthStageLabel;
        private VisualElement profileChips;
        private VisualElement growthGaugeFill;
        private Label breedingStatusLabel;
        private Button nestBoxButton;
        private Button moveEggsButton;
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
        private Button pairButton;
        private VisualElement milestoneModal;
        private Label milestoneStageLabel;
        private Label milestoneMessageLabel;
        private Button milestoneContinueButton;
        private readonly VisualElement[] decorImageElements = new VisualElement[3];
        private Button decorButton;
        private VisualElement decorDrawer;
        private Button decorDrawerCloseButton;
        private Label decorSlotLabel;
        private readonly Dictionary<string, Button> decorRowButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Label> decorStatusLabels = new Dictionary<string, Label>();
        private VisualElement topBar;
        private Button debugButton;
        private VisualElement debugPanel;
        private Label debugAppliedElapsedLabel;
        private Slider debugWeightSlider;
        private VisualElement petElement;
        private VisualElement shopPanel;
        private Button shopSectionAnimalsButton;
        private Button shopSectionSuppliesButton;
        private Button shopSectionWholesaleButton;
        private Label shopRestockLabel;
        private Label shopMessageLabel;
        private ScrollView shopList;
        private VisualElement ledgerPanel;
        private VisualElement familyTreeModal;
        private Button ledgerSectionAnimalsButton;
        private Button ledgerSectionMoneyButton;
        private Label ledgerSummaryLabel;
        private ScrollView ledgerList;
        private VisualElement confirmModal;
        private Label confirmMessageLabel;
        private Button confirmYesButton;
        private Button confirmNoButton;
        private VisualElement shopFailureModal;
        private Label shopFailureMessage;
        private Label cageListEmptyLabel;
        private VisualElement breedingPanel;
        private VisualElement incubatorPanel;
        private VisualElement eventsPanel;
        private Label incubatorEggDetail;
        private Label breedingSeasonLabel;
        private Label breedingMessageLabel;
        private ScrollView breedingList;
        private readonly List<string> tempSavePaths = new List<string>();

        private string CreateTempSavePath()
        {
            var path = Path.Combine(Path.GetTempPath(), $"terrarium-view-test-{Guid.NewGuid():N}.json");
            tempSavePaths.Add(path);
            return path;
        }

        private string ProfileChipsText() => string.Concat(profileChips.Query<Label>().ToList().Select(l => l.text));

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
            profileChips = new VisualElement { name = "profile-chips" };
            growthGaugeFill = new VisualElement { name = "growth-gauge-fill" };
            breedingStatusLabel = new Label { name = "breeding-status-label" };
            nestBoxButton = new Button { name = "nest-box-button" };
            moveEggsButton = new Button { name = "cage-move-eggs-button" };
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
            pairButton = new Button { name = "pair-button" };
            milestoneModal = new VisualElement { name = "milestone-modal" };
            milestoneStageLabel = new Label { name = "milestone-stage-label" };
            milestoneMessageLabel = new Label { name = "milestone-message-label" };
            milestoneContinueButton = new Button { name = "milestone-continue-button" };
            milestoneModal.Add(milestoneStageLabel);
            milestoneModal.Add(milestoneMessageLabel);
            milestoneModal.Add(milestoneContinueButton);

            for (var i = 0; i < decorImageElements.Length; i++)
            {
                decorImageElements[i] = new VisualElement { name = $"decor-image-{i}" };
            }
            decorButton = new Button { name = "decor-button" };
            decorDrawer = new VisualElement { name = "decor-drawer" };
            decorDrawerCloseButton = new Button { name = "decor-drawer-close-button" };
            decorSlotLabel = new Label { name = "decor-slot-label" };
            decorDrawer.Add(decorSlotLabel);
            decorRowButtons.Clear();
            decorStatusLabels.Clear();
            foreach (var decor in DecorItems.All)
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

            petElement = new VisualElement { name = "pet-image" };

            shopPanel = new VisualElement { name = "shop-panel" };
            shopSectionAnimalsButton = new Button { name = "shop-section-animals" };
            shopSectionSuppliesButton = new Button { name = "shop-section-supplies" };
            shopSectionWholesaleButton = new Button { name = "shop-section-wholesale" };
            shopRestockLabel = new Label { name = "shop-restock-label" };
            shopMessageLabel = new Label { name = "shop-message-label" };
            shopList = new ScrollView { name = "shop-list" };
            shopPanel.Add(shopSectionAnimalsButton);
            shopPanel.Add(shopSectionSuppliesButton);
            shopPanel.Add(shopSectionWholesaleButton);
            shopPanel.Add(shopRestockLabel);
            shopPanel.Add(shopMessageLabel);
            shopPanel.Add(shopList);

            ledgerPanel = new VisualElement { name = "ledger-panel" };
            ledgerSectionAnimalsButton = new Button { name = "ledger-section-animals" };
            ledgerSectionMoneyButton = new Button { name = "ledger-section-money" };
            ledgerSummaryLabel = new Label { name = "ledger-summary-label" };
            ledgerList = new ScrollView { name = "ledger-list" };
            ledgerPanel.Add(ledgerSectionAnimalsButton);
            ledgerPanel.Add(ledgerSectionMoneyButton);
            ledgerPanel.Add(ledgerSummaryLabel);
            ledgerPanel.Add(ledgerList);

            confirmModal = new VisualElement { name = "confirm-modal" };
            confirmMessageLabel = new Label { name = "confirm-message-label" };
            confirmYesButton = new Button { name = "confirm-yes-button" };
            confirmNoButton = new Button { name = "confirm-no-button" };
            confirmModal.Add(confirmMessageLabel);
            confirmModal.Add(confirmYesButton);
            confirmModal.Add(confirmNoButton);

            shopFailureModal = new VisualElement { name = "shop-failure-modal" };
            shopFailureModal.style.display = DisplayStyle.None;
            shopFailureMessage = new Label { name = "shop-failure-message" };
            shopFailureModal.Add(shopFailureMessage);

            cageListEmptyLabel = new Label { name = "cage-list-empty-label" };

            breedingPanel = new VisualElement { name = "breeding-panel" };
            breedingSeasonLabel = new Label { name = "breeding-season-label" };
            breedingMessageLabel = new Label { name = "breeding-message-label" };
            breedingList = new ScrollView { name = "breeding-list" };
            breedingPanel.Add(breedingSeasonLabel);
            breedingPanel.Add(breedingMessageLabel);
            breedingPanel.Add(breedingList);

            root.Add(growthStageLabel);
            root.Add(profileChips);
            root.Add(growthGaugeFill);
            root.Add(breedingStatusLabel);
            root.Add(nestBoxButton);
            root.Add(moveEggsButton);
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
            root.Add(pairButton);
            root.Add(milestoneModal);
            foreach (var element in decorImageElements)
            {
                root.Add(element);
            }
            root.Add(decorButton);
            root.Add(decorDrawer);
            root.Add(shopPanel);
            root.Add(ledgerPanel);
            root.Add(confirmModal);
            root.Add(shopFailureModal);
            familyTreeModal = new VisualElement { name = "family-tree-modal" };
            familyTreeModal.style.display = DisplayStyle.None;
            familyTreeModal.Add(new Label { name = "family-tree-title" });
            foreach (var boxName in new[] { "mgm", "mgf", "pgm", "pgf", "mother", "father", "self" })
            {
                familyTreeModal.Add(new VisualElement { name = $"family-tree-{boxName}" });
            }

            familyTreeModal.Add(new Button { name = "family-tree-cage-button" });
            familyTreeModal.Add(new Button { name = "family-tree-close-button" });
            root.Add(familyTreeModal);
            root.Add(cageListEmptyLabel);
            incubatorPanel = new VisualElement { name = "incubator-panel" };
            incubatorPanel.Add(new VisualElement { name = "incubator-card-list" });
            incubatorEggDetail = new Label { name = "incubator-egg-detail" };
            incubatorPanel.Add(incubatorEggDetail);
            incubatorPanel.Add(new VisualElement { name = "hatch-tray-list" });
            incubatorPanel.Add(new Label { name = "hatch-tray-empty" });

            eventsPanel = new VisualElement { name = "events-panel" };
            var prepSurface = new VisualElement { name = "event-prep" };
            prepSurface.Add(new Label { name = "event-prep-title" });
            prepSurface.Add(new Label { name = "event-prep-info" });
            prepSurface.Add(new ScrollView { name = "event-prep-list" });
            prepSurface.Add(new Label { name = "event-prep-total" });
            prepSurface.Add(new Label { name = "event-prep-count" });
            prepSurface.Add(new Label { name = "event-prep-message" });
            prepSurface.Add(new Button { name = "event-prep-save-button" });
            prepSurface.Add(new Button { name = "event-prep-register-button" });
            eventsPanel.Add(prepSurface);
            var daySurface = new VisualElement { name = "event-day" };
            daySurface.Add(new Label { name = "event-day-title" });
            daySurface.Add(new Label { name = "event-day-left" });
            daySurface.Add(new VisualElement { name = "event-day-stage" });
            daySurface.Add(new ScrollView { name = "event-day-offers" });
            foreach (var buttonName in new[] { "serve", "browse", "serve-all", "skip", "finish" })
            {
                daySurface.Add(new Button { name = $"event-day-{buttonName}-button" });
            }

            daySurface.Add(new ScrollView { name = "event-day-log" });
            eventsPanel.Add(daySurface);
            var resultSurface = new VisualElement { name = "event-result" };
            resultSurface.Add(new Label { name = "event-result-title" });
            foreach (var blockName in new[] { "sold", "unsold", "bought" })
            {
                resultSurface.Add(new VisualElement { name = $"event-result-{blockName}" });
            }

            resultSurface.Add(new Label { name = "event-result-totals" });
            resultSurface.Add(new Button { name = "event-result-close-button" });
            eventsPanel.Add(resultSurface);
            var buyModal = new VisualElement { name = "event-buy-modal" };
            buyModal.Add(new Label { name = "event-buy-message" });
            buyModal.Add(new Button { name = "event-buy-yes-button" });
            buyModal.Add(new Button { name = "event-buy-no-button" });
            root.Add(buyModal);
            var blockedModal = new VisualElement { name = "event-blocked-modal" };
            blockedModal.Add(new Label { name = "event-blocked-message" });
            blockedModal.Add(new Button { name = "event-blocked-shop-button" });
            blockedModal.Add(new Button { name = "event-blocked-close-button" });
            root.Add(blockedModal);

            root.Add(breedingPanel);
            root.Add(incubatorPanel);
            root.Add(eventsPanel);
            root.Add(topBar);
            root.Add(debugButton);
            root.Add(debugPanel);
            root.Add(petElement);

            view.BindElements(root);

            // Awake() (which builds the real navigator) is skipped by this harness (see the
            // comment below), so build one directly against the same hand-built tree: it lets
            // pair-button tests exercise ShellNavigator.ShowTab/Tab like the real app does.
            typeof(TerrariumView).GetField("navigator", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(view, new ShellNavigator(root));

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

                if (File.Exists(ColonySaveService.BackupPathFor(path)))
                {
                    File.Delete(ColonySaveService.BackupPathFor(path));
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
        public void LoadColony_WithASchemaTwoSave_ShowsTheMigrationFeedbackMessage()
        {
            var path = CreateTempSavePath();
            var nowUtc = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
            File.WriteAllText(path,
                "{\"schemaVersion\":2,\"lastSavedAtUtc\":\"2026-09-25T11:00:00.0000000+00:00\",\"hunger\":70,\"hydration\":60," +
                "\"cleanliness\":50,\"health\":90,\"growth\":50,\"growthStage\":\"Juvenile\",\"selectedDecorId\":\"plant_01\"," +
                "\"unlockedDecorIds\":[\"rock_01\",\"plant_01\"],\"lastShedAtUtc\":\"2026-09-20T00:00:00.0000000+00:00\"," +
                "\"nextShedAtUtc\":\"2026-11-19T00:00:00.0000000+00:00\"}");

            view.LoadColony(path, nowUtc, new TimeService(() => nowUtc));

            // Migration also moves the legacy unlockedDecorIds into the shared inventory, whose
            // feedback message is shown after (and overrides) the migration message (task 5 spec).
            Assert.That(feedbackLabel.text, Is.EqualTo(TerrariumView.DecorMovedMessage));
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
        public void LoadColony_NewGame_ShowsTheStarterRockInTheFirstSlotOnly()
        {
            var path = CreateTempSavePath();

            view.LoadColony(path, DateTimeOffset.UtcNow);

            Assert.That(decorImageElements[0].style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(decorImageElements[0].ClassListContains("decor-icon-rock_01"), Is.True);
            Assert.That(decorImageElements[1].style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(decorImageElements[2].style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void OnDecorRowClicked_ForAnOwnedItem_PlacesItInTheNextSlotAndUpdatesTheDrawer()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, DateTimeOffset.UtcNow);
            view.Session.Colony.Inventory.Add("plant_01", 1);

            view.OnDecorRowClicked("plant_01");

            Assert.That(view.CurrentCage.DecorIds, Is.EqualTo(new[] { "rock_01", "plant_01" }));
            Assert.That(decorImageElements[1].style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(decorImageElements[1].ClassListContains("decor-icon-plant_01"), Is.True);
            Assert.That(view.Session.Colony.Inventory.Count("plant_01"), Is.EqualTo(0));
            Assert.That(decorStatusLabels["plant_01"].text, Is.EqualTo("置いています（タップで外す）"));
            Assert.That(decorSlotLabel.text, Is.EqualTo("装飾の枠 2/2"));
        }

        [Test]
        public void OnDecorRowClicked_TappedAgain_RemovesItAndReturnsItToTheInventory()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, DateTimeOffset.UtcNow);
            view.Session.Colony.Inventory.Add("plant_01", 1);
            view.OnDecorRowClicked("plant_01");

            view.OnDecorRowClicked("plant_01");

            Assert.That(view.CurrentCage.DecorIds, Is.EqualTo(new[] { "rock_01" }));
            Assert.That(decorImageElements[1].style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Session.Colony.Inventory.Count("plant_01"), Is.EqualTo(1));
        }

        [Test]
        public void OnDecorRowClicked_ForAnUnownedItem_DoesNothingAndShowsNotOwned()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, DateTimeOffset.UtcNow);

            view.OnDecorRowClicked("water_dish_01");
            view.OnDecorButtonClicked();

            Assert.That(view.CurrentCage.DecorIds, Is.EqualTo(new[] { "rock_01" }));
            Assert.That(decorStatusLabels["water_dish_01"].text, Is.EqualTo("未所持（ショップで購入）"));
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
        public void ApplyLiveTickDelta_WithoutACompletedStep_DoesNotRerenderUntilAStepCompletes()
        {
            var path = CreateTempSavePath();
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            view.LoadColony(path, nowUtc);
            view.OnDebugMultiplierClicked(40d);
            growthStageLabel.text = "SENTINEL";

            // At x40, one real second is 40 game seconds: short of the 1-minute (60s) step,
            // so the tick has nothing new to show and must not re-render.
            view.ApplyLiveTickDelta(TimeSpan.FromSeconds(1));
            Assert.That(growthStageLabel.text, Is.EqualTo("SENTINEL"), "a tick with no applied elapsed and no events must not call Render");

            // The second tick crosses the 1-minute step: now it must re-render.
            view.ApplyLiveTickDelta(TimeSpan.FromSeconds(1));
            Assert.That(growthStageLabel.text, Is.Not.EqualTo("SENTINEL"), "once a step completes, Render must run again");
        }

        [Test]
        public void OnDebugSimulate12HoursClicked_AppliesExactlyTwelveHoursOfProgress()
        {
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var tuning = new CareTuning();
            // A fresh LoadColony() rolls the starting animal's personality at random
            // (StarterGenetics.Apply); Shy stretches the pre-shed fast to x1.5 and can pull
            // the x0.3 hunger-decay window into these 12 hours, making the assertion below
            // flaky (~20% of runs, the Shy share of the five personalities). Pin the
            // personality via SaveColonyWith so the scenario (a Baby, hunger 80, no shed for
            // 14h) is deterministic.
            var path = SaveColonyWith(new PetState
            {
                Personality = Personality.Calm,
                LastSavedAtUtc = nowUtc,
                LastShedAtUtc = nowUtc,
                NextShedAtUtc = nowUtc + SheddingModel.IntervalFor(GrowthStage.Baby, tuning),
            }, nowUtc);
            view.LoadColony(path, nowUtc);

            view.OnDebugSimulate12HoursClicked();

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
        public void SelectCage_OfAnEmptyCage_ReturnsFalseAndKeepsThePreviousSelection()
        {
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var path = SaveColonyWith(new PetState { LastSavedAtUtc = nowUtc, NextShedAtUtc = nowUtc.AddDays(3) }, nowUtc);
            view.LoadColony(path, nowUtc, new TimeService(() => nowUtc));
            var previousState = view.State;
            var previousCage = view.CurrentCage;
            var emptyCage = view.Session.Colony.AddCage(CageSize.Standard);

            var selected = view.SelectCage(emptyCage.Id);

            Assert.That(selected, Is.False);
            Assert.That(view.State, Is.SameAs(previousState));
            Assert.That(view.CurrentCage, Is.SameAs(previousCage));
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

        [Test]
        public void LoadColony_ForANewGame_ShowsTheStarterProfileOnTheProfileLabel()
        {
            var path = CreateTempSavePath();

            view.LoadColony(path, DateTimeOffset.UtcNow);

            StringAssert.StartsWith("ノーマル（ヘテロ不明）", ProfileChipsText());
        }

        [Test]
        public void SelectingACageWithADifferentMorph_ShowsItsNameAndADifferentSprite()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, DateTimeOffset.UtcNow);
            var firstFrame = petElement.style.backgroundImage.value.texture;

            // Debug-adds a second animal into a freshly-bought cage; StarterGenetics.Showcase[1]
            // ("トレンパーアルビノ") lands on the second animal (NextAnimalId 2 - 1 = index 1).
            view.OnDebugAddCageClicked();
            view.OnDebugAddPetClicked();
            var secondCage = view.Session.Colony.Cages[1];

            var selected = view.SelectCage(secondCage.Id);

            Assert.That(selected, Is.True);
            StringAssert.StartsWith("トレンパーアルビノ", ProfileChipsText());
            Assert.That(petElement.style.backgroundImage.value.texture, Is.Not.SameAs(firstFrame),
                "a different morph must render with a different recoloured texture");
        }

        [Test]
        public void ASheddingRevealsTheSexOfAYoungUnrevealedAnimal()
        {
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var pet = new PetState
            {
                Name = "レオパ1",
                Sex = Sex.Male,
                Stage = GrowthStage.Juvenile,
                SexRevealed = false,
                LastSavedAtUtc = nowUtc,
                NextShedAtUtc = nowUtc.AddHours(1),
            };
            var path = SaveColonyWith(pet, nowUtc);
            view.LoadColony(path, nowUtc, new TimeService(() => nowUtc));

            view.OnDebugSimulate12HoursClicked();

            Assert.That(feedbackLabel.text, Is.EqualTo(CageStatusText.SexRevealMessage(view.State)));
            Assert.That(view.State.SexKnown, Is.True);
            StringAssert.DoesNotContain("性別不明", CageStatusText.TitleFor(view.CurrentCage, view.State));
        }

        // ---- Shop tab (§9): animals, supplies, wholesale, and the confirmation dialog. ----

        private static readonly DateTimeOffset ShopNow = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);

        private ShopView ShopViewOf() =>
            (ShopView)typeof(TerrariumView).GetField("shopView", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        /// <summary>
        /// The very ShopService the view itself uses (built in LoadColony), rather than a
        /// same-looking instance built fresh in the test — using our own copy would hide the
        /// exact "ShopView could drift from the real ShopService.Economy" bug this test class
        /// exists to catch.
        /// </summary>
        private ShopService ShopServiceOf() =>
            (ShopService)typeof(TerrariumView).GetField("shopService", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        private void RenderShop() =>
            ShopViewOf().Render(view.Session.Colony, ShopServiceOf(), view.Session.Calendar, ShopNow);

        /// <summary>UI Toolkit queues Button clicks and only pumps them on a real update tick,
        /// which EditMode/coroutine-less code never gets; invoke the wired Clickable action
        /// directly instead (same technique as ShellTests.FindClickAction).</summary>
        private static Action FindClickAction(Clickable clickable)
        {
            foreach (var field in typeof(Clickable).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.GetValue(clickable) is Action action)
                {
                    return action;
                }
            }

            return null;
        }

        private void ClickConfirmYes()
        {
            var action = FindClickAction(confirmYesButton.clickable);
            Assert.That(action, Is.Not.Null, "confirm-yes-button has no click action wired up");
            action();
        }

        private void ClickConfirmNo()
        {
            var action = FindClickAction(confirmNoButton.clickable);
            Assert.That(action, Is.Not.Null, "confirm-no-button has no click action wired up");
            action();
        }

        [Test]
        public void ShopAnimalsSection_ListsEveryOfferWithItsShopPrice()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var shopService = ShopServiceOf();

            RenderShop();

            var offers = view.Session.Colony.Shop.Offers;
            Assert.That(offers.Count, Is.InRange(4, 6));

            var priceLabels = shopList.Query<Label>(className: "shop-row-price-label").ToList();
            Assert.That(priceLabels, Has.Count.EqualTo(offers.Count));
            for (var i = 0; i < offers.Count; i++)
            {
                Assert.That(priceLabels[i].text, Is.EqualTo(ShopText.Yen(shopService.PriceOf(offers[i]))));
            }
        }

        [Test]
        public void ShopBuyAnimal_WithAnEmptyCageAndEnoughMoney_ConfirmYes_AddsTheAnimalAndChargesThePrice()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            view.Session.Colony.AddCage(CageSize.Standard); // an empty cage to receive the purchase
            view.Session.Colony.Wallet.Money = 1_000_000;
            var moneyBefore = view.Session.Colony.Wallet.Money;
            var animalsBefore = view.Session.Colony.Animals.Count;
            var offer = view.Session.Colony.Shop.Offers[0];
            var shopService = ShopServiceOf();
            var price = shopService.PriceOf(offer);

            view.OnShopOfferBuyRequested(offer.OfferId);
            Assert.That(confirmModal.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            ClickConfirmYes();

            Assert.That(view.Session.Colony.Animals.Count, Is.EqualTo(animalsBefore + 1));
            Assert.That(view.Session.Colony.Wallet.Money, Is.EqualTo(moneyBefore - price));
            var lastEntry = view.Session.Colony.Wallet.Ledger.Last();
            Assert.That(lastEntry.Category, Is.EqualTo(LedgerCategory.AnimalPurchase));
            var boughtPet = view.Session.Colony.Animals.Last();
            var cage = view.Session.Colony.CageOf(boughtPet);
            Assert.That(shopMessageLabel.text, Is.EqualTo(ShopText.BoughtAnimalMessage(boughtPet, cage)));
        }

        [Test]
        public void ShopBuyAnimal_WithNoEmptyCage_ConfirmYes_ShowsTheFailureAndChangesNothing()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            // The starter colony's only cage already holds the starter animal, so there is no
            // empty cage for the newly bought one.
            var moneyBefore = view.Session.Colony.Wallet.Money;
            var animalsBefore = view.Session.Colony.Animals.Count;
            var offer = view.Session.Colony.Shop.Offers[0];

            view.OnShopOfferBuyRequested(offer.OfferId);
            ClickConfirmYes();

            Assert.That(view.Session.Colony.Animals.Count, Is.EqualTo(animalsBefore));
            Assert.That(view.Session.Colony.Wallet.Money, Is.EqualTo(moneyBefore));
            Assert.That(shopFailureModal.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(shopFailureMessage.text, Is.EqualTo(ShopText.FailureMessage(ShopResult.NoEmptyCage)));
        }

        [Test]
        public void ShopBuyAnimal_ConfirmNo_ChangesNothing()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            view.Session.Colony.AddCage(CageSize.Standard);
            view.Session.Colony.Wallet.Money = 1_000_000;
            var moneyBefore = view.Session.Colony.Wallet.Money;
            var animalsBefore = view.Session.Colony.Animals.Count;
            var offer = view.Session.Colony.Shop.Offers[0];

            view.OnShopOfferBuyRequested(offer.OfferId);
            ClickConfirmNo();

            Assert.That(confirmModal.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Session.Colony.Animals.Count, Is.EqualTo(animalsBefore));
            Assert.That(view.Session.Colony.Wallet.Money, Is.EqualTo(moneyBefore));
        }

        [Test]
        public void ShopBuySupply_StandardCage_AddsACageAndAnExtraEmptySlotShowsAtHome()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            view.Session.Colony.Wallet.Money = 1_000_000;
            var cagesBefore = view.Session.Colony.Cages.Count;

            view.OnShopItemBuyRequested("cage_standard");
            ClickConfirmYes();

            Assert.That(view.Session.Colony.Cages.Count, Is.EqualTo(cagesBefore + 1));
            var newCage = view.Session.Colony.Cages.Last();
            Assert.That(newCage.IsEmpty, Is.True);

            var rackList = new VisualElement();
            var home = new HomeView(rackList);
            home.Render(view.Session.Colony, ShopNow, view.Tuning);
            var emptyCageSlots = rackList.Query<Label>(className: "rack-name").ToList()
                .Count(l => l.text == "空きケージ");
            Assert.That(emptyCageSlots, Is.EqualTo(1));
        }

        [Test]
        public void ShopBuySupply_InsufficientMoney_ShowsNotEnoughMoneyDialogAndChangesNothing()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            view.Session.Colony.Wallet.Money = 0;
            var cagesBefore = view.Session.Colony.Cages.Count;

            view.OnShopItemBuyRequested("cage_standard");
            ClickConfirmYes();

            Assert.That(confirmModal.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(shopFailureModal.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(shopFailureMessage.text, Is.EqualTo("お金が足りません！"));
            Assert.That(view.Session.Colony.Cages.Count, Is.EqualTo(cagesBefore));
            Assert.That(view.Session.Colony.Wallet.Money, Is.Zero);
        }

        [Test]
        public void HomeView_MarksAVisitingPairAndAWeakAnimal()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;

            var maleCage = colony.AddCage(CageSize.Standard);
            var male = colony.AddAnimal(new PetState { Name = "タロウ", Sex = Sex.Male, SexRevealed = true, Stage = GrowthStage.Adult }, maleCage);
            var femaleCage = colony.AddCage(CageSize.Standard);
            var female = colony.AddAnimal(new PetState { Name = "ハナ", Sex = Sex.Female, SexRevealed = true, Stage = GrowthStage.Adult }, femaleCage);
            maleCage.VisitorAnimalId = female.Id;
            colony.Pairings.Add(new Pairing { MaleId = male.Id, FemaleId = female.Id });

            var weakCage = colony.AddCage(CageSize.Standard);
            var weak = colony.AddAnimal(new PetState { Name = "スズ", Weak = true }, weakCage);

            var rackList = new VisualElement();
            var home = new HomeView(rackList);
            home.Render(colony, ShopNow, view.Tuning);

            var slots = rackList.Query<Button>(className: "rack-slot").ToList();
            var maleSlot = slots[colony.Cages.IndexOf(maleCage)];
            Assert.That(maleSlot.ClassListContains("rack-slot-visiting"), Is.True);
            Assert.That(maleSlot.Q<Label>(className: "rack-visitor-label").text, Is.EqualTo("訪問中"));

            var femaleSlot = slots[colony.Cages.IndexOf(femaleCage)];
            Assert.That(femaleSlot.enabledSelf, Is.False);
            Assert.That(femaleSlot.Q<Label>(className: "rack-name").text, Is.Empty);

            var weakSlot = slots[colony.Cages.IndexOf(weakCage)];
            var badge = weakSlot.Q<Label>(className: "rack-badge-weak");
            Assert.That(badge, Is.Not.Null);
            Assert.That(badge.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void ShopWholesale_SellingTheUnselectedAnimal_RemovesItAndPaysTheWholesalePrice()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var second = view.Session.Colony.AddAnimal(new PetState { Name = "ふたり目" }, view.Session.Colony.AddCage(CageSize.Standard));
            var moneyBefore = view.Session.Colony.Wallet.Money;
            var shopService = ShopServiceOf();
            var pay = shopService.WholesalePriceOf(second);
            var selectedBefore = view.State;

            view.OnShopWholesaleRequested(second.Id);
            ClickConfirmYes();

            Assert.That(view.Session.Colony.Animals.Count, Is.EqualTo(1));
            Assert.That(view.Session.Colony.Wallet.Money, Is.EqualTo(moneyBefore + pay));
            Assert.That(view.State, Is.SameAs(selectedBefore), "the selection must not change when a different animal is sold");
            Assert.That(shopMessageLabel.text, Is.EqualTo(ShopText.WholesaleMessage("ふたり目", pay)));
        }

        [Test]
        public void ShopWholesale_SellingTheSelectedAnimal_SelectsAnotherOccupiedCage()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var selected = view.State;
            var second = view.Session.Colony.AddAnimal(new PetState { Name = "ふたり目" }, view.Session.Colony.AddCage(CageSize.Standard));

            view.OnShopWholesaleRequested(selected.Id);
            ClickConfirmYes();

            Assert.That(view.Session.Colony.Animals.Count, Is.EqualTo(1));
            Assert.That(view.State, Is.SameAs(second), "a cage still holding an animal must become the new selection");
        }

        [Test]
        public void ShopWholesale_TheLastAnimal_WarnsInTheConfirmationAndLeavesTheColonyEmpty()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var only = view.State;

            view.OnShopWholesaleRequested(only.Id);

            StringAssert.EndsWith(ShopText.LastAnimalWarning, confirmMessageLabel.text);

            ClickConfirmYes();

            // (a) the colony has no animals, no selection, and the cage detail screen is not
            // showing something for an animal that no longer exists.
            Assert.That(view.Session.Colony.Animals.Count, Is.EqualTo(0));
            Assert.That(view.State, Is.Null);
            Assert.That(view.CurrentCage, Is.Null);
        }

        [UnityTest]
        public IEnumerator ShopWholesale_TheLastAnimal_LeavesEverythingSafeAfterwards()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var only = view.State;

            view.OnShopWholesaleRequested(only.Id);
            ClickConfirmYes();

            // Bulk care must not change money any further once the colony is empty.
            var moneyBefore = view.Session.Colony.Wallet.Money;

            // (b) rendering the home view throws nothing and shows every slot as empty.
            var rackList = new VisualElement();
            var home = new HomeView(rackList);
            Assert.DoesNotThrow(() => home.Render(view.Session.Colony, ShopNow, view.Tuning));
            foreach (var slot in rackList.Query<Button>(className: "rack-slot").ToList())
            {
                Assert.That(slot.enabledSelf, Is.False);
            }

            // (c) bulk care no-ops with the no-animals feedback and spends nothing.
            view.OnFeedAllClicked();
            Assert.That(feedbackLabel.text, Is.EqualTo(CageStatusText.NoAnimalsMessage));
            Assert.That(view.Session.Colony.Wallet.Money, Is.EqualTo(moneyBefore));
            view.OnWaterAllClicked();
            Assert.That(feedbackLabel.text, Is.EqualTo(CageStatusText.NoAnimalsMessage));
            view.OnCleanAllClicked();
            Assert.That(feedbackLabel.text, Is.EqualTo(CageStatusText.NoAnimalsMessage));
            Assert.That(view.Session.Colony.Wallet.Money, Is.EqualTo(moneyBefore));

            // (d) stepping through cages throws nothing (there is nothing to step through).
            Assert.DoesNotThrow(() => view.ShowCageStep(1));

            // (e) a few live-tick frames and a time jump throw nothing.
            for (var i = 0; i < 3; i++)
            {
                yield return null;
            }

            Assert.DoesNotThrow(() => view.OnDebugSimulate12HoursClicked());

            // (f) saving and reloading the same save throws nothing and stays empty.
            Assert.DoesNotThrow(() => view.LoadColony(path, ShopNow, new TimeService(() => ShopNow)));
            Assert.That(view.State, Is.Null);

            // (g) buying a new animal from the shop selects its cage and it can be opened from the cage list.
            view.Session.Colony.Wallet.Money = 1_000_000;
            var offer = view.Session.Colony.Shop.Offers[0];
            view.OnShopOfferBuyRequested(offer.OfferId);
            ClickConfirmYes();

            Assert.That(view.State, Is.Not.Null);
            var boughtCage = view.CurrentCage;
            Assert.That(boughtCage, Is.Not.Null);

            var cageListRoot = new VisualElement();
            var cageListView = new CageListView(cageListRoot);
            var openedCageId = -1;
            cageListView.CageSelected += id => openedCageId = id;
            cageListView.Render(view.Session.Colony, ShopNow, view.Tuning);
            var row = cageListRoot.Query<Button>(className: "cage-row").ToList()[0];
            FindClickAction(row.clickable)();
            Assert.That(openedCageId, Is.EqualTo(boughtCage.Id));
        }

        [Test]
        public void ShopBuyAnimal_MonthRollsOverWhileConfirmationIsOpen_AbortsAndBuysNothing()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            view.Session.Colony.AddCage(CageSize.Standard);
            view.Session.Colony.Wallet.Money = 1_000_000;
            var animalsBefore = view.Session.Colony.Animals.Count;
            var offerBefore = view.Session.Colony.Shop.Offers[0];

            // Open the confirmation for this month's offer...
            view.OnShopOfferBuyRequested(offerBefore.OfferId);
            Assert.That(confirmModal.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            // ...then time passes (LiveTickLoop / OnApplicationPause->Resume keep ticking while
            // the dialog is open) past the next restock, which reuses the same OfferIds (1, 2, ...)
            // for a different set of animals at different prices (ShopStockGenerator). This also
            // bills a month of electricity, so compare money against the point right after the
            // time jump (not before it) to isolate whatever the confirmation itself does.
            view.Session.SimulateGameTime(TimeSpan.FromDays(GameCalendar.DaysPerMonth + 1));
            Assert.That(view.Session.Colony.Shop.Offers, Has.None.SameAs(offerBefore), "the month must have restocked with brand new offer objects");
            var moneyAfterRestock = view.Session.Colony.Wallet.Money;

            // Confirming now must not buy whatever animal happens to occupy the old OfferId today.
            ClickConfirmYes();

            Assert.That(view.Session.Colony.Animals.Count, Is.EqualTo(animalsBefore));
            Assert.That(view.Session.Colony.Wallet.Money, Is.EqualTo(moneyAfterRestock));
            Assert.That(shopMessageLabel.text, Is.EqualTo(ShopText.OfferChangedMessage));
        }

        [Test]
        public void ShopWholesale_AnimalSoldWhileConfirmationIsOpen_AbortsAndPaysNothing()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var second = view.Session.Colony.AddAnimal(new PetState { Name = "ふたり目" }, view.Session.Colony.AddCage(CageSize.Standard));
            var shopService = ShopServiceOf();
            var moneyBefore = view.Session.Colony.Wallet.Money;

            view.OnShopWholesaleRequested(second.Id);
            Assert.That(confirmModal.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            // The animal is sold through some other path while the dialog is still open.
            shopService.Wholesale(view.Session.Colony, second.Id, ShopNow);
            Assert.That(view.Session.Colony.Animals, Has.None.SameAs(second));

            ClickConfirmYes();

            Assert.That(view.Session.Colony.Wallet.Money, Is.EqualTo(moneyBefore + shopService.WholesalePriceOf(second)), "the second wholesale must be a no-op, not a double payout");
            Assert.That(shopMessageLabel.text, Is.EqualTo(ShopText.OfferChangedMessage));
        }

        // ---- Ledger tab (§6.2): every animal, and money in and out. ----

        private static readonly DateTimeOffset LedgerNow = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);

        private LedgerView LedgerViewOf() =>
            (LedgerView)typeof(TerrariumView).GetField("ledgerView", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        private void RenderLedger() =>
            LedgerViewOf().Render(view.Session.Colony, view.Session.Calendar, LedgerNow, view.Tuning);

        [Test]
        public void LedgerAnimalsSection_ListsOneRowPerAnimalWithItsNameAndDetail()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, LedgerNow, new TimeService(() => LedgerNow));
            view.Session.Colony.AddAnimal(new PetState
            {
                Name = "ふたり目",
                Stage = GrowthStage.Juvenile,
                WeightGrams = 22.04d,
                SexRevealed = true,
                Sex = Sex.Male,
                HatchedAtUtc = LedgerNow.AddDays(-5.5d),
            }, view.Session.Colony.AddCage(CageSize.Standard));

            RenderLedger();

            var occupied = view.Session.Colony.OccupiedCages();
            var names = ledgerList.Query<Label>(className: "ledger-row-name-label").ToList();
            var details = ledgerList.Query<Label>(className: "ledger-row-detail-label").ToList();
            Assert.That(names.Count, Is.EqualTo(occupied.Count));
            Assert.That(details.Count, Is.EqualTo(occupied.Count * 2), "each row has its detail and its parents line");
            for (var i = 0; i < occupied.Count; i++)
            {
                var pet = view.Session.Colony.AnimalIn(occupied[i]);
                Assert.That(names[i].text, Is.EqualTo(pet.Name));
                Assert.That(details[i * 2].text, Is.EqualTo(LedgerText.AnimalDetail(pet, LedgerNow)));
            }

            var secondPet = view.Session.Colony.Animals.Last();
            Assert.That(details[details.Count - 2].text, Is.EqualTo("♂オス・ヤング・22.0g・生後5か月"));
        }

        [Test]
        public void LedgerAnimalsSection_WithNoAnimals_ShowsTheNoAnimalsMessage()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, LedgerNow, new TimeService(() => LedgerNow));
            view.Session.Colony.RemoveAnimal(view.Session.Colony.Animals[0]);

            RenderLedger();

            var empty = ledgerList.Query<Label>(className: "ledger-empty-label").ToList();
            Assert.That(empty.Count, Is.EqualTo(1));
            Assert.That(empty[0].text, Is.EqualTo(CageStatusText.NoAnimalsMessage));
        }

        [Test]
        public void LedgerAnimalRow_Tapped_ThenSeeCage_OpensThatAnimalsCageDetail()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, LedgerNow, new TimeService(() => LedgerNow));
            var second = view.Session.Colony.AddAnimal(new PetState { Name = "ふたり目" }, view.Session.Colony.AddCage(CageSize.Standard));

            view.OnLedgerAnimalTapped(second.Id);
            Assert.That(familyTreeModal.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(familyTreeModal.Q<Button>("family-tree-cage-button").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            view.OnFamilyTreeCageClicked();

            Assert.That(familyTreeModal.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.State, Is.SameAs(second));
            Assert.That(view.CurrentCage, Is.SameAs(view.Session.Colony.CageOf(second)));
        }

        [Test]
        public void LedgerAnimalRow_ShowsParentsLine_AndShopAnimalIsUnknown()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, LedgerNow, new TimeService(() => LedgerNow));
            var colony = view.Session.Colony;
            var mother = colony.AddAnimal(new PetState { Name = "ハナ", SexRevealed = true, Sex = Sex.Female }, colony.AddCage(CageSize.Standard));
            var father = colony.AddAnimal(new PetState { Name = "タロウ", SexRevealed = true, Sex = Sex.Male }, colony.AddCage(CageSize.Standard));
            colony.AddAnimal(new PetState { Name = "コハナ", MotherId = mother.Id, FatherId = father.Id }, colony.AddCage(CageSize.Standard));

            RenderLedger();

            var lines = ledgerList.Query<Label>(className: "ledger-row-detail-label").ToList().Select(l => l.text).ToList();
            Assert.That(lines, Does.Contain("両親：ハナ×タロウ"));
            Assert.That(lines, Does.Contain("両親：不明"));
        }

        [Test]
        public void LedgerAnimalRow_Tapped_ShowsMotherInTreeAndMarksSoldParentGone()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, LedgerNow, new TimeService(() => LedgerNow));
            var colony = view.Session.Colony;
            var mother = colony.AddAnimal(new PetState { Name = "ハナ", SexRevealed = true, Sex = Sex.Female }, colony.AddCage(CageSize.Standard));
            var father = colony.AddAnimal(new PetState { Name = "タロウ", SexRevealed = true, Sex = Sex.Male }, colony.AddCage(CageSize.Standard));
            var child = colony.AddAnimal(new PetState { Name = "コハナ", MotherId = mother.Id, FatherId = father.Id }, colony.AddCage(CageSize.Standard));

            view.OnLedgerAnimalTapped(child.Id);
            Assert.That(familyTreeModal.Q<Label>("family-tree-title").text, Is.EqualTo("コハナ"));
            Assert.That(familyTreeModal.Q<VisualElement>("family-tree-mother").Q<Label>().text, Does.StartWith("ハナ"));
            Assert.That(familyTreeModal.Q<VisualElement>("family-tree-mgm").ClassListContains("family-box--unknown"), Is.True);
            Assert.That(familyTreeModal.Q<VisualElement>("family-tree-self").Q<Label>().text, Does.EndWith("?"));

            colony.RemoveAnimal(mother);
            view.OnLedgerAnimalTapped(child.Id);
            var box = familyTreeModal.Q<VisualElement>("family-tree-mother");
            Assert.That(box.Q<Label>().text, Does.Contain("ハナ（手放した）"));
            Assert.That(box.ClassListContains("family-box--gone"), Is.True);
        }

        [Test]
        public void LedgerAnimalRow_ForATrayAnimal_IsListedAndTreeHidesCageButton()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, LedgerNow, new TimeService(() => LedgerNow));
            var tray = view.Session.Colony.AddAnimal(new PetState { Name = "トレイっ子" }, null);

            RenderLedger();
            var lines = ledgerList.Query<Label>(className: "ledger-row-detail-label").ToList().Select(l => l.text).ToList();
            Assert.That(lines, Does.Contain(IncubatorText.TrayLocation));

            view.OnLedgerAnimalTapped(tray.Id);
            Assert.That(familyTreeModal.Q<Button>("family-tree-cage-button").style.display.value, Is.EqualTo(DisplayStyle.None));
            view.OnFamilyTreeCloseClicked();
            Assert.That(familyTreeModal.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        /// <summary>
        /// The row's body and its small pair button are separate sibling Buttons (not one
        /// nested in the other, which would fire both on a single tap) — verified here at the
        /// LedgerView level, directly on its own AnimalTapped/PairingRequested events, since
        /// the further forwarding to TerrariumView happens only in Awake() (skipped by this
        /// harness; see SetUp).
        /// </summary>
        [Test]
        public void LedgerAnimalRow_BodyAndPairButtonAreSeparateAndFireOnlyTheirOwnEvent()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, LedgerNow, new TimeService(() => LedgerNow));
            var second = view.Session.Colony.AddAnimal(new PetState { Name = "ふたり目", SexRevealed = true, Sex = Sex.Female }, view.Session.Colony.AddCage(CageSize.Standard));

            RenderLedger();
            var ledgerView = LedgerViewOf();
            int? tappedId = null;
            int? pairRequestedId = null;
            ledgerView.AnimalTapped += id => tappedId = id;
            ledgerView.PairingRequested += id => pairRequestedId = id;

            var body = ledgerList.Query<Button>(className: "ledger-row-body").ToList()
                .Single(b => b.Q<Label>(className: "ledger-row-name-label").text == second.Name);
            var pairButton = ledgerList.Q<Button>($"ledger-pair-{second.Id}");
            Assert.That(pairButton, Is.Not.Null);

            FindClickAction(body.clickable)();
            Assert.That(tappedId, Is.EqualTo(second.Id));
            Assert.That(pairRequestedId, Is.Null);

            tappedId = null;
            FindClickAction(pairButton.clickable)();
            Assert.That(pairRequestedId, Is.EqualTo(second.Id));
            Assert.That(tappedId, Is.Null);
        }

        [Test]
        public void LedgerAnimalRow_ForASexUnknownAnimal_HasNoPairButton()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, LedgerNow, new TimeService(() => LedgerNow));
            var baby = view.Session.Colony.AddAnimal(new PetState { Name = "ベビー", SexRevealed = false }, view.Session.Colony.AddCage(CageSize.Standard));

            RenderLedger();

            Assert.That(ledgerList.Q<Button>($"ledger-pair-{baby.Id}"), Is.Null);
        }

        [Test]
        public void OnLedgerPairRequested_PreselectsThatAnimalAndOpensTheBreedingTab()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, LedgerNow, new TimeService(() => LedgerNow));
            var second = view.Session.Colony.AddAnimal(new PetState { Name = "ふたり目", SexRevealed = true, Sex = Sex.Male }, view.Session.Colony.AddCage(CageSize.Standard));

            view.OnLedgerPairRequested(second.Id);

            Assert.That(NavigatorOf().Tab, Is.EqualTo(ShellTab.Breeding));
            Assert.That(PairingViewOf().SelectedMaleId, Is.EqualTo(second.Id));
        }

        [Test]
        public void LedgerMoneySection_AfterFeeding_TheNewestRowIsTheFoodCharge()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, LedgerNow, new TimeService(() => LedgerNow));

            view.OnFeedClicked();
            RenderLedger();
            ClickNamed("ledger-section-money");
            RenderLedger();

            var lastEntry = view.Session.Colony.Wallet.Ledger.Last();
            Assert.That(lastEntry.Category, Is.EqualTo(LedgerCategory.Food));
            Assert.That(lastEntry.Amount, Is.EqualTo(-30));

            var noteLabels = ledgerList.Query<Label>(className: "ledger-row-detail-label").ToList();
            var amountLabels = ledgerList.Query<Label>(className: "ledger-money-amount-label").ToList();
            Assert.That(noteLabels[0].text, Is.EqualTo(LedgerText.EntryLine(lastEntry, view.Session.Calendar)));
            Assert.That(amountLabels[0].text, Is.EqualTo("-¥30"));

            var calendar = view.Session.Calendar;
            var totals = LedgerText.MonthTotals(view.Session.Colony.Wallet.Ledger, calendar, calendar.MonthIndexAt(LedgerNow));
            Assert.That(ledgerSummaryLabel.text, Is.EqualTo(LedgerText.MonthSummary(totals.Income, totals.Expense)));
        }

        [Test]
        public void LedgerMoneySection_AfterBuyingAnAnimal_ShowsTheAnimalPurchaseRow()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, LedgerNow, new TimeService(() => LedgerNow));
            view.Session.Colony.AddCage(CageSize.Standard);
            view.Session.Colony.Wallet.Money = 1_000_000;
            var offer = view.Session.Colony.Shop.Offers[0];

            view.OnShopOfferBuyRequested(offer.OfferId);
            ClickConfirmYes();

            ClickNamed("ledger-section-money");
            RenderLedger();

            var lastEntry = view.Session.Colony.Wallet.Ledger.Last();
            Assert.That(lastEntry.Category, Is.EqualTo(LedgerCategory.AnimalPurchase));
            var noteLabels = ledgerList.Query<Label>(className: "ledger-row-detail-label").ToList();
            Assert.That(noteLabels[0].text, Is.EqualTo(LedgerText.EntryLine(lastEntry, view.Session.Calendar)));
        }

        // ---- Breeding on the cage detail screen (§7, §11 Task 11): weakness, visitors, nest box, debug pair. ----

        [Test]
        public void Render_WhenTheAnimalIsWeak_ShowsTheWeakStatusInRedOnTheDetailScreen()
        {
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var pet = new PetState { Name = "レオパ1", Weak = true, LastSavedAtUtc = nowUtc, NextShedAtUtc = nowUtc.AddDays(3) };
            var path = SaveColonyWith(pet, nowUtc);

            view.LoadColony(path, nowUtc, new TimeService(() => nowUtc));

            StringAssert.StartsWith(BreedingText.WeakStatus, breedingStatusLabel.text);
            Assert.That(breedingStatusLabel.style.visibility.value, Is.EqualTo(Visibility.Visible));
            Assert.That(breedingStatusLabel.ClassListContains("breeding-status-weak"), Is.True);
        }

        [Test]
        public void SelectCage_ForAMalesCageWithAVisitor_ShowsTheVisitingLabel()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            var maleCage = colony.AddCage(CageSize.Standard);
            var male = colony.AddAnimal(new PetState { Name = "タロウ", Sex = Sex.Male, SexRevealed = true, Stage = GrowthStage.Adult, WeightGrams = 55d, HatchedAtUtc = ShopNow.AddDays(-30) }, maleCage);
            var femaleCage = colony.AddCage(CageSize.Standard);
            var female = colony.AddAnimal(new PetState { Name = "ハナ", Sex = Sex.Female, SexRevealed = true, Stage = GrowthStage.Adult, WeightGrams = 55d, HatchedAtUtc = ShopNow.AddDays(-30) }, femaleCage);
            Assert.That(view.Session.Breeding.StartPairing(colony, male.Id, female.Id, ShopNow, view.Session.Calendar), Is.EqualTo(PairingProblem.None));

            var selected = view.SelectCage(maleCage.Id);

            Assert.That(selected, Is.True);
            StringAssert.Contains("訪問中", breedingStatusLabel.text);
            StringAssert.Contains("ハナ", breedingStatusLabel.text);
        }

        [Test]
        public void ShowCageStep_SkipsAnAwayCageWhoseResidentIsVisitingAnotherCage()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            var starterCage = colony.Cages[0];
            var maleCage = colony.AddCage(CageSize.Standard);
            var male = colony.AddAnimal(new PetState { Name = "タロウ", Sex = Sex.Male, SexRevealed = true, Stage = GrowthStage.Adult, WeightGrams = 55d, HatchedAtUtc = ShopNow.AddDays(-30) }, maleCage);
            var femaleCage = colony.AddCage(CageSize.Standard);
            var female = colony.AddAnimal(new PetState { Name = "ハナ", Sex = Sex.Female, SexRevealed = true, Stage = GrowthStage.Adult, WeightGrams = 55d, HatchedAtUtc = ShopNow.AddDays(-30) }, femaleCage);
            Assert.That(view.Session.Breeding.StartPairing(colony, male.Id, female.Id, ShopNow, view.Session.Calendar), Is.EqualTo(PairingProblem.None));
            view.SelectCage(starterCage.Id);

            view.ShowCageStep(1);

            Assert.That(view.CurrentCage, Is.SameAs(maleCage), "the away femaleCage must be skipped");
        }

        [Test]
        public void OnLedgerAnimalTapped_ForAVisitingFemale_OpensTheMalesCage()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            var maleCage = colony.AddCage(CageSize.Standard);
            var male = colony.AddAnimal(new PetState { Name = "タロウ", Sex = Sex.Male, SexRevealed = true, Stage = GrowthStage.Adult, WeightGrams = 55d, HatchedAtUtc = ShopNow.AddDays(-30) }, maleCage);
            var femaleCage = colony.AddCage(CageSize.Standard);
            var female = colony.AddAnimal(new PetState { Name = "ハナ", Sex = Sex.Female, SexRevealed = true, Stage = GrowthStage.Adult, WeightGrams = 55d, HatchedAtUtc = ShopNow.AddDays(-30) }, femaleCage);
            Assert.That(view.Session.Breeding.StartPairing(colony, male.Id, female.Id, ShopNow, view.Session.Calendar), Is.EqualTo(PairingProblem.None));

            view.OnLedgerAnimalTapped(female.Id);
            view.OnFamilyTreeCageClicked();

            Assert.That(view.CurrentCage, Is.SameAs(maleCage));
            Assert.That(view.State, Is.SameAs(male));
        }

        [Test]
        public void OnNestBoxButtonClicked_PlacesAndRemovesTheNestBoxUpdatingTheButtonAndInventory()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            view.Session.Colony.Inventory.Add(ShopCatalog.NestBoxId, 1);
            var cage = view.CurrentCage;

            view.OnNestBoxButtonClicked();

            Assert.That(cage.HasNestBox, Is.True);
            Assert.That(nestBoxButton.text, Is.EqualTo("産卵床を外す"));
            Assert.That(view.Session.Colony.Inventory.Count(ShopCatalog.NestBoxId), Is.EqualTo(0));
            Assert.That(feedbackLabel.text, Is.EqualTo(BreedingText.NestBoxPlacedMessage));

            view.Session.Colony.Eggs.Add(new Egg { Id = 1, CageId = cage.Id, Place = EggPlace.NestBox });

            view.OnNestBoxButtonClicked();

            Assert.That(cage.HasNestBox, Is.True, "cannot remove a nest box with eggs inside");
            Assert.That(feedbackLabel.text, Is.EqualTo(BreedingText.NestBoxMessage(NestBoxResult.EggsInside)));
        }

        [Test]
        public void OnDebugSimulate12HoursClicked_RepeatedlyUntilPairingEnds_ShowsThePairingSuccessMessage()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            var maleCage = colony.AddCage(CageSize.Standard);
            var male = colony.AddAnimal(new PetState
            {
                Name = "タロウ", Sex = Sex.Male, SexRevealed = true, Stage = GrowthStage.Adult,
                WeightGrams = 55d, HatchedAtUtc = ShopNow.AddDays(-30), PersonalityKnown = true,
            }, maleCage);
            var femaleCage = colony.AddCage(CageSize.Standard);
            var female = colony.AddAnimal(new PetState
            {
                Name = "ハナ", Sex = Sex.Female, SexRevealed = true, Stage = GrowthStage.Adult,
                WeightGrams = 55d, HatchedAtUtc = ShopNow.AddDays(-30), PersonalityKnown = true,
            }, femaleCage);
            Assert.That(view.Session.Breeding.StartPairing(colony, male.Id, female.Id, ShopNow, view.Session.Calendar), Is.EqualTo(PairingProblem.None));
            colony.PairingOf(female).SuccessChance = 1d; // pin the mating roll to always succeed

            for (var i = 0; i < 7 && female.Gravid == null; i++)
            {
                view.OnDebugSimulate12HoursClicked();
            }

            StringAssert.StartsWith("ペアリング成功！", feedbackLabel.text);
            Assert.That(female.Gravid, Is.Not.Null);
        }

        [Test]
        public void OnDebugSimulate12HoursClicked_ContinuedPastGravid_LaysAnEggWithoutErrors()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            // A fresh colony seeds one starter baby (Colony.CreateNew) that this test never
            // feeds; left in place it goes Weak partway through the loops below and its
            // feedback message crowds out the egg-laid message this test asserts on.
            colony.RemoveAnimal(colony.Animals[0]);
            var maleCage = colony.AddCage(CageSize.Standard);
            var male = colony.AddAnimal(new PetState
            {
                Name = "タロウ", Sex = Sex.Male, SexRevealed = true, Stage = GrowthStage.Adult,
                WeightGrams = 55d, HatchedAtUtc = ShopNow.AddDays(-30), PersonalityKnown = true,
            }, maleCage);
            var femaleCage = colony.AddCage(CageSize.Standard);
            femaleCage.HasNestBox = true; // without one, every laid egg is Loose and dries out
            // (LooseEggDryGameDays) well within a single debug click, so colony.Eggs never holds one
            var female = colony.AddAnimal(new PetState
            {
                Name = "ハナ", Sex = Sex.Female, SexRevealed = true, Stage = GrowthStage.Adult,
                WeightGrams = 55d, HatchedAtUtc = ShopNow.AddDays(-30), PersonalityKnown = true,
            }, femaleCage);
            Assert.That(view.Session.Breeding.StartPairing(colony, male.Id, female.Id, ShopNow, view.Session.Calendar), Is.EqualTo(PairingProblem.None));
            colony.PairingOf(female).SuccessChance = 1d; // pin the mating roll to always succeed

            // Reaching the first clutch takes up to ~28 in-game days (see below), long enough that
            // unattended hunger decay would otherwise starve both animals into BreedingEnd.Weak
            // before any egg is laid. Top up care stats before every tick, as a diligent player
            // would, so the test isolates the breeding/clutch path rather than the hunger path
            // (which has its own coverage elsewhere).
            void KeepAnimalsCaredFor()
            {
                foreach (var pet in new[] { male, female })
                {
                    pet.Hunger = 100d;
                    pet.Hydration = 100d;
                    pet.Cleanliness = 100d;
                    pet.Health = 100d;
                }
            }

            for (var i = 0; i < 7 && female.Gravid == null; i++)
            {
                KeepAnimalsCaredFor();
                view.OnDebugSimulate12HoursClicked();
            }
            Assert.That(female.Gravid, Is.Not.Null, "precondition: pairing must reach Gravid before this test can check egg-laying");

            // OnDebugSimulate12HoursClicked advances the clock by TimeSpan.FromHours(12) on the
            // same axis GameCalendar.RealTimeFor uses (1 game day = 48 minutes on that axis), so
            // one click is ~15 game-calendar-days, not the 0.5 the "12 hours" label suggests.
            // FirstClutchMinGameDays/MaxGameDays (21-28 game days = ~16.8-22.4 hours on this
            // axis) falls comfortably inside a single click from here, so a handful of clicks is
            // a generous margin. An earlier version of this test looped up to 70 clicks (~1050
            // game-days) expecting to need that many.
            for (var i = 0; i < 10 && colony.Eggs.Count == 0; i++)
            {
                KeepAnimalsCaredFor();
                view.OnDebugSimulate12HoursClicked();
            }

            Assert.That(colony.Eggs.Any(e => e.MotherId == female.Id && e.FatherId == male.Id), Is.True,
                "the debug time multiplier should be able to carry a pairing all the way through to a real, " +
                "simulation-laid egg (via BreedingService.Lay), not just to Gravid");
            // BreedingText formats the nest-box case as "{name}が産卵床に卵を{n}個産みました"
            // (the loose-egg wording omits "産卵床に"); the cage here has a nest box.
            StringAssert.Contains($"{female.Name}が産卵床に卵を", feedbackLabel.text);
        }

        [Test]
        public void OnDebugAddPairClicked_AddsAnAdultMaleAndFemaleBothAbleToBreed()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            var animalsBefore = colony.Animals.Count;

            view.OnDebugAddPairClicked();

            Assert.That(colony.Animals.Count, Is.EqualTo(animalsBefore + 2));
            var added = colony.Animals.Skip(animalsBefore).ToList();
            Assert.That(added.Count(a => a.Sex == Sex.Male), Is.EqualTo(1));
            Assert.That(added.Count(a => a.Sex == Sex.Female), Is.EqualTo(1));
            foreach (var pet in added)
            {
                Assert.That(pet.Stage, Is.EqualTo(GrowthStage.Adult));
                Assert.That(view.Session.Breeding.CheckCandidate(colony, pet, ShopNow, view.Session.Calendar), Is.EqualTo(PairingProblem.None));
            }
        }

        private void ClickNamed(string buttonName)
        {
            var button = ledgerPanel.Q<Button>(buttonName);
            Assert.That(button, Is.Not.Null, buttonName);
            var action = FindClickAction(button.clickable);
            Assert.That(action, Is.Not.Null, $"{buttonName} has no click action wired up");
            action();
        }

        // ---- Breeding tab (§13): ongoing pairings, picking a pair, forecast, start/cancel. ----

        private PairingView PairingViewOf() =>
            (PairingView)typeof(TerrariumView).GetField("pairingView", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        private ShellNavigator NavigatorOf() =>
            (ShellNavigator)typeof(TerrariumView).GetField("navigator", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        private void RenderBreeding() =>
            PairingViewOf().Render(view.Session.Colony, view.Session.Breeding, view.Session.Calendar, view.Session.GameNowUtc, view.Session.Room);

        private void ClickBreedingPanelButton(string buttonName)
        {
            var button = breedingPanel.Q<Button>(buttonName);
            Assert.That(button, Is.Not.Null, buttonName);
            var action = FindClickAction(button.clickable);
            Assert.That(action, Is.Not.Null, $"{buttonName} has no click action wired up");
            action();
        }

        [Test]
        public void OnPairButtonClicked_WithASexKnownAnimal_PreselectsItAndOpensTheBreedingTab()
        {
            var female = new PetState { Name = "メス", SexRevealed = true, Sex = Sex.Female };
            var path = SaveColonyWith(female, ShopNow);
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));

            // pair-button's clicked handler is wired in Awake() (skipped by this harness, see
            // SetUp); call the handler directly, same technique as OnPairingStartRequested above.
            view.OnPairButtonClicked();

            Assert.That(NavigatorOf().Tab, Is.EqualTo(ShellTab.Breeding));
            Assert.That(PairingViewOf().SelectedFemaleId, Is.EqualTo(view.State.Id));
        }

        [Test]
        public void OnPairButtonClicked_WithASexUnknownAnimal_StillOpensTheBreedingTab()
        {
            var baby = new PetState { Name = "ベビー", SexRevealed = false };
            var path = SaveColonyWith(baby, ShopNow);
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));

            view.OnPairButtonClicked();

            Assert.That(NavigatorOf().Tab, Is.EqualTo(ShellTab.Breeding));
            Assert.That(PairingViewOf().SelectedFemaleId, Is.Null);
            Assert.That(PairingViewOf().SelectedMaleId, Is.Null);
        }

        [Test]
        public void BreedingTab_WithAnEligiblePair_ShowsForecastAndStartsThePairingOnConfirm()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            var animalsBefore = colony.Animals.Count;
            view.OnDebugAddPairClicked();
            var added = colony.Animals.Skip(animalsBefore).ToList();
            var male = added.Single(a => a.Sex == Sex.Male);
            var female = added.Single(a => a.Sex == Sex.Female);

            RenderBreeding();

            var pairingView = PairingViewOf();
            Assert.That(breedingList.Q<Button>($"breeding-candidate-{female.Id}"), Is.Not.Null);
            Assert.That(breedingList.Q<Button>($"breeding-candidate-{male.Id}"), Is.Not.Null);

            ClickBreedingPanelButton($"breeding-candidate-{female.Id}");
            ClickBreedingPanelButton($"breeding-candidate-{male.Id}");

            Assert.That(pairingView.SelectedFemaleId, Is.EqualTo(female.Id));
            Assert.That(pairingView.SelectedMaleId, Is.EqualTo(male.Id));

            var forecastLines = breedingList.Query<Label>(className: "breeding-forecast-line").ToList().Select(l => l.text).ToList();
            Assert.That(forecastLines.Any(l => l.Contains("相性：")), Is.True);
            Assert.That(forecastLines.Any(l => l.Contains("交尾の成功率：約")), Is.True);

            var startButton = breedingList.Q<Button>("breeding-start-button");
            Assert.That(startButton.enabledSelf, Is.True);

            // PairingView.StartRequested is only wired to TerrariumView in Awake(), which this
            // harness skips (state is already set before SetActive fires) — call the handler
            // directly, exactly as the existing ShopView tests call OnShopOfferBuyRequested.
            view.OnPairingStartRequested(male.Id, female.Id);
            ClickConfirmYes();

            Assert.That(colony.Pairings.Count, Is.EqualTo(1));
            Assert.That(colony.IsVisiting(female), Is.True);
        }

        [Test]
        public void BreedingTab_StartingAPairing_WhileViewingTheFemalesCage_FollowsHerToTheMalesCage()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            var animalsBefore = colony.Animals.Count;
            view.OnDebugAddPairClicked();
            var added = colony.Animals.Skip(animalsBefore).ToList();
            var male = added.Single(a => a.Sex == Sex.Male);
            var female = added.Single(a => a.Sex == Sex.Female);

            view.SelectCage(colony.CageOf(female).Id);
            Assert.That(view.State, Is.SameAs(female));

            view.OnPairingStartRequested(male.Id, female.Id);
            ClickConfirmYes();

            Assert.That(view.CurrentCage, Is.SameAs(colony.CageOf(male)));
            Assert.That(view.State, Is.SameAs(male));
        }

        [Test]
        public void BreedingTab_WithASexUnknownBaby_HidesItsRowAndShowsTheUnknownSexNotice()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            foreach (var existing in colony.Animals)
            {
                existing.SexRevealed = true; // isolate the notice's "1匹" count to just our new baby
            }

            var cage = colony.AddCage(CageSize.Standard);
            var baby = colony.AddAnimal(new PetState { Name = "ベビー", SexRevealed = false }, cage);

            RenderBreeding();

            Assert.That(breedingList.Q<Button>($"breeding-candidate-{baby.Id}"), Is.Null);
            var lines = breedingList.Query<Label>(className: "breeding-line").ToList().Select(l => l.text).ToList();
            Assert.That(lines.Any(l => l.Contains("性別が分かっていない個体（1匹）は選べません")), Is.True);
        }

        [Test]
        public void BreedingTab_WithAWeakCandidate_BlocksTheRowWithTheWeakReason()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            var animalsBefore = colony.Animals.Count;
            view.OnDebugAddPairClicked();
            var female = colony.Animals.Skip(animalsBefore).Single(a => a.Sex == Sex.Female);
            female.Weak = true;

            RenderBreeding();

            var row = breedingList.Q<Button>($"breeding-candidate-{female.Id}");
            Assert.That(row, Is.Not.Null);
            Assert.That(row.enabledSelf, Is.False);
            Assert.That(row.ClassListContains("breeding-row-blocked"), Is.True);
            var reasonLabels = row.Query<Label>(className: "breeding-row-reason").ToList().Select(l => l.text).ToList();
            Assert.That(reasonLabels, Has.Some.Contains("衰弱中です"));
        }

        [Test]
        public void BreedingTab_OutOfSeason_DisablesStartButtonWithTheSeasonReason()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            var animalsBefore = colony.Animals.Count;
            view.OnDebugAddPairClicked();
            var added = colony.Animals.Skip(animalsBefore).ToList();
            var male = added.Single(a => a.Sex == Sex.Male);
            var female = added.Single(a => a.Sex == Sex.Female);

            // The in-game calendar starts at April (month 4) when the colony is created and
            // compresses real time 48x (GameCalendar.RealMinutesPerGameDay); 200 game days
            // lands on game-day 200 -> October (month 10), out of the March-September season.
            view.Session.SimulateGameTime(GameCalendar.RealTimeFor(200));

            RenderBreeding();
            var pairingView = PairingViewOf();
            ClickBreedingPanelButton($"breeding-candidate-{female.Id}");
            ClickBreedingPanelButton($"breeding-candidate-{male.Id}");

            var startButton = breedingList.Q<Button>("breeding-start-button");
            Assert.That(startButton.enabledSelf, Is.False);
            var reasonLabels = breedingList.Query<Label>(className: "breeding-row-reason").ToList().Select(l => l.text).ToList();
            Assert.That(reasonLabels, Has.Some.Contains("繁殖期ではありません"));
        }

        [Test]
        public void BreedingTab_CancelOngoingPairing_ConfirmYes_EndsItAndReturnsTheFemaleHome()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            var animalsBefore = colony.Animals.Count;
            view.OnDebugAddPairClicked();
            var added = colony.Animals.Skip(animalsBefore).ToList();
            var male = added.Single(a => a.Sex == Sex.Male);
            var female = added.Single(a => a.Sex == Sex.Female);
            var femaleHomeCage = colony.CageOf(female);
            Assert.That(view.Session.Breeding.StartPairing(colony, male.Id, female.Id, ShopNow, view.Session.Calendar), Is.EqualTo(PairingProblem.None));

            RenderBreeding();
            Assert.That(breedingList.Q<Button>($"breeding-cancel-{female.Id}"), Is.Not.Null);
            view.OnPairingCancelRequested(female.Id);
            ClickConfirmYes();

            Assert.That(colony.Pairings, Is.Empty);
            Assert.That(colony.IsVisiting(female), Is.False);
            Assert.That(colony.CageOf(female), Is.SameAs(femaleHomeCage));
        }

        [Test]
        public void BreedingTab_FemaleGoesWeakWhileConfirmationIsOpen_DoesNotStartAndShowsTheWeakReason()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            var animalsBefore = colony.Animals.Count;
            view.OnDebugAddPairClicked();
            var added = colony.Animals.Skip(animalsBefore).ToList();
            var male = added.Single(a => a.Sex == Sex.Male);
            var female = added.Single(a => a.Sex == Sex.Female);

            RenderBreeding();
            ClickBreedingPanelButton($"breeding-candidate-{female.Id}");
            ClickBreedingPanelButton($"breeding-candidate-{male.Id}");
            view.OnPairingStartRequested(male.Id, female.Id);

            female.Weak = true; // time passes while the dialog is open

            ClickConfirmYes();

            Assert.That(colony.Pairings, Is.Empty);
            StringAssert.Contains("衰弱中です", breedingMessageLabel.text);
        }

        // ---- Events tab (phase 6 Task 12): the preparation screen. ----

        private PetState OpenEventTab(out int number)
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            NavigatorOf().ShowTab(ShellTab.Events);
            view.WireEventEvents();
            var session = view.Session;
            number = session.Events.OpenNumber(session.Colony, session.Calendar, session.GameNowUtc);
            var second = session.Colony.AddAnimal(new PetState { Name = "ふたり目" }, session.Colony.AddCage(CageSize.Standard));
            RenderEvents();
            return second;
        }

        private void RenderEvents()
        {
            var eventView = (EventView)typeof(TerrariumView).GetField("eventView", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            eventView.Render(view.Session, view.Session.GameNowUtc);
        }

        private T EventElement<T>(string name) where T : VisualElement
        {
            var element = eventsPanel.Q<T>(name);
            Assert.That(element, Is.Not.Null, name);
            return element;
        }

        private IEnumerable<string> EventTexts() => eventsPanel.Query<Label>().ToList().Select(l => l.text);

        [Test]
        public void EventsTab_ShowsThePrepSurfaceWithTheNameAndFee()
        {
            OpenEventTab(out var number);

            Assert.That(eventsPanel.Q("event-prep").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(eventsPanel.Q("event-day").style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(eventsPanel.Q("event-result").style.display.value, Is.EqualTo(DisplayStyle.None));
            StringAssert.Contains(EventSchedule.NameOf(number), EventElement<Label>("event-prep-title").text);
            StringAssert.Contains(EventText.Yen(EventSchedule.BoothFee(EventSchedule.KindOf(number))), EventElement<Label>("event-prep-info").text);
        }

        [Test]
        public void EventsTab_GravidAnimalCannotBeChecked_AndShowsTheReason()
        {
            var second = OpenEventTab(out _);
            second.Gravid = new GravidState();
            // The signature changes with the animal's problem, so the next render rebuilds the rows.
            RenderEvents();

            Assert.That(EventElement<Button>($"event-check-{second.Id}").enabledSelf, Is.False);
            Assert.That(EventTexts(), Has.Member("抱卵中"));
        }

        [Test]
        public void EventsTab_RegisteringTwoAnimals_PaysTheFeeAndMakesTheEntry()
        {
            var second = OpenEventTab(out var number);
            var colony = view.Session.Colony;
            var first = colony.Animals.First(a => a.Id != second.Id);
            var before = colony.Wallet.Money;

            FindClickAction(EventElement<Button>($"event-check-{first.Id}").clickable)();
            FindClickAction(EventElement<Button>($"event-check-{second.Id}").clickable)();
            FindClickAction(EventElement<Button>("event-prep-register-button").clickable)();
            ClickConfirmYes();
            RenderEvents();

            Assert.That(colony.Wallet.Money, Is.EqualTo(before - EventSchedule.BoothFee(EventSchedule.KindOf(number))));
            Assert.That(colony.Events.Entry, Is.Not.Null);
            Assert.That(colony.Events.Entry.Listings.Count, Is.EqualTo(2));
            StringAssert.Contains("申し込み済み", EventElement<Label>("event-prep-title").text);
        }

        [Test]
        public void EventsTab_PlusRaisesThePriceByOneStep_AndLowersTheChance()
        {
            var second = OpenEventTab(out _);
            var priceLabel = EventElement<Label>($"event-price-{second.Id}");
            var chanceLabel = EventElement<Label>($"event-chance-{second.Id}");
            var priceBefore = priceLabel.text;
            var chanceBefore = chanceLabel.text;

            FindClickAction(EventElement<Button>($"event-plus-{second.Id}").clickable)();

            Assert.That(priceLabel.text, Is.Not.EqualTo(priceBefore));
            var parse = new Func<string, long>(t => long.Parse(t.Replace("円", string.Empty).Replace(",", string.Empty)));
            Assert.That(parse(priceLabel.text), Is.EqualTo(parse(priceBefore) + 1000L));
            var percent = new Func<string, int>(t => int.Parse(new string(t.Where(char.IsDigit).ToArray())));
            Assert.That(percent(chanceLabel.text), Is.LessThan(percent(chanceBefore)));

            // A render with nothing changed must keep the picked value.
            RenderEvents();
            Assert.That(parse(priceLabel.text), Is.EqualTo(parse(priceBefore) + 1000L));
        }

        // ---- Phase 6 Task 14: notices, the listed status, the debug skip. ----

        private static void InvokeHandleReport(TerrariumView target, ColonyTickReport report) =>
            typeof(TerrariumView).GetMethod("HandleReport", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, new object[] { report });

        [Test]
        public void ListedAnimal_ShowsTheListedStatusOnTheRackAndInTheCageRow()
        {
            var second = OpenEventTab(out var number);
            var session = view.Session;
            var requests = new List<ListingRequest> { new ListingRequest { AnimalId = second.Id, Price = 1000L, TolerancePercent = 0 } };
            Assert.That(session.Events.Register(session.Colony, session.Calendar, number, requests, session.GameNowUtc), Is.EqualTo(EventRegisterResult.Ok));

            var rackList = new VisualElement();
            new HomeView(rackList).Render(session.Colony, session.GameNowUtc, view.Tuning);
            var texts = rackList.Query<Label>(className: "rack-alert").ToList().ConvertAll(l => l.text);
            Assert.That(texts.Exists(t => t.Contains(EventText.ListedStatus)), Is.True);

            var cage = session.Colony.CageOf(second);
            StringAssert.Contains(EventText.ListedStatus, CageStatusText.AlertsFor(session.Colony, cage, second, session.GameNowUtc, view.Tuning));
        }

        [Test]
        public void ShopWholesale_OfAListedAnimal_IsRefusedWithTheListedMessage()
        {
            var second = OpenEventTab(out var number);
            var session = view.Session;
            var requests = new List<ListingRequest> { new ListingRequest { AnimalId = second.Id, Price = 1000L, TolerancePercent = 0 } };
            Assert.That(session.Events.Register(session.Colony, session.Calendar, number, requests, session.GameNowUtc), Is.EqualTo(EventRegisterResult.Ok));
            var money = session.Colony.Wallet.Money;

            view.OnShopWholesaleRequested(second.Id);

            Assert.That(shopMessageLabel.text, Is.EqualTo("出品中の個体は売れません"));
            Assert.That(session.Colony.Animals.Contains(second), Is.True);
            Assert.That(session.Colony.Wallet.Money, Is.EqualTo(money));
        }

        [Test]
        public void DebugNextEventDay_ForARegisteredEvent_ShowsTheDayStartedNotice()
        {
            var second = OpenEventTab(out var number);
            var session = view.Session;
            var requests = new List<ListingRequest> { new ListingRequest { AnimalId = second.Id, Price = 1000L, TolerancePercent = 0 } };
            Assert.That(session.Events.Register(session.Colony, session.Calendar, number, requests, session.GameNowUtc), Is.EqualTo(EventRegisterResult.Ok));

            view.OnDebugNextEventDayClicked();

            StringAssert.Contains(EventText.DayStartedNotice(number), feedbackLabel.text);
        }

        [Test]
        public void EventDeadlineNotice_AppearsThreeDaysBeforeTheDeadline()
        {
            OpenEventTab(out var number);
            var session = view.Session;
            var days = EventSchedule.GameDaysUntilStart(session.Calendar, number, session.GameNowUtc);
            var report = session.SimulateGameTime(GameCalendar.RealTimeFor(days - 3d + 0.1d));
            InvokeHandleReport(view, report);

            Assert.That(report.Events.DeadlineNoticeNumber, Is.EqualTo(number));
            StringAssert.Contains(EventText.DeadlineNotice(number, 3), feedbackLabel.text);
        }

        // ---- Events tab (phase 6 Task 13): the event day and result screens. ----

        private PetState OpenEventDay(long listingPrice = 1000L)
        {
            var second = OpenEventTab(out var number);
            var session = view.Session;
            var requests = new List<ListingRequest> { new ListingRequest { AnimalId = second.Id, Price = listingPrice, TolerancePercent = 0 } };
            Assert.That(session.Events.Register(session.Colony, session.Calendar, number, requests, session.GameNowUtc), Is.EqualTo(EventRegisterResult.Ok));
            session.SkipToNextEventDay();
            RenderEvents();
            return second;
        }

        private void ClickEventButton(string name) => FindClickAction(EventElement<Button>(name).clickable)();

        private EventView EventViewOf() =>
            (EventView)typeof(TerrariumView).GetField("eventView", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        private static bool Shown(VisualElement element) => element.resolvedStyle.display != DisplayStyle.None && element.style.display != DisplayStyle.None;

        [Test]
        public void EventDay_AfterSkippingToTheDay_ShowsTheDaySurface()
        {
            OpenEventDay();

            Assert.That(Shown(EventElement<VisualElement>("event-day")), Is.True);
            Assert.That(Shown(EventElement<VisualElement>("event-prep")), Is.False);
            StringAssert.Contains("残りの行動", EventElement<Label>("event-day-left").text);
        }

        private void ShowReplayLine(string bubble)
        {
            var eventView = EventViewOf();
            ClickEventButton("event-day-serve-button");
            eventView.Tick(0.1f);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(EventView).GetField("replayLines", flags).SetValue(eventView, new List<ReplayLine> { new ReplayLine { Bubble = bubble } });
            typeof(EventView).GetField("replayRevealed", flags).SetValue(eventView, 1);
            typeof(EventView).GetMethod("UpdateVisitor", flags).Invoke(eventView, null);
        }

        [Test]
        public void EventDay_AClerkLine_IsDrawnInTheClerkAnchor_NotOverTheCustomer()
        {
            OpenEventDay();
            ShowReplayLine(EventReplay.ServeBubble);

            var bubble = EventElement<VisualElement>("event-bubble");
            Assert.That(Shown(bubble), Is.True);
            Assert.That(bubble.ClassListContains("event-bubble--clerk"), Is.True);
            Assert.That(bubble.style.left.keyword, Is.EqualTo(StyleKeyword.Null), "the clerk anchor is USS-driven, not the customer's left");
            Assert.That(bubble.Q<VisualElement>("event-bubble-tail"), Is.Not.Null);
        }

        [Test]
        public void EventDay_ASaleLine_IsDrawnOverTheCustomer()
        {
            OpenEventDay();
            ShowReplayLine(EventReplay.SaleBubble);

            var bubble = EventElement<VisualElement>("event-bubble");
            Assert.That(Shown(bubble), Is.True);
            Assert.That(bubble.ClassListContains("event-bubble--clerk"), Is.False);
            Assert.That(bubble.style.left.value.value, Is.EqualTo(Mathf.Max(EventViewOf().CustomerLeft + 3f, 1f)).Within(0.01f));
        }

        [Test]
        public void EventDay_CrowdWalksInTwoRows_OnTheDayScreen_AndKeepsMoving()
        {
            OpenEventDay();
            var eventView = EventViewOf();

            Assert.That(eventView.VisibleCrowdCount, Is.GreaterThan(1));
            Assert.That(eventView.VisibleCrowdRows(true), Is.GreaterThan(0));
            Assert.That(eventView.VisibleCrowdRows(false), Is.GreaterThan(0));
            var before = EventElement<VisualElement>("event-passerby-near-0").style.left.value.value;

            eventView.Tick(0.2f);

            Assert.That(EventElement<VisualElement>("event-passerby-near-0").style.left.value.value, Is.Not.EqualTo(before));
            Assert.That(eventView.VisibleCrowdCount, Is.GreaterThan(1));
        }

        [Test]
        public void EventDay_TheCustomerStopsAtTheCounterWhileTheLinesPlay_ThenWalksOff()
        {
            OpenEventDay();
            var eventView = EventViewOf();
            ClickEventButton("event-day-serve-button");
            var customer = EventElement<VisualElement>("event-visitor");

            eventView.Tick(0.5f);
            var walking = eventView.CustomerLeft;
            Assert.That(walking, Is.Not.EqualTo(EventView.CustomerStopPercent).Within(0.01f));
            for (var i = 0; i < 12; i++)
            {
                eventView.Tick(0.25f);
            }

            Assert.That(eventView.Playing, Is.True);
            Assert.That(eventView.CustomerLeft, Is.EqualTo(EventView.CustomerStopPercent).Within(0.01f));
            var stopped = customer.style.left.value.value;
            eventView.Tick(0.5f);
            Assert.That(customer.style.left.value.value, Is.EqualTo(stopped));
            Assert.That(Shown(customer), Is.True);

            for (var i = 0; i < 80 && eventView.Playing; i++)
            {
                eventView.Tick(0.25f);
            }

            Assert.That(eventView.Playing, Is.False);
            Assert.That(Shown(customer), Is.True, "the customer walks off after the replay");
            for (var i = 0; i < 40; i++)
            {
                eventView.Tick(0.25f);
            }

            Assert.That(Shown(customer), Is.False);
        }

        [Test]
        public void EventDay_ServeLocksTheButtonsUntilSkipped_AndUsesOneAction()
        {
            OpenEventDay();
            var before = view.Session.Events.ActionsLeft(view.Session.Colony);

            ClickEventButton("event-day-serve-button");

            Assert.That(EventViewOf().Playing, Is.True);
            Assert.That(EventElement<Button>("event-day-serve-button").enabledSelf, Is.False);
            Assert.That(EventElement<Button>("event-day-browse-button").enabledSelf, Is.False);
            Assert.That(EventElement<Button>("event-day-serve-all-button").enabledSelf, Is.False);
            Assert.That(view.Session.Events.ActionsLeft(view.Session.Colony), Is.EqualTo(before - 1));

            ClickEventButton("event-day-skip-button");

            Assert.That(EventViewOf().Playing, Is.False);
            Assert.That(EventElement<ScrollView>("event-day-log").Query<Label>().ToList().Count, Is.GreaterThan(0));
            StringAssert.Contains($"{before - 1}/", EventElement<Label>("event-day-left").text);
            Assert.That(EventElement<Button>("event-day-serve-button").enabledSelf, Is.True);
        }

        [Test]
        public void EventDay_ACheapListing_SellsOutOfTheCageAndPaysTheOwner()
        {
            var second = OpenEventDay();
            var colony = view.Session.Colony;
            var money = colony.Wallet.Money;

            while (colony.Events.Entry != null && colony.AnimalById(second.Id) != null && view.Session.Events.ActionsLeft(colony) > 0)
            {
                ClickEventButton("event-day-serve-button");
                ClickEventButton("event-day-skip-button");
            }

            // The roll is seeded by the listing, not controlled here: assert the outcome is consistent either way.
            var sold = colony.AnimalById(second.Id) == null;
            Assert.That(colony.Wallet.Money > money, Is.EqualTo(sold));
            if (sold)
            {
                Assert.That(colony.Cages.Any(c => c.AnimalId == second.Id), Is.False);
            }
            else
            {
                Assert.That(colony.Events.Entry == null || view.Session.Events.ActionsLeft(colony) == 0, Is.True);
            }
        }

        private EventBoothOffer OpenOffer()
        {
            OpenEventDay();
            ClickEventButton("event-day-browse-button");
            ClickEventButton("event-day-skip-button");
            return view.Session.Colony.Events.Entry.BoothOffers[0];
        }

        private void FillEveryCage()
        {
            foreach (var cage in view.Session.Colony.Cages)
            {
                if (cage.IsEmpty)
                {
                    view.Session.Colony.AddAnimal(new PetState { Name = "ふさぎ" }, cage);
                }
            }
        }

        private void ClickModalButton(string name) => FindClickAction(eventsPanel.parent.Q<Button>(name).clickable)();

        private bool ModalShown(string name) => Shown(eventsPanel.parent.Q<VisualElement>(name));

        [Test]
        public void EventDay_Buy_WhenPossible_OpensTheConfirmModal()
        {
            var offer = OpenOffer();
            view.OnDebugAddCageClicked();
            view.Session.Colony.Wallet.Money = 1_000_000L;
            ClickEventButton($"event-offer-buy-{offer.OfferId}");
            StringAssert.Contains(offer.MorphLabel, eventsPanel.parent.Q<Label>("event-buy-message").text);
            Assert.That(ModalShown("event-buy-modal"), Is.True);
            Assert.That(ModalShown("event-blocked-modal"), Is.False);
        }

        [Test]
        public void EventDay_Buy_WithoutAFreeCage_ShowsTheReasonDialogWithTheShopButton()
        {
            var offer = OpenOffer();
            FillEveryCage();
            view.Session.Colony.Wallet.Money = 1_000_000L;
            ClickEventButton($"event-offer-buy-{offer.OfferId}");
            Assert.That(ModalShown("event-buy-modal"), Is.False);
            Assert.That(ModalShown("event-blocked-modal"), Is.True);
            Assert.That(eventsPanel.parent.Q<Label>("event-blocked-message").text, Is.EqualTo("ケージが足りません"));
            Assert.That(Shown(eventsPanel.parent.Q<Button>("event-blocked-shop-button")), Is.True);

            ClickModalButton("event-blocked-shop-button");
            Assert.That(ModalShown("event-blocked-modal"), Is.False);
            var navigator = (ShellNavigator)typeof(TerrariumView).GetField("navigator", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            Assert.That(navigator.Tab, Is.EqualTo(ShellTab.Shop));
            Assert.That(ShopViewOf().Section, Is.EqualTo(ShopSection.Supplies));
        }

        [Test]
        public void EventDay_Buy_WithoutMoney_ShowsTheReasonDialogWithoutTheShopButton()
        {
            var offer = OpenOffer();
            view.OnDebugAddCageClicked();
            view.Session.Colony.Wallet.Money = 0L;
            ClickEventButton($"event-offer-buy-{offer.OfferId}");
            Assert.That(ModalShown("event-buy-modal"), Is.False);
            Assert.That(ModalShown("event-blocked-modal"), Is.True);
            Assert.That(eventsPanel.parent.Q<Label>("event-blocked-message").text, Is.EqualTo("所持金が足りません"));
            Assert.That(Shown(eventsPanel.parent.Q<Button>("event-blocked-shop-button")), Is.False);

            ClickModalButton("event-blocked-close-button");
            Assert.That(ModalShown("event-blocked-modal"), Is.False);
        }

        [Test]
        public void EventDay_OpenConfirmModal_TurnsIntoTheReasonDialogWhenTheCagesRunOut()
        {
            var offer = OpenOffer();
            view.OnDebugAddCageClicked();
            view.Session.Colony.Wallet.Money = 1_000_000L;
            ClickEventButton($"event-offer-buy-{offer.OfferId}");
            Assert.That(ModalShown("event-buy-modal"), Is.True);

            FillEveryCage();
            EventViewOf().Tick(0f);
            Assert.That(ModalShown("event-buy-modal"), Is.False);
            Assert.That(ModalShown("event-blocked-modal"), Is.True);
        }

        [Test]
        public void EventDay_CageBoughtInTheShop_MakesTheKeptOfferBuyable()
        {
            var offer = OpenOffer();
            var colony = view.Session.Colony;
            FillEveryCage();
            colony.Wallet.Money = 1_000_000L;
            Assert.That(view.Session.Events.CheckBuy(colony, offer.OfferId), Is.EqualTo(EventBuyResult.NoEmptyCage));
            ClickEventButton($"event-offer-buy-{offer.OfferId}");
            ClickModalButton("event-blocked-shop-button");

            var bought = ShopServiceOf().BuyItem(colony, "cage_standard", ShopNow);
            Assert.That(bought, Is.EqualTo(ShopResult.Ok));
            Assert.That(view.Session.Events.CheckBuy(colony, offer.OfferId), Is.EqualTo(EventBuyResult.Ok));

            var navigator = (ShellNavigator)typeof(TerrariumView).GetField("navigator", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            navigator.ShowTab(ShellTab.Events);
            RenderEvents();
            Assert.That(navigator.Tab, Is.EqualTo(ShellTab.Events));
            Assert.That(colony.Events.Entry.BoothOffers.Contains(offer), Is.True);
            Assert.That(EventElement<VisualElement>("event-day-offers").Q<VisualElement>($"event-offer-{offer.OfferId}"), Is.Not.Null);
            ClickEventButton($"event-offer-buy-{offer.OfferId}");
            Assert.That(ModalShown("event-buy-modal"), Is.True);
        }

        [Test]
        public void EventDay_ServeAll_ShowsTheResult_AndCloseGoesBackToThePrepOfTheNextEvent()
        {
            OpenEventDay();

            ClickEventButton("event-day-serve-all-button");
            ClickConfirmYes();

            Assert.That(Shown(EventElement<VisualElement>("event-result")), Is.True);
            StringAssert.Contains("差し引き", EventElement<Label>("event-result-totals").text);

            ClickEventButton("event-result-close-button");

            Assert.That(Shown(EventElement<VisualElement>("event-prep")), Is.True);
            Assert.That(Shown(EventElement<VisualElement>("event-result")), Is.False);
            Assert.That(view.Session.Colony.Events.Entry, Is.Null);
        }

        // ---- Events tab (phase 6 Task 15): the whole loop through the real UI. ----

        private ColonySaveService EventSaveService() => new ColonySaveService(new CareTuning(), new EconomyTuning());

        [Test]
        public void EventPrep_UntouchedDefaults_ConfirmWarnsOfZeroListings_AndRegistersNone()
        {
            OpenEventTab(out var number);
            var colony = view.Session.Colony;
            RenderEvents();

            ClickEventButton("event-prep-register-button");
            var message = confirmMessageLabel.text;
            Assert.That(message, Is.EqualTo(EventText.ConfirmRegister(number, 0)));
            StringAssert.StartsWith("出品する動物が0匹です", message);

            ClickConfirmYes();
            Assert.That(colony.Events.Entry, Is.Not.Null);
            Assert.That(colony.Events.Entry.Listings.Count, Is.EqualTo(0));
        }

        [Test]
        public void EventPrep_OneTickedAnimal_ConfirmNamesOneListing()
        {
            var second = OpenEventTab(out var number);
            RenderEvents();
            ClickEventButton($"event-check-{second.Id}");
            ClickEventButton("event-prep-register-button");
            Assert.That(confirmMessageLabel.text, Is.EqualTo(EventText.ConfirmRegister(number, 1)));
            ClickConfirmYes();
            Assert.That(view.Session.Colony.Events.Entry.Listings.Count, Is.EqualTo(1));
        }

        [Test]
        public void EventLoop_RegisterPlayBuyAndLedger()
        {
            var second = OpenEventTab(out var number);
            var session = view.Session;
            var colony = session.Colony;
            var first = colony.Animals.First(a => a.Id != second.Id);
            var kind = EventSchedule.KindOf(number);
            var startMoney = colony.Wallet.Money;

            // Pin the listing content so the seeded sale rolls are the same on every run.
            first.Genotype = Genotype.Normal();
            second.Genotype = Genotype.Normal();
            RenderEvents();

            // Register two animals at the minimum listing price each through the buttons.
            foreach (var pet in new[] { first, second })
            {
                ClickEventButton($"event-check-{pet.Id}");
                for (var i = 0; i < 100; i++)
                {
                    ClickEventButton($"event-minus-{pet.Id}");
                }
            }

            ClickEventButton("event-prep-register-button");
            ClickConfirmYes();
            RenderEvents();
            Assert.That(colony.Events.Entry, Is.Not.Null);
            Assert.That(colony.Events.Entry.Listings.Count, Is.EqualTo(2));
            Assert.That(colony.Events.Entry.Listings.All(l => l.Price == EventView.MinListingPrice), Is.True);
            Assert.That(colony.Wallet.Money, Is.EqualTo(startMoney - EventSchedule.BoothFee(kind)));

            // Save and read back: the entry and its listings survive.
            session.Save();
            var reloaded = EventSaveService().LoadOrCreate(session.SavePath, ShopNow, new System.Random(1));
            Assert.That(reloaded.Events.Entry, Is.Not.Null);
            Assert.That(reloaded.Events.Entry.Number, Is.EqualTo(number));
            Assert.That(reloaded.Events.Entry.Listings.Count, Is.EqualTo(2));

            // Jump to the day: the notice appears and the tab shows the day surface.
            view.OnDebugNextEventDayClicked();
            StringAssert.Contains(EventText.DayStartedNotice(number), feedbackLabel.text);
            RenderEvents();
            Assert.That(Shown(EventElement<VisualElement>("event-day")), Is.True);

            // Look at the other booths once, skipping the animation, then buy one offer.
            view.OnDebugAddCageClicked();
            colony.Wallet.Money = 1_000_000L;
            var moneyBeforeDay = colony.Wallet.Money;
            ClickEventButton("event-day-browse-button");
            ClickEventButton("event-day-skip-button");
            var offer = colony.Events.Entry.BoothOffers[0];
            ClickEventButton($"event-offer-buy-{offer.OfferId}");
            Assert.That(eventsPanel.parent.Q<Button>("event-buy-yes-button").enabledSelf, Is.True);
            FindClickAction(eventsPanel.parent.Q<Button>("event-buy-yes-button").clickable)();
            Assert.That(offer.Bought, Is.True);

            // Serve every remaining action, which ends the day and shows the result.
            ClickEventButton("event-day-serve-all-button");
            ClickConfirmYes();
            Assert.That(Shown(EventElement<VisualElement>("event-result")), Is.True);

            var summary = colony.Events.LastSummary;
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Number, Is.EqualTo(number));
            Assert.That(summary.Bought.Count, Is.EqualTo(1));
            Assert.That(summary.PurchaseTotal, Is.EqualTo(offer.Price));
            Assert.That(summary.Sold.Count + summary.Unsold.Count, Is.EqualTo(2));
            foreach (var line in summary.Sold)
            {
                Assert.That(colony.AnimalById(line.AnimalId), Is.Null, "a sold animal leaves the colony");
            }

            foreach (var line in summary.Unsold)
            {
                Assert.That(colony.AnimalById(line.AnimalId), Is.Not.Null, "an unsold animal stays");
            }

            Assert.That(summary.Net, Is.EqualTo(summary.TotalSales - summary.PurchaseTotal - summary.BoothFee));
            Assert.That(colony.Wallet.Money - moneyBeforeDay, Is.EqualTo(summary.TotalSales - summary.PurchaseTotal));
            var totals = EventElement<Label>("event-result-totals").text;
            StringAssert.Contains(EventText.SignedYen(summary.Net), totals);
            StringAssert.Contains(EventText.Yen(summary.TotalSales), totals);
            StringAssert.Contains(EventText.Yen(summary.PurchaseTotal), totals);

            // Closing returns to the prep of the next event with nothing marked as listed.
            ClickEventButton("event-result-close-button");
            Assert.That(Shown(EventElement<VisualElement>("event-prep")), Is.True);
            Assert.That(colony.Events.Entry, Is.Null);
            Assert.That(colony.Events.LastSummarySeen, Is.True);
            Assert.That(EventTexts(), Has.None.EqualTo(EventText.ListedStatus));
            foreach (var cage in colony.Cages)
            {
                var shown = colony.AnimalIn(cage);
                Assert.That(shown == null || !colony.Events.IsListed(shown.Id), Is.True);
                if (shown != null)
                {
                    Assert.That(CageStatusText.AlertsFor(colony, cage, shown, session.GameNowUtc, view.Tuning), Does.Not.Contain(EventText.ListedStatus));
                }
            }

            // The money ledger carries the fee, the purchase and the sale.
            RenderLedger();
            ClickNamed("ledger-section-money");
            RenderLedger();
            var rows = ledgerList.Query<Label>(className: "ledger-row-detail-label").ToList().Select(l => l.text).ToList();
            Assert.That(rows.Any(r => r.Contains(LedgerText.CategoryLabel(LedgerCategory.BoothFee))), Is.True, "booth fee row");
            Assert.That(rows.Any(r => r.Contains(LedgerText.CategoryLabel(LedgerCategory.AnimalPurchase))), Is.True, "purchase row");
            Assert.That(summary.Sold.Count, Is.GreaterThan(0), "the pinned cheap listings sell");
            Assert.That(rows.Any(r => r.Contains(LedgerText.CategoryLabel(LedgerCategory.EventSale))), Is.True, "sale row");
            Assert.That(rows.Any(r => r.Contains(EventSchedule.NameOf(number))), Is.True, "the event name is in the notes");

            // Save once more: the summary stays seen and no entry is left.
            session.Save();
            var final = EventSaveService().LoadOrCreate(session.SavePath, ShopNow, new System.Random(1));
            Assert.That(final.Events.LastSummarySeen, Is.True);
            Assert.That(final.Events.Entry, Is.Null);
        }

        [Test]
        public void EventLoop_AppClosedPastTheEventDay_ResumesTheDaySurfaceOnLoad()
        {
            var second = OpenEventTab(out var number);
            var session = view.Session;
            var requests = new List<ListingRequest> { new ListingRequest { AnimalId = second.Id, Price = 1000L, TolerancePercent = 0 } };
            Assert.That(session.Events.Register(session.Colony, session.Calendar, number, requests, session.GameNowUtc), Is.EqualTo(EventRegisterResult.Ok));

            // The app is closed while the whole event day passes.
            var untilNextDay = EventSchedule.EndUtc(session.Calendar, number) + TimeSpan.FromHours(1) - session.GameNowUtc;
            session.SimulateGameTime(untilNextDay);
            session.Save();

            view.LoadColony(session.SavePath, ShopNow, new TimeService(() => ShopNow));
            view.WireEventEvents();
            RenderEvents();

            StringAssert.Contains(EventText.PendingNotice(number), feedbackLabel.text);
            Assert.That(view.Session.Colony.Events.Entry, Is.Not.Null);
            Assert.That(Shown(EventElement<VisualElement>("event-day")), Is.True);
            Assert.That(view.Session.Events.ActionsLeft(view.Session.Colony), Is.GreaterThan(0));
        }

        // ---- Incubator tab (phase 5 Task 15): cards, temperature, water, candled eggs. ----

        private void OpenIncubatorTab()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            NavigatorOf().ShowTab(ShellTab.Incubator);

            // Awake() (skipped by this harness) wires the view's events and the tab-changed
            // render; do the same here so the buttons and slots reach the real handlers.
            view.WireIncubatorEvents();
            RenderIncubator();
        }

        private IncubatorView IncubatorViewOf() =>
            (IncubatorView)typeof(TerrariumView).GetField("incubatorView", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        private void RenderIncubator()
        {
            var session = view.Session;
            IncubatorViewOf().Render(session.Colony, session.Incubation, session.GameNowUtc, session.Room.TemperatureC, new CareTuning());
        }

        private Button IncubatorButton(string name)
        {
            var button = incubatorPanel.Q<Button>(name);
            Assert.That(button, Is.Not.Null, name);
            return button;
        }

        private void ClickIncubatorButton(string name) => FindClickAction(IncubatorButton(name).clickable)();

        [Test]
        public void IncubatorTab_ShowsOneCardForTheStarterIncubator()
        {
            OpenIncubatorTab();

            Assert.That(incubatorPanel.Query(className: "incubator-card").ToList().Count, Is.EqualTo(1));
            var incubator = view.Session.Colony.Incubators[0];
            Assert.That(incubatorPanel.Query<Label>().ToList().Select(l => l.text), Has.Member(IncubatorText.SetTemperature(28)));
            Assert.That(incubatorPanel.Query(className: "egg-slot--empty").ToList().Count, Is.EqualTo(IncubatorRules.Capacity(incubator.Model)));
        }

        [Test]
        public void IncubatorTab_PlusRaisesTheSettingAndSavesIt_AndStopsAt34()
        {
            OpenIncubatorTab();
            var incubator = view.Session.Colony.Incubators[0];

            ClickIncubatorButton($"incubator-{incubator.Id}-temp-up");

            Assert.That(incubator.SetTemperatureC, Is.EqualTo(29));
            var reloaded = new ColonySaveService(new CareTuning(), new EconomyTuning())
                .LoadOrCreate(view.Session.SavePath, ShopNow, new System.Random(1));
            Assert.That(reloaded.Incubators[0].SetTemperatureC, Is.EqualTo(29));

            for (var i = 0; i < 5; i++)
            {
                ClickIncubatorButton($"incubator-{incubator.Id}-temp-up");
            }

            Assert.That(incubator.SetTemperatureC, Is.EqualTo(34));
            Assert.That(IncubatorButton($"incubator-{incubator.Id}-temp-up").enabledSelf, Is.False);
            Assert.That(IncubatorButton($"incubator-{incubator.Id}-temp-down").enabledSelf, Is.True);
        }

        [Test]
        public void IncubatorTab_AddWaterRaisesHumidity_AndWarnsAboveTheMouldLine()
        {
            OpenIncubatorTab();
            var incubator = view.Session.Colony.Incubators[0];
            var tuning = new CareTuning();

            ClickIncubatorButton($"incubator-{incubator.Id}-water");
            Assert.That(incubator.HumidityPercent, Is.EqualTo(80d + tuning.AddWaterPercent));
            Assert.That(incubatorPanel.Query<Label>().ToList().Select(l => l.text), Has.Member(IncubatorText.Humidity(incubator.HumidityPercent)));

            incubator.HumidityPercent = tuning.HumidityMoldAbovePercent;
            ClickIncubatorButton($"incubator-{incubator.Id}-water");
            Assert.That(feedbackLabel.text, Is.EqualTo(IncubatorText.MoldWarning(tuning)));
            Assert.That(incubatorPanel.Query(className: "humidity-bar--warning").ToList().Count, Is.EqualTo(1));
        }

        [Test]
        public void IncubatorTab_TappingADebugEggShowsItsDetailLine()
        {
            OpenIncubatorTab();
            var colony = view.Session.Colony;
            Assert.That(view.Session.Incubation.AddDebugEggs(colony, ShopNow, 3), Is.EqualTo(3));
            IncubatorViewOf().Invalidate();
            RenderIncubator();
            var egg = colony.Eggs[0];

            var slot = incubatorPanel.Q<VisualElement>($"egg-slot-{egg.Id}");
            Assert.That(slot, Is.Not.Null);
            // The hand-built tree has no panel, so a ClickEvent would not dispatch; raise the slot's tap event itself.
            view.OnIncubatorEggTapped(egg);

            StringAssert.StartsWith($"卵{egg.Id}：", incubatorEggDetail.text);
            Assert.That(incubatorPanel.Query(className: "egg-slot--selected").ToList().Count, Is.EqualTo(1));
        }

        [Test]
        public void IncubatorTab_RemoveButtonOnlyOnRemovableEggs_RemovalClearsSelectionAndSaves()
        {
            OpenIncubatorTab();
            var colony = view.Session.Colony;
            view.Session.Incubation.AddDebugEggs(colony, view.Session.GameNowUtc, 2);
            var dead = colony.Eggs[0];
            var live = colony.Eggs[1];
            dead.Fertile = false;
            dead.LaidAtUtc = view.Session.GameNowUtc - GameCalendar.RealTimeFor(8d);
            IncubatorViewOf().Invalidate();
            RenderIncubator();

            Assert.That(incubatorPanel.Q<Button>($"egg-remove-{dead.Id}"), Is.Not.Null);
            Assert.That(incubatorPanel.Q<Button>($"egg-remove-{live.Id}"), Is.Null);

            view.OnIncubatorEggTapped(dead);
            Assert.That(IncubatorViewOf().SelectedEggId, Is.EqualTo(dead.Id));
            ClickIncubatorButton($"egg-remove-{dead.Id}");

            Assert.That(colony.Eggs.Contains(dead), Is.False);
            Assert.That(IncubatorViewOf().SelectedEggId, Is.Null);
            var reloaded = new ColonySaveService(new CareTuning(), new EconomyTuning())
                .LoadOrCreate(view.Session.SavePath, ShopNow, new System.Random(1));
            Assert.That(reloaded.Eggs.Exists(e => e.Id == dead.Id), Is.False);
            Assert.That(reloaded.Eggs.Exists(e => e.Id == live.Id), Is.True);
        }

        private PetState HatchIntoTray()
        {
            var session = view.Session;
            session.Incubation.AddDebugEggs(session.Colony, session.GameNowUtc, 1);
            var egg = session.Colony.Eggs[session.Colony.Eggs.Count - 1];
            var pet = new Hatchery(new CareTuning()).Hatch(session.Colony, egg, session.GameNowUtc, 28d);
            IncubatorViewOf().Invalidate();
            RenderIncubator();
            return pet;
        }

        [Test]
        public void HatchTray_ShowsAHatchling_AndMoveToCageSeatsItAndEmptiesTheTray()
        {
            OpenIncubatorTab();
            var colony = view.Session.Colony;
            colony.AddCage(CageSize.Standard);
            var pet = HatchIntoTray();

            Assert.That(incubatorPanel.Q<VisualElement>($"tray-row-{pet.Id}"), Is.Not.Null);
            ClickIncubatorButton($"tray-move-{pet.Id}");

            var cage = colony.CageOf(pet);
            Assert.That(cage, Is.Not.Null);
            Assert.That(colony.TrayAnimals(), Is.Empty);
            Assert.That(incubatorPanel.Q<VisualElement>($"tray-row-{pet.Id}"), Is.Null);
            Assert.That(feedbackLabel.text, Is.EqualTo($"{pet.Name}をケージ{cage.Id}へ移しました"));
        }

        [Test]
        public void HatchTray_ThumbnailIsScaledToFit_SoTheHatchlingIsNotSquashed()
        {
            OpenIncubatorTab();
            var pet = HatchIntoTray();

            var row = incubatorPanel.Q<VisualElement>($"tray-row-{pet.Id}");
            var thumb = row.Children().First();
            Assert.That(thumb.ClassListContains("cage-row-thumb"), Is.True, "the shared scale-to-fit thumbnail style");
        }

        [Test]
        public void RebuildPetActor_GivesTheNewActorTheCurrentGrowthStageAtOnce()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var pet = (PetState)typeof(TerrariumView).GetField("state", flags).GetValue(view);
            pet.Stage = GrowthStage.Adult;

            typeof(TerrariumView).GetMethod("RebuildPetActor", flags).Invoke(view, null);

            var actor = (PetActor)typeof(TerrariumView).GetField("petActor", flags).GetValue(view);
            Assert.That(actor.BodyWidth, Is.EqualTo(new PetBehaviourTuning().AdultBodyWidth));
        }

        [Test]
        public void HatchTray_Sell_ShowsTheResultOnTheIncubatorTab()
        {
            OpenIncubatorTab();
            var pet = HatchIntoTray();
            var pay = ShopServiceOf().WholesalePriceOf(pet);

            view.OnTraySellRequested(pet);
            ClickConfirmYes();

            Assert.That(view.Session.Colony.TrayAnimals(), Is.Empty);
            Assert.That(feedbackLabel.text, Is.EqualTo(ShopText.WholesaleMessage(pet.Name, pay)));
        }

        [Test]
        public void SwitchingTabs_ClosesTheFamilyTree()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, LedgerNow, new TimeService(() => LedgerNow));
            var second = view.Session.Colony.AddAnimal(new PetState { Name = "ふたり目" }, view.Session.Colony.AddCage(CageSize.Standard));
            view.OnLedgerAnimalTapped(second.Id);
            Assert.That(familyTreeModal.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            // Awake() is skipped by this harness, so hook the real handler up the way Awake does.
            var onTabChanged = (Action<ShellTab>)Delegate.CreateDelegate(typeof(Action<ShellTab>), view,
                typeof(TerrariumView).GetMethod("OnTabChanged", BindingFlags.NonPublic | BindingFlags.Instance));
            NavigatorOf().TabChanged += onTabChanged;
            NavigatorOf().ShowTab(ShellTab.Shop);

            Assert.That(familyTreeModal.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void RepeatedNavigationAcrossAllTabs_ClosesFamilyTreeWithoutUnexpectedLogs()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, LedgerNow, new TimeService(() => LedgerNow));
            var second = view.Session.Colony.AddAnimal(new PetState { Name = "ふたり目" }, view.Session.Colony.AddCage(CageSize.Standard));
            typeof(TerrariumView).GetField("cageListView", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(view, new CageListView(new ScrollView()));
            var onTabChanged = (Action<ShellTab>)Delegate.CreateDelegate(typeof(Action<ShellTab>), view,
                typeof(TerrariumView).GetMethod("OnTabChanged", BindingFlags.NonPublic | BindingFlags.Instance));
            NavigatorOf().TabChanged += onTabChanged;

            for (var round = 0; round < 3; round++)
            {
                foreach (ShellTab tab in Enum.GetValues(typeof(ShellTab)))
                {
                    view.OnLedgerAnimalTapped(second.Id);
                    Assert.That(familyTreeModal.style.display.value, Is.EqualTo(DisplayStyle.Flex));

                    NavigatorOf().ShowTab(tab);

                    Assert.That(familyTreeModal.style.display.value, Is.EqualTo(DisplayStyle.None));
                    Assert.That(NavigatorOf().Tab, Is.EqualTo(tab));
                }
            }

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void HatchTray_WithNoEmptyCage_DisablesMoveAndSaysSo()
        {
            OpenIncubatorTab();
            var colony = view.Session.Colony;
            foreach (var cage in colony.Cages.FindAll(c => c.IsEmpty))
            {
                colony.AddAnimal(new PetState { Name = "満室" }, cage);
            }

            var pet = HatchIntoTray();

            Assert.That(IncubatorButton($"tray-move-{pet.Id}").enabledSelf, Is.False);
            Assert.That(incubatorPanel.Query<Label>().ToList().Select(l => l.text), Has.Member(IncubatorText.NoEmptyCage));
        }

        [Test]
        public void HatchTray_SellAfterConfirm_RemovesItAndRecordsLineage()
        {
            OpenIncubatorTab();
            var colony = view.Session.Colony;
            var pet = HatchIntoTray();
            var lineageBefore = colony.Lineage.Count;

            ClickIncubatorButton($"tray-sell-{pet.Id}");
            ClickConfirmYes();

            Assert.That(colony.AnimalById(pet.Id), Is.Null);
            Assert.That(colony.TrayAnimals(), Is.Empty);
            Assert.That(colony.Lineage.Count, Is.EqualTo(lineageBefore + 1));
        }

        [Test]
        public void HatchTray_BulkFeedingLeavesTrayHatchlingsAlone()
        {
            OpenIncubatorTab();
            var pet = HatchIntoTray();
            var hungerBefore = pet.Hunger;

            view.OnFeedAllClicked();
            view.OnWaterAllClicked();
            view.OnCleanAllClicked();

            Assert.That(pet.Hunger, Is.EqualTo(hungerBefore));
        }

        [Test]
        public void OnMoveEggsButtonClicked_MovesTheCageEggsIntoTheIncubatorAndHidesTheButton()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            var cage = view.CurrentCage;
            colony.Eggs.Add(new Egg { Id = 1, CageId = cage.Id, Place = EggPlace.NestBox });
            colony.Eggs.Add(new Egg { Id = 2, CageId = cage.Id, Place = EggPlace.Loose });
            view.Render(view.State, view.Tuning);
            Assert.That(moveEggsButton.resolvedStyle.display == DisplayStyle.None || moveEggsButton.style.display == DisplayStyle.None, Is.False);

            view.OnMoveEggsButtonClicked();

            Assert.That(IncubatorService.EggsInIncubators(colony), Is.EqualTo(2));
            Assert.That(feedbackLabel.text, Is.EqualTo("孵卵器へ2個移しました"));
            Assert.That(moveEggsButton.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void OnMoveEggsButtonClicked_WithFullIncubatorsSaysThereIsNoRoom()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            var cage = view.CurrentCage;
            for (var i = 0; i < 6; i++)
            {
                colony.Eggs.Add(new Egg { Id = 10 + i, Place = EggPlace.Incubator, IncubatorId = colony.Incubators[0].Id, CageId = -1 });
            }

            colony.Eggs.Add(new Egg { Id = 1, CageId = cage.Id, Place = EggPlace.NestBox });

            view.OnMoveEggsButtonClicked();

            Assert.That(feedbackLabel.text, Is.EqualTo("孵卵器に空きがありません"));
            Assert.That(colony.EggsIn(cage).Count, Is.EqualTo(1));
        }

        [Test]
        public void HomeView_ShowsEggAndTrayCountsOnTheIncubatorButtonAndToleratesTrayAnimals()
        {
            var path = CreateTempSavePath();
            view.LoadColony(path, ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            colony.Eggs.Add(new Egg { Id = 1, Place = EggPlace.Incubator, IncubatorId = colony.Incubators[0].Id, CageId = -1 });
            colony.Eggs.Add(new Egg { Id = 2, Place = EggPlace.Incubator, IncubatorId = colony.Incubators[0].Id, CageId = -1 });
            var button = new Button();
            var home = new HomeView(new VisualElement(), button);

            home.Render(colony, ShopNow, view.Tuning);
            Assert.That(button.text, Is.EqualTo("孵卵器（卵2・トレイ0）"));

            colony.AddAnimal(new PetState { Name = "トレイ" }, null);
            Assert.That(colony.TrayAnimals().Count, Is.EqualTo(1));
            home.Render(colony, ShopNow, view.Tuning);
            Assert.That(button.text, Is.EqualTo("孵卵器（卵2・トレイ1）"));
        }

        [Test]
        public void OnDebugAddIncubatorEggsClicked_AddsEggsAndReportsTheCount()
        {
            view.LoadColony(CreateTempSavePath(), ShopNow, new TimeService(() => ShopNow));

            view.OnDebugAddIncubatorEggsClicked();

            var added = view.Session.Colony.Eggs.Count;
            Assert.That(added, Is.GreaterThan(0));
            Assert.That(feedbackLabel.text, Is.EqualTo($"孵卵器に卵を{added}個追加しました"));

            for (var i = 0; i < 10; i++)
            {
                view.OnDebugAddIncubatorEggsClicked();
            }

            Assert.That(feedbackLabel.text, Is.EqualTo("孵卵器に空きがありません"));
        }

        [Test]
        public void OnDebugSimulate12HoursClicked_HatchesDebugEggsAndNotifiesTheTray()
        {
            view.LoadColony(CreateTempSavePath(), ShopNow, new TimeService(() => ShopNow));
            view.OnDebugAddIncubatorEggsClicked();

            var notified = false;
            // Debug eggs start well developed; one click is ~15 game days, so a handful of clicks is plenty.
            for (var i = 0; i < 5 && !notified; i++)
            {
                view.OnDebugSimulate12HoursClicked();
                notified = feedbackLabel.text.Contains("が孵化しました（孵化トレイ）");
            }

            Assert.That(notified, Is.True, "no hatch notice within 5 clicks; feedback was: " + feedbackLabel.text);
        }

        [Test]
        public void OnDebugSimulate12HoursClicked_WarnsWhenIncubatorHumidityFallsBelowTheDryLine()
        {
            view.LoadColony(CreateTempSavePath(), ShopNow, new TimeService(() => ShopNow));
            var colony = view.Session.Colony;
            var incubator = colony.Incubators[0];
            colony.Eggs.Add(new Egg
            {
                Id = colony.NextEggId++, CageId = -1, LaidAtUtc = ShopNow, Place = EggPlace.Incubator,
                IncubatorId = incubator.Id, Fertile = true, ChildGenotype = Genotype.Normal(),
                ChildKnown = new KnownGenetics(), AppliedUntilUtc = ShopNow, Failure = EggFailure.Dried,
            });
            incubator.HumidityPercent = 61d;

            view.OnDebugSimulate12HoursClicked();

            StringAssert.Contains($"孵卵器{incubator.Id}の湿度が60%を下回りました", feedbackLabel.text, feedbackLabel.text);
        }

        [Test]
        public void FullLoop_LayMoveToIncubatorHatchAndMoveToCage_WithoutErrors()
        {
            view.LoadColony(CreateTempSavePath(), ShopNow, new TimeService(() => ShopNow));
            view.WireIncubatorEvents();
            var colony = view.Session.Colony;
            colony.RemoveAnimal(colony.Animals[0]); // the starter baby would go Weak and crowd out messages
            var maleCage = colony.AddCage(CageSize.Standard);
            var male = colony.AddAnimal(new PetState
            {
                Name = "タロウ", Sex = Sex.Male, SexRevealed = true, Stage = GrowthStage.Adult,
                WeightGrams = 55d, HatchedAtUtc = ShopNow.AddDays(-30), PersonalityKnown = true,
            }, maleCage);
            var femaleCage = colony.AddCage(CageSize.Standard);
            femaleCage.HasNestBox = true; // without one the eggs are Loose and dry out within one click
            var female = colony.AddAnimal(new PetState
            {
                Name = "ハナ", Sex = Sex.Female, SexRevealed = true, Stage = GrowthStage.Adult,
                WeightGrams = 55d, HatchedAtUtc = ShopNow.AddDays(-30), PersonalityKnown = true,
            }, femaleCage);
            Assert.That(view.Session.Breeding.StartPairing(colony, male.Id, female.Id, ShopNow, view.Session.Calendar), Is.EqualTo(PairingProblem.None));
            colony.PairingOf(female).SuccessChance = 1d;

            void KeepAnimalsCaredFor()
            {
                foreach (var pet in new[] { male, female })
                {
                    pet.Hunger = 100d;
                    pet.Hydration = 100d;
                    pet.Cleanliness = 100d;
                    pet.Health = 100d;
                }
            }

            // Same pacing as the lay test above: one click is ~15 game days, so Gravid takes a few
            // clicks and the first clutch (21-28 game days) one or two more; the caps are generous.
            for (var i = 0; i < 7 && female.Gravid == null; i++)
            {
                KeepAnimalsCaredFor();
                view.OnDebugSimulate12HoursClicked();
            }

            Assert.That(female.Gravid, Is.Not.Null, "precondition: pairing must reach Gravid before the clutch can be laid");

            for (var i = 0; i < 10 && colony.Eggs.Count == 0; i++)
            {
                KeepAnimalsCaredFor();
                view.OnDebugSimulate12HoursClicked();
            }

            Assert.That(colony.EggsIn(femaleCage).Count, Is.GreaterThan(0), "precondition: an egg must be laid in the nest box");

            // Move the clutch to the incubator through the cage detail's handler.
            Assert.That(view.SelectCage(femaleCage.Id), Is.True);
            view.Render(view.State, view.Tuning);
            view.OnMoveEggsButtonClicked(); // the harness skips Awake, so the click handler is invoked directly
            Assert.That(colony.EggsIn(femaleCage).Count, Is.EqualTo(0));
            Assert.That(colony.Eggs.Count, Is.GreaterThan(0));
            Assert.That(colony.Eggs.All(e => e.Place == EggPlace.Incubator), Is.True);

            // Shortcut to just before hatching: keep one egg (so one cage is enough for the tray
            // step), make it fertile and nearly developed. The hatch itself is the real simulation.
            var egg = colony.Eggs[0];
            colony.Eggs.RemoveAll(e => e != egg);
            egg.Fertile = true;
            egg.ChildGenotype = female.Genotype;
            egg.DevelopmentPercent = 99.5d;
            colony.Incubators[0].HumidityPercent = 80d; // it drifts while the incubator sat empty during the earlier clicks; the brief starts from 80%

            // End the pairing so a second clutch laid during the click cannot crowd the hatch message
            // out of the one-line summary (it shows the first message plus "ほか n件").
            view.Session.Breeding.CancelPairing(colony, female);
            female.Gravid = null;
            Assert.That(colony.PairingOf(female), Is.Null);
            KeepAnimalsCaredFor();
            view.OnDebugSimulate12HoursClicked();

            StringAssert.Contains("が孵化しました（孵化トレイ）", feedbackLabel.text);
            Assert.That(colony.Eggs.Contains(egg), Is.False);
            var tray = colony.TrayAnimals();
            Assert.That(tray.Count, Is.EqualTo(1));
            var hatchling = tray[0];

            // Seat the hatchling from the incubator tab's tray.
            colony.AddCage(CageSize.Standard);
            NavigatorOf().ShowTab(ShellTab.Incubator);
            IncubatorViewOf().Invalidate();
            RenderIncubator();
            ClickIncubatorButton($"tray-move-{hatchling.Id}");
            Assert.That(colony.CageOf(hatchling), Is.Not.Null);
            Assert.That(colony.TrayAnimals(), Is.Empty);

            var reloaded = new ColonySaveService(new CareTuning(), new EconomyTuning())
                .LoadOrCreate(view.Session.SavePath, ShopNow, new System.Random(1));
            var reloadedPet = reloaded.Animals.First(a => a.Id == hatchling.Id);
            Assert.That(reloadedPet.MotherId, Is.EqualTo(female.Id));
            Assert.That(reloadedPet.FatherId, Is.EqualTo(male.Id));
            Assert.That(reloadedPet.CareGraceUntilUtc.HasValue, Is.True);
            Assert.That(reloaded.CageOf(reloadedPet), Is.Not.Null);

            LogAssert.NoUnexpectedReceived();
        }
    }
}
