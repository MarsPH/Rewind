#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TimeEcho.Editor
{
    /// <summary>
    /// Creates a real .prefab on disk once, entirely in the Unity Editor.
    /// Installs that prefab into the selected Gameplay Core prefab or open scene.
    /// Never creates or alters visual UI during Play Mode.
    /// </summary>
    public static class EditableChargeHudInstaller
    {
        private const string GeneratedRoot = "Assets/MahanAssets/Generated";
        private const string HudFolder = GeneratedRoot + "/HUD";
        private const string PrefabPath = HudFolder + "/Editable Ability HUD.prefab";
        private const string BoostSpritePath = "Assets/MahanAssets/Runtime/UI/Resources/TimeEchoHUD/BoostIcon.png";
        private const string StasisSpritePath = "Assets/MahanAssets/Runtime/UI/Resources/TimeEchoHUD/StasisIcon.png";

        [MenuItem("Tools/Time Echo/HUD/Create and Install Editable Charge HUD")]
        public static void CreateAndInstall()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Exit Play Mode", "Install the HUD outside Play Mode so changes persist.", "OK");
                return;
            }

            GameObject prefab = GetOrCreatePrefab(false);
            if (prefab == null) return;

            GameObject selected = Selection.activeGameObject;
            if (selected != null && PrefabUtility.IsPartOfPrefabAsset(selected))
            {
                string selectedPath = AssetDatabase.GetAssetPath(selected);
                if (selectedPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    InstallInPrefabAsset(selectedPath, prefab);
                    return;
                }
            }

            GameHud hud = FindHudInScene(selected);
            if (hud == null)
            {
                EditorUtility.DisplayDialog("Select a Gameplay Core", "Select your Gameplay Core in the Hierarchy or select its .prefab in the Project window, then run this menu again. The prefab file was created at " + PrefabPath, "OK");
                Selection.activeObject = prefab;
                return;
            }

            InstallInHud(hud, prefab, true);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorUtility.DisplayDialog("Editable HUD installed", "HUD added to your selected Game UI. SAVE THE SCENE (Ctrl+S). To edit the shared layout and sprites, open: " + PrefabPath, "OK");
        }

        [MenuItem("Tools/Time Echo/HUD/Create HUD Prefab Only")]
        public static void CreatePrefabOnly()
        {
            GameObject prefab = GetOrCreatePrefab(false);
            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
        }

        [MenuItem("Tools/Time Echo/HUD/Reset HUD Prefab to Default Layout")]
        public static void ResetPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorUtility.DisplayDialog("Reset editable HUD?", "This OVERWRITES all changes made to the shared HUD prefab. Existing scene instance overrides may remain. Continue?", "Reset Prefab", "Cancel"))
                return;

            GameObject prefab = GetOrCreatePrefab(true);
            if (prefab != null) Selection.activeObject = prefab;
        }

        private static GameHud FindHudInScene(GameObject selection)
        {
            if (selection != null)
            {
                GameHud direct = selection.GetComponent<GameHud>();
                if (direct != null) return direct;
                GameHud withinRoot = selection.transform.root.GetComponentInChildren<GameHud>(true);
                if (withinRoot != null) return withinRoot;
            }

            GameHud[] all = UnityEngine.Object.FindObjectsOfType<GameHud>(true);
            return all.Length == 1 ? all[0] : null;
        }

        private static void InstallInPrefabAsset(string path, GameObject hudPrefab)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                GameHud target = contents.GetComponentInChildren<GameHud>(true);
                if (target == null)
                {
                    EditorUtility.DisplayDialog("Wrong prefab", "Selected prefab does not contain GameHud. Select the Gameplay Core prefab instead.", "OK");
                    return;
                }

                InstallInHud(target, hudPrefab, false);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Gameplay Core updated", "The editable HUD is now part of the selected Gameplay Core prefab. Open the new HUD prefab to change position, sprite, colors and spacing: " + PrefabPath, "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void InstallInHud(GameHud hud, GameObject prefab, bool recordUndo)
        {
            AbilityChargeHud existing = hud.GetComponentInChildren<AbilityChargeHud>(true);
            if (existing != null && existing.gameObject != hud.gameObject)
            {
                // Already installed; never overwrite the user's edited visual layout.
                if (recordUndo) Selection.activeGameObject = existing.gameObject;
                Debug.Log("[Time Echo HUD] Already installed. Edit the existing HUD prefab or the scene instance.", existing);
                return;
            }

            // Remove only an obsolete component attached directly to GameHud by the old runtime UI.
            if (existing != null && existing.gameObject == hud.gameObject)
            {
                if (recordUndo) Undo.DestroyObjectImmediate(existing);
                else UnityEngine.Object.DestroyImmediate(existing);
            }

            // A previous Play Mode-generated child should not ordinarily persist, but clean it if saved.
            Transform legacy = hud.transform.Find("Ability Charges (shared energy)");
            if (legacy != null)
            {
                if (recordUndo) Undo.DestroyObjectImmediate(legacy.gameObject);
                else UnityEngine.Object.DestroyImmediate(legacy.gameObject);
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, hud.transform) as GameObject;
            if (instance == null) throw new InvalidOperationException("Failed to instantiate editable HUD prefab.");
            if (recordUndo) Undo.RegisterCreatedObjectUndo(instance, "Install editable charge HUD");

            // Put behind full-screen guidance and letterbox, but above the temporal overlay.
            instance.transform.SetSiblingIndex(Mathf.Min(2, hud.transform.childCount - 1));

            AbilityChargeHud view = instance.GetComponent<AbilityChargeHud>();
            PlayerVitality vitality = hud.transform.root.GetComponentInChildren<PlayerVitality>(true);
            if (view != null && vitality != null)
            {
                SerializedObject serial = new SerializedObject(view);
                serial.FindProperty("vitality").objectReferenceValue = vitality;
                serial.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(view);
                if (PrefabUtility.IsPartOfPrefabInstance(view))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            }

            HideOldBars(hud, recordUndo);
            if (recordUndo) Selection.activeGameObject = instance;
        }

        private static void HideOldBars(GameHud hud, bool recordUndo)
        {
            SerializedObject data = new SerializedObject(hud);
            Image energy = data.FindProperty("vitalityFill").objectReferenceValue as Image;
            Text energyText = data.FindProperty("vitalityText").objectReferenceValue as Text;

            if (energy != null)
            {
                Transform parent = energy.transform.parent;
                GameObject toHide = parent != null && parent != hud.transform &&
                    parent.GetComponent<Canvas>() == null ? parent.gameObject : energy.gameObject;
                SetInactive(toHide, recordUndo);
            }
            if (energyText != null) SetInactive(energyText.gameObject, recordUndo);
        }

        private static void SetInactive(GameObject obj, bool recordUndo)
        {
            if (obj == null || !obj.activeSelf) return;
            if (recordUndo) Undo.RecordObject(obj, "Hide legacy energy bar");
            obj.SetActive(false);
            EditorUtility.SetDirty(obj);
            if (PrefabUtility.IsPartOfPrefabInstance(obj))
                PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        }

        private static GameObject GetOrCreatePrefab(bool overwrite)
        {
            EnsureFolder(GeneratedRoot);
            EnsureFolder(HudFolder);

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null && !overwrite) return existing;

            Sprite boost = AssetDatabase.LoadAssetAtPath<Sprite>(BoostSpritePath);
            Sprite stasis = AssetDatabase.LoadAssetAtPath<Sprite>(StasisSpritePath);
            if (boost == null || stasis == null)
            {
                EditorUtility.DisplayDialog("Missing icon sprites", "Import the two PNGs under Assets/MahanAssets/Runtime/UI/Resources/TimeEchoHUD, then try again.", "OK");
                return null;
            }

            GameObject root = new GameObject("Editable Ability HUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            try
            {
                RectTransform panel = root.GetComponent<RectTransform>();
                SetTopLeft(panel, new Vector2(34f, -32f), new Vector2(470f, 148f));
                Image background = root.GetComponent<Image>();
                background.color = new Color(0.035f, 0.055f, 0.10f, 0.90f);
                background.raycastTarget = false;
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;

                Font font = FindFont();
                MakeText(panel, "Boost Label", "BOOST", font, 20, Color.white, 18, -12, 126, 34);
                MakeText(panel, "Stasis Label", "STASIS", font, 20, Color.white, 18, -57, 126, 34);
                MakeText(panel, "Rewind Label", "REWIND", font, 17, new Color(.74f, .79f, 1f), 18, -109, 126, 25);
                Text free = MakeText(panel, "Free Rewind", "FREE", font, 17, new Color(.46f, .94f, .65f), 385, -109, 65, 25);
                free.alignment = TextAnchor.MiddleRight;

                Image[] boostIcons = MakeRow(panel, "Boost Icons", boost, new Color(1f, .77f, .20f), -15);
                Image[] stasisIcons = MakeRow(panel, "Stasis Icons", stasis, new Color(.34f, .89f, 1f), -60);
                Text boostCounter = MakeText(panel, "Boost Count", "4/4", font, 16, Color.white, 379, -16, 80, 34);
                Text stasisCounter = MakeText(panel, "Stasis Count", "4/4", font, 16, Color.white, 379, -61, 80, 34);
                boostCounter.alignment = TextAnchor.MiddleRight;
                stasisCounter.alignment = TextAnchor.MiddleRight;

                AbilityChargeHud driver = root.AddComponent<AbilityChargeHud>();
                SerializedObject serialized = new SerializedObject(driver);
                SetArray(serialized.FindProperty("boostIcons"), boostIcons);
                SetArray(serialized.FindProperty("stasisIcons"), stasisIcons);
                serialized.FindProperty("boostCount").objectReferenceValue = boostCounter;
                serialized.FindProperty("stasisCount").objectReferenceValue = stasisCounter;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null) throw new InvalidOperationException("Could not save HUD prefab to " + PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void SetArray(SerializedProperty array, Image[] images)
        {
            array.arraySize = images.Length;
            for (int i = 0; i < images.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
        }

        private static Image[] MakeRow(RectTransform panel, string name, Sprite sprite, Color tint, float y)
        {
            RectTransform row = NewRect(name, panel, new Vector2(150, y), new Vector2(212, 34));
            Image[] result = new Image[4];
            for (int i = 0; i < result.Length; i++)
            {
                RectTransform slot = NewRect("Charge " + (i + 1), row, new Vector2(i * 46, 0), new Vector2(35, 34));
                Image plate = slot.gameObject.AddComponent<Image>();
                plate.color = new Color(tint.r * .18f, tint.g * .18f, tint.b * .18f, .9f);
                plate.raycastTarget = false;

                RectTransform glyph = NewRect("Icon (replace sprite here)", slot, new Vector2(4, -3), new Vector2(27, 27));
                Image image = glyph.gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.color = tint;
                image.preserveAspect = true;
                image.raycastTarget = false;
                result[i] = image;
            }
            return result;
        }

        private static Text MakeText(RectTransform parent, string name, string value, Font font, int size, Color tint,
            float x, float y, float width, float height)
        {
            RectTransform rect = NewRect(name, parent, new Vector2(x, y), new Vector2(width, height));
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = tint;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            SetTopLeft(rect, position, size);
            return rect;
        }

        private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Font FindFont()
        {
            // Unity 6 uses LegacyRuntime.ttf; older versions use Arial.ttf.
            foreach (string name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try
                {
                    Font builtin = Resources.GetBuiltinResource<Font>(name);
                    if (builtin != null) return builtin;
                }
                catch (Exception) { /* This built-in name may not exist in this Unity version. */ }
            }

            // Last resort: use an imported source Font asset from this project.
            foreach (string guid in AssetDatabase.FindAssets("t:Font"))
            {
                Font font = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
                if (font != null) return font;
            }
            Debug.LogWarning("[Time Echo HUD] No font found. Assign a Font in the generated HUD prefab's Text components.");
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
