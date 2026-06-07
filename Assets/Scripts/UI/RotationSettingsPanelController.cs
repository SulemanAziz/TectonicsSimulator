using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RotationController: MonoBehaviour
{
    public GlobeRotation Rotator;
    public TextMeshProUGUI RotationSpeedValueLabel;
    public UnityEngine.UI.Slider RotationValueSlider;
    private int rotationvalue;

    void Start()
    {
        Hide();
    }
    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void ChangeRotationSpeed(int val)
    {
        
    }
}