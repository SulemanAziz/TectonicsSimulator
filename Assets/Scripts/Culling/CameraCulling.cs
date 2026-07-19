using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Per-face culling & LOD for the 6-mesh Planet.
///
/// GAME VIEW:  All 6 renderers stay ENABLED at all times — the planet never disappears.
/// SCENE VIEW: Culled faces are temporarily disabled only while the Scene camera renders,
///             then immediately re-enabled so the Game camera always sees the full planet.
///
/// Visibility stages:
///   Stage 1 — Back-face dot product (planet-center reference, threshold -0.3).
///   Stage 2 — Renderer-bounds frustum test.
///
/// LOD: closest visible face → highResolution; others → planet.resolution (slider).
/// </summary>
[RequireComponent(typeof(Planet))]
public class CameraCulling : MonoBehaviour
{
    [Header("Camera References")]
    [Tooltip("Primary globe viewing camera (FOV-based zoom).")]
    public Camera globeCamera;

    [Tooltip("Free-look camera used in FlyCamera mode.")]
    public Camera flyCamera;

    [Header("LOD Settings")]
    [Tooltip("Camera-to-face-center distance below which the closest face upgrades to highResolution.")]
    public float lodDistanceThreshold = 2.5f;

    [Tooltip("Camera FOV below which the closest face upgrades to highResolution.")]
    public float lodFovThreshold = 40f;

    [Tooltip("Resolution applied to the single closest visible face when the LOD condition is met.")]
    public int highResolution = 720;

    // The 6 cube-face outward directions, matching Planet.Init() order:
    // up, down, left, right, forward, back
    private static readonly Vector3[] FaceNormals =
    {
        Vector3.up, Vector3.down, Vector3.left,
        Vector3.right, Vector3.forward, Vector3.back
    };

    private MeshRenderer[] _renderers;
    private Planet         _planet;
    private readonly Plane[] _frustumPlanes = new Plane[6];

    // Stored per-frame visibility for the Scene-view render callbacks.
    private bool[] _visible;

    // Hysteresis: prevent frame-to-frame LOD flickering.
    private int   _lastHighResFaceIndex = -1;
    private const float HysteresisFactor = 1.1f;

    // Back-face cull threshold: -0.3 ≈ 107° from camera → back hemisphere.
    private const float BACKFACE_THRESHOLD = -0.3f;

    // Track GlobeCamera FOV to detect zoom changes (LOD only on zoom, not rotation).
    private float _lastGlobeFov = -1f;

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────
    private void Start()
    {
        _renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
        _planet    = GetComponent<Planet>();
        _visible   = new bool[_renderers.Length];
        for (int i = 0; i < _visible.Length; i++) _visible[i] = true;
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        // Subscribe to per-camera render events so we can hide faces ONLY
        // while the Scene-view camera is rendering.
        // Built-in pipeline callbacks:
        Camera.onPreCull    += OnAnyCameraPreCull;
        Camera.onPostRender += OnAnyCameraPostRender;
        // SRP (URP/HDRP) callbacks:
        RenderPipelineManager.beginCameraRendering += OnSRPBeginCamera;
        RenderPipelineManager.endCameraRendering   += OnSRPEndCamera;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        Camera.onPreCull    -= OnAnyCameraPreCull;
        Camera.onPostRender -= OnAnyCameraPostRender;
        RenderPipelineManager.beginCameraRendering -= OnSRPBeginCamera;
        RenderPipelineManager.endCameraRendering   -= OnSRPEndCamera;
        // Make sure everything is visible when the component is disabled.
        SetAllEnabled(true);
#endif
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Per-camera Scene-view culling (editor only)
    // ─────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private bool IsSceneViewCamera(Camera cam)
    {
        if (cam == null) return false;
        // Check all open Scene views.
        foreach (SceneView sv in SceneView.sceneViews)
            if (sv != null && sv.camera == cam) return true;
        return false;
    }

    // Built-in pipeline
    private void OnAnyCameraPreCull(Camera cam)
    {
        if (IsSceneViewCamera(cam)) ApplyCulling();
    }

    private void OnAnyCameraPostRender(Camera cam)
    {
        if (IsSceneViewCamera(cam)) RestoreAll();
    }

    // SRP (URP / HDRP)
    private void OnSRPBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (IsSceneViewCamera(cam)) ApplyCulling();
    }

    private void OnSRPEndCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (IsSceneViewCamera(cam)) RestoreAll();
    }

    /// <summary>Temporarily disable culled renderers for Scene-view render pass.</summary>
    private void ApplyCulling()
    {
        if (_renderers == null || _visible == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            bool show = i < _visible.Length && _visible[i];
            if (_renderers[i].enabled != show)
                _renderers[i].enabled = show;
        }
    }

    /// <summary>Re-enable all renderers after Scene-view render pass.</summary>
    private void RestoreAll()
    {
        SetAllEnabled(true);
    }
