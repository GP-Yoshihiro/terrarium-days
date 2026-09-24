using System.Collections.Generic;
using TerrariumDays.Core;

namespace TerrariumDays.Gameplay
{
    /// <summary>
    /// Fixed set of the up-to-5 selectable decorations and their unlock stage.
    /// See Terrarium_Days_仕様書.md section 9.3: rock_01 starts unlocked, Juvenile
    /// unlocks 2 more, Adult unlocks the final 2.
    /// </summary>
    public static class DecorCatalog
    {
        public static readonly IReadOnlyList<DecorDefinition> All = new List<DecorDefinition>
        {
            new DecorDefinition(PetState.DefaultDecorId, "岩", GrowthStage.Baby),
            new DecorDefinition("plant_01", "観葉植物", GrowthStage.Juvenile),
            new DecorDefinition("water_dish_01", "水入れ", GrowthStage.Juvenile),
            new DecorDefinition("heat_lamp_01", "保温ランプ", GrowthStage.Adult),
            new DecorDefinition("driftwood_01", "流木", GrowthStage.Adult),
        };
    }
}
