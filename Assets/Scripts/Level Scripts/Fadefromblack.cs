using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fades in everything under this object's CanvasGroup when the scene is entered.
/// Put this on the Canvas (or on a parent that holds all your UI elements).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CanvasFadeIn : MonoBehaviour
{
    [SerializeField] private float delay = 0.2f;
    [SerializeField] private float fadeDuration = 1.5f;

    [Header("First visit only")]
    [Tooltip("If on, the fade only plays the first time this scene is entered.")]
    [SerializeField] private bool onlyFirstVisit = true;
    [Tooltip("If on, 'first time' is remembered even after closing the game. If off, it resets each time the game is launched.")]
    [SerializeField] private bool rememberBetweenSessions = false;

    private static readonly HashSet<string> visitedThisSession = new HashSet<string>();

    private CanvasGroup group;
    private string visitKey;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        visitKey = "FadeInSeen_" + SceneManager.GetActiveScene().name;

        if (onlyFirstVisit && HasVisited())
        {
            group.alpha = 1f;
            enabled = false;
            return;
        }

        // Start hidden so nothing flashes on the first frame.
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void Start()
    {
        if (!enabled) return;
        MarkVisited();
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        yield return new WaitForSecondsRealtime(delay);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    private bool HasVisited()
    {
        return rememberBetweenSessions
            ? PlayerPrefs.GetInt(visitKey, 0) == 1
            : visitedThisSession.Contains(visitKey);
    }

    private void MarkVisited()
    {
        if (rememberBetweenSessions)
        {
            PlayerPrefs.SetInt(visitKey, 1);
            PlayerPrefs.Save();
        }
        else
        {
            visitedThisSession.Add(visitKey);
        }
    }
}