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
        private SaveService saveService;
        private OfflineProgressCalculator offlineProgressCalculator;
        private TimeService timeService;
        private string savePath;

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

        private Button debugButton;
        private VisualElement debugPanel;
        private Button debugCloseButton;
        private Button debug1xButton;
        private Button debug60xButton;
        private Button debug600xButton;
        private Button debugSimulate12hButton;
        private Button debugClearSaveButton;
        private Label debugAppliedElapsedLabel;
        private Slider debugHungerSlider;
        private Slider debugHydrationSlider;
        private Slider debugCleanlinessSlider;
        private Slider debugHealthSlider;
        private Slider debugGrowthSlider;
        private Coroutine liveTickCoroutine;
        private DateTimeOffset virtualNow;
        private TimeSpan lastAppliedElapsed;
        private EventCallback<ChangeEvent<float>> debugHungerSliderCallback;
        private EventCallback<ChangeEvent<float>> debugHydrationSliderCallback;
        private EventCallback<ChangeEvent<float>> debugCleanlinessSliderCallback;
        private EventCallback<ChangeEvent<float>> debugHealthSliderCallback;
        private EventCallback<ChangeEvent<float>> debugGrowthSliderCallback;
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

            debugHungerSliderCallback = evt => OnDebugHungerChanged(evt.newValue);
            debugHydrationSliderCallback = evt => OnDebugHydrationChanged(evt.newValue);
            debugCleanlinessSliderCallback = evt => OnDebugCleanlinessChanged(evt.newValue);
            debugHealthSliderCallback = evt => OnDebugHealthChanged(evt.newValue);
            debugGrowthSliderCallback = evt => OnDebugGrowthChanged(evt.newValue);
            debugHungerSlider?.RegisterValueChangedCallback(debugHungerSliderCallback);
            debugHydrationSlider?.RegisterValueChangedCallback(debugHydrationSliderCallback);
            debugCleanlinessSlider?.RegisterValueChangedCallback(debugCleanlinessSliderCallback);
            debugHealthSlider?.RegisterValueChangedCallback(debugHealthSliderCallback);
            debugGrowthSlider?.RegisterValueChangedCallback(debugGrowthSliderCallback);

            ApplyDebugVisibility(Debug.isDebugBuild);

            var resolvedSavePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            LoadStateAndApplyOfflineProgress(resolvedSavePath, new TimeService().UtcNow());

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
            virtualNow = state.LastSavedAtUtc;

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
        /// Loads (or creates) the save at savePath, applies capped offline progress up to
        /// nowUtc, initializes the view with the result, and shows the milestone modal if
        /// that crossed a growth-stage boundary. Public so tests can drive it directly
        /// without a real UIDocument, mirroring Initialize/BindElements.
        /// </summary>
        public void LoadStateAndApplyOfflineProgress(string atSavePath, DateTimeOffset nowUtc, TimeService clock = null)
        {
            savePath = atSavePath;
            var loadTuning = new CareTuning();
            saveService = new SaveService();
            offlineProgressCalculator = new OfflineProgressCalculator(loadTuning);
            timeService = clock ?? new TimeService();

            var loadedState = saveService.LoadOrCreateDefault(savePath, nowUtc);
            var result = offlineProgressCalculator.Apply(loadedState, loadedState.LastSavedAtUtc, nowUtc);

            Initialize(loadedState, loadTuning);
            ApplyCalculatorResult(result);

            // Start live progression from "now", not from the old save time.
            virtualNow = nowUtc;
            ResyncClock(nowUtc);
        }

        /// <summary>
        /// Advances state.LastSavedAtUtc by exactly what the calculator applied (not to
        /// the raw target time) so any sub-minute remainder survives for next time, then
        /// renders and handles a growth-stage crossing if one occurred. Shared by the
        /// initial load, the live debug tick, and the one-shot debug simulate action.
        /// </summary>
        private void ApplyCalculatorResult(OfflineProgressResult result)
        {
            state.LastSavedAtUtc += result.AppliedElapsed;

            if (result.AppliedElapsed <= TimeSpan.Zero)
            {
                return;
            }

            lastAppliedElapsed = result.AppliedElapsed;
            Render(state, tuning);
            UpdateDebugAppliedElapsedLabel();

            if (result.NewGrowthStage.HasValue)
            {
                DecorUnlockService.GrantUnlocksForStage(state, result.NewGrowthStage.Value);
                ShowMilestoneModal(result.NewGrowthStage.Value);
            }

            if (result.ShedCount > 0)
            {
                ShowFeedback(ShedMessage);
            }
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

            if (debugGrowthSliderCallback != null)
            {
                debugGrowthSlider?.UnregisterValueChangedCallback(debugGrowthSliderCallback);
            }
        }

        public void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveCurrentState();
                return;
            }

            if (state == null || offlineProgressCalculator == null || timeService == null)
            {
                return;
            }

            // Resume: apply the capped real time spent in the background, then re-anchor.
            var nowUtc = timeService.UtcNow();
            virtualNow = nowUtc;
            ApplyCalculatorResult(offlineProgressCalculator.Apply(state, state.LastSavedAtUtc, nowUtc));
            SaveCurrentState();
        }

        public void OnApplicationQuit()
        {
            SaveCurrentState();
        }

        private void SaveCurrentState()
        {
            if (state == null || saveService == null || string.IsNullOrEmpty(savePath))
            {
                return;
            }

            if (timeService != null)
            {
                ResyncClock(timeService.UtcNow());
            }

            saveService.Save(savePath, state);
        }

        /// <summary>
        /// Re-anchors both progress clocks on the real clock. The debug multiplier lets
        /// virtualNow run hours ahead of real time; moving only LastSavedAtUtc back to real
        /// time made the next tick replay that whole gap and wipe out the care action just
        /// taken. Keeps a sub-step remainder so no progress is lost, and drops anything
        /// larger (the part beyond the offline cap).
        /// </summary>
        private void ResyncClock(DateTimeOffset nowUtc)
        {
            var pending = virtualNow - state.LastSavedAtUtc;
            var step = TimeSpan.FromMinutes(tuning.OfflineProgressStepMinutes);
            if (pending < TimeSpan.Zero || pending >= step)
            {
                pending = TimeSpan.Zero;
            }

            state.LastSavedAtUtc = nowUtc - pending;
            virtualNow = nowUtc;
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

            petActor?.Effects.Clear();
            petElement?.UnregisterCallback<ClickEvent>(OnPetElementClicked);
            petElement = root.Q<VisualElement>("pet-image");
            petActor = null;
            if (petElement != null)
            {
                petActor = new PetActor(petElement, petBehaviourTuning, artLayout, new System.Random(),
                    PetSpriteLibrary.LoadFromResources(), effectsLayerElement ?? terrariumViewElement);
                petElement.RegisterCallback<ClickEvent>(OnPetElementClicked);
            }

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

            debugButton = root.Q<Button>("debug-button");
            debugPanel = root.Q<VisualElement>("debug-panel");
            debugCloseButton = root.Q<Button>("debug-close-button");
            debug1xButton = root.Q<Button>("debug-1x-button");
            debug60xButton = root.Q<Button>("debug-60x-button");
            debug600xButton = root.Q<Button>("debug-600x-button");
            debugSimulate12hButton = root.Q<Button>("debug-simulate-12h-button");
            debugClearSaveButton = root.Q<Button>("debug-clear-save-button");
            debugAppliedElapsedLabel = root.Q<Label>("debug-applied-elapsed-label");
            debugHungerSlider = root.Q<Slider>("debug-hunger-slider");
            debugHydrationSlider = root.Q<Slider>("debug-hydration-slider");
            debugCleanlinessSlider = root.Q<Slider>("debug-cleanliness-slider");
            debugHealthSlider = root.Q<Slider>("debug-health-slider");
            debugGrowthSlider = root.Q<Slider>("debug-growth-slider");

            SetUpDrawOrder();
        }

        public void OnFeedClicked()
        {
            var before = state.Hunger;
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
            if (virtualNow != default)
            {
                return virtualNow;
            }

            return timeService != null ? timeService.UtcNow() : DateTimeOffset.UtcNow;
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

        private void OnPetElementClicked(ClickEvent evt)
        {
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

            milestoneStageLabel.text = stage.ToString();
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
        /// a separate testable method rather than reading Debug.isDebugBuild inline.
        /// </summary>
        public void ApplyDebugVisibility(bool isDebugBuild)
        {
            if (debugButton != null)
            {
                debugButton.style.display = isDebugBuild ? DisplayStyle.Flex : DisplayStyle.None;
            }
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
        /// skill). Accumulates scaled real time into virtualNow every call regardless of
        /// whether a whole minute-step actually completed, so sub-minute remainders are
        /// never lost between calls — see ApplyCalculatorResult for the matching half.
        /// </summary>
        public void ApplyLiveTickDelta(TimeSpan realDelta)
        {
            if (state == null || offlineProgressCalculator == null || timeService == null)
            {
                return;
            }

            var scaledDelta = timeService.ScaleElapsed(realDelta);
            if (scaledDelta <= TimeSpan.Zero)
            {
                return;
            }

            virtualNow += scaledDelta;
            var result = offlineProgressCalculator.Apply(state, state.LastSavedAtUtc, virtualNow);
            ApplyCalculatorResult(result);
        }

        public void OnDebugSimulate12HoursClicked()
        {
            if (state == null || offlineProgressCalculator == null)
            {
                return;
            }

            var target = state.LastSavedAtUtc + TimeSpan.FromHours(12);
            var result = offlineProgressCalculator.Apply(state, state.LastSavedAtUtc, target);
            ApplyCalculatorResult(result);
            SaveCurrentState();
        }

        public void OnDebugClearSaveClicked()
        {
            if (string.IsNullOrEmpty(savePath))
            {
                return;
            }

            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            var nowUtc = timeService != null ? timeService.UtcNow() : DateTimeOffset.UtcNow;
            LoadStateAndApplyOfflineProgress(savePath, nowUtc);
            RefreshDebugPanel();
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

        public void OnDebugGrowthChanged(double value)
        {
            if (state == null)
            {
                return;
            }

            state.Growth = value;
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
            debugGrowthSlider?.SetValueWithoutNotify((float)state.Growth);
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
            growthStageLabel.text = petState.GrowthStage.ToString();
            growthGaugeFill.style.width = new Length((float)GrowthGaugeCalculator.PercentWithinStage(petState.Growth), LengthUnit.Percent);

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
