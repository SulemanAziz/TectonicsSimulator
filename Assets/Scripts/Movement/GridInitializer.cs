using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// A dedicated utility class responsible for taking raw boundary data and
/// converting it into a fully initialized, solidly filled SimulationGrid.
/// </summary>
public class GridInitializer
{
    public Dictionary<int, TectonicPlate> PlateRegistry { get; private set; }
    
    // Bounding boxes [minLon, maxLon, minLat, maxLat] to skip 95% of math calculations
    private Dictionary<int, float[]> PlateBoundingBoxes { get; set; }
    
    // The mathematically clean, unwrapped polygons used for the Raycast
    private Dictionary<int, List<float[]>> UnwrappedPlateMap { get; set; }

    public GridInitializer()
    {
        PlateRegistry = new Dictionary<int, TectonicPlate>();
        PlateBoundingBoxes = new Dictionary<int, float[]>();
        UnwrappedPlateMap = new Dictionary<int, List<float[]>>();
    }

    public SimulationGrid Initialize(Dictionary<string, List<float[]>> rawPlateMap, int resolutionPerDegree)
    {
        // 1. CLEAR STATE to prevent errors on re-initialization
        PlateRegistry.Clear();
        PlateBoundingBoxes.Clear();
        UnwrappedPlateMap.Clear();

        SimulationGrid grid = new SimulationGrid(resolutionPerDegree);

        // 2. SAFEGUARD against empty maps
        if (rawPlateMap == null || rawPlateMap.Count == 0) return grid;

        BuildPlateRegistry(rawPlateMap);
        FillGrid(grid);
        ResolveUnassignedCells(grid);
        ValidateGrid(grid);
        return grid;
    }

    /// <summary>
    /// Converts string keys into proper TectonicPlate objects.
    /// CRITICAL FIX: "Unwraps" the coordinates so plates crossing the International Date Line
    /// are continuous in 2D space, and explicitly closes plates that wrap around the poles.
    /// </summary>
    private void BuildPlateRegistry(Dictionary<string, List<float[]>> rawPlateMap)
    {
        int currentId = 0;
        foreach (var plateEntry in rawPlateMap)
        {
            string plateName = plateEntry.Key;
            List<float[]> rawPolygon = plateEntry.Value;

            Color32[] palette = new Color32[6];
            byte alpha = 20; // Doesn't seem to change much
            palette[0] = new Color32(0, 240, 230,alpha); //Cyan
            palette[1] = new Color32(230, 0, 128,alpha); //Magenta
            palette[2] = new Color32(230, 235, 0,alpha); //Yellow
            palette[3] = new Color32(140, 0, 230,alpha); //Violet
            palette[4] = new Color32(230, 100, 0,alpha); //Orange-ish
            palette[5] = new Color32(50, 230, 0,alpha); //Lime Green

            long seed = 2654435761; // This is a magic number, change it to vary the colour layout

            int randomcolorindex = (int) math.floor( currentId * seed  % palette.Length );
            Color32 plateColor = palette[randomcolorindex];
            
            TectonicPlate newPlate = new TectonicPlate(currentId, plateName, plateColor);
            PlateRegistry.Add(currentId, newPlate);

            // 1. UNWRAP CONTINUOUS POLYGON
            // This prevents Date Line (-180 to +180) jumps from breaking the Raycast
            List<float[]> unwrapped = new List<float[]>();
            float currentOffset = 0f;
            float sumLat = 0f;

            for (int i = 0; i < rawPolygon.Count; i++)
            {
                if (i > 0)
                {
                    float diff = rawPolygon[i][0] - rawPolygon[i - 1][0];
                    if (diff > 180f) currentOffset -= 360f;
                    else if (diff < -180f) currentOffset += 360f;
                }
                
                float uLon = rawPolygon[i][0] + currentOffset;
                float uLat = rawPolygon[i][1];
                unwrapped.Add(new float[] { uLon, uLat });
                sumLat += uLat;
            }

            // 2. POLAR SEALING
            // If the polygon made a full 360 degree lap around the globe, it contains a pole.
            // A 2D Raycast requires closed boundaries, so we draw a line explicitly along the pole.
            if (Mathf.Abs(currentOffset) >= 350f) 
            {
                float avgLat = sumLat / rawPolygon.Count;
                float poleLat = avgLat > 0 ? 90f : -90f; // North or South Pole

                float lastLon = unwrapped[unwrapped.Count - 1][0];
                float firstLon = unwrapped[0][0];
                
                // Seal the gap across the absolute top or bottom of the map
                unwrapped.Add(new float[] { lastLon, poleLat });
                unwrapped.Add(new float[] { firstLon, poleLat });
            }

            UnwrappedPlateMap.Add(currentId, unwrapped);

            // 3. BOUNDING BOXES
            float minLon = float.MaxValue, maxLon = float.MinValue;
            float minLat = float.MaxValue, maxLat = float.MinValue;
            
            foreach (var p in unwrapped)
            {
                if (p[0] < minLon) minLon = p[0];
                if (p[0] > maxLon) maxLon = p[0];
                if (p[1] < minLat) minLat = p[1];
                if (p[1] > maxLat) maxLat = p[1];
            }

            PlateBoundingBoxes.Add(currentId, new float[] { minLon, maxLon, minLat, maxLat });
            currentId++;
        }
    }

