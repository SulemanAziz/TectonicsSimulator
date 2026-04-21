// using System.Collections.Generic;
// using UnityEngine;

// public static class Triangulator
// {
//     public static int[] Triangulate(List<Vector2> points)
//     {
//         List<int> indices = new List<int>();

//         List<int> V = new List<int>();
//         for (int i = 0; i < points.Count; i++)
//             V.Add(i);

//         int guard = 0;

//         while (V.Count > 2 && guard < 5000)
//         {
//             guard++;

//             for (int i = 0; i < V.Count; i++)
//             {
//                 int prev = V[(i - 1 + V.Count) % V.Count];
//                 int curr = V[i];
//                 int next = V[(i + 1) % V.Count];

//                 if (IsEar(prev, curr, next, points, V))
//                 {
//                     indices.Add(prev);
//                     indices.Add(curr);
//                     indices.Add(next);

//                     V.RemoveAt(i);
//                     break;
//                 }
//             }
//         }

//         return indices.ToArray();
//     }

//     static bool IsEar(int i0, int i1, int i2, List<Vector2> pts,
// List<int> indices)
//     {
//         Vector2 a = pts[i0];
//         Vector2 b = pts[i1];
//         Vector2 c = pts[i2];

//         // Check if triangle is clockwise (Unity prefers clockwise)
//         if (Vector3.Cross(b - a, c - b).z >= 0)
//             return false;

//         // Check if any point is inside triangle
//         foreach (int i in indices)
//         {
//             if (i == i0 || i == i1 || i == i2) continue;

//             if (PointInTriangle(pts[i], a, b, c))
//                 return false;
//         }

//         return true;
//     }

//     static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
//     {
//         float area = 0.5f * (-b.y * c.x + a.y * (-b.x + c.x) + a.x *
// (b.y - c.y) + b.x * c.y);
//         float s = 1 / (2 * area) * (a.y * c.x - a.x * c.y + (c.y -
// a.y) * p.x + (a.x - c.x) * p.y);
//         float t = 1 / (2 * area) * (a.x * b.y - a.y * b.x + (a.y -
// b.y) * p.x + (b.x - a.x) * p.y);

//         return s >= 0 && t >= 0 && (s + t) <= 1;
//     }
// }