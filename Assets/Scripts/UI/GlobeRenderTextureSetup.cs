using UnityEngine;
using UnityEngine.UI;

public class GlobeRenderTextureSetup : MonoBehaviour
{
    public Camera globeCamera;
    public RawImage globeViewportImage;

    private const int TextureSize = 128; // What does this control?
    private const int TextureDepth = 24; // and this?

    public RenderTexture GlobeTexture { get; private set; }

    void Awake()
    {
        // Create texture early so it exists before any other script reads it
        GlobeTexture = new RenderTexture(TextureSize, TextureSize, TextureDepth);
        GlobeTexture.name = "GlobeRenderTexture";
        GlobeTexture.Create();
    }

    void Start()
    {
        ResizeTexture(1280,720); // Resized the texture to a 16:9 resolution, no image warping seen now.
        ApplyTexture();
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

    /// <summary>Resizes the render texture to new dimensions.</summary>
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
}
