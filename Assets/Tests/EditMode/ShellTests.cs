using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.UI;
using UnityEngine.UIElements;

namespace TerrariumDays.Tests
{
    public sealed class ShellTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        private static VisualElement BuildShell()
        {
            var root = new VisualElement();
            foreach (var name in new[] { "home-screen", "main-screen", "cage-list-panel", "cage-detail-panel", "placeholder-panel" })
            {
                root.Add(new VisualElement { name = name });
            }

            root.Add(new Label { name = "placeholder-label" });
            foreach (var name in new[] { "tab-cages", "tab-incubator", "tab-shop", "tab-events", "tab-ledger" })
            {
                root.Add(new Button { name = name });
            }

            return root;
        }

        private static DisplayStyle Display(VisualElement root, string name) => root.Q(name).style.display.value;

        [Test]
        public void TheAppStartsOnTheHomeScreen()
        {
            var root = BuildShell();
            var nav = new ShellNavigator(root);

            Assert.That(nav.Screen, Is.EqualTo(ShellScreen.Home));
            Assert.That(Display(root, "home-screen"), Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Display(root, "main-screen"), Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ShowCageDetail_EntersTheMainScreenOnTheCagesTab()
        {
            var root = BuildShell();
            var nav = new ShellNavigator(root);

            nav.ShowCageDetail();

            Assert.That((nav.Screen, nav.Tab, nav.ShowingCageDetail), Is.EqualTo((ShellScreen.Main, ShellTab.Cages, true)));
            Assert.That(Display(root, "cage-detail-panel"), Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Display(root, "cage-list-panel"), Is.EqualTo(DisplayStyle.None));
            Assert.That(Display(root, "home-screen"), Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TabsNotBuiltYet_ShowWhenTheyArrive()
        {
            var root = BuildShell();
            var nav = new ShellNavigator(root);

            nav.ShowTab(ShellTab.Shop);

            Assert.That(Display(root, "placeholder-panel"), Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Label>("placeholder-label").text, Is.EqualTo("ショップは段階3で追加されます"));
            Assert.That(root.Q<Button>("tab-shop").ClassListContains("tab-selected"), Is.True);
        }

        [Test]
        public void CageTitle_HidesTheSexOfABaby()
        {
            var cage = new Cage { Id = 2 };

            Assert.That(CageStatusText.TitleFor(cage, new PetState { Name = "ハナ" }), Is.EqualTo("ケージ2 ハナ（ベビー・性別不明）"));
            Assert.That(CageStatusText.TitleFor(cage, new PetState { Name = "ハナ", Stage = GrowthStage.Juvenile, Sex = Sex.Male, SexRevealed = true }), Is.EqualTo("ケージ2 ハナ（ヤング・♂）"));
            Assert.That(CageStatusText.TitleFor(cage, null), Is.EqualTo("ケージ2（空き）"));
        }

        [Test]
        public void Alerts_ListWhatNeedsAttention()
        {
            var care = new CareTuning();
            var pet = new PetState { Hunger = 30d, Hydration = 80d, Cleanliness = 30d, NextShedAtUtc = Now.AddMinutes(30) };

            Assert.That(CageStatusText.AlertsFor(pet, Now, care), Is.EqualTo("空腹・汚れ・脱皮前"));
            Assert.That(CageStatusText.AlertsFor(new PetState { NextShedAtUtc = Now.AddDays(3) }, Now, care), Is.EqualTo(string.Empty));
        }

        [Test]
        public void ProfileFor_ShowsMorphAndPersonality()
        {
            var pet = new PetState
            {
                Genotype = Genotype.Normal().Set(GeneId.Eclipse, 2),
                Known = new KnownGenetics().SetHet(GeneId.TremperAlbino, 1d),
                Personality = Personality.Curious,
            };

            Assert.That(CageStatusText.ProfileFor(pet), Is.EqualTo("エクリプス ヘテロトレンパーアルビノ・好奇心旺盛"));
        }

        [Test]
        public void ProfileFor_HidesAnUnknownPersonality()
        {
            var pet = new PetState { Known = KnownGenetics.Unknown(), PersonalityKnown = false };

            Assert.That(CageStatusText.ProfileFor(pet), Is.EqualTo("ノーマル（ヘテロ不明）・性格不明"));
        }

        [Test]
        public void SexRevealMessage()
        {
            Assert.That(CageStatusText.SexRevealMessage(new PetState { Name = "レオパ2", Sex = Sex.Male }), Is.EqualTo("レオパ2は♂オスでした"));
            Assert.That(CageStatusText.SexRevealMessage(new PetState { Name = "レオパ3", Sex = Sex.Female }), Is.EqualTo("レオパ3は♀メスでした"));
        }

        [Test]
        public void HomeView_ShowsFourSlotsPerRackAndReportsTheTappedCage()
        {
            var colony = Colony.CreateNew(Now, new EconomyTuning(), new CareTuning(), new System.Random(1));
            var rackList = new VisualElement();
            var home = new HomeView(rackList);
            var selected = -1;
            home.CageSelected += id => selected = id;

            home.Render(colony, Now, new CareTuning());

            var slots = rackList.Query<Button>(className: "rack-slot").ToList();
            Assert.That(slots, Has.Count.EqualTo(4));
            Assert.That(slots[0].enabledSelf, Is.True);
            Assert.That(slots[1].enabledSelf, Is.False, "no cage bought for this slot yet");

            // UI Toolkit events queue by default and only get pumped on a real (editor/player)
            // update tick, which an EditMode test never gets — SendEvent()-ing a
            // NavigationSubmitEvent/ClickEvent at the element is a no-op here even when it is
            // parented under a live panel. Invoke the Button's own Clickable action directly
            // instead (see task-9-brief decisions) while still keeping the assertion that
            // tapping slot 0 reports the cage's id.
            var clicked = FindClickAction(slots[0].clickable);
            Assert.That(clicked, Is.Not.Null, "Button 0 has no click action wired up");
            clicked();

            Assert.That(selected, Is.EqualTo(colony.Cages[0].Id));
        }

        [Test]
        public void HomeView_ABoughtEmptyCageSlotIsDisabled()
        {
            var colony = Colony.CreateNew(Now, new EconomyTuning(), new CareTuning(), new System.Random(1));
            colony.AddCage(CageSize.Standard); // bought but no animal placed in it
            var rackList = new VisualElement();
            var home = new HomeView(rackList);

            home.Render(colony, Now, new CareTuning());

            var slots = rackList.Query<Button>(className: "rack-slot").ToList();
            Assert.That(slots[1].enabledSelf, Is.False, "a bought cage with no animal must not be selectable");
            Assert.That(slots[1].Q<Label>(className: "rack-name").text, Is.EqualTo("空きケージ"));
        }

        [Test]
        public void CageListSignature_IsStableWhenNothingChanges()
        {
            var colony = Colony.CreateNew(Now, new EconomyTuning(), new CareTuning(), new System.Random(1));
            var tuning = new CareTuning();

            Assert.That(HomeView.CageListSignature(colony, Now, tuning), Is.EqualTo(HomeView.CageListSignature(colony, Now, tuning)));
        }

        [Test]
        public void CageListSignature_ChangesWhenAnAlertAppears()
        {
            var colony = Colony.CreateNew(Now, new EconomyTuning(), new CareTuning(), new System.Random(1));
            var tuning = new CareTuning();
            var before = HomeView.CageListSignature(colony, Now, tuning);

            colony.Animals[0].Hunger = 10d;

            Assert.That(HomeView.CageListSignature(colony, Now, tuning), Is.Not.EqualTo(before));
        }

        [Test]
        public void HomeView_Render_SkipsRebuildingUnchangedRacksButRebuildsWhenSomethingShownChanges()
        {
            var colony = Colony.CreateNew(Now, new EconomyTuning(), new CareTuning(), new System.Random(1));
            var tuning = new CareTuning();
            var rackList = new VisualElement();
            var home = new HomeView(rackList);

            home.Render(colony, Now, tuning);
            var firstRow = rackList.Query<Button>(className: "rack-slot").ToList()[0];

            // A second render of an unchanged colony must not tear down and recreate the
            // rows — doing so once a second could swallow a tap the player is mid-gesture on.
            home.Render(colony, Now, tuning);
            var sameRow = rackList.Query<Button>(className: "rack-slot").ToList()[0];
            Assert.That(sameRow, Is.SameAs(firstRow), "an unchanged colony must not rebuild the rack list");

            // Once something the row actually displays changes (its alert text), the row
            // must be rebuilt.
            colony.Animals[0].Hunger = 10d;
            home.Render(colony, Now, tuning);
            var rebuiltRow = rackList.Query<Button>(className: "rack-slot").ToList()[0];
            Assert.That(rebuiltRow, Is.Not.SameAs(firstRow), "a changed alert must rebuild the row");
        }

        [Test]
        public void ProfileTokens_SplitTheNameIntoWordsThatMustNotBreak()
        {
            var pet = new PetState
            {
                Genotype = Genotype.Normal().Set(GeneId.Eclipse, 2),
                Known = new KnownGenetics().SetHet(GeneId.TremperAlbino, 1d).SetHet(GeneId.Blizzard, 0.66d),
                Personality = Personality.Curious,
            };

            Assert.That(CageStatusText.ProfileTokens(pet), Is.EqualTo(new[]
            {
                "エクリプス", "ヘテロトレンパーアルビノ", "66%ポッシブルヘテロブリザード", "好奇心旺盛",
            }));
        }

        [Test]
        public void ProfileTokens_KeepHetsUnknownWithTheNameAndHideAnUnknownPersonality()
        {
            var pet = new PetState { Known = KnownGenetics.Unknown(), PersonalityKnown = false };

            Assert.That(CageStatusText.ProfileTokens(pet), Is.EqualTo(new[] { "ノーマル（ヘテロ不明）", "性格不明" }));
        }

        [Test]
        public void SexLabel_HidesTheSexUntilItIsKnown()
        {
            Assert.That(CageStatusText.SexLabel(new PetState { Sex = Sex.Female, SexRevealed = false }), Is.EqualTo("性別不明"));
            Assert.That(CageStatusText.SexLabel(new PetState { Sex = Sex.Female, SexRevealed = true }), Is.EqualTo("♀メス"));
            Assert.That(CageStatusText.SexLabel(new PetState { Sex = Sex.Male, SexRevealed = true }), Is.EqualTo("♂オス"));
        }

        [Test]
        public void ThumbnailCrop_IsTheBodyPlusPaddingWithYFromTheBottom()
        {
            var pet = new TerrariumArtLayout().Pet;

            var rect = ThumbnailCrop.BodyRect(pet, 6f);
            Assert.That((rect.X, rect.Y, rect.Width, rect.Height), Is.EqualTo((16f, 82f, 194f, 82f)));

            var clamped = ThumbnailCrop.BodyRect(pet, 30f);
            Assert.That((clamped.X, clamped.Y, clamped.Width, clamped.Height), Is.EqualTo((0f, 58f, 234f, 130f)));
        }

        [Test]
        public void TheTabBarCanBeUsedFromHomeAndShowsNoSelectionThere()
        {
            var root = BuildShell();
            var nav = new ShellNavigator(root);

            nav.ShowTab(ShellTab.Ledger);
            Assert.That(Display(root, "main-screen"), Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Button>("tab-ledger").ClassListContains("tab-selected"), Is.True);

            nav.ShowHome();
            foreach (var name in new[] { "tab-cages", "tab-incubator", "tab-shop", "tab-events", "tab-ledger" })
            {
                Assert.That(root.Q<Button>(name).ClassListContains("tab-selected"), Is.False, name);
            }
        }

        private static Action FindClickAction(Clickable clickable)
        {
            foreach (var field in typeof(Clickable).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                if (field.GetValue(clickable) is Action action)
                {
                    return action;
                }
            }

            return null;
        }
    }
}
