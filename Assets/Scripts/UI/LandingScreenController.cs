using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LandingScreenController : MonoBehaviour
{
    [Header("Canvas References")]
    public CanvasGroup canvasGroup;
    public CameraTransitionController transitionController;
    public GameObject explorerWindowCanvas;

    [Header("Popup Panels")]
    public GameObject aboutPanel;
    public GameObject helpPanel;
    public GameObject settingsPanel;

    [Header("Audio")]
    public AudioSource backgroundMusicSource;
    public AudioSource sfxSource;
    public AudioClip buttonClickClip;
    public AudioClip panelOpenClip;
    public AudioClip panelCloseClip;

    private const float FadeInDuration  = 1.5f;
    private const float FadeOutDuration = 0.8f;

    void Start()
    {
        canvasGroup.alpha = 0f;

        if (aboutPanel   != null) aboutPanel.SetActive(false);
        if (helpPanel    != null) helpPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (backgroundMusicSource != null && !backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.loop = true;
            backgroundMusicSource.Play();
        }

        StartCoroutine(FadeInCoroutine());
    }

    // ── Primary navigation ──────────────────────────────────────────────────

    /// <summary>Called by the Explore Button's OnClick event.</summary>
    public void OnExploreClicked()
    {
        PlaySfx(buttonClickClip);
        StartCoroutine(TransitionCoroutine());
    }

    // ── Panel controls ──────────────────────────────────────────────────────

    /// <summary>Opens the About panel. Wired to the About Button OnClick.</summary>
    public void OnAboutClicked()
    {
        PlaySfx(panelOpenClip);
        OpenPanel(aboutPanel);
    }

    /// <summary>Opens the Help panel. Wired to the Help Button OnClick.</summary>
    public void OnHelpClicked()
    {
        PlaySfx(panelOpenClip);
        OpenPanel(helpPanel);
    }

    /// <summary>Opens the Settings panel. Wired to the Settings Button OnClick.</summary>
    public void OnSettingsClicked()
    {
        PlaySfx(panelOpenClip);
        OpenPanel(settingsPanel);
    }

    /// <summary>Closes all open panels. Wired to every Close/X button's OnClick.</summary>
    public void OnClosePanel()
    {
        PlaySfx(panelCloseClip);
        CloseAllPanels();
    }

    /// <summary>Sets BGM volume. Wired to the BGM Slider OnValueChanged (dynamic float).</summary>
    public void SetBGMVolume(float value)
    {
        if (backgroundMusicSource != null)
            backgroundMusicSource.volume = Mathf.Clamp01(value);
    }

    /// <summary>Sets SFX volume. Wired to the SFX Slider OnValueChanged (dynamic float).</summary>
    public void SetSFXVolume(float value)
    {
        if (sfxSource != null)
            sfxSource.volume = Mathf.Clamp01(value);
    }

    // ── Internal helpers ────────────────────────────────────────────────────

    private void OpenPanel(GameObject panel)
    {
        if (panel == null) return;
        CloseAllPanels();
        panel.SetActive(true);
    }

    private void CloseAllPanels()
    {
        if (aboutPanel   != null) aboutPanel.SetActive(false);
        if (helpPanel    != null) helpPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip);
    }

    // ── Fade coroutines ─────────────────────────────────────────────────────

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
        canvasGroup.interactable   = false;
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
