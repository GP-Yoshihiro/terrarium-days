using System;
using System.Collections.Generic;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    /// <summary>Rooms for the event tests. Day 0 (2026-04-01) is Epoch, so event 0 (big, spring) starts 14 game days later.</summary>
    internal static class EventTestRoom
    {
        public static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

        /// <summary>The starter baby plus (animals − 1) shop animals, each in its own cage, three racks and plenty of money.</summary>
        public static Colony Create(int animals, out GameCalendar calendar)
        {
            var care = new CareTuning();
            var colony = Colony.CreateNew(Epoch, new EconomyTuning(), care, new Random(1));
            colony.RackCount = 3;
            colony.Wallet.Money = 1000000;
            colony.Animals[0].Name = "テスト0";
            for (var i = 1; i < animals; i++)
            {
                var pet = ShopStockGenerator.Animal(new Random(100 + i), Epoch, care);
                pet.Name = "テスト" + i;
                colony.AddAnimal(pet, colony.AddCage(CageSize.Standard));
            }

            calendar = new GameCalendar(colony.CalendarEpochUtc);
            return colony;
        }

        public static List<ListingRequest> AtBaseValue(EventService service, Colony colony, int number)
        {
            var requests = new List<ListingRequest>();
            foreach (var pet in colony.Animals)
            {
                requests.Add(new ListingRequest { AnimalId = pet.Id, Price = service.BaseValueOf(colony, pet, number) });
            }

            return requests;
        }

        public static List<ListingRequest> AtPrice(Colony colony, long price)
        {
            var requests = new List<ListingRequest>();
            foreach (var pet in colony.Animals)
            {
                requests.Add(new ListingRequest { AnimalId = pet.Id, Price = price });
            }

            return requests;
        }

        public static List<ListingRequest> One(int animalId, long price, int tolerancePercent = 0) =>
            new List<ListingRequest> { new ListingRequest { AnimalId = animalId, Price = price, TolerancePercent = tolerancePercent } };
    }
}
