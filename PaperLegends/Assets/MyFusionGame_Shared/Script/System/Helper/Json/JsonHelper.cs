using System.Collections.Generic;
using UnityEngine;

public static class JsonHelper
{
    [System.Serializable]
    private class Wrapper<T>
    {
        public List<T> Items;
    }

    public static string ToJson<T>(List<T> list, bool prettyPrint = false)
    {
        if (list == null)
        {
            return "[]";
        }
        Wrapper<T> wrapper = new Wrapper<T> { Items = list };
        string json = JsonUtility.ToJson(wrapper, prettyPrint);
        int start = json.IndexOf('[');
        int end = json.LastIndexOf(']');
        if (start >= 0 && end >= start)
        {
            return json.Substring(start, end - start + 1);
        }
        return json;
    }

    public static List<T> FromJson<T>(string json)
    {
        if (string.IsNullOrEmpty(json))
            return new List<T>();

        string wrapperJson = $"{{\"Items\":{json}}}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrapperJson);
        return wrapper != null ? wrapper.Items : new List<T>();
    }
}
