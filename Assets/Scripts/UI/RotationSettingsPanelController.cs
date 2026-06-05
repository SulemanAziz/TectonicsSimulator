using UnityEngine;
using UnityEngine.UI;

public class RotationController: MonoBehaviour
{
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
}