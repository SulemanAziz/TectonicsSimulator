using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// A single rotation record from MerdithPlateRotations.json.
/// Describes where a plate's Euler pole was and how much it had rotated at a given time.
/// </summary>
[System.Serializable]
public class RotationRecord
{
    public int movingPlateId;
    public string movingPlateCode;
    public float timeMa;
    public float poleLat;
    public float poleLon;
    public float angleDeg;
    public int fixedPlateId;
    public string fixedPlateCode;
    public bool interpolated;
    public string comment;
    public int sourceLine;
}

/// <summary>
/// Loads MerdithPlateRotations.json and organizes records by plate ID,
/// sorted by time so we can quickly interpolate at any given Ma.
/// </summary>
public class PlateRotationLoader
{
    // Key: movingPlateId, Value: list of rotation records sorted ascending by timeMa
    public Dictionary<int, List<RotationRecord>> RotationsByPlate { get; private set; }

    // All unique plate IDs found in the rotation data
    public List<int> PlateIds { get; private set; }

    // Time range available in the data
    public float MinTimeMa { get; private set; }
    public float MaxTimeMa { get; private set; }

    // Wrapper so Newtonsoft can deserialize the top-level JSON object
    [System.Serializable]
    private class RotationFile { public List<RotationRecord> records; }

    // Maps our plate names (from TectonicPlates.json) → Merdith movingPlateId
    // Key: lowercase plate name substring, Value: Merdith plate ID
    private static readonly Dictionary<string, int> NameToMerdithId = new Dictionary<string, int>
    {
        // Major plates
        { "africa",          701 },
        { "antarctica",      802 },
        { "australia",       801 },
        { "eurasia",         301 },
        { "north america",   101 },
        { "south america",   201 },
        { "india",           501 },
        { "pacific",         901 },
        { "nazca",           911 },
        { "caribbean",       210 },
        { "cocos",           909 },
        { "philippine",      608 },
        { "juan de fuca",    909 },
        { "scotia",          290 },
        { "arabia",          503 },
        { "somalia",         702 },
        { "amur",            380 },
        { "okhotsk",         404 },
        { "siberia",         401 },
        // Minor plates
        { "sunda",           614 },
        { "tonga",           821 },
        { "niuafo",          864 },
        { "woodlark",        829 },
        { "molucca",         499 },
        { "sandwich",        276 },
        { "north bismarck",  830 },
        { "new hebrides",    827 },
        { "indochina",       603 },
        { "okinawa",         648 },
        { "caroline",        653 },
        { "mariana",         699 },
        { "solomon",         734 },
        { "yangtze",        6021 },
        { "burma",           446 },
        { "rivera",          970 },
        { "bird",            681 },
        { "banda",           664 },
        { "coral",           829 },
        { "juan fernandez",  923 },
        { "panama",          229 },
        { "easter",          229 },
    };

    // Plates confirmed not present in Merdith dataset — suppress warnings for these
    private static readonly System.Collections.Generic.HashSet<string> KnownUnmapped =
        new System.Collections.Generic.HashSet<string>
    {
        "kermadec", "altiplano", "futuna", "anatolian", "manus", "galapagos"
    };

    public void Load(string resourcePath = "PlateData/MerdithPlateRotations")
    {
        TextAsset json = Resources.Load<TextAsset>(resourcePath);
        if (json == null)
        {
            Debug.LogError($"[PlateRotationLoader] Could not find file at Resources/{resourcePath}");
            return;
        }

        // The JSON is a wrapper object with a "records" array
        RotationFile file = JsonConvert.DeserializeObject<RotationFile>(json.text);
        List<RotationRecord> allRecords = file?.records;

        if (allRecords == null || allRecords.Count == 0)
        {
            Debug.LogError("[PlateRotationLoader] JSON parsed but no records found.");
            return;
        }

        RotationsByPlate = new Dictionary<int, List<RotationRecord>>();
        MinTimeMa = float.MaxValue;
        MaxTimeMa = float.MinValue;

        foreach (RotationRecord record in allRecords)
        {
            if (!RotationsByPlate.ContainsKey(record.movingPlateId))
                RotationsByPlate[record.movingPlateId] = new List<RotationRecord>();

            RotationsByPlate[record.movingPlateId].Add(record);

            if (record.timeMa < MinTimeMa) MinTimeMa = record.timeMa;
            if (record.timeMa > MaxTimeMa) MaxTimeMa = record.timeMa;
        }

        // Sort each plate's records by time ascending (0 Ma first, oldest last)
        PlateIds = new List<int>();
        foreach (var kvp in RotationsByPlate)
        {
            kvp.Value.Sort((a, b) => a.timeMa.CompareTo(b.timeMa));
            PlateIds.Add(kvp.Key);
        }

        Debug.Log($"[PlateRotationLoader] Loaded {allRecords.Count} records across {RotationsByPlate.Count} plates. Time range: {MinTimeMa} Ma to {MaxTimeMa} Ma.");
    }

