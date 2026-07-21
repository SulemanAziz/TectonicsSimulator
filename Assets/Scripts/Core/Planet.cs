using UnityEngine;
using System.IO;
using System.Data;
using System.Collections.Generic;
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
    // -60 = 60 million years in the FUTURE (ML predicted)
    // 0 = present day, 560 = 560 million years ago
    [Header("Time Control")]
    [Range(-60f, 560f)]
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
    private SimulationGrid _baseGrid;        // snapshot at 0 Ma (never modified)
    private float _previousTimeMa = 0f;     // track delta time between steps
    private float[,] _elevationBuffer;      // persists elevation values across time steps

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

        Init();
        GenerateMesh();

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
    /// Negative timeMa = future (ML predicted rotations from StreamingAssets CSVs).
    /// </summary>
    public void ApplyTimeStep(float timeMa)
    {
        if (_movementSystem == null || _baseGrid == null) return;

        // At exactly 0 Ma use the base grid directly (no computation needed).
        // NOTE: uses Abs so that negative (future) times are NOT caught here.
        if (Mathf.Abs(timeMa) <= 0.01f)
        {
            Grid = _baseGrid;
        }
        else
        {
            Grid = _movementSystem.BuildGridAtTime(_baseGrid, Initializer.PlateRegistry, timeMa);
        }

        // ── ELEVATION ACCUMULATION: restore saved elevation into the fresh grid ──
        // BuildGridAtTime returns cells with Elevation = 0. We copy the accumulated
        // elevation from the persistent buffer so the step delta builds on history.
        if (_elevationBuffer != null)
        {
            for (int ex = 0; ex < Grid.Width; ex++)
            {
                for (int ey = 0; ey < Grid.Height; ey++)
                {
                    SimulationCell ec = Grid.GetCell(ex, ey);
                    ec.Elevation = _elevationBuffer[ex, ey];
                    Grid.SetCell(ex, ey, ec);
                }
            }
        }

        // Detect plate boundaries and classify collision types
        _collisionDetector?.DetectBoundaries(Grid, Initializer.PlateRegistry, timeMa);

        // Apply elevation changes based on boundary types
        float deltaTimeMa = Mathf.Abs(timeMa - _previousTimeMa);
        _elevationSystem?.ApplyElevationStep(Grid, Initializer.PlateRegistry, deltaTimeMa);
        _previousTimeMa = timeMa;

        // ── Save updated elevations back to the persistent buffer ──
        if (_elevationBuffer != null)
        {
            for (int ex = 0; ex < Grid.Width; ex++)
                for (int ey = 0; ey < Grid.Height; ey++)
                    _elevationBuffer[ex, ey] = Grid.GetCell(ex, ey).Elevation;
        }

        // Repaint all 6 mesh faces (colors + elevation displacement)
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
                RotationLoader.LoadFuturePredictions();
                RotationLoader.LogMappingResults(Initializer.PlateRegistry);
                _movementSystem    = new PlateMovementSystem(RotationLoader);
                _collisionDetector = new PlateCollisionDetector(RotationLoader);
                _elevationSystem   = new ElevationSystem();
                _elevationSystem.InitialiseElevations(Grid, Initializer.PlateRegistry);

                // Seed the persistent elevation buffer from the just-initialised grid
                _elevationBuffer = new float[Grid.Width, Grid.Height];
                for (int ex = 0; ex < Grid.Width; ex++)
                    for (int ey = 0; ey < Grid.Height; ey++)
                        _elevationBuffer[ex, ey] = Grid.GetCell(ex, ey).Elevation;

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

    /// <summary>
    /// Updates the resolution of a single face mesh for LOD.
    /// Called by CameraCulling when the camera is close enough to upgrade a face.
    /// </summary>
    public void UpdateFaceResolution(int faceIndex, int targetResolution)
    {
        if (terrainFaces != null && faceIndex >= 0 && faceIndex < terrainFaces.Length)
        {
            if (terrainFaces[faceIndex] != null)
            {
                terrainFaces[faceIndex].UpdateResolution(targetResolution);
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
            _baseGrid = Grid;

            // Re-initialize elevation system and buffer for the new dataset
            if (_elevationSystem != null)
            {
                _elevationSystem.InitialiseElevations(Grid, Initializer.PlateRegistry);
                _elevationBuffer = new float[Grid.Width, Grid.Height];
                for (int ex = 0; ex < Grid.Width; ex++)
                    for (int ey = 0; ey < Grid.Height; ey++)
                        _elevationBuffer[ex, ey] = Grid.GetCell(ex, ey).Elevation;
            }
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