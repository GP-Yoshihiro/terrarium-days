using System;
using System.Text;
using TerrariumDays.Core;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    public enum LedgerSection
    {
        Animals,
        Money
    }

    /// <summary>
    /// The ledger tab (§6.2): every animal at a glance, and every money movement. Draws its
    /// rows in code (like ShopView) and skips rebuilding when nothing shown has changed. All
    /// the text is built by the pure <see cref="LedgerText"/>.
    /// </summary>
    public sealed class LedgerView
    {
        private readonly Button animalsButton;
        private readonly Button moneyButton;
        private readonly Label summaryLabel;
        private readonly ScrollView list;

        private Colony colony;
        private GameCalendar calendar;
        private DateTimeOffset nowUtc;
        private CareTuning tuning;
        private string lastSignature;

        public LedgerView(VisualElement root)
        {
            animalsButton = root.Q<Button>("ledger-section-animals");
            moneyButton = root.Q<Button>("ledger-section-money");
            summaryLabel = root.Q<Label>("ledger-summary-label");
            list = root.Q<ScrollView>("ledger-list");

            if (animalsButton != null)
            {
                animalsButton.clicked += OnAnimalsSectionClicked;
            }

            if (moneyButton != null)
            {
                moneyButton.clicked += OnMoneySectionClicked;
            }

            UpdateSectionButtons();
        }

        public LedgerSection Section { get; private set; } = LedgerSection.Animals;

        public event Action<int> AnimalTapped;

        /// <summary>Forces the next Render to rebuild even if the signature has not changed; call when the tab becomes visible again.</summary>
        public void Invalidate()
        {
            lastSignature = null;
        }

        public void Render(Colony renderColony, GameCalendar renderCalendar, DateTimeOffset renderNowUtc, CareTuning renderTuning)
        {
            colony = renderColony;
            calendar = renderCalendar;
            nowUtc = renderNowUtc;
            tuning = renderTuning;
            RenderInternal();
        }

        public void Dispose()
        {
            if (animalsButton != null)
            {
                animalsButton.clicked -= OnAnimalsSectionClicked;
            }

            if (moneyButton != null)
            {
                moneyButton.clicked -= OnMoneySectionClicked;
            }
        }

        private void OnAnimalsSectionClicked() => SetSection(LedgerSection.Animals);

        private void OnMoneySectionClicked() => SetSection(LedgerSection.Money);

        private void SetSection(LedgerSection section)
        {
            if (Section == section)
            {
                return;
            }

            Section = section;
            Invalidate();
            RenderInternal();
        }

        private void RenderInternal()
        {
            if (colony == null || calendar == null || list == null)
            {
                return;
            }

            var signature = Signature();
            if (signature == lastSignature)
            {
                return;
            }

            lastSignature = signature;

            UpdateSectionButtons();

            if (summaryLabel != null)
            {
                var showSummary = Section == LedgerSection.Money;
                summaryLabel.style.display = showSummary ? DisplayStyle.Flex : DisplayStyle.None;
                summaryLabel.text = showSummary ? MonthSummaryText() : string.Empty;
            }

            list.Clear();
            switch (Section)
            {
                case LedgerSection.Animals:
                    RenderAnimals();
                    break;
                case LedgerSection.Money:
                    RenderMoney();
                    break;
            }
        }

        private void UpdateSectionButtons()
        {
            animalsButton?.EnableInClassList("tab-selected", Section == LedgerSection.Animals);
            moneyButton?.EnableInClassList("tab-selected", Section == LedgerSection.Money);
        }

        private string MonthSummaryText()
        {
            var totals = LedgerText.MonthTotals(colony.Wallet.Ledger, calendar, calendar.MonthIndexAt(nowUtc));
            return LedgerText.MonthSummary(totals.Income, totals.Expense);
        }

        /// <summary>Animals: HomeView's own list signature plus each animal's age in months (shown in AnimalDetail but not in CageListSignature). Money: the ledger's length and the money on hand.</summary>
        private string Signature()
        {
            var sb = new StringBuilder();
            sb.Append(Section);
            switch (Section)
            {
                case LedgerSection.Animals:
                    sb.Append('|').Append(HomeView.CageListSignature(colony, nowUtc, tuning));
                    foreach (var pet in colony.Animals)
                    {
                        sb.Append(';').Append(pet.Id).Append(':').Append((int)Math.Floor(GrowthModel.AgeMonths(pet, nowUtc)));
                    }

                    break;
                case LedgerSection.Money:
                    sb.Append('|').Append(colony.Wallet.Ledger.Count).Append(':').Append(colony.Wallet.Money);
                    break;
            }

            return sb.ToString();
        }

        private void RenderAnimals()
        {
            var occupied = colony.OccupiedCages();
            if (occupied.Count == 0)
            {
                list.Add(EmptyLabel(CageStatusText.NoAnimalsMessage));
                return;
            }

            foreach (var cage in occupied)
            {
                var pet = colony.AnimalIn(cage);
                if (pet != null)
                {
                    list.Add(AnimalRow(pet));
                }
            }
        }

        private VisualElement AnimalRow(PetState pet)
        {
            var row = new Button(() => AnimalTapped?.Invoke(pet.Id));
            row.AddToClassList("ledger-row");

            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.AddToClassList("ledger-row-header");
            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList("ledger-row-thumb");
            var thumbnail = MorphSprites.Thumbnail(pet);
            if (thumbnail != null)
            {
                thumb.style.backgroundImage = new StyleBackground(thumbnail);
            }

            header.Add(thumb);
            header.Add(NameLabel(pet.Name));
            row.Add(header);

            row.Add(ProfileLabel(string.Join(" ", CageStatusText.ProfileTokens(pet))));
            row.Add(DetailLabel(LedgerText.AnimalDetail(pet, nowUtc)));

            return row;
        }

        private void RenderMoney()
        {
            var recent = LedgerText.Recent(colony.Wallet.Ledger);
            if (recent.Count == 0)
            {
                list.Add(EmptyLabel("まだ記録がありません"));
                return;
            }

            foreach (var entry in recent)
            {
                list.Add(MoneyRow(entry));
            }
        }

        private VisualElement MoneyRow(LedgerEntry entry)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("ledger-money-row");

            row.Add(DetailLabel(LedgerText.EntryLine(entry, calendar)));

            var amountLabel = new Label(LedgerText.SignedYen(entry.Amount)) { pickingMode = PickingMode.Ignore };
            amountLabel.AddToClassList("ledger-money-amount-label");
            amountLabel.AddToClassList(entry.Amount >= 0 ? "ledger-money-income" : "ledger-money-expense");
            row.Add(amountLabel);

            return row;
        }

        private static Label EmptyLabel(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("ledger-empty-label");
            return label;
        }

        private static Label NameLabel(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("ledger-row-name-label");
            return label;
        }

        private static Label ProfileLabel(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("ledger-row-profile-label");
            return label;
        }

        private static Label DetailLabel(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("ledger-row-detail-label");
            return label;
        }
    }
}
