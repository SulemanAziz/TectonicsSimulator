using UnityEngine;
using System.IO;
using System.Data;
using System.Collections.Generic;
using UnityEngine.Analytics;

public class Planet : MonoBehaviour
{
    [Range(2, 256)]
    public int resolution = 10;

    [Range (1f,3f)]
    public float WATERLEVEL = 1f;

    [Range (2f, 5f)]
    public float MountainLevel = 2.25f;
    public Texture2D OceanheightMap;
    public Texture2D heightMap;

    public Dictionary<string, List<List<float>>> PlateMap;

    [Range(0f, 1f)]
    public float Oceanelevation = 0.15f;
    [Range(0f, 1f)]
    public float Topographyelevation = 0.15f;

    [SerializeField, HideInInspector]
    MeshFilter[] meshFilters;
    TerrainFaces[] terrainFaces;

    void OnValidate()
    {
        // Only regenerate automatically if we are NOT playing the game
        if (!Application.isPlaying) 
        {
            Init();
            GenerateMesh();
        }
    }

    // --- CHANGE 2: Added Start() so it builds on Play ---
    void Start()
    {
        Init();
        GenerateMesh();
    }

    void Init()
    {
        // Load Resources heightmap if not already set in inspector
        if (OceanheightMap == null) OceanheightMap = Resources.Load<Texture2D>("BathyProcessed");
        if (heightMap == null) heightMap = Resources.Load<Texture2D>("TopoHeight");
        if(PlateMap == null){
            /* string path = "TectonicPlates";
            PlateMap = Mapping.Map(path); */
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

            terrainFaces[i] = new TerrainFaces(meshFilters[i].sharedMesh, resolution, directions[i], OceanheightMap, heightMap, PlateMap, Oceanelevation, Topographyelevation, WATERLEVEL, MountainLevel);
        }
    }

    void GenerateMesh()
    {
        if (terrainFaces == null) return; 

        foreach(TerrainFaces faces in terrainFaces)
        {
            faces.ConstructMesh();
        }
    }
}