using System.Collections.Generic;
using UnityEngine;

public class PaperLegendWorldHealthBarManager : MonoBehaviour
{
    private static PaperLegendWorldHealthBarManager _instance;

    [Header("Config")]
    [SerializeField] private PaperLegendWorldHealthBar worldHealthBarPrefab;
    [SerializeField, Min(0.05f)] private float scanIntervalSeconds = 0.5f;
    [SerializeField] private bool autoAttachToPaperLegendCharacters = true;

    private readonly Dictionary<PaperLegendCharacterNetworkHandler, PaperLegendWorldHealthBar> _bars =
        new Dictionary<PaperLegendCharacterNetworkHandler, PaperLegendWorldHealthBar>();

    private float _scanCountdown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindObjectOfType<PaperLegendWorldHealthBarManager>(true) != null)
            return;

        GameObject go = new GameObject("PaperLegendWorldHealthBarManager");
        DontDestroyOnLoad(go);
        go.AddComponent<PaperLegendWorldHealthBarManager>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!autoAttachToPaperLegendCharacters)
            return;

        _scanCountdown -= Time.unscaledDeltaTime;
        if (_scanCountdown > 0f)
            return;

        _scanCountdown = scanIntervalSeconds;
        PruneMissingBars();
        EnsureBarsForCharacters();
    }

    private void EnsureBarsForCharacters()
    {
        PaperLegendCharacterNetworkHandler[] characters = FindObjectsOfType<PaperLegendCharacterNetworkHandler>(true);
        for (int i = 0; i < characters.Length; i++)
        {
            var character = characters[i];
            if (character == null || !character.gameObject.scene.IsValid())
                continue;

            if (_bars.ContainsKey(character))
                continue;

            PaperLegendWorldHealthBar bar = CreateBar(character);
            if (bar == null)
                continue;

            bar.Bind(character);
            _bars[character] = bar;
        }
    }

    private PaperLegendWorldHealthBar CreateBar(PaperLegendCharacterNetworkHandler character)
    {
        if (character == null)
            return null;

        if (worldHealthBarPrefab != null)
        {
            PaperLegendWorldHealthBar spawned = Instantiate(worldHealthBarPrefab);
            spawned.name = $"WorldHealthBar_Player_{character.PlayerId}";
            return spawned;
        }

        PaperLegendWorldHealthBar existing = character.GetComponent<PaperLegendWorldHealthBar>();
        if (existing != null)
            return existing;

        return character.gameObject.AddComponent<PaperLegendWorldHealthBar>();
    }

    private void PruneMissingBars()
    {
        if (_bars.Count == 0)
            return;

        var removed = ListPool<PaperLegendCharacterNetworkHandler>.Get();
        foreach (var pair in _bars)
        {
            if (pair.Key == null || pair.Value == null)
                removed.Add(pair.Key);
        }

        for (int i = 0; i < removed.Count; i++)
            _bars.Remove(removed[i]);

        ListPool<PaperLegendCharacterNetworkHandler>.Release(removed);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> Pool = new Stack<List<T>>();

        public static List<T> Get()
        {
            return Pool.Count > 0 ? Pool.Pop() : new List<T>();
        }

        public static void Release(List<T> list)
        {
            if (list == null)
                return;

            list.Clear();
            Pool.Push(list);
        }
    }
}
