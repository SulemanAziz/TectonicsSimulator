using UnityEngine;

/// <summary>
/// The core mathematical model of the planet.
/// This class holds a high-resolution 2D array of SimulationCells.
/// It represents the entire globe wrapped into a flat grid (Equirectangular projection).
/// </summary>
public class SimulationGrid
{
    // The actual 2D array holding the physical data of our planet's crust.
    // The X axis represents Longitude (-180 to +180).
    // The Y axis represents Latitude (-90 to +90).
    private SimulationCell[,] grid;

    // How many grid cells represent 1 degree of the Earth.
    // A resolution of 10 means the grid is 3600 x 1800 cells (0.1 degree accuracy).
    private int resolutionPerDegree;

    // The total dimensions of our 2D array.
    public int Width { get; private set; }  // Usually 360 * resolution
    public int Height { get; private set; } // Usually 180 * resolution

    /// <summary>
    /// Creates a new empty grid covering the entire sphere.
    /// </summary>
    /// <param name="resPerDegree">Determines the density of the grid.</param>
    public SimulationGrid(int resPerDegree)
    {
        resolutionPerDegree = resPerDegree;
        
        // 360 degrees of longitude, 180 degrees of latitude
        Width = 360 * resolutionPerDegree;
        Height = 180 * resolutionPerDegree;

        grid = new SimulationCell[Width, Height];

        // Initialize every single cell to the default unassigned state (PlateId = -1).
        // This ensures no garbage data exists before the GridInitializer runs.
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                grid[x, y] = SimulationCell.Default();
            }
        }
    }

    /// <summary>
    /// Allows the GridInitializer to set the raw data of a specific cell during setup.
    /// </summary>
    public void SetCell(int x, int y, SimulationCell cell)
    {
        // Safety check to ensure we don't write outside the array bounds.
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            grid[x, y] = cell;
        }
    }

    /// <summary>
    /// Retrieves a copy of the cell at the given grid coordinates.
    /// </summary>
    public SimulationCell GetCell(int x, int y)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            return grid[x, y];
        }
        return SimulationCell.Default();
    }

    /// <summary>
    /// The most important method for the visual Mesh!
    /// Given a real-world latitude and longitude in radians (from GeoMath),
    /// this calculates which cell in our 2D array that corresponds to, and returns the Plate ID.
    /// </summary>
    /// <param name="latitudeRad">Latitude in radians (-PI/2 to PI/2)</param>
    /// <param name="longitudeRad">Longitude in radians (-PI to PI)</param>
    /// <returns>The integer ID of the tectonic plate at this location.</returns>
    public int GetPlateIdAt(float latitudeRad, float longitudeRad)
    {
        // Convert radians to degrees
        float latDeg = latitudeRad * Mathf.Rad2Deg;
        float lonDeg = longitudeRad * Mathf.Rad2Deg;

        // Map degrees to array indices.
        // Longitude: -180 to 180 maps to index 0 to Width
        int x = Mathf.FloorToInt((lonDeg + 180f) * resolutionPerDegree);
        // Latitude: -90 to 90 maps to index 0 to Height
        int y = Mathf.FloorToInt((latDeg + 90f) * resolutionPerDegree);

        // Handle longitude wrapping (if exactly 180 degrees, it wraps back to 0)
        // If x is exactly Width, it wraps to 0. Also handle negative wraps safely.
        x = (x % Width + Width) % Width; 

        // Clamp latitude to prevent out-of-bounds at the exact North/South poles
        y = Mathf.Clamp(y, 0, Height - 1);

        // Read the cell and return its plate ID
        return grid[x, y].PlateId;
    }
}
