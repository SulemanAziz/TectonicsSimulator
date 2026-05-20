using UnityEngine;
using UnityEngine.UI;

public class GlobeRenderTextureSetup : MonoBehaviour
{
    public Camera globeCamera;
    public RawImage globeViewportImage;

    private const int TextureSize = 128; // What does this control?
    private const int TextureDepth = 24; // and this?

    public RenderTexture GlobeTexture { get; private set; }

    private int lastWidth;
    private int lastHeight;

    void Awake()
    {
        // Create texture early so it exists before any other script reads it
        GlobeTexture = new RenderTexture(TextureSize, TextureSize, TextureDepth);
        GlobeTexture.name = "GlobeRenderTexture";
        GlobeTexture.Create();
    }

    void Start()
    {
        ResizeToScreen();
        ApplyTexture();
    }

    void Update()
    {
        // Cheap guard — only resizes when Screen dimensions actually change.
        ResizeToScreen();
    }

    /// <summary>Re-assigns the RenderTexture to the camera and viewport image. Safe to call at any time.</summary>
    public void ApplyTexture()
    {
        if (GlobeTexture == null) return;
        if (globeCamera != null)
            globeCamera.targetTexture = GlobeTexture;
        if (globeViewportImage != null)
            globeViewportImage.texture = GlobeTexture;
    }

    /// <summary>Resizes the render texture to match the current screen dimensions and corrects the camera aspect ratio.</summary>
    public void ResizeTexture(int width, int height)
    {
        if (GlobeTexture == null) return;
        GlobeTexture.Release();
        GlobeTexture.width  = width;
        GlobeTexture.height = height;
        GlobeTexture.Create();
    }

    void OnDestroy()
    {
        if (GlobeTexture != null)
        {
            GlobeTexture.Release();
            Destroy(GlobeTexture);
        }
    }

    // ── Internal helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Resizes the RenderTexture and corrects camera.aspect to match the
    /// current screen size. No-ops when dimensions have not changed.
    /// </summary>
    private void ResizeToScreen()
    {
        int w = Screen.width;
        int h = Screen.height;

        if (w == lastWidth && h == lastHeight) return;

        lastWidth  = w;
        lastHeight = h;

        ResizeTexture(w, h);

        if (globeCamera != null)
            globeCamera.aspect = (float)w / (float)h;

        ApplyTexture();
    }
}
