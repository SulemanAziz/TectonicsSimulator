using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using UnityEngine.Rendering;
public class TerrainFaces
{
    Mesh mesh;
    int resolution;
    UnityEngine.Vector3 localUp;
    UnityEngine.Vector3 axisA;
    UnityEngine.Vector3 axisB;
    Texture2D OceanheightMap;
    Texture2D TerrainheightMap;
    Dictionary<string, List<float[]>> PlateMap;
    
    // CHANGED: Instead of hashing plates, we hash the physical vertices for O(P) boundary updates!
    // This maps a coordinate key to a list of vertex indices.
    Dictionary<long, List<int>> vertexSpatialHash;
    
    int PlatePrecisionFactor; // keys per degree (10 => 0.1° resolution)
    float PlateToleranceDegrees; // tolerance in degrees for matching
    float OceanheightMultiplier;
    float HeightMultiplier;
    float WaterLevel;
    float MountainLevel;

    // Caching colors for instantaneous toggle
    Color[] baseColors;
    Color[] plateColors;
    bool platesCurrentlyShowing = true;

    public static UnityEngine.Vector3 PointOnUnitCubeToPointOnUnitSphere(UnityEngine.Vector3 p)
    {
        float x2 = p.x * p.x;
        float y2 = p.y * p.y;
        float z2 = p.z * p.z;

        float x = (float)(p.x * Math.Sqrt(1 - (y2+z2)/2 + (y2*z2)/3 ) ) ;
        float y = (float)(p.y * Math.Sqrt(1 - (z2+x2)/2 + (z2*x2)/3 ) ) ;
        float z = (float)(p.z * Math.Sqrt(1 - (x2+y2)/2 + (x2*y2)/3 ) ) ;

        return new UnityEngine.Vector3(x,y,z);
    }
    
    public TerrainFaces(Mesh m, int res, UnityEngine.Vector3 up, Texture2D Oceanheightmap, Texture2D TerrainheightMap = null, Dictionary<string, List<float[]>> PlateMap = null, int PlatePrecisionFactor = 10, float PlateToleranceDegrees = 0.5f , float OceanheightMultiplier = 0f, float HeightMultiplier = 0f, float WaterLevel = 1f, float MountainLevel = 2.25f)
    {
        mesh = m;
        resolution = res;
        localUp = up;

        axisA = new UnityEngine.Vector3(localUp.y, localUp.z, localUp.x);
        axisB = UnityEngine.Vector3.Cross(localUp, axisA);

        this.OceanheightMap = Oceanheightmap;
        this.TerrainheightMap = TerrainheightMap;
        this.PlateMap = PlateMap;

        this.OceanheightMultiplier = OceanheightMultiplier;
        this.HeightMultiplier = HeightMultiplier;
        this.WaterLevel = WaterLevel;
        this.MountainLevel = MountainLevel;

        this.PlatePrecisionFactor = PlatePrecisionFactor;
        this.PlateToleranceDegrees = PlateToleranceDegrees;
    }

    public void ConstructMesh()
    {
        UnityEngine.Vector3[] vertices = new UnityEngine.Vector3[resolution * resolution];
        
        // Initialize cached color arrays
        baseColors = new Color[resolution * resolution];
        plateColors = new Color[resolution * resolution];
        
        // Initialize our reverse lookup map
        vertexSpatialHash = new Dictionary<long, List<int>>();
        
        int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];
        int triIndex = 0;
 
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = x + y * resolution;
                UnityEngine.Vector2 percent = new UnityEngine.Vector2(x, y) / (resolution - 1);
                UnityEngine.Vector3 pointOnUnitCube = localUp + (percent.x - 0.5f) * 2 * axisA + (percent.y - 0.5f) * 2 * axisB;

                UnityEngine.Vector3 pointOnUnitSphere = PointOnUnitCubeToPointOnUnitSphere(pointOnUnitCube);
 
