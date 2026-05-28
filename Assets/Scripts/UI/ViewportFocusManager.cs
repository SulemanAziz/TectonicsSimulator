using UnityEngine;
using UnityEngine.UI;

public class ViewportFocusManager : MonoBehaviour
{
    public GameObject flyCamera;

    [Tooltip("The Explore Globe start button — shown when not exploring.")]
    public Button exploreGlobeButton;

    [Tooltip("The Stop Exploring button — shown only while exploring. Assign a red-tinted button.")]
    public Button stopExploreButton;

    private bool isExploring = false;

    void Start()
    {
        RefreshButtons();
    }

    /// <summary>Called when pointer enters the globe viewport area.</summary>
    public void OnPointerEnterViewport()
    {
        // Only auto-enable if actively engaged in exploring
        if (isExploring && flyCamera != null)
            flyCamera.SetActive(true);
    }

    /// <summary>Called when pointer exits the globe viewport area.</summary>
    public void OnPointerExitViewport()
    {
        // Only disable if not in explicit explore mode
        if (!isExploring && flyCamera != null)
            flyCamera.SetActive(false);
    }

    /// <summary>Called by the Explore Globe button. Activates explore mode and keeps FlyCamera on.</summary>
    public void OnExploreGlobeClicked()
    {
        isExploring = true;
        if (flyCamera != null) flyCamera.SetActive(true);
        flyCamera.SetActive(true);
        RefreshButtons();
    }

    /// <summary>Called by the Stop Exploring button. Fully deactivates explore mode.</summary>
    public void OnStopExploreClicked()
    {
        StopExploring();
    }

    /// <summary>Deactivates explore mode unconditionally. Called externally when needed.</summary>
    public void StopExploring()
    {
        isExploring = false;
        if (flyCamera != null) flyCamera.SetActive(false);
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        // Ensure both buttons are always visible at the same time
        if (exploreGlobeButton != null)
            exploreGlobeButton.gameObject.SetActive(true);
        if (stopExploreButton != null)
            stopExploreButton.gameObject.SetActive(true);
    }
}
