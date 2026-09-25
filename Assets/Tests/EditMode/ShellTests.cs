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
            Assert.That(CageStatusText.TitleFor(cage, new PetState { Name = "ハナ", Stage = GrowthStage.Juvenile, Sex = Sex.Male }), Is.EqualTo("ケージ2 ハナ（ヤング・♂）"));
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
        public void HomeView_ShowsFourSlotsPerRackAndReportsTheTappedCage()
        {
            var colony = Colony.CreateNew(Now, new EconomyTuning(), new CareTuning(), new System.Random(1));
            var rackList = new VisualElement();
            var home = new HomeView(rackList, null);
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
