using UnityEngine;
using System.IO;
using System.Data;
using System.Collections.Generic;
using UnityEngine.Analytics;
public class Planet : MonoBehaviour
{
    [Range(2, 1024)]
    public int resolution = 128;

    [Range (5, 50)]
    public int PlatePrecisionFactor = 10;

    [Range (0.1f, 10f)]
    public float PlateToleranceDegrees = 0.3f;
    public bool ShowPlates = false;
    public string currentDataFile = "TectonicPlates";
    // Track the previous state of the toggle for smart validation
    private bool PlateState = false;

    public Texture2D OceanheightMap;
    public Texture2D TerrainheightMap;
    public Texture2D ColorMap;
    private Dictionary<string, List<float[]>> PlateMap;

    [Range(0f, 1f)]
    public float OceanElevation = 0.1f;
    [Range(0f, 1f)]
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

        if(PlateMap == null) PlateMap = Mapping.Map(currentDataFile);
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

            terrainFaces[i] = new TerrainFaces(meshFilters[i].sharedMesh, resolution, directions[i], OceanheightMap, TerrainheightMap, ColorMap, PlateMap, PlatePrecisionFactor, PlateToleranceDegrees, OceanElevation, TopographyElevation);
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