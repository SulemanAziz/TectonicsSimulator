using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json; 

public static class Mapping
{
    public static Dictionary<string, List<List<float>>> Map(string path)
    {
        TextAsset textAsset = Resources.Load<TextAsset>(path);
        if (textAsset == null)
        {
            Debug.LogError("TectonicPlates.json not found in Resources");
            return new Dictionary<string, List<List<float>>>();
        }
        return JsonConvert.DeserializeObject<Dictionary<string, List<List<float>>>>(textAsset.text);
    }
    
    public static void Render_plateBoundaries(Dictionary<string, List<List<float>>> plates, Transform parent)
    {
        // Keep Sphere rendering in mind...

        
    }
}