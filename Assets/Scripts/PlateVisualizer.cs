using UnityEngine;
using System.Collections.Generic;

public static class PlateVisualizer
{
    public static void RenderPlateBoundaries(
        Dictionary<string, List<float[]>> plates, 
        Transform parent)
    {
        if (plates == null) return;

        foreach (var kvp in plates)
        {
            var plateName = kvp.Key;
            var polygon = kvp.Value;
            if (polygon.Count < 2) continue;

            GameObject lineObj = new GameObject("Boundary_" + plateName);
            lineObj.transform.parent = parent;
            lineObj.transform.localPosition = Vector3.zero;

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Standard"));
            lr.startColor = Color.red;
            lr.endColor = Color.red;
            lr.startWidth = 0.5f;
            lr.endWidth = 0.5f;
            lr.positionCount = polygon.Count + 1;

            for (int i = 0; i < polygon.Count; i++)
            {
                float lon = polygon[i][0];
                float lat = polygon[i][1];

                // Pass degrees directly - GeoMaths.CoordinateToPoint handles conversion
                Coordinate coord = new Coordinate(lat, lon);
                Vector3 pos = GeoMaths.CoordinateToPoint(coord, 1.05f); // Increased offset for visibility
                lr.SetPosition(i, pos);
                
                if (i == 0)
                {
                    Debug.Log($"Plate {plateName}: First point - Lon:{lon}, Lat:{lat}, WorldPos:{pos}");
                }
            }

            lr.SetPosition(polygon.Count, lr.GetPosition(0));
        }
    }
}