using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Scans the SimulationGrid for cells that sit on a plate boundary,
/// classifies each boundary as Convergent / Divergent / Transform,
/// and writes the result back into each SimulationCell.
///
/// Classification is based on the dot product of the two plates'
/// surface velocity vectors at the shared border cell.
///
///   dot < -threshold  →  Convergent  (plates pushing together)
///   dot >  threshold  →  Divergent   (plates pulling apart)
///   |dot| <= threshold →  Transform  (plates sliding past each other)
/// </summary>
public class PlateCollisionDetector
{
    // Minimum dot-product magnitude to count as convergent/divergent.
    // Values below this are classified as transform faults.
    private const float DotThreshold = 0.05f;

    private PlateRotationLoader _loader;

    public PlateCollisionDetector(PlateRotationLoader loader)
    {
        _loader = loader;
    }

    /// <summary>
    /// Runs collision detection across the entire grid at the given time.
    /// Writes BoundaryType and NeighbourPlateId into every boundary cell.
    /// </summary>
    public void DetectBoundaries(SimulationGrid grid,
                                  Dictionary<int, TectonicPlate> plateRegistry,
                                  float timeMa)
    {
        // Pre-compute a surface velocity vector for each plate at this time
        Dictionary<int, Vector3> plateVelocities = ComputePlateVelocities(plateRegistry, timeMa);

        int width  = grid.Width;
        int height = grid.Height;

        // Neighbour offsets: right, left, up, down
        int[] dx = { 1, -1, 0,  0 };
        int[] dy = { 0,  0, 1, -1 };

        Parallel.For(0, width, x =>
        {
            for (int y = 0; y < height; y++)
            {
                SimulationCell cell = grid.GetCell(x, y);
                if (cell.PlateId < 0) continue;

                bool isBoundary  = false;
                int  neighbourId = -1;
                int  neighbourX  = x;
                int  neighbourY  = y;

                // Check 4 neighbours
                for (int d = 0; d < 4; d++)
                {
                    int nx = (x + dx[d] + width)  % width;
                    int ny = Mathf.Clamp(y + dy[d], 0, height - 1);

                    SimulationCell neighbour = grid.GetCell(nx, ny);
                    if (neighbour.PlateId >= 0 && neighbour.PlateId != cell.PlateId)
                    {
                        isBoundary  = true;
                        neighbourId = neighbour.PlateId;
                        neighbourX  = nx;
                        neighbourY  = ny;
                        break;
                    }
                }

                if (!isBoundary)
                {
                    if (cell.Boundary != BoundaryType.None)
                    {
                        cell.Boundary         = BoundaryType.None;
                        cell.NeighbourPlateId = -1;
                        grid.SetCell(x, y, cell);
                    }
                    continue;
                }

                // Classify the boundary using relative velocity
                BoundaryType boundary = ClassifyBoundary(
                    x, y, neighbourX, neighbourY, grid, cell.PlateId, neighbourId, plateVelocities);

                cell.Boundary         = boundary;
                cell.NeighbourPlateId = neighbourId;
                grid.SetCell(x, y, cell);
            }
        });

        LogBoundarySummary(grid);
        LogVelocitySample(plateVelocities);
    }

    // ── Classification ────────────────────────────────────────────────

