using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the Advanced Settings overlay panel. Sliders here are deferred —
/// values are only written to the Planet and the mesh only rebuilt when the
/// user explicitly clicks Apply.
/// </summary>
public class AdvancedSettingsPanelController : MonoBehaviour
{
    [Header("Simulation Reference")]
    public Planet planet;

    [Header("Sliders")]
    public Slider oceanElevSlider;
    public Slider topoElevSlider;
    public Slider precisionSlider;
    [Header("Value Labels")]
    public Text oceanElevValueLabel;
    public Text topoElevValueLabel;
    public Text precisionValueLabel;


    [Header("Buttons")]
    public Button applyButton;
    public Button closeButton;

    // Captured on Awake so ResetToDefaults can restore them
    private float defaultOceanElev;
    private float defaultTopoElev;
    private int   defaultPrecision;


    void Awake()
    {
        // Capture defaults before anything mutates them
        if (planet != null)
        {
            defaultOceanElev  = planet.OceanElevation;
            defaultTopoElev   = planet.TopographyElevation;
            // Refactored: GridResolutionPerDegree replaces PlatePrecisionFactor
            defaultPrecision  = planet.GridResolutionPerDegree;
        }
    }

    void Start()
    {
        if (planet == null)
        {
            Debug.LogWarning("[AdvancedSettingsPanelController] Planet reference is not assigned.", this);
        }

        InitSliders();

        applyButton?.onClick.AddListener(OnApplyClicked);
        closeButton?.onClick.AddListener(Hide);

        BindSlider(oceanElevSlider,  oceanElevValueLabel,  false);
        BindSlider(topoElevSlider,   topoElevValueLabel,   false);
        BindSlider(precisionSlider,  precisionValueLabel,  true);

        // Panel starts hidden
        Hide();
    }

    /// <summary>Opens the Advanced Settings panel.</summary>
    public void Show()
    {
        gameObject.SetActive(true);
    }

    /// <summary>Closes the Advanced Settings panel without applying changes.</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Writes slider values to the Planet and triggers a single mesh rebuild,
    /// then hides the panel.
    /// </summary>
    public void OnApplyClicked()
    {
        if (planet == null) { Hide(); return; }

        planet.OceanElevation        = oceanElevSlider  != null ? oceanElevSlider.value  : defaultOceanElev;
        planet.TopographyElevation   = topoElevSlider   != null ? topoElevSlider.value   : defaultTopoElev;
        // Refactored: GridResolutionPerDegree replaces PlatePrecisionFactor
        planet.GridResolutionPerDegree = precisionSlider != null ? Mathf.RoundToInt(precisionSlider.value) : defaultPrecision;

        planet.Init();
        planet.GenerateMesh();

        Hide();
    }

    /// <summary>
    /// Resets all sliders to the provided default values and writes them back
    /// to the Planet immediately. Called by ExplorerWindowController when the
    /// user clicks Default Settings.
    /// </summary>
    public void ResetToDefaults(float ocean, float topo, int precision)
    {
        SetSlider(oceanElevSlider,  oceanElevValueLabel,  ocean,     false);
        SetSlider(topoElevSlider,   topoElevValueLabel,   topo,      false);
        SetSlider(precisionSlider,  precisionValueLabel,  precision, true);

        if (planet == null) return;

        planet.OceanElevation        = ocean;
        planet.TopographyElevation   = topo;
        // Refactored: GridResolutionPerDegree replaces PlatePrecisionFactor
        planet.GridResolutionPerDegree = precision;
    }

    // ── Internal helpers ────────────────────────────────────────────────────

    private void InitSliders()
    {
        if (planet == null) return;
        SetSlider(oceanElevSlider,  oceanElevValueLabel,  planet.OceanElevation,       false);
        SetSlider(topoElevSlider,   topoElevValueLabel,   planet.TopographyElevation,  false);
        SetSlider(precisionSlider,  precisionValueLabel,  planet.GridResolutionPerDegree, true);
    }

    private void SetSlider(Slider slider, Text label, float value, bool wholeNumber)
    {
        slider?.SetValueWithoutNotify(value);
        if (label != null)
            label.text = wholeNumber ? Mathf.RoundToInt(value).ToString() : value.ToString("F2");
    }

    private void BindSlider(Slider slider, Text label, bool wholeNumber)
    {
        if (slider == null) return;
        slider.onValueChanged.AddListener(v =>
        {
            if (label != null)
                label.text = wholeNumber ? Mathf.RoundToInt(v).ToString() : v.ToString("F2");
        });
    }
}
