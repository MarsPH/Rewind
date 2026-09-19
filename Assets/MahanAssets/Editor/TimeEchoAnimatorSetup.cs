#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TimeEcho.Editor
{
    /// <summary>
    /// Creates the complete player Animator once. Optional state-named folders supply real sprite frames.
    /// Re-running does not overwrite hand-edited Animator graphs or clips for empty sprite folders.
    /// </summary>
    public sealed class TimeEchoAnimatorSetup : EditorWindow
    {
        private const string Output = "Assets/TimeEchoGame/Generated/Animation";
        private const string ControllerPath = Output + "/Player.controller";
        private static readonly string[] States = { "Idle", "Run", "Jump", "Fall", "Boost", "Death", "Rewind" };
        private static readonly HashSet<string> Looping = new HashSet<string> { "Idle", "Run", "Rewind" };

        private GameObject player;
        private DefaultAsset spriteRoot;
        private int framesPerSecond = 12;
        private bool replaceExistingController;

        [MenuItem("Tools/Time Echo/Animation/Setup Player Animator")]
        public static void OpenWindow()
        {
            TimeEchoAnimatorSetup window = GetWindow<TimeEchoAnimatorSetup>("Time Echo Animation");
            window.minSize = new Vector2(445f, 410f);
            window.Show();
        }

        private void OnEnable()
        {
            player = FindPlayer(Selection.activeGameObject);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("TIME ECHO  |  PLAYER ANIMATION", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This builds the actual Animator Controller and 7 clips. Your original gameplay, input, " +
                "boost and rewind scripts are not replaced. Assign a sprite folder to fill the clips automatically.",
                MessageType.Info);

            player = (GameObject)EditorGUILayout.ObjectField("Player (scene or prefab mode)", player, typeof(GameObject), true);
            spriteRoot = (DefaultAsset)EditorGUILayout.ObjectField("Sprite folder (optional)", spriteRoot, typeof(DefaultAsset), false);
            framesPerSecond = EditorGUILayout.IntSlider("Clip samples / second", framesPerSecond, 1, 60);
            replaceExistingController = EditorGUILayout.ToggleLeft(
                "Replace an existing, different Animator Controller on the selected player", replaceExistingController);

            EditorGUILayout.Space(7f);
            EditorGUILayout.LabelField("Folder structure", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(
                "YourSprites/Idle/     YourSprites/Run/     YourSprites/Jump/\n" +
                "YourSprites/Fall/     YourSprites/Boost/     YourSprites/Death/\n" +
                "YourSprites/Rewind/", EditorStyles.textArea, GUILayout.Height(66f));
            EditorGUILayout.LabelField("PNG frames OR a sliced sprite sheet can go inside each folder.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Missing folders use placeholder clips; existing clips remain untouched.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(9f);

            if (player == null || player.GetComponent<PlayerMotor2D>() == null ||
                player.GetComponent<SpriteRenderer>() == null)
            {
                EditorGUILayout.HelpBox("Select the actual Player object with PlayerMotor2D and SpriteRenderer, not Gameplay Core.", MessageType.Warning);
            }

            if (GUILayout.Button("Generate clips + Animator and attach to Player", GUILayout.Height(38f)))
            {
                SetupSelectedPlayer();
            }
            if (GUILayout.Button("Generate assets only (for the shared base prefab)", GUILayout.Height(26f)))
            {
                if (TryFolder(out string root))
                {
                    Sprite fallback = player != null ? player.GetComponent<SpriteRenderer>()?.sprite : null;
                    AnimatorController controller = GetOrCreateController(fallback, root, framesPerSecond);
                    Selection.activeObject = controller;
                    Debug.Log("Time Echo animation: generated assets at " + Output + ". Select a scene Player and use Setup to attach them.");
                }
            }
        }

        private void SetupSelectedPlayer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Exit Play Mode", "Animation setup can only change objects outside Play Mode.", "OK");
                return;
            }
            GameObject target = FindPlayer(player);
            if (target == null || target.GetComponent<SpriteRenderer>() == null)
            {
                EditorUtility.DisplayDialog("Select your Player", "Choose the Player object in the scene or Prefab Mode.", "OK");
                return;
            }
            if (EditorUtility.IsPersistent(target))
            {
                EditorUtility.DisplayDialog("Open the prefab", "Open your Player prefab in Prefab Mode or select a scene instance, then run setup again.", "OK");
                return;
            }
            if (!TryFolder(out string root)) return;

            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            Animator animator = target.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null && !replaceExistingController)
            {
                string existing = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                if (existing != ControllerPath)
                {
                    EditorUtility.DisplayDialog("Animator already configured",
                        "This Player has a different Animator Controller. Enable 'Replace an existing...' in the window to replace it; no changes were made.", "OK");
                    return;
                }
            }

            AnimatorController controller = GetOrCreateController(renderer.sprite, root, framesPerSecond);
            if (animator == null) animator = Undo.AddComponent<Animator>(target);
            Undo.RecordObject(animator, "Assign Time Echo Animator");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; // 2D motion remains under PlayerMotor2D control.
            EditorUtility.SetDirty(animator);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);

            PlayerAnimationDriver driver = target.GetComponent<PlayerAnimationDriver>();
            if (driver == null) driver = Undo.AddComponent<PlayerAnimationDriver>(target);
            Undo.RecordObject(driver, "Connect Time Echo animation driver");
            // Only serialize a TimeDirector belonging to this prefab/Gameplay Core.
            // Standalone Player prefabs can leave it null; the driver resolves it at runtime.
            TimeDirector director = target.transform.parent != null
                ? target.transform.parent.GetComponentInChildren<TimeDirector>(true) : null;
            driver.Configure(animator, renderer, target.GetComponent<PlayerMotor2D>(),
                target.GetComponent<PlayerVitality>(), director);
            EditorUtility.SetDirty(driver);
            PrefabUtility.RecordPrefabInstancePropertyModifications(driver);

            if (root != null && HasSpriteFrames(root))
            {
                Undo.RecordObject(renderer, "Show original sprite colors");
                renderer.color = Color.white;
                EditorUtility.SetDirty(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            AssetDatabase.SaveAssets();
            Selection.activeObject = controller;
            Debug.Log("Time Echo: Animator and PlayerAnimationDriver connected on " + target.name + 
                ". Clips and Controller: " + Output, target);
        }

        private bool TryFolder(out string path)
        {
            path = null;
            if (spriteRoot == null) return true;
            path = AssetDatabase.GetAssetPath(spriteRoot);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
            {
                EditorUtility.DisplayDialog("Choose a folder", "The Sprite Folder must be an existing folder under Assets, not a PNG or a sprite.", "OK");
                return false;
            }
            return true;
        }

        private static GameObject FindPlayer(GameObject candidate)
        {
            if (candidate == null) return null;
            if (candidate.GetComponent<PlayerMotor2D>() != null) return candidate;
            PlayerMotor2D motor = candidate.GetComponentInChildren<PlayerMotor2D>(true);
            return motor != null ? motor.gameObject : null;
        }

        private static bool HasSpriteFrames(string root)
        {
            return States.Any(state => GetFrames(root, state).Count != 0);
        }

        /// <summary>Called by the original prefab builder as well as by the animation window.</summary>
        public static AnimatorController GetOrCreateController(Sprite fallbackSprite, string spriteRoot = null, int fps = 12)
        {
            EnsureFolder(Output);
            fps = Mathf.Clamp(fps, 1, 60);
            var clips = new Dictionary<string, AnimationClip>();
            foreach (string state in States)
            {
                string path = Output + "/Player_" + state + ".anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                bool newClip = clip == null;
                if (newClip)
                {
                    clip = new AnimationClip { name = "Player_" + state };
                    AssetDatabase.CreateAsset(clip, path);
                }
                List<Sprite> frames = GetFrames(spriteRoot, state);
                if (frames.Count != 0)
                {
                    WriteSpriteFrames(clip, frames, fps, Looping.Contains(state));
                }
                else if (newClip && fallbackSprite != null)
                {
                    WriteSpriteFrames(clip, new List<Sprite> { fallbackSprite }, fps, Looping.Contains(state));
                }
                clips.Add(state, clip);
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                BuildGraph(controller, clips);
            }
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void WriteSpriteFrames(AnimationClip clip, List<Sprite> frames, int fps, bool loop)
        {
            // SpriteRenderer belongs to the Player root; never keyframe its Transform or Rigidbody.
            var binding = new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
            var keys = new ObjectReferenceKeyframe[frames.Count + 1];
            for (int i = 0; i < frames.Count; i++)
            {
                keys[i] = new ObjectReferenceKeyframe { time = (float)i / fps, value = frames[i] };
            }
            keys[frames.Count] = new ObjectReferenceKeyframe
            {
                time = (float)frames.Count / fps,
                value = frames[frames.Count - 1]
            };
            clip.frameRate = fps;
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            SerializedObject serialized = new SerializedObject(clip);
            SerializedProperty loopProperty = serialized.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (loopProperty != null)
            {
                loopProperty.boolValue = loop;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(clip);
        }

        private static List<Sprite> GetFrames(string root, string state)
        {
            var results = new List<Sprite>();
            if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root)) return results;
            string folder = AssetDatabase.GetSubFolders(root)
                .FirstOrDefault(path => string.Equals(Path.GetFileName(path), state, StringComparison.OrdinalIgnoreCase));
            if (folder == null) return results;

            var assetPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D t:Sprite", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) assetPaths.Add(path);
            }
            // AssetDatabase filters can miss sprites imported from some sliced textures.
            // Also inspect all non-folder assets inside the chosen state folder.
            foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path)) assetPaths.Add(path);
            }
            foreach (string path in assetPaths)
            {
                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                Array.Sort(sprites, (a, b) => CompareNatural(a.name, b.name));
                results.AddRange(sprites);
            }
            results.Sort((a, b) => CompareNatural(a.name, b.name));
            return results;
        }

        private static int CompareNatural(string left, string right)
        {
            Match a = Regex.Match(left, @"^(.*?)(\d+)$");
            Match b = Regex.Match(right, @"^(.*?)(\d+)$");
            if (a.Success && b.Success)
            {
                int prefix = string.Compare(a.Groups[1].Value, b.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
                if (prefix != 0) return prefix;
                if (long.TryParse(a.Groups[2].Value, out long n1) && long.TryParse(b.Groups[2].Value, out long n2))
                {
                    int numeric = n1.CompareTo(n2);
                    if (numeric != 0) return numeric;
                }
            }
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static void BuildGraph(AnimatorController controller, Dictionary<string, AnimationClip> clips)
        {
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Rewinding", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Launch", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            for (int i = 0; i < States.Length; i++)
            {
                string name = States[i];
                AnimatorState state = machine.AddState(name, new Vector3(220f + (i % 3) * 245f, 60f + (i / 3) * 130f, 0f));
                state.motion = clips[name];
                state.writeDefaultValues = false;
                states.Add(name, state);
            }
            machine.defaultState = states["Idle"];

            var startRun = Add(states["Idle"], states["Run"], false);
            startRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            startRun.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            var stopRun = Add(states["Run"], states["Idle"], false);
            stopRun.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            stopRun.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

            foreach (string groundState in new[] { "Idle", "Run" })
            {
                var jump = Add(states[groundState], states["Jump"], false);
                jump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
                jump.AddCondition(AnimatorConditionMode.Greater, 0.05f, "VerticalSpeed");
                var fall = Add(states[groundState], states["Fall"], false);
                fall.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
                fall.AddCondition(AnimatorConditionMode.Less, 0.05f, "VerticalSpeed");
            }
            Add(states["Jump"], states["Fall"], false).AddCondition(AnimatorConditionMode.Less, 0.05f, "VerticalSpeed");
            foreach (string airborne in new[] { "Jump", "Fall" })
            {
                var idle = Add(states[airborne], states["Idle"], false);
                idle.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
                idle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
                var run = Add(states[airborne], states["Run"], false);
                run.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
                run.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            }
            // Death takes priority over the other Any State transitions.
            var die = machine.AddAnyStateTransition(states["Death"]);
            die.hasExitTime = false; die.duration = 0f; die.canTransitionToSelf = false;
            die.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            Add(states["Death"], states["Idle"], false).AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");

            var rewind = machine.AddAnyStateTransition(states["Rewind"]);
            rewind.hasExitTime = false; rewind.duration = 0f; rewind.canTransitionToSelf = false;
            rewind.AddCondition(AnimatorConditionMode.If, 0f, "Rewinding");
            var rewindExit = Add(states["Rewind"], states["Idle"], false);
            rewindExit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Rewinding");

            var boost = machine.AddAnyStateTransition(states["Boost"]);
            boost.hasExitTime = false; boost.duration = 0f; boost.canTransitionToSelf = false;
            boost.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            boost.AddCondition(AnimatorConditionMode.IfNot, 0f, "Rewinding");
            boost.AddCondition(AnimatorConditionMode.If, 0f, "Launch");
            var boostJump = Add(states["Boost"], states["Jump"], true);
            boostJump.exitTime = 0.9f;
            boostJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            boostJump.AddCondition(AnimatorConditionMode.Greater, 0.05f, "VerticalSpeed");
            var boostFall = Add(states["Boost"], states["Fall"], true);
            boostFall.exitTime = 0.9f;
            boostFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            boostFall.AddCondition(AnimatorConditionMode.Less, 0.05f, "VerticalSpeed");
            var boostRun = Add(states["Boost"], states["Run"], true);
            boostRun.exitTime = 0.9f;
            boostRun.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            boostRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var exitBoost = Add(states["Boost"], states["Idle"], true);
            exitBoost.exitTime = 0.9f; // Fallback if no other locomotion condition matches.

            EditorUtility.SetDirty(controller);
        }

        private static AnimatorStateTransition Add(AnimatorState from, AnimatorState to, bool hasExitTime)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = hasExitTime;
            transition.duration = 0f;
            return transition;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