    /// <summary>
    /// Determines boundary type from the dot product of relative velocity
    /// with the vector pointing from cell A toward cell B.
    /// </summary>
    private BoundaryType ClassifyBoundary(int x,  int y,
                                           int nx, int ny,
                                           SimulationGrid grid,
                                           int plateIdA, int plateIdB,
                                           Dictionary<int, Vector3> velocities)
    {
        // ω vectors for each plate
        Vector3 omegaA = velocities.TryGetValue(plateIdA, out var va) ? va : Vector3.zero;
        Vector3 omegaB = velocities.TryGetValue(plateIdB, out var vb) ? vb : Vector3.zero;

        // Position of cell A on unit sphere
        float latA = (((float)y  / grid.Height) * 180f - 90f)  * Mathf.Deg2Rad;
        float lonA = (((float)x  / grid.Width)  * 360f - 180f) * Mathf.Deg2Rad;
        Vector3 posA = new Vector3(
            Mathf.Cos(latA) * Mathf.Cos(lonA),
            Mathf.Sin(latA),
            Mathf.Cos(latA) * Mathf.Sin(lonA));

        // Position of neighbour cell B on unit sphere
        float latB = (((float)ny / grid.Height) * 180f - 90f)  * Mathf.Deg2Rad;
        float lonB = (((float)nx / grid.Width)  * 360f - 180f) * Mathf.Deg2Rad;
        Vector3 posB = new Vector3(
            Mathf.Cos(latB) * Mathf.Cos(lonB),
            Mathf.Sin(latB),
            Mathf.Cos(latB) * Mathf.Sin(lonB));

        // Surface velocities at cell A: v = ω × p  (always tangential to sphere)
        Vector3 surfVelA = Vector3.Cross(omegaA, posA);
        Vector3 surfVelB = Vector3.Cross(omegaB, posA); // both evaluated at same point

        // Relative velocity of plate B with respect to plate A at this boundary
        Vector3 relVel = surfVelB - surfVelA;
        if (relVel.magnitude < 0.0001f) return BoundaryType.Transform;

        // Boundary normal: direction from A toward B projected onto tangent plane
        Vector3 toNeighbour = (posB - posA);
        // Remove radial component to keep it tangential
        Vector3 boundaryNormal = (toNeighbour - Vector3.Dot(toNeighbour, posA) * posA).normalized;

        if (boundaryNormal == Vector3.zero) return BoundaryType.Transform;

        // Project relative velocity onto boundary normal
        // Negative = plates moving toward each other = convergent
        // Positive = plates moving apart = divergent
        float approach = Vector3.Dot(relVel, boundaryNormal);

        if (approach < -DotThreshold) return BoundaryType.Convergent;
        if (approach >  DotThreshold) return BoundaryType.Divergent;
        return BoundaryType.Transform;
    }

    // ── Velocity Computation ──────────────────────────────────────────

    /// <summary>
    /// For each plate, computes a representative surface velocity vector
    /// using the angular velocity from the rotation data.
    ///
    /// v = ω × r   (cross product of angular velocity vector and position)
    /// We use the pole axis as ω direction and angleDeg/timeMa as magnitude.
    /// </summary>
    private Dictionary<int, Vector3> ComputePlateVelocities(
        Dictionary<int, TectonicPlate> plateRegistry, float timeMa)
    {
        var velocities = new Dictionary<int, Vector3>();

        // Avoid division by zero at 0 Ma
        float safeTime = Mathf.Max(timeMa, 0.01f);

        foreach (var kvp in plateRegistry)
        {
            int plateId       = kvp.Key;
            TectonicPlate plate = kvp.Value;
            RotationRecord record = _loader.GetRotationByName(plate.Name, safeTime);

            if (record == null || Mathf.Abs(record.angleDeg) < 0.001f)
            {
                velocities[plateId] = Vector3.zero;
                continue;
            }

            // Angular velocity vector: axis scaled by degrees-per-Ma
            Vector3 axis         = PlateRotationLoader.PoleToVector(record.poleLat, record.poleLon);
            float   angularSpeed = record.angleDeg / safeTime; // deg/Ma
            // Store axis and speed as a scaled vector (ω = axis * speed)
            // Velocity at any surface point p = Cross(omega, p)
            // We store omega itself; velocity is computed per-boundary in ClassifyBoundary
            velocities[plateId] = axis * angularSpeed;
        }

        return velocities;
    }

    // ── Diagnostics ───────────────────────────────────────────────────

    private void LogVelocitySample(Dictionary<int, Vector3> velocities)
    {
        float minMag = float.MaxValue, maxMag = 0f, sum = 0f;
        int count = 0;
        foreach (var v in velocities.Values)
        {
            float m = v.magnitude;
            if (m < minMag) minMag = m;
            if (m > maxMag) maxMag = m;
            sum += m;
            count++;
        }
        Debug.Log($"[CollisionDetector] Omega magnitudes — min: {minMag:F4} max: {maxMag:F4} avg: {sum/count:F4}");
    }

    private void LogBoundarySummary(SimulationGrid grid)
    {
        int convergent = 0, divergent = 0, transform = 0;

        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
            {
                switch (grid.GetCell(x, y).Boundary)
                {
                    case BoundaryType.Convergent: convergent++; break;
                    case BoundaryType.Divergent:  divergent++;  break;
                    case BoundaryType.Transform:  transform++;  break;
                }
            }

        Debug.Log($"[CollisionDetector] Boundaries — Convergent: {convergent} | Divergent: {divergent} | Transform: {transform}");
    }
}
