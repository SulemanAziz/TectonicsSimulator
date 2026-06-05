using UnityEngine;

/// <summary>
/// Represents a single tectonic plate in the simulation.
/// Instead of using raw strings from the JSON, we parse them into these formal objects.
/// This allows us to attach physical properties (density, velocity, Euler poles) later.
/// </summary>
public class TectonicPlate
{
    // A stable, unique integer ID for this plate.
    // This is crucial because looking up integers in a 2D array (our SimulationGrid)
    // is thousands of times faster than comparing strings.
    public int Id;

    // The original name of the plate from the JSON data (e.g., "Pacific", "North American").
    // Useful for debugging and UI.
    public string Name;

    // The color this plate will be rendered as on the globe mesh.
    public Color DisplayColor;

    // The base density of the plate's crust.
    // In future simulation steps, denser plates (oceanic) will subduct under lighter plates (continental).
    public float BaseDensity;

    // (Future) The axis of rotation for the plate's movement on the sphere.
    // public Vector3 EulerPole;

    // (Future) How fast the plate rotates around its Euler Pole.
    // public float AngularVelocity;

    /// <summary>
    /// Constructor to easily initialize a new plate.
    /// </summary>
    public TectonicPlate(int id, string name, Color displayColor, float baseDensity = 1.0f)
    {
        Id = id;
        Name = name;
        DisplayColor = displayColor;
        BaseDensity = baseDensity;
    }
}
