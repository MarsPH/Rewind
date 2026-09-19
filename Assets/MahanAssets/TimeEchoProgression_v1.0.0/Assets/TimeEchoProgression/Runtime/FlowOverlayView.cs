using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TimeEcho.Flow
{
    internal sealed class FlowOverlayView : MonoBehaviour
    {
        private const int GlitchBarCount = 20;

        private CanvasGroup rootGroup;
        private Image cover;
        private RectTransform glitchRoot;
        private readonly Image[] glitchBars = new Image[GlitchBarCount];
        private CanvasGroup subtitleGroup;
        private Text subtitle;
        private Text skipHint;
        private CanvasGroup letterboxGroup;
        private CanvasGroup menuGroup;
        private Text menuTitle;
        private Button newGameButton;
        private Button continueButton;
        private Button quitButton;
        private CanvasGroup winGroup;
        private Text winTitle;
        private Text winSubtitle;
        private AudioSource voiceSource;
        private AudioSource effectSource;
        private Font runtimeFont;
        private int activeGlitchBars = GlitchBarCount;

        public static FlowOverlayView Create(Transform parent)
        {
            GameObject root = new GameObject("TimeEcho Flow UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            FlowOverlayView view = root.AddComponent<FlowOverlayView>();
            view.rootGroup = root.GetComponent<CanvasGroup>();
            view.Build();
            return view;
        }

        private void Build()
        {
            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.ignoreListenerPause = true;
            effectSource = gameObject.AddComponent<AudioSource>();
            effectSource.playOnAwake = false;
            effectSource.ignoreListenerPause = true;

            cover = CreateImage("Cover", transform, Color.black, StretchAll());
            cover.raycastTarget = false;
            cover.gameObject.SetActive(false);

            glitchRoot = CreateRect("Glitch", transform, StretchAll());
            glitchRoot.gameObject.SetActive(false);
            for (int i = 0; i < glitchBars.Length; i++)
            {
                Image bar = CreateImage("Glitch Bar " + (i + 1), glitchRoot, Color.white, new RectLayout());
                bar.raycastTarget = false;
                glitchBars[i] = bar;
            }

            letterboxGroup = CreateGroup("Letterbox", transform, StretchAll());
            CreateImage("Top", letterboxGroup.transform, Color.black, Anchor(0f, 0.88f, 1f, 1f));
            CreateImage("Bottom", letterboxGroup.transform, Color.black, Anchor(0f, 0f, 1f, 0.12f));
            SetGroup(letterboxGroup, false, false);

            subtitleGroup = CreateGroup("Game Master Guidance", transform, Anchor(0.12f, 0.08f, 0.88f, 0.28f));
            Image subtitleBackground = CreateImage("Background", subtitleGroup.transform, new Color(0f, 0f, 0f, 0.78f), StretchAll());
            subtitleBackground.raycastTarget = false;
            subtitle = CreateText("Guidance", subtitleGroup.transform, string.Empty, 34, TextAnchor.MiddleCenter, StretchAll(28f, 18f, 28f, 18f));
            subtitle.fontStyle = FontStyle.Bold;
            skipHint = CreateText("Skip Hint", transform, string.Empty, 20, TextAnchor.LowerRight, Anchor(0.72f, 0.02f, 0.98f, 0.08f));
            skipHint.color = new Color(1f, 1f, 1f, 0.7f);
            SetGroup(subtitleGroup, false, false);
            skipHint.gameObject.SetActive(false);

            BuildMenu();
            BuildWinScreen();
            SetVisualAlpha(0f, Color.black, false);
        }

        private void BuildMenu()
        {
            menuGroup = CreateGroup("Generated Main Menu", transform, StretchAll());
            CreateImage("Background", menuGroup.transform, new Color(0.025f, 0.03f, 0.055f, 1f), StretchAll());
            RectTransform panel = CreateRect("Panel", menuGroup.transform, Anchor(0.31f, 0.18f, 0.69f, 0.82f));
            CreateImage("Panel Background", panel, new Color(0.04f, 0.045f, 0.08f, 0.94f), StretchAll());
            menuTitle = CreateText("Title", panel, "TIME ECHO", 64, TextAnchor.MiddleCenter, Anchor(0.05f, 0.70f, 0.95f, 0.95f));
            menuTitle.fontStyle = FontStyle.Bold;
            newGameButton = CreateButton("New Game", panel, "NEW GAME", Anchor(0.18f, 0.48f, 0.82f, 0.62f));
            continueButton = CreateButton("Continue", panel, "CONTINUE", Anchor(0.18f, 0.30f, 0.82f, 0.44f));
            quitButton = CreateButton("Quit", panel, "QUIT", Anchor(0.18f, 0.12f, 0.82f, 0.26f));

            newGameButton.onClick.AddListener(() => TimeEchoFlowManager.Instance?.StartNewGame());
            continueButton.onClick.AddListener(() => TimeEchoFlowManager.Instance?.ContinueGame());
            quitButton.onClick.AddListener(() => TimeEchoFlowManager.Instance?.QuitGame());
            SetGroup(menuGroup, false, true);
        }

        private void BuildWinScreen()
        {
            winGroup = CreateGroup("Generated Win Screen", transform, StretchAll());
            CreateImage("Background", winGroup.transform, new Color(0.015f, 0.018f, 0.032f, 1f), StretchAll());
            winTitle = CreateText("Win Title", winGroup.transform, "YOU ESCAPED", 70, TextAnchor.MiddleCenter, Anchor(0.1f, 0.58f, 0.9f, 0.78f));
            winTitle.fontStyle = FontStyle.Bold;
            winSubtitle = CreateText("Win Subtitle", winGroup.transform, string.Empty, 30, TextAnchor.MiddleCenter, Anchor(0.18f, 0.45f, 0.82f, 0.58f));
            Button replay = CreateButton("Replay", winGroup.transform, "PLAY AGAIN", Anchor(0.36f, 0.27f, 0.64f, 0.37f));
            Button menu = CreateButton("Menu", winGroup.transform, "MAIN MENU", Anchor(0.36f, 0.15f, 0.64f, 0.25f));
            replay.onClick.AddListener(() => TimeEchoFlowManager.Instance?.StartNewGame());
            menu.onClick.AddListener(() => TimeEchoFlowManager.Instance?.ReturnToMenu());
            SetGroup(winGroup, false, true);
        }

        public void ConfigureMenu(TimeEchoFlowConfig config, bool hasContinue)
        {
            if (config == null) return;
            menuTitle.text = config.gameTitle;
            SetButtonLabel(newGameButton, config.newGameLabel);
            SetButtonLabel(continueButton, config.continueLabel);
            SetButtonLabel(quitButton, config.quitLabel);
            continueButton.gameObject.SetActive(config.enableContinueButton);
            continueButton.interactable = hasContinue;
            winTitle.text = config.winTitle;
            winSubtitle.text = config.winSubtitle;
        }

        public void ShowMenu(bool show)
        {
            SetGroup(menuGroup, show, true);
            rootGroup.blocksRaycasts = show || (winGroup != null && winGroup.interactable) || cover.gameObject.activeSelf;
            if (show)
            {
                SetGroup(winGroup, false, true);
            }
        }

        public void ShowWin(bool show)
        {
            SetGroup(winGroup, show, true);
            rootGroup.blocksRaycasts = show || (menuGroup != null && menuGroup.interactable) || cover.gameObject.activeSelf;
            if (show)
            {
                SetGroup(menuGroup, false, true);
            }
        }

        public void HideScreens()
        {
            SetGroup(menuGroup, false, true);
            SetGroup(winGroup, false, true);
            rootGroup.blocksRaycasts = cover.gameObject.activeSelf;
        }

        public void BeginSequence(FlowSequence sequence)
        {
            if (sequence == null) return;
            activeGlitchBars = Mathf.Clamp(sequence.glitchBursts, 0, GlitchBarCount);
            subtitle.text = sequence.guidanceText ?? string.Empty;
            SetGroup(subtitleGroup, !string.IsNullOrWhiteSpace(sequence.guidanceText), false);
            skipHint.text = sequence.allowSkip ? "Press " + sequence.skipKey + " to skip" : string.Empty;
            skipHint.gameObject.SetActive(sequence.allowSkip);
            SetGroup(letterboxGroup, sequence.showLetterbox && sequence.style == FlowSequenceStyle.Cutscene, false);

            voiceSource.Stop();
            effectSource.Stop();
            if (sequence.voiceClip != null)
            {
                voiceSource.clip = sequence.voiceClip;
                voiceSource.volume = sequence.voiceVolume;
                voiceSource.Play();
            }
            if (sequence.soundEffect != null)
            {
                effectSource.clip = sequence.soundEffect;
                effectSource.volume = sequence.effectVolume;
                effectSource.Play();
            }
        }

        public void EndSequence()
        {
            voiceSource.Stop();
            effectSource.Stop();
            SetGroup(subtitleGroup, false, false);
            SetGroup(letterboxGroup, false, false);
            skipHint.gameObject.SetActive(false);
            glitchRoot.gameObject.SetActive(false);
            cover.gameObject.SetActive(false);
            rootGroup.blocksRaycasts = menuGroup.interactable || winGroup.interactable;
        }

        public IEnumerator AnimateToCover(FlowSequence sequence)
        {
            float target = TargetCoverAlpha(sequence);
            yield return AnimateAlpha(0f, target, sequence != null ? sequence.fadeIn : 0f, sequence);
        }

        public IEnumerator Hold(FlowSequence sequence)
        {
            if (sequence == null) yield break;
            float elapsed = 0f;
            float duration = sequence.RequiredHold;
            while (elapsed < duration)
            {
                if (sequence.allowSkip && WasSkipPressed(sequence.skipKey))
                {
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                UpdateGlitch(sequence, TargetCoverAlpha(sequence));
                yield return null;
            }
        }

        public IEnumerator AnimateFromCover(FlowSequence sequence)
        {
            float source = TargetCoverAlpha(sequence);
            yield return AnimateAlpha(source, 0f, sequence != null ? sequence.fadeOut : 0f, sequence);
        }

        public void ForceCovered(FlowSequence sequence)
        {
            SetVisualAlpha(TargetCoverAlpha(sequence), sequence != null ? sequence.coverColor : Color.black, sequence != null && sequence.style == FlowSequenceStyle.Glitch);
        }

        public void SetLoadingProgress(float progress)
        {
            if (skipHint.gameObject.activeSelf)
            {
                return;
            }

            skipHint.text = "LOADING " + Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f) + "%";
            skipHint.gameObject.SetActive(true);
        }

        private IEnumerator AnimateAlpha(float from, float to, float duration, FlowSequence sequence)
        {
            Color color = sequence != null ? sequence.coverColor : Color.black;
            if (duration <= 0f)
            {
                SetVisualAlpha(to, color, sequence != null && sequence.style == FlowSequenceStyle.Glitch);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (sequence != null && sequence.allowSkip && WasSkipPressed(sequence.skipKey))
                {
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Lerp(from, to, t * t * (3f - 2f * t));
                SetVisualAlpha(alpha, color, sequence != null && sequence.style == FlowSequenceStyle.Glitch);
                yield return null;
            }

            SetVisualAlpha(to, color, sequence != null && sequence.style == FlowSequenceStyle.Glitch);
        }

        private void SetVisualAlpha(float alpha, Color color, bool glitch)
        {
            bool visible = alpha > 0.001f;
            cover.gameObject.SetActive(visible);
            Color coverColor = color;
            coverColor.a = Mathf.Clamp01(alpha);
            cover.color = coverColor;
            rootGroup.blocksRaycasts = visible || menuGroup.interactable || winGroup.interactable;
            UpdateGlitchVisual(glitch, alpha);
        }

        private void UpdateGlitch(FlowSequence sequence, float alpha)
        {
            bool show = sequence != null && sequence.style == FlowSequenceStyle.Glitch;
            UpdateGlitchVisual(show, alpha);
        }

        private void UpdateGlitchVisual(bool requested, float alpha)
        {
            bool show = requested && alpha > 0.02f;
            glitchRoot.gameObject.SetActive(show);
            if (!show) return;

            for (int i = 0; i < glitchBars.Length; i++)
            {
                Image bar = glitchBars[i];
                bar.gameObject.SetActive(i < activeGlitchBars);
                if (i >= activeGlitchBars) continue;
                float height = UnityEngine.Random.Range(4f, 65f);
                float y = UnityEngine.Random.Range(-520f, 520f);
                float x = UnityEngine.Random.Range(-80f, 80f);
                RectTransform rect = bar.rectTransform;
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.offsetMin = new Vector2(x, y - height * 0.5f);
                rect.offsetMax = new Vector2(x, y + height * 0.5f);
                Color tint = i % 3 == 0 ? new Color(0.55f, 0.12f, 0.9f, alpha * 0.45f) :
                    i % 3 == 1 ? new Color(0.05f, 0.85f, 1f, alpha * 0.3f) :
                    new Color(1f, 1f, 1f, alpha * 0.18f);
                bar.color = tint;
            }
        }

        private static float TargetCoverAlpha(FlowSequence sequence)
        {
            if (sequence == null || !sequence.enabled) return 0f;
            switch (sequence.style)
            {
                case FlowSequenceStyle.Fade:
                case FlowSequenceStyle.Glitch:
                    return 1f;
                case FlowSequenceStyle.Cutscene:
                    return Mathf.Clamp01(sequence.cutsceneDimAmount);
                default:
                    return 0f;
            }
        }

        private static bool WasSkipPressed(KeyCode legacyKey)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            if (legacyKey == KeyCode.Escape) return keyboard.escapeKey.wasPressedThisFrame;
            if (legacyKey == KeyCode.Return || legacyKey == KeyCode.KeypadEnter) return keyboard.enterKey.wasPressedThisFrame;
            return keyboard.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(legacyKey);
#endif
        }

        private Image CreateImage(string name, Transform parent, Color color, RectLayout layout)
        {
            RectTransform rect = CreateRect(name, parent, layout);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment, RectLayout layout)
        {
            RectTransform rect = CreateRect(name, parent, layout);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = runtimeFont;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string name, Transform parent, string label, RectLayout layout)
        {
            Image background = CreateImage(name, parent, new Color(0.22f, 0.13f, 0.38f, 1f), layout);
            Button button = background.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.38f, 0.22f, 0.62f, 1f);
            colors.pressedColor = new Color(0.13f, 0.7f, 0.76f, 1f);
            colors.disabledColor = new Color(0.1f, 0.1f, 0.14f, 0.65f);
            button.colors = colors;
            Text text = CreateText("Label", background.transform, label, 28, TextAnchor.MiddleCenter, StretchAll(12f, 5f, 12f, 5f));
            text.fontStyle = FontStyle.Bold;
            return button;
        }

        private CanvasGroup CreateGroup(string name, Transform parent, RectLayout layout)
        {
            RectTransform rect = CreateRect(name, parent, layout);
            return rect.gameObject.AddComponent<CanvasGroup>();
        }

        private static RectTransform CreateRect(string name, Transform parent, RectLayout layout)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = layout.anchorMin;
            rect.anchorMax = layout.anchorMax;
            rect.offsetMin = layout.offsetMin;
            rect.offsetMax = layout.offsetMax;
            return rect;
        }

        private static void SetGroup(CanvasGroup group, bool visible, bool interactive)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible && interactive;
            group.blocksRaycasts = visible && interactive;
            group.gameObject.SetActive(visible);
        }

        private static void SetButtonLabel(Button button, string value)
        {
            Text label = button != null ? button.GetComponentInChildren<Text>() : null;
            if (label != null) label.text = value;
        }

        private static RectLayout StretchAll(float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            return new RectLayout
            {
                anchorMin = Vector2.zero,
                anchorMax = Vector2.one,
                offsetMin = new Vector2(left, bottom),
                offsetMax = new Vector2(-right, -top)
            };
        }

        private static RectLayout Anchor(float xMin, float yMin, float xMax, float yMax)
        {
            return new RectLayout
            {
                anchorMin = new Vector2(xMin, yMin),
                anchorMax = new Vector2(xMax, yMax),
                offsetMin = Vector2.zero,
                offsetMax = Vector2.zero
            };
        }

        private struct RectLayout
        {
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 offsetMin;
            public Vector2 offsetMax;
        }
    }
}
