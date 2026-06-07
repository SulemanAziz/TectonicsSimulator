using TMPro;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class PlatesController: MonoBehaviour
{
    public Planet planet;
    public TextMeshProUGUI valuelabel;
    public UnityEngine.UI.Slider valueslider;
    private int resolutionvalue;
    void Start()
    {
        resolutionvalue = planet.GridResolutionPerDegree;
        UpdateValueLabel();
        Hide();
    }

    void OnValidate()
    {
        resolutionvalue = planet.GridResolutionPerDegree;
        UpdateValueLabel();
        UpdateSliderValue();
    }
    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void UpdateGridResolution()
    {
        resolutionvalue = (int) valueslider.value;
        UpdateValueLabel();
    }

    public void ConfirmChanges()
    {
        planet.GridResolutionPerDegree = resolutionvalue;
        planet.Init();
        planet.GenerateMesh();
        
        UpdateValueLabel();
        UpdateSliderValue();
        Hide();
    }

    public void CancelChanges()
    {
        resolutionvalue = planet.GridResolutionPerDegree;
        UpdateValueLabel();
        UpdateSliderValue();
        Hide();
    }

    private void UpdateValueLabel()
    {
        valuelabel.text = resolutionvalue.ToString();
    }

    private void UpdateSliderValue()
    {
        valueslider.value = resolutionvalue;
    }
}