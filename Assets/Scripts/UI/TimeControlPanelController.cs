using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI controller for the geological time scrubber.
/// Attach to a panel with a Slider, labels, and Play/Pause/Reset buttons.
///
/// Required UI elements (wire up in Inspector):
///   - timeSlider       : Slider (min 0, max 560)
///   - timeLabel        : TextMeshProUGUI  e.g. "250.0 Ma"
///   - eraLabel         : TextMeshProUGUI  e.g. "Paleozoic"
///   - playPauseButton  : Button (toggles AutoPlay)
///   - playPauseLabel   : TextMeshProUGUI on the button ("Play" / "Pause")
///   - resetButton      : Button (jumps back to 0 Ma)
///   - speedSlider      : Slider (controls playback speed 0.1 – 50 Ma/s)
///   - speedLabel       : TextMeshProUGUI  e.g. "5.0 Ma/s"
///   - planet           : Planet reference
/// </summary>
public class TimeControlPanelController : MonoBehaviour
{
    [Header("Planet Reference")]
    public Planet planet;

    [Header("Time Slider")]
    public Slider timeSlider;
    public TextMeshProUGUI timeLabel;
    public TextMeshProUGUI eraLabel;

    [Header("Playback Controls")]
    public Button playPauseButton;
    public TextMeshProUGUI playPauseLabel;
    public Button resetButton;

    [Header("Speed Control")]
    public Slider speedSlider;
    public TextMeshProUGUI speedLabel;

    // Whether the slider is being dragged (suppress Update writes during drag)
    private bool _isDragging = false;

    void Start()
    {
        if (planet == null)
        {
            Debug.LogError("[TimeControlPanelController] Planet reference not set.");
            return;
        }

        // Initialize slider range and value
        if (timeSlider != null)
        {
            timeSlider.minValue = 0f;
            timeSlider.maxValue = 560f;
            timeSlider.value = planet.CurrentTimeMa;

            timeSlider.onValueChanged.AddListener(OnTimeSliderChanged);

            // Detect drag start/end so Update doesn't fight the user
            var trigger = timeSlider.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>()
                       ?? timeSlider.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            AddTriggerEvent(trigger, UnityEngine.EventSystems.EventTriggerType.PointerDown, _ => _isDragging = true);
            AddTriggerEvent(trigger, UnityEngine.EventSystems.EventTriggerType.PointerUp,   _ =>
            {
                _isDragging = false;
                planet.CurrentTimeMa = timeSlider.value;
            });
        }

        // Speed slider
        if (speedSlider != null)
        {
            speedSlider.minValue = 0.1f;
            speedSlider.maxValue = 50f;
            speedSlider.value = planet.PlaybackSpeedMaPerSecond;
            speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
        }

        // Buttons
        playPauseButton?.onClick.AddListener(OnPlayPauseClicked);
        resetButton?.onClick.AddListener(OnResetClicked);

        RefreshLabels(planet.CurrentTimeMa);
        RefreshPlayPauseLabel();
    }

    void Update()
    {
        if (planet == null || _isDragging) return;

        // Keep slider in sync with planet time (driven by AutoPlay or inspector)
        if (timeSlider != null && Mathf.Abs(timeSlider.value - planet.CurrentTimeMa) > 0.05f)
        {
            timeSlider.SetValueWithoutNotify(planet.CurrentTimeMa);
            RefreshLabels(planet.CurrentTimeMa);
        }
    }

    // ── Event Handlers ────────────────────────────────────────────────

    private void OnTimeSliderChanged(float value)
    {
        planet.CurrentTimeMa = value;
        RefreshLabels(value);
    }

    private void OnSpeedSliderChanged(float value)
    {
        planet.PlaybackSpeedMaPerSecond = value;
        if (speedLabel != null)
            speedLabel.text = $"{value:F1} Ma/s";
    }

    private void OnPlayPauseClicked()
    {
        planet.AutoPlay = !planet.AutoPlay;
        RefreshPlayPauseLabel();
    }

    private void OnResetClicked()
    {
        planet.AutoPlay = false;
        planet.CurrentTimeMa = 0f;
        if (timeSlider != null) timeSlider.SetValueWithoutNotify(0f);
        RefreshLabels(0f);
        RefreshPlayPauseLabel();
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private void RefreshLabels(float timeMa)
    {
        if (timeLabel != null)
            timeLabel.text = $"{timeMa:F1} Ma";

        if (eraLabel != null)
            eraLabel.text = GetEraName(timeMa);
    }

    private void RefreshPlayPauseLabel()
    {
        if (playPauseLabel != null)
            playPauseLabel.text = planet.AutoPlay ? "Pause" : "Play";
    }

    /// <summary>
    /// Returns the geological era name for a given time in Ma.
    /// </summary>
    private string GetEraName(float timeMa)
    {
        if (timeMa < 2.6f)   return "Quaternary";
        if (timeMa < 23f)    return "Neogene";
        if (timeMa < 66f)    return "Paleogene";
        if (timeMa < 145f)   return "Cretaceous";
        if (timeMa < 201f)   return "Jurassic";
        if (timeMa < 252f)   return "Triassic";
        if (timeMa < 299f)   return "Permian";
        if (timeMa < 359f)   return "Carboniferous";
        if (timeMa < 419f)   return "Devonian";
        if (timeMa < 444f)   return "Silurian";
        if (timeMa < 485f)   return "Ordovician";
        if (timeMa < 538f)   return "Cambrian";
        return "Ediacaran";
    }

    private void AddTriggerEvent(UnityEngine.EventSystems.EventTrigger trigger,
                                  UnityEngine.EventSystems.EventTriggerType type,
                                  UnityEngine.Events.UnityAction<UnityEngine.EventSystems.BaseEventData> action)
    {
        var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }
}
