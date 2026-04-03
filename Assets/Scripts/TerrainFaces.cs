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
    
    // CHANGED: Using HashSet<long> instead of HashSet<string> to eliminate garbage collection strings.
    HashSet<long> PlateCoordSet;
    
    int PlatePrecisionFactor; // keys per degree (10 => 0.1° resolution)
    float PlateToleranceDegrees; // tolerance in degrees for matching
    float OceanheightMultiplier;
    float HeightMultiplier;
    float WaterLevel;
    float MountainLevel;

    // CHANGED: Caching colors for instantaneous toggle without mesh rebuilds.
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

        // Build a hash set of plate coordinates using long instead of string
        if (this.PlateMap != null)
        {
            BuildPlateHash();
        }
    }

    /// <summary>
    /// Builds the HashSet using bitwise packing to combine X and Y into a single 64-bit long integer.
    /// This vastly accelerates lookup speeds and removes string allocations entirely.
    /// </summary>
    private void BuildPlateHash()
    {
        PlateCoordSet = new HashSet<long>();
        if (this.PlateMap == null) return;
        
        foreach (var plate in this.PlateMap)
        {
            foreach (float[] pt in plate.Value)
            {
                int lonKey = Mathf.RoundToInt(pt[0] * PlatePrecisionFactor);
                int latKey = Mathf.RoundToInt(pt[1] * PlatePrecisionFactor);
                
                // Pack lon and lat into a single long to avoid string allocation
                long key = ((long)lonKey << 32) | (uint)latKey;
                PlateCoordSet.Add(key);
            }
        }
    }

    public void ConstructMesh()
    {
        UnityEngine.Vector3[] vertices = new UnityEngine.Vector3[resolution * resolution];
        
        // Initialize cached color arrays
        baseColors = new Color[resolution * resolution];
        plateColors = new Color[resolution * resolution];
        
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

                    // By default, the plate colors map matches the base colors map precisely
                    plateColors[i] = baseColors[i];

                    // Check if this vertex is on a plate boundary using fast long hashing
                    if (PlateMap != null && PlateCoordSet != null)
                    {
                        bool onPlate = false;

                        // Convert coordinate radians to degrees for comparison with PlateMap (which is in degrees)
                        int lonKey = Mathf.RoundToInt(coord.longitude * Mathf.Rad2Deg * PlatePrecisionFactor);
                        int latKey = Mathf.RoundToInt(coord.latitude * Mathf.Rad2Deg * PlatePrecisionFactor);

                        // Compute neighbor range based on desired tolerance
                        int neighborRange = Mathf.CeilToInt(PlateToleranceDegrees * PlatePrecisionFactor);

                        for (int dx = -neighborRange; dx <= neighborRange && !onPlate; dx++)
                        {
                            for (int dy = -neighborRange; dy <= neighborRange && !onPlate; dy++)
                            {
                                long key = ((long)(lonKey + dx) << 32) | (uint)(latKey + dy);
                                if (PlateCoordSet.Contains(key))
                                {
                                    plateColors[i] = new Color32(255, 255, 0, 1); // Yellow marking the boundary
                                    onPlate = true;
                                }
                            }
                        }
                    }
                }
                else
                {
                    vertices[i] = pointOnUnitSphere;
                    baseColors[i] = Color.blue;
                    plateColors[i] = Color.blue;
                }
 
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
        
        // Initial mesh colors application checking toggle state
        mesh.colors = platesCurrentlyShowing ? plateColors : baseColors;

        mesh.RecalculateNormals();
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
    /// Allows swapping the loaded plate mapping and updates colors dynamically, minimizing expensive vertex generation.
    /// </summary>
    public void UpdatePlateData(Dictionary<string, List<float[]>> newPlateMap)
    {
        this.PlateMap = newPlateMap;
        BuildPlateHash();
        RecalculatePlateColors();
    }

    /// <summary>
    /// Recalculates only the yellow boundaries. This logic ignores geometry, making dynamic map switching much faster.
    /// </summary>
    private void RecalculatePlateColors()
    {
        if (baseColors == null || plateColors == null) return;
        
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = x + y * resolution;
                
                // Reset to regular terrain color first
                plateColors[i] = baseColors[i];

                if (OceanheightMap != null || TerrainheightMap != null)
                {
                    UnityEngine.Vector2 percent = new UnityEngine.Vector2(x, y) / (resolution - 1);
                    UnityEngine.Vector3 pointOnUnitCube = localUp + (percent.x - 0.5f) * 2 * axisA + (percent.y - 0.5f) * 2 * axisB;
                    UnityEngine.Vector3 pointOnUnitSphere = PointOnUnitCubeToPointOnUnitSphere(pointOnUnitCube);
                    var coord = GeoMaths.PointToCoordinate(pointOnUnitSphere);

                    if (PlateMap != null && PlateCoordSet != null)
                    {
                        bool onPlate = false;
                        int lonKey = Mathf.RoundToInt(coord.longitude * Mathf.Rad2Deg * PlatePrecisionFactor);
                        int latKey = Mathf.RoundToInt(coord.latitude * Mathf.Rad2Deg * PlatePrecisionFactor);
                        int neighborRange = Mathf.CeilToInt(PlateToleranceDegrees * PlatePrecisionFactor);

                        for (int dx = -neighborRange; dx <= neighborRange && !onPlate; dx++)
                        {
                            for (int dy = -neighborRange; dy <= neighborRange && !onPlate; dy++)
                            {
                                long key = ((long)(lonKey + dx) << 32) | (uint)(latKey + dy);
                                if (PlateCoordSet.Contains(key))
                                {
                                    plateColors[i] = new Color32(255, 255, 0, 1); // Yellow
                                    onPlate = true;
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
