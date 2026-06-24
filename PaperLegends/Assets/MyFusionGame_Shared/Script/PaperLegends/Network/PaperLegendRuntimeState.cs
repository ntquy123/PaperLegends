using System;
using System.Collections.Generic;

public static class PaperLegendRuntimeState
{
    public const int DefaultFreeForAllPlayers = 4;
    public const int DefaultMinRealPlayers = 1;

    private static bool _isPaperLegendMatch;
    private static readonly object CharacterSelectionLock = new object();
    private static readonly Dictionary<int, int> SelectedCharacterModelsByPlayerId = new Dictionary<int, int>();
    private static readonly Queue<int> ReservedBotCharacterModels = new Queue<int>();

    public static bool IsPaperLegendMatch => _isPaperLegendMatch || IsEnabledByEnvironment();

    public static void SetPaperLegendMatch(bool enabled)
    {
        _isPaperLegendMatch = enabled;
    }

    public static int ResolveMaxPlayers(int configuredMaxPlayers)
    {
        int resolved = configuredMaxPlayers > 0 ? configuredMaxPlayers : DefaultFreeForAllPlayers;
        return Math.Max(1, Math.Min(DefaultFreeForAllPlayers, resolved));
    }

    public static int ResolveMinRealPlayers(int configuredRealPlayers, int maxPlayers)
    {
        int resolved = configuredRealPlayers > 0 ? configuredRealPlayers : DefaultMinRealPlayers;
        return Math.Max(1, Math.Min(Math.Max(1, maxPlayers), resolved));
    }

    public static void ClearCharacterSelections()
    {
        lock (CharacterSelectionLock)
        {
            SelectedCharacterModelsByPlayerId.Clear();
            ReservedBotCharacterModels.Clear();
        }
    }

    public static void SetSelectedCharacterModel(int playerId, int characterModelId)
    {
        if (playerId <= 0 || characterModelId <= 0)
            return;

        lock (CharacterSelectionLock)
        {
            SelectedCharacterModelsByPlayerId[playerId] = characterModelId;
        }
    }

    public static bool TryGetSelectedCharacterModel(int playerId, out int characterModelId)
    {
        characterModelId = 0;
        if (playerId <= 0)
            return false;

        lock (CharacterSelectionLock)
        {
            return SelectedCharacterModelsByPlayerId.TryGetValue(playerId, out characterModelId) && characterModelId > 0;
        }
    }

    public static void SetReservedBotCharacterModels(IEnumerable<int> characterModelIds)
    {
        lock (CharacterSelectionLock)
        {
            ReservedBotCharacterModels.Clear();

            if (characterModelIds == null)
                return;

            foreach (int modelId in characterModelIds)
            {
                if (modelId > 0)
                    ReservedBotCharacterModels.Enqueue(modelId);
            }
        }
    }

    public static bool TryDequeueReservedBotCharacterModel(out int characterModelId)
    {
        lock (CharacterSelectionLock)
        {
            if (ReservedBotCharacterModels.Count > 0)
            {
                characterModelId = ReservedBotCharacterModels.Dequeue();
                return characterModelId > 0;
            }
        }

        characterModelId = 0;
        return false;
    }

    private static bool IsEnabledByEnvironment()
    {
#if UNITY_SERVER
        string ruleset = Environment.GetEnvironmentVariable("GAME_RULESET");
        if (string.IsNullOrWhiteSpace(ruleset))
            ruleset = Environment.GetEnvironmentVariable("GAME_ID");

        if (string.IsNullOrWhiteSpace(ruleset))
            return false;

        ruleset = ruleset.Trim();
        return string.Equals(ruleset, "paper_legends", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ruleset, "paperlegends", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ruleset, "dau_tuong_giay", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ruleset, "bun_giay", StringComparison.OrdinalIgnoreCase);
#else
        return false;
#endif
    }
}
