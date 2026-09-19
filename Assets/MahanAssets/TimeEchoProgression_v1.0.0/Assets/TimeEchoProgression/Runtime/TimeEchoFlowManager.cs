using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace TimeEcho.Flow
{
    public enum FlowTransitionReason
    {
        Boot,
        NewGame,
        Continue,
        NextLevel,
        Death,
        Restart,
        Win,
        ReturnToMenu,
        Custom
    }

    [DefaultExecutionOrder(-2000)]
    public sealed class TimeEchoFlowManager : MonoBehaviour
    {
        public static TimeEchoFlowManager Instance { get; private set; }

        [SerializeField] private TimeEchoFlowConfig config;

        private FlowOverlayView overlay;
        private FlowSaveData save;
        private Coroutine activeRoutine;
        private IDisposable gameplayLock;
        private TimeEcho.PlayerVitality watchedVitality;
        private GameObject generatedEventSystem;
        private bool isChangingScene;
        private bool deathQueued;
        private int currentLevelIndex = -1;

        public TimeEchoFlowConfig Config => config;
        public FlowState State { get; private set; } = FlowState.Booting;
        public int CurrentLevelIndex => currentLevelIndex;
        public bool IsBusy => activeRoutine != null || isChangingScene;
        public bool HasStartedGame => save != null && save.hasStarted;
        public int HighestUnlockedLevelIndex => save != null ? save.highestUnlockedLevelIndex : 0;
        public int DeathCount => save != null ? save.deathCount : 0;

        public event Action<FlowState, FlowState> StateChanged;
        public event Action<FlowTransitionReason, string> TransitionStarted;
        public event Action<FlowTransitionReason, string> TransitionFinished;
        public event Action<int, string> LevelEntered;
        public event Action<int> PlayerDied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null)
            {
                return;
            }

            TimeEchoFlowConfig loadedConfig = Resources.Load<TimeEchoFlowConfig>(TimeEchoFlowConfig.ResourceName);
            if (loadedConfig == null)
            {
                return;
            }

            GameObject root = new GameObject("TimeEcho Flow Manager");
            TimeEchoFlowManager manager = root.AddComponent<TimeEchoFlowManager>();
            manager.config = loadedConfig;
            manager.Initialize();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (config == null)
            {
                config = Resources.Load<TimeEchoFlowConfig>(TimeEchoFlowConfig.ResourceName);
            }

            if (overlay == null && config != null)
            {
                Initialize();
            }
        }

        private void OnDestroy()
        {
            UnwatchVitality();
            ReleaseGameplayLock();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Configure(TimeEchoFlowConfig flowConfig)
        {
            config = flowConfig;
            if (Application.isPlaying && overlay == null)
            {
                Initialize();
            }
        }

        private void Initialize()
        {
            if (config == null)
            {
                Debug.LogError("Time Echo Progression needs a TimeEchoFlowConfig in a Resources folder.", this);
                enabled = false;
                return;
            }

            if (Instance != this)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }

            save = FlowSaveStore.Load(config);
            overlay = FlowOverlayView.Create(transform);
            overlay.ConfigureMenu(config, save.hasStarted);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            HandleSceneReady(SceneManager.GetActiveScene(), FlowTransitionReason.Boot, true);
        }

        public void StartNewGame()
        {
            if (IsBusy || config == null) return;
            save = new FlowSaveData
            {
                currentLevelIndex = 0,
                highestUnlockedLevelIndex = 0,
                hasStarted = true,
                hasWon = false,
                deathCount = 0
            };
            FlowSaveStore.Delete(config);
            FlowSaveStore.Save(config, save);
            overlay.ConfigureMenu(config, false);
            LoadWithSequence(config.FirstLevelScene, config.menuToGame, FlowTransitionReason.NewGame, config.playIntroWhenEnteringFromMenu);
        }

        public void ContinueGame()
        {
            if (IsBusy || config == null || save == null || !save.hasStarted) return;
            int index = Mathf.Clamp(save.currentLevelIndex, 0, Mathf.Max(0, config.levels.Count - 1));
            LevelFlowDefinition level = config.GetLevel(index);
            if (level == null || string.IsNullOrWhiteSpace(level.sceneName))
            {
                Debug.LogError("The saved level is missing from TimeEchoFlowConfig.", this);
                return;
            }

            LoadWithSequence(level.sceneName, config.menuToGame, FlowTransitionReason.Continue, config.playIntroWhenEnteringFromMenu);
        }

        public void CompleteCurrentLevel()
        {
            CompleteCurrentLevel(string.Empty);
        }

        public void CompleteCurrentLevel(string destinationOverride)
        {
            if (IsBusy || config == null)
            {
                return;
            }

            int index = config.FindLevelIndex(SceneManager.GetActiveScene().name);
            if (index < 0)
            {
                Debug.LogError("The active scene is not listed as a level in TimeEchoFlowConfig.", this);
                return;
            }

            LevelFlowDefinition current = config.GetLevel(index);
            bool hasExplicitDestination = !string.IsNullOrWhiteSpace(destinationOverride) ||
                                          (current != null && !string.IsNullOrWhiteSpace(current.nextSceneOverride));
            string destination = !string.IsNullOrWhiteSpace(destinationOverride)
                ? destinationOverride
                : current != null ? current.nextSceneOverride : string.Empty;

            if (!hasExplicitDestination && index + 1 < config.levels.Count)
            {
                LevelFlowDefinition next = config.GetLevel(index + 1);
                destination = next != null ? next.sceneName : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(destination))
            {
                WinGame();
                return;
            }

            save.hasStarted = true;
            int configuredDestinationIndex = config.FindLevelIndex(destination);
            if (configuredDestinationIndex >= 0)
            {
                save.currentLevelIndex = configuredDestinationIndex;
                save.highestUnlockedLevelIndex = Mathf.Max(save.highestUnlockedLevelIndex, configuredDestinationIndex);
            }
            FlowSaveStore.Save(config, save);

            FlowSequence sequence = current != null ? current.nextLevel.Resolve(config.nextLevel) : config.nextLevel;
            LoadWithSequence(destination, sequence, FlowTransitionReason.NextLevel, config.playIntroAfterNextLevel);
        }

        public void ReportPlayerDeath()
        {
            if (config == null || State == FlowState.Menu || State == FlowState.Won || State == FlowState.Dead || isChangingScene)
            {
                return;
            }

            if (activeRoutine != null)
            {
                deathQueued = true;
                return;
            }
            activeRoutine = StartCoroutine(DeathRoutine());
        }

        public void RestartCurrentLevel()
        {
            if (IsBusy || config == null) return;
            LevelFlowDefinition level = config.GetLevel(currentLevelIndex);
            FlowSequence sequence = level != null ? level.restart.Resolve(config.restart) : config.restart;
            LoadWithSequence(SceneManager.GetActiveScene().name, sequence, FlowTransitionReason.Restart, config.playIntroAfterRestart);
        }

        public void ReturnToMenu()
        {
            if (IsBusy || config == null) return;
            LoadWithSequence(config.menuScene, config.returnToMenu, FlowTransitionReason.ReturnToMenu, false);
        }

        public void LoadCustomScene(string sceneName)
        {
            if (IsBusy || string.IsNullOrWhiteSpace(sceneName)) return;
            LoadWithSequence(sceneName, config != null ? config.nextLevel : null, FlowTransitionReason.Custom, false);
        }

        public void PlaySequence(FlowSequence sequence)
        {
            if (IsBusy || sequence == null || !sequence.enabled) return;
            activeRoutine = StartCoroutine(OneShotSequenceRoutine(sequence));
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ClearSavedProgress()
        {
            if (config == null) return;
            FlowSaveStore.Delete(config);
            save = new FlowSaveData();
            overlay?.ConfigureMenu(config, false);
        }

        private IEnumerator DeathRoutine()
        {
            ChangeState(FlowState.Dead);
            save.deathCount++;
            FlowSaveStore.Save(config, save);
            PlayerDied?.Invoke(save.deathCount);
            StopTemporalControl();
            AcquireGameplayLock(true);

            if (config.deathDelay > 0f)
            {
                yield return WaitUnscaled(config.deathDelay);
            }

            LevelFlowDefinition level = config.GetLevel(currentLevelIndex);
            FlowSequence deathSequence = level != null ? level.death.Resolve(config.death) : config.death;
            yield return PlayPresentation(deathSequence);

            DeathRule rule = level != null ? level.deathRule.Resolve(config.defaultDeathRule) : config.defaultDeathRule;
            bool sentBack;
            string destination = ResolveDeathDestination(rule, out sentBack);
            if (string.IsNullOrWhiteSpace(destination))
            {
                destination = SceneManager.GetActiveScene().name;
            }

            int destinationIndex = config.FindLevelIndex(destination);
            if (destinationIndex >= 0)
            {
                save.currentLevelIndex = destinationIndex;
                if (sentBack && rule.eraseForwardProgressWhenSentBack)
                {
                    save.highestUnlockedLevelIndex = destinationIndex;
                }
            }
            FlowSaveStore.Save(config, save);

            FlowSequence restartSequence = level != null ? level.restart.Resolve(config.restart) : config.restart;
            bool playIntro = sentBack ? config.playIntroAfterDeathSendsPlayerBack : config.playIntroAfterRestart;
            activeRoutine = null;
            LoadWithSequence(destination, restartSequence, FlowTransitionReason.Death, playIntro);
        }

        private void WinGame()
        {
            save.hasWon = true;
            save.hasStarted = true;
            FlowSaveStore.Save(config, save);

            if (!string.IsNullOrWhiteSpace(config.winScene) && Application.CanStreamedLevelBeLoaded(config.winScene))
            {
                LoadWithSequence(config.winScene, config.winning, FlowTransitionReason.Win, false);
                return;
            }

            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(WinWithoutSceneRoutine());
        }

        private IEnumerator WinWithoutSceneRoutine()
        {
            ChangeState(FlowState.Presenting);
            AcquireGameplayLock(true);
            yield return PlayPresentation(config.winning);
            ReleaseGameplayLock();
            ChangeState(FlowState.Won);
            EnsureEventSystem();
            overlay.ConfigureMenu(config, save.hasStarted);
            overlay.ShowWin(true);
            activeRoutine = null;
        }

        private IEnumerator OneShotSequenceRoutine(FlowSequence sequence)
        {
            StopTemporalControl();
            if (sequence.lockGameplay)
            {
                AcquireGameplayLock(sequence.showLetterbox);
            }
            yield return PlayPresentation(sequence);
            ReleaseGameplayLock();
            if (currentLevelIndex >= 0)
            {
                ChangeState(FlowState.Playing);
            }
            activeRoutine = null;
            StartQueuedDeathIfNeeded();
        }

        private string ResolveDeathDestination(DeathRule rule, out bool sentBack)
        {
            sentBack = false;
            if (rule == null)
            {
                return SceneManager.GetActiveScene().name;
            }

            DeathDestinationMode mode = rule.destination;
            if (mode == DeathDestinationMode.ChancePreviousOtherwiseRestart)
            {
                mode = UnityEngine.Random.value < rule.previousLevelChance
                    ? DeathDestinationMode.PreviousLevel
                    : DeathDestinationMode.RestartCurrent;
            }

            if (mode == DeathDestinationMode.PreviousLevel)
            {
                if (currentLevelIndex > 0)
                {
                    sentBack = true;
                    LevelFlowDefinition previous = config.GetLevel(currentLevelIndex - 1);
                    return previous != null ? previous.sceneName : SceneManager.GetActiveScene().name;
                }

                mode = rule.firstLevelFallback == DeathDestinationMode.PreviousLevel
                    ? DeathDestinationMode.RestartCurrent
                    : rule.firstLevelFallback;
            }

            switch (mode)
            {
                case DeathDestinationMode.FirstLevel:
                    return config.FirstLevelScene;
                case DeathDestinationMode.MainMenu:
                    return config.menuScene;
                case DeathDestinationMode.SpecificScene:
                    return rule.specificScene;
                default:
                    return SceneManager.GetActiveScene().name;
            }
        }

        private void LoadWithSequence(string sceneName, FlowSequence sequence, FlowTransitionReason reason, bool playIntroAfterLoad)
        {
            if (IsBusy || string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("Time Echo cannot load scene '" + sceneName + "'. Add it to Build Settings or correct the Flow Config.", this);
                return;
            }

            activeRoutine = StartCoroutine(LoadRoutine(sceneName, sequence, reason, playIntroAfterLoad));
        }

        private IEnumerator LoadRoutine(string sceneName, FlowSequence sequence, FlowTransitionReason reason, bool playIntroAfterLoad)
        {
            isChangingScene = true;
            ChangeState(FlowState.Presenting);
            TransitionStarted?.Invoke(reason, sceneName);
            StopTemporalControl();
            AcquireGameplayLock(sequence == null || sequence.showLetterbox);
            overlay.HideScreens();

            if (sequence != null && sequence.enabled)
            {
                if (sequence.delayBefore > 0f) yield return WaitUnscaled(sequence.delayBefore);
                overlay.BeginSequence(sequence);
            }

            ChangeState(FlowState.Loading);
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (load == null)
            {
                Debug.LogError("Unity did not create a load operation for scene '" + sceneName + "'.", this);
                FinishFailedTransition();
                yield break;
            }

            load.allowSceneActivation = false;
            if (sequence != null && sequence.enabled)
            {
                yield return overlay.AnimateToCover(sequence);
            }

            while (load.progress < 0.9f)
            {
                if (sequence != null && sequence.enabled)
                {
                    overlay.SetLoadingProgress(load.progress / 0.9f);
                }
                yield return null;
            }

            if (sequence != null && sequence.enabled)
            {
                overlay.ForceCovered(sequence);
            }
            load.allowSceneActivation = true;
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            ReleaseGameplayLock();
            AcquireGameplayLock(sequence == null || sequence.showLetterbox);

            if (sequence != null && sequence.enabled)
            {
                yield return overlay.Hold(sequence);
                yield return overlay.AnimateFromCover(sequence);
                overlay.EndSequence();
            }

            ReleaseGameplayLock();
            isChangingScene = false;
            activeRoutine = null;
            HandleSceneReady(SceneManager.GetActiveScene(), reason, false);
            TransitionFinished?.Invoke(reason, sceneName);

            if (playIntroAfterLoad && currentLevelIndex >= 0)
            {
                PlayCurrentLevelIntro();
            }
        }

        private IEnumerator PlayPresentation(FlowSequence sequence)
        {
            if (sequence == null || !sequence.enabled)
            {
                yield break;
            }

            ChangeState(FlowState.Presenting);
            if (sequence.delayBefore > 0f) yield return WaitUnscaled(sequence.delayBefore);
            overlay.BeginSequence(sequence);
            yield return overlay.AnimateToCover(sequence);
            yield return overlay.Hold(sequence);
            yield return overlay.AnimateFromCover(sequence);
            overlay.EndSequence();
        }

        private void PlayCurrentLevelIntro()
        {
            if (IsBusy || currentLevelIndex < 0) return;
            LevelFlowDefinition level = config.GetLevel(currentLevelIndex);
            FlowSequence intro = level != null ? level.intro.Resolve(config.levelIntro) : config.levelIntro;
            if (intro == null || !intro.enabled)
            {
                ChangeState(FlowState.Playing);
                return;
            }

            activeRoutine = StartCoroutine(LevelIntroRoutine(intro));
        }

        private IEnumerator LevelIntroRoutine(FlowSequence sequence)
        {
            StopTemporalControl();
            if (sequence.lockGameplay)
            {
                AcquireGameplayLock(sequence.showLetterbox);
            }
            yield return PlayPresentation(sequence);
            ReleaseGameplayLock();
            ChangeState(FlowState.Playing);
            activeRoutine = null;
            StartQueuedDeathIfNeeded();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            deathQueued = false;
            currentLevelIndex = config != null ? config.FindLevelIndex(scene.name) : -1;
            UnwatchVitality();
            if (config != null && config.automaticallyWatchPlayerVitality)
            {
                StartCoroutine(WatchVitalityNextFrame());
            }

            if (!isChangingScene && config != null && overlay != null)
            {
                StartCoroutine(HandleExternalSceneLoadNextFrame(scene));
            }
        }

        private IEnumerator HandleExternalSceneLoadNextFrame(Scene scene)
        {
            yield return null;
            if (!isChangingScene)
            {
                HandleSceneReady(scene, FlowTransitionReason.Custom, false);
            }
        }

        private IEnumerator WatchVitalityNextFrame()
        {
            yield return null;
            if (config == null || !config.automaticallyWatchPlayerVitality) yield break;
            TimeEcho.PlayerVitality vitality = FindObjectOfType<TimeEcho.PlayerVitality>();
            WatchVitality(vitality);
        }

        public void WatchVitality(TimeEcho.PlayerVitality vitality)
        {
            if (watchedVitality == vitality) return;
            UnwatchVitality();
            watchedVitality = vitality;
            if (watchedVitality != null)
            {
                watchedVitality.Died += ReportPlayerDeath;
            }
        }

        private void UnwatchVitality()
        {
            if (watchedVitality != null)
            {
                watchedVitality.Died -= ReportPlayerDeath;
                watchedVitality = null;
            }
        }

        private void HandleSceneReady(Scene scene, FlowTransitionReason reason, bool fromBoot)
        {
            if (config == null || overlay == null) return;
            currentLevelIndex = config.FindLevelIndex(scene.name);
            overlay.HideScreens();

            if (string.Equals(scene.name, config.menuScene, StringComparison.Ordinal))
            {
                ChangeState(FlowState.Menu);
                overlay.ConfigureMenu(config, save != null && save.hasStarted);
                if (config.showGeneratedMainMenu)
                {
                    EnsureEventSystem();
                    overlay.ShowMenu(true);
                }
                return;
            }

            if (string.Equals(scene.name, config.winScene, StringComparison.Ordinal))
            {
                ChangeState(FlowState.Won);
                EnsureEventSystem();
                overlay.ShowWin(true);
                return;
            }

            if (currentLevelIndex >= 0)
            {
                save.hasStarted = true;
                save.currentLevelIndex = currentLevelIndex;
                save.highestUnlockedLevelIndex = Mathf.Max(save.highestUnlockedLevelIndex, currentLevelIndex);
                FlowSaveStore.Save(config, save);
                ChangeState(FlowState.Playing);
                LevelFlowDefinition level = config.GetLevel(currentLevelIndex);
                LevelEntered?.Invoke(currentLevelIndex, level != null ? level.sceneName : scene.name);

                if (fromBoot)
                {
                    PlayCurrentLevelIntro();
                }
                return;
            }

            ChangeState(FlowState.Playing);
        }

        private void StopTemporalControl()
        {
            if (TimeEcho.TimeDirector.Instance != null)
            {
                TimeEcho.TimeDirector.Instance.SetMode(TimeEcho.TimeMode.Flowing);
            }
        }

        private void AcquireGameplayLock(bool showLetterbox)
        {
            ReleaseGameplayLock();
            if (!showLetterbox && TimeEcho.PresentationDirector.Instance == null)
            {
                return;
            }
            gameplayLock = TimeEcho.PresentationDirector.Instance?.AcquireGameplayLock(showLetterbox);
        }

        private void ReleaseGameplayLock()
        {
            gameplayLock?.Dispose();
            gameplayLock = null;
        }

        private void EnsureEventSystem()
        {
            EventSystem[] existingSystems = FindObjectsOfType<EventSystem>();
            for (int i = 0; i < existingSystems.Length; i++)
            {
                EventSystem existing = existingSystems[i];
                if (existing != null && existing.gameObject != generatedEventSystem)
                {
                    if (generatedEventSystem != null) Destroy(generatedEventSystem);
                    generatedEventSystem = null;
                    return;
                }
            }

            if (generatedEventSystem != null)
            {
                return;
            }

            generatedEventSystem = new GameObject("TimeEcho Generated EventSystem", typeof(EventSystem));
            generatedEventSystem.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            generatedEventSystem.AddComponent<InputSystemUIInputModule>();
#else
            generatedEventSystem.AddComponent<StandaloneInputModule>();
#endif
        }

        private void FinishFailedTransition()
        {
            overlay.EndSequence();
            ReleaseGameplayLock();
            isChangingScene = false;
            activeRoutine = null;
            ChangeState(currentLevelIndex >= 0 ? FlowState.Playing : FlowState.Menu);
        }

        private void StartQueuedDeathIfNeeded()
        {
            if (!deathQueued) return;
            deathQueued = false;
            ReportPlayerDeath();
        }

        private void ChangeState(FlowState next)
        {
            if (State == next) return;
            FlowState previous = State;
            State = next;
            if (config != null && config.verboseLogging)
            {
                Debug.Log("Time Echo Flow: " + previous + " -> " + next, this);
            }
            StateChanged?.Invoke(previous, next);
        }

        private static IEnumerator WaitUnscaled(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
