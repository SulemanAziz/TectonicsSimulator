using UnityEngine;

public class GlobeRotation : MonoBehaviour
{
    public Planet planetScript;

    [Header("Controls")]
    public bool EnableRotation = false;

    [Header("Earth Settings")]
    [Tooltip("Earth's axial tilt is approx 23.5 degrees")]
    public float tiltAngle = 23.5f;
    
    [Header("Simulation Speed")]
    [Range(1, 100)]
    public int Speed = 20;

    // We calculate this hidden vector based on the tilt angle
    private Vector3 realWorldAxis;

    void Start()
    {
        CalculateAxis();
    }

    void OnValidate()
    {
        // Recalculate axis if you change numbers in the inspector
        CalculateAxis();
    }

    void CalculateAxis()
    {
        // 1. Start with a standard "Up" vector (0, 1, 0)
        // 2. Tilt it by 23.5 degrees to the left/right (Z-axis rotation)
        // Quaternion.Euler(x, y, z) creates a rotation
        realWorldAxis = Quaternion.Euler(0, 0, -tiltAngle) * Vector3.up;
    }

    void Update()
    {
        if (EnableRotation == true)
        {
            // We use -Speed to rotate Counter-Clockwise (West to East)
            // Space.World ensures the axis stays fixed in space (like the North Star)
            RotateGlobe(planetScript, -Speed, realWorldAxis);
        }
    }

    void RotateGlobe(Planet P, int speed, Vector3 rotAxis)
    {
        if (P != null)
        {
            // We explicitly use Space.World to rotate around the calculated tilt
            P.transform.Rotate(rotAxis * speed * Time.deltaTime, Space.World);
        }
    }
}