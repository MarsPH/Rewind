using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Play button -> fade out menu UI -> pan camera right -> manga-style panel pages
/// -> reveal a "Start Game" button.
/// Attach to any GameObject (e.g. your Canvas) and assign the fields in the Inspector.
/// </summary>
public class MenuCutsceneController : MonoBehaviour
{
    [System.Serializable]
    public class PanelPage
    {
        [Tooltip("Panels of this page. They appear one by one, in order, then disappear together.")]
        public GameObject[] panels;
    }

    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button startGameButton;

    [Header("Menu UI to fade out (buttons, icons, images, title...)")]
    [Tooltip("Put a CanvasGroup on each element (or on a parent that holds several). Include the play button's group too.")]
    [SerializeField] private CanvasGroup[] uiToFade;
    [SerializeField] private float fadeOutDuration = 1.5f;

    [Header("Camera pan")]
    [SerializeField] private Camera cam;
    [Tooltip("How far the camera travels, in world units.")]
    [SerializeField] private float panDistance = 10f;
    [SerializeField] private float panDuration = 3f;
    [SerializeField] private Vector3 panDirection = Vector3.right;
    [SerializeField] private AnimationCurve panEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Manga pages")]
    [SerializeField] private PanelPage[] pages;
    [SerializeField] private float panelFadeInDuration = 0.4f;
    [Tooltip("Pause after a panel finishes appearing, before the next one starts.")]
    [SerializeField] private float delayBetweenPanels = 0.8f;
    [Tooltip("How long a completed page stays on screen.")]
    [SerializeField] private float pageHoldTime = 2f;
    [SerializeField] private float pageFadeOutDuration = 0.5f;
    [SerializeField] private float gapBetweenPages = 0.3f;

    [Header("Start game")]
    [SerializeField] private float startButtonFadeInDuration = 0.6f;
    [Tooltip("Leave empty to only fire the event below.")]
    [SerializeField] private string gameSceneName;
    public UnityEvent onStartGame;

    private bool playing;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;

        // Hide all panels and the start button until needed.
        foreach (var page in pages)
        {
            foreach (var panel in page.panels)
            {
                GetGroup(panel).alpha = 0f;
                panel.SetActive(false);
            }
        }

        if (startGameButton != null)
        {
            GetGroup(startGameButton.gameObject).alpha = 0f;
            startGameButton.gameObject.SetActive(false);
            startGameButton.onClick.AddListener(OnStartClicked);
        }

        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);
    }

    private void OnPlayClicked()
    {
        if (playing) return;
        playing = true;
        playButton.interactable = false;
        StartCoroutine(CutsceneRoutine());
    }

    private IEnumerator CutsceneRoutine()
    {
        // 1. Fade out the menu UI
        foreach (var g in uiToFade)
        {
            g.interactable = false;
            g.blocksRaycasts = false;
        }
        yield return FadeGroups(uiToFade, 0f, fadeOutDuration);

        // 2. Pan camera
        yield return PanCamera();

        // 3. Manga pages
        foreach (var page in pages)
        {
            // Panels appear one by one
            foreach (var panel in page.panels)
            {
                panel.SetActive(true);
                yield return FadeGroups(new[] { GetGroup(panel) }, 1f, panelFadeInDuration);
                yield return new WaitForSecondsRealtime(delayBetweenPanels);
            }

            yield return new WaitForSecondsRealtime(pageHoldTime);

            // Whole page disappears
            var groups = new CanvasGroup[page.panels.Length];
            for (int i = 0; i < groups.Length; i++) groups[i] = GetGroup(page.panels[i]);
            yield return FadeGroups(groups, 0f, pageFadeOutDuration);
            foreach (var panel in page.panels) panel.SetActive(false);

            yield return new WaitForSecondsRealtime(gapBetweenPages);
        }

        // 4. Show the Start Game button
        if (startGameButton != null)
        {
            var g = GetGroup(startGameButton.gameObject);
            g.alpha = 0f;
            startGameButton.gameObject.SetActive(true);
            yield return FadeGroups(new[] { g }, 1f, startButtonFadeInDuration);
        }
    }

    private IEnumerator PanCamera()
    {
        Transform t = cam.transform;
        Vector3 start = t.position;
        Vector3 end = start + panDirection.normalized * panDistance;

        float elapsed = 0f;
        while (elapsed < panDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = panEase.Evaluate(Mathf.Clamp01(elapsed / panDuration));
            t.position = Vector3.LerpUnclamped(start, end, k);
            yield return null;
        }
        t.position = end;
    }

    private void OnStartClicked()
    {
        startGameButton.interactable = false;
        onStartGame?.Invoke();
        if (!string.IsNullOrEmpty(gameSceneName))
            SceneManager.LoadScene(gameSceneName);
    }

    // ---------- helpers ----------

    private static CanvasGroup GetGroup(GameObject go)
    {
        var g = go.GetComponent<CanvasGroup>();
        if (g == null) g = go.AddComponent<CanvasGroup>();
        return g;
    }

    /// <summary>Fades all groups to the target alpha at the same time.</summary>
    private static IEnumerator FadeGroups(CanvasGroup[] groups, float targetAlpha, float duration)
    {
        if (groups == null || groups.Length == 0) yield break;

        float[] startAlphas = new float[groups.Length];
        for (int i = 0; i < groups.Length; i++) startAlphas[i] = groups[i].alpha;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < groups.Length; i++)
                groups[i].alpha = Mathf.Lerp(startAlphas[i], targetAlpha, k);
            yield return null;
        }

        foreach (var g in groups) g.alpha = targetAlpha;
    }
}