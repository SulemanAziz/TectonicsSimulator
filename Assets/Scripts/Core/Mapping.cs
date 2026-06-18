using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;

public static class Mapping
{
    public static Dictionary<string, List<float[]>> Map(string path)
    {
        TextAsset textAsset = Resources.Load<TextAsset>(path);
        if (textAsset == null)
        {
            Debug.LogError("TectonicPlates.json not found in Resources");
            return new Dictionary<string, List<float[]>>();
        }
        return JsonConvert.DeserializeObject<Dictionary<string, List<float[]>>>(textAsset.text);
    }
}