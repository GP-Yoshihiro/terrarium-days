using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Schema-3 save: the whole colony. JsonUtility shape (public fields, lists only).</summary>
    [Serializable]
    public sealed class ColonySaveData
    {
        public int schemaVersion;
        public string calendarEpochUtc;
        public long money;
        public List<LedgerSaveData> ledger = new List<LedgerSaveData>();
        public List<AnimalSaveData> animals = new List<AnimalSaveData>();
        public List<CageSaveData> cages = new List<CageSaveData>();
        public int rackCount;
        public int incubatorCount;
        public int nextAnimalId;
        public int nextCageId;
        public int lastBilledMonthIndex;
        public int inventoryVersion;
        public List<InventorySaveData> inventory = new List<InventorySaveData>();
        public List<string> incubators = new List<string>();
        public int shopSeed;
        public bool shopStocked;
        public int shopMonthIndex;
        public List<ShopOfferSaveData> shopOffers = new List<ShopOfferSaveData>();
    }

    [Serializable]
    public sealed class InventorySaveData
    {
        public string id;
        public int count;
    }

    [Serializable]
    public sealed class ShopOfferSaveData
    {
        public int offerId;
        public AnimalSaveData animal;
    }

    [Serializable]
    public sealed class AnimalSaveData
    {
        public int id;
        public string name;
        public string sex;
        public double weightGrams;
        public string hatchedAtUtc;
        public string stage;
        public string stageUpDueAtUtc;
        public double hunger;
        public double hydration;
        public double cleanliness;
        public double health;
        public string selectedDecorId;
        public List<string> unlockedDecorIds;
        public string lastSavedAtUtc;
        public string lastShedAtUtc;
        public string nextShedAtUtc;
        public int genomeVersion;
        public List<GeneSaveData> genes;
        public double hypo;
        public double tangerine;
        public List<HetSaveData> hets;
        public bool hetsUnknown;
        public string personality;
        public bool personalityKnown;
        public bool sexRevealed;
        public string personalityRevealAtUtc;
    }

    [Serializable]
    public sealed class GeneSaveData
    {
        public string gene;
        public int copies;
    }

    [Serializable]
    public sealed class HetSaveData
    {
        public string gene;
        public double probability;
    }

    [Serializable]
    public sealed class CageSaveData
    {
        public int id;
        public string size;
        public int animalId;
        public List<string> decorIds = new List<string>();
    }

    [Serializable]
    public sealed class LedgerSaveData
    {
        public string atUtc;
        public string category;
        public long amount;
        public string note;
    }

    /// <summary>Reads only the schema version, to choose between loading and migrating.</summary>
    [Serializable]
    public sealed class SchemaProbe
    {
        public int schemaVersion;
    }
}
