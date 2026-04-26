using System.Collections.Generic;
using System.Threading.Tasks;
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

            Color plateColor = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);
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
}
