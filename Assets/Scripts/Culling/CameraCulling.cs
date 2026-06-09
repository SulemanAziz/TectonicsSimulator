using UnityEngine;

/// <summary>
/// Disables MeshRenderers on Planet face children when they fall outside the active
/// camera's view frustum. Uses Unity's built-in AABB frustum test — no custom math,
/// zero per-frame heap allocations.
/// </summary>
[RequireComponent(typeof(Planet))]
public class CameraCulling : MonoBehaviour
{
    [Header("Camera References")]
    [Tooltip("Primary globe viewing camera (FOV-based zoom).")]
    public Camera globeCamera;

    [Tooltip("Free-look camera used in FlyCamera mode.")]
    public Camera flyCamera;

    private MeshRenderer[] _renderers;
    private readonly Plane[] _planes = new Plane[6];

    private void Start()
    {
        _renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
    }

    private void LateUpdate()
    {
        if (_renderers == null || _renderers.Length == 0) return;

        bool globeActive = globeCamera != null && globeCamera.isActiveAndEnabled;
        bool flyActive   = flyCamera   != null && flyCamera.isActiveAndEnabled;

        // No active camera — keep everything visible.
        if (!globeActive && !flyActive)
        {
            SetAllEnabled(true);
            return;
        }

        // Prefer GlobeCamera; fall back to FlyCamera when Globe is off.
        Camera activeCam = globeActive ? globeCamera : flyCamera;
        GeometryUtility.CalculateFrustumPlanes(activeCam, _planes);

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;

            bool visible = GeometryUtility.TestPlanesAABB(_planes, _renderers[i].bounds);

            // Write only on state change — avoids dirtying the renderer every frame.
            if (_renderers[i].enabled != visible)
                _renderers[i].enabled = visible;
        }
    }

    /// <summary>
    /// Call this after Planet.Init() recreates mesh children so the renderer list is refreshed.
    /// </summary>
    public void RefreshRenderers()
    {
        _renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
    }

    private void SetAllEnabled(bool value)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].enabled = value;
    }
}
