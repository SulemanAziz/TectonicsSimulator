// using UnityEngine;
// using System.Collections.Generic;



// public class TriangulationTest : MonoBehaviour
// {

//     public static Vector3 CoordinateToPoint(float lat, float longi, float radius = 1)
// 	{
// 		// Convert Degrees to Radians
// 		float latRad = lat * Mathf.Deg2Rad;
// 		float lonRad = longi * Mathf.Deg2Rad;

// 		float y = Mathf.Sin(latRad);
// 		float r = Mathf.Cos(latRad); 
		
// 		// Note: Standard Unity forward is Z+, Right is X+
// 		// Adjusting to match common mapping projections
// 		float x = Mathf.Sin(lonRad) * r;
// 		float z = -Mathf.Cos(lonRad) * r;

// 		return new Vector3(x, y, z) * radius;
// 	}
//     void Init()
//     {
//         List<Vector2> polygon = new List<Vector2>()
// {
//     CoordinateToPoint(-76.684f, 8.7313f),
//     CoordinateToPoint(-76.7653f, 9.06455f),
//     CoordinateToPoint(-76.9243f, 9.37587f),
//     CoordinateToPoint(-77.0829f, 9.63133f),
//     CoordinateToPoint(-77.2914f, 9.89982f),
//     CoordinateToPoint(-77.4993f, 10.0985f),
//     CoordinateToPoint(-77.6786f, 10.2696f),
//     CoordinateToPoint(-77.9217f, 10.4324f),
//     CoordinateToPoint(-78.1287f, 10.547f),
//     CoordinateToPoint(-78.2289f, 10.6146f),
//     CoordinateToPoint(-78.4272f, 10.6388f),
//     CoordinateToPoint(-78.7455f, 10.6534f),
//     CoordinateToPoint(-78.9698f, 10.5795f),
//     CoordinateToPoint(-79.1511f, 10.4787f),
//     CoordinateToPoint(-79.2344f, 10.4145f),
//     CoordinateToPoint(-79.4321f, 10.4311f),
//     CoordinateToPoint(-79.7482f, 10.3894f),
//     CoordinateToPoint(-80.1053f, 10.305f),
//     CoordinateToPoint(-80.3646f, 10.2712f),
//     CoordinateToPoint(-80.6357f, 10.1539f),
//     CoordinateToPoint(-80.879f, 10.0579f),
//     CoordinateToPoint(-81.0792f, 9.93537f),
//     CoordinateToPoint(-81.3063f, 9.77762f),
//     CoordinateToPoint(-81.4692f, 9.58708f),
//     CoordinateToPoint(-81.6542f, 9.44432f),
//     CoordinateToPoint(-82.0802f, 9.43984f),
//     CoordinateToPoint(-82.3126f, 9.50913f),
//     CoordinateToPoint(-82.5246f, 9.5994f),
//     CoordinateToPoint(-82.8019f, 9.76995f),
//     CoordinateToPoint(-83.0213f, 9.87311f),
//     CoordinateToPoint(-83.2085f, 10.0526f),
//     CoordinateToPoint(-83.4648f, 10.2088f),
//     CoordinateToPoint(-83.7824f, 10.3213f),
//     CoordinateToPoint(-84.0727f, 10.4479f),
//     CoordinateToPoint(-84.4241f, 10.5374f),
//     CoordinateToPoint(-84.7462f, 10.5933f),
//     CoordinateToPoint(-85.1238f, 10.6534f),
//     CoordinateToPoint(-85.4349f, 10.6269f),
//     CoordinateToPoint(-85.7164f, 10.5674f),
//     CoordinateToPoint(-85.969f, 10.4886f),
//     CoordinateToPoint(-86.2479f, 10.3882f),
//     CoordinateToPoint(-86.648f, 10.235f),
//     CoordinateToPoint(-86.4487f, 9.9383f),
//     CoordinateToPoint(-86.2036f, 9.68364f),
//     CoordinateToPoint(-85.9105f, 9.42337f),
//     CoordinateToPoint(-85.6124f, 9.20327f),
//     CoordinateToPoint(-85.3805f, 9.08908f),
//     CoordinateToPoint(-85.0438f, 8.93723f),
//     CoordinateToPoint(-84.6858f, 8.77169f),
//     CoordinateToPoint(-84.4187f, 8.63674f),
//     CoordinateToPoint(-84.1577f, 8.47377f),
//     CoordinateToPoint(-84.0072f, 8.30028f),
//     CoordinateToPoint(-83.8377f, 8.18189f),
//     CoordinateToPoint(-83.6054f, 8.04445f),
//     CoordinateToPoint(-83.3599f, 7.92759f),
//     CoordinateToPoint(-83.2331f, 7.84854f),
//     CoordinateToPoint(-83.0742f, 7.59169f),
//     CoordinateToPoint(-82.8748f, 7.36639f),
//     CoordinateToPoint(-82.811f, 7.3026f),
//     CoordinateToPoint(-82.7482f, 7.28342f),
//     CoordinateToPoint(-82.6857f, 7.27797f),
//     CoordinateToPoint(-82.6171f, 7.30701f),
//     CoordinateToPoint(-82.492f, 7.29605f),
//     CoordinateToPoint(-82.3385f, 7.25127f),
//     CoordinateToPoint(-82.2734f, 7.12881f),
//     CoordinateToPoint(-82.2021f, 7.04084f),
//     CoordinateToPoint(-82.1252f, 7.00801f),
//     CoordinateToPoint(-82.0896f, 6.96742f),
//     CoordinateToPoint(-81.9436f, 6.94971f),
//     CoordinateToPoint(-81.8886f, 6.97836f),
//     CoordinateToPoint(-81.741f, 7.06054f),
//     CoordinateToPoint(-81.5843f, 7.03949f),
//     CoordinateToPoint(-81.4874f, 7.05516f),
//     CoordinateToPoint(-81.3423f, 7.09933f),
//     CoordinateToPoint(-81.1628f, 7.17174f),
//     CoordinateToPoint(-81.0385f, 7.22241f),
//     CoordinateToPoint(-80.8937f, 7.29417f),
//     CoordinateToPoint(-80.7835f, 7.35838f),
//     CoordinateToPoint(-80.687f, 7.41545f),
//     CoordinateToPoint(-80.5391f, 7.33519f),
//     CoordinateToPoint(-80.3628f, 7.22073f),
//     CoordinateToPoint(-80.2498f, 7.13273f),
//     CoordinateToPoint(-80.0958f, 7.10068f),
//     CoordinateToPoint(-79.8456f, 7.1601f),
//     CoordinateToPoint(-79.6576f, 7.18385f),
//     CoordinateToPoint(-79.4697f, 7.22831f),
//     CoordinateToPoint(-79.2402f, 7.30109f),
//     CoordinateToPoint(-78.9818f, 7.31874f),
//     CoordinateToPoint(-78.7511f, 7.32893f),
//     CoordinateToPoint(-78.6463f, 7.33731f),
//     CoordinateToPoint(-78.2285f, 7.50952f),
//     CoordinateToPoint(-78.0129f, 7.63734f),
//     CoordinateToPoint(-77.7067f, 7.82883f),
//     CoordinateToPoint(-77.5335f, 8.01173f),
//     CoordinateToPoint(-77.3309f, 8.10457f),
//     CoordinateToPoint(-77.1492f, 8.19017f),
//     CoordinateToPoint(-76.906f, 8.44344f),
//     CoordinateToPoint(-76.684f, 8.7313f)
// };

//         int[] triangles = Triangulator.Triangulate(polygon);

//         Vector3[] vertices = new Vector3[polygon.Count];
//         for (int i = 0; i < polygon.Count; i++)
//         {
//             vertices[i] = new Vector3(polygon[i].x, polygon[i].y, 0);
//         }

//         Mesh mesh = new Mesh();
//         mesh.vertices = vertices;
//         System.Array.Reverse(triangles);
//         mesh.triangles = triangles;
//         mesh.RecalculateNormals();

//         MeshFilter mf = gameObject.AddComponent<MeshFilter>();
//         MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();

//         mf.mesh = mesh;
//         mr.material = new Material(Shader.Find("Standard"));
//     }
// }