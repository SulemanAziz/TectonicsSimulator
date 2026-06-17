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
    public bool ShowBoundaries = true;
    public string currentDataFile = "TectonicPlates";
    // Track the previous state of the toggle for smart validation
    private bool PlateState = false;
    private bool BoundaryState = true;

    // ── TIME CONTROL ──────────────────────────────────────────────────
    // 0 = present day, 560 = 560 million years ago
    [Header("Time Control")]
    [Range(0f, 560f)]
    public float CurrentTimeMa = 0f;
    private float _lastTimeMa = -1f;       // detect changes
    public bool AutoPlay = false;          // scrub forward in time automatically
    [Range(0.1f, 50f)]
    public float PlaybackSpeedMaPerSecond = 5f;

    // ── MOVEMENT SYSTEM ───────────────────────────────────────────────
    public PlateRotationLoader RotationLoader { get; private set; }
    private PlateMovementSystem    _movementSystem;
    private PlateCollisionDetector _collisionDetector;
    private ElevationSystem        _elevationSystem;
    private SimulationGrid _baseGrid;      // snapshot at 0 Ma (never modified)
    private float _previousTimeMa = 0f;   // track delta time between steps

    public Texture2D OceanheightMap;
    public Texture2D TerrainheightMap;
    public Texture2D ColorMap;
    
    // Core Simulation Data Structures (Replaces raw PlateMap)
    public SimulationGrid Grid { get; private set; }
    public GridInitializer Initializer { get; private set; }



    [Range(0f, 0.2f)]
    public float OceanElevation = 0.1f;
    [Range(0f, 0.2f)]
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

        if (ShowBoundaries != BoundaryState)
        {
            BoundaryState = ShowBoundaries;
            ToggleBoundaryRendering(BoundaryState);
        }

        if (!Application.isPlaying) 
        {
            Init();
            GenerateMesh();
            PlateState = ShowPlates;
            TogglePlateRendering(PlateState);
        }

        Init();
        GenerateMesh();
        if (ShowPlates != PlateState)
        {
            PlateState = ShowPlates;
            TogglePlateRendering(PlateState);
        }
        if (Mathf.Abs(CurrentTimeMa - _lastTimeMa) > 0.01f)
        {
            _lastTimeMa = CurrentTimeMa;
            ApplyTimeStep(CurrentTimeMa);
        }
    }

    void Start()
    {
        Init();
        GenerateMesh();
    }

    void Update()
    {
        if ( !Application.isPlaying || _movementSystem == null || _baseGrid == null) return;

        // Advance time automatically if AutoPlay is on
        if (AutoPlay)
        {
            CurrentTimeMa += PlaybackSpeedMaPerSecond * Time.deltaTime;
            if (CurrentTimeMa > 560f) CurrentTimeMa = 0f;
        }

        // Only rebuild grid when time actually changes
        if (Mathf.Abs(CurrentTimeMa - _lastTimeMa) > 0.01f)
        {
            _lastTimeMa = CurrentTimeMa;
            ApplyTimeStep(CurrentTimeMa);
        }
    }

    /// <summary>
    /// Rebuilds the simulation grid at the given time and repaints the mesh.
    /// </summary>
    public void ApplyTimeStep(float timeMa)
    {
        if (_movementSystem == null || _baseGrid == null) return;

        // At 0 Ma use the base grid directly (no computation needed)
        if (timeMa <= 0.01f)
        {
            Grid = _baseGrid;
        }
        else
        {
            Grid = _movementSystem.BuildGridAtTime(_baseGrid, Initializer.PlateRegistry, timeMa);
        }

        // Detect plate boundaries and classify collision types
        _collisionDetector?.DetectBoundaries(Grid, Initializer.PlateRegistry, timeMa);

        // Apply elevation changes based on boundary types
        float deltaTimeMa = Mathf.Abs(timeMa - _previousTimeMa);
        _elevationSystem?.ApplyElevationStep(Grid, Initializer.PlateRegistry, deltaTimeMa);
        _previousTimeMa = timeMa;

        // Repaint all 6 mesh faces
        if (terrainFaces != null)
        {
            foreach (TerrainFaces face in terrainFaces)
            {
                if (face != null) face.UpdateSimulationData(Grid, Initializer.PlateRegistry);
            }
        }

        Debug.Log($"[Planet] Updated to {timeMa:F1} Ma.");
    }

    public void Init()
    {
        // Load Resources heightmap if not already set in inspector
        if (OceanheightMap == null) OceanheightMap = Resources.Load<Texture2D>("Ocean");
        if (TerrainheightMap == null) TerrainheightMap = Resources.Load<Texture2D>("TopoHeight");
        if (ColorMap == null) ColorMap = Resources.Load<Texture2D>("ColorMap");

        // Initialize our new Simulation Grid only once, and ONLY when playing!
        if (Initializer == null) Initializer = new GridInitializer();
        if (Grid == null)
        {
            long expectedCells = (360L * GridResolutionPerDegree) * (180L * GridResolutionPerDegree);
            if (expectedCells > 20_000_000)
            {
                Debug.LogError($"[Planet] Aborting Grid Initialization! Requested GridResolutionPerDegree of {GridResolutionPerDegree} would create {expectedCells:N0} cells, exceeding the safe limit of 20,000,000. Please lower the resolution.");
                return;
            }

            var rawPlateData = Mapping.Map(currentDataFile);
            Grid = Initializer.Initialize(rawPlateData, GridResolutionPerDegree);

            // Store a permanent base grid snapshot at 0 Ma
            _baseGrid = Grid;

            // Load rotation data and set up movement system
            if (RotationLoader == null)
            {
                RotationLoader     = new PlateRotationLoader();
                RotationLoader.Load();
                RotationLoader.LogMappingResults(Initializer.PlateRegistry);
                _movementSystem    = new PlateMovementSystem(RotationLoader);
                _collisionDetector = new PlateCollisionDetector(RotationLoader);
                _elevationSystem   = new ElevationSystem();
                _elevationSystem.InitialiseElevations(Grid, Initializer.PlateRegistry);
                Debug.Log("[Planet] Plate movement, collision and elevation systems ready.");
            }

            _lastTimeMa = 0f;
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

    public void ToggleBoundaryRendering(bool show)
    {
        ShowBoundaries = show;
        BoundaryState  = show;
        if (terrainFaces != null)
        {
            foreach (TerrainFaces face in terrainFaces)
            {
                if (face != null) face.ToggleBoundaries(show);
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

    public void ChangeColorTexture(Texture2D textureselection)
    {
        ColorMap = textureselection;
        Init();
        GenerateMesh();
    }
}