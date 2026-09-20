#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TimeEcho.Editor
{
    public static class PersistentPuzzleBuilder
    {
        private const string Root = "Assets/TimeEchoGame";
        private const string Generated = Root + "/Generated";
        private const string GeneratedPuzzle = Generated + "/Puzzles/PersistentTriggers";
        private const string BasePrefabs = Generated + "/Prefabs/Base";
        private const string AudioCues = Generated + "/AudioCues";

        private const string PressurePlatePath = BasePrefabs + "/Persistent Pressure Plate 2D.prefab";
        private const string LeverPath = BasePrefabs + "/Persistent Lever 2D.prefab";
        private const string BridgePath = BasePrefabs + "/Persistent Drop Bridge 2D.prefab";
        private const string ExamplePath = BasePrefabs + "/Persistent Drop Bridge Example 2D.prefab";
        private const string PixelPath = GeneratedPuzzle + "/PersistentTriggerPixel.png";

        [MenuItem("Tools/Time Echo/Puzzles/Create or Update Persistent Trigger Assets")]
        public static void CreateOrUpdateAssets()
        {
            if (!CanEditAssets())
            {
                return;
            }

            bool alreadyExists = AssetDatabase.LoadAssetAtPath<GameObject>(PressurePlatePath) != null;
            if (alreadyExists && !EditorUtility.DisplayDialog(
                    "Update persistent puzzle bases?",
                    "This refreshes the package-owned pressure plate, lever, bridge, and example prefabs. Your prefab variants and scene overrides remain separate.",
                    "Update Bases",
                    "Cancel"))
            {
                return;
            }

            EnsureFolders();
            Sprite pixel = GetOrCreatePixelSprite();
            AudioCue activateCue = GetOrCreateCue("PersistentTriggerActivate");
            AudioCue deactivateCue = GetOrCreateCue("PersistentTriggerDeactivate");

            SaveBasePrefab(CreatePressurePlate(pixel, activateCue, deactivateCue), PressurePlatePath);
            SaveBasePrefab(CreateLever(pixel, activateCue, deactivateCue), LeverPath);
            SaveBasePrefab(CreateDropBridge(pixel, "Persistent Drop Bridge 2D"), BridgePath);
            SaveBasePrefab(CreateDropBridgeExample(pixel, activateCue, deactivateCue), ExamplePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateGeneratedAssets();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(ExamplePath);
            EditorUtility.DisplayDialog(
                "Persistent puzzle triggers ready",
                "For the complete test, add 'Persistent Drop Bridge Example 2D' to a scene. For your own puzzle, use the pressure plate or lever prefab and edit its Persistent Action Sequence 2D list.",
                "OK");
        }

        [MenuItem("Tools/Time Echo/Puzzles/Add Persistent Pressure Plate to Open Scene")]
        public static void AddPressurePlateToScene()
        {
            AddPrefabToScene(PressurePlatePath, "Persistent Pressure Plate 2D");
        }

        [MenuItem("Tools/Time Echo/Puzzles/Add Persistent Lever to Open Scene")]
        public static void AddLeverToScene()
        {
            AddPrefabToScene(LeverPath, "Persistent Lever 2D");
        }

        [MenuItem("Tools/Time Echo/Puzzles/Add Drop Bridge Example to Open Scene")]
        public static void AddExampleToScene()
        {
            AddPrefabToScene(ExamplePath, "Persistent Drop Bridge Example 2D");
        }

        private static GameObject CreatePressurePlate(
            Sprite pixel,
            AudioCue activateCue,
            AudioCue deactivateCue)
        {
            GameObject root = new GameObject("Persistent Pressure Plate 2D");
            BoxCollider2D triggerCollider = root.AddComponent<BoxCollider2D>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector2(1.8f, 0.75f);
            triggerCollider.offset = new Vector2(0f, 0.08f);

            PersistentActionSequence2D actions = root.AddComponent<PersistentActionSequence2D>();

            CreateSpriteObject(
                "Base",
                root.transform,
                pixel,
                new Color(0.16f, 0.18f, 0.24f, 1f),
                new Vector2(0f, -0.12f),
                new Vector2(1.75f, 0.3f),
                5);

            GameObject top = CreateSpriteObject(
                "Moving Plate",
                root.transform,
                pixel,
                new Color(0.65f, 0.68f, 0.76f, 1f),
                new Vector2(0f, 0.12f),
                new Vector2(1.4f, 0.22f),
                6);

            SpriteRenderer stateRenderer = top.GetComponent<SpriteRenderer>();
            PersistentTrigger2D trigger = root.AddComponent<PersistentTrigger2D>();
            trigger.Configure(
                PersistentTriggerBehaviour.LatchOnce,
                actions,
                top.transform,
                stateRenderer,
                new Vector2(0f, -0.13f),
                0f,
                new Color(0.65f, 0.68f, 0.76f, 1f),
                new Color(0.25f, 0.92f, 0.56f, 1f),
                activateCue,
                deactivateCue);

            return root;
        }

        private static GameObject CreateLever(
            Sprite pixel,
            AudioCue activateCue,
            AudioCue deactivateCue)
        {
            GameObject root = new GameObject("Persistent Lever 2D");
            CircleCollider2D triggerCollider = root.AddComponent<CircleCollider2D>();
            triggerCollider.isTrigger = true;
            triggerCollider.radius = 0.85f;

            PersistentActionSequence2D actions = root.AddComponent<PersistentActionSequence2D>();

            CreateSpriteObject(
                "Lever Base",
                root.transform,
                pixel,
                new Color(0.18f, 0.2f, 0.28f, 1f),
                Vector2.zero,
                new Vector2(0.58f, 0.58f),
                5);

            GameObject pivot = new GameObject("Moving Lever Pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 0.08f, 0f);

            GameObject handle = CreateSpriteObject(
                "Handle",
                pivot.transform,
                pixel,
                new Color(0.76f, 0.72f, 0.58f, 1f),
                new Vector2(0f, 0.38f),
                new Vector2(0.18f, 0.78f),
                6);

            CreateSpriteObject(
                "Knob",
                handle.transform,
                pixel,
                new Color(0.82f, 0.28f, 0.2f, 1f),
                new Vector2(0f, 0.52f),
                new Vector2(1.9f, 0.42f),
                7);

            SpriteRenderer stateRenderer = handle.GetComponent<SpriteRenderer>();
            PersistentTrigger2D trigger = root.AddComponent<PersistentTrigger2D>();
            trigger.Configure(
                PersistentTriggerBehaviour.ToggleOnEnter,
                actions,
                pivot.transform,
                stateRenderer,
                Vector2.zero,
                -70f,
                new Color(0.76f, 0.72f, 0.58f, 1f),
                new Color(0.25f, 0.92f, 0.56f, 1f),
                activateCue,
                deactivateCue);

            return root;
        }

        private static GameObject CreateDropBridge(Sprite pixel, string objectName)
        {
            GameObject root = new GameObject(objectName);
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.simulated = false;
            body.gravityScale = 1f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(3.4f, 0.42f);

            GameObject visual = CreateSpriteObject(
                "Bridge Visual",
                root.transform,
                pixel,
                new Color(0.44f, 0.31f, 0.25f, 1f),
                Vector2.zero,
                new Vector2(3.4f, 0.42f),
                3);
            visual.GetComponent<SpriteRenderer>().drawMode = SpriteDrawMode.Simple;

            return root;
        }

        private static GameObject CreateDropBridgeExample(
            Sprite pixel,
            AudioCue activateCue,
            AudioCue deactivateCue)
        {
            GameObject root = new GameObject("Persistent Drop Bridge Example 2D");

            GameObject leftPlatform = CreateStaticPlatform(
                "Left Platform",
                root.transform,
                pixel,
                new Vector2(-3f, -0.25f),
                new Vector2(4f, 0.5f));
            GameObject rightPlatform = CreateStaticPlatform(
                "Right Platform",
                root.transform,
                pixel,
                new Vector2(3.5f, -0.25f),
                new Vector2(3f, 0.5f));
            leftPlatform.GetComponent<SpriteRenderer>().sortingOrder = 1;
            rightPlatform.GetComponent<SpriteRenderer>().sortingOrder = 1;

            GameObject plate = CreatePressurePlate(pixel, activateCue, deactivateCue);
            plate.transform.SetParent(root.transform, false);
            plate.transform.localPosition = new Vector3(-3f, 0.15f, 0f);

            GameObject bridge = CreateDropBridge(pixel, "Falling Bridge (Persistent)");
            bridge.transform.SetParent(root.transform, false);
            bridge.transform.localPosition = new Vector3(0.5f, 3.2f, 0f);

            GameObject trapWall = CreateStaticPlatform(
                "Trap Wall (Appears and Persists)",
                root.transform,
                pixel,
                new Vector2(-4.35f, 1.25f),
                new Vector2(0.35f, 2.5f));
            trapWall.GetComponent<SpriteRenderer>().color = new Color(0.46f, 0.16f, 0.2f, 1f);
            trapWall.SetActive(false);

            PersistentActionSequence2D sequence = plate.GetComponent<PersistentActionSequence2D>();
            sequence.ConfigureSteps(new List<PersistentActionStep2D>
            {
                new PersistentActionStep2D
                {
                    label = "Close trap wall",
                    delay = 0f,
                    action = PersistentActionKind.SetObjectActive,
                    targetObject = trapWall,
                    boolValue = true
                },
                new PersistentActionStep2D
                {
                    label = "Drop bridge",
                    delay = 0.15f,
                    action = PersistentActionKind.ReleaseRigidbody,
                    targetBody = bridge.GetComponent<Rigidbody2D>(),
                    floatValue = 1f,
                    releaseConstraints = RigidbodyConstraints2D.FreezeRotation
                }
            });

            return root;
        }

        private static GameObject CreateStaticPlatform(
            string name,
            Transform parent,
            Sprite pixel,
            Vector2 localPosition,
            Vector2 size)
        {
            GameObject platform = CreateSpriteObject(
                name,
                parent,
                pixel,
                new Color(0.22f, 0.24f, 0.3f, 1f),
                localPosition,
                size,
                1);
            BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            return platform;
        }

        private static GameObject CreateSpriteObject(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            Vector2 localPosition,
            Vector2 localScale,
            int sortingOrder)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            gameObject.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return gameObject;
        }

        private static void SaveBasePrefab(GameObject source, string path)
        {
            try
            {
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(source, path);
                if (saved == null)
                {
                    throw new InvalidOperationException("Could not save prefab at " + path + ".");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static void AddPrefabToScene(string prefabPath, string displayName)
        {
            if (!CanEditAssets())
            {
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Persistent puzzle assets not built",
                    "Run Tools > Time Echo > Puzzles > Create or Update Persistent Trigger Assets first.",
                    "OK");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Could not instantiate " + displayName + ".");
            }

            Vector3 position = Vector3.zero;
            if (Selection.activeTransform != null)
            {
                position = Selection.activeTransform.position;
            }
            else if (SceneView.lastActiveSceneView != null)
            {
                position = SceneView.lastActiveSceneView.pivot;
            }
            position.z = 0f;
            instance.transform.position = position;

            Undo.RegisterCreatedObjectUndo(instance, "Add " + displayName);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = instance;
        }

        private static bool CanEditAssets()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return true;
            }

            EditorUtility.DisplayDialog(
                "Exit Play Mode",
                "Persistent puzzle assets can only be created or added outside Play Mode.",
                "OK");
            return false;
        }

        private static void ValidateGeneratedAssets()
        {
            GameObject pressurePlate = AssetDatabase.LoadAssetAtPath<GameObject>(PressurePlatePath);
            GameObject lever = AssetDatabase.LoadAssetAtPath<GameObject>(LeverPath);
            GameObject bridge = AssetDatabase.LoadAssetAtPath<GameObject>(BridgePath);
            GameObject example = AssetDatabase.LoadAssetAtPath<GameObject>(ExamplePath);

            if (pressurePlate == null || pressurePlate.GetComponent<PersistentTrigger2D>() == null)
            {
                throw new InvalidOperationException("Persistent Pressure Plate 2D validation failed.");
            }
            if (lever == null || lever.GetComponent<PersistentTrigger2D>() == null)
            {
                throw new InvalidOperationException("Persistent Lever 2D validation failed.");
            }
            if (bridge == null || bridge.GetComponent<Rigidbody2D>() == null)
            {
                throw new InvalidOperationException("Persistent Drop Bridge 2D validation failed.");
            }
            if (bridge.GetComponent<RewindableRigidbody2D>() != null)
            {
                throw new InvalidOperationException("The persistent bridge must not opt into rewind.");
            }
            if (example == null ||
                example.GetComponentInChildren<PersistentActionSequence2D>() == null)
            {
                throw new InvalidOperationException("Persistent Drop Bridge Example 2D validation failed.");
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder(Generated);
            EnsureFolder(Generated + "/Puzzles");
            EnsureFolder(GeneratedPuzzle);
            EnsureFolder(Generated + "/Prefabs");
            EnsureFolder(BasePrefabs);
            EnsureFolder(AudioCues);
        }

        private static AudioCue GetOrCreateCue(string cueName)
        {
            string path = AudioCues + "/" + cueName + ".asset";
            AudioCue cue = AssetDatabase.LoadAssetAtPath<AudioCue>(path);
            if (cue != null)
            {
                return cue;
            }

            cue = ScriptableObject.CreateInstance<AudioCue>();
            cue.name = cueName;
            AssetDatabase.CreateAsset(cue, path);
            return cue;
        }

        private static Sprite GetOrCreatePixelSprite()
        {
            Sprite foundationPixel = AssetDatabase.LoadAssetAtPath<Sprite>(Generated + "/Pixel.png");
            if (foundationPixel != null)
            {
                return foundationPixel;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PixelPath);
            if (sprite != null)
            {
                return sprite;
            }

            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }
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
    }
}
#endif
