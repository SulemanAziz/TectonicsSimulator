using UnityEngine;
using UnityEngine.UI;
public class ToggleButtonAppearance: MonoBehaviour
{
    public GameObject EnableButton;
    public GameObject DisableButton;
    public void FreeCamActive()
    {
        EnableButton.SetActive(false);
        DisableButton.SetActive(true);
    }

    public void FreeCamInActive()
    {
        EnableButton.SetActive(true);
        DisableButton.SetActive(false);
    }

    void Start()
    {
        FreeCamInActive();
    }
}