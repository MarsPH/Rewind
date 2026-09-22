using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimeEcho.Flow
{
    public enum FlowState
    {
        Booting,
        Menu,
        Presenting,
        Playing,
        Loading,
        Dead,
        Won
    }

    public enum FlowSequenceStyle
    {
        None,
        SubtitleOnly,
        Cutscene,
        Fade,
        Glitch
    }

    public enum DeathDestinationMode
    {
        RestartCurrent,
        PreviousLevel,
        ChancePreviousOtherwiseRestart,
        FirstLevel,
        MainMenu,
        SpecificScene
    }

    [Serializable]
    public sealed class FlowSequence
    {
        public bool enabled = true;
        public FlowSequenceStyle style = FlowSequenceStyle.Glitch;

        [Header("Game Master")]
        [TextArea(2, 5)] public string guidanceText;
        public AudioClip voiceClip;
        public AudioClip soundEffect;
        [Range(0f, 1f)] public float voiceVolume = 1f;
        [Range(0f, 1f)] public float effectVolume = 1f;

        [Header("Timing (unscaled seconds)")]
        [Min(0f)] public float delayBefore = 0.1f;
        [Min(0f)] public float fadeIn = 0.35f;
        [Min(0f)] public float minimumHold = 0.7f;
        [Min(0f)] public float fadeOut = 0.35f;
        public bool waitForVoiceToFinish = true;

        [Header("Player control")]
        public bool lockGameplay = true;
        public bool allowSkip = false;
        public KeyCode skipKey = KeyCode.Space;

        [Tooltip("For scene-loading transitions: finish the glitch/fade, reveal the new scene, then play the guidance while gameplay continues.")]
        public bool guidanceAfterSceneReveal;

        [Header("Placeholder visuals")]
        public Color coverColor = Color.black;
        [Range(0f, 1f)] public float cutsceneDimAmount = 0.55f;
        [Range(0, 20)] public int glitchBursts = 7;
        public bool showLetterbox = true;

        public float RequiredHold
        {
            get
            {
                float voiceLength = waitForVoiceToFinish && voiceClip != null ? voiceClip.length : 0f;
                return Mathf.Max(minimumHold, voiceLength);
            }
        }

        public bool HasGuidance => !string.IsNullOrWhiteSpace(guidanceText) || voiceClip != null;

    }

    [Serializable]
    public sealed class FlowSequenceOverride
    {
        public bool useOverride;
        public FlowSequence sequence = new FlowSequence();

        public FlowSequence Resolve(FlowSequence fallback)
        {
            return useOverride && sequence != null ? sequence : fallback;
        }
    }

    [Serializable]
    public sealed class DeathRule
    {
        public DeathDestinationMode destination = DeathDestinationMode.ChancePreviousOtherwiseRestart;

        [Tooltip("Used only by ChancePreviousOtherwiseRestart. 0 never goes back; 1 always goes back.")]
        [Range(0f, 1f)] public float previousLevelChance = 0.35f;

        [Tooltip("Used by SpecificScene.")]
        public string specificScene;

        [Tooltip("If PreviousLevel is chosen on the first level, this destination is used instead.")]
        public DeathDestinationMode firstLevelFallback = DeathDestinationMode.RestartCurrent;

        [Tooltip("Normally the highest unlocked level remains saved even when death sends the player backward.")]
        public bool eraseForwardProgressWhenSentBack;
    }

    [Serializable]
    public sealed class DeathRuleOverride
    {
        public bool useOverride;
        public DeathRule rule = new DeathRule();

        public DeathRule Resolve(DeathRule fallback)
        {
            return useOverride && rule != null ? rule : fallback;
        }
    }

    [Serializable]
    public sealed class LevelFlowDefinition
    {
        [Tooltip("Must exactly match the Unity scene asset name, without .unity.")]
        public string sceneName;
        public string displayName;

        [Tooltip("Leave empty to use the next entry in this list.")]
        public string nextSceneOverride;

        public FlowSequenceOverride intro = new FlowSequenceOverride();
        public FlowSequenceOverride nextLevel = new FlowSequenceOverride();
        public FlowSequenceOverride death = new FlowSequenceOverride();
        public FlowSequenceOverride restart = new FlowSequenceOverride();
        public DeathRuleOverride deathRule = new DeathRuleOverride();
        public AmbienceOverride ambience = new AmbienceOverride();
    }

    [Serializable]
    public sealed class AmbienceSettings
    {
        public bool enabled = true;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.65f;
        public bool loop = true;
        [Min(0f)] public float crossfadeSeconds = 1f;
    }

    [Serializable]
    public sealed class AmbienceOverride
    {
        public bool useOverride;
        public AmbienceSettings settings = new AmbienceSettings();

        public AmbienceSettings Resolve(AmbienceSettings fallback)
        {
            return useOverride && settings != null ? settings : fallback;
        }
    }

    [CreateAssetMenu(fileName = ResourceName, menuName = "Time Echo/Progression/Flow Config")]
    public sealed class TimeEchoFlowConfig : ScriptableObject
    {
        public const string ResourceName = "TimeEchoFlowConfig";

        [Header("Scenes")]
        public string menuScene = "MainMenu";
        public string winScene = "Win";
        public List<LevelFlowDefinition> levels = new List<LevelFlowDefinition>();

        [Header("Default sequences")]
        public FlowSequence menuToGame = new FlowSequence();
        public FlowSequence levelIntro = new FlowSequence();
        public FlowSequence nextLevel = new FlowSequence();
        public FlowSequence death = new FlowSequence();
        public FlowSequence restart = new FlowSequence();
        public FlowSequence winning = new FlowSequence();
        public FlowSequence returnToMenu = new FlowSequence();

        [Header("Opening cutscene")]
        [Tooltip("Played after New Game is pressed and before the first level loads. Continue does not replay it.")]
        public OpeningCutsceneAsset openingCutscene;

        [Header("Global return to menu")]
        public bool enableReturnToMenuHotkey = true;
        public KeyCode returnToMenuKey = KeyCode.P;

        [Header("Global level-change sound")]
        public AudioClip levelChangeSound;
        [Range(0f, 1f)] public float levelChangeSoundVolume = 1f;
        public bool playChangeSoundOnNextLevel = true;
        public bool playChangeSoundOnRestart = true;
        public bool playChangeSoundOnNewGame = true;
        public bool playChangeSoundOnMenu;
        public bool playChangeSoundOnWin = true;

        [Header("Persistent ambience")]
        public AmbienceSettings menuAmbience = new AmbienceSettings();
        public AmbienceSettings defaultLevelAmbience = new AmbienceSettings();
        public AmbienceSettings winAmbience = new AmbienceSettings();

        [Header("Death")]
        public DeathRule defaultDeathRule = new DeathRule();
        public bool automaticallyWatchPlayerVitality = true;
        [Min(0f)] public float deathDelay = 0.1f;

        [Header("Intro behavior")]
        public bool playIntroWhenEnteringFromMenu = true;
        public bool playIntroAfterNextLevel = true;
        public bool playIntroAfterRestart = false;
        public bool playIntroAfterDeathSendsPlayerBack = true;

        [Header("Placeholder screens")]
        public bool showGeneratedMainMenu = false;
        public string gameTitle = "TIME ECHO";
        public string newGameLabel = "NEW GAME";
        public string continueLabel = "CONTINUE";
        public string quitLabel = "QUIT";
        public string winTitle = "YOU ESCAPED";
        public string winSubtitle = "The Game Master has lost control.";

        [Header("Saving")]
        public bool saveProgress = true;
        public string playerPrefsKey = "TimeEcho.Flow.Save.v1";
        public bool enableContinueButton = true;

        [Header("Diagnostics")]
        public bool verboseLogging;

        public int FindLevelIndex(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || levels == null)
            {
                return -1;
            }

            for (int i = 0; i < levels.Count; i++)
            {
                LevelFlowDefinition level = levels[i];
                if (level != null && string.Equals(level.sceneName, sceneName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        public string FirstLevelScene => levels != null && levels.Count > 0 && levels[0] != null
            ? levels[0].sceneName
            : string.Empty;

        public LevelFlowDefinition GetLevel(int index)
        {
            return levels != null && index >= 0 && index < levels.Count ? levels[index] : null;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(playerPrefsKey))
            {
                playerPrefsKey = "TimeEcho.Flow.Save.v1";
            }
        }
    }
}
