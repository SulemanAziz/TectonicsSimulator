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
    Texture2D ColorMap;
    public SimulationGrid Grid;
    public Dictionary<int, TectonicPlate> PlateRegistry;
    float OceanheightMultiplier;
    float HeightMultiplier;
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
    
    public TerrainFaces(Mesh m, int res, UnityEngine.Vector3 up, Texture2D Oceanheightmap, Texture2D TerrainheightMap = null, Texture2D ColorMap = null, SimulationGrid grid = null, Dictionary<int, TectonicPlate> plateRegistry = null, float OceanheightMultiplier = 0f, float HeightMultiplier = 0f)
    {
        mesh = m;
        resolution = res;
        localUp = up;

        axisA = new UnityEngine.Vector3(localUp.y, localUp.z, localUp.x);
        axisB = UnityEngine.Vector3.Cross(localUp, axisA);

        this.OceanheightMap = Oceanheightmap;
        this.TerrainheightMap = TerrainheightMap;
        this.Grid = grid;
        this.PlateRegistry = plateRegistry;
        this.ColorMap = ColorMap;

        this.OceanheightMultiplier = OceanheightMultiplier;
        this.HeightMultiplier = HeightMultiplier;
    }

    public void ConstructMesh()
    {
        UnityEngine.Vector3[] vertices = new UnityEngine.Vector3[resolution * resolution];
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

                    float Oceansample = OceanheightMap.GetPixelBilinear(u, v).maxColorComponent;
                    float Oceanradius = 1f + Oceansample * OceanheightMultiplier;
                    float Terrainsample = TerrainheightMap.GetPixelBilinear(u,v).maxColorComponent;
                    float Terrainradius = 1f + Terrainsample * HeightMultiplier;

                    vertices[i] = pointOnUnitSphere * (Oceanradius + Terrainradius);

                    // Read color from the colormap
                    baseColors[i] = ColorMap.GetPixelBilinear(u,v);
                    plateColors[i] = baseColors[i];
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
        
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        // Calculate initial plate overlays and apply colors to mesh
        RecalculatePlateColors();
    }

    public void TogglePlates(bool show)
    {
        platesCurrentlyShowing = show;
        if (mesh != null && baseColors != null && plateColors != null)
        {
            mesh.colors = platesCurrentlyShowing ? plateColors : baseColors;
        }
    }

    public void UpdateSimulationData(SimulationGrid newGrid, Dictionary<int, TectonicPlate> newRegistry)
    {
        this.Grid = newGrid;
        this.PlateRegistry = newRegistry;
        RecalculatePlateColors();
    }

    private void RecalculatePlateColors()
    {
        if (baseColors == null || plateColors == null) return;
        
        // 1. Instantly copy the pristine terrain over everything to clear old plates
        System.Array.Copy(baseColors, plateColors, baseColors.Length);

        // 2. Map new plates by querying the SimulationGrid!
        // This fully replaces the old N^2 spatial hash boundary-line logic.
        if (Grid != null && PlateRegistry != null && mesh.vertices != null)
        {
            UnityEngine.Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                // Normalize the vertex to get its position on a unit sphere, ignoring elevation displacement
                UnityEngine.Vector3 pointOnUnitSphere = vertices[i].normalized;
                
                // Convert 3D position to Lat/Lon radians
                var coord = GeoMaths.PointToCoordinate(pointOnUnitSphere);
                
                // Query our Grid in O(1) time!
                int plateId = Grid.GetPlateIdAt(coord.latitude, coord.longitude);

                // If the cell was properly assigned by GridInitializer, color it with the plate's distinct color
                if (plateId != -1 && PlateRegistry.TryGetValue(plateId, out TectonicPlate plate))
                {
                    // Blend the solid plate color with the base terrain texture for a nice overlay effect
                    plateColors[i] = Color.Lerp(baseColors[i], plate.DisplayColor, 0.5f); 
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
