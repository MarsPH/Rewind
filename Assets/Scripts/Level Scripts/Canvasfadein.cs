using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to your Canvas. On scene start the screen is black, then fades to reveal the scene.
/// Creates its own full-screen black Image, so no extra setup is needed.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class FadeFromBlack : MonoBehaviour
{
    [Tooltip("Time to stay fully black before the fade starts.")]
    [SerializeField] private float holdBlackTime = 0.3f;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private Color coverColor = Color.black;

    private Image cover;

    private void Awake()
    {
        // Build a full-screen black cover as the last (top-most) child of the canvas.
        var go = new GameObject("BlackCover", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        go.transform.SetAsLastSibling();

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        cover = go.GetComponent<Image>();
        cover.color = coverColor;
        cover.raycastTarget = true; // blocks clicks while black
    }

    private void Start()
    {
        StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        yield return new WaitForSecondsRealtime(holdBlackTime);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            Color c = coverColor;
            c.a = Mathf.Lerp(coverColor.a, 0f, Mathf.Clamp01(elapsed / fadeDuration));
            cover.color = c;
            yield return null;
        }

        Destroy(cover.gameObject);
    }
}