using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Updates SimulationCell.Elevation based on plate boundary types.
///
/// Rules (simplified isostasy):
///
///   Convergent — continental vs continental  → orogeny (mountains), elevation rises
///   Convergent — oceanic vs continental      → subduction, trench (elevation drops on oceanic side)
///   Convergent — oceanic vs oceanic          → island arc, mild rise on one side
///   Divergent                                → rift / mid-ocean ridge, elevation drops then levels
///   Transform                                → neutral, minor variation
///   Interior                                 → baseline elevation from crust thickness
/// </summary>
public class ElevationSystem
{
    // Elevation deltas applied per time step (in arbitrary simulation units)
    private const float OrogenyRise       =  0.08f;   // mountain building
    private const float SubductionDrop    = -0.06f;   // trench on oceanic side
    private const float IslandArcRise     =  0.03f;   // volcanic arc
    private const float RiftDrop          = -0.04f;   // rift valley / spreading ridge
    private const float BaselineContinental = 0.3f;   // starting elevation for continental crust
    private const float BaselineOceanic     = 0.05f;  // starting elevation for oceanic crust

    // Oceanic crust has higher BaseDensity in TectonicPlate
    private const float OceanicDensityThreshold = 1.5f;

    public void InitialiseElevations(SimulationGrid grid,
                                      Dictionary<int, TectonicPlate> plateRegistry)
    {
        Parallel.For(0, grid.Width, x =>
        {
            for (int y = 0; y < grid.Height; y++)
            {
                SimulationCell cell = grid.GetCell(x, y);
                if (cell.PlateId < 0) continue;

                bool isOceanic = IsOceanic(cell.PlateId, plateRegistry);
                cell.CrustThickness = isOceanic ? 7f  : 35f;   // km (Earth averages)
                cell.Elevation      = isOceanic ? BaselineOceanic : BaselineContinental;
                grid.SetCell(x, y, cell);
            }
        });
    }

    /// <summary>
    /// Applies one step of elevation change based on current boundary types.
    /// Called every time the grid is rebuilt at a new time.
    /// deltaTime is the Ma difference from the last step.
    /// </summary>
    public void ApplyElevationStep(SimulationGrid grid,
                                    Dictionary<int, TectonicPlate> plateRegistry,
                                    float deltaTimeMa)
    {
        // Scale effect by time elapsed (larger jumps = bigger changes)
        float scale = Mathf.Clamp(deltaTimeMa / 10f, 0.01f, 2f);

        Parallel.For(0, grid.Width, x =>
        {
            for (int y = 0; y < grid.Height; y++)
            {
                SimulationCell cell = grid.GetCell(x, y);
                if (cell.PlateId < 0) continue;

                float delta = 0f;

                switch (cell.Boundary)
                {
                    case BoundaryType.Convergent:
                        delta = ConvergentDelta(cell, plateRegistry);
                        break;

                    case BoundaryType.Divergent:
                        delta = RiftDrop;
                        break;

                    case BoundaryType.Transform:
                        delta = 0f;   // transforms don't change elevation significantly
                        break;

                    case BoundaryType.None:
                        // Interior: slowly relax toward baseline (isostatic rebound)
                        float baseline = IsOceanic(cell.PlateId, plateRegistry)
                            ? BaselineOceanic : BaselineContinental;
                        delta = (baseline - cell.Elevation) * 0.05f;
                        break;
                }

                cell.Elevation = Mathf.Clamp(cell.Elevation + delta * scale, -1f, 1f);
                grid.SetCell(x, y, cell);
            }
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private float ConvergentDelta(SimulationCell cell,
                                   Dictionary<int, TectonicPlate> plateRegistry)
    {
        bool thisOceanic      = IsOceanic(cell.PlateId,          plateRegistry);
        bool neighbourOceanic = IsOceanic(cell.NeighbourPlateId, plateRegistry);

        if (!thisOceanic && !neighbourOceanic)
            return OrogenyRise;        // continental-continental → Himalayas-style

        if (thisOceanic && !neighbourOceanic)
            return SubductionDrop;     // this plate subducts → trench

        if (!thisOceanic && neighbourOceanic)
            return OrogenyRise * 0.5f; // continental overrides → Andes-style

        // oceanic-oceanic
        return IslandArcRise;          // one side forms island arc
    }

    private bool IsOceanic(int plateId, Dictionary<int, TectonicPlate> plateRegistry)
    {
        if (plateId < 0) return false;
        return plateRegistry.TryGetValue(plateId, out TectonicPlate plate)
               && plate.BaseDensity >= OceanicDensityThreshold;
    }
}
