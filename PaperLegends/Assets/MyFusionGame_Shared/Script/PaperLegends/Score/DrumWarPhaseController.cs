using UnityEngine;

[DisallowMultipleComponent]
public class DrumWarPhaseController : MonoBehaviour
{
    [Header("Sky")]
    [SerializeField] private Material stormSkybox;

    [Header("Rain")]
    [SerializeField] private GameObject rainEffectRoot;

    [Header("Music")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip drumWarMusic;
    [SerializeField, Range(0f, 1f)] private float drumWarMusicVolume = 1f;
    [SerializeField] private bool loopDrumWarMusic = true;

    [Header("Wind")]
    [SerializeField] private WindZone windZone;
    [SerializeField, Min(0f)] private float drumWarWindMain = 1.4f;
    [SerializeField, Min(0f)] private float drumWarWindTurbulence = 1.1f;
    [SerializeField, Min(0f)] private float drumWarWindPulseMagnitude = 0.25f;
    [SerializeField, Min(0f)] private float drumWarWindPulseSpeed = 1.5f;

    [Header("Restore")]
    [SerializeField] private bool restoreOriginalStateWhenInactive = false;

    private bool _applied;
    private Material _originalSkybox;
    private bool _hadOriginalSkybox;
    private bool _originalRainActive;
    private AudioClip _originalBgmClip;
    private bool _originalBgmLoop;
    private float _originalBgmVolume;
    private bool _originalBgmWasPlaying;
    private float _originalWindMain;
    private float _originalWindTurbulence;

    private void Awake()
    {
        CaptureOriginalState();
        Apply(false);
    }

    private void Update()
    {
        var scoreManager = GameScoreManager.Instance;
        bool shouldApply = scoreManager != null && scoreManager.IsDrumWarActive;

        if (shouldApply != _applied)
            Apply(shouldApply);

        if (_applied)
            TickWindPulse();
    }

    private void CaptureOriginalState()
    {
        _originalSkybox = RenderSettings.skybox;
        _hadOriginalSkybox = _originalSkybox != null;

        if (rainEffectRoot != null)
            _originalRainActive = rainEffectRoot.activeSelf;

        AudioSource source = ResolveBgmSource();
        if (source != null)
        {
            _originalBgmClip = source.clip;
            _originalBgmLoop = source.loop;
            _originalBgmVolume = source.volume;
            _originalBgmWasPlaying = source.isPlaying;
        }

        if (windZone != null)
        {
            _originalWindMain = windZone.windMain;
            _originalWindTurbulence = windZone.windTurbulence;
        }
    }

    private void Apply(bool active)
    {
        _applied = active;

        if (active)
            ApplyDrumWar();
        else if (restoreOriginalStateWhenInactive)
            RestoreOriginalState();
    }

    private void ApplyDrumWar()
    {
#if !UNITY_SERVER
        if (stormSkybox != null)
            RenderSettings.skybox = stormSkybox;

        if (rainEffectRoot != null)
            rainEffectRoot.SetActive(true);

        AudioSource source = ResolveBgmSource();
        if (source != null && drumWarMusic != null)
        {
            source.clip = drumWarMusic;
            source.volume = drumWarMusicVolume;
            source.loop = loopDrumWarMusic;
            source.Play();
        }

        if (windZone != null)
        {
            windZone.windMain = drumWarWindMain;
            windZone.windTurbulence = drumWarWindTurbulence;
        }
#endif
    }

    private void RestoreOriginalState()
    {
#if !UNITY_SERVER
        if (_hadOriginalSkybox)
            RenderSettings.skybox = _originalSkybox;

        if (rainEffectRoot != null)
            rainEffectRoot.SetActive(_originalRainActive);

        AudioSource source = ResolveBgmSource();
        if (source != null)
        {
            source.clip = _originalBgmClip;
            source.loop = _originalBgmLoop;
            source.volume = _originalBgmVolume;

            if (_originalBgmWasPlaying && _originalBgmClip != null)
                source.Play();
            else
                source.Stop();
        }

        if (windZone != null)
        {
            windZone.windMain = _originalWindMain;
            windZone.windTurbulence = _originalWindTurbulence;
        }
#endif
    }

    private void TickWindPulse()
    {
#if !UNITY_SERVER
        if (windZone == null || drumWarWindPulseMagnitude <= 0f || drumWarWindPulseSpeed <= 0f)
            return;

        float pulse = Mathf.Sin(Time.time * drumWarWindPulseSpeed) * drumWarWindPulseMagnitude;
        windZone.windMain = Mathf.Max(0f, drumWarWindMain + pulse);
#endif
    }

    private AudioSource ResolveBgmSource()
    {
        if (bgmSource != null)
            return bgmSource;

#if !UNITY_SERVER
        return ClientGameplayBridge.Sound.GetChannelSource(ClientGameplayBridge.Sound.Channel.Bgm);
#else
        return null;
#endif
    }
}
