using UnityEngine;

/// <summary>
/// The type of boundary interaction between two adjacent plates.
/// Determined by comparing plate velocities at the shared border.
/// </summary>
public enum BoundaryType
{
    None        = 0,  // Interior cell — no plate boundary nearby
    Convergent  = 1,  // Plates pushing toward each other → mountains or subduction
    Divergent   = 2,  // Plates pulling apart → rift valleys or mid-ocean ridges
    Transform   = 3   // Plates sliding past each other → fault lines
}

/// <summary>
/// Represents a single discrete location on the surface of the planet.
/// The planet's surface is divided into millions of these cells in the SimulationGrid.
/// This struct holds all the physical simulation data for that specific spot.
/// </summary>
public struct SimulationCell
{
    // The ID of the tectonic plate that currently "owns" this piece of crust.
    // -1 means this cell has not been assigned to any plate (useful for catching initialization bugs!).
    public int PlateId;

    // The physical thickness of the crust at this location.
    // Thicker crust = higher elevation.
    public float CrustThickness;

    // The calculated elevation of this cell, derived from CrustThickness, Density, and Isostasy.
    // This value will eventually be read by the visual mesh (TerrainFaces) to displace vertices.
    public float Elevation;

    // The type of plate boundary at this cell (None if interior).
    public BoundaryType Boundary;

    // The ID of the neighbouring plate if this is a boundary cell (-1 if interior).
    public int NeighbourPlateId;

    /// <summary>
    /// Initializes a blank cell with default safe values.
    /// PlateId is explicitly set to -1 to flag it as unassigned.
    /// </summary>
    public static SimulationCell Default()
    {
        return new SimulationCell
        {
            PlateId          = -1,
            CrustThickness   = 0f,
            Elevation        = 0f,
            Boundary         = BoundaryType.None,
            NeighbourPlateId = -1
        };
    }
}
