using UnityEngine;

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

    /// <summary>
    /// Initializes a blank cell with default safe values.
    /// PlateId is explicitly set to -1 to flag it as unassigned.
    /// </summary>
    public static SimulationCell Default()
    {
        return new SimulationCell
        {
            PlateId = -1, // -1 means unassigned/invalid
            CrustThickness = 0f,
            Elevation = 0f
        };
    }
}