    /// <summary>
    /// Resolves a plate name (from TectonicPlates.json) to a Merdith plate ID.
    /// Returns -1 if no match found.
    /// </summary>
    public int ResolveMerdithId(string plateName)
    {
        string lower = plateName.ToLower();
        foreach (var kvp in NameToMerdithId)
            if (lower.Contains(kvp.Key))
                return kvp.Value;
        return -1;
    }

    /// <summary>
    /// Logs which plates matched and which didn't — call once after Init to verify mapping.
    /// </summary>
    public void LogMappingResults(System.Collections.Generic.Dictionary<int, TectonicPlate> plateRegistry)
    {
        int matched = 0, unmatched = 0;
        foreach (var kvp in plateRegistry)
        {
            string name = kvp.Value.Name;
            int merdithId = ResolveMerdithId(name);
            if (merdithId >= 0)
            {
                matched++;
            }
            else if (!KnownUnmapped.Contains(name.ToLower()))
            {
                Debug.LogWarning($"[RotationLoader] NO MATCH: '{name}'");
                unmatched++;
            }
        }
        Debug.Log($"[RotationLoader] Mapping complete — {matched} matched, {unmatched} unmatched (6 minor plates have no Merdith data).");
    }

    /// <summary>
    /// Returns interpolated rotation for a plate looked up by name.
    /// </summary>
    public RotationRecord GetRotationByName(string plateName, float timeMa)
    {
        int merdithId = ResolveMerdithId(plateName);
        if (merdithId < 0) return null;
        return GetRotationAtTime(merdithId, timeMa);
    }

    /// <summary>
    /// Returns interpolated rotation data for a given plate at the requested time.
    /// Returns null if the plate has no records.
    /// </summary>
    public RotationRecord GetRotationAtTime(int plateId, float timeMa)
    {
        if (!RotationsByPlate.TryGetValue(plateId, out List<RotationRecord> records) || records.Count == 0)
            return null;

        // Clamp to available range for this plate
        if (timeMa <= records[0].timeMa) return records[0];
        if (timeMa >= records[records.Count - 1].timeMa) return records[records.Count - 1];

        // Binary search for the two bracketing records
        int lo = 0, hi = records.Count - 1;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (records[mid].timeMa <= timeMa) lo = mid;
            else hi = mid;
        }

        RotationRecord r0 = records[lo];
        RotationRecord r1 = records[hi];
        float t = (timeMa - r0.timeMa) / (r1.timeMa - r0.timeMa);

        // Interpolate angle linearly
        float angle = Mathf.Lerp(r0.angleDeg, r1.angleDeg, t);

        // SLERP the Euler pole direction
        Vector3 pole0 = PoleToVector(r0.poleLat, r0.poleLon);
        Vector3 pole1 = PoleToVector(r1.poleLat, r1.poleLon);
        Vector3 poleInterp = Vector3.Slerp(pole0, pole1, t).normalized;

        // Convert back to lat/lon
        float poleLat = Mathf.Asin(poleInterp.y) * Mathf.Rad2Deg;
        float poleLon = Mathf.Atan2(poleInterp.z, poleInterp.x) * Mathf.Rad2Deg;

        return new RotationRecord
        {
            movingPlateId = plateId,
            timeMa = timeMa,
            poleLat = poleLat,
            poleLon = poleLon,
            angleDeg = angle,
            fixedPlateId = r0.fixedPlateId,
            interpolated = true
        };
    }

    /// <summary>
    /// Converts an Euler pole (lat/lon in degrees) to a 3D unit vector.
    /// </summary>
    public static Vector3 PoleToVector(float latDeg, float lonDeg)
    {
        float latRad = latDeg * Mathf.Deg2Rad;
        float lonRad = lonDeg * Mathf.Deg2Rad;
        return new Vector3(
            Mathf.Cos(latRad) * Mathf.Cos(lonRad),
            Mathf.Sin(latRad),
            Mathf.Cos(latRad) * Mathf.Sin(lonRad)
        );
    }

    /// <summary>
    /// Builds a Unity Quaternion that rotates a point on the sphere
    /// according to the given rotation record.
    /// </summary>
    public static Quaternion BuildRotationQuaternion(RotationRecord record)
    {
        Vector3 axis = PoleToVector(record.poleLat, record.poleLon);
        return Quaternion.AngleAxis(record.angleDeg, axis);
    }
}
