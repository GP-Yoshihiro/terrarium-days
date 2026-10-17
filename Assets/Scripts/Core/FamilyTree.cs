namespace TerrariumDays.Core
{
    /// <summary>What we remember about an animal that left the room (phase 5 spec §2.5, decision 25).</summary>
    public sealed class LineageRecord
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string MorphName { get; set; } = string.Empty;

        public Sex Sex { get; set; } = Sex.Female;

        public bool SexKnown { get; set; }

        public int MotherId { get; set; }

        public int FatherId { get; set; }
    }

    /// <summary>One box in the family tree.</summary>
    public sealed class FamilyMember
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string MorphName { get; set; } = string.Empty;

        public Sex Sex { get; set; } = Sex.Female;

        public bool SexKnown { get; set; }

        /// <summary>Left the room; known only from the lineage record.</summary>
        public bool Gone { get; set; }

        /// <summary>No information (shop animal's parents, debug eggs).</summary>
        public bool Unknown { get; set; }

        public int MotherId { get; set; }

        public int FatherId { get; set; }

        public static FamilyMember UnknownMember() => new FamilyMember { Unknown = true };
    }

    public sealed class FamilyTreeData
    {
        public FamilyMember Self { get; set; }
        public FamilyMember Mother { get; set; }
        public FamilyMember Father { get; set; }
        public FamilyMember MaternalGrandmother { get; set; }
        public FamilyMember MaternalGrandfather { get; set; }
        public FamilyMember PaternalGrandmother { get; set; }
        public FamilyMember PaternalGrandfather { get; set; }
    }

    /// <summary>Three generations (§4.2): living animals first, then lineage records, else unknown.</summary>
    public static class FamilyTree
    {
        public static FamilyTreeData For(Colony colony, PetState pet)
        {
            var self = FromPet(pet);
            var mother = Member(colony, self.MotherId);
            var father = Member(colony, self.FatherId);
            return new FamilyTreeData
            {
                Self = self,
                Mother = mother,
                Father = father,
                MaternalGrandmother = Member(colony, mother.MotherId),
                MaternalGrandfather = Member(colony, mother.FatherId),
                PaternalGrandmother = Member(colony, father.MotherId),
                PaternalGrandfather = Member(colony, father.FatherId)
            };
        }

        public static FamilyMember Member(Colony colony, int id)
        {
            if (id <= 0)
            {
                return FamilyMember.UnknownMember();
            }

            var pet = colony.AnimalById(id);
            if (pet != null)
            {
                return FromPet(pet);
            }

            var record = colony.Lineage.Find(r => r.Id == id);
            if (record == null)
            {
                return FamilyMember.UnknownMember();
            }

            return new FamilyMember
            {
                Id = record.Id,
                Name = record.Name ?? string.Empty,
                MorphName = record.MorphName ?? string.Empty,
                Sex = record.Sex,
                SexKnown = record.SexKnown,
                Gone = true,
                MotherId = record.MotherId,
                FatherId = record.FatherId
            };
        }

        public static LineageRecord RecordOf(PetState pet) => new LineageRecord
        {
            Id = pet.Id,
            Name = pet.Name,
            MorphName = MorphNamer.FullName(pet.Genotype, pet.Known),
            Sex = pet.Sex,
            SexKnown = pet.SexKnown,
            MotherId = pet.MotherId,
            FatherId = pet.FatherId
        };

        private static FamilyMember FromPet(PetState pet) => new FamilyMember
        {
            Id = pet.Id,
            Name = pet.Name,
            MorphName = MorphNamer.FullName(pet.Genotype, pet.Known),
            Sex = pet.Sex,
            SexKnown = pet.SexKnown,
            MotherId = pet.MotherId,
            FatherId = pet.FatherId
        };
    }
}
