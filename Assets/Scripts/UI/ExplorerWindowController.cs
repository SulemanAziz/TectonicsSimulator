using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ExplorerWindowController : MonoBehaviour
{
    [Header("Simulation References")]
    public Planet planet;
    public GlobeRotation globeRotation;
    public Camera globeCamera;
    public ViewportFocusManager viewportFocusManager;
    public RawImage globeViewportImage;

    [Header("Top Toolbar")]
    public Toggle rotateToggle;
    public Toggle platesToggle;

    [Header("Left Panel — Sliders")]
    public Slider speedSlider;
    public Slider resolutionSlider;
    public Slider tiltSlider;

    [Header("Left Panel — Value Labels")]
    public Text speedValueLabel;
    public Text resolutionValueLabel;
    public Text tiltValueLabel;

    [Header("Left Panel — Buttons")]
    public Button defaultSettingsButton;

    [Header("Advanced Settings")]
    public AdvancedSettingsPanelController advancedSettingsPanel;

    [Header("Bottom Bar")]
    public Button zoomInButton;
    public Button zoomOutButton;
    public Button resetButton;
    public Button exploreGlobeButton;
    public Button stopExploreButton;
    public Text camPositionLabel;

    [Header("Zoom Settings")]
    public float zoomStep = 5f;
    public float minFov = 0.1f;
    public float maxFov = 90f;

    private float defaultSpeed;
    private int   defaultResolution;
    private float defaultTilt;

    // Advanced settings defaults — captured once at startup before any Apply
    private float defaultOceanElev;
    private float defaultTopoElev;
    private int   defaultPrecision;
    private float defaultTolerance;

    private Vector3 defaultCamPosition;
    private Quaternion defaultCamRotation;
    private float defaultFov;

    void Start()
    {
        if (globeCamera != null)
        {
            defaultCamPosition = globeCamera.transform.position;
            defaultCamRotation = globeCamera.transform.rotation;
            defaultFov = globeCamera.fieldOfView;
        }

        CaptureSimulationDefaults();
        InitializeControlValues();
        BindControls();
        SetupViewportPointerEvents();
    }

    private void CaptureSimulationDefaults()
    {
        if (planet == null || globeRotation == null) return;
        defaultSpeed      = globeRotation.Speed;
        defaultResolution = planet.resolution;
        defaultTilt       = globeRotation.tiltAngle;

        defaultOceanElev  = planet.OceanElevation;
        defaultTopoElev   = planet.TopographyElevation;
        defaultPrecision  = planet.GridResolutionPerDegree;
        defaultTilt       = globeRotation.tiltAngle;
    }

    private void InitializeControlValues()
    {
        if (planet == null || globeRotation == null) return;
        rotateToggle?.SetIsOnWithoutNotify(globeRotation.EnableRotation);
        platesToggle?.SetIsOnWithoutNotify(planet.ShowPlates);

        SetSlider(speedSlider,      speedValueLabel,      globeRotation.Speed, true);
        SetSlider(resolutionSlider, resolutionValueLabel, planet.resolution,   true);
        SetSlider(tiltSlider,       tiltValueLabel,       globeRotation.tiltAngle, false);
    }

    private void SetSlider(Slider slider, Text label, float value, bool wholeNumber)
    {
        slider?.SetValueWithoutNotify(value);
        if (label != null)
            label.text = wholeNumber ? Mathf.RoundToInt(value).ToString() : value.ToString("F2");
    }

    private void BindControls()
    {
        rotateToggle?.onValueChanged.AddListener(v => globeRotation.EnableRotation = v);
        platesToggle?.onValueChanged.AddListener(v =>
        {
            planet.TogglePlateRendering(v);
            planet.ShowPlates = v;
        });

        BindSlider(speedSlider,      speedValueLabel,      true,  v => globeRotation.Speed = Mathf.RoundToInt(v));
        BindSlider(resolutionSlider, resolutionValueLabel, true,  v => planet.resolution = Mathf.RoundToInt(v), rebuild: true);
        // Note: Advanced sliders (ocean, topo, precision, tolerance) are bound in AdvancedSettingsPanelController, not here.
        BindSlider(tiltSlider,       tiltValueLabel,       false, v => globeRotation.tiltAngle = v);

        defaultSettingsButton?.onClick.AddListener(OnDefaultSettings);
        zoomInButton?.onClick.AddListener(OnZoomIn);
        zoomOutButton?.onClick.AddListener(OnZoomOut);
        resetButton?.onClick.AddListener(OnReset);
        exploreGlobeButton?.onClick.AddListener(() => viewportFocusManager?.OnExploreGlobeClicked());
        stopExploreButton?.onClick.AddListener(() => viewportFocusManager?.OnStopExploreClicked());
    }

    private void BindSlider(Slider slider, Text label, bool wholeNumber, System.Action<float> onChanged, bool rebuild = false)
    {
        if (slider == null) return;
        slider.onValueChanged.AddListener(v =>
        {
            onChanged(v);
            if (label != null)
                label.text = wholeNumber ? Mathf.RoundToInt(v).ToString() : v.ToString("F2");
        });
        if (rebuild) AddPointerUpRebuild(slider);
    }

    private void AddPointerUpRebuild(Slider slider)
    {
        if (slider == null) return;
        EventTrigger trigger = slider.gameObject.GetComponent<EventTrigger>()
                            ?? slider.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        entry.callback.AddListener(_ => RebuildPlanetMesh());
        trigger.triggers.Add(entry);
    }

    private void SetupViewportPointerEvents()
    {
        if (globeViewportImage == null || viewportFocusManager == null) return;
        EventTrigger trigger = globeViewportImage.gameObject.GetComponent<EventTrigger>()
                            ?? globeViewportImage.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => viewportFocusManager.OnPointerEnterViewport());
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => viewportFocusManager.OnPointerExitViewport());
        trigger.triggers.Add(exit);
    }

    private void RebuildPlanetMesh()
    {
        if (planet == null) return;
        planet.Init();
        planet.GenerateMesh();
    }

    private void OnDefaultSettings()
    {
        if (planet == null || globeRotation == null) return;
        globeRotation.Speed           = Mathf.RoundToInt(defaultSpeed);
        planet.resolution             = defaultResolution;
        planet.OceanElevation         = defaultOceanElev;
        planet.TopographyElevation    = defaultTopoElev;
        planet.GridResolutionPerDegree   = defaultPrecision;
        globeRotation.tiltAngle       = defaultTilt;

        SetSlider(speedSlider,      speedValueLabel,      defaultSpeed,     true);
        SetSlider(resolutionSlider, resolutionValueLabel, defaultResolution, true);
        SetSlider(tiltSlider,       tiltValueLabel,       defaultTilt,       false);

        // Restore planet advanced fields to their captured defaults
        planet.OceanElevation        = defaultOceanElev;
        planet.TopographyElevation   = defaultTopoElev;
        // Refactored to use GridResolutionPerDegree instead of PlatePrecisionFactor
        planet.GridResolutionPerDegree = defaultPrecision;
        planet.PlateToleranceDegrees = defaultTolerance;

        // Sync the advanced-settings sliders to the restored values
        advancedSettingsPanel?.ResetToDefaults(
            defaultOceanElev,
            defaultTopoElev,
            defaultPrecision,
            defaultTolerance
        );

        RebuildPlanetMesh();
    }

    private void OnZoomIn()
    {
        if (globeCamera == null) return;
        globeCamera.fieldOfView = Mathf.Clamp(globeCamera.fieldOfView - zoomStep, minFov, maxFov);
    }

    private void OnZoomOut()
    {
        if (globeCamera == null) return;
        globeCamera.fieldOfView = Mathf.Clamp(globeCamera.fieldOfView + zoomStep, minFov, maxFov);
    }

    private void OnReset()
    {
        if (globeCamera == null) return;
        globeCamera.transform.position = defaultCamPosition;
        globeCamera.transform.rotation = defaultCamRotation;
        globeCamera.fieldOfView = defaultFov;
        // Reset is camera only — explore mode is controlled independently by Stop Explore button
    }

    void Update()
    {
        if (globeCamera != null && camPositionLabel != null)
        {
            Vector3 pos = globeCamera.transform.position;
            camPositionLabel.text = $"Cam Position: ({pos.x}, {pos.y}, {pos.z})";
        }
    }
}