    /// <summary>
    /// Fills the grid using Parallel multi-threading and the advanced Unwrapped Bounding Boxes.
    /// </summary>
    private void FillGrid(SimulationGrid grid)
    {
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        Debug.Log("[GridInitializer] Starting Optimized Grid Fill Algorithm...");

        Parallel.For(0, grid.Width, x =>
        {
            float lonDeg = ((float)x / grid.Width) * 360f - 180f;

            for (int y = 0; y < grid.Height; y++)
            {
                float latDeg = ((float)y / grid.Height) * 180f - 90f;
                SimulationCell cell = SimulationCell.Default();

                foreach (var plateEntry in PlateRegistry)
                {
                    int plateId = plateEntry.Key;
                    float[] bounds = PlateBoundingBoxes[plateId];
                    List<float[]> polygon = UnwrappedPlateMap[plateId];

                    // Zero-allocation inline check for standard and unwrapped coordinates
                    if ((lonDeg >= bounds[0] && lonDeg <= bounds[1] && latDeg >= bounds[2] && latDeg <= bounds[3] && IsPointInPolygon(lonDeg, latDeg, polygon)) ||
                        (lonDeg - 360f >= bounds[0] && lonDeg - 360f <= bounds[1] && latDeg >= bounds[2] && latDeg <= bounds[3] && IsPointInPolygon(lonDeg - 360f, latDeg, polygon)) ||
                        (lonDeg + 360f >= bounds[0] && lonDeg + 360f <= bounds[1] && latDeg >= bounds[2] && latDeg <= bounds[3] && IsPointInPolygon(lonDeg + 360f, latDeg, polygon)))
                    {
                        cell.PlateId = plateId;
                        break; // Already assigned this cell, skip remaining plates
                    }
                }
                
                grid.SetCell(x, y, cell);
            }
        });
        
        sw.Stop();
        Debug.Log($"[GridInitializer] Grid Fill Complete in {sw.ElapsedMilliseconds} ms!");
    }

    /// <summary>
    /// Mathematical Ray-Casting intersection algorithm. 
    /// Now incredibly robust because coordinates are mathematically unwrapped and sealed!
    /// </summary>
    private bool IsPointInPolygon(float testLon, float testLat, List<float[]> polygon)
    {
        bool isInside = false;
        int j = polygon.Count - 1;

        for (int i = 0; i < polygon.Count; i++)
        {
            float p1Lon = polygon[i][0];
            float p1Lat = polygon[i][1];
            float p2Lon = polygon[j][0];
            float p2Lat = polygon[j][1];

            // Counts raycast intersections shooting horizontally to the LEFT (-X axis)
            if ((p1Lat < testLat && p2Lat >= testLat || p2Lat < testLat && p1Lat >= testLat) &&
                (p1Lon + (testLat - p1Lat) / (p2Lat - p1Lat) * (p2Lon - p1Lon) < testLon))
            {
                isInside = !isInside;
            }
            j = i;
        }

        return isInside;
    }

    private void ValidateGrid(SimulationGrid grid)
    {
        int unassignedCount = 0;
        int totalCells = grid.Width * grid.Height;

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                if (grid.GetCell(x, y).PlateId == -1) unassignedCount++;
            }
        }

        if (unassignedCount > 0)
        {
            float errorPercentage = ((float)unassignedCount / totalCells) * 100f;
            Debug.LogWarning($"[GridInitializer Validation] WARNING: {unassignedCount} cells ({errorPercentage:F2}%) remain unassigned (PlateId == -1)!");
        }
        else
        {
            Debug.Log("[GridInitializer Validation] SUCCESS: 100% of the planet's surface was successfully claimed by tectonic plates!");
        }
    }

    /// <summary>
    /// Finds any cells that remained unassigned due to mathematical boundary precision issues,
    /// and assigns them to the plate of their nearest valid neighbor.
    /// </summary>
    private void ResolveUnassignedCells(SimulationGrid grid)
    {
        int resolvedCount = 0;
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                SimulationCell cell = grid.GetCell(x, y);
                if (cell.PlateId == -1)
                {
                    // Look at neighbors to find a valid plate ID
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = (x + dx[i] + grid.Width) % grid.Width; // Wrap longitude
                        int ny = Mathf.Clamp(y + dy[i], 0, grid.Height - 1); // Clamp latitude

                        SimulationCell neighbor = grid.GetCell(nx, ny);
                        if (neighbor.PlateId != -1)
                        {
                            cell.PlateId = neighbor.PlateId;
                            grid.SetCell(x, y, cell);
                            resolvedCount++;
                            break;
                        }
                    }
                }
            }
        }
        if (resolvedCount > 0)
        {
            Debug.Log($"[GridInitializer] Resolved {resolvedCount} unassigned boundary/precision cells by assigning them to neighboring plates.");
        }
    }
}
