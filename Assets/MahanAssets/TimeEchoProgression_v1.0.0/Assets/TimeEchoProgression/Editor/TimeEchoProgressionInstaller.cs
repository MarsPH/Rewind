#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TimeEcho.Flow.Editor
{
    [InitializeOnLoad]
    internal static class TimeEchoProgressionInstaller
    {
        private const string Root = "Assets/TimeEchoProgression";
        private const string ResourcesFolder = Root + "/Resources";
        private const string GeneratedFolder = Root + "/Generated";
        private const string PrefabsFolder = GeneratedFolder + "/Prefabs";
        private const string ConfigPath = ResourcesFolder + "/TimeEchoFlowConfig.asset";

        static TimeEchoProgressionInstaller()
        {
            EditorApplication.delayCall += AutoInstall;
        }

        [MenuItem("Tools/Time Echo/Progression/Install or Repair Flow Assets")]
        public static void InstallOrRepair()
        {
            EnsureFolders();
            TimeEchoFlowConfig config = AssetDatabase.LoadAssetAtPath<TimeEchoFlowConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<TimeEchoFlowConfig>();
                ApplyDefaults(config);
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            CreateExitPrefab();
            CreateSequenceTriggerPrefab();
            CreateMenuActionsPrefab();
            CreateOpeningCutsceneAsset(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            Debug.Log("Time Echo Progression is ready. Edit " + ConfigPath + " and replace the placeholder scene names with your real scene names.", config);
        }

        [MenuItem("Tools/Time Echo/Progression/Select Flow Config")]
        private static void SelectConfig()
        {
            TimeEchoFlowConfig config = AssetDatabase.LoadAssetAtPath<TimeEchoFlowConfig>(ConfigPath);
            if (config == null)
            {
                InstallOrRepair();
                return;
            }
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        [MenuItem("Tools/Time Echo/Progression/Validate Configuration")]
        private static void ValidateConfiguration()
        {
            TimeEchoFlowConfig config = AssetDatabase.LoadAssetAtPath<TimeEchoFlowConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError("No TimeEchoFlowConfig exists. Run Install or Repair Flow Assets first.");
                return;
            }

            HashSet<string> buildScenes = new HashSet<string>();
            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (buildScene.enabled)
                {
                    buildScenes.Add(Path.GetFileNameWithoutExtension(buildScene.path));
                }
            }

            int errors = 0;
            errors += ValidateScene(config.menuScene, "Menu", buildScenes);
            if (!string.IsNullOrWhiteSpace(config.winScene))
            {
                errors += ValidateScene(config.winScene, "Win", buildScenes);
            }

            HashSet<string> duplicates = new HashSet<string>();
            for (int i = 0; i < config.levels.Count; i++)
            {
                LevelFlowDefinition level = config.levels[i];
                string scene = level != null ? level.sceneName : string.Empty;
                errors += ValidateScene(scene, "Level " + (i + 1), buildScenes);
                if (!string.IsNullOrWhiteSpace(scene) && !duplicates.Add(scene))
                {
                    Debug.LogError("Time Echo Flow: scene '" + scene + "' appears more than once in the level list.", config);
                    errors++;
                }
            }

            if (errors == 0)
            {
                Debug.Log("Time Echo Flow validation passed. All configured scenes are unique and enabled in Build Settings.", config);
            }
            else
            {
                Debug.LogError("Time Echo Flow found " + errors + " configuration problem(s). See the messages above.", config);
            }
        }

        private static void AutoInstall()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.LoadAssetAtPath<TimeEchoFlowConfig>(ConfigPath) != null)
            {
                return;
            }
            InstallOrRepair();
        }

        private static void ApplyDefaults(TimeEchoFlowConfig config)
        {
            config.menuScene = "MainMenu";
            config.winScene = "Win";
            config.levels.Clear();
            for (int i = 1; i <= 20; i++)
            {
                config.levels.Add(new LevelFlowDefinition
                {
                    sceneName = "Level_" + i.ToString("00"),
                    displayName = "Map " + i
                });
            }

            Configure(config.menuToGame, FlowSequenceStyle.Fade, string.Empty, 0.3f, 0.25f, 0.3f, false);
            Configure(config.levelIntro, FlowSequenceStyle.Cutscene, "You made it this far. Don't get comfortable.", 0.25f, 1.4f, 0.25f, true);
            Configure(config.nextLevel, FlowSequenceStyle.Glitch, "Oh, you thought that was the end? Let's fix that.", 0.35f, 1.1f, 0.35f, false);
            Configure(config.death, FlowSequenceStyle.Cutscene, "No. That ending was too easy.", 0.2f, 1.2f, 0.2f, true);
            Configure(config.restart, FlowSequenceStyle.Glitch, "Back you go.", 0.25f, 0.65f, 0.25f, false);
            Configure(config.winning, FlowSequenceStyle.Glitch, "No... that was not supposed to happen.", 0.45f, 1.8f, 0.55f, false);
            Configure(config.returnToMenu, FlowSequenceStyle.Fade, string.Empty, 0.25f, 0.25f, 0.25f, false);

            config.nextLevel.guidanceAfterSceneReveal = true;
            config.restart.guidanceAfterSceneReveal = true;

            config.defaultDeathRule.destination = DeathDestinationMode.ChancePreviousOtherwiseRestart;
            config.defaultDeathRule.previousLevelChance = 0.35f;
            config.defaultDeathRule.firstLevelFallback = DeathDestinationMode.RestartCurrent;
            EditorUtility.SetDirty(config);
        }

        private static void Configure(FlowSequence sequence, FlowSequenceStyle style, string text, float fadeIn, float hold, float fadeOut, bool skippable)
        {
            sequence.enabled = true;
            sequence.style = style;
            sequence.guidanceText = text;
            sequence.fadeIn = fadeIn;
            sequence.minimumHold = hold;
            sequence.fadeOut = fadeOut;
            sequence.allowSkip = skippable;
            sequence.lockGameplay = true;
            sequence.showLetterbox = style == FlowSequenceStyle.Cutscene;
            sequence.glitchBursts = style == FlowSequenceStyle.Glitch ? 7 : 0;
        }

        private static int ValidateScene(string sceneName, string label, HashSet<string> buildScenes)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("Time Echo Flow: " + label + " scene name is empty.");
                return 1;
            }
            if (!buildScenes.Contains(sceneName))
            {
                Debug.LogError("Time Echo Flow: " + label + " scene '" + sceneName + "' is not enabled in Build Settings.");
                return 1;
            }
            return 0;
        }

        private static void CreateExitPrefab()
        {
            string path = PrefabsFolder + "/LevelExitTrigger.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            GameObject root = new GameObject("Level Exit Trigger");
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(2f, 3f);
            root.AddComponent<LevelExitTrigger2D>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateSequenceTriggerPrefab()
        {
            string path = PrefabsFolder + "/GameMasterSequenceTrigger.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            GameObject root = new GameObject("Game Master Sequence Trigger");
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(2f, 3f);
            root.AddComponent<GameMasterSequenceTrigger2D>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateMenuActionsPrefab()
        {
            string path = PrefabsFolder + "/CustomMenuActions.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            GameObject root = new GameObject("Custom Menu Actions");
            root.AddComponent<TimeEchoMenuActions>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateOpeningCutsceneAsset(TimeEchoFlowConfig config)
        {
            string path = GeneratedFolder + "/OpeningCutscene.asset";
            OpeningCutsceneAsset cutscene = AssetDatabase.LoadAssetAtPath<OpeningCutsceneAsset>(path);
            if (cutscene == null)
            {
                cutscene = ScriptableObject.CreateInstance<OpeningCutsceneAsset>();
                cutscene.slides.Add(new OpeningCutsceneSlide
                {
                    text = "Something is watching.",
                    duration = 2.5f,
                    backgroundColor = Color.black,
                    textColor = Color.white
                });
                cutscene.slides.Add(new OpeningCutsceneSlide
                {
                    text = "The rules will not stay the same.",
                    duration = 2.5f,
                    backgroundColor = Color.black,
                    textColor = Color.white
                });
                cutscene.slides.Add(new OpeningCutsceneSlide
                {
                    text = "Run.",
                    duration = 1.5f,
                    backgroundColor = Color.black,
                    textColor = Color.white
                });
                AssetDatabase.CreateAsset(cutscene, path);
            }

            if (config.openingCutscene == null)
            {
                config.openingCutscene = cutscene;
                EditorUtility.SetDirty(config);
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "TimeEchoProgression");
            EnsureFolder(Root, "Resources");
            EnsureFolder(Root, "Generated");
            EnsureFolder(GeneratedFolder, "Prefabs");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
#endif