                // Sample Bathymetry heightmap and Topography heightmap then displace radially
                if (OceanheightMap != null || TerrainheightMap != null)
                {
                    var coord = GeoMaths.PointToCoordinate(pointOnUnitSphere);

                    float u = (coord.longitude / (Mathf.PI * 2f)) + 0.5f;
                    float v = (coord.latitude  / Mathf.PI) + 0.5f;

                    float sampleO = OceanheightMap.GetPixelBilinear(u, v).r; // assume grayscale
                    float radiusO = 1f + sampleO * OceanheightMultiplier;
                    float sample = TerrainheightMap.GetPixelBilinear(u,v).r;
                    float radius = 1f + sample * HeightMultiplier;

                    vertices[i] = pointOnUnitSphere * (radiusO + radius);

                    // Build the Spatial Hash for this vertex (used for rapid Boundary-Centric physics updating later)
                    int lonKey = Mathf.RoundToInt(coord.longitude * Mathf.Rad2Deg * PlatePrecisionFactor);
                    int latKey = Mathf.RoundToInt(coord.latitude * Mathf.Rad2Deg * PlatePrecisionFactor);
                    long key = ((long)lonKey << 32) | (uint)latKey;
                    
                    if (!vertexSpatialHash.ContainsKey(key)) {
                        vertexSpatialHash[key] = new List<int>();
                    }
                    vertexSpatialHash[key].Add(i);

                    // Apply topography paint
                    Color mountaincolor = new Color32(245,245,245,1); // White Smoke
                    Color terraincolor = new Color32(128,200,19,1); // Muted Green
                    Color watercolor = new Color32(0,102,204,1); // Ocean Blue
                    
                    if(radiusO + radius > WaterLevel)
                    {
                        if (radiusO + radius > MountainLevel)
                        {
                            baseColors[i] = mountaincolor;
                        }
                        else {
                            baseColors[i] = terraincolor;
                        }
                    }
                    else
                    {
                        baseColors[i] = watercolor;
                    }
                }
                else
                {
                    vertices[i] = pointOnUnitSphere;
                    baseColors[i] = Color.blue;
                }

                // By default, mirror clean terrain into plate array temporarily
                plateColors[i] = baseColors[i];
 
                if (x != resolution - 1 && y != resolution - 1)
                {
                    //Clockwise triangle coordinates on vertices
                    triangles[triIndex] = i;
                    triangles[triIndex + 1] = i + resolution + 1;
                    triangles[triIndex + 2] = i + resolution;

                    triangles[triIndex + 3] = i;
                    triangles[triIndex + 4] = i + 1;
                    triangles[triIndex + 5] = i + resolution + 1;

                    triIndex += 6;
                }
            }
        }

        mesh.Clear();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; //Update maximum integer representation limit to increase resolution.

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        
        mesh.RecalculateNormals();

        // Calculate initial plate overlays and apply colors to mesh
        RecalculatePlateColors();
    }

    /// <summary>
    /// Swaps the rendered mesh colors without recalculating the geometry. O(1) instantaneous operation.
    /// </summary>
    public void TogglePlates(bool show)
    {
        platesCurrentlyShowing = show;
        if (mesh != null && baseColors != null && plateColors != null)
        {
            mesh.colors = platesCurrentlyShowing ? plateColors : baseColors;
        }
    }

    /// <summary>
    /// Swaps the loaded plate mapping and instantly recalculates boundary colors using fast O(P) boundary-centric updates.
    /// Perfectly suited for real-time Plate Simulation updates!
    /// </summary>
    public void UpdatePlateData(Dictionary<string, List<float[]>> newPlateMap)
    {
        this.PlateMap = newPlateMap;
        RecalculatePlateColors();
    }

    /// <summary>
    /// NEW BOUNDARY-CENTRIC ALGORITHM:
    /// Recalculates only the yellow boundaries via O(P) Plate-Data scanning. 
    /// This completely skips sweeping through the entire globe grid!
    /// </summary>
    private void RecalculatePlateColors()
    {
        if (baseColors == null || plateColors == null) return;
        
        // 1. Instantly copy the pristine terrain over everything to clear old plates
        System.Array.Copy(baseColors, plateColors, baseColors.Length);

        // 2. Map new plates using our Spatial Dictionary mapping!
        if (PlateMap != null && vertexSpatialHash != null)
        {
            int neighborRange = Mathf.CeilToInt(PlateToleranceDegrees * PlatePrecisionFactor);

            // ONLY iterate through actual Plate Data, completely removing N^2 mesh loop bottleneck
            foreach (var plate in PlateMap)
            {
                foreach (float[] pt in plate.Value)
                {
                    int lonKey = Mathf.RoundToInt(pt[0] * PlatePrecisionFactor);
                    int latKey = Mathf.RoundToInt(pt[1] * PlatePrecisionFactor);

                    // Add simulation thickness to our mathematical line
                    for (int dx = -neighborRange; dx <= neighborRange; dx++)
                    {
                        for (int dy = -neighborRange; dy <= neighborRange; dy++)
                        {
                            // Craft candidate hash
                            long key = ((long)(lonKey + dx) << 32) | (uint)(latKey + dy);
                            
                            // Check our fast spatial cache to see if ANY physical mesh vertex exists here
                            if (vertexSpatialHash.TryGetValue(key, out List<int> vertexIndices))
                            {
                                // Paint every matching vertex index yellow
                                foreach (int idx in vertexIndices)
                                {
                                    plateColors[idx] = new Color32(255, 255, 0, 1);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Apply updated colors to mesh immediately if supposed to be visible
        if (platesCurrentlyShowing && mesh != null)
        {
            mesh.colors = plateColors;
        }
    }
}
