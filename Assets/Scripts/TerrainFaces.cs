using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public class TerrainFaces
{
    Mesh mesh;
    int resolution;
    Vector3 localUp;
    Vector3 axisA;
    Vector3 axisB;
    Texture2D OceanheightMap;
    Texture2D heightMap;

    float OceanheightMultiplier;
    float heightMultiplier;

    float WATERLEVEL;
    float MountainLevel;


    public static Vector3 PointOnUnitCubeToPointOnUnitSphere(Vector3 p)
    {
        float x2 = p.x * p.x;
        float y2 = p.y * p.y;
        float z2 = p.z * p.z;

        float x = (float)(p.x * Math.Sqrt(1 - (y2+z2)/2 + (y2*z2)/3 ) ) ;
        float y = (float)(p.y * Math.Sqrt(1 - (z2+x2)/2 + (z2*x2)/3 ) ) ;
        float z = (float)(p.z * Math.Sqrt(1 - (x2+y2)/2 + (x2*y2)/3 ) ) ;

        return new Vector3(x,y,z);
    }
    
    public TerrainFaces(Mesh m, int res, Vector3 up, Texture2D Oceanheightmap, Texture2D heightMap = null, float OceanheightMultiplier = 0f, float heightMultiplier = 0f, float WATERLEVEL = 1f, float MountainLevel = 2.25f)
    {
        mesh = m;
        resolution = res;
        localUp = up;

        axisA = new Vector3(localUp.y, localUp.z, localUp.x);
        axisB = Vector3.Cross(localUp, axisA);

        this.OceanheightMap = Oceanheightmap;
        this.heightMap = heightMap;

        this.OceanheightMultiplier = OceanheightMultiplier;
        this.heightMultiplier = heightMultiplier;
        this.WATERLEVEL = WATERLEVEL;
        this.MountainLevel = MountainLevel;
    }


    public void ConstructMesh()
    {
        Vector3[] vertices = new Vector3[resolution * resolution];
        Color[] colors = new Color[resolution * resolution];
        int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];
        int triIndex = 0;
 
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = x + y * resolution;
                UnityEngine.Vector2 percent = new UnityEngine.Vector2(x, y) / (resolution - 1);
                UnityEngine.Vector3 pointOnUnitCube = localUp + (percent.x - 0.5f) * 2 * axisA + (percent.y - 0.5f) * 2 * axisB;

                UnityEngine.Vector3 pointOnUnitSphere = PointOnUnitCubeToPointOnUnitSphere(pointOnUnitCube);
 
                // Sample Bathymetry heightmap and Topography heightmap then displace radially
                
                if (OceanheightMap != null || heightMap != null)
                {
                    var coord = GeoMaths.PointToCoordinate(pointOnUnitSphere);

                    float u = (coord.longitude / (Mathf.PI * 2f)) + 0.5f;
                    float v = (coord.latitude  / Mathf.PI) + 0.5f;

                    float sampleO = OceanheightMap.GetPixelBilinear(u, v).r; // assume grayscale
                    float radiusO = 1f + sampleO * OceanheightMultiplier;

                    float sample = heightMap.GetPixelBilinear(u,v).r;
                    float radius = 1f + sample * heightMultiplier;

                    vertices[i] = pointOnUnitSphere * (radiusO + radius);

                    
                    if(radiusO + radius > WATERLEVEL)
                    {
                        if (radiusO + radius > MountainLevel)
                        {
                            colors[i] = Color.whiteSmoke;
                        }
                        else
                        colors[i] = Color.lightGreen;
                    }
                    else
                    {
                        colors[i] = Color.softBlue;
                    }
                }
                else
                {
                    vertices[i] = pointOnUnitSphere;
                    colors[i] = Color.softBlue;
                }
 
                if (x != resolution - 1 && y != resolution - 1)
                {
                    //Clockwise triangle coordinates on vertices
                    triangles[triIndex] = i;
                    triangles[triIndex + 1] = i + resolution + 1;
                    triangles[triIndex + 2] = i + resolution;

                    triangles[triIndex + 3] = i;
                    triangles[triIndex + 4] = i + 1;
                    triangles[triIndex + 5] = i + resolution + 1;

                    triIndex += 6;
                }
            }
        }

        mesh.Clear();

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;

        mesh.RecalculateNormals();
    }
}
