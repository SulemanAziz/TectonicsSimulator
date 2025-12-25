using UnityEngine;
using System.IO;

public class Planet : MonoBehaviour
{
    [Range(2, 256)]
    public int resolution = 10;

    [Range (1f,3f)]
    public float WATERLEVEL = 1f;

    [Range (2f, 5f)]
    public float MountainLevel = 2.25f;

    // Heightmap placed in Assets/Resources/TopoHeight.png (name without extension)
    public Texture2D OceanheightMap;
    public Texture2D heightMap;

    [Range(0f, 1f)]
    public float Oceanelevation = 0.15f;
    [Range(0f, 1f)]
    public float Topographyelevation = 0.15f;


    [SerializeField, HideInInspector]
    MeshFilter[] meshFilters;
    TerrainFaces[] terrainFaces;
    // Material that supports vertex colors (reuse for all faces)
    static Material vertexColorMaterial;
    void OnValidate()
    {
        Init();
        GenerateMesh();
    }
    void Init()
    {
        // Load Resources heightmap if not already set in inspector
        if (OceanheightMap == null)
        {
            OceanheightMap = Resources.Load<Texture2D>("BathyProcessed");
        }

        if(heightMap == null)
        {
            heightMap = Resources.Load<Texture2D>("TopoHeight");
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

                meshFilters[i].sharedMesh = new Mesh();
            }

            terrainFaces[i] = new TerrainFaces(meshFilters[i].sharedMesh, resolution, directions[i], OceanheightMap, heightMap, Oceanelevation, Topographyelevation, WATERLEVEL, MountainLevel);
        }
    }

    void GenerateMesh()
    {
        foreach(TerrainFaces faces in terrainFaces)
        {
            faces.ConstructMesh();
        }
    }
}