using System;
using UnityEngine;

public static class GameMapHelper
{
    private static readonly GameMapId[] OnlinePlayableMaps =
    {
        GameMapId.HometownHouse,
        GameMapId.VillageRoad
    };

    public static string ToSceneName(GameMapId mapId)
    {
        return ((int)mapId).ToString();
    }

    public static bool TryParseMapId(string sceneName, out GameMapId mapId)
    {
        mapId = default;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        if (int.TryParse(sceneName, out var numericId) && Enum.IsDefined(typeof(GameMapId), numericId))
        {
            mapId = (GameMapId)numericId;
            return true;
        }

        return Enum.TryParse(sceneName, true, out mapId);
    }

    public static GameMapId GetRandomMapId(GameMapId fallback = GameMapId.HometownHouse)
    {
        if (OnlinePlayableMaps.Length == 0)
        {
            return fallback;
        }

        int index = UnityEngine.Random.Range(0, OnlinePlayableMaps.Length);
        return OnlinePlayableMaps[index];
    }
}
