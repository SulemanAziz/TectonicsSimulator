using System;
using System.Collections.Generic;
using System.Linq;
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
    bool boundariesShowing = true;

    // Base sphere vertices (from heightmap only, before simulation elevation).
    // Stored so we can re-apply updated SimulationCell.Elevation every time step
    // without re-baking the expensive heightmap texture lookups.
    private Vector3[] _baseVertices;

    // Scale factor: how much 1.0 unit of cell.Elevation displaces the mesh radially.
    // Keep small so it overlays on top of the heightmap displacement.
    private const float ElevationScale = 0.04f;

    // Boundary colors
    static readonly Color ConvergentColor = new Color(1f,   0.15f, 0.15f); // red
    static readonly Color DivergentColor  = new Color(0.2f, 0.5f,  1f);    // blue
    static readonly Color TransformColor  = new Color(1f,   0.85f, 0.1f);  // yellow

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
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Store base vertices (heightmap-displaced positions) for later elevation updates
        _baseVertices = (Vector3[])vertices.Clone();

        // Apply initial simulation elevation displacement (if grid is already loaded)
        UpdateElevationDisplacement();

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
        // Re-displace vertices from the accumulated elevation then refresh colors
        UpdateElevationDisplacement();
        RecalculatePlateColors();
    }

    /// <summary>
    /// Offsets each mesh vertex radially by SimulationCell.Elevation on top of the
    /// base heightmap position stored in _baseVertices.
    /// Called every time the simulation grid is updated.
    /// </summary>
    private void UpdateElevationDisplacement()
    {
        if (_baseVertices == null || Grid == null || mesh == null) return;

        Vector3[] displaced = (Vector3[])_baseVertices.Clone();

        for (int i = 0; i < displaced.Length; i++)
        {
            Vector3 pointOnUnitSphere = _baseVertices[i].normalized;
            var coord = GeoMaths.PointToCoordinate(pointOnUnitSphere);

            int gridX = Mathf.Clamp(
                Mathf.FloorToInt((coord.longitude * Mathf.Rad2Deg + 180f) * (Grid.Width  / 360f)),
                0, Grid.Width  - 1);
            int gridY = Mathf.Clamp(
                Mathf.FloorToInt((coord.latitude  * Mathf.Rad2Deg +  90f) * (Grid.Height / 180f)),
                0, Grid.Height - 1);

            SimulationCell cell = Grid.GetCell(gridX, gridY);
            if (cell.PlateId < 0) continue;

            // Displace radially outward by scaled elevation
            displaced[i] = _baseVertices[i] + pointOnUnitSphere * (cell.Elevation * ElevationScale);
        }

        mesh.vertices = displaced;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    public void ToggleBoundaries(bool show)
    {
        boundariesShowing = show;
        RecalculatePlateColors();
    }

    private void RecalculatePlateColors()
    {
        if (baseColors == null || plateColors == null) return;
        
        // 1. Instantly copy the pristine terrain over everything to clear old plates
        System.Array.Copy(baseColors, plateColors, baseColors.Length);

        // 2. Map new plates and boundaries by querying the SimulationGrid
        if (Grid != null && PlateRegistry != null && mesh.vertices != null)
        {
            UnityEngine.Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                UnityEngine.Vector3 pointOnUnitSphere = vertices[i].normalized;
                var coord = GeoMaths.PointToCoordinate(pointOnUnitSphere);

                // Convert lat/lon to grid indices to read the full SimulationCell
                int gridX = Mathf.Clamp(Mathf.FloorToInt((coord.longitude * Mathf.Rad2Deg + 180f) * (Grid.Width  / 360f)), 0, Grid.Width  - 1);
                int gridY = Mathf.Clamp(Mathf.FloorToInt((coord.latitude  * Mathf.Rad2Deg +  90f) * (Grid.Height / 180f)), 0, Grid.Height - 1);
                SimulationCell cell = Grid.GetCell(gridX, gridY);

                if (cell.PlateId == -1) continue;

                // Boundary rendering takes priority over plate color
                if (boundariesShowing && cell.Boundary != BoundaryType.None)
                {
                    Color boundaryColor = cell.Boundary switch
                    {
                        BoundaryType.Convergent => ConvergentColor,
                        BoundaryType.Divergent  => DivergentColor,
                        BoundaryType.Transform  => TransformColor,
                        _                       => baseColors[i]
                    };
                    plateColors[i] = boundaryColor;
                }
                else if (PlateRegistry.TryGetValue(cell.PlateId, out TectonicPlate plate))
                {
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
