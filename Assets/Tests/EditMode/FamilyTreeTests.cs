using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class FamilyTreeTests
    {
        private static PetState Add(Colony colony, string name, Sex sex, int motherId = 0, int fatherId = 0, bool sexKnown = true)
        {
            var pet = colony.AddAnimal(new PetState { Sex = sex, SexRevealed = sexKnown }, null);
            pet.Name = name;
            pet.MotherId = motherId;
            pet.FatherId = fatherId;
            return pet;
        }

        [Test]
        public void ShopAnimal_HasUnknownParentsAndGrandparents()
        {
            var colony = new Colony();
            var pet = Add(colony, "ハナ", Sex.Female);

            var tree = FamilyTree.For(colony, pet);

            Assert.AreEqual("ハナ", tree.Self.Name);
            Assert.IsTrue(tree.Mother.Unknown);
            Assert.IsTrue(tree.Father.Unknown);
            Assert.IsTrue(tree.MaternalGrandmother.Unknown);
            Assert.IsTrue(tree.PaternalGrandfather.Unknown);
        }

        [Test]
        public void ThreeGenerations_AreFilledFromLivingAnimals()
        {
            var colony = new Colony();
            var grandma = Add(colony, "ウメ", Sex.Female);
            var grandpa = Add(colony, "マツ", Sex.Male);
            var mother = Add(colony, "ハナ", Sex.Female, grandma.Id, grandpa.Id);
            var father = Add(colony, "タロウ", Sex.Male);
            var child = Add(colony, "レオパ9", Sex.Female, mother.Id, father.Id, sexKnown: false);

            var tree = FamilyTree.For(colony, child);

            Assert.AreEqual("ハナ", tree.Mother.Name);
            Assert.AreEqual("タロウ", tree.Father.Name);
            Assert.AreEqual("ウメ", tree.MaternalGrandmother.Name);
            Assert.AreEqual("マツ", tree.MaternalGrandfather.Name);
            Assert.IsTrue(tree.PaternalGrandmother.Unknown);
            Assert.IsFalse(tree.Self.SexKnown);
            Assert.IsFalse(tree.Mother.Gone);
            Assert.IsFalse(string.IsNullOrEmpty(tree.Mother.MorphName));
        }

        [Test]
        public void SoldAnimals_StayInTheLineage()
        {
            var colony = new Colony();
            var grandma = Add(colony, "ウメ", Sex.Female);
            var mother = Add(colony, "ハナ", Sex.Female, grandma.Id);
            var child = Add(colony, "レオパ9", Sex.Male, mother.Id);

            colony.RemoveAnimal(grandma);
            colony.RemoveAnimal(mother);
            var tree = FamilyTree.For(colony, child);

            Assert.AreEqual("ハナ", tree.Mother.Name);
            Assert.IsTrue(tree.Mother.Gone);
            Assert.AreEqual("ウメ", tree.MaternalGrandmother.Name, "the record keeps the sold mother's own mother");
            Assert.IsTrue(tree.MaternalGrandmother.Gone);
        }

        [Test]
        public void RemoveAnimal_RecordsNameMorphSexAndParents_Once()
        {
            var colony = new Colony();
            var pet = Add(colony, "ハナ", Sex.Female, 3, 4, sexKnown: false);

            colony.RemoveAnimal(pet);
            colony.RemoveAnimal(pet);

            Assert.AreEqual(1, colony.Lineage.Count);
            var record = colony.Lineage[0];
            Assert.AreEqual(pet.Id, record.Id);
            Assert.AreEqual("ハナ", record.Name);
            Assert.AreEqual(MorphNamer.FullName(pet.Genotype, pet.Known), record.MorphName);
            Assert.AreEqual(Sex.Female, record.Sex);
            Assert.IsFalse(record.SexKnown);
            Assert.AreEqual(3, record.MotherId);
            Assert.AreEqual(4, record.FatherId);
        }

        [Test]
        public void AParentIdWithNoAnimalAndNoRecord_IsUnknown()
        {
            var colony = new Colony();
            var child = Add(colony, "レオパ9", Sex.Male, 77, 0);

            var tree = FamilyTree.For(colony, child);

            Assert.IsTrue(tree.Mother.Unknown);
        }
    }
}
