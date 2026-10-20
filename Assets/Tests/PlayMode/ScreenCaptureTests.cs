using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TerrariumDays.Tests
{
    /// <summary>
    /// Renders the real Terrarium scene at iPhone 15 Pro resolution into Logs/Screens/*.png
    /// so layout and animation can be checked without a device. Explicit: it only runs when
    /// selected, e.g. scripts/run-unity-tests.sh with
    /// -testFilter TerrariumDays.Tests.ScreenCaptureTests (see scripts/capture-screens.sh).
    /// Safe-area insets are not simulated (the editor reports a full-screen safe area).
    /// </summary>
    [Explicit("Writes screenshots; run on demand via scripts/capture-screens.sh")]
    public sealed class ScreenCaptureTests
    {
        private const int Width = 1179;
        private const int Height = 2556;

        private RenderTexture target;
        private PanelSettings panelSettings;

        [UnityTest]
        public IEnumerator CaptureTerrariumScreens()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);

            yield return new WaitForSeconds(1.5f);
            yield return Capture(outputDir, "00-home");
            var navigator = NavigatorOf(view);
            navigator.ShowCageList();
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "00-cage-list");
            navigator.ShowCageDetail();
            yield return new WaitForSeconds(1.2f);
            yield return Capture(outputDir, "01-idle");

            view.OnCleanClicked();
            yield return new WaitForSeconds(0.45f);
            yield return Capture(outputDir, "02-happy-hearts");

            yield return new WaitForSeconds(2f);
            view.OnFeedClicked();
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "03-eat");

            yield return new WaitForSeconds(2.2f);
            for (var i = 0; i < 4; i++)
            {
                view.OnPetTapped();
                yield return new WaitForSeconds(0.1f);
            }

            yield return new WaitForSeconds(0.2f);
            yield return Capture(outputDir, "04-threat");
        }

        /// <summary>
        /// Captures the home rack and the cage list with a visiting pair, a weak animal and a
        /// cage with eggs into Logs/Screens/home-breeding.png and cage-list-breeding.png, so
        /// the visiting border, the small visitor thumbnail, and the weakness mark can be
        /// checked by eye: they must fit inside the four-column slot without clipped text.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureHomeBreeding()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);
            yield return new WaitForSeconds(1.5f);

            var colony = view.Session.Colony;
            var showcase = StarterGenetics.Showcase;
            colony.RackCount = Mathf.Max(colony.RackCount, Mathf.CeilToInt((colony.Cages.Count + 4) / (float)Colony.CagesPerRack));

            var maleCage = colony.AddCage(CageSize.Standard);
            var male = colony.AddAnimal(new PetState
            {
                Name = "タロウ",
                Sex = Sex.Male,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 55d,
                Genotype = showcase[0].Genotype.Clone(),
                Known = KnownGenetics.Unknown(),
            }, maleCage);

            var femaleCage = colony.AddCage(CageSize.Standard);
            var female = colony.AddAnimal(new PetState
            {
                Name = "ハナ",
                Sex = Sex.Female,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 50d,
                Genotype = showcase[1 % showcase.Count].Genotype.Clone(),
                Known = KnownGenetics.Unknown(),
            }, femaleCage);
            maleCage.VisitorAnimalId = female.Id;
            var pairingNow = view.Session.GameNowUtc;
            colony.Pairings.Add(new Pairing
            {
                Id = colony.NextPairingId++,
                MaleId = male.Id,
                FemaleId = female.Id,
                StartedAtUtc = pairingNow,
                EndsAtUtc = pairingNow.AddDays(3),
            });

            var weakCage = colony.AddCage(CageSize.Standard);
            colony.AddAnimal(new PetState
            {
                Name = "スズ",
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 48d,
                Weak = true,
                Genotype = showcase[2 % showcase.Count].Genotype.Clone(),
                Known = KnownGenetics.Unknown(),
            }, weakCage);

            var eggCage = colony.AddCage(CageSize.Standard);
            eggCage.HasNestBox = true;
            var eggMotherSeason = BreedingRules.SeasonOf(view.Session.Calendar.DateAt(pairingNow), view.Session.Breeding.Care);
            var eggMother = colony.AddAnimal(new PetState
            {
                Name = "モモ",
                Sex = Sex.Female,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 52d,
                Gravid = new GravidState
                {
                    SeasonYear = eggMotherSeason,
                    ClutchesPlanned = 1,
                    NextClutchAtUtc = pairingNow.AddDays(30),
                },
                Genotype = showcase[3 % showcase.Count].Genotype.Clone(),
                Known = KnownGenetics.Unknown(),
            }, eggCage);
            colony.Eggs.Add(new Egg { Id = colony.NextEggId++, MotherId = eggMother.Id, CageId = eggCage.Id, Place = EggPlace.NestBox });

            yield return new WaitForSeconds(1.5f);
            yield return Capture(outputDir, "home-breeding");

            var navigator = NavigatorOf(view);
            navigator.ShowCageList();
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "cage-list-breeding");
        }

        /// <summary>
        /// Captures the cage detail screen for a visiting pair and for a weak, gravid animal
        /// whose eggs sit in the nest box, into Logs/Screens/cage-visiting.png and cage-eggs.png,
        /// so breeding-status-label's wording, weak-red colour and layout (never overlapping the
        /// terrarium or the care buttons, at most three lines) can be checked by eye.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureCageBreeding()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);
            yield return new WaitForSeconds(1.5f);

            var colony = view.Session.Colony;
            var showcase = StarterGenetics.Showcase;
            colony.RackCount = Mathf.Max(colony.RackCount, Mathf.CeilToInt((colony.Cages.Count + 3) / (float)Colony.CagesPerRack));

            var maleCage = colony.AddCage(CageSize.Standard);
            var male = colony.AddAnimal(new PetState
            {
                Name = "タロウ",
                Sex = Sex.Male,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 55d,
                Genotype = showcase[0].Genotype.Clone(),
                Known = KnownGenetics.Unknown(),
            }, maleCage);

            var femaleCage = colony.AddCage(CageSize.Standard);
            var female = colony.AddAnimal(new PetState
            {
                Name = "ハナ",
                Sex = Sex.Female,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 50d,
                Genotype = showcase[1 % showcase.Count].Genotype.Clone(),
                Known = KnownGenetics.Unknown(),
            }, femaleCage);
            maleCage.VisitorAnimalId = female.Id;
            var pairingNow = view.Session.GameNowUtc;
            colony.Pairings.Add(new Pairing
            {
                Id = colony.NextPairingId++,
                MaleId = male.Id,
                FemaleId = female.Id,
                StartedAtUtc = pairingNow,
                EndsAtUtc = pairingNow.AddDays(3),
            });

            var navigator = NavigatorOf(view);
            view.SelectCage(maleCage.Id);
            navigator.ShowCageDetail();
            yield return new WaitForSeconds(1.2f);
            yield return Capture(outputDir, "cage-visiting");

            var eggCage = colony.AddCage(CageSize.Standard);
            eggCage.HasNestBox = true;
            var eggMotherSeason = BreedingRules.SeasonOf(view.Session.Calendar.DateAt(pairingNow), view.Session.Breeding.Care);
            var eggMother = colony.AddAnimal(new PetState
            {
                Name = "モモ",
                Sex = Sex.Female,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 52d,
                Weak = true,
                Gravid = new GravidState
                {
                    SeasonYear = eggMotherSeason,
                    ClutchesPlanned = 1,
                    NextClutchAtUtc = pairingNow.AddDays(30),
                },
                Genotype = showcase[2 % showcase.Count].Genotype.Clone(),
                Known = KnownGenetics.Unknown(),
            }, eggCage);
            colony.Eggs.Add(new Egg { Id = colony.NextEggId++, MotherId = eggMother.Id, CageId = eggCage.Id, Place = EggPlace.NestBox });

            view.SelectCage(eggCage.Id);
            navigator.ShowCageDetail();
            yield return new WaitForSeconds(1.2f);
            yield return Capture(outputDir, "cage-eggs");
        }

        /// <summary>One detail screen per showcase morph (grown, so murphy shows) into Logs/Screens/morph-*.png.</summary>
        [UnityTest]
        public IEnumerator CaptureMorphGallery()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);
            yield return new WaitForSeconds(1.5f);

            var colony = view.Session.Colony;
            var neededCages = colony.Cages.Count + StarterGenetics.Showcase.Count;
            colony.RackCount = Mathf.Max(colony.RackCount, Mathf.CeilToInt(neededCages / (float)Colony.CagesPerRack));

            var navigator = NavigatorOf(view);
            for (var i = 0; i < StarterGenetics.Showcase.Count; i++)
            {
                var showcase = StarterGenetics.Showcase[i];
                var cage = colony.AddCage(CageSize.Standard);
                var pet = new PetState
                {
                    Name = showcase.Label,
                    Stage = GrowthStage.Adult,
                    WeightGrams = 50d,
                    SexRevealed = true,
                    Genotype = showcase.Genotype.Clone(),
                    Known = KnownGenetics.Unknown(),
                };
                colony.AddAnimal(pet, cage);

                view.SelectCage(cage.Id);
                navigator.ShowCageDetail();
                yield return new WaitForSeconds(1f);
                yield return Capture(outputDir, $"morph-{i:00}");
            }
        }

        /// <summary>
        /// Fills a large cage's three decor slots (two floor pieces at different depths plus
        /// the hanging heat lamp) and captures it into Logs/Screens/decor-slots.png, so the
        /// slot placement (TerrariumArtLayout.DecorSlotSpots) can be checked by eye: the floor
        /// pieces must not overlap and the gecko must be sleeping right beside one of them.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureDecorSlots()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);
            yield return new WaitForSeconds(1.5f);

            var colony = view.Session.Colony;
            var cage = colony.Cages[0];
            cage.Size = CageSize.Large;
            cage.DecorIds.Clear();
            cage.DecorIds.Add("rock_01");
            cage.DecorIds.Add("driftwood_01");
            cage.DecorIds.Add("heat_lamp_01");

            var navigator = NavigatorOf(view);
            view.SelectCage(cage.Id);
            navigator.ShowCageDetail();
            yield return new WaitForSeconds(1.5f);
            yield return Capture(outputDir, "decor-slots");
        }

        /// <summary>
        /// Captures the shop tab's three sections and its confirmation dialog into
        /// Logs/Screens/shop-*.png: rows must fit the screen width, long morph names wrap by
        /// word, and the buy/confirm buttons are tap-sized.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureShopScreens()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);
            yield return new WaitForSeconds(1.5f);

            var colony = view.Session.Colony;
            colony.Wallet.Money = 1_000_000;
            var secondCage = colony.AddCage(CageSize.Standard);
            colony.AddAnimal(new PetState
            {
                Name = "ふたり目",
                Stage = GrowthStage.Adult,
                WeightGrams = 55d,
                SexRevealed = true,
                Genotype = StarterGenetics.Showcase[StarterGenetics.Showcase.Count - 1].Genotype.Clone(),
                Known = KnownGenetics.Unknown(),
            }, secondCage);

            var navigator = NavigatorOf(view);
            navigator.ShowTab(ShellTab.Shop);
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "shop-animals");

            var document = view.GetComponent<UIDocument>();
            ClickNamed(document, "shop-section-supplies");
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "shop-supplies");

            ClickNamed(document, "shop-section-wholesale");
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "shop-wholesale");

            // Tap a real "卸す" row button, exercising the actual Awake-wired event path into
            // TerrariumView so the confirmation shown is the real one, not a hand-built stand-in.
            var wholesaleButton = document.rootVisualElement.Q<Button>(className: "shop-buy-button");
            Assert.That(wholesaleButton, Is.Not.Null, "expected at least one wholesale row");
            FindClickAction(wholesaleButton.clickable)();
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "shop-confirm-dialog");
        }

        /// <summary>
        /// Captures the ledger tab's two sections into Logs/Screens/ledger-*.png: the money
        /// figures must line up on the right and a long ledger note must wrap rather than run
        /// off the screen.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureLedgerScreens()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);
            yield return new WaitForSeconds(1.5f);

            var colony = view.Session.Colony;
            colony.Wallet.Money = 1_000_000;
            var secondCage = colony.AddCage(CageSize.Standard);
            colony.AddAnimal(new PetState
            {
                Name = "ふたり目",
                Stage = GrowthStage.Adult,
                WeightGrams = 55d,
                SexRevealed = true,
                Sex = Sex.Female,
                Genotype = StarterGenetics.Showcase[StarterGenetics.Showcase.Count - 1].Genotype.Clone(),
                Known = KnownGenetics.Unknown(),
            }, secondCage);

            // A long note so wrapping (not overflow) can be checked.
            colony.Wallet.Charge(1200, LedgerCategory.Purchase, "観葉植物と流木と小型ケージをまとめて購入しました", view.Session.GameNowUtc);

            var navigator = NavigatorOf(view);
            navigator.ShowTab(ShellTab.Ledger);
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "ledger-animals");

            var document = view.GetComponent<UIDocument>();
            ClickNamed(document, "ledger-section-money");
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "ledger-money");
        }

        /// <summary>
        /// Captures the empty incubator tab and the family tree modal (unknown boxes only)
        /// into Logs/Screens/incubator-empty.png and family-tree-empty.png (Task 14), so the
        /// frames, margins and headings can be checked before the cards and boxes are filled.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureIncubatorAndFamilyTreeEmpty()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);
            yield return new WaitForSeconds(1.5f);

            NavigatorOf(view).ShowTab(ShellTab.Incubator);
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "incubator-empty");

            var root = view.GetComponent<UIDocument>().rootVisualElement;

            root.Q<Label>("family-tree-title").text = "家系図";
            foreach (var name in new[] { "mgm", "mgf", "pgm", "pgf", "mother", "father", "self" })
            {
                var box = root.Q("family-tree-" + name);
                box.AddToClassList("family-box--unknown");
                box.Add(new Label("不明"));
            }

            root.Q("family-tree-modal").style.display = DisplayStyle.Flex;
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "family-tree-empty");
            root.Q("family-tree-modal").style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Captures the incubator tab with three debug eggs in the starter incubator into
        /// Logs/Screens/incubator-cards.png (Task 15) to check picture, bar and buttons fit.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureIncubatorCards()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);
            yield return new WaitForSeconds(1.5f);

            view.Session.Colony.Eggs.Clear();
            view.Session.Incubation.AddDebugEggs(view.Session.Colony, view.Session.GameNowUtc, 3);
            NavigatorOf(view).ShowTab(ShellTab.Incubator);
            yield return new WaitForSeconds(0.5f);
            yield return Capture(outputDir, "incubator-cards");
        }

        /// <summary>
        /// Captures the incubator tab with two hatchlings in the tray, one in care grace and
        /// one past it, into Logs/Screens/hatch-tray.png (Task 16).
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureHatchTray()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);
            yield return new WaitForSeconds(1.5f);

            var session = view.Session;
            var hatchery = new Hatchery(new CareTuning());
            session.Colony.Eggs.Clear();
            foreach (var leftover in session.Colony.TrayAnimals())
            {
                session.Colony.RemoveAnimal(leftover);
            }

            session.Incubation.AddDebugEggs(session.Colony, session.GameNowUtc, 2);
            hatchery.Hatch(session.Colony, session.Colony.Eggs[0], session.GameNowUtc, 28d);
            var expired = hatchery.Hatch(session.Colony, session.Colony.Eggs[0], session.GameNowUtc, 28d);
            expired.CareGraceUntilUtc = session.GameNowUtc - System.TimeSpan.FromHours(1);
            foreach (var cage in session.Colony.Cages.FindAll(c => c.IsEmpty))
            {
                session.Colony.AddAnimal(new PetState { Name = "満室" }, cage);
            }

            NavigatorOf(view).ShowTab(ShellTab.Incubator);
            yield return new WaitForSeconds(0.5f);
            yield return Capture(outputDir, "hatch-tray");
        }

        /// <summary>
        /// Captures the cage detail with two nest-box eggs (showing "孵卵器へ移す（全部）") into
        /// Logs/Screens/cage-detail-eggs.png, then the home screen with the incubator counts
        /// into Logs/Screens/home-incubator-counts.png (Task 17).
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureCageEggsAndHomeCounts()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);
            yield return new WaitForSeconds(1.5f);

            var session = view.Session;
            var cage = view.CurrentCage;
            session.Colony.Eggs.Clear();
            cage.HasNestBox = true;
            session.Colony.Eggs.Add(new Egg { Id = 1, CageId = cage.Id, Place = EggPlace.NestBox, Fertile = true, LaidAtUtc = session.GameNowUtc });
            session.Colony.Eggs.Add(new Egg { Id = 2, CageId = cage.Id, Place = EggPlace.NestBox, Fertile = true, LaidAtUtc = session.GameNowUtc });
            NavigatorOf(view).ShowCageDetail();
            view.Render(view.State, view.Tuning);
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "cage-detail-eggs");

            view.OnMoveEggsButtonClicked();
            NavigatorOf(view).ShowHome();
            yield return new WaitForSeconds(1.5f);
            yield return Capture(outputDir, "home-incubator-counts");
        }

        /// <summary>
        /// Captures the ledger with a parents line per row and a three-generation family tree
        /// (grandparents unknown) into Logs/Screens/ledger-parents.png and family-tree.png (Task 18).
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureLedgerParentsAndFamilyTree()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);
            yield return new WaitForSeconds(1.5f);

            var colony = view.Session.Colony;
            colony.RackCount = Mathf.Max(colony.RackCount, Mathf.CeilToInt((colony.Cages.Count + 4) / (float)Colony.CagesPerRack));
            var genotype = StarterGenetics.Showcase[StarterGenetics.Showcase.Count - 1].Genotype;
            PetState Pet(string name, Sex sex, int mother = 0, int father = 0) => new PetState
            {
                Name = name,
                Stage = GrowthStage.Adult,
                WeightGrams = 55d,
                SexRevealed = true,
                Sex = sex,
                Genotype = genotype.Clone(),
                Known = KnownGenetics.Unknown(),
                MotherId = mother,
                FatherId = father,
            };

            var mother = colony.AddAnimal(Pet("ハナ", Sex.Female), colony.AddCage(CageSize.Standard));
            var father = colony.AddAnimal(Pet("タロウ", Sex.Male), colony.AddCage(CageSize.Standard));
            var child = colony.AddAnimal(Pet("コハナ", Sex.Female, mother.Id, father.Id), colony.AddCage(CageSize.Standard));
            child.SexRevealed = false;

            NavigatorOf(view).ShowTab(ShellTab.Ledger);
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "ledger-parents");

            view.OnLedgerAnimalTapped(child.Id);
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "family-tree");
        }

        /// <summary>
        /// Captures the empty breeding tab into Logs/Screens/breeding-empty.png so the tab
        /// bar's now-six buttons (each label unclipped, at least 44px tall) can be checked by
        /// eye alongside the placeholder-panel skeleton (Task 12; Task 13 fills the panel).
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureBreedingEmpty()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);
            yield return new WaitForSeconds(1.5f);

            var navigator = NavigatorOf(view);
            navigator.ShowTab(ShellTab.Breeding);
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "breeding-empty");
        }

        /// <summary>
        /// Captures the breeding tab with a pair selected and the forecast showing, and with an
        /// ongoing pairing, a gravid female and a cage with eggs, into Logs/Screens/breeding-
        /// forecast.png and breeding-ongoing.png (Task 13), so the reason labels (right of each
        /// row) and the forecast lines can be checked by eye: neither should clip or overflow.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureBreedingTab()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);
            yield return new WaitForSeconds(1.5f);

            var colony = view.Session.Colony;
            var showcase = StarterGenetics.Showcase;
            colony.RackCount = Mathf.Max(colony.RackCount, Mathf.CeilToInt((colony.Cages.Count + 6) / (float)Colony.CagesPerRack));

            var maleCage = colony.AddCage(CageSize.Standard);
            var male = colony.AddAnimal(new PetState
            {
                Name = "タロウ",
                Sex = Sex.Male,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 55d,
                Genotype = showcase[0].Genotype.Clone(),
                Known = KnownGenetics.Unknown(),
            }, maleCage);

            var femaleCage = colony.AddCage(CageSize.Standard);
            var female = colony.AddAnimal(new PetState
            {
                Name = "ハナ",
                Sex = Sex.Female,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 50d,
                Genotype = showcase[1 % showcase.Count].Genotype.Clone(),
                Known = KnownGenetics.Unknown(),
            }, femaleCage);
            maleCage.VisitorAnimalId = female.Id;
            var pairingNow = view.Session.GameNowUtc;
            colony.Pairings.Add(new Pairing
            {
                Id = colony.NextPairingId++,
                MaleId = male.Id,
                FemaleId = female.Id,
                StartedAtUtc = pairingNow,
                EndsAtUtc = pairingNow.AddDays(3),
            });

            var eggCage = colony.AddCage(CageSize.Standard);
            eggCage.HasNestBox = true;
            var eggMotherSeason = BreedingRules.SeasonOf(view.Session.Calendar.DateAt(pairingNow), view.Session.Breeding.Care);
            var eggMother = colony.AddAnimal(new PetState
            {
                Name = "モモ",
                Sex = Sex.Female,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 52d,
                Gravid = new GravidState
                {
                    SeasonYear = eggMotherSeason,
                    ClutchesPlanned = 1,
                    NextClutchAtUtc = pairingNow.AddDays(30),
                },
                Genotype = showcase[2 % showcase.Count].Genotype.Clone(),
                Known = KnownGenetics.Unknown(),
            }, eggCage);
            colony.Eggs.Add(new Egg { Id = colony.NextEggId++, MotherId = eggMother.Id, CageId = eggCage.Id, Place = EggPlace.NestBox });

            // A second, unpaired pair to select for the forecast screenshot. HatchedAtUtc must
            // be well in the past (in real time, compressed 48x into game time) or CheckCandidate
            // blocks them as TooYoung.
            var hatchedAt = view.Session.GameNowUtc - System.TimeSpan.FromDays(12);
            var secondMaleCage = colony.AddCage(CageSize.Standard);
            var secondMale = colony.AddAnimal(new PetState
            {
                Name = "ジロウ",
                Sex = Sex.Male,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 55d,
                HatchedAtUtc = hatchedAt,
                Genotype = showcase[3 % showcase.Count].Genotype.Clone(),
                Known = KnownGenetics.Unknown(),
            }, secondMaleCage);

            var secondFemaleCage = colony.AddCage(CageSize.Standard);
            var secondFemale = colony.AddAnimal(new PetState
            {
                Name = "サキ",
                Sex = Sex.Female,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 50d,
                HatchedAtUtc = hatchedAt,
                Genotype = showcase[4 % showcase.Count].Genotype.Clone(),
                Known = KnownGenetics.Unknown(),
            }, secondFemaleCage);

            var navigator = NavigatorOf(view);
            navigator.ShowTab(ShellTab.Breeding);
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "breeding-ongoing");

            var document = view.GetComponent<UIDocument>();
            ClickNamed(document, $"breeding-candidate-{secondFemale.Id}");
            ClickNamed(document, $"breeding-candidate-{secondMale.Id}");
            yield return new WaitForSeconds(0.3f);

            var breedingList = document.rootVisualElement.Q<ScrollView>("breeding-list");
            var startButton = document.rootVisualElement.Q<Button>("breeding-start-button");
            breedingList.ScrollTo(startButton);
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "breeding-forecast");
        }

        [TearDown]
        public void TearDown()
        {
            if (panelSettings != null)
            {
                panelSettings.targetTexture = null;
            }

            if (target != null)
            {
                target.Release();
                Object.Destroy(target);
            }
        }

        /// <summary>Loads the Terrarium scene, routes its panel to an offscreen RenderTexture and ensures Logs/Screens exists.</summary>
        private IEnumerator SetupScene(System.Action<TerrariumView> onView, System.Action<string> onOutputDir)
        {
            SceneManager.LoadScene("Terrarium");
            yield return null;

            var document = Object.FindFirstObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null);
            onView(document.GetComponent<TerrariumView>());

            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            panelSettings = document.panelSettings;
            panelSettings.targetTexture = target;

            var outputDir = Path.Combine(Application.dataPath, "..", "Logs", "Screens");
            Directory.CreateDirectory(outputDir);
            onOutputDir(outputDir);
        }

        private static ShellNavigator NavigatorOf(TerrariumView view)
        {
            var navigatorField = typeof(TerrariumView).GetField("navigator", BindingFlags.NonPublic | BindingFlags.Instance);
            return (ShellNavigator)navigatorField.GetValue(view);
        }

        /// <summary>
        /// Finds a named Button anywhere in the document and invokes its wired click action.
        /// UI Toolkit queues pointer-driven clicks and only pumps them on a real update tick
        /// that a coroutine-driven PlayMode test does not reliably get within one frame, so the
        /// Clickable delegate is invoked directly instead (same technique as ShellTests).
        /// </summary>
        private static void ClickNamed(UIDocument document, string buttonName)
        {
            var button = document.rootVisualElement.Q<Button>(buttonName);
            Assert.That(button, Is.Not.Null, buttonName);
            var action = FindClickAction(button.clickable);
            Assert.That(action, Is.Not.Null, $"{buttonName} has no click action wired up");
            action();
        }

        private static System.Action FindClickAction(Clickable clickable)
        {
            foreach (var field in typeof(Clickable).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.GetValue(clickable) is System.Action action)
                {
                    return action;
                }
            }

            return null;
        }

        /// <summary>
        /// Captures the three event surfaces and the buy confirmation with stand-in text into
        /// Logs/Screens/event-*.png so the frame layout can be checked before the screens are filled.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureEventFrames()
        {
            TerrariumView view = null;
            string outputDir = null;
            yield return SetupScene(v => view = v, dir => outputDir = dir);
            yield return new WaitForSeconds(1.5f);

            var root = Object.FindFirstObjectByType<UIDocument>().rootVisualElement;
            NavigatorOf(view).ShowTab(ShellTab.Events);

            void Show(string surface)
            {
                foreach (var n in new[] { "event-prep", "event-day", "event-result" })
                {
                    root.Q(n).style.display = n == surface ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            void Text(string n, string text) => root.Q<Label>(n).text = text;

            void Row(VisualElement parent, string cls, string text, params string[] buttons)
            {
                var row = new VisualElement();
                row.AddToClassList(cls);
                parent.Add(row);
                var label = new Label(text);
                label.AddToClassList(cls == "event-offer-row" ? "event-offer-label" : "event-listing-name");
                row.Add(label);
                foreach (var b in buttons)
                {
                    var button = new Button { text = b };
                    button.AddToClassList(cls == "event-offer-row" ? "event-offer-buy" : "event-listing-plus");
                    row.Add(button);
                }
            }

            Show("event-prep");
            Text("event-prep-title", "第7回 春のレプタイルフェア 4月15日");
            Text("event-prep-info", "出店料 10,000円 / 出品は最大10匹 / 締め切り 4月14日");
            var prepList = root.Q<ScrollView>("event-prep-list");
            for (var i = 0; i < 6; i++)
            {
                Row(prepList, "event-listing-row", "ヒョウモントカゲモドキ 28,500円 売れる確率 62%", "+");
            }

            Text("event-prep-total", "期待値の合計 84,000円");
            Text("event-prep-count", "出品 2/10匹");
            Text("event-prep-message", "出店料 10,000円を払います");
            yield return Capture(outputDir, "event-prep");

            Show("event-day");
            Text("event-day-title", "第7回 春のレプタイルフェア");
            Text("event-day-left", "残りの行動 3/8");
            var offers = root.Q("event-day-offers");
            Row(offers, "event-offer-row", "ノーマル 12,000円", "購入");
            Row(offers, "event-offer-row", "タンジェリン 30,000円", "購入");
            var log = root.Q<ScrollView>("event-day-log");
            for (var i = 0; i < 6; i++)
            {
                log.Add(new Label("接客 " + (i + 1) + ": ヒョウモンさんが27,000円で買いました"));
            }

            yield return Capture(outputDir, "event-day");

            Show("event-result");
            Text("event-result-title", "第7回 春のレプタイルフェア 結果");
            root.Q("event-result-sold").Add(new Label("ヒョウモン 27,000円"));
            root.Q("event-result-unsold").Add(new Label("コーン 売れ残り"));
            root.Q("event-result-bought").Add(new Label("ノーマル 12,000円"));
            Text("event-result-totals", "売上 27,000円 / 仕入れ 12,000円 / 出店料 10,000円 / 差し引き 5,000円");
            yield return Capture(outputDir, "event-result");

            Text("event-buy-message", "ノーマルを12,000円で買いますか");
            Text("event-buy-reason", "ケージが足りません");
            root.Q<Button>("event-buy-yes-button").SetEnabled(false);
            root.Q("event-buy-modal").style.display = DisplayStyle.Flex;
            yield return Capture(outputDir, "event-buy-modal");
        }

        private IEnumerator Capture(string outputDir, string name)
        {
            yield return null;
            yield return null;

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(Path.Combine(outputDir, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
