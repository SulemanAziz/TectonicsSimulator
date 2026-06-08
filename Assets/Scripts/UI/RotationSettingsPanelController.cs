using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RotationController: MonoBehaviour
{
    public GlobeRotation Rotator;
    public Planet planet;
    public TextMeshProUGUI RotationSpeedValueLabel;
    public TextMeshProUGUI AxialTiltValueLabel;
    
    public UnityEngine.UI.Slider RotationSpeedSlider;
    public UnityEngine.UI.Slider AxialTitlValueSlider;

    private int Default_rotationspeed;
    private float Default_tiltvalue;
    private Vector3 Default_GlobePosition;

    void Start()
    {
        Hide();
        Default_rotationspeed = Rotator.Speed;
        Default_tiltvalue = Rotator.tiltAngle;
        Default_GlobePosition = planet.transform.position;
    }
    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void ChangeRotationSpeed()
    {
        Rotator.Speed = (int) RotationSpeedSlider.value;
        RotationSpeedValueLabel.text = RotationSpeedSlider.value.ToString();
    }

    public void ChangeAxialTilt()
    {
        Rotator.tiltAngle = AxialTitlValueSlider.value;
        AxialTiltValueLabel.text = AxialTitlValueSlider.value.ToString();
    }

    public void ResetSettings()
    {
        RotationSpeedSlider.value = Default_rotationspeed;
        AxialTitlValueSlider.value = Default_tiltvalue;
        planet.transform.position = Default_GlobePosition;
        Hide();
    }
   
}