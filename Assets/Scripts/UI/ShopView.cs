using System;
using System.Collections.Generic;
using System.Text;
using TerrariumDays.Core;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    public enum ShopSection
    {
        Animals,
        Supplies,
        Wholesale
    }

    /// <summary>
    /// The shop tab (§9): animals for sale this month, supplies, and wholesaling the player's
    /// own animals. Draws its rows in code (like HomeView) and skips rebuilding when nothing
    /// shown has changed. Prices always come through <see cref="ShopService"/>; this view owns
    /// no economy rules, only the catalog's own default prices for supplies (the colony never
    /// customises <see cref="EconomyTuning"/>).
    /// </summary>
    public sealed class ShopView
    {
        private readonly EconomyTuning economy = new EconomyTuning();
        private readonly Button animalsButton;
        private readonly Button suppliesButton;
        private readonly Button wholesaleButton;
        private readonly Label restockLabel;
        private readonly ScrollView list;

        private Colony colony;
        private ShopService shopService;
        private GameCalendar calendar;
        private DateTimeOffset nowUtc;
        private string lastSignature;

        public ShopView(VisualElement root)
        {
            animalsButton = root.Q<Button>("shop-section-animals");
            suppliesButton = root.Q<Button>("shop-section-supplies");
            wholesaleButton = root.Q<Button>("shop-section-wholesale");
            restockLabel = root.Q<Label>("shop-restock-label");
            list = root.Q<ScrollView>("shop-list");

            if (animalsButton != null)
            {
                animalsButton.clicked += OnAnimalsSectionClicked;
            }

            if (suppliesButton != null)
            {
                suppliesButton.clicked += OnSuppliesSectionClicked;
            }

            if (wholesaleButton != null)
            {
                wholesaleButton.clicked += OnWholesaleSectionClicked;
            }

            UpdateSectionButtons();
        }

        public ShopSection Section { get; private set; } = ShopSection.Animals;

        public event Action<int> OfferBuyRequested;
        public event Action<string> ItemBuyRequested;
        public event Action<int> WholesaleRequested;

        /// <summary>Forces the next Render to rebuild even if the signature has not changed; call when the tab becomes visible again.</summary>
        public void Invalidate()
        {
            lastSignature = null;
        }

        public void Render(Colony renderColony, ShopService renderShopService, GameCalendar renderCalendar, DateTimeOffset renderNowUtc)
        {
            colony = renderColony;
            shopService = renderShopService;
            calendar = renderCalendar;
            nowUtc = renderNowUtc;
            RenderInternal();
        }

        public void Dispose()
        {
            if (animalsButton != null)
            {
                animalsButton.clicked -= OnAnimalsSectionClicked;
            }

            if (suppliesButton != null)
            {
                suppliesButton.clicked -= OnSuppliesSectionClicked;
            }

            if (wholesaleButton != null)
            {
                wholesaleButton.clicked -= OnWholesaleSectionClicked;
            }
        }

        private void OnAnimalsSectionClicked() => SetSection(ShopSection.Animals);

        private void OnSuppliesSectionClicked() => SetSection(ShopSection.Supplies);

        private void OnWholesaleSectionClicked() => SetSection(ShopSection.Wholesale);

        private void SetSection(ShopSection section)
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
            if (colony == null || shopService == null || list == null)
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

            if (restockLabel != null)
            {
                var showRestock = Section == ShopSection.Animals && calendar != null;
                restockLabel.style.display = showRestock ? DisplayStyle.Flex : DisplayStyle.None;
                restockLabel.text = showRestock ? ShopText.NextRestock(calendar, nowUtc) : string.Empty;
            }

            list.Clear();
            switch (Section)
            {
                case ShopSection.Animals:
                    RenderAnimals();
                    break;
                case ShopSection.Supplies:
                    RenderSupplies();
                    break;
                case ShopSection.Wholesale:
                    RenderWholesale();
                    break;
            }
        }

        private void UpdateSectionButtons()
        {
            animalsButton?.EnableInClassList("tab-selected", Section == ShopSection.Animals);
            suppliesButton?.EnableInClassList("tab-selected", Section == ShopSection.Supplies);
            wholesaleButton?.EnableInClassList("tab-selected", Section == ShopSection.Wholesale);
        }

        private string Signature()
        {
            var sb = new StringBuilder();
            sb.Append(Section).Append('|').Append(calendar != null ? calendar.MonthIndexAt(nowUtc) : -1);
            switch (Section)
            {
                case ShopSection.Animals:
                    foreach (var offer in colony.Shop.Offers)
                    {
                        sb.Append(';').Append(offer.OfferId).Append(':').Append(shopService.PriceOf(offer));
                    }

                    break;
                case ShopSection.Supplies:
                    foreach (var item in ShopCatalog.Items(economy))
                    {
                        sb.Append(';').Append(item.Id).Append(':').Append(colony.Inventory.Count(item.Id));
                    }

                    break;
                case ShopSection.Wholesale:
                    foreach (var pet in colony.Animals)
                    {
                        sb.Append(';').Append(pet.Id).Append(':').Append(pet.Name).Append(':')
                            .Append(shopService.WholesalePriceOf(pet)).Append(':').Append(colony.Animals.Count);
                    }

                    break;
            }

            return sb.ToString();
        }

        private void RenderAnimals()
        {
            if (colony.Shop.Offers.Count == 0)
            {
                list.Add(EmptyLabel("今月の入荷は売り切れました"));
                return;
            }

            foreach (var offer in colony.Shop.Offers)
            {
                list.Add(AnimalRow(offer));
            }
        }

        private VisualElement AnimalRow(ShopOffer offer)
        {
            var animal = offer.Animal;
            var row = new VisualElement();
            row.AddToClassList("shop-row");

            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.AddToClassList("shop-row-header");
            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList("shop-row-thumb");
            var thumbnail = MorphSprites.Thumbnail(animal);
            if (thumbnail != null)
            {
                thumb.style.backgroundImage = new StyleBackground(thumbnail);
            }

            header.Add(thumb);

            var words = new List<string>(CageStatusText.ProfileTokens(animal));
            if (words.Count > 0)
            {
                // The last token is the personality word; OfferDetail already says the
                // personality is revealed only after purchase, so it is left out here.
                words.RemoveAt(words.Count - 1);
            }

            header.Add(NameLabel(string.Join(" ", words)));
            row.Add(header);

            row.Add(DetailLabel(ShopText.OfferDetail(animal)));

            var footer = new VisualElement();
            footer.AddToClassList("shop-row-footer");
            footer.Add(PriceLabel(ShopText.Yen(shopService.PriceOf(offer))));

            var buyButton = new Button(() => OfferBuyRequested?.Invoke(offer.OfferId)) { text = "買う" };
            buyButton.AddToClassList("shop-buy-button");
            footer.Add(buyButton);
            row.Add(footer);

            return row;
        }

        private void RenderSupplies()
        {
            foreach (var item in ShopCatalog.Items(economy))
            {
                list.Add(ItemRow(item));
            }
        }

        private VisualElement ItemRow(ShopItem item)
        {
            var row = new VisualElement();
            row.AddToClassList("shop-row");

            row.Add(NameLabel(ShopText.ItemLabel(item)));
            if (item.Kind == ShopItemKind.Decor || item.Kind == ShopItemKind.NestBox)
            {
                var owned = colony.Inventory.Count(item.Id);
                if (owned > 0)
                {
                    row.Add(DetailLabel($"所持{owned}"));
                }
            }

            var footer = new VisualElement();
            footer.AddToClassList("shop-row-footer");
            footer.Add(PriceLabel(ShopText.Yen(item.Price)));

            var buyButton = new Button(() => ItemBuyRequested?.Invoke(item.Id)) { text = "買う" };
            buyButton.AddToClassList("shop-buy-button");
            footer.Add(buyButton);
            row.Add(footer);

            return row;
        }

        private void RenderWholesale()
        {
            if (colony.Animals.Count == 0)
            {
                list.Add(EmptyLabel(CageStatusText.NoAnimalsMessage));
                return;
            }

            foreach (var pet in colony.Animals)
            {
                list.Add(WholesaleRow(pet));
            }
        }

        private VisualElement WholesaleRow(PetState pet)
        {
            var row = new VisualElement();
            row.AddToClassList("shop-row");

            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.AddToClassList("shop-row-header");
            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList("shop-row-thumb");
            var thumbnail = MorphSprites.Thumbnail(pet);
            if (thumbnail != null)
            {
                thumb.style.backgroundImage = new StyleBackground(thumbnail);
            }

            header.Add(thumb);
            header.Add(NameLabel(pet.Name));
            row.Add(header);

            row.Add(DetailLabel(string.Join(" ", CageStatusText.ProfileTokens(pet))));
            var market = shopService.MarketOf(pet);
            var pay = shopService.WholesalePriceOf(pet);
            row.Add(DetailLabel(ShopText.WholesaleLine(market, pay)));

            var footer = new VisualElement();
            footer.AddToClassList("shop-row-footer");
            footer.style.justifyContent = Justify.FlexEnd;
            var sellButton = new Button(() => WholesaleRequested?.Invoke(pet.Id)) { text = "卸す" };
            sellButton.AddToClassList("shop-buy-button");
            footer.Add(sellButton);
            row.Add(footer);

            return row;
        }

        private static Label EmptyLabel(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("shop-empty-label");
            return label;
        }

        private static Label NameLabel(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("shop-row-name-label");
            return label;
        }

        private static Label DetailLabel(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("shop-row-detail-label");
            return label;
        }

        private static Label PriceLabel(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("shop-row-price-label");
            return label;
        }
    }
}
