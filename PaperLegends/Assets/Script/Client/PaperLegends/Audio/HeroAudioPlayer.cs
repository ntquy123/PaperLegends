using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class HeroAudioPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private HeroConfig heroConfig;
    [SerializeField] private HeroAudioConfig config;

    private readonly Dictionary<string, AudioClip> audioClipCache = new Dictionary<string, AudioClip>();
    private readonly List<AsyncOperationHandle<AudioClip>> audioClipHandles = new List<AsyncOperationHandle<AudioClip>>();

    public HeroConfig HeroConfig => heroConfig;
    public HeroAudioConfig Config => config;

    private void Awake()
    {
        ResolveAudioSource();
    }

    public void SetConfig(HeroAudioConfig nextConfig)
    {
        config = nextConfig;
    }

    public void SetHeroConfig(HeroConfig nextConfig)
    {
        heroConfig = nextConfig;
    }

    public void PlaySkill(int index)
    {
        SkillConfig skill = heroConfig != null ? heroConfig.GetSkillBySlot(index) : null;
        PlayOneShot(skill != null ? skill.castSound : null);
    }

    public void PlaySkill(int skillId, int fallbackSlot)
    {
        SkillConfig skill = heroConfig != null ? heroConfig.GetSkillById(skillId) : null;
        if (skill == null && heroConfig != null)
            skill = heroConfig.GetSkillBySlot(fallbackSlot);

        PlayOneShot(skill != null ? skill.castSound : null);
    }

    public void PlayNormalAttack()
    {
        HeroAudioConfig audioConfig = ResolveAudioConfig();
        PlayOneShot(audioConfig != null ? audioConfig.normalAttack : null);
    }

    public void PlayFlick()
    {
        HeroAudioConfig audioConfig = ResolveAudioConfig();
        PlayOneShot(audioConfig != null ? audioConfig.flick : null);
    }

    public void PlayMove()
    {
        HeroAudioConfig audioConfig = ResolveAudioConfig();
        PlayOneShot(audioConfig != null ? audioConfig.move : null);
    }

    public void PlayHit()
    {
        HeroAudioConfig audioConfig = ResolveAudioConfig();
        PlayOneShot(audioConfig != null ? audioConfig.hit : null);
    }

    public void PlayDie()
    {
        HeroAudioConfig audioConfig = ResolveAudioConfig();
        PlayOneShot(audioConfig != null ? audioConfig.die : null);
    }

    public static void PlaySkillForCharacter(PaperLegendCharacterNetworkHandler character, int skillId, int slot)
    {
        HeroAudioPlayer player = FindForCharacter(character);
        if (player != null)
            player.EnsureHeroConfig(character);

        if (player != null)
            player.PlaySkill(skillId, slot);
    }

    public static HeroAudioPlayer FindForCharacter(PaperLegendCharacterNetworkHandler character)
    {
        if (character == null)
            return null;

        return character.GetComponentInChildren<HeroAudioPlayer>(true);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < audioClipHandles.Count; i++)
        {
            if (audioClipHandles[i].IsValid())
                Addressables.Release(audioClipHandles[i]);
        }

        audioClipHandles.Clear();
        audioClipCache.Clear();
    }

    private void PlayOneShot(AssetReferenceAudioClip clipReference)
    {
        if (!IsReferenceUsable(clipReference))
            return;

        StartCoroutine(PlayOneShotRoutine(clipReference));
    }

    private IEnumerator PlayOneShotRoutine(AssetReferenceAudioClip clipReference)
    {
        ResolveAudioSource();
        if (audioSource == null)
            yield break;

        string cacheKey = ResolveReferenceCacheKey(clipReference);
        if (!string.IsNullOrEmpty(cacheKey) && audioClipCache.TryGetValue(cacheKey, out AudioClip cachedClip) && cachedClip != null)
        {
            audioSource.PlayOneShot(cachedClip);
            yield break;
        }

        yield return AddressablesHelper.EnsureCatalogLoaded();

        AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(clipReference.RuntimeKey);
        yield return handle;

        if (this == null)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
            yield break;
        }

        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Debug.LogWarning($"[PaperLegends][Audio] Failed to load addressable audio: {cacheKey}");
            if (handle.IsValid())
                Addressables.Release(handle);
            yield break;
        }

        audioClipHandles.Add(handle);
        if (!string.IsNullOrEmpty(cacheKey))
            audioClipCache[cacheKey] = handle.Result;

        ResolveAudioSource();
        if (audioSource != null)
            audioSource.PlayOneShot(handle.Result);
    }

    private HeroAudioConfig ResolveAudioConfig()
    {
        return heroConfig != null && heroConfig.audioConfig != null
            ? heroConfig.audioConfig
            : config;
    }

    private void EnsureHeroConfig(PaperLegendCharacterNetworkHandler character)
    {
        if (heroConfig != null || character == null)
            return;

        heroConfig = HeroConfigCatalog.ResolveHero(character.CharacterModelId);
    }

    private void ResolveAudioSource()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
    }

    private static bool IsReferenceUsable(AssetReference reference)
    {
        return reference != null && reference.RuntimeKeyIsValid();
    }

    private static string ResolveReferenceCacheKey(AssetReference reference)
    {
        if (reference == null)
            return string.Empty;

        if (!string.IsNullOrEmpty(reference.AssetGUID))
            return reference.AssetGUID;

        object runtimeKey = reference.RuntimeKey;
        return runtimeKey != null ? runtimeKey.ToString() : string.Empty;
    }
}
