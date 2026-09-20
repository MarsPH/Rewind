
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class MahanEffectAnimationGenerator
{
    private class EffectDefinition
    {
        public string folderName;
        public string clipName;
        public string controllerName;
        public bool loop;
        public float samples;
    }

    private static readonly EffectDefinition[] Definitions =
    {
        new EffectDefinition
        {
            folderName = "FireballAnimation",
            clipName = "Fireball",
            controllerName = "Fireball",
            loop = true,
            samples = 12f
        },
        new EffectDefinition
        {
            folderName = "PortalAnimation",
            clipName = "Portal",
            controllerName = "Portal",
            loop = true,
            samples = 10f
        },
        new EffectDefinition
        {
            folderName = "ExplosionAnimation",
            clipName = "Explosion",
            controllerName = "Explosion",
            loop = false,
            samples = 14f
        },
        new EffectDefinition
        {
            folderName = "CircularExplosion",
            clipName = "CircularExplosion",
            controllerName = "CircularExplosion",
            loop = false,
            samples = 14f
        }
    };

    private const string OutputRoot = "Assets/MahanAssets/Generated/Effects";

    [MenuItem("Tools/Mahan/Generate Effect Animations")]
    public static void GenerateEffectAnimations()
    {
        EnsureFolder("Assets/MahanAssets");
        EnsureFolder("Assets/MahanAssets/Generated");
        EnsureFolder(OutputRoot);
        EnsureFolder(OutputRoot + "/Animations");
        EnsureFolder(OutputRoot + "/AnimatorControllers");

        int generatedCount = 0;

        foreach (var definition in Definitions)
        {
            string sourceFolder = FindFolderPath(definition.folderName);
            if (string.IsNullOrEmpty(sourceFolder))
            {
                Debug.LogWarning($"[Effect Animation Generator] Could not find folder '{definition.folderName}'. Import the sprites first.");
                continue;
            }

            List<Sprite> sprites = LoadSprites(sourceFolder);
            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[Effect Animation Generator] No sprites found in '{sourceFolder}'.");
                continue;
            }

            string clipPath = $"{OutputRoot}/Animations/{definition.clipName}.anim";
            string controllerPath = $"{OutputRoot}/AnimatorControllers/{definition.controllerName}.controller";

            CreateClip(clipPath, sprites, definition.samples, definition.loop);
            CreateController(controllerPath, clipPath);

            generatedCount++;
            Debug.Log($"[Effect Animation Generator] Generated {definition.clipName} from {sourceFolder} with {sprites.Count} frames.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Effect Animations", $"Generated {generatedCount} effect animation set(s).\n\nClips: {OutputRoot}/Animations\nControllers: {OutputRoot}/AnimatorControllers", "OK");
    }

    private static void CreateClip(string clipPath, List<Sprite> sprites, float samples, bool loop)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.frameRate = samples;

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = string.Empty,
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / samples,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.stopTime = sprites.Count / samples;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
    }

    private static void CreateController(string controllerPath, string clipPath)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        foreach (var state in stateMachine.states.ToList())
        {
            stateMachine.RemoveState(state.state);
        }

        AnimatorState defaultState = stateMachine.AddState(clip.name);
        defaultState.motion = clip;
        stateMachine.defaultState = defaultState;

        EditorUtility.SetDirty(controller);
    }

    private static List<Sprite> LoadSprites(string folderPath)
    {
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        var sprites = new List<Sprite>();

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    changed = true;
                }
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }
                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort((a, b) => NaturalCompare(a.name, b.name));
        return sprites;
    }

    private static string FindFolderPath(string folderName)
    {
        string[] folderGuids = AssetDatabase.FindAssets(folderName, new[] { "Assets" });
        foreach (string guid in folderGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path) && Path.GetFileName(path) == folderName)
            {
                return path;
            }
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }
        AssetDatabase.CreateFolder(parent, name);
    }

    private static int NaturalCompare(string a, string b)
    {
        return EditorUtility.NaturalCompare(a, b);
    }
}
