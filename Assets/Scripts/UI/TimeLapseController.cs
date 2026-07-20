using System;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimeLapseController : MonoBehaviour
{
    private readonly Color32 onColor = new Color32(0xF6, 0xEF, 0xEF, 0xFF);   // ON
    private readonly Color32 offColor = new Color32(0x5B, 0x5B, 0x5B, 0xFF);  // OFF

    public Button EnableFreeCam;
    public Button DisableFreeCam;
    public Canvas MainScene;
    public Camera MainCamera;
    public Camera TimeLapseCamera;
    public Camera TimeLapseFreeCam;

    public TextMeshProUGUI Title;
    public Texture2D[] TimeSequenceRaster;
    public Image current_display_image;
    public Planet planet;

    public Button Play_Pause_Button;
    public Button ForwardButton;
    public Button BackwardButton;
    public Button ExitButton;

    public Toggle LoopSequence;
    public Toggle RotationToggle;

    public float playbackStepSeconds = 3f;

    bool isPlaying = false;
    int current_texture_index = 0;

    Sprite[] timeSprites;
    Coroutine playRoutine;

    void Awake()
    {
        Debug.Log("current_display_image is " + (current_display_image == null ? "NULL" : "SET"));
        Debug.Log("Title is " + (Title == null ? "NULL" : "SET"));

        LoadTextures();
        BuildSpritesFromLoadedTextures();

        if (TimeSequenceRaster != null && TimeSequenceRaster.Length > 0)
        {
            current_texture_index = Mathf.Clamp(current_texture_index, 0, TimeSequenceRaster.Length - 1);
            SetFrame(current_texture_index);
        }

        SetupToggleVisuals(LoopSequence);
        SetupToggleVisuals(RotationToggle);

        DisableFreeCam.gameObject.SetActive(false);
        EnableFreeCam.gameObject.SetActive(true);

    }

    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            LoadTextures();
            BuildSpritesFromLoadedTextures();
        }
    }

    void Update()
    {
        if (planet == null) return;

        if (RotationToggle != null && RotationToggle.isOn)
        {
            planet.transform.Rotate(Vector3.up, 10f * Time.deltaTime, Space.Self);
        }
    }

    void LoadTextures()
    {
        var textures = Resources.LoadAll<Texture2D>("TimeFrameRaster");

        if (textures == null || textures.Length == 0)
        {
            TimeSequenceRaster = Array.Empty<Texture2D>();
            timeSprites = Array.Empty<Sprite>();
            Debug.LogError("No textures found at Resources/TimeFrameRaster");
            return;
        }

        int ExtractIndex(string textureName)
        {
            var last = textureName.Split('_').Last();
            return int.TryParse(last, out var i) ? i : -1;
        }

        TimeSequenceRaster = textures
            .OrderBy(t => ExtractIndex(t.name))
            .ToArray();

        Debug.Log($"Loaded {TimeSequenceRaster.Length} frames. First={TimeSequenceRaster.First().name}, Last={TimeSequenceRaster.Last().name}");
    }

    void BuildSpritesFromLoadedTextures()
    {
        if (TimeSequenceRaster == null || TimeSequenceRaster.Length == 0)
        {
            timeSprites = Array.Empty<Sprite>();
            return;
        }

        timeSprites = new Sprite[TimeSequenceRaster.Length];

        for (int i = 0; i < TimeSequenceRaster.Length; i++)
        {
            var tex = TimeSequenceRaster[i];

            timeSprites[i] = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f)
            );
        }
    }

    void SetFrame(int index)
    {
        if (TimeSequenceRaster == null || TimeSequenceRaster.Length == 0) return;
        if (index < 0 || index >= TimeSequenceRaster.Length) return;

        var texture = TimeSequenceRaster[index];

        // Planet update
        if (planet != null)
            planet.ChangeColorTexture(texture);

        // Title update (file/asset name)
        if (Title != null)
            Title.text = texture.name;

        // UI Image update
        if (current_display_image != null &&
            timeSprites != null &&
            index < timeSprites.Length)
        {
            current_display_image.sprite = timeSprites[index];
        }
    }

    public void PlayorPauseSequence()
    {
        if (TimeSequenceRaster == null || TimeSequenceRaster.Length == 0) return;

        if (!isPlaying)
        {
            isPlaying = true;
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayLoop());
        }
        else
        {
            isPlaying = false;
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = null;
            Debug.Log("Paused Sequence");
        }
    }

    IEnumerator PlayLoop()
    {
        while (isPlaying)
        {
            SetFrame(current_texture_index);

            yield return new WaitForSeconds(playbackStepSeconds);

            int lastIndex = TimeSequenceRaster.Length - 1;
            if (current_texture_index < lastIndex)
            {
                current_texture_index++;
            }
            else
            {
                if (LoopSequence != null && LoopSequence.isOn)
                    current_texture_index = 0;
                else
                {
                    isPlaying = false;
                    playRoutine = null;
                    yield break;
                }
            }
        }
    }

    public void IncrementTexture()
    {
        if (isPlaying) return;
        if (TimeSequenceRaster == null || TimeSequenceRaster.Length == 0) return;

        int lastIndex = TimeSequenceRaster.Length - 1;
        if (current_texture_index < lastIndex)
        {
            current_texture_index++;
            SetFrame(current_texture_index);
        }
    }

    public void DecrementTexture()
    {
        if (isPlaying) return;
        if (TimeSequenceRaster == null || TimeSequenceRaster.Length == 0) return;

        if (current_texture_index > 0)
        {
            current_texture_index--;
            SetFrame(current_texture_index);
        }
    }

        void SetupToggleVisuals(Toggle toggle)
    {
        if (toggle == null) return;

        toggle.onValueChanged.AddListener(_ => UpdateToggleSpriteColor(toggle));
        UpdateToggleSpriteColor(toggle); // set initial color
    }

    void UpdateToggleSpriteColor(Toggle toggle)
    {
        if (toggle == null) return;

        // Prefer the Toggle's target graphic (commonly the checkmark/background Image)
        Image img = toggle.targetGraphic as Image;

        // Fallbacks if targetGraphic isn't an Image
        if (img == null) img = toggle.GetComponent<Image>();
        if (img == null) img = toggle.GetComponentInChildren<Image>(true);

        if (img != null)
            img.color = toggle.isOn ? onColor : offColor;
    }

    public void ToggleFreeCamON()
    {
        EnableFreeCam.gameObject.SetActive(false);
        DisableFreeCam.gameObject.SetActive(true);

        TimeLapseFreeCam.gameObject.SetActive(true);
        TimeLapseCamera.gameObject.SetActive(false);
    }
    public void ToggleFreeCamOFF()
    {
        DisableFreeCam.gameObject.SetActive(false);
        EnableFreeCam.gameObject.SetActive(true);

        TimeLapseCamera.gameObject.SetActive(true);
        TimeLapseFreeCam.gameObject.SetActive(false);
    }
    public void Exit()
    {
        planet.ChangeColorTexture(TimeSequenceRaster[0]);
        gameObject.SetActive(false);
        TimeLapseCamera.gameObject.SetActive(false);
        TimeLapseFreeCam.gameObject.SetActive(false);
        MainScene.gameObject.SetActive(true);
        MainCamera.gameObject.SetActive(true);
    }
}
