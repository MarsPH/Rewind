#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TimeEcho.Editor
{
    public static class ProjectileShooterBuilder
    {
        private const string Root = "Assets/TimeEchoGame";
        private const string Generated = Root + "/Generated";
        private const string GeneratedCombat = Generated + "/Combat/ProjectileShooter";
        private const string Profiles = GeneratedCombat + "/Profiles";
        private const string BasePrefabs = Generated + "/Prefabs/Base";
        private const string AudioCues = Generated + "/AudioCues";

        private const string ShooterProfilePath = Profiles + "/Default Shooter Profile.asset";
        private const string ProjectileProfilePath = Profiles + "/Default Enemy Projectile Profile.asset";
        private const string ShooterPrefabPath = BasePrefabs + "/Projectile Shooter 2D.prefab";
        private const string ProjectilePrefabPath = BasePrefabs + "/Enemy Projectile 2D.prefab";
        private const string FallbackPixelPath = GeneratedCombat + "/ProjectilePixel.png";
        private const string TrailMaterialPath = GeneratedCombat + "/Projectile Trail.mat";

        [MenuItem("Tools/Time Echo/Combat/Create or Update Projectile Shooter Assets")]
        public static void CreateOrUpdateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Exit Play Mode",
                    "Projectile shooter assets can only be built outside Play Mode.",
                    "OK");
                return;
            }

            bool alreadyExists = AssetDatabase.LoadAssetAtPath<GameObject>(ShooterPrefabPath) != null;
            if (alreadyExists && !EditorUtility.DisplayDialog(
                    "Update projectile shooter bases?",
                    "This refreshes the package-owned shooter and projectile base prefabs. Your prefab variants, duplicated profiles, and scene overrides remain separate.",
                    "Update Bases",
                    "Cancel"))
            {
                return;
            }

            EnsureFolders();
            Sprite pixel = GetOrCreatePixelSprite();
            Material trailMaterial = GetOrCreateTrailMaterial();
            AudioCue fireCue = GetOrCreateCue("ProjectileFire");
            AudioCue impactCue = GetOrCreateCue("ProjectileImpact");
            AudioCue windupCue = GetOrCreateCue("ShooterWindup");
            AudioCue acquiredCue = GetOrCreateCue("ShooterTargetAcquired");

            ProjectileProfile2D projectileProfile = GetOrCreateProjectileProfile(pixel, trailMaterial, impactCue);
            ShooterProfile2D shooterProfile = GetOrCreateShooterProfile(fireCue, windupCue, acquiredCue);
            ConfigurableProjectile2D projectilePrefab = BuildProjectilePrefab(projectileProfile, pixel, trailMaterial);
            ConfigurableShooter2D shooterPrefab = BuildShooterPrefab(
                shooterProfile,
                projectileProfile,
                projectilePrefab,
                pixel);

            Validate(projectileProfile, shooterProfile, projectilePrefab, shooterPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = shooterPrefab.gameObject;

            EditorUtility.DisplayDialog(
                "Projectile shooter ready",
                "Drag Assets/TimeEchoGame/Generated/Prefabs/Base/Projectile Shooter 2D.prefab into a level. Duplicate the two profiles or create a prefab variant before changing game-specific art and values.",
                "OK");
        }

        [MenuItem("Tools/Time Echo/Combat/Add Projectile Shooter to Open Scene")]
        public static void AddShooterToOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Exit Play Mode", "Add the shooter outside Play Mode.", "OK");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShooterPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Shooter assets not built",
                    "Run Tools > Time Echo > Combat > Create or Update Projectile Shooter Assets first.",
                    "OK");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Could not instantiate the projectile shooter prefab.");
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

            Undo.RegisterCreatedObjectUndo(instance, "Add Projectile Shooter 2D");
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = instance;
        }

        private static ProjectileProfile2D GetOrCreateProjectileProfile(
            Sprite pixel,
            Material trailMaterial,
            AudioCue impactCue)
        {
            ProjectileProfile2D profile = AssetDatabase.LoadAssetAtPath<ProjectileProfile2D>(ProjectileProfilePath);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<ProjectileProfile2D>();
            profile.name = "Default Enemy Projectile Profile";
            profile.sprite = pixel;
            profile.trailMaterial = trailMaterial;
            profile.impactCue = impactCue;
            profile.Sanitize();
            AssetDatabase.CreateAsset(profile, ProjectileProfilePath);
            return profile;
        }

        private static ShooterProfile2D GetOrCreateShooterProfile(
            AudioCue fireCue,
            AudioCue windupCue,
            AudioCue acquiredCue)
        {
            ShooterProfile2D profile = AssetDatabase.LoadAssetAtPath<ShooterProfile2D>(ShooterProfilePath);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<ShooterProfile2D>();
            profile.name = "Default Shooter Profile";
            profile.fireCue = fireCue;
            profile.windupCue = windupCue;
            profile.targetAcquiredCue = acquiredCue;
            profile.Sanitize();
            AssetDatabase.CreateAsset(profile, ShooterProfilePath);
            return profile;
        }

        private static ConfigurableProjectile2D BuildProjectilePrefab(
            ProjectileProfile2D profile,
            Sprite pixel,
            Material trailMaterial)
        {
            GameObject root = new GameObject("Enemy Projectile 2D");
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = profile.gravityScale;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            CircleCollider2D circle = root.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = profile.circleRadius;
            circle.offset = profile.colliderOffset;

            BoxCollider2D box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = profile.boxSize;
            box.offset = profile.colliderOffset;
            box.enabled = profile.colliderShape == ProjectileColliderShape.Box;
            circle.enabled = profile.colliderShape == ProjectileColliderShape.Circle;

            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.time = profile.trailTime;
            trail.startWidth = profile.trailStartWidth;
            trail.endWidth = profile.trailEndWidth;
            trail.colorGradient = profile.trailColor;
            trail.sharedMaterial = profile.trailMaterial != null ? profile.trailMaterial : trailMaterial;
            trail.enabled = profile.useTrail;

            GameObject visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(root.transform, false);
            visualObject.transform.localScale = new Vector3(profile.visualScale.x, profile.visualScale.y, 1f);
            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sprite = profile.sprite != null ? profile.sprite : pixel;
            renderer.color = profile.tint;
            renderer.sortingLayerName = profile.sortingLayerName;
            renderer.sortingOrder = profile.sortingOrder;

            ConfigurableProjectile2D projectile = root.AddComponent<ConfigurableProjectile2D>();
            projectile.ConfigureReferences(body, visualObject.transform, renderer, circle, box, trail);

            root.SetActive(false);
            GameObject saved = SaveBasePrefab(root, ProjectilePrefabPath);
            return saved.GetComponent<ConfigurableProjectile2D>();
        }

        private static ConfigurableShooter2D BuildShooterPrefab(
            ShooterProfile2D shooterProfile,
            ProjectileProfile2D projectileProfile,
            ConfigurableProjectile2D projectilePrefab,
            Sprite pixel)
        {
            GameObject root = new GameObject("Projectile Shooter 2D");
            BoxCollider2D bodyCollider = root.AddComponent<BoxCollider2D>();
            bodyCollider.size = new Vector2(0.9f, 0.9f);

            GameObject pivotObject = new GameObject("Aim Pivot");
            pivotObject.transform.SetParent(root.transform, false);

            CreateSpriteObject(
                "Body",
                pivotObject.transform,
                pixel,
                new Color(0.55f, 0.12f, 0.16f, 1f),
                Vector2.zero,
                new Vector2(0.85f, 0.85f),
                2);

            CreateSpriteObject(
                "Barrel",
                pivotObject.transform,
                pixel,
                new Color(0.95f, 0.28f, 0.18f, 1f),
                new Vector2(0.48f, 0f),
                new Vector2(0.85f, 0.22f),
                3);

            GameObject muzzleObject = new GameObject("Muzzle");
            muzzleObject.transform.SetParent(pivotObject.transform, false);
            muzzleObject.transform.localPosition = new Vector3(0.92f, 0f, 0f);

            ConfigurableShooter2D shooter = root.AddComponent<ConfigurableShooter2D>();
            shooter.Configure(
                shooterProfile,
                projectileProfile,
                projectilePrefab,
                pivotObject.transform,
                muzzleObject.transform);

            GameObject saved = SaveBasePrefab(root, ShooterPrefabPath);
            return saved.GetComponent<ConfigurableShooter2D>();
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

        private static GameObject SaveBasePrefab(GameObject source, string path)
        {
            try
            {
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(source, path);
                if (saved == null)
                {
                    throw new InvalidOperationException($"Could not save the base prefab at {path}.");
                }
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static void Validate(
            ProjectileProfile2D projectileProfile,
            ShooterProfile2D shooterProfile,
            ConfigurableProjectile2D projectilePrefab,
            ConfigurableShooter2D shooterPrefab)
        {
            if (projectileProfile == null || shooterProfile == null)
            {
                throw new InvalidOperationException("Projectile shooter profile generation failed.");
            }
            if (projectilePrefab == null || !projectilePrefab.HasRequiredReferences)
            {
                throw new InvalidOperationException("Enemy Projectile 2D prefab validation failed.");
            }
            if (shooterPrefab == null || !shooterPrefab.HasRequiredReferences)
            {
                throw new InvalidOperationException("Projectile Shooter 2D prefab validation failed.");
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder(Generated);
            EnsureFolder(Generated + "/Combat");
            EnsureFolder(GeneratedCombat);
            EnsureFolder(Profiles);
            EnsureFolder(Generated + "/Prefabs");
            EnsureFolder(BasePrefabs);
            EnsureFolder(AudioCues);
        }

        private static AudioCue GetOrCreateCue(string cueName)
        {
            string path = $"{AudioCues}/{cueName}.asset";
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

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FallbackPixelPath);
            if (sprite != null)
            {
                return sprite;
            }

            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(FallbackPixelPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(FallbackPixelPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(FallbackPixelPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 16f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(FallbackPixelPath);
        }

        private static Material GetOrCreateTrailMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                throw new InvalidOperationException("Could not find a compatible shader for the projectile trail.");
            }

            material = new Material(shader) { name = "Projectile Trail" };
            AssetDatabase.CreateAsset(material, TrailMaterialPath);
            return material;
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
