using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>
    /// JsonUtility-serializable save shape. Field names and casing match the schema in
    /// Terrarium_Days_仕様書.md section 8.1 exactly.
    /// </summary>
    [Serializable]
    public sealed class PetSaveData
    {
        public int schemaVersion;
        public string lastSavedAtUtc;
        public double hunger;
        public double hydration;
        public double cleanliness;
        public double health;
        public double growth;
        public string growthStage;
        public string selectedDecorId;
        public List<string> unlockedDecorIds;
    }
}
