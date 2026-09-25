namespace TerrariumDays.Core
{
    /// <summary>
    /// What the pet "says" when tapped: a hint at its most pressing need, so the player
    /// can read its condition from the pet itself and not only from the status bars.
    /// </summary>
    public static class PetMoodMessage
    {
        public const string Hungry = "おなかがすいたみたい…";
        public const string Thirsty = "のどがかわいたみたい…";
        public const string Dirty = "テラリウムがよごれてきた…";
        public const string Unwell = "元気がないみたい…";
        public const string Happy = "ごきげんだよ！";
        public const string Threatened = "さわりすぎて怒っちゃった！";
        public const string Woken = "起こしちゃった…";

        public static string For(PetState state, CareTuning tuning)
        {
            var lowest = System.Math.Min(state.Hunger, System.Math.Min(state.Hydration, state.Cleanliness));

            if (lowest < tuning.HealthyCareThreshold)
            {
                if (lowest == state.Hunger)
                {
                    return Hungry;
                }

                return lowest == state.Hydration ? Thirsty : Dirty;
            }

            return state.Health < tuning.HealthyCareThreshold ? Unwell : Happy;
        }
    }
}
