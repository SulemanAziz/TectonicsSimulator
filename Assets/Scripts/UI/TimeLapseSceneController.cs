using UnityEngine;
using UnityEngine.UI;
public class TimeLapseSceneController: MonoBehaviour
{
    public Canvas timeLapse;
    public Camera timeLapseCamera;
    public Camera MainCamera;
    public Canvas MainScene;

    public void Switch_to_TimeLapse()
    {
        MainScene.gameObject.SetActive(false);
        MainCamera.gameObject.SetActive(false);
        timeLapse.gameObject.SetActive(true);
        timeLapseCamera.gameObject.SetActive(true);
    }

}