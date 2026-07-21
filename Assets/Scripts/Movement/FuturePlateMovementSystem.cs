using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Applies future plate movement predictions from ML-generated CSV files.
///
/// Each CSV (e.g. prediction_5Ma.csv) lives in StreamingAssets/ and contains
/// predicted Euler pole rotations for every major plate ID at that future time.
///
/// CSV schema: plateId,pred_timeMa,pred_poleLat,pred_poleLon,pred_angleDeg
///
/// The algorithm is identical to PlateMovementSystem (Euler-pole inverse rotation),
/// but the rotation data comes from the prediction CSVs instead of MerdithPlateRotations.json.
/// </summary>
public class FuturePlateMovementSystem
{
    // Available discrete future snapshots (Ma into the future, positive numbers)
    public static readonly int[] AvailableSteps = { 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60 };

    // Cache loaded CSV data: key = future Ma (5,10,...60), value = (plateId → rotation)
    private readonly Dictionary<int, Dictionary<int, FuturePoleRecord>> _cache
        = new Dictionary<int, Dictionary<int, FuturePoleRecord>>();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the SimulationGrid for a future time in Ma (0 = present, 60 = 60 Ma future).
    /// Snaps to the nearest available CSV step (multiples of 5).
    /// Returns null if no data is available.
    /// </summary>
    public SimulationGrid BuildFutureGridAtTime(SimulationGrid baseGrid,
                                                Dictionary<int, TectonicPlate> plateRegistry,
                                                float futureTimeMa)
    {
        if (futureTimeMa <= 0.01f) return baseGrid;

        int snappedMa = SnapToNearest(futureTimeMa);
        var records   = GetRecords(snappedMa);
        if (records == null || records.Count == 0)
        {
            Debug.LogWarning($"[FutureMovement] No prediction data for {snappedMa} Ma. Returning base grid.");
            return baseGrid;
        }

        // Build quaternion per Merdith plate ID from the CSV
        Dictionary<int, Quaternion> rotations        = BuildRotations(records);
        Dictionary<int, Quaternion> inverseRotations = new Dictionary<int, Quaternion>();
        foreach (var kvp in rotations)
            inverseRotations[kvp.Key] = Quaternion.Inverse(kvp.Value);

        // Build a plateId → Merdith ID lookup for fast matching
        Dictionary<int, int> plateToMerdith = BuildPlateToMerdithMap(plateRegistry);

        int width  = baseGrid.Width;
        int height = baseGrid.Height;
        int resPerDegree = width / 360;
        SimulationGrid newGrid = new SimulationGrid(resPerDegree);

        Parallel.For(0, width, x =>
        {
            float lonDeg = ((float)x / width) * 360f - 180f;
            float lonRad = lonDeg * Mathf.Deg2Rad;

            for (int y = 0; y < height; y++)
            {
                float latDeg = ((float)y / height) * 180f - 90f;
                float latRad = latDeg * Mathf.Deg2Rad;

                Vector3 point = LatLonToPoint(latRad, lonRad);

                int bestPlateId = FindPlateForPoint(point, inverseRotations,
                                                    plateToMerdith, baseGrid);

                SimulationCell cell = SimulationCell.Default();
                cell.PlateId = bestPlateId;
                newGrid.SetCell(x, y, cell);
            }
        });

        return newGrid;
    }

    // ── Private: plate lookup ─────────────────────────────────────────────────

    /// <summary>
    /// For a 3D point on the sphere, finds which plate it belongs to in the
    /// future frame by inverse-rotating back to the base grid.
    /// </summary>
    private int FindPlateForPoint(Vector3 point,
                                   Dictionary<int, Quaternion> inverseRotations,
                                   Dictionary<int, int> plateToMerdith,
                                   SimulationGrid baseGrid)
    {
        // Quick guess using unrotated position
        float fallbackLat = Mathf.Asin(Mathf.Clamp(point.y, -1f, 1f));
        float fallbackLon = Mathf.Atan2(point.z, point.x);
        int guessPlateId  = baseGrid.GetPlateIdAt(fallbackLat, fallbackLon);

        // Try the guessed plate first (fast path for interior cells)
        if (guessPlateId >= 0 && plateToMerdith.TryGetValue(guessPlateId, out int guessMerdithId)
            && inverseRotations.TryGetValue(guessMerdithId, out Quaternion invRotGuess))
        {
            Vector3 unrotated = invRotGuess * point;
            float latRad = Mathf.Asin(Mathf.Clamp(unrotated.y, -1f, 1f));
            float lonRad = Mathf.Atan2(unrotated.z, unrotated.x);
            if (baseGrid.GetPlateIdAt(latRad, lonRad) == guessPlateId)
                return guessPlateId;
        }

        // Fallback: test all plates
        foreach (var kvp in plateToMerdith)
        {
            int plateId    = kvp.Key;
            int merdithId  = kvp.Value;
            if (plateId == guessPlateId) continue;
            if (!inverseRotations.TryGetValue(merdithId, out Quaternion invRot)) continue;

            Vector3 unrotated  = invRot * point;
            float latRad       = Mathf.Asin(Mathf.Clamp(unrotated.y, -1f, 1f));
            float lonRad       = Mathf.Atan2(unrotated.z, unrotated.x);
            int basePlateId    = baseGrid.GetPlateIdAt(latRad, lonRad);
            if (basePlateId == plateId)
                return plateId;
        }

        return guessPlateId >= 0 ? guessPlateId : baseGrid.GetPlateIdAt(fallbackLat, fallbackLon);
    }

    // ── Private: rotation building ────────────────────────────────────────────

    private Dictionary<int, Quaternion> BuildRotations(Dictionary<int, FuturePoleRecord> records)
    {
        var result = new Dictionary<int, Quaternion>();
        foreach (var kvp in records)
        {
            var rec = kvp.Value;
            Vector3 axis = PlateRotationLoader.PoleToVector(rec.poleLat, rec.poleLon);
            result[kvp.Key] = Quaternion.AngleAxis(rec.angleDeg, axis);
        }
        return result;
    }

    /// <summary>
    /// Maps our internal plateId → Merdith plate ID using the same lookup
    /// table already in PlateRotationLoader.
    /// </summary>
    private Dictionary<int, int> BuildPlateToMerdithMap(Dictionary<int, TectonicPlate> plateRegistry)
    {
        var loader = new PlateRotationLoader(); // used only for ResolveMerdithId
        // We need an instance but don't call Load() — ResolveMerdithId is pure dictionary lookup
        var map = new Dictionary<int, int>();
        foreach (var kvp in plateRegistry)
        {
            int merdithId = loader.ResolveMerdithId(kvp.Value.Name);
            if (merdithId >= 0)
                map[kvp.Key] = merdithId;
        }
        return map;
    }

    // ── Private: CSV loading ──────────────────────────────────────────────────

    private Dictionary<int, FuturePoleRecord> GetRecords(int futureMa)
    {
        if (_cache.TryGetValue(futureMa, out var cached)) return cached;

        string path = Path.Combine(Application.streamingAssetsPath, $"prediction_{futureMa}Ma.csv");
        if (!File.Exists(path))
        {
            Debug.LogError($"[FutureMovement] CSV not found: {path}");
            return null;
        }

        var records = new Dictionary<int, FuturePoleRecord>();
        string[] lines = File.ReadAllLines(path);

        for (int i = 1; i < lines.Length; i++) // skip header
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] parts = line.Split(',');
            if (parts.Length < 5) continue;

            if (!int.TryParse(parts[0].Split('.')[0], out int plateId))   continue;
            if (!float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float poleLat)) continue;
            if (!float.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float poleLon)) continue;
            if (!float.TryParse(parts[4], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float angleDeg)) continue;

            records[plateId] = new FuturePoleRecord
            {
                plateId  = plateId,
                poleLat  = poleLat,
                poleLon  = poleLon,
                angleDeg = angleDeg
            };
        }

        Debug.Log($"[FutureMovement] Loaded {records.Count} plate records from prediction_{futureMa}Ma.csv");
        _cache[futureMa] = records;
        return records;
    }

    // ── Private: snap utility ────────────────────────────────────────────────

    private static int SnapToNearest(float futureMa)
    {
        int best = AvailableSteps[0];
        float bestDist = Mathf.Abs(futureMa - best);
        foreach (int step in AvailableSteps)
        {
            float d = Mathf.Abs(futureMa - step);
            if (d < bestDist) { bestDist = d; best = step; }
        }
        return best;
    }

    private static Vector3 LatLonToPoint(float latRad, float lonRad)
    {
        return new Vector3(
            Mathf.Cos(latRad) * Mathf.Cos(lonRad),
            Mathf.Sin(latRad),
            Mathf.Cos(latRad) * Mathf.Sin(lonRad));
    }
}

/// <summary>
/// One row from a prediction_NMa.csv file.
/// </summary>
public class FuturePoleRecord
{
    public int   plateId;
    public float poleLat;
    public float poleLon;
    public float angleDeg;
}
