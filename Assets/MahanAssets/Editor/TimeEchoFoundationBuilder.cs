#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TimeEcho.Editor
{
    public static class TimeEchoFoundationBuilder
    {
        private const string Root = "Assets/TimeEchoGame";
        private const string Generated = Root + "/Generated";
        private const string Demo = Root + "/Demo";
        private const string ScenePath = Demo + "/TimeEchoDemo.unity";
        private const string TuningPath = Generated + "/GameTuning.asset";
        private const string PixelPath = Generated + "/Pixel.png";
        private const string LineMaterialPath = Generated + "/AimLine.mat";
        private const string BasePrefabs = Generated + "/Prefabs/Base";
        private const string GameplayCorePrefabPath = BasePrefabs + "/Gameplay Core.prefab";
        private const string PlatformPrefabPath = BasePrefabs + "/Platform.prefab";
        private const string TimeShardPrefabPath = BasePrefabs + "/Time Shard.prefab";
        private const string SpikeTrapPrefabPath = BasePrefabs + "/Spike Trap.prefab";
        private const string RewindGuidancePrefabPath = BasePrefabs + "/Rewind Guidance Trigger.prefab";

        private static readonly string[] CueNames =
        {
            "RunStep", "KeyboardJump", "AimedLaunch", "Land", "DashBoost", "Hurt", "Death",
            "PowerUp", "Pickup", "RewindStart", "RewindLoop", "RewindEnd", "TickingLoop",
            "StasisEnter", "StasisLoop", "TrapImpact", "Spike", "Boulder", "Fireball",
            "PixelTalk", "ProjectileFire", "ProjectileImpact"
        };

        [MenuItem("Tools/Time Echo/Create Playable Foundation")]
        public static void CreatePlayableFoundation()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog(
                    "Replace demo scene?",
                    "TimeEchoDemo.unity already exists. This replaces the generated demo and refreshes the package-owned base prefabs. Your other scenes and prefab variants stay untouched.",
                    "Replace Demo",
                    "Cancel"))
            {
                return;
            }

            EnsureFolder(Generated);
            EnsureFolder(Generated + "/AudioCues");
            EnsureFolder(BasePrefabs);
            EnsureFolder(Demo);

            GameTuning tuning = GetOrCreateTuning();
            Dictionary<string, AudioCue> cues = GetOrCreateCues();
            Sprite pixel = GetOrCreatePixelSprite();
            Material lineMaterial = GetOrCreateLineMaterial();
            PrefabAssets prefabs = CreateOrUpdateSharedPrefabs(tuning, cues, pixel, lineMaterial);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject gameplayCore = PrefabUtility.InstantiatePrefab(prefabs.GameplayCore, scene) as GameObject;
            Transform playerTransform = gameplayCore != null ? gameplayCore.transform.Find("Player") : null;
            if (gameplayCore == null || playerTransform == null)
            {
                throw new InvalidOperationException("Could not instantiate the validated Gameplay Core prefab.");
            }

            GameObject player = playerTransform.gameObject;

            CreateWorld(prefabs);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = player;

            EditorUtility.DisplayDialog(
                "Time Echo foundation created",
                "Open Assets/TimeEchoGame/Demo/TimeEchoDemo.unity and press Play. Shared base prefabs are in Generated/Prefabs/Base. Create variants for your game instead of editing those bases directly.",
                "Open Demo");
        }

        [MenuItem("Tools/Time Echo/Prefabs/Create or Update Shared Prefabs")]
        public static void UpdateSharedPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Exit Play Mode", "Shared prefabs can only be updated outside Play Mode.", "OK");
                return;
            }

            bool alreadyExists = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayCorePrefabPath) != null;
            if (alreadyExists && !EditorUtility.DisplayDialog(
                    "Update shared base prefabs?",
                    "This refreshes package-owned base prefabs. Prefab instances and variants keep their overrides, while non-overridden properties receive the update.",
                    "Update Bases",
                    "Cancel"))
            {
                return;
            }

            EnsureFolder(Generated);
            EnsureFolder(Generated + "/AudioCues");
            EnsureFolder(BasePrefabs);

            GameTuning tuning = GetOrCreateTuning();
            Dictionary<string, AudioCue> cues = GetOrCreateCues();
            Sprite pixel = GetOrCreatePixelSprite();
            Material lineMaterial = GetOrCreateLineMaterial();
            PrefabAssets prefabs = CreateOrUpdateSharedPrefabs(tuning, cues, pixel, lineMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefabs.GameplayCore;
            EditorUtility.DisplayDialog(
                "Shared prefabs updated",
                "The base prefabs are ready in Assets/TimeEchoGame/Generated/Prefabs/Base.",
                "OK");
        }

        private static PrefabAssets CreateOrUpdateSharedPrefabs(
            GameTuning tuning,
            Dictionary<string, AudioCue> cues,
            Sprite pixel,
            Material lineMaterial)
        {
            PrefabAssets prefabs = new PrefabAssets
            {
                GameplayCore = SaveBasePrefab(BuildGameplayCore(tuning, cues, pixel, lineMaterial), GameplayCorePrefabPath),
                Platform = SaveBasePrefab(BuildPlatform(pixel), PlatformPrefabPath),
                TimeShard = SaveBasePrefab(BuildTimeShard(pixel, cues), TimeShardPrefabPath),
                SpikeTrap = SaveBasePrefab(BuildSpikeTrap(pixel, cues), SpikeTrapPrefabPath),
                RewindGuidance = SaveBasePrefab(BuildRewindGuidance(), RewindGuidancePrefabPath)
            };

            ValidateSharedPrefabs(prefabs);
            return prefabs;
        }

        private static void ValidateSharedPrefabs(PrefabAssets prefabs)
        {
            PlayerMotor2D motor = prefabs.GameplayCore != null
                ? prefabs.GameplayCore.GetComponentInChildren<PlayerMotor2D>(true)
                : null;
            PlayerAnimationDriver animationDriver = prefabs.GameplayCore != null
                ? prefabs.GameplayCore.GetComponentInChildren<PlayerAnimationDriver>(true)
                : null;
            Animator playerAnimator = prefabs.GameplayCore != null
                ? prefabs.GameplayCore.GetComponentInChildren<Animator>(true)
                : null;
            AimedActionController aimedAction = prefabs.GameplayCore != null
                ? prefabs.GameplayCore.GetComponentInChildren<AimedActionController>(true)
                : null;
            GameHud hud = prefabs.GameplayCore != null
                ? prefabs.GameplayCore.GetComponentInChildren<GameHud>(true)
                : null;

            if (prefabs.GameplayCore == null ||
                prefabs.GameplayCore.GetComponentInChildren<GameInput>(true) == null ||
                motor == null ||
                playerAnimator == null || playerAnimator.runtimeAnimatorController == null ||
                animationDriver == null || !HasObjectReference(animationDriver, "animator") ||
                prefabs.GameplayCore.GetComponentInChildren<TimeDirector>(true) == null ||
                hud == null ||
                prefabs.GameplayCore.GetComponentInChildren<StaticLevelCamera2D>(true) == null)
            {
                throw new InvalidOperationException("Gameplay Core prefab validation failed.");
            }

            if (!HasObjectReference(motor, "tuning") ||
                !HasObjectReference(motor, "input") ||
                !HasObjectReference(motor, "timeDirector") ||
                !HasObjectReference(motor, "groundProbe") ||
                aimedAction == null ||
                !HasObjectReference(aimedAction, "motor") ||
                !HasObjectReference(aimedAction, "arrow") ||
                !HasObjectReference(aimedAction, "worldCamera") ||
                !HasObjectReference(hud, "vitality") ||
                !HasObjectReference(hud, "timeDirector") ||
                !HasObjectReference(hud, "vitalityFill") ||
                !HasObjectReference(hud, "timerText"))
            {
                throw new InvalidOperationException("Gameplay Core prefab contains a missing internal reference.");
            }

            if (prefabs.Platform == null || prefabs.Platform.GetComponent<BoxCollider2D>() == null ||
                prefabs.TimeShard == null || prefabs.TimeShard.GetComponent<Collectible>() == null ||
                prefabs.SpikeTrap == null || prefabs.SpikeTrap.GetComponent<DamageDealer2D>() == null ||
                prefabs.RewindGuidance == null || prefabs.RewindGuidance.GetComponent<GuidanceTrigger2D>() == null)
            {
                throw new InvalidOperationException("One or more level-piece prefabs failed validation.");
            }
        }

        private static GameObject BuildGameplayCore(
            GameTuning tuning,
            Dictionary<string, AudioCue> cues,
            Sprite pixel,
            Material lineMaterial)
        {
            GameObject root = new GameObject("Gameplay Core");

            GameObject systems = new GameObject("Game Systems");
            systems.transform.SetParent(root.transform, false);
            GameInput gameInput = systems.AddComponent<GameInput>();
            GameSession session = systems.AddComponent<GameSession>();
            systems.AddComponent<AudioService>();
            TimeDirector timeDirector = systems.AddComponent<TimeDirector>();
            RunTimer runTimer = systems.AddComponent<RunTimer>();
            PresentationDirector presentation = systems.AddComponent<PresentationDirector>();
            AudioStateController audioState = systems.AddComponent<AudioStateController>();

            GameObject player = CreateSpriteObject("Player", pixel, Color.white);
            player.transform.SetParent(root.transform, false);
            player.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            player.transform.localScale = new Vector3(0.8f, 1.05f, 1f);
            Rigidbody2D playerBody = player.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = tuning.movement.style == MovementStyle.Platformer ? tuning.movement.gravityScale : 0f;
            playerBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            playerBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            BoxCollider2D playerCollider = player.AddComponent<BoxCollider2D>();
            playerCollider.size = new Vector2(0.85f, 0.95f);

            PlayerVitality vitality = player.AddComponent<PlayerVitality>();
            PlayerMotor2D motor = player.AddComponent<PlayerMotor2D>();
            player.AddComponent<RewindableRigidbody2D>();
            AimedActionController aimedAction = player.AddComponent<AimedActionController>();
            Animator playerAnimator = player.AddComponent<Animator>();
            playerAnimator.runtimeAnimatorController = TimeEchoAnimatorSetup.GetOrCreateController(pixel);
            playerAnimator.applyRootMotion = false;
            PlayerAnimationDriver animation = player.AddComponent<PlayerAnimationDriver>();
            PlayerAudioFeedback playerAudio = player.AddComponent<PlayerAudioFeedback>();

            GameObject groundProbeObject = new GameObject("Ground Probe");
            groundProbeObject.transform.SetParent(player.transform, false);
            groundProbeObject.transform.localPosition = new Vector3(0f, -0.56f, 0f);

            GameObject arrowObject = new GameObject("Aim Arrow");
            arrowObject.transform.SetParent(player.transform, false);
            LineRenderer shaft = arrowObject.AddComponent<LineRenderer>();
            shaft.sharedMaterial = lineMaterial;
            GameObject headObject = new GameObject("Head");
            headObject.transform.SetParent(arrowObject.transform, false);
            LineRenderer head = headObject.AddComponent<LineRenderer>();
            head.sharedMaterial = lineMaterial;
            AimArrowView arrow = arrowObject.AddComponent<AimArrowView>();
            arrow.Configure(tuning, shaft, head);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            Camera worldCamera = cameraObject.AddComponent<Camera>();
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 5.5f;
            worldCamera.backgroundColor = new Color(0.035f, 0.055f, 0.09f);
            cameraObject.AddComponent<AudioListener>();
            StaticLevelCamera2D staticCamera = cameraObject.AddComponent<StaticLevelCamera2D>();
            cameraObject.transform.position = new Vector3(0f, 1f, -10f);

            vitality.Configure(tuning);
            timeDirector.Configure(tuning, vitality);
            motor.Configure(tuning, gameInput, timeDirector, groundProbeObject.transform);
            aimedAction.Configure(tuning, gameInput, timeDirector, motor, vitality, arrow, worldCamera);
            animation.Configure(playerAnimator, player.GetComponent<SpriteRenderer>(), motor, vitality, timeDirector);
            playerAudio.Configure(motor, vitality, timeDirector);
            staticCamera.Configure(new Vector2(0f, 0.5f), new Vector2(24f, 9f), 0.5f);
            runTimer.Configure(tuning, timeDirector);
            audioState.Configure(timeDirector);
            AssignPlayerAudioCues(playerAudio, cues);

            Canvas canvas = CreateCanvas();
            canvas.transform.SetParent(root.transform, false);
            HudObjects hud = CreateHud(canvas.transform, tuning);
            GameHud gameHud = canvas.gameObject.AddComponent<GameHud>();
            gameHud.Configure(
                tuning,
                vitality,
                timeDirector,
                runTimer,
                session,
                hud.VitalityFill,
                hud.Overlay,
                hud.VitalityText,
                hud.TimerText,
                hud.CollectibleText,
                hud.ModeText);

            GuidanceDirector guidance = hud.GuidanceGroup.gameObject.AddComponent<GuidanceDirector>();
            guidance.Configure(hud.GuidanceGroup, hud.GuidanceText);
            presentation.Configure(hud.LetterboxGroup);
            return root;
        }

        private static GameObject BuildPlatform(Sprite pixel)
        {
            GameObject platform = CreateSpriteObject("Platform", pixel, new Color(0.18f, 0.28f, 0.3f));
            platform.transform.localScale = new Vector3(4f, 0.45f, 1f);
            platform.AddComponent<BoxCollider2D>();
            return platform;
        }

        private static GameObject BuildTimeShard(Sprite pixel, Dictionary<string, AudioCue> cues)
        {
            GameObject collectible = CreateSpriteObject("Time Shard", pixel, new Color(0.35f, 0.95f, 1f));
            collectible.transform.localScale = new Vector3(0.35f, 0.55f, 1f);
            CircleCollider2D collectibleTrigger = collectible.AddComponent<CircleCollider2D>();
            collectibleTrigger.isTrigger = true;
            Collectible collectibleComponent = collectible.AddComponent<Collectible>();
            collectibleComponent.Configure(string.Empty, collectible, collectibleTrigger);
            SetObjectReference(collectibleComponent, "pickupCue", cues["Pickup"]);
            SetString(collectibleComponent, "pickupGuidance", "Time shards restore energy and can be collected again after rewinding past their pickup.");
            return collectible;
        }

        private static GameObject BuildSpikeTrap(Sprite pixel, Dictionary<string, AudioCue> cues)
        {
            GameObject spike = CreateSpriteObject("Spike Trap", pixel, new Color(0.95f, 0.25f, 0.32f));
            spike.transform.localScale = new Vector3(1.2f, 0.5f, 1f);
            BoxCollider2D spikeCollider = spike.AddComponent<BoxCollider2D>();
            spikeCollider.isTrigger = true;
            DamageDealer2D damage = spike.AddComponent<DamageDealer2D>();
            SetFloat(damage, "damage", 25f);
            SetObjectReference(damage, "impactCue", cues["Spike"]);
            return spike;
        }

        private static GameObject BuildRewindGuidance()
        {
            GameObject guidance = new GameObject("Rewind Guidance Trigger");
            BoxCollider2D guidanceCollider = guidance.AddComponent<BoxCollider2D>();
            guidanceCollider.size = new Vector2(2f, 3f);
            guidanceCollider.isTrigger = true;
            GuidanceTrigger2D guidanceTrigger = guidance.AddComponent<GuidanceTrigger2D>();
            SetString(guidanceTrigger, "message", "Hold LMB to aim, then release once the arrow brightens to boost. RMB rewinds; in stasis (LMB + RMB), release either button to boost along the arrow for the same energy cost.");
            return guidance;
        }

        private static GameObject SaveBasePrefab(GameObject source, string path)
        {
            try
            {
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(source, path);
                if (saved == null)
                {
                    throw new InvalidOperationException($"Could not save the shared prefab at {path}.");
                }

                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static void CreateWorld(PrefabAssets prefabs)
        {
            GameObject world = new GameObject("World");

            PlacePrefab(prefabs.Platform, world.transform, "Ground", new Vector2(0f, -1f), new Vector2(24f, 1f));
            PlacePrefab(prefabs.Platform, world.transform, "Platform", new Vector2(-5f, 1f), new Vector2(4f, 0.45f));
            PlacePrefab(prefabs.Platform, world.transform, "Platform", new Vector2(4.5f, 2f), new Vector2(4f, 0.45f));

            GameObject collectible = PlacePrefab(
                prefabs.TimeShard,
                world.transform,
                "Time Shard",
                new Vector2(3.5f, 0.25f),
                new Vector2(0.35f, 0.55f));
            Collectible collectibleComponent = collectible.GetComponent<Collectible>();
            SetString(collectibleComponent, "stableId", "demo-time-shard-01");
            PrefabUtility.RecordPrefabInstancePropertyModifications(collectibleComponent);

            PlacePrefab(
                prefabs.SpikeTrap,
                world.transform,
                "Spike Trap",
                new Vector2(7f, -0.25f),
                new Vector2(1.2f, 0.5f));

            PlacePrefab(
                prefabs.RewindGuidance,
                world.transform,
                "Rewind Guidance Trigger",
                new Vector2(1.5f, 0.5f),
                Vector2.one);
        }

        private static GameObject PlacePrefab(
            GameObject prefab,
            Transform parent,
            string instanceName,
            Vector2 position,
            Vector2 scale)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"Could not instantiate shared prefab {prefab.name}.");
            }

            instance.name = instanceName;
            instance.transform.localPosition = new Vector3(position.x, position.y, 0f);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            return instance;
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject("Game UI", typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static HudObjects CreateHud(Transform canvas, GameTuning tuning)
        {
            HudObjects hud = new HudObjects();

            hud.Overlay = CreateImage(canvas, "Temporal Overlay", Color.clear);
            Stretch(hud.Overlay.rectTransform);
            hud.Overlay.raycastTarget = false;
            hud.Overlay.enabled = false;

            CreateBar(canvas, "Shared Energy Bar", new Vector2(220f, -42f), new Vector2(340f, 24f),
                new Color(0f, 0f, 0f, 0.65f), new Color(0.25f, 0.75f, 1f), out hud.VitalityFill);
            hud.VitalityText = CreateText(canvas, "Energy Label", "ENERGY  100 / 100", 22, TextAnchor.MiddleLeft);
            SetTopLeft(hud.VitalityText.rectTransform, new Vector2(50f, -10f), new Vector2(360f, 32f));

            hud.TimerText = CreateText(canvas, "Run Timer", "00:00.00", 34, TextAnchor.UpperCenter);
            SetTopCenter(hud.TimerText.rectTransform, new Vector2(0f, -24f), new Vector2(300f, 55f));

            hud.CollectibleText = CreateText(canvas, "Collectibles", "SHARDS  0 / 1", 22, TextAnchor.MiddleRight);
            SetTopRight(hud.CollectibleText.rectTransform, new Vector2(-45f, -26f), new Vector2(300f, 42f));

            hud.ModeText = CreateText(canvas, "Time Mode", string.Empty, 42, TextAnchor.MiddleCenter);
            SetCenter(hud.ModeText.rectTransform, new Vector2(0f, 300f), new Vector2(500f, 70f));
            hud.ModeText.color = new Color(0.85f, 0.95f, 1f);

            Text controls = CreateText(
                canvas,
                "Controls",
                "MOVE  WASD / ARROWS     HOLD LMB > AIM > RELEASE TO BOOST     REWIND  RMB     STASIS  LMB + RMB",
                18,
                TextAnchor.MiddleCenter);
            SetBottomCenter(controls.rectTransform, new Vector2(0f, 24f), new Vector2(1200f, 40f));
            controls.color = new Color(1f, 1f, 1f, 0.72f);

            GameObject guidanceObject = new GameObject("Guidance", typeof(RectTransform));
            guidanceObject.transform.SetParent(canvas, false);
            RectTransform guidanceRect = guidanceObject.GetComponent<RectTransform>();
            SetCenter(guidanceRect, new Vector2(0f, -265f), new Vector2(900f, 90f));
            Image guidanceBackground = guidanceObject.AddComponent<Image>();
            guidanceBackground.color = new Color(0.02f, 0.035f, 0.06f, 0.88f);
            hud.GuidanceGroup = guidanceObject.AddComponent<CanvasGroup>();
            hud.GuidanceText = CreateText(guidanceObject.transform, "Guidance Text", string.Empty, 24, TextAnchor.MiddleCenter);
            Stretch(hud.GuidanceText.rectTransform, 22f, 12f);

            GameObject letterboxObject = new GameObject("Letterbox", typeof(RectTransform));
            letterboxObject.transform.SetParent(canvas, false);
            Stretch(letterboxObject.GetComponent<RectTransform>());
            hud.LetterboxGroup = letterboxObject.AddComponent<CanvasGroup>();
            Image topBar = CreateImage(letterboxObject.transform, "Top Bar", Color.black);
            SetTopStretch(topBar.rectTransform, 110f);
            Image bottomBar = CreateImage(letterboxObject.transform, "Bottom Bar", Color.black);
            SetBottomStretch(bottomBar.rectTransform, 110f);
            hud.LetterboxGroup.alpha = 0f;
            hud.LetterboxGroup.blocksRaycasts = false;

            return hud;
        }

        private static void AssignPlayerAudioCues(PlayerAudioFeedback feedback, Dictionary<string, AudioCue> cues)
        {
            SetObjectReference(feedback, "runStep", cues["RunStep"]);
            SetObjectReference(feedback, "keyboardJump", cues["KeyboardJump"]);
            SetObjectReference(feedback, "aimedLaunch", cues["AimedLaunch"]);
            SetObjectReference(feedback, "land", cues["Land"]);
            SetObjectReference(feedback, "dashBoost", cues["DashBoost"]);
            SetObjectReference(feedback, "hurt", cues["Hurt"]);
            SetObjectReference(feedback, "death", cues["Death"]);
            SetObjectReference(feedback, "rewindStart", cues["RewindStart"]);
            SetObjectReference(feedback, "rewindLoop", cues["RewindLoop"]);
            SetObjectReference(feedback, "rewindEnd", cues["RewindEnd"]);
            SetObjectReference(feedback, "tickingLoop", cues["TickingLoop"]);
            SetObjectReference(feedback, "stasisEnter", cues["StasisEnter"]);
            SetObjectReference(feedback, "stasisLoop", cues["StasisLoop"]);
        }

        private static GameTuning GetOrCreateTuning()
        {
            GameTuning tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(TuningPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<GameTuning>();
                AssetDatabase.CreateAsset(tuning, TuningPath);
            }

            tuning.Sanitize();
            EditorUtility.SetDirty(tuning);
            return tuning;
        }

        private static Dictionary<string, AudioCue> GetOrCreateCues()
        {
            Dictionary<string, AudioCue> cues = new Dictionary<string, AudioCue>();
            foreach (string cueName in CueNames)
            {
                string path = $"{Generated}/AudioCues/{cueName}.asset";
                AudioCue cue = AssetDatabase.LoadAssetAtPath<AudioCue>(path);
                if (cue == null)
                {
                    cue = ScriptableObject.CreateInstance<AudioCue>();
                    cue.name = cueName;
                    AssetDatabase.CreateAsset(cue, path);
                }
                cues.Add(cueName, cue);
            }

            return cues;
        }

        private static Sprite GetOrCreatePixelSprite()
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PixelPath);
            if (sprite != null) return sprite;

            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(PixelPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(PixelPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(PixelPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 16f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(PixelPath);
        }

        private static Material GetOrCreateLineMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(LineMaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            material = new Material(shader) { name = "Aim Line" };
            AssetDatabase.CreateAsset(material, LineMaterialPath);
            return material;
        }

        private static GameObject CreateSpriteObject(string name, Sprite sprite, Color color)
        {
            GameObject gameObject = new GameObject(name);
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            return gameObject;
        }

        private static void AddSceneToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(scene => scene.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            string[] pieces = path.Split('/');
            string current = pieces[0];
            for (int i = 1; i < pieces.Length; i++)
            {
                string next = current + "/" + pieces[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, pieces[i]);
                }
                current = next;
            }
        }

        private static Font GetBuiltinFont()
        {
            try
            {
                Font legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (legacy != null) return legacy;
            }
            catch (ArgumentException) { }

            try
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static Text CreateText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.AddComponent<Text>();
            text.font = GetBuiltinFont();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static void CreateBar(Transform parent, string name, Vector2 position, Vector2 size, Color backgroundColor, Color fillColor, out Image fill)
        {
            Image background = CreateImage(parent, name, backgroundColor);
            SetTopLeft(background.rectTransform, position - size * 0.5f, size);
            background.raycastTarget = false;

            fill = CreateImage(background.transform, "Fill", fillColor);
            Stretch(fill.rectTransform, 3f, 3f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect, float horizontalInset = 0f, float verticalInset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalInset, verticalInset);
            rect.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        }

        private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetTopCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetTopRight(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetBottomCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetTopStretch(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -height);
            rect.offsetMax = Vector2.zero;
        }

        private static void SetBottomStretch(RectTransform rect, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, height);
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool HasObjectReference(UnityEngine.Object target, string propertyName)
        {
            if (target == null)
            {
                return false;
            }

            SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);
            return property != null && property.objectReferenceValue != null;
        }

        private static void SetString(UnityEngine.Object target, string propertyName, string value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(UnityEngine.Object target, string propertyName, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class HudObjects
        {
            public Image VitalityFill;
            public Image Overlay;
            public Text VitalityText;
            public Text TimerText;
            public Text CollectibleText;
            public Text ModeText;
            public CanvasGroup GuidanceGroup;
            public Text GuidanceText;
            public CanvasGroup LetterboxGroup;
        }

        private sealed class PrefabAssets
        {
            public GameObject GameplayCore;
            public GameObject Platform;
            public GameObject TimeShard;
            public GameObject SpikeTrap;
            public GameObject RewindGuidance;
        }
    }
}
#endif
