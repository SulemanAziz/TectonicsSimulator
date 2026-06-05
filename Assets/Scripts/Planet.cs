using UnityEngine;
using System.IO;
using System.Data;
using System.Collections.Generic;
using UnityEngine.Analytics;
public class Planet : MonoBehaviour
{
    [Range(2, 1024)]
    public int resolution = 128;

    [Range(5, 50)]
    public int GridResolutionPerDegree = 10;
    public bool ShowPlates = false;
    public string currentDataFile = "TectonicPlates";
    // Track the previous state of the toggle for smart validation
    private bool PlateState = false;

    public Texture2D OceanheightMap;
    public Texture2D TerrainheightMap;
    public Texture2D ColorMap;
    
    // Core Simulation Data Structures (Replaces raw PlateMap)
    public SimulationGrid Grid { get; private set; }
    public GridInitializer Initializer { get; private set; }

    // Compatibility field retained for UI references. No longer used in the decoupled grid simulation.
    public float PlateToleranceDegrees = 1.0f;

    [Range(0f, 1f)]
    public float OceanElevation = 0.1f;
    [Range(0f, 1f)]
    public float TopographyElevation = 0.15f;

    [SerializeField, HideInInspector]
    MeshFilter[] meshFilters;
    TerrainFaces[] terrainFaces;

    
    void OnValidate()
    {
        // Allow the user to toggle plates via Inspector whether playing or not
        if (ShowPlates != PlateState)
        {
            PlateState = ShowPlates;
            TogglePlateRendering(PlateState);
        }

        if (!Application.isPlaying) 
        {
            Init();
            GenerateMesh();
        }

        Init();
        GenerateMesh();
    }

    void Start()
    {
        Init();
        GenerateMesh();
    }

    public void ChangeColorTexture(Texture2D textureselection)
    {
        ColorMap = textureselection;
        Init();
        GenerateMesh();
    }

    public void Init()
    {
        // Load Resources heightmap if not already set in inspector
        if (OceanheightMap == null) OceanheightMap = Resources.Load<Texture2D>("Ocean");
        if (TerrainheightMap == null) TerrainheightMap = Resources.Load<Texture2D>("TopoHeight");
        if (ColorMap == null) ColorMap = Resources.Load<Texture2D>("ColorMap");

        // Initialize our new Simulation Grid only once, and ONLY when playing!
        if (Initializer == null) Initializer = new GridInitializer();
        if (Grid == null && Application.isPlaying) 
        {
            long expectedCells = (360L * GridResolutionPerDegree) * (180L * GridResolutionPerDegree);
            if (expectedCells > 20_000_000)
            {
                Debug.LogError($"[Planet] Aborting Grid Initialization! Requested GridResolutionPerDegree of {GridResolutionPerDegree} would create {expectedCells:N0} cells, exceeding the safe limit of 20,000,000. Please lower the resolution.");
                return;
            }

            var rawPlateData = Mapping.Map(currentDataFile);
            Grid = Initializer.Initialize(rawPlateData, GridResolutionPerDegree);
        }
        if (meshFilters == null || meshFilters.Length == 0) meshFilters = new MeshFilter[6];
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

            // Pass the Grid and PlateRegistry to TerrainFaces instead of the raw PlateMap
            // BUGFIX: We also explicitly pass OceanElevation and TopographyElevation here so that the UI sliders correctly displace the terrain mesh heightmaps.
            terrainFaces[i] = new TerrainFaces(meshFilters[i].sharedMesh, resolution, directions[i], OceanheightMap, TerrainheightMap, ColorMap, Grid, Initializer.PlateRegistry, OceanElevation, TopographyElevation);
        }
    }

    public void GenerateMesh()
    {
        if (terrainFaces == null) return; 

        foreach(TerrainFaces faces in terrainFaces)
        {
            faces.ConstructMesh();
            faces.TogglePlates(ShowPlates);
        }
    }

    public void TogglePlateRendering(bool show)
    {
        if (terrainFaces != null)
        {
            foreach (TerrainFaces face in terrainFaces)
            {
                if (face != null) face.TogglePlates(show);
            }
        }
    }

    public void LoadGeologicalData(string filename)
    {
        currentDataFile = filename;
        if (Initializer == null) Initializer = new GridInitializer();
        var rawPlateData = Mapping.Map(currentDataFile);
        
        // Only do the heavy fill if we are playing to avoid editor freezes
        if (Application.isPlaying) 
        {
            long expectedCells = (360L * GridResolutionPerDegree) * (180L * GridResolutionPerDegree);
            if (expectedCells > 20_000_000)
            {
                Debug.LogError($"[Planet] Aborting Data Load! Requested GridResolutionPerDegree of {GridResolutionPerDegree} would create {expectedCells:N0} cells, exceeding the safe limit of 20,000,000. Please lower the resolution.");
                return;
            }

            Grid = Initializer.Initialize(rawPlateData, GridResolutionPerDegree);
        }

        if (terrainFaces != null)
        {
            foreach (TerrainFaces face in terrainFaces)
            {
                if (face != null) face.UpdateSimulationData(Grid, Initializer.PlateRegistry);
            }
        }
    }
}