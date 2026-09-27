using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerrariumDays.Core;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    /// <summary>
    /// The breeding tab (§7.2): ongoing pairings/gravid/eggs, picking a male and a female, the
    /// forecast, and starting or cancelling a pairing. Draws its rows in code (like ShopView)
    /// and skips rebuilding when nothing shown has changed, so a rebuild mid-tap never swallows
    /// the tap (see ShopView.Signature).
    /// </summary>
    public sealed class PairingView
    {
        private readonly Label seasonLabel;
        private readonly Label messageLabel;
        private readonly ScrollView list;

        private Colony colony;
        private BreedingService breeding;
        private GameCalendar calendar;
        private DateTimeOffset nowUtc;
        private RoomClimate room;
        private string lastSignature;

        public PairingView(VisualElement root)
        {
            seasonLabel = root.Q<Label>("breeding-season-label");
            messageLabel = root.Q<Label>("breeding-message-label");
            list = root.Q<ScrollView>("breeding-list");
        }

        public int? SelectedMaleId { get; private set; }

        public int? SelectedFemaleId { get; private set; }

        public event Action<int, int> StartRequested;

        public event Action<int> CancelRequested;

        /// <summary>Forces the next Render to rebuild even if the signature has not changed; call when the tab becomes visible again.</summary>
        public void Invalidate()
        {
            lastSignature = null;
        }

        /// <summary>Picks this animal for its known role, ready for the player to pick the other side.</summary>
        public void Preselect(PetState pet)
        {
            if (pet == null || !pet.SexKnown)
            {
                return;
            }

            if (pet.Sex == Sex.Female)
            {
                SelectedFemaleId = pet.Id;
            }
            else
            {
                SelectedMaleId = pet.Id;
            }

            Invalidate();
        }

        public void ShowMessage(string message)
        {
            if (messageLabel != null)
            {
                messageLabel.text = message;
            }
        }

        public void Render(Colony renderColony, BreedingService renderBreeding, GameCalendar renderCalendar, DateTimeOffset renderNowUtc,
            RoomClimate renderRoom)
        {
            colony = renderColony;
            breeding = renderBreeding;
            calendar = renderCalendar;
            nowUtc = renderNowUtc;
            room = renderRoom;
            RenderInternal();
        }

        public void Dispose()
        {
            // Every row/button is rebuilt (and its handler with it) each RenderInternal, so
            // there is nothing standing that needs unhooking here.
        }

        private void RenderInternal()
        {
            if (colony == null || breeding == null || calendar == null || list == null)
            {
                return;
            }

            ClearInvalidSelection();

            var signature = Signature();
            if (signature == lastSignature)
            {
                return;
            }

            lastSignature = signature;

            if (seasonLabel != null)
            {
                seasonLabel.text = BreedingText.SeasonLine(calendar.DateAt(nowUtc), breeding.Care);
            }

            list.Clear();
            RenderOngoing();
            RenderCandidates(Sex.Female, "メスを選ぶ");
            RenderCandidates(Sex.Male, "オスを選ぶ");
            RenderUnknownSexNotice();
            RenderForecast();
            RenderStartButton();
        }

        private void ClearInvalidSelection()
        {
            if (SelectedFemaleId.HasValue && !IsSelectable(colony.AnimalById(SelectedFemaleId.Value)))
            {
                SelectedFemaleId = null;
            }

            if (SelectedMaleId.HasValue && !IsSelectable(colony.AnimalById(SelectedMaleId.Value)))
            {
                SelectedMaleId = null;
            }
        }

        private bool IsSelectable(PetState pet)
        {
            if (pet == null)
            {
                return false;
            }

            var problem = breeding.CheckCandidate(colony, pet, nowUtc, calendar);
            return problem == PairingProblem.None || problem == PairingProblem.OutOfSeason;
        }

        private string Signature()
        {
            var sb = new StringBuilder();
            var date = calendar.DateAt(nowUtc);
            sb.Append(date.Year).Append('-').Append(date.Month).Append('-').Append(date.Day);
            sb.Append('|').Append(SelectedMaleId).Append('|').Append(SelectedFemaleId);
            foreach (var pairing in colony.Pairings)
            {
                sb.Append(";p:").Append(pairing.Id);
            }

            foreach (var pet in colony.Animals)
            {
                if (pet.Gravid != null)
                {
                    sb.Append(";g:").Append(pet.Id).Append(':').Append(pet.Gravid.ClutchesLaid);
                }
            }

            foreach (var cage in colony.Cages)
            {
                var eggCount = colony.EggsIn(cage).Count;
                if (eggCount > 0)
                {
                    sb.Append(";e:").Append(cage.Id).Append(':').Append(eggCount);
                }

                var pet = colony.AnimalIn(cage);
                if (pet != null && pet.SexKnown)
                {
                    sb.Append(";c:").Append(pet.Id).Append(':').Append((int)breeding.CheckCandidate(colony, pet, nowUtc, calendar));
                }
            }

            sb.Append(";u:").Append(colony.Animals.Count(a => !a.SexKnown));
            return sb.ToString();
        }

        private void RenderOngoing()
        {
            list.Add(SectionTitle("進行中"));

            var gravidFemales = colony.Animals.FindAll(a => a.Gravid != null);
            var eggCages = colony.Cages.FindAll(c => colony.EggsIn(c).Count > 0);
            if (colony.Pairings.Count == 0 && gravidFemales.Count == 0 && eggCages.Count == 0)
            {
                list.Add(Line("進行中の繁殖はありません"));
                return;
            }

            foreach (var pairing in colony.Pairings)
            {
                var male = colony.AnimalById(pairing.MaleId);
                var female = colony.AnimalById(pairing.FemaleId);
                if (male == null || female == null)
                {
                    continue;
                }

                var row = new VisualElement();
                row.AddToClassList("breeding-row-header");

                var lineLabel = Line(BreedingText.PairingLine(pairing, male, female, nowUtc));
                lineLabel.style.flexGrow = 1;
                row.Add(lineLabel);

                var cancelButton = new Button(() => CancelRequested?.Invoke(female.Id)) { text = "やめる", name = $"breeding-cancel-{female.Id}" };
                cancelButton.AddToClassList("breeding-cancel-button");
                row.Add(cancelButton);

                list.Add(row);
            }

            foreach (var female in gravidFemales)
            {
                list.Add(Line(BreedingText.GravidLine(female, calendar)));
            }

            foreach (var cage in eggCages)
            {
                list.Add(Line($"ケージ{cage.Id}："));
                foreach (var eggLine in BreedingText.EggLines(colony.EggsIn(cage), room, nowUtc, breeding.Care))
                {
                    list.Add(Line(eggLine));
                }
            }
        }

        private void RenderCandidates(Sex role, string title)
        {
            list.Add(SectionTitle(title));

            foreach (var cage in colony.Cages)
            {
                var pet = colony.AnimalIn(cage);
                if (pet == null || !pet.SexKnown || pet.Sex != role)
                {
                    continue;
                }

                list.Add(CandidateRow(pet, role));
            }

            list.Add(Line(BreedingText.Requirement(role, breeding.Care)));
        }

        private VisualElement CandidateRow(PetState pet, Sex role)
        {
            var problem = breeding.CheckCandidate(colony, pet, nowUtc, calendar);
            var blocked = problem != PairingProblem.None && problem != PairingProblem.OutOfSeason;
            var selectedId = role == Sex.Female ? SelectedFemaleId : SelectedMaleId;
            var selected = selectedId == pet.Id;

            var row = new Button { name = $"breeding-candidate-{pet.Id}" };
            row.AddToClassList("breeding-row");
            row.EnableInClassList("breeding-row-selected", selected);
            row.EnableInClassList("breeding-row-blocked", blocked);
            row.SetEnabled(!blocked);

            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.AddToClassList("breeding-row-header");

            var lineLabel = new Label(BreedingText.CandidateLine(pet, nowUtc)) { pickingMode = PickingMode.Ignore };
            lineLabel.AddToClassList("breeding-row-name-label");
            header.Add(lineLabel);

            if (blocked)
            {
                var reasonLabel = new Label(BreedingText.ProblemLabel(problem)) { pickingMode = PickingMode.Ignore };
                reasonLabel.AddToClassList("breeding-row-reason");
                header.Add(reasonLabel);
            }

            row.Add(header);

            if (!blocked)
            {
                row.clicked += () => OnCandidateClicked(role, pet.Id);
            }

            return row;
        }

        private void OnCandidateClicked(Sex role, int petId)
        {
            if (role == Sex.Female)
            {
                SelectedFemaleId = SelectedFemaleId == petId ? (int?)null : petId;
            }
            else
            {
                SelectedMaleId = SelectedMaleId == petId ? (int?)null : petId;
            }

            Invalidate();
            RenderInternal();
        }

        private void RenderUnknownSexNotice()
        {
            var unknown = colony.Animals.Count(a => !a.SexKnown);
            if (unknown > 0)
            {
                list.Add(Line($"性別が分かっていない個体（{unknown}匹）は選べません"));
            }
        }

        private void RenderForecast()
        {
            var male = SelectedMaleId.HasValue ? colony.AnimalById(SelectedMaleId.Value) : null;
            var female = SelectedFemaleId.HasValue ? colony.AnimalById(SelectedFemaleId.Value) : null;
            if (male == null || female == null)
            {
                return;
            }

            var forecast = BreedingForecast.For(male, female, breeding.Care);
            foreach (var line in BreedingText.ForecastLines(forecast))
            {
                var label = new Label(line) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("breeding-forecast-line");
                list.Add(label);
            }
        }

        private void RenderStartButton()
        {
            var male = SelectedMaleId.HasValue ? colony.AnimalById(SelectedMaleId.Value) : null;
            var female = SelectedFemaleId.HasValue ? colony.AnimalById(SelectedFemaleId.Value) : null;
            var problem = male != null && female != null
                ? breeding.CheckPair(colony, male, female, nowUtc, calendar)
                : PairingProblem.None;
            var canStart = male != null && female != null && problem == PairingProblem.None;

            var startButton = new Button(() => StartRequested?.Invoke(male.Id, female.Id)) { text = "ペアリングを始める", name = "breeding-start-button" };
            startButton.AddToClassList("breeding-start-button");
            startButton.SetEnabled(canStart);
            list.Add(startButton);

            if (!canStart && male != null && female != null)
            {
                var reasonLabel = new Label(BreedingText.ProblemLabel(problem)) { pickingMode = PickingMode.Ignore };
                reasonLabel.AddToClassList("breeding-row-reason");
                list.Add(reasonLabel);
            }
        }

        private static Label SectionTitle(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("breeding-section-title");
            return label;
        }

        private static Label Line(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("breeding-line");
            return label;
        }
    }
}
