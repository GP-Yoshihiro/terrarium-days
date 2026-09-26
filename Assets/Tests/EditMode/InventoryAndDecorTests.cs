using System;
using System.Linq;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class InventoryAndDecorTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        private static Colony NewColony() => Colony.CreateNew(Now, new EconomyTuning(), new CareTuning(), new Random(1));

        [Test]
        public void Inventory_AddsAndTakes()
        {
            var inventory = new Inventory();
            inventory.Add("plant_01", 2);

            Assert.That(inventory.Count("plant_01"), Is.EqualTo(2));
            Assert.That(inventory.TryTake("plant_01"), Is.True);
            Assert.That(inventory.TryTake("plant_01"), Is.True);
            Assert.That(inventory.TryTake("plant_01"), Is.False);
            Assert.That(inventory.Count("plant_01"), Is.EqualTo(0));
            Assert.That(inventory.Items, Is.Empty);
        }

        [Test]
        public void Inventory_RejectsNegativeAmounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Inventory().Add("rock_01", -1));
        }

        [Test]
        public void Inventory_ListsItemsInIdOrder()
        {
            var inventory = new Inventory();
            inventory.Add("water_dish_01", 1);
            inventory.Add("plant_01", 3);

            Assert.That(inventory.Items.Select(i => i.Key), Is.EqualTo(new[] { "plant_01", "water_dish_01" }));
        }

        [TestCase(CageSize.Small, 1)]
        [TestCase(CageSize.Standard, 2)]
        [TestCase(CageSize.Large, 3)]
        public void SlotsFor_FollowsTheCageSize(CageSize size, int slots)
        {
            Assert.That(DecorSlots.SlotsFor(size), Is.EqualTo(slots));
        }

        [Test]
        public void ANewColony_StartsWithARockInCageOneAndOneSimpleIncubator()
        {
            var colony = NewColony();

            Assert.That(colony.Cages[0].DecorIds, Is.EqualTo(new[] { "rock_01" }));
            Assert.That(colony.Inventory.Count("rock_01"), Is.EqualTo(0));
            Assert.That(colony.Incubators, Is.EqualTo(new[] { IncubatorModel.Simple }));
            Assert.That(colony.IncubatorCount, Is.EqualTo(1));
        }

        [Test]
        public void Place_TakesFromTheInventoryAndFillsASlot()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Small);
            colony.Inventory.Add("plant_01", 1);

            Assert.That(DecorSlots.Place(colony, cage, "plant_01"), Is.EqualTo(DecorResult.Ok));

            Assert.That(cage.DecorIds, Is.EqualTo(new[] { "plant_01" }));
            Assert.That(colony.Inventory.Count("plant_01"), Is.EqualTo(0));
        }

        [Test]
        public void Place_RefusesWhenAlreadyPlacedFullOrNotOwned()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Small);
            colony.Inventory.Add("plant_01", 2);
            colony.Inventory.Add("rock_01", 1);

            Assert.That(DecorSlots.Place(colony, cage, "plant_01"), Is.EqualTo(DecorResult.Ok));
            Assert.That(DecorSlots.Place(colony, cage, "plant_01"), Is.EqualTo(DecorResult.AlreadyPlaced));
            Assert.That(DecorSlots.Place(colony, cage, "rock_01"), Is.EqualTo(DecorResult.SlotsFull));
            Assert.That(colony.Inventory.Count("rock_01"), Is.EqualTo(1));

            var big = colony.AddCage(CageSize.Large);
            Assert.That(DecorSlots.Place(colony, big, "water_dish_01"), Is.EqualTo(DecorResult.NotOwned));
        }

        [Test]
        public void Remove_ReturnsTheItemToTheInventory()
        {
            var colony = NewColony();
            var cage = colony.Cages[0];

            Assert.That(DecorSlots.Remove(colony, cage, "rock_01"), Is.EqualTo(DecorResult.Ok));
            Assert.That(cage.DecorIds, Is.Empty);
            Assert.That(colony.Inventory.Count("rock_01"), Is.EqualTo(1));
            Assert.That(DecorSlots.Remove(colony, cage, "rock_01"), Is.EqualTo(DecorResult.NotPlaced));
        }

        [Test]
        public void StatusText_TellsWhatATapWillDo()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);

            Assert.That(DecorSlots.StatusText(colony, cage, "plant_01"), Is.EqualTo("未所持（ショップで購入）"));
            colony.Inventory.Add("plant_01", 1);
            Assert.That(DecorSlots.StatusText(colony, cage, "plant_01"), Is.EqualTo("所持1（タップで置く）"));
            DecorSlots.Place(colony, cage, "plant_01");
            Assert.That(DecorSlots.StatusText(colony, cage, "plant_01"), Is.EqualTo("置いています（タップで外す）"));
            Assert.That(DecorSlots.SlotSummary(cage), Is.EqualTo("装飾の枠 1/2"));

            colony.Inventory.Add("rock_01", 1);
            DecorSlots.Place(colony, cage, "rock_01");
            colony.Inventory.Add("water_dish_01", 1);
            Assert.That(DecorSlots.StatusText(colony, cage, "water_dish_01"), Is.EqualTo("所持1・枠がいっぱい"));
        }

        [Test]
        public void RemoveAnimal_FreesItsCageAndKeepsTheDecor()
        {
            var colony = NewColony();
            var pet = colony.Animals[0];
            var cage = colony.CageOf(pet);

            Assert.That(colony.RemoveAnimal(pet), Is.True);

            Assert.That(cage.IsEmpty, Is.True);
            Assert.That(colony.Animals, Is.Empty);
            Assert.That(cage.DecorIds, Is.EqualTo(new[] { "rock_01" }));
            Assert.That(colony.RemoveAnimal(pet), Is.False);
        }

        [Test]
        public void Incubators_AreCountedFromTheList()
        {
            var colony = NewColony();
            colony.Incubators.Add(IncubatorModel.Standard);

            Assert.That(colony.IncubatorCount, Is.EqualTo(2));
        }

        [Test]
        public void ArtLayout_PutsFloorDecorOnItsSlotSpotAndKeepsHangingDecorWhereItHangs()
        {
            var layout = new TerrariumArtLayout();

            var rock = layout.PlacementFor("rock_01", 1);
            Assert.That((rock.X, rock.Depth, rock.IsHanging), Is.EqualTo((0.5f, 0.5f, false)));
            Assert.That(rock.BodyWidthFraction, Is.EqualTo(layout.Decor["rock_01"].BodyWidthFraction));

            var lamp = layout.PlacementFor("heat_lamp_01", 1);
            Assert.That((lamp.X, lamp.IsHanging), Is.EqualTo((0.72f, true)));

            Assert.That(layout.PlacementFor("rock_01", 5).X, Is.EqualTo(layout.Decor["rock_01"].X), "no spot for that slot: keep its own place");
        }

        [Test]
        public void DecorItems_AllHaveArtAndLabels()
        {
            var layout = new TerrariumArtLayout();
            foreach (var decor in DecorItems.All)
            {
                Assert.That(layout.Decor.ContainsKey(decor.Id), Is.True, decor.Id);
                Assert.That(DecorItems.IsDecor(decor.Id), Is.True);
            }

            Assert.That(DecorItems.IsDecor("nest_box"), Is.False);
            Assert.That(DecorItems.LabelOf("driftwood_01"), Is.EqualTo("流木"));
        }
    }
}
