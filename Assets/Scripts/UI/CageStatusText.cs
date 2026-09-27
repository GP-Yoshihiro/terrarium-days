using System;
using System.Collections.Generic;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>Short texts shown on home slots and in the cage list.</summary>
    public static class CageStatusText
    {
        /// <summary>Shown when the colony has no animals (the last one was wholesaled) — the cage list, and bulk care feedback.</summary>
        public const string NoAnimalsMessage = "個体がいません。ショップで迎えましょう";

        public static string TitleFor(Cage cage, PetState pet)
        {
            if (pet == null)
            {
                return $"ケージ{cage.Id}（空き）";
            }

            var sex = !pet.SexKnown ? "性別不明" : pet.Sex == Sex.Female ? "♀" : "♂";
            return $"ケージ{cage.Id} {pet.Name}（{GrowthModel.StageLabel(pet.Stage)}・{sex}）";
        }

        /// <summary>The cage-list title for a cage whose resident is away visiting (§7.2): id only, no name or stage.</summary>
        public static string AwayTitle(Cage cage) => $"ケージ{cage.Id}";

        /// <summary>The breeding alerts (§7) then the care alerts, joined by "・". Only the breeding alerts when <paramref name="shown"/> is null.</summary>
        public static string AlertsFor(Colony colony, Cage cage, PetState shown, DateTimeOffset nowUtc, CareTuning tuning)
        {
            var alerts = new List<string>(BreedingText.Alerts(colony, cage, shown));
            if (shown != null)
            {
                if (shown.Hunger < tuning.HealthyCareThreshold)
                {
                    alerts.Add("空腹");
                }

                if (shown.Hydration < tuning.HealthyCareThreshold)
                {
                    alerts.Add("水");
                }

                if (shown.Cleanliness < tuning.HealthyCareThreshold)
                {
                    alerts.Add("汚れ");
                }

                switch (AppetiteModel.Evaluate(shown, nowUtc, tuning))
                {
                    case AppetiteState.PreShed:
                        alerts.Add("脱皮前");
                        break;
                    case AppetiteState.PreGrowth:
                        alerts.Add("成長前");
                        break;
                }
            }

            return string.Join("・", alerts);
        }

        /// <summary>The morph name and personality shown under the growth-stage label (§4.1/§5.2).</summary>
        public static string ProfileFor(PetState pet)
        {
            var personality = pet.PersonalityKnown ? PersonalityTraits.Label(pet.Personality) : "性格不明";
            return $"{MorphNamer.FullName(pet.Genotype, pet.Known)}・{personality}";
        }

        /// <summary>The profile as words that must not be broken across lines (§4.2 name words, then the personality).</summary>
        public static List<string> ProfileTokens(PetState pet)
        {
            var tokens = new List<string>(MorphNamer.FullName(pet.Genotype, pet.Known).Split(' '));
            tokens.Add(pet.PersonalityKnown ? PersonalityTraits.Label(pet.Personality) : "性格不明");
            return tokens;
        }

        public static string SexLabel(PetState pet) => !pet.SexKnown ? "性別不明" : pet.Sex == Sex.Female ? "♀メス" : "♂オス";

        /// <summary>Feedback text shown the moment a shed reveals the animal's sex.</summary>
        public static string SexRevealMessage(PetState pet) =>
            $"{pet.Name}は{(pet.Sex == Sex.Female ? "♀メス" : "♂オス")}でした";
    }
}
