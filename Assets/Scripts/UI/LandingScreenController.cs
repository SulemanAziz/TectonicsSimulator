using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LandingScreenController : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public CameraTransitionController transitionController;
    public GameObject explorerWindowCanvas;

    private const float FadeInDuration = 1.5f;
    private const float FadeOutDuration = 0.8f;

    void Start()
    {
        canvasGroup.alpha = 0f;
        StartCoroutine(FadeInCoroutine());
    }

    /// <summary>Called by the Explore Button's OnClick event in the Inspector.</summary>
    public void OnExploreClicked()
    {
        StartCoroutine(TransitionCoroutine());
    }

    private IEnumerator FadeInCoroutine()
    {
        float elapsed = 0f;
        while (elapsed < FadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / FadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    private IEnumerator TransitionCoroutine()
    {
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;
        while (elapsed < FadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / FadeOutDuration));
            yield return null;
        }
        canvasGroup.alpha = 0f;

        transitionController.BeginFlyIn(OnFlyInComplete);
    }

    private void OnFlyInComplete()
    {
        gameObject.SetActive(false);
        explorerWindowCanvas.SetActive(true);
    }
}
