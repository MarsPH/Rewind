using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TimeEcho.Visuals
{
    /// <summary>
    /// Draws a second copy of the current animated sprite, but its shader keeps
    /// only pixels close to Target Color. This makes the eyes glow without
    /// applying emission to the cloak or the rest of the character.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SelectiveSpriteGlow2D : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private SpriteRenderer sourceRenderer;

        [Header("Pixel Selection")]
        [SerializeField] private Color targetColor = Color.white;
        [SerializeField, Range(0.01f, 1f)] private float colorTolerance = 0.22f;
        [SerializeField, Range(0.001f, 0.5f)] private float selectionSoftness = 0.06f;
        [SerializeField, Range(0f, 1f)] private float brightnessThreshold = 0.72f;

        [Header("Glow")]
        [SerializeField] private Color glowColor = new Color(0.25f, 0.9f, 1f, 1f);
        [SerializeField, Min(0f)] private float glowIntensity = 5f;
        [SerializeField] private int sortingOrderOffset = 1;

        [Header("Pulse")]
        [SerializeField] private bool pulse = true;
        [SerializeField, Min(0f)] private float pulseSpeed = 2.2f;
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.18f;

        [Header("Animation Exceptions")]
        [Tooltip("The glow is hidden when the current sprite name contains one of these values. " +
                 "The final death frames are excluded because most of those frames are white.")]
        [SerializeField] private string[] suppressWhenSpriteNameContains =
        {
            "Death06",
            "Death07",
            "Death08",
            "Death09"
        };

        [Header("Automatic Bloom")]
        [SerializeField] private bool configureBloomAtRuntime = true;
        [SerializeField, Min(0f)] private float bloomIntensity = 1.15f;
        [SerializeField, Min(0f)] private float bloomThreshold = 0.75f;
        [SerializeField, Range(0f, 1f)] private float bloomScatter = 0.7f;

        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int TargetColorId = Shader.PropertyToID("_TargetColor");
        private static readonly int ColorToleranceId = Shader.PropertyToID("_ColorTolerance");
        private static readonly int SelectionSoftnessId = Shader.PropertyToID("_SelectionSoftness");
        private static readonly int BrightnessThresholdId = Shader.PropertyToID("_BrightnessThreshold");

        private SpriteRenderer overlayRenderer;
        private Material runtimeMaterial;
        private MaterialPropertyBlock propertyBlock;

        private void Reset()
        {
            sourceRenderer = GetComponent<SpriteRenderer>();
        }

        private void Awake()
        {
            EnsureOverlay();
        }

        private void Start()
        {
            if (configureBloomAtRuntime)
            {
                RuntimeBloomSetup.EnsureBloom(
                    Camera.main,
                    bloomIntensity,
                    bloomThreshold,
                    bloomScatter);
            }
        }

        private void OnEnable()
        {
            EnsureOverlay();
            SynchronizeOverlay();
        }

        private void LateUpdate()
        {
            EnsureOverlay();
            SynchronizeOverlay();
        }

        private void OnDisable()
        {
            if (overlayRenderer != null)
                overlayRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }

        private void OnValidate()
        {
            colorTolerance = Mathf.Max(0.01f, colorTolerance);
            selectionSoftness = Mathf.Max(0.001f, selectionSoftness);
            glowIntensity = Mathf.Max(0f, glowIntensity);
            pulseSpeed = Mathf.Max(0f, pulseSpeed);
            bloomIntensity = Mathf.Max(0f, bloomIntensity);
            bloomThreshold = Mathf.Max(0f, bloomThreshold);

            if (sourceRenderer == null)
                sourceRenderer = GetComponent<SpriteRenderer>();
        }

        private void EnsureOverlay()
        {
            if (sourceRenderer == null)
                sourceRenderer = GetComponent<SpriteRenderer>();

            if (overlayRenderer != null && runtimeMaterial != null)
                return;

            Shader glowShader = Resources.Load<Shader>("SelectiveSpriteGlow2D");
            if (glowShader == null)
                glowShader = Shader.Find("TimeEcho/Selective Sprite Glow 2D");

            if (glowShader == null)
            {
                Debug.LogError(
                    "TimeEcho selective glow shader was not found. Reimport the SelectiveCharacterGlow folder.",
                    this);
                enabled = false;
                return;
            }

            var overlayObject = new GameObject("Selective Glow Overlay");
            overlayObject.hideFlags = HideFlags.HideAndDontSave;
            overlayObject.layer = gameObject.layer;
            overlayObject.transform.SetParent(transform, false);

            overlayRenderer = overlayObject.AddComponent<SpriteRenderer>();
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;

            runtimeMaterial = new Material(glowShader)
            {
                name = "TimeEcho Selective Glow (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };

            overlayRenderer.sharedMaterial = runtimeMaterial;
            propertyBlock = new MaterialPropertyBlock();
        }

        private void SynchronizeOverlay()
        {
            if (sourceRenderer == null || overlayRenderer == null)
                return;

            Sprite currentSprite = sourceRenderer.sprite;
            bool shouldRender = sourceRenderer.enabled &&
                                currentSprite != null &&
                                !IsSuppressed(currentSprite.name) &&
                                glowIntensity > 0f;

            overlayRenderer.enabled = shouldRender;
            if (!shouldRender)
                return;

            overlayRenderer.sprite = currentSprite;
            overlayRenderer.flipX = false;
            overlayRenderer.flipY = false;
            overlayRenderer.transform.localPosition = Vector3.zero;
            overlayRenderer.transform.localRotation = Quaternion.identity;
            overlayRenderer.transform.localScale = new Vector3(
                sourceRenderer.flipX ? -1f : 1f,
                sourceRenderer.flipY ? -1f : 1f,
                1f);
            overlayRenderer.drawMode = sourceRenderer.drawMode;
            overlayRenderer.size = sourceRenderer.size;
            overlayRenderer.maskInteraction = sourceRenderer.maskInteraction;
            overlayRenderer.spriteSortPoint = sourceRenderer.spriteSortPoint;
            overlayRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            overlayRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
            overlayRenderer.color = Color.white;
            overlayRenderer.gameObject.layer = gameObject.layer;

            float pulseMultiplier = 1f;
            if (pulse && pulseAmount > 0f)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f);
                pulseMultiplier = Mathf.Lerp(1f - pulseAmount, 1f + pulseAmount, wave);
            }

            propertyBlock.Clear();
            propertyBlock.SetColor(TargetColorId, targetColor);
            propertyBlock.SetColor(GlowColorId, glowColor);
            propertyBlock.SetFloat(GlowIntensityId,
                glowIntensity * pulseMultiplier * sourceRenderer.color.a);
            propertyBlock.SetFloat(ColorToleranceId, colorTolerance);
            propertyBlock.SetFloat(SelectionSoftnessId, selectionSoftness);
            propertyBlock.SetFloat(BrightnessThresholdId, brightnessThreshold);
            overlayRenderer.SetPropertyBlock(propertyBlock);
        }

        private bool IsSuppressed(string spriteName)
        {
            if (suppressWhenSpriteNameContains == null)
                return false;

            foreach (string value in suppressWhenSpriteNameContains)
            {
                if (!string.IsNullOrWhiteSpace(value) &&
                    spriteName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static class RuntimeBloomSetup
        {
            private static Volume runtimeVolume;
            private static VolumeProfile runtimeProfile;

            public static void EnsureBloom(
                Camera targetCamera,
                float intensity,
                float threshold,
                float scatter)
            {
                if (targetCamera == null)
                {
                    Debug.LogWarning(
                        "Selective glow could not configure Bloom because no Main Camera was found.");
                    return;
                }

                targetCamera.allowHDR = true;

                UniversalAdditionalCameraData cameraData =
                    targetCamera.GetComponent<UniversalAdditionalCameraData>();
                if (cameraData == null)
                    cameraData = targetCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

                cameraData.renderPostProcessing = true;

                if (SceneAlreadyHasBloom())
                    return;

                if (runtimeVolume == null)
                {
                    var volumeObject = new GameObject("TimeEcho Selective Glow Bloom");
                    volumeObject.hideFlags = HideFlags.HideAndDontSave;
                    volumeObject.transform.SetParent(targetCamera.transform, false);

                    runtimeVolume = volumeObject.AddComponent<Volume>();
                    runtimeVolume.isGlobal = true;
                    runtimeVolume.priority = -100f;

                    runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                    runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
                    runtimeVolume.sharedProfile = runtimeProfile;

                    Bloom bloom = runtimeProfile.Add<Bloom>(true);
                    bloom.active = true;
                    bloom.intensity.Override(intensity);
                    bloom.threshold.Override(threshold);
                    bloom.scatter.Override(scatter);
                }
            }

            private static bool SceneAlreadyHasBloom()
            {
                Volume[] volumes = UnityEngine.Object.FindObjectsOfType<Volume>(true);
                foreach (Volume volume in volumes)
                {
                    if (volume == null || !volume.enabled || volume.sharedProfile == null)
                        continue;

                    if (volume.sharedProfile.TryGet(out Bloom bloom) && bloom.active)
                        return true;
                }

                return false;
            }
        }
    }
}
