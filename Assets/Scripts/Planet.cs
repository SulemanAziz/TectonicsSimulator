using UnityEngine;
using System.IO;
using System.Data;
using System.Collections.Generic;
using UnityEngine.Analytics;

public class Planet : MonoBehaviour
{
    [Range(2, 1024)]
    public int resolution = 128;

    [Range (1f,3f)]
    public float WaterLevel = 2.1f;

    [Range (2f, 5f)]
    public float MountainLevel = 2.22f;

    [Range (5, 50)]
    public int PlatePrecisionFactor = 10;

    [Range (0.1f, 10f)]
    public float PlateToleranceDegrees = 0.5f;

    [Header("Rendering API")]
    [Tooltip("Toggle to instantly show or hide tectonic plate boundaries")]
    public bool ShowPlates = true;
    [Tooltip("The path or filename for the tectonic map data")]
    public string currentDataFile = "TectonicPlates";

    // Track the previous state of the toggle for smart validation
    private bool previousShowPlates = true;

    public Texture2D OceanheightMap;
    public Texture2D TerrainheightMap;
    public Dictionary<string, List<float[]>> PlateMap;

    [Range(0f, 1f)]
    public float OceanElevation = 0.1f;
    [Range(0f, 1f)]
    public float TopographyElevation = 0.18f;

    [SerializeField, HideInInspector]
    MeshFilter[] meshFilters;
    TerrainFaces[] terrainFaces;

    void OnValidate()
    {
        // Only regenerate automatically if we are NOT playing the game
        if (!Application.isPlaying) 
        {
            // Smart optimization: If ONLY the ShowPlates boolean changed, 
            // just toggle the colors. Don't rebuild the whole mesh!
            if (ShowPlates != previousShowPlates)
            {
                TogglePlateRendering(ShowPlates);
                return; // Exit early
            }

            Init();
            GenerateMesh();
        }
    }

    // --- CHANGE 2: Added Start() so it builds on Play ---
    void Start()
    {
        previousShowPlates = ShowPlates;
        Init();
        GenerateMesh();
    }

    void Init()
    {
        // Load Resources heightmap if not already set in inspector
        if (OceanheightMap == null) OceanheightMap = Resources.Load<Texture2D>("BathyProcessed");
        if (TerrainheightMap == null) TerrainheightMap = Resources.Load<Texture2D>("TopoHeight");
        
        if(PlateMap == null){
            PlateMap = Mapping.Map(currentDataFile);
        }

        if (meshFilters == null || meshFilters.Length == 0)
        {
            meshFilters = new MeshFilter[6];
        }
        terrainFaces = new TerrainFaces[6];

        UnityEngine.Vector3[] directions = { UnityEngine.Vector3.up, UnityEngine.Vector3.down, UnityEngine.Vector3.left, UnityEngine.Vector3.right, UnityEngine.Vector3.forward, UnityEngine.Vector3.back };

        for (int i = 0; i < 6; i++)
        {
            if (meshFilters[i] == null)
            {
                GameObject meshObj = new GameObject("mesh");
                meshObj.transform.parent = transform;

                meshFilters[i] = meshObj.AddComponent<MeshFilter>();

                if (meshObj.GetComponent<MeshRenderer>() == null)
                {
                   var mr = meshObj.AddComponent<MeshRenderer>();
                   mr.sharedMaterial = new Material(Shader.Find("Standard")); 
                }

                meshFilters[i].sharedMesh = new Mesh();
            }

            terrainFaces[i] = new TerrainFaces(meshFilters[i].sharedMesh, resolution, directions[i], OceanheightMap, TerrainheightMap, PlateMap, PlatePrecisionFactor, PlateToleranceDegrees, OceanElevation, TopographyElevation, WaterLevel, MountainLevel);
        }
    }

    void GenerateMesh()
    {
        if (terrainFaces == null) return; 

        foreach(TerrainFaces faces in terrainFaces)
        {
            faces.ConstructMesh();
            // Apply current visibility correctly on generation
            faces.TogglePlates(ShowPlates);
        }
    }

    /// <summary>
    /// Instantly switches plate rendering on or off without blocking the main thread or rebuilding geometry.
    /// This is an O(1) instantaneous operation acting purely on cached Mesh color arrays.
    /// </summary>
    public void TogglePlateRendering(bool show)
    {
        ShowPlates = show;
        previousShowPlates = show; // sync tracking

        if (terrainFaces != null)
        {
            foreach (TerrainFaces face in terrainFaces)
            {
                if (face != null) face.TogglePlates(show);
            }
        }
    }

    /// <summary>
    /// Dynamically loads a mapping file and updates the visual display synchronously
    /// without needing to rebuild underlying vertex geometry.
    /// </summary>
    public void LoadGeologicalData(string filename)
    {
        currentDataFile = filename;
        PlateMap = Mapping.Map(currentDataFile);

        if (terrainFaces != null)
        {
            foreach (TerrainFaces face in terrainFaces)
            {
                if (face != null) face.UpdatePlateData(PlateMap);
            }
        }
    }
}