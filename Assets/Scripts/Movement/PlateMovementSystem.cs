using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Applies Euler pole rotations from PlateRotationLoader to the SimulationGrid.
/// For each plate, rotates every cell's geographic position by the quaternion
/// derived from the Merdith rotation data at the current time (Ma).
/// </summary>
public class PlateMovementSystem
{
    private PlateRotationLoader _loader;

    public PlateMovementSystem(PlateRotationLoader loader)
    {
        _loader = loader;
    }

    /// <summary>
    /// Rebuilds the SimulationGrid for a given time in Ma.
    /// Returns a new grid with cells reassigned based on rotated plate positions.
    /// </summary>
    public SimulationGrid BuildGridAtTime(SimulationGrid baseGrid, Dictionary<int, TectonicPlate> plateRegistry, float timeMa)
    {
        int width = baseGrid.Width;
        int height = baseGrid.Height;

        // Build rotation quaternion per plate at this time
        Dictionary<int, Quaternion> plateRotations = BuildPlateRotations(plateRegistry, timeMa);

        // Pre-compute inverse rotations once to avoid computing them in parallel loop
        Dictionary<int, Quaternion> inversePlateRotations = new Dictionary<int, Quaternion>();
        foreach (var kvp in plateRotations)
        {
            inversePlateRotations[kvp.Key] = Quaternion.Inverse(kvp.Value);
        }

        // New grid same resolution as base
        int resPerDegree = width / 360;
        SimulationGrid newGrid = new SimulationGrid(resPerDegree);

        // For each cell in the new grid, find which plate it belongs to
        // by inverse-rotating each cell position back to the reference frame (0 Ma)
        // and checking against the base grid.
        Parallel.For(0, width, x =>
        {
            float lonDeg = ((float)x / width) * 360f - 180f;
            float lonRad = lonDeg * Mathf.Deg2Rad;

            for (int y = 0; y < height; y++)
            {
                float latDeg = ((float)y / height) * 180f - 90f;
                float latRad = latDeg * Mathf.Deg2Rad;

                // Convert this grid cell to a 3D point on the unit sphere
                Vector3 point = LatLonToPoint(latRad, lonRad);

                // Find the best matching plate by testing which plate's inverse rotation
                // maps this point closest to a known plate region in the base grid.
                // We pass the pre-computed inverse rotations for optimization.
                int bestPlateId = FindPlateForPoint(point, inversePlateRotations, baseGrid);

                SimulationCell cell = SimulationCell.Default();
                cell.PlateId = bestPlateId;
                newGrid.SetCell(x, y, cell);
            }
        });

        return newGrid;
    }

    /// <summary>
    /// For a given 3D point on the sphere, applies the inverse rotation of each plate
    /// and checks if the back-rotated point falls within that plate in the base grid.
    /// Optimised to check the plate at the current coordinates first.
    /// </summary>
    private int FindPlateForPoint(Vector3 point, Dictionary<int, Quaternion> inversePlateRotations, SimulationGrid baseGrid)
    {
        // Guess the plate based on the unrotated point's location.
        // For plate interiors, this guess will be correct and will return immediately (O(1)).
        float fallbackLat = Mathf.Asin(Mathf.Clamp(point.y, -1f, 1f));
        float fallbackLon = Mathf.Atan2(point.z, point.x);
        int guessPlateId = baseGrid.GetPlateIdAt(fallbackLat, fallbackLon);

        if (guessPlateId >= 0 && inversePlateRotations.TryGetValue(guessPlateId, out Quaternion invRotGuess))
        {
            Vector3 unrotated = invRotGuess * point;
            float latRad = Mathf.Asin(Mathf.Clamp(unrotated.y, -1f, 1f));
            float lonRad = Mathf.Atan2(unrotated.z, unrotated.x);
            if (baseGrid.GetPlateIdAt(latRad, lonRad) == guessPlateId)
            {
                return guessPlateId;
            }
        }

        // Fallback: If guess was wrong (near plate boundaries or high displacement), test all other plates
        foreach (var kvp in inversePlateRotations)
        {
            int plateId = kvp.Key;
            if (plateId == guessPlateId) continue;

            Quaternion invRot = kvp.Value;

            // Inverse rotate: bring the current point back to 0 Ma reference frame
            Vector3 unrotated = invRot * point;

            // Convert back to lat/lon
            float latRad = Mathf.Asin(Mathf.Clamp(unrotated.y, -1f, 1f));
            float lonRad = Mathf.Atan2(unrotated.z, unrotated.x);

            // Check if this point was on this plate in the base grid
            int basePlateId = baseGrid.GetPlateIdAt(latRad, lonRad);
            if (basePlateId == plateId)
                return plateId;
        }

        return guessPlateId >= 0 ? guessPlateId : baseGrid.GetPlateIdAt(fallbackLat, fallbackLon);
    }

    /// <summary>
    /// Builds a rotation quaternion for every plate at the given time.
    /// Plates not found in rotation data get identity (no movement).
    /// </summary>
    private Dictionary<int, Quaternion> BuildPlateRotations(Dictionary<int, TectonicPlate> plateRegistry, float timeMa)
    {
        var rotations = new Dictionary<int, Quaternion>();

        foreach (var kvp in plateRegistry)
        {
            int plateId       = kvp.Key;
            TectonicPlate plate = kvp.Value;

            // Use name-based lookup to match our plates to Merdith rotation data
            RotationRecord record = _loader.GetRotationByName(plate.Name, timeMa);

            rotations[plateId] = record != null
                ? PlateRotationLoader.BuildRotationQuaternion(record)
                : Quaternion.identity;
        }

        return rotations;
    }

    /// <summary>
    /// Converts lat/lon in radians to a 3D point on the unit sphere.
    /// </summary>
    private Vector3 LatLonToPoint(float latRad, float lonRad)
    {
        return new Vector3(
            Mathf.Cos(latRad) * Mathf.Cos(lonRad),
            Mathf.Sin(latRad),
            Mathf.Cos(latRad) * Mathf.Sin(lonRad)
        );
    }
}
