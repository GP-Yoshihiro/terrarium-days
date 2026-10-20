using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Schema-4 save: the whole colony. JsonUtility shape (public fields, lists only).</summary>
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
        public List<int> newRackIndices = new List<int>();
        public int incubatorCount;
        public int nextAnimalId;
        public int nextCageId;
        public int lastBilledMonthIndex;
        public int inventoryVersion;
        public List<InventorySaveData> inventory = new List<InventorySaveData>();
        public List<string> incubators = new List<string>();
        public List<IncubatorSaveData> incubatorStates = new List<IncubatorSaveData>();
        public int shopSeed;
        public bool shopStocked;
        public int shopMonthIndex;
        public List<ShopOfferSaveData> shopOffers = new List<ShopOfferSaveData>();
        public long gameClockOffsetTicks;
        public int nextPairingId;
        public int nextEggId;
        public List<PairingSaveData> pairings = new List<PairingSaveData>();
        public List<GravidSaveData> gravidStates = new List<GravidSaveData>();
        public List<EggSaveData> eggs = new List<EggSaveData>();
        public List<LineageSaveData> lineage = new List<LineageSaveData>();
        public EventStateSaveData events = new EventStateSaveData();
    }

    [Serializable]
    public sealed class PairingSaveData
    {
        public int id;
        public int maleId;
        public int femaleId;
        public string startedAtUtc;
        public string endsAtUtc;
        public double successChance;
    }

    [Serializable]
    public sealed class GravidSaveData
    {
        public int animalId;
        public int pairingId;
        public int fatherId;
        public string fatherName;
        public List<GeneSaveData> fatherGenes;
        public double fatherHypo;
        public double fatherTangerine;
        public List<HetSaveData> fatherHets;
        public bool fatherHetsUnknown;
        public string compatibility;
        public int seasonYear;
        public int clutchesPlanned;
        public int clutchesLaid;
        public string nextClutchAtUtc;
    }

    [Serializable]
    public sealed class EggSaveData
    {
        public int id;
        public int motherId;
        public int fatherId;
        public int cageId;
        public string laidAtUtc;
        public string place;
        public bool fertile;
        public List<GeneSaveData> genes;
        public double hypo;
        public double tangerine;
        public List<HetSaveData> hets;
        public bool hetsUnknown;
        public double developmentPercent;
        public double middleThirdTemperatureSum;
        public double middleThirdGameDays;
        public double coldGameDays;
        public string failure;
        public string appliedUntilUtc;
        public int incubatorId;
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
        public bool weak;
        public int motherId;
        public int fatherId;
        public string careGraceUntilUtc;
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
        public bool hasNestBox;
    }

    [Serializable]
    public sealed class LedgerSaveData
    {
        public string atUtc;
        public string category;
        public long amount;
        public string note;
    }

    [Serializable]
    public sealed class IncubatorSaveData
    {
        public int id;
        public string model;
        public int setTemperatureC;
        public double humidityPercent;
        public string appliedUntilUtc;
    }

    [Serializable]
    public sealed class LineageSaveData
    {
        public int id;
        public string name;
        public string morphName;
        public string sex;
        public bool sexKnown;
        public int motherId;
        public int fatherId;
    }

    /// <summary>Reads only the schema version, to choose between loading and migrating.</summary>
    [Serializable]
    public sealed class SchemaProbe
    {
        public int schemaVersion;
    }

    [Serializable]
    public sealed class EventStateSaveData
    {
        public bool hasEntry;
        public EventEntrySaveData entry = new EventEntrySaveData();
        public bool hasSummary;
        public EventSummarySaveData summary = new EventSummarySaveData();
        public bool summarySeen;

        /// <summary>The notice event number plus one (0 = none), so a missing field reads as none.</summary>
        public int noticeNumberPlusOne;
        public int noticeStage;
    }

    [Serializable]
    public sealed class EventEntrySaveData
    {
        public int number;
        public long feePaid;
        public bool frozen;
        public int listingSeed;
        public int nextOfferId;
        public List<EventListingSaveData> listings = new List<EventListingSaveData>();
        public List<EventActionSaveData> actions = new List<EventActionSaveData>();
        public List<EventBoothOfferSaveData> boothOffers = new List<EventBoothOfferSaveData>();
        public List<EventAnimalLineSaveData> purchases = new List<EventAnimalLineSaveData>();
    }

    [Serializable]
    public sealed class EventListingSaveData
    {
        public int animalId;
        public long price;
        public int tolerancePercent;
        public long frozenBase;
        public double frozenChance;

        /// <summary>The action it sold in plus one (0 = unsold).</summary>
        public int soldInActionPlusOne;
    }

    [Serializable]
    public sealed class EventAnimalLineSaveData
    {
        public int animalId;
        public string name;
        public string morphLabel;
        public long amount;
        public int minuteOfDay;
    }

    [Serializable]
    public sealed class EventActionSaveData
    {
        public int index;
        public string kind;
        public string playedAtUtc;
        public int boothMinuteOfDay;
        public List<EventAnimalLineSaveData> sales = new List<EventAnimalLineSaveData>();
    }

    [Serializable]
    public sealed class EventBoothOfferSaveData
    {
        public int offerId;
        public int actionIndex;
        public bool hasAnimal;
        public AnimalSaveData animal;
        public string morphLabel;
        public long price;
        public bool rare;
        public bool bought;
    }

    [Serializable]
    public sealed class EventSummarySaveData
    {
        public int number;
        public long boothFee;
        public List<EventAnimalLineSaveData> sold = new List<EventAnimalLineSaveData>();
        public List<EventAnimalLineSaveData> unsold = new List<EventAnimalLineSaveData>();
        public List<EventAnimalLineSaveData> bought = new List<EventAnimalLineSaveData>();
    }
}
