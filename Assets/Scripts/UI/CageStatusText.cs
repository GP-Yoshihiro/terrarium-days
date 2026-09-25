using System;
using System.Collections.Generic;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>Short texts shown on home slots and in the cage list.</summary>
    public static class CageStatusText
    {
        public static string TitleFor(Cage cage, PetState pet)
        {
            if (pet == null)
            {
                return $"ケージ{cage.Id}（空き）";
            }

            var sex = !pet.SexKnown ? "性別不明" : pet.Sex == Sex.Female ? "♀" : "♂";
            return $"ケージ{cage.Id} {pet.Name}（{GrowthModel.StageLabel(pet.Stage)}・{sex}）";
        }

        public static string AlertsFor(PetState pet, DateTimeOffset nowUtc, CareTuning tuning)
        {
            var alerts = new List<string>();
            if (pet.Hunger < tuning.HealthyCareThreshold)
            {
                alerts.Add("空腹");
            }

            if (pet.Hydration < tuning.HealthyCareThreshold)
            {
                alerts.Add("水");
            }

            if (pet.Cleanliness < tuning.HealthyCareThreshold)
            {
                alerts.Add("汚れ");
            }

            switch (AppetiteModel.Evaluate(pet, nowUtc, tuning))
            {
                case AppetiteState.PreShed:
                    alerts.Add("脱皮前");
                    break;
                case AppetiteState.PreGrowth:
                    alerts.Add("成長前");
                    break;
            }

            return string.Join("・", alerts);
        }
    }
}
