using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    public enum ShellScreen
    {
        Home,
        Main
    }

    public enum ShellTab
    {
        Cages,
        Incubator,
        Shop,
        Events,
        Ledger
    }

    /// <summary>
    /// Swaps full-screen views (home ↔ main) and the tab panels. Screens are overlays, so
    /// they use display; nothing in-flow is toggled here.
    /// </summary>
    public sealed class ShellNavigator
    {
        private readonly VisualElement home;
        private readonly VisualElement main;
        private readonly VisualElement cageList;
        private readonly VisualElement cageDetail;
        private readonly VisualElement shop;
        private readonly VisualElement ledger;
        private readonly VisualElement placeholder;
        private readonly Label placeholderLabel;
        private readonly Dictionary<ShellTab, Button> tabButtons = new Dictionary<ShellTab, Button>();

        public ShellNavigator(VisualElement root)
        {
            home = root.Q("home-screen");
            main = root.Q("main-screen");
            cageList = root.Q("cage-list-panel");
            cageDetail = root.Q("cage-detail-panel");
            shop = root.Q("shop-panel");
            ledger = root.Q("ledger-panel");
            placeholder = root.Q("placeholder-panel");
            placeholderLabel = root.Q<Label>("placeholder-label");
            Bind(root, "tab-cages", ShellTab.Cages);
            Bind(root, "tab-incubator", ShellTab.Incubator);
            Bind(root, "tab-shop", ShellTab.Shop);
            Bind(root, "tab-events", ShellTab.Events);
            Bind(root, "tab-ledger", ShellTab.Ledger);
            ShowHome();
        }

        public ShellScreen Screen { get; private set; }

        public ShellTab Tab { get; private set; }

        public bool ShowingCageDetail { get; private set; }

        public event Action<ShellTab> TabChanged;

        public static string PlaceholderTextFor(ShellTab tab)
        {
            switch (tab)
            {
                case ShellTab.Incubator:
                    return "孵卵器は段階5で追加されます";
                case ShellTab.Events:
                    return "イベントは段階6で追加されます";
                default:
                    return string.Empty;
            }
        }

        public void ShowHome()
        {
            Screen = ShellScreen.Home;
            SetDisplay(home, true);
            SetDisplay(main, false);
            foreach (var pair in tabButtons)
            {
                pair.Value.RemoveFromClassList("tab-selected");
            }
        }

        public void ShowCageDetail()
        {
            ShowingCageDetail = true;
            ShowTab(ShellTab.Cages);
        }

        public void ShowCageList()
        {
            ShowingCageDetail = false;
            ShowTab(ShellTab.Cages);
        }

        public void ShowTab(ShellTab tab)
        {
            Screen = ShellScreen.Main;
            Tab = tab;
            SetDisplay(home, false);
            SetDisplay(main, true);
            SetDisplay(cageDetail, tab == ShellTab.Cages && ShowingCageDetail);
            SetDisplay(cageList, tab == ShellTab.Cages && !ShowingCageDetail);
            SetDisplay(shop, tab == ShellTab.Shop);
            SetDisplay(ledger, tab == ShellTab.Ledger);
            var hasPanel = tab == ShellTab.Cages || (tab == ShellTab.Shop && shop != null) || (tab == ShellTab.Ledger && ledger != null);
            SetDisplay(placeholder, !hasPanel);
            if (placeholderLabel != null)
            {
                placeholderLabel.text = PlaceholderTextFor(tab);
            }

            foreach (var pair in tabButtons)
            {
                pair.Value.EnableInClassList("tab-selected", pair.Key == tab);
            }

            TabChanged?.Invoke(tab);
        }

        private void Bind(VisualElement root, string name, ShellTab tab)
        {
            var button = root.Q<Button>(name);
            if (button == null)
            {
                return;
            }

            tabButtons[tab] = button;
            button.clicked += () =>
            {
                if (tab == ShellTab.Cages)
                {
                    ShowCageList();
                }
                else
                {
                    ShowTab(tab);
                }
            };
        }

        private static void SetDisplay(VisualElement element, bool visible)
        {
            if (element != null)
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
