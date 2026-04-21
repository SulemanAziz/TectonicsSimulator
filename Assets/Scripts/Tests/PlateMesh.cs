void ConstructPlateMesh(UnityEngine.Vector3[] vertices)
    {
        foreach(var plate in PlateMap)
        {
            GameObject PlateBoundary = new GameObject("Plate_"+plate.Key);
            MeshFilter pm = PlateBoundary.AddComponent<MeshFilter>();
            MeshRenderer pr = PlateBoundary.AddComponent<MeshRenderer>();
            pr.material = new Material(Shader.Find("Custom/VertexColorLit"));
            Mesh PlateMesh = new Mesh();

            UnityEngine.Vector3[] BoundingVertices = null;

            foreach(float[] pt in plate.Value)
            {
                int lonKey = Mathf.RoundToInt(pt[0] * Mathf.Rad2Deg);
                int latKey = Mathf.RoundToInt(pt[1] * Mathf.Rad2Deg);
                long key = ((long)lonKey << 32) | (uint)latKey;

                if (vertexSpatialHash.ContainsKey(key))
                {
                    int index = vertexSpatialHash[key];
                    BoundingVertices.Append(vertices[index]);
                }
            }

            PlateMesh.vertices = BoundingVertices;
        //    PlateMesh.triangles = ?
            pm.mesh.Clear();
            pm.mesh = PlateMesh;

        }
    }
