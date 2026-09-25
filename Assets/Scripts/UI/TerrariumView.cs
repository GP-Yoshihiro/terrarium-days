using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    /// <summary>
    /// Binds a PetState/CareTuning snapshot onto the Terrarium screen's VisualElements and
    /// forwards the three care-button taps to CareService. Owns no elapsed-time or care
    /// decision logic itself; it only calls the services and displays their result.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TerrariumView : MonoBehaviour
    {
        private const float FeedbackDurationSeconds = 1.5f;
        private const float MaxLiveTickFrameSeconds = 0.25f;
        private const string FeedSuccessMessage = "ごはんを食べた！";
        private const string RefreshWaterSuccessMessage = "新しいお水にした！";
        private const string CleanSuccessMessage = "テラリウムがきれい！";
        private const string SaveFileName = "terrarium-save.json";
        private const string ShedMessage = "脱皮した！きれいな体になったよ";
        private const string RefusePreShedMessage = "脱皮が近くて食欲がないみたい…";
        private const string RefusePreGrowthMessage = "成長前で食欲がないみたい…";
        private const float ClockRefreshSeconds = 1f;

        private PetState state;
        private CareTuning tuning;
        private CareService careService;
        private TimeService timeService;
        private readonly EconomyTuning economyTuning = new EconomyTuning();
        private ColonySession session;
        private ColonyCareService colonyCare;
        private Cage currentCage;

        public ColonySession Session => session;

        public Cage CurrentCage => currentCage;

        public event Action ColonyChanged;

        private Label growthStageLabel;
        private VisualElement growthGaugeFill;

        private VisualElement hungerBarFill;
        private Label hungerValueLabel;
        private VisualElement hydrationBarFill;
        private Label hydrationValueLabel;
        private VisualElement cleanlinessBarFill;
        private Label cleanlinessValueLabel;
        private VisualElement healthBarFill;
        private Label healthValueLabel;

        private Label feedbackLabel;
        private Button feedButton;
        private Button waterButton;
        private Button cleanButton;
        private Coroutine feedbackHideCoroutine;

        private VisualElement milestoneModal;
        private Label milestoneStageLabel;
        private Label milestoneMessageLabel;
        private Button milestoneContinueButton;

        private VisualElement decorImageElement;
        private Button decorButton;
        private VisualElement decorDrawer;
        private Button decorDrawerCloseButton;
        private readonly Dictionary<string, Button> decorRowButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Label> decorStatusLabels = new Dictionary<string, Label>();
        private readonly Dictionary<string, Action> decorRowClickHandlers = new Dictionary<string, Action>();
        private string appliedDecorIconClass;

        private VisualElement topBar;
        private Button debugButton;
        private VisualElement debugPanel;
        private Button debugCloseButton;
        private Button debug1xButton;
        private Button debug60xButton;
        private Button debug600xButton;
        private Button debugSimulate12hButton;
        private Button debugClearSaveButton;
        private Button debugAddCageButton;
        private Button debugAddPetButton;
        private Label debugAppliedElapsedLabel;
        private Slider debugHungerSlider;
        private Slider debugHydrationSlider;
        private Slider debugCleanlinessSlider;
        private Slider debugHealthSlider;
        private Slider debugWeightSlider;
        private Coroutine liveTickCoroutine;
        private TimeSpan lastAppliedElapsed;
        private EventCallback<ChangeEvent<float>> debugHungerSliderCallback;
        private EventCallback<ChangeEvent<float>> debugHydrationSliderCallback;
        private EventCallback<ChangeEvent<float>> debugCleanlinessSliderCallback;
        private EventCallback<ChangeEvent<float>> debugHealthSliderCallback;
        private EventCallback<ChangeEvent<float>> debugWeightSliderCallback;
        private readonly PetBehaviourTuning petBehaviourTuning = new PetBehaviourTuning();
        private readonly TerrariumArtLayout artLayout = new TerrariumArtLayout();
        private VisualElement terrariumViewElement;
        private VisualElement terrariumBackgroundElement;
        private TerrariumProjection terrariumProjection;
        private VisualElement petElement;
        private PetActor petActor;
        private VisualElement effectsLayerElement;
        private VisualElement lightingElement;
        private Label dayPhaseLabel;
        private Label weatherLabel;
        private readonly WeatherService weatherService = new WeatherService();
        private float clockTimer;
        private TerrariumDrawOrder drawOrder;
        private VisualElement safeAreaRoot;
        private Rect appliedSafeArea;
        private Vector2 appliedPanelSize;

        private ShellNavigator navigator;
        private HomeView homeView;
        private CageListView cageListView;
        private Label moneyLabel;
        private Label gameDateLabel;
        private Label cageTitleLabel;
        private const float SwipeThresholdPixels = 60f;
        private Vector2? swipeStartPosition;
        private bool suppressNextPetTap;

        public PetState State => state;

        public CareTuning Tuning => tuning;

        private void Awake()
        {
            if (state != null)
            {
                // Already initialized directly (e.g. by a PlayMode test driving this
                // instance via BindElements/Initialize before activating the GameObject
                // so StartCoroutine works) — do not clobber it by rebinding against a
                // real but unconfigured UIDocument.
                return;
            }

            var document = GetComponent<UIDocument>();
            BindElements(document.rootVisualElement);

            var root = document.rootVisualElement;
            navigator = new ShellNavigator(root);
            var sprites = PetSpriteLibrary.LoadFromResources();
            homeView = new HomeView(root.Q("rack-list"), sprites);
            cageListView = new CageListView(root.Q("cage-list"), sprites);
            moneyLabel = root.Q<Label>("money-label");
            gameDateLabel = root.Q<Label>("game-date-label");
            cageTitleLabel = root.Q<Label>("cage-title-label");
            homeView.CageSelected += id => { SelectCage(id); navigator.ShowCageDetail(); };
            cageListView.CageSelected += id => { SelectCage(id); navigator.ShowCageDetail(); };
            root.Q<Button>("home-button").clicked += () =>
            {
                homeView.Invalidate();
                navigator.ShowHome();
                RefreshShell();
            };
            root.Q<Button>("prev-cage-button").clicked += () => ShowCageStep(-1);
            root.Q<Button>("next-cage-button").clicked += () => ShowCageStep(1);
            root.Q<Button>("feed-all-button").clicked += OnFeedAllClicked;
            root.Q<Button>("water-all-button").clicked += OnWaterAllClicked;
            root.Q<Button>("clean-all-button").clicked += OnCleanAllClicked;
            ColonyChanged += RefreshShell;
            navigator.TabChanged += tab =>
            {
                if (tab == ShellTab.Cages && !navigator.ShowingCageDetail)
                {
                    cageListView.Invalidate();
                }

                RefreshShell();
            };

            terrariumViewElement?.RegisterCallback<PointerDownEvent>(OnTerrariumPointerDown);
            terrariumViewElement?.RegisterCallback<PointerUpEvent>(OnTerrariumPointerUp);

            safeAreaRoot = document.rootVisualElement.Q<VisualElement>("root");
            safeAreaRoot?.RegisterCallback<GeometryChangedEvent>(OnSafeAreaRootGeometryChanged);

            if (feedButton != null)
            {
                feedButton.clicked += OnFeedClicked;
            }

            if (waterButton != null)
            {
                waterButton.clicked += OnRefreshWaterClicked;
            }

            if (cleanButton != null)
            {
                cleanButton.clicked += OnCleanClicked;
            }

            if (milestoneContinueButton != null)
            {
                milestoneContinueButton.clicked += OnMilestoneContinueClicked;
            }

            if (decorButton != null)
            {
                decorButton.clicked += OnDecorButtonClicked;
            }

            if (decorDrawerCloseButton != null)
            {
                decorDrawerCloseButton.clicked += OnDecorDrawerCloseClicked;
            }

            foreach (var pair in decorRowButtons)
            {
                var decorId = pair.Key;
                Action handler = () => OnDecorRowClicked(decorId);
                decorRowClickHandlers[decorId] = handler;
                pair.Value.clicked += handler;
            }

            if (debugButton != null)
            {
                debugButton.clicked += OnDebugButtonClicked;
            }

            if (debugCloseButton != null)
            {
                debugCloseButton.clicked += OnDebugCloseClicked;
            }

            if (debug1xButton != null)
            {
                debug1xButton.clicked += OnDebug1xClicked;
            }

            if (debug60xButton != null)
            {
                debug60xButton.clicked += OnDebug60xClicked;
            }

            if (debug600xButton != null)
            {
                debug600xButton.clicked += OnDebug600xClicked;
            }

            if (debugSimulate12hButton != null)
            {
                debugSimulate12hButton.clicked += OnDebugSimulate12HoursClicked;
            }

            if (debugClearSaveButton != null)
            {
                debugClearSaveButton.clicked += OnDebugClearSaveClicked;
            }

            if (debugAddCageButton != null)
            {
                debugAddCageButton.clicked += OnDebugAddCageClicked;
            }

            if (debugAddPetButton != null)
            {
                debugAddPetButton.clicked += OnDebugAddPetClicked;
            }

            debugHungerSliderCallback = evt => OnDebugHungerChanged(evt.newValue);
            debugHydrationSliderCallback = evt => OnDebugHydrationChanged(evt.newValue);
            debugCleanlinessSliderCallback = evt => OnDebugCleanlinessChanged(evt.newValue);
            debugHealthSliderCallback = evt => OnDebugHealthChanged(evt.newValue);
            debugWeightSliderCallback = evt => OnDebugWeightChanged(evt.newValue);
            debugHungerSlider?.RegisterValueChangedCallback(debugHungerSliderCallback);
            debugHydrationSlider?.RegisterValueChangedCallback(debugHydrationSliderCallback);
            debugCleanlinessSlider?.RegisterValueChangedCallback(debugCleanlinessSliderCallback);
            debugHealthSlider?.RegisterValueChangedCallback(debugHealthSliderCallback);
            debugWeightSlider?.RegisterValueChangedCallback(debugWeightSliderCallback);

            ApplyDebugVisibility(Debug.isDebugBuild);

            var resolvedSavePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            LoadColony(resolvedSavePath, DateTimeOffset.UtcNow, new TimeService());

            // Status keeps changing while the app is open, not only across launches.
            StartLiveTickIfNeeded();

            UpdateClock(DateTime.Now);
            StartCoroutine(weatherService.Run(OnWeatherText));
        }

        /// <summary>
        /// Sets the live state this view mutates on care-button taps and paints it.
        /// Separate from Awake so tests can drive it directly without a real UIDocument.
        /// </summary>
        public void Initialize(PetState initialState, CareTuning initialTuning)
        {
            state = initialState;
            tuning = initialTuning;
            careService = new CareService(tuning);

            if (feedbackLabel != null)
            {
                // The label keeps its reserved height; only its visibility toggles, so a
                // message never shifts the terrarium or the buttons.
                feedbackLabel.style.visibility = Visibility.Hidden;
            }

            if (milestoneModal != null)
            {
                milestoneModal.style.display = DisplayStyle.None;
            }

            if (decorDrawer != null)
            {
                decorDrawer.style.display = DisplayStyle.None;
            }

            if (debugPanel != null)
            {
                debugPanel.style.display = DisplayStyle.None;
            }

            Render(state, tuning);
        }

        /// <summary>
        /// Loads (or creates/migrates) the colony, applies the time away to every animal and
        /// shows the first occupied cage. Public so tests can drive it without a UIDocument.
        /// </summary>
        public void LoadColony(string atSavePath, DateTimeOffset nowUtc, TimeService clock = null)
        {
            tuning = new CareTuning();
            timeService = clock ?? new TimeService(() => nowUtc);
            session = new ColonySession(atSavePath, timeService, tuning, economyTuning, new System.Random());
            colonyCare = new ColonyCareService(tuning, economyTuning);
            careService = new CareService(tuning);
            var report = session.Load();
            var first = session.Colony.OccupiedCages();
            SelectCage(first.Count > 0 ? first[0].Id : session.Colony.Cages[0].Id);
            HandleReport(report);
            if (session.Migrated)
            {
                ShowFeedback("データを新しい形式に移しました（ケージ1）");
            }
        }

        public void SelectCage(int cageId)
        {
            currentCage = session.Colony.Cages.Find(c => c.Id == cageId);
            var pet = session.Colony.AnimalIn(currentCage);
            if (pet == null)
            {
                return;
            }

            Initialize(pet, tuning);
            RebuildPetActor();
            ColonyChanged?.Invoke();
        }

        public void ShowCageStep(int delta)
        {
            var occupied = session.Colony.OccupiedCages();
            if (occupied.Count == 0)
            {
                return;
            }

            var index = Math.Max(0, occupied.IndexOf(currentCage));
            var next = ((index + delta) % occupied.Count + occupied.Count) % occupied.Count;
            SelectCage(occupied[next].Id);
        }

        private void HandleReport(ColonyTickReport report)
        {
            if (report.AppliedElapsed > TimeSpan.Zero)
            {
                lastAppliedElapsed = report.AppliedElapsed;
                UpdateDebugAppliedElapsedLabel();
            }

            foreach (var (pet, stage) in report.StageUps)
            {
                DecorUnlockService.GrantUnlocksForStage(pet, stage);
                if (pet == state)
                {
                    ShowMilestoneModal(stage);
                }
            }

            if (report.Sheds.Contains(state))
            {
                ShowFeedback(ShedMessage);
            }

            if (state != null && (report.AppliedElapsed > TimeSpan.Zero || report.HasEvents))
            {
                Render(state, tuning);
            }

            if (report.HasEvents)
            {
                ColonyChanged?.Invoke();
            }
        }

        public void OnFeedAllClicked() => OnBulkCare(() => colonyCare.FeedAll(session.Colony, GameNowUtc()).ToMessage());

        public void OnWaterAllClicked() => OnBulkCare(() => $"{colonyCare.RefreshWaterAll(session.Colony)}匹の水を替えました");

        public void OnCleanAllClicked() => OnBulkCare(() => $"{colonyCare.CleanAll(session.Colony)}ケージを掃除しました");

        private void OnBulkCare(Func<string> action)
        {
            if (session == null)
            {
                return;
            }

            ShowFeedback(action());
            Render(state, tuning);
            SaveCurrentState();
            ColonyChanged?.Invoke();
        }

        /// <summary>
        /// Rebuilds the pet actor (walk position, mood, etc.) for the currently selected
        /// cage's animal. Extracted from BindElements so switching cages gets a fresh actor
        /// without rebuilding the rest of the visual tree.
        /// </summary>
        private void RebuildPetActor()
        {
            petActor?.Effects.Clear();
            petActor = null;
            if (petElement != null)
            {
                petActor = new PetActor(petElement, petBehaviourTuning, artLayout, new System.Random(),
                    PetSpriteLibrary.LoadFromResources(), effectsLayerElement ?? terrariumViewElement);
            }

            if (terrariumProjection != null)
            {
                petActor?.SetViewSize(terrariumProjection.ViewWidth, terrariumProjection.ViewHeight);
            }

            PlaceDecor();
        }

        private void Update()
        {
            petActor?.Tick(Time.deltaTime);
            drawOrder?.Apply();

            clockTimer -= Time.unscaledDeltaTime;
            if (clockTimer <= 0f)
            {
                clockTimer = ClockRefreshSeconds;
                UpdateClock(DateTime.Now);
            }

            if (safeAreaRoot != null && Screen.safeArea != appliedSafeArea)
            {
                ApplySafeArea();
            }
        }

        private void OnSafeAreaRootGeometryChanged(GeometryChangedEvent evt)
        {
            ApplySafeArea();
        }

        /// <summary>
        /// Pads the screen root by Screen.safeArea so the top bar clears the Dynamic Island
        /// and the care buttons clear the home indicator. The root keeps its full-screen
        /// background so the padded strips are not left black.
        /// </summary>
        private void ApplySafeArea()
        {
            if (safeAreaRoot?.panel == null)
            {
                return;
            }

            var panelSize = safeAreaRoot.panel.visualTree.layout.size;
            if (float.IsNaN(panelSize.x) || panelSize.x <= 0f || panelSize.y <= 0f)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            if (safeArea == appliedSafeArea && panelSize == appliedPanelSize)
            {
                return;
            }

            appliedSafeArea = safeArea;
            appliedPanelSize = panelSize;
            var insets = SafeAreaInsets.Compute(safeArea, new Vector2(Screen.width, Screen.height), panelSize);
            safeAreaRoot.style.paddingLeft = insets.Left;
            safeAreaRoot.style.paddingTop = insets.Top;
            safeAreaRoot.style.paddingRight = insets.Right;
            safeAreaRoot.style.paddingBottom = insets.Bottom;
        }

        private void OnDestroy()
        {
            safeAreaRoot?.UnregisterCallback<GeometryChangedEvent>(OnSafeAreaRootGeometryChanged);
            petElement?.UnregisterCallback<ClickEvent>(OnPetElementClicked);
            terrariumViewElement?.UnregisterCallback<GeometryChangedEvent>(OnTerrariumGeometryChanged);
            terrariumViewElement?.UnregisterCallback<PointerDownEvent>(OnTerrariumPointerDown);
            terrariumViewElement?.UnregisterCallback<PointerUpEvent>(OnTerrariumPointerUp);

            if (feedButton != null)
            {
                feedButton.clicked -= OnFeedClicked;
            }

            if (waterButton != null)
            {
                waterButton.clicked -= OnRefreshWaterClicked;
            }

            if (cleanButton != null)
            {
                cleanButton.clicked -= OnCleanClicked;
            }

            if (milestoneContinueButton != null)
            {
                milestoneContinueButton.clicked -= OnMilestoneContinueClicked;
            }

            if (decorButton != null)
            {
                decorButton.clicked -= OnDecorButtonClicked;
            }

            if (decorDrawerCloseButton != null)
            {
                decorDrawerCloseButton.clicked -= OnDecorDrawerCloseClicked;
            }

            foreach (var pair in decorRowClickHandlers)
            {
                if (decorRowButtons.TryGetValue(pair.Key, out var rowButton))
                {
                    rowButton.clicked -= pair.Value;
                }
            }

            if (debugButton != null)
            {
                debugButton.clicked -= OnDebugButtonClicked;
            }

            if (debugCloseButton != null)
            {
                debugCloseButton.clicked -= OnDebugCloseClicked;
            }

            if (debug1xButton != null)
            {
                debug1xButton.clicked -= OnDebug1xClicked;
            }

            if (debug60xButton != null)
            {
                debug60xButton.clicked -= OnDebug60xClicked;
            }

            if (debug600xButton != null)
            {
                debug600xButton.clicked -= OnDebug600xClicked;
            }

            if (debugSimulate12hButton != null)
            {
                debugSimulate12hButton.clicked -= OnDebugSimulate12HoursClicked;
            }

            if (debugClearSaveButton != null)
            {
                debugClearSaveButton.clicked -= OnDebugClearSaveClicked;
            }

            if (debugAddCageButton != null)
            {
                debugAddCageButton.clicked -= OnDebugAddCageClicked;
            }

            if (debugAddPetButton != null)
            {
                debugAddPetButton.clicked -= OnDebugAddPetClicked;
            }

            if (debugHungerSliderCallback != null)
            {
                debugHungerSlider?.UnregisterValueChangedCallback(debugHungerSliderCallback);
            }

            if (debugHydrationSliderCallback != null)
            {
                debugHydrationSlider?.UnregisterValueChangedCallback(debugHydrationSliderCallback);
            }

            if (debugCleanlinessSliderCallback != null)
            {
                debugCleanlinessSlider?.UnregisterValueChangedCallback(debugCleanlinessSliderCallback);
            }

            if (debugHealthSliderCallback != null)
            {
                debugHealthSlider?.UnregisterValueChangedCallback(debugHealthSliderCallback);
            }

            if (debugWeightSliderCallback != null)
            {
                debugWeightSlider?.UnregisterValueChangedCallback(debugWeightSliderCallback);
            }
        }

        public void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveCurrentState();
                return;
            }

            if (session != null)
            {
                HandleReport(session.Resume());
            }
        }

        public void OnApplicationQuit()
        {
            SaveCurrentState();
        }

        private void SaveCurrentState()
        {
            session?.Save();
        }

        public void BindElements(VisualElement root)
        {
            growthStageLabel = root.Q<Label>("growth-stage-label");
            growthGaugeFill = root.Q<VisualElement>("growth-gauge-fill");

            hungerBarFill = root.Q<VisualElement>("hunger-bar-fill");
            hungerValueLabel = root.Q<Label>("hunger-value-label");
            hydrationBarFill = root.Q<VisualElement>("hydration-bar-fill");
            hydrationValueLabel = root.Q<Label>("hydration-value-label");
            cleanlinessBarFill = root.Q<VisualElement>("cleanliness-bar-fill");
            cleanlinessValueLabel = root.Q<Label>("cleanliness-value-label");
            healthBarFill = root.Q<VisualElement>("health-bar-fill");
            healthValueLabel = root.Q<Label>("health-value-label");

            feedbackLabel = root.Q<Label>("feedback-label");
            feedButton = root.Q<Button>("feed-button");
            waterButton = root.Q<Button>("water-button");
            cleanButton = root.Q<Button>("clean-button");

            milestoneModal = root.Q<VisualElement>("milestone-modal");
            milestoneStageLabel = root.Q<Label>("milestone-stage-label");
            milestoneMessageLabel = root.Q<Label>("milestone-message-label");
            milestoneContinueButton = root.Q<Button>("milestone-continue-button");

            terrariumViewElement?.UnregisterCallback<GeometryChangedEvent>(OnTerrariumGeometryChanged);
            terrariumViewElement = root.Q<VisualElement>("terrarium-view");
            terrariumViewElement?.RegisterCallback<GeometryChangedEvent>(OnTerrariumGeometryChanged);
            terrariumBackgroundElement = root.Q<VisualElement>("terrarium-background");
            effectsLayerElement = root.Q<VisualElement>("terrarium-effects");
            lightingElement = root.Q<VisualElement>("terrarium-lighting");
            dayPhaseLabel = root.Q<Label>("day-phase-label");
            weatherLabel = root.Q<Label>("weather-label");

            petElement?.UnregisterCallback<ClickEvent>(OnPetElementClicked);
            petElement = root.Q<VisualElement>("pet-image");
            if (petElement != null)
            {
                petElement.RegisterCallback<ClickEvent>(OnPetElementClicked);
            }

            RebuildPetActor();

            decorImageElement = root.Q<VisualElement>("decor-image");
            decorButton = root.Q<Button>("decor-button");
            decorDrawer = root.Q<VisualElement>("decor-drawer");
            decorDrawerCloseButton = root.Q<Button>("decor-drawer-close-button");

            decorRowButtons.Clear();
            decorStatusLabels.Clear();
            foreach (var decor in DecorCatalog.All)
            {
                var rowButton = root.Q<Button>($"decor-row-{decor.Id}");
                if (rowButton != null)
                {
                    decorRowButtons[decor.Id] = rowButton;
                }

                var statusLabel = root.Q<Label>($"decor-status-{decor.Id}");
                if (statusLabel != null)
                {
                    decorStatusLabels[decor.Id] = statusLabel;
                }
            }

            topBar = root.Q<VisualElement>("top-bar");
            debugButton = root.Q<Button>("debug-button");
            debugPanel = root.Q<VisualElement>("debug-panel");
            debugCloseButton = root.Q<Button>("debug-close-button");
            debug1xButton = root.Q<Button>("debug-1x-button");
            debug60xButton = root.Q<Button>("debug-60x-button");
            debug600xButton = root.Q<Button>("debug-600x-button");
            debugSimulate12hButton = root.Q<Button>("debug-simulate-12h-button");
            debugClearSaveButton = root.Q<Button>("debug-clear-save-button");
            debugAddCageButton = root.Q<Button>("debug-add-cage-button");
            debugAddPetButton = root.Q<Button>("debug-add-pet-button");
            debugAppliedElapsedLabel = root.Q<Label>("debug-applied-elapsed-label");
            debugHungerSlider = root.Q<Slider>("debug-hunger-slider");
            debugHydrationSlider = root.Q<Slider>("debug-hydration-slider");
            debugCleanlinessSlider = root.Q<Slider>("debug-cleanliness-slider");
            debugHealthSlider = root.Q<Slider>("debug-health-slider");
            debugWeightSlider = root.Q<Slider>("debug-weight-slider");

            SetUpDrawOrder();
        }

        public void OnFeedClicked()
        {
            var before = state.Hunger;

            if (session != null)
            {
                var outcome = colonyCare.Feed(session.Colony, state, GameNowUtc());
                if (outcome == FeedOutcome.Full)
                {
                    // Already full: no charge, no weight/hunger change, no eat animation.
                    ShowFeedback(CareFeedbackMessage.AlreadyFullMessage);
                    Render(state, tuning);
                    return;
                }

                if (outcome == FeedOutcome.NotEnoughMoney)
                {
                    ShowFeedback("お金が足りなくて餌を買えません");
                    Render(state, tuning);
                    SaveCurrentState();
                    return;
                }

                if (outcome != FeedOutcome.Ate)
                {
                    // A fasting leopard gecko just turns away; nothing is wrong with it.
                    petActor?.RefuseFood();
                    ShowFeedback(outcome == FeedOutcome.RefusedPreShed ? RefusePreShedMessage : RefusePreGrowthMessage);
                    Render(state, tuning);
                    SaveCurrentState();
                    return;
                }

                OnCareApplied(before, FeedSuccessMessage, actor => actor.Feed());
                return;
            }

            var appetite = careService.Feed(state, GameNowUtc());
            if (appetite != AppetiteState.Normal)
            {
                // A fasting leopard gecko just turns away; nothing is wrong with it.
                petActor?.RefuseFood();
                ShowFeedback(appetite == AppetiteState.PreShed ? RefusePreShedMessage : RefusePreGrowthMessage);
                Render(state, tuning);
                SaveCurrentState();
                return;
            }

            OnCareApplied(before, FeedSuccessMessage, actor => actor.Feed());
        }

        /// <summary>The game clock: real time, or the debug-accelerated clock while it runs ahead.</summary>
        private DateTimeOffset GameNowUtc()
        {
            return session != null ? session.GameNowUtc : (timeService?.UtcNow() ?? DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// Shows the local time of day, tints the terrarium for it, and tells the pet so it
        /// keeps a gecko's rhythm (asleep by day, up at dusk and night).
        /// </summary>
        public void UpdateClock(DateTime localNow)
        {
            var phase = DayPhaseClock.PhaseAt(localNow, petBehaviourTuning);
            if (dayPhaseLabel != null)
            {
                dayPhaseLabel.text = $"{DayPhaseClock.Label(phase)} {localNow:HH:mm}";
            }

            if (lightingElement != null)
            {
                DayPhaseClock.Tint(phase, petBehaviourTuning, out var r, out var g, out var b, out var a);
                lightingElement.style.backgroundColor = new Color(r, g, b, a);
            }

            petActor?.SetPhase(phase);
            RefreshShell();
        }

        private void RefreshShell()
        {
            if (session == null)
            {
                return;
            }

            var now = GameNowUtc();
            if (moneyLabel != null)
            {
                moneyLabel.text = $"所持金 ¥{session.Colony.Wallet.Money:N0}";
            }

            if (gameDateLabel != null)
            {
                gameDateLabel.text = session.Calendar.DateAt(now).ToDisplayText();
            }

            if (cageTitleLabel != null && currentCage != null)
            {
                cageTitleLabel.text = CageStatusText.TitleFor(currentCage, state);
            }

            // Rebuilding a list re-creates every row's Button, which can swallow a tap the
            // player is mid-gesture on; only render the list that is actually on screen, and
            // HomeView/CageListView additionally skip the rebuild when nothing shown in it
            // has changed since the last render (see HomeView.CageListSignature).
            if (navigator != null && navigator.Screen == ShellScreen.Home)
            {
                homeView?.Render(session.Colony, now, tuning);
            }

            if (navigator != null && navigator.Screen == ShellScreen.Main && navigator.Tab == ShellTab.Cages && !navigator.ShowingCageDetail)
            {
                cageListView?.Render(session.Colony, now, tuning);
            }
        }

        private void OnTerrariumPointerDown(PointerDownEvent evt)
        {
            BeginGestureTracking(evt.position);
        }

        /// <summary>
        /// Starts tracking a new pointer gesture: records the start position and clears any
        /// tap-suppression left over from a previous gesture. Without this, a swipe on the
        /// background (which never reaches a pet click to consume the flag) would leave
        /// suppressNextPetTap armed and silently swallow a later, unrelated tap on the pet.
        /// Public so PlayMode tests can drive the same seam the PointerDown handler uses.
        /// </summary>
        public void BeginGestureTracking(Vector2 position)
        {
            swipeStartPosition = position;
            suppressNextPetTap = false;
        }

        private void OnTerrariumPointerUp(PointerUpEvent evt)
        {
            if (swipeStartPosition == null)
            {
                return;
            }

            var dx = evt.position.x - swipeStartPosition.Value.x;
            swipeStartPosition = null;
            ApplySwipeDelta(dx);
        }

        /// <summary>
        /// Applies a horizontal drag distance in pixels: steps the cage when it crosses the
        /// swipe threshold, and marks that the very next pet tap should be ignored (see
        /// OnPetElementClicked) so a swipe starting/ending on the gecko is never also read
        /// as a tap on it. Public (rather than folded into the pointer-event handlers) so
        /// PlayMode tests can drive it directly without simulating raw pointer events.
        /// </summary>
        public void ApplySwipeDelta(float dx)
        {
            var step = SwipeStep(dx, SwipeThresholdPixels);
            if (step == 0)
            {
                return;
            }

            suppressNextPetTap = true;
            ShowCageStep(step);
        }

        /// <summary>Pure swipe-direction decision: -1/0/+1, tested without any UI plumbing.</summary>
        public static int SwipeStep(float dx, float threshold)
        {
            if (Math.Abs(dx) < threshold)
            {
                return 0;
            }

            return dx < 0 ? 1 : -1;
        }

        private void OnWeatherText(string text)
        {
            if (weatherLabel != null)
            {
                weatherLabel.text = text;
            }
        }

        public void OnRefreshWaterClicked()
        {
            var before = state.Hydration;
            careService.RefreshWater(state);
            OnCareApplied(before, RefreshWaterSuccessMessage, actor => actor.Cheer());
        }

        public void OnCleanClicked()
        {
            var before = state.Cleanliness;
            careService.Clean(state);
            OnCareApplied(before, CleanSuccessMessage, actor => actor.Cheer());
        }

        private void OnCareApplied(double valueBeforeAction, string successMessage, Action<PetActor> petReaction)
        {
            var message = CareFeedbackMessage.For(valueBeforeAction, successMessage);
            if (message == successMessage && petActor != null)
            {
                petReaction(petActor);
            }

            ShowFeedback(message);
            Render(state, tuning);
            SaveCurrentState();
        }

        public void OnPetElementClicked(ClickEvent evt)
        {
            if (suppressNextPetTap)
            {
                // A recognised swipe that started/ended on the gecko must not also count as
                // a tap on it; consume the flag so only this one click is skipped.
                suppressNextPetTap = false;
                return;
            }

            OnPetTapped();
        }

        /// <summary>
        /// Tapping the pet: it reacts (happy, wakes up, or threatens when poked too often)
        /// and the message hints at its most pressing need.
        /// </summary>
        public void OnPetTapped()
        {
            if (state == null)
            {
                return;
            }

            var reaction = petActor?.Tap() ?? PetTapReaction.Noticed;
            switch (reaction)
            {
                case PetTapReaction.Threat:
                    ShowFeedback(PetMoodMessage.Threatened);
                    break;
                case PetTapReaction.Woke:
                    ShowFeedback(PetMoodMessage.Woken);
                    break;
                default:
                    ShowFeedback(PetMoodMessage.For(state, tuning));
                    break;
            }
        }

        public void ShowMilestoneModal(GrowthStage stage)
        {
            if (milestoneModal == null)
            {
                return;
            }

            milestoneStageLabel.text = GrowthModel.StageLabel(stage);
            milestoneMessageLabel.text = MilestoneMessageFor(stage);
            milestoneModal.style.display = DisplayStyle.Flex;
        }

        public void OnMilestoneContinueClicked()
        {
            if (milestoneModal == null)
            {
                return;
            }

            milestoneModal.style.display = DisplayStyle.None;
        }

        private static string MilestoneMessageFor(GrowthStage stage)
        {
            switch (stage)
            {
                case GrowthStage.Juvenile:
                    return "少し大きくなったね！";
                case GrowthStage.Adult:
                    return "りっぱな成体になったよ！";
                default:
                    return string.Empty;
            }
        }

        public void OnDecorButtonClicked()
        {
            RefreshDecorDrawer();

            if (decorDrawer != null)
            {
                decorDrawer.style.display = DisplayStyle.Flex;
            }
        }

        public void OnDecorDrawerCloseClicked()
        {
            if (decorDrawer != null)
            {
                decorDrawer.style.display = DisplayStyle.None;
            }
        }

        public void OnDecorRowClicked(string decorId)
        {
            if (state == null || !state.UnlockedDecorIds.Contains(decorId))
            {
                return;
            }

            state.SelectedDecorId = decorId;
            RenderDecorImage(state);
            RefreshDecorDrawer();
            SaveCurrentState();
        }

        private void RefreshDecorDrawer()
        {
            if (state == null)
            {
                return;
            }

            foreach (var decor in DecorCatalog.All)
            {
                var unlocked = state.UnlockedDecorIds.Contains(decor.Id);

                if (decorStatusLabels.TryGetValue(decor.Id, out var statusLabel))
                {
                    if (!unlocked)
                    {
                        statusLabel.text = $"{decor.UnlockStage}で解放";
                    }
                    else if (state.SelectedDecorId == decor.Id)
                    {
                        statusLabel.text = "選択中";
                    }
                    else
                    {
                        statusLabel.text = string.Empty;
                    }
                }

                if (decorRowButtons.TryGetValue(decor.Id, out var rowButton))
                {
                    rowButton.SetEnabled(unlocked);
                }
            }
        }

        private void RenderDecorImage(PetState petState)
        {
            if (decorImageElement == null)
            {
                return;
            }

            if (appliedDecorIconClass != null)
            {
                decorImageElement.RemoveFromClassList(appliedDecorIconClass);
            }

            appliedDecorIconClass = $"decor-icon-{petState.SelectedDecorId}";
            decorImageElement.AddToClassList(appliedDecorIconClass);
            PlaceDecor();
        }

        private void OnTerrariumGeometryChanged(GeometryChangedEvent evt)
        {
            var size = terrariumViewElement.contentRect.size;
            terrariumProjection = new TerrariumProjection(size.x, size.y, artLayout);
            PlaceBackground();
            petActor?.SetViewSize(size.x, size.y);
            PlaceDecor();
        }

        private void PlaceBackground()
        {
            if (terrariumBackgroundElement == null || terrariumProjection == null || !terrariumProjection.IsValid)
            {
                return;
            }

            var box = terrariumProjection.BackgroundBox();
            terrariumBackgroundElement.style.left = box.Left;
            terrariumBackgroundElement.style.top = box.Top;
            terrariumBackgroundElement.style.width = box.Width;
            terrariumBackgroundElement.style.height = box.Height;
        }

        /// <summary>
        /// Stands the selected decor on the floor (or hangs it from the glass top) using the
        /// same projection as the pet, so both line up with the background at any screen size.
        /// </summary>
        private void PlaceDecor()
        {
            if (decorImageElement == null || state == null || terrariumProjection == null || !terrariumProjection.IsValid
                || !artLayout.Decor.TryGetValue(state.SelectedDecorId, out var placement))
            {
                return;
            }

            var bodyWidth = placement.BodyWidthFraction * terrariumProjection.ViewWidth;
            var box = placement.IsHanging
                ? terrariumProjection.PlaceHanging(placement.Sprite, placement.X, bodyWidth)
                : terrariumProjection.PlaceOnFloor(placement.Sprite, placement.X, placement.Depth, bodyWidth);

            decorImageElement.style.left = box.Left;
            decorImageElement.style.top = box.Top;
            decorImageElement.style.width = box.Width;
            decorImageElement.style.height = box.Height;
            decorImageElement.style.bottom = StyleKeyword.Auto;

            // Floor decor doubles as the gecko's hide: it sleeps right behind/beside it.
            if (placement.IsHanging)
            {
                petActor?.ClearShelter();
            }
            else
            {
                var pixels = box.Width / placement.Sprite.ImageWidth;
                var bodyCenterX = box.Left + (placement.Sprite.BodyLeft + placement.Sprite.BodyRight) / 2f * pixels;
                petActor?.SetShelterAt(bodyCenterX, placement.Depth);
            }
            drawOrder?.Apply();
        }

        /// <summary>
        /// Registers everything drawn inside the terrarium with one sort rule: background at
        /// the back, then decor and pet ordered by where they stand on the floor (further
        /// forward = drawn later), then effects on top. See Core/DrawOrder.cs.
        /// </summary>
        private void SetUpDrawOrder()
        {
            drawOrder = null;
            if (terrariumViewElement == null)
            {
                return;
            }

            drawOrder = new TerrariumDrawOrder(terrariumViewElement);
            drawOrder.Register(terrariumBackgroundElement, () => new DrawSortKey(DrawLayer.Background, 0f, 0));
            drawOrder.Register(decorImageElement, () => new DrawSortKey(DrawLayer.World, DecorSortDepth(), DrawTieBreak.Decor));
            drawOrder.Register(petElement, () => new DrawSortKey(DrawLayer.World, petActor?.Depth ?? 0f, DrawTieBreak.Pet));
            drawOrder.Register(lightingElement, () => new DrawSortKey(DrawLayer.Lighting, 0f, 0));
            drawOrder.Register(effectsLayerElement, () => new DrawSortKey(DrawLayer.Effects, 0f, 0));
            drawOrder.Apply();
        }

        /// <summary>Floor depth of the selected decor's ground contact; hanging decor sorts behind the floor.</summary>
        private float DecorSortDepth()
        {
            if (state == null || !artLayout.Decor.TryGetValue(state.SelectedDecorId, out var placement) || placement.IsHanging)
            {
                return DrawSortKey.BehindFloor;
            }

            return placement.Depth;
        }

        /// <summary>
        /// Shows/hides the debug button. Call from Awake with Debug.isDebugBuild — kept as
        /// a separate testable method rather than reading Debug.isDebugBuild inline. Also
        /// toggles a class on the top bar so the cage-nav row (see Terrarium.uss
        /// ".top-bar-with-debug .cage-nav") reserves room for the debug button instead of
        /// letting it cover the next-cage button, without affecting release builds where the
        /// debug button is hidden and no room needs to be reserved.
        /// </summary>
        public void ApplyDebugVisibility(bool isDebugBuild)
        {
            if (debugButton != null)
            {
                debugButton.style.display = isDebugBuild ? DisplayStyle.Flex : DisplayStyle.None;
            }

            topBar?.EnableInClassList("top-bar-with-debug", isDebugBuild);
        }

        public void OnDebugButtonClicked()
        {
            RefreshDebugPanel();

            if (debugPanel != null)
            {
                debugPanel.style.display = DisplayStyle.Flex;
            }
        }

        public void OnDebugCloseClicked()
        {
            if (debugPanel != null)
            {
                debugPanel.style.display = DisplayStyle.None;
            }
        }

        private void OnDebug1xClicked()
        {
            OnDebugMultiplierClicked(1d);
        }

        private void OnDebug60xClicked()
        {
            OnDebugMultiplierClicked(60d);
        }

        private void OnDebug600xClicked()
        {
            OnDebugMultiplierClicked(600d);
        }

        public void OnDebugMultiplierClicked(double multiplier)
        {
            if (timeService == null)
            {
                return;
            }

            timeService.TimeMultiplier = multiplier;
            StartLiveTickIfNeeded();
        }

        private void StartLiveTickIfNeeded()
        {
            if (liveTickCoroutine == null)
            {
                liveTickCoroutine = StartCoroutine(LiveTickLoop());
            }
        }

        private IEnumerator LiveTickLoop()
        {
            while (true)
            {
                yield return null;
                // Clamp: the first frame after resuming reports the whole background time,
                // which OnApplicationPause(false) has already applied.
                ApplyLiveTickDelta(TimeSpan.FromSeconds(Mathf.Min(Time.unscaledDeltaTime, MaxLiveTickFrameSeconds)));
            }
        }

        /// <summary>
        /// One live-tick step. Public so tests can drive it directly with a controlled
        /// TimeSpan instead of waiting on real frames (see unity-playmode-integration-test
        /// skill).
        /// </summary>
        public void ApplyLiveTickDelta(TimeSpan realDelta)
        {
            if (session != null)
            {
                HandleReport(session.Advance(realDelta));
            }
        }

        public void OnDebugSimulate12HoursClicked()
        {
            if (session == null)
            {
                return;
            }

            HandleReport(session.SimulateGameTime(TimeSpan.FromHours(12)));
            SaveCurrentState();
        }

        public void OnDebugClearSaveClicked()
        {
            if (session == null || string.IsNullOrEmpty(session.SavePath))
            {
                return;
            }

            var path = session.SavePath;
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            LoadColony(path, timeService.UtcNow(), timeService);
            RefreshDebugPanel();
        }

        public void OnDebugAddCageClicked()
        {
            if (session?.Colony.AddCage(CageSize.Standard) == null)
            {
                ShowFeedback("ラックがいっぱいです");
                return;
            }

            SaveCurrentState();
            ColonyChanged?.Invoke();
        }

        public void OnDebugAddPetClicked()
        {
            var empty = session?.Colony.Cages.Find(c => c.IsEmpty);
            if (empty == null)
            {
                ShowFeedback("空きケージがありません");
                return;
            }

            var now = GameNowUtc();
            session.Colony.AddAnimal(new PetState
            {
                Name = $"レオパ{session.Colony.NextAnimalId}",
                Sex = UnityEngine.Random.value < 0.5f ? Sex.Female : Sex.Male,
                HatchedAtUtc = now,
                LastSavedAtUtc = now,
                LastShedAtUtc = now,
                NextShedAtUtc = now + SheddingModel.IntervalFor(GrowthStage.Baby, tuning),
            }, empty);
            SaveCurrentState();
            ColonyChanged?.Invoke();
        }

        public void OnDebugHungerChanged(double value)
        {
            if (state == null)
            {
                return;
            }

            state.Hunger = value;
            Render(state, tuning);
            SaveCurrentState();
        }

        public void OnDebugHydrationChanged(double value)
        {
            if (state == null)
            {
                return;
            }

            state.Hydration = value;
            Render(state, tuning);
            SaveCurrentState();
        }

        public void OnDebugCleanlinessChanged(double value)
        {
            if (state == null)
            {
                return;
            }

            state.Cleanliness = value;
            Render(state, tuning);
            SaveCurrentState();
        }

        public void OnDebugHealthChanged(double value)
        {
            if (state == null)
            {
                return;
            }

            state.Health = value;
            Render(state, tuning);
            SaveCurrentState();
        }

        public void OnDebugWeightChanged(double grams)
        {
            if (state == null)
            {
                return;
            }

            state.WeightGrams = grams;
            Render(state, tuning);
            SaveCurrentState();
        }

        private void RefreshDebugPanel()
        {
            if (state == null)
            {
                return;
            }

            debugHungerSlider?.SetValueWithoutNotify((float)state.Hunger);
            debugHydrationSlider?.SetValueWithoutNotify((float)state.Hydration);
            debugCleanlinessSlider?.SetValueWithoutNotify((float)state.Cleanliness);
            debugHealthSlider?.SetValueWithoutNotify((float)state.Health);
            debugWeightSlider?.SetValueWithoutNotify((float)state.WeightGrams);
            UpdateDebugAppliedElapsedLabel();
        }

        private void UpdateDebugAppliedElapsedLabel()
        {
            if (debugAppliedElapsedLabel != null)
            {
                debugAppliedElapsedLabel.text = $"前回反映: {FormatElapsed(lastAppliedElapsed)}";
            }
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            return $"{(int)elapsed.TotalHours}時間{elapsed.Minutes}分";
        }

        private void ShowFeedback(string message)
        {
            if (feedbackLabel == null)
            {
                return;
            }

            feedbackLabel.text = message;
            feedbackLabel.style.visibility = Visibility.Visible;

            if (feedbackHideCoroutine != null)
            {
                StopCoroutine(feedbackHideCoroutine);
            }

            feedbackHideCoroutine = StartCoroutine(HideFeedbackAfterDelay());
        }

        private IEnumerator HideFeedbackAfterDelay()
        {
            yield return new WaitForSeconds(FeedbackDurationSeconds);
            feedbackLabel.style.visibility = Visibility.Hidden;
            feedbackHideCoroutine = null;
        }

        public void Render(PetState petState, CareTuning careTuning)
        {
            growthStageLabel.text = $"{GrowthModel.StageLabel(petState.Stage)} {petState.WeightGrams:0.0}g";
            growthGaugeFill.style.width = new Length((float)(GrowthModel.ProgressToNextStage(petState, GameNowUtc(), careTuning) * 100d), LengthUnit.Percent);

            RenderStatusRow(hungerBarFill, hungerValueLabel, petState.Hunger, careTuning);
            RenderStatusRow(hydrationBarFill, hydrationValueLabel, petState.Hydration, careTuning);
            RenderStatusRow(cleanlinessBarFill, cleanlinessValueLabel, petState.Cleanliness, careTuning);
            RenderStatusRow(healthBarFill, healthValueLabel, petState.Health, careTuning);

            RenderDecorImage(petState);
            petActor?.SetCondition(PetMoodEvaluator.Evaluate(petState, careTuning, petBehaviourTuning), petState.GrowthStage);
            petActor?.SetAppetite(AppetiteModel.Evaluate(petState, GameNowUtc(), careTuning));
        }

        private static void RenderStatusRow(VisualElement barFill, Label valueLabel, double value, CareTuning tuningForThresholds)
        {
            var clamped = StatusValue.Clamp(value);
            barFill.style.width = new Length((float)clamped, LengthUnit.Percent);
            valueLabel.text = Mathf.RoundToInt((float)clamped).ToString();

            barFill.RemoveFromClassList("status-good");
            barFill.RemoveFromClassList("status-warning");
            barFill.RemoveFromClassList("status-critical");
            barFill.AddToClassList(LevelClassName(CareStatusPresentation.LevelFor(clamped, tuningForThresholds)));
        }

        private static string LevelClassName(StatusBarLevel level)
        {
            switch (level)
            {
                case StatusBarLevel.Good:
                    return "status-good";
                case StatusBarLevel.Warning:
                    return "status-warning";
                default:
                    return "status-critical";
            }
        }
    }
}
