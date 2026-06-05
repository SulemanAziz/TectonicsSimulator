using UnityEngine;
using UnityEngine.UI;

public class ImageChanger : MonoBehaviour
{   
    [Header ("ImageSelection")]
    public Sprite currentImage;

    public void changeImage(int ID)
    {
        switch (ID)
        {
            case 0:
                currentImage = Resources.Load<Sprite>("ColorMaps/Bouguer-Gravity-Anomalies-WGM");
                ApplyImage();
                break;
            case 1:
                currentImage = Resources.Load<Sprite>("ColorMaps/BWTopo");
                ApplyImage();
                break;
            case 2:
                currentImage = Resources.Load<Sprite>("ColorMaps/NASA-ColorMap");
                ApplyImage();
                break;
        }
    }

    public void ApplyImage()
    {
        if(currentImage != null)
        {
            Debug.Log("Update function called - Image being assigned");
            gameObject.GetComponent<Image>().sprite = currentImage;
        }
        else
        {
            gameObject.GetComponent<Image>().sprite = null;
        }
    }

    /// <summary>
    /// Called when the script is loaded or a value is changed in the
    /// inspector (Called in the editor only).
    /// </summary>
    void OnValidate()
    {
        if (currentImage != null)
        {
            gameObject.GetComponent<Image>().sprite = currentImage;
        }

        else
        {
            gameObject.GetComponent<Image>().sprite = null;
        }
    }
}
