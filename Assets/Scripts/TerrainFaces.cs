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
    Texture2D heightMap;
    Dictionary<string, List<float[]>> PlateMap;
    HashSet<string> PlateCoordSet;
    int PlatePrecisionFactor; // keys per degree (10 => 0.1° resolution)
    float PlateToleranceDegrees; // tolerance in degrees for matching
    float OceanheightMultiplier;
    float HeightMultiplier;
    float WaterLevel;
    float MountainLevel;

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
    
    public TerrainFaces(Mesh m, int res, UnityEngine.Vector3 up, Texture2D Oceanheightmap, Texture2D heightMap = null, Dictionary<string, List<float[]>> PlateMap = null, int PlatePrecisionFactor = 10, float PlateToleranceDegrees = 0.5f , float OceanheightMultiplier = 0f, float HeightMultiplier = 0f, float WaterLevel = 1f, float MountainLevel = 2.25f)
    {
        mesh = m;
        resolution = res;
        localUp = up;

        axisA = new UnityEngine.Vector3(localUp.y, localUp.z, localUp.x);
        axisB = UnityEngine.Vector3.Cross(localUp, axisA);

        this.OceanheightMap = Oceanheightmap;
        this.heightMap = heightMap;
        this.PlateMap = PlateMap;

        // Build a hash set of plate coordinates
        if (this.PlateMap != null)
        {
            PlateCoordSet = new HashSet<string>();
            foreach (var plate in this.PlateMap)
            {
                foreach (float[] pt in plate.Value)
                {
                    int lonKey = Mathf.RoundToInt(pt[0] * PlatePrecisionFactor);
                    int latKey = Mathf.RoundToInt(pt[1] * PlatePrecisionFactor);
                    PlateCoordSet.Add(lonKey + ":" + latKey);
                }
            }
        }

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
        Color[] colors = new Color[resolution * resolution];
        
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
                
                if (OceanheightMap != null || heightMap != null)
                {
                    var coord = GeoMaths.PointToCoordinate(pointOnUnitSphere);

                    float u = (coord.longitude / (Mathf.PI * 2f)) + 0.5f;
                    float v = (coord.latitude  / Mathf.PI) + 0.5f;

                    float sampleO = OceanheightMap.GetPixelBilinear(u, v).r; // assume grayscale
                    
                    float radiusO = 1f + sampleO * OceanheightMultiplier;

                    float sample = heightMap.GetPixelBilinear(u,v).r;
                    
                    float radius = 1f + sample * HeightMultiplier;

                    vertices[i] = pointOnUnitSphere * (radiusO + radius);

                    Color mountaincolor = new Color32(245,245,245,1); // White Smoke
                    Color terraincolor = new Color32(128,200,19,1); // Muted Green
                    Color watercolor = new Color32(0,102,204,1); // Ocean Blue
                    
                    if(radiusO + radius > WaterLevel)
                    {
                        if (radiusO + radius > MountainLevel)
                        {
                            colors[i] = mountaincolor;
                        }
                        else
                        colors[i] = terraincolor;
                    }
                    else
                    {
                        colors[i] = watercolor;
                    }

                    // Check if this vertex is on a plate boundary using hashed coords for performance
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
                                string key = (lonKey + dx) + ":" + (latKey + dy);
                                if (PlateCoordSet.Contains(key))
                                {
                                    colors[i] = new Color32(255, 255, 0, 1); // Yellow
                                    onPlate = true;
                                }
                            }
                        }
                    }
                }
                else
                {
                    vertices[i] = pointOnUnitSphere;
                    colors[i] = Color.blue;
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
        mesh.colors = colors;

        mesh.RecalculateNormals();
    }
}
