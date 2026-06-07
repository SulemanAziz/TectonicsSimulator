using UnityEngine;

public class FreeCamController: MonoBehaviour
{
    public Camera GlobeCamera;
    private Vector3 CameraDefaultPosition;
    void Start()
    {
        GlobeCamera.GetComponent<FlyCamera>().enabled = false;
        CameraDefaultPosition = GlobeCamera.transform.position;
    }

    public void EnableFreeCam()
    {
        GlobeCamera.GetComponent<FlyCamera>().enabled = true;
    }

    public void DisableFreeCam()
    {
        GlobeCamera.GetComponent<FlyCamera>().enabled = false;
    }
}