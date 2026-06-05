using System;
using System.Collections;
using UnityEngine;

public class CameraTransitionController : MonoBehaviour
{
    public Transform planetTransform;
    public Camera mainCam;
    public GameObject globeCameraObject;
    public GlobeRenderTextureSetup renderTextureSetup;
    public float duration = 2.5f;

    [Tooltip("How many units away from the planet the camera arrives at.")]
    public float arrivalDistance = 6.5f;

    /// <summary>Starts fly-in animation toward the planet. Invokes onComplete when finished.</summary>
    public void BeginFlyIn(Action onComplete)
    {
        StartCoroutine(FlyInCoroutine(onComplete));
    }

    private IEnumerator FlyInCoroutine(Action onComplete)
    {
        Vector3 planetPos = planetTransform.position;
        Vector3 startPos  = mainCam.transform.position;
        Quaternion startRot = mainCam.transform.rotation;

        // Target fixed native position to maintain default visual zoom
        Vector3 targetPos = globeCameraObject.transform.position;
        Quaternion targetRot = globeCameraObject.transform.rotation;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            mainCam.transform.position = Vector3.Lerp(startPos, targetPos, t);
            mainCam.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        // Ensure GlobeCamera is positioned precisely
        Camera globeCam = globeCameraObject.GetComponent<Camera>();
        globeCameraObject.transform.position = targetPos;
        globeCameraObject.transform.rotation = targetRot;

        // Ensure RenderTexture is assigned before GlobeCamera becomes active
        // (targetTexture assignment works on inactive cameras too)
        renderTextureSetup?.ApplyTexture();

        // Alter main camera so it doesn't double-render the planet, but still renders screen elements
        mainCam.cullingMask = 0; // Prevent rendering anything except UI and background (Skybox)
        globeCameraObject.SetActive(true);

        // Force another apply now that the camera is active — Unity sometimes clears targetTexture on enable
        renderTextureSetup?.ApplyTexture();

        onComplete?.Invoke();
    }
}