#endif

    // ─────────────────────────────────────────────────────────────────────────
    // Main update: compute visibility + apply LOD (never touches renderer.enabled)
    // ─────────────────────────────────────────────────────────────────────────
    private void LateUpdate()
    {
        if (_renderers == null || _renderers.Length == 0 || _planet == null) return;

        // ── Pick active camera (Globe or Fly only — MainCam is for UI) ───────
        bool globeActive = globeCamera != null && globeCamera.isActiveAndEnabled;
        bool flyActive   = flyCamera   != null && flyCamera.isActiveAndEnabled;

        if (!globeActive && !flyActive)
        {
            SetAllVisible();
            return;
        }

        Camera  cam          = globeActive ? globeCamera : flyCamera;
        Vector3 camPos       = cam.transform.position;
        float   fov          = cam.fieldOfView;
        Vector3 planetCenter = _planet.transform.position;

        GeometryUtility.CalculateFrustumPlanes(cam, _frustumPlanes);

        // ── Camera direction from planet center ──────────────────────────────
        Vector3 camFromPlanet = (camPos - planetCenter).normalized;

        // ── Per-face visibility ──────────────────────────────────────────────
        if (_visible == null || _visible.Length != _renderers.Length)
            _visible = new bool[_renderers.Length];

        int   closestIndex  = -1;
        float closestDistSq = float.MaxValue;

        int faceCount = Mathf.Min(_renderers.Length, FaceNormals.Length);
        for (int i = 0; i < faceCount; i++)
        {
            if (_renderers[i] == null) { _visible[i] = false; continue; }

            // Stage 1: Back-face dot product
            Vector3 worldNormal = _planet.transform.TransformDirection(FaceNormals[i]).normalized;
            float dot = Vector3.Dot(worldNormal, camFromPlanet);
            if (dot < BACKFACE_THRESHOLD)
            {
                _visible[i] = false;
                continue;
            }

            // Stage 2: Renderer-bounds frustum test
            if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, _renderers[i].bounds))
            {
                _visible[i] = false;
                continue;
            }

            _visible[i] = true;

            // Track closest visible face for LOD.
            Vector3 faceCenterWorld = _planet.transform.TransformPoint(FaceNormals[i].normalized);
            float   distSq         = (camPos - faceCenterWorld).sqrMagnitude;
            if (distSq < closestDistSq)
            {
                closestDistSq = distSq;
                closestIndex  = i;
            }
        }

        // ── LOD decision ─────────────────────────────────────────────────────
        // LOD only triggers when:
        //   1. FlyCamera is the active camera, OR
        //   2. GlobeCamera FOV changed (user pressed Zoom +/-).
        // Globe rotation alone does NOT trigger LOD (avoids flicker).
        bool globeFovChanged = false;
        if (globeActive)
        {
            if (_lastGlobeFov >= 0f && Mathf.Abs(fov - _lastGlobeFov) > 0.01f)
                globeFovChanged = true;
            _lastGlobeFov = fov;
        }

        bool allowLod = flyActive || globeFovChanged;

        bool upgradeClosest = false;
        if (allowLod && closestIndex != -1)
        {
            float dist       = Mathf.Sqrt(closestDistSq);
            float distThresh = lodDistanceThreshold * (closestIndex == _lastHighResFaceIndex ? HysteresisFactor : 1f);
            float fovThresh  = lodFovThreshold      * (closestIndex == _lastHighResFaceIndex ? HysteresisFactor : 1f);
            upgradeClosest   = (dist < distThresh) || (fov < fovThresh);
        }

        int highResFace = allowLod ? (upgradeClosest ? closestIndex : -1) : _lastHighResFaceIndex;

        // ── Apply LOD (play mode only) — NEVER touch renderer.enabled here ──
        if (Application.isPlaying)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;

                // Guarantee the renderer is on for the Game camera.
                if (!_renderers[i].enabled)
                    _renderers[i].enabled = true;

                // Only update resolution when LOD is allowed.
                if (allowLod)
                {
                    bool isFaceVisible = i < _visible.Length && _visible[i];
                    int targetRes = (isFaceVisible && i == highResFace) ? highResolution : _planet.resolution;
                    _planet.UpdateFaceResolution(i, targetRes);
                }
            }
        }

        _lastHighResFaceIndex = highResFace;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Mark all faces as visible (used when no relevant camera is active).</summary>
    private void SetAllVisible()
    {
        if (_visible == null || _visible.Length != (_renderers != null ? _renderers.Length : 0))
            _visible = new bool[_renderers != null ? _renderers.Length : 0];
        for (int i = 0; i < _visible.Length; i++) _visible[i] = true;
        SetAllEnabled(true);
    }

    /// <summary>Call after Planet.Init() recreates mesh children to refresh the renderer list.</summary>
    public void RefreshRenderers()
    {
        _renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
        _visible   = new bool[_renderers.Length];
        for (int i = 0; i < _visible.Length; i++) _visible[i] = true;
    }

    private void SetAllEnabled(bool value)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].enabled = value;
    }
}
