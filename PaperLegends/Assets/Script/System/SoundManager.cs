using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("SFX Audio Source")]
    //🔊 sfxSource	Hiệu ứng tiếng bắn, va chạm, thắng/thua
    public AudioSource sfxSource;
    //🎵 bgmSource	Nhạc nền (background music)
    public AudioSource bgmSource;
    //📢 voiceSource	Âm thanh báo lượt, cảnh báo
    public AudioSource voiceSource;
    //🔔 uiSource	Âm click nút, menu, đếm ngược
    public AudioSource uiSource;
    //📢 heartbeatSource Âm thanh báo nhip tim bản thân
    public AudioSource heartbeatSource;
    [Header("Clips")]
    public AudioClip BackGroundSound;
    public List<AudioClip> footstepClips;
    public List<AudioClip> ballRollingClips;
    public List<AudioClip> ballHitClips;
    public AudioClip TurnNotifi;
    public AudioClip YourTurn;
    public AudioClip OpponentTurn;
    public AudioClip ExamTurn;
    public AudioClip Shoot;
    public AudioClip beepSound;
    public AudioClip buttonClickClip;
    [Header("Ball Switch UI")]
    public AudioClip nextBallButtonClickClip;
    public AudioClip ballSwitchSelectClip;
    public AudioClip findMatchClip;
    public AudioClip cancelMatchClip;
    public AudioClip AngryPower;
    public AudioClip FloodWarning;
    public AudioClip BallHitWater;
    public AudioClip BallHitPuddle;
    public AudioClip BallHitGrass;
    public AudioClip BallHitSwamp;
    public AudioClip StepInWater;
    public AudioClip BallHitRock;
    public AudioClip BallHitTree;
    public AudioClip ballHitKengClip;
    [Header("Shot Ball Ground Audio")]
    public AudioClip shotBallGroundImpactClip;
    public AudioClip shotBallRollingLoopClip;
    public AudioClip LuckyDrawNormal;
    public AudioClip LuckyDrawEpic;
    public AudioClip HeartbeatClip;

    [Header("Kill Cam")]
    public AudioClip killCamShooterPredictedClip;
    public AudioClip killCamShooterHitClip;
    public AudioClip killCamVictimHitClip;

    [Header("Elimination Audio")]
    public AudioClip playerEliminatedClip;

    [Header("Environment Audio")]
    public AudioClip morningClip;
    public AudioClip afternoonClip;
    public AudioClip eveningClip;
    public AudioClip rainClip;

    [Header("Tutorial Audio")]
    public List<AudioClip> tutorialStepVoiceClips = new();

    [Header("Ball Upgrade")]
    public AudioClip upgradeSuccessClip;
    public AudioClip upgradeFailureClip;
    public AudioClip upgradeExplosionClip;

    [Header("Ball Fusion")]
    public AudioClip fusionSuccessClip;
    public AudioClip fusionFailureClip;
    public AudioClip fusionExplosionClip;

    [Header("Banana Peel")]
    public AudioClip bananaSlipClip;

    [Header("Skill SFX")]
    public AudioClip useSkillClip;
    public AudioClip huSkillClip;
    public AudioClip ballBigSkillPopClip;
    public AudioClip ballBigSkillGlassClip;
    public AudioClip ballSmallSkillWhoopClip;
    public AudioClip ballSmallSkillPopClip;

    [Header("Ball Hit Volume")]
    [SerializeField] private float minImpactForce = 5f;
    [SerializeField] private float maxImpactForce = 50f;
    [SerializeField] private float minBallHitVolume = 0.1f;
    [SerializeField] private float maxBallHitVolume = 1f;

    // Danh sách âm thanh combo theo thứ tự từ x1 -> x6
    public List<AudioClip> comboClips;
    private Coroutine footstepCoroutine;
    private readonly HashSet<GameObject> footstepLoopOwners = new();
    private bool footstepLoopRequested;
    private AudioSource footstepSource;
    private Dictionary<GameObject, Coroutine> rollingCoroutines = new();
    private readonly Dictionary<GameObject, Coroutine> shotBallRollingCoroutines = new();
    private readonly Dictionary<GameObject, AudioSource> shotBallRollingSources = new();
    private AudioSource shootChargeSource;

    private bool _isBgmOverridden;
    private bool _hasTutorialBgmVolumeOverride;
    private float _tutorialPreviousBgmVolume = 1f;
    private const float ShotBallRollingMinSpeed = 0.03f;
    private const float ShotBallRollingFullVolumeSpeed = 2.5f;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            StopBackGroundSound();
            // StartCoroutine(LoadAudioClips());
        }
        else Destroy(gameObject);
    }

    private IEnumerator LoadAudioClips()
    {
        yield return AddressablesHelper.LoadAudioClip($"{AddressablePaths.Audio.Root}/Lullaby-Enzalla.mp3", clip => BackGroundSound = clip);
        yield return AddressablesHelper.LoadAudioClip($"{AddressablePaths.Audio.Root}/Run.mp3", clip =>
        {
            if (footstepClips == null) footstepClips = new List<AudioClip>();
            if (clip != null) footstepClips.Add(clip);
        });
    }

    private AudioClip GetRandomClip(List<AudioClip> clips)
    {
        if (clips == null || clips.Count == 0) return null;
        return clips[Random.Range(0, clips.Count)];
    }

    // ------------------------
    // 🚶 LOOP BƯỚC CHÂN
    // ------------------------
 
    public void StartFootstepLoop(float interval = 0.5f)
    {
        footstepLoopRequested = true;
        EnsureFootstepLoop(interval);
    }

    public void StartFootstepLoop(GameObject owner, float interval = 0.5f)
    {
        if (owner == null)
        {
            StartFootstepLoop(interval);
            return;
        }

        CleanupFootstepOwners();
        footstepLoopOwners.Add(owner);
        EnsureFootstepLoop(interval);
    }
    public void StartBallRollingLoop(GameObject owner, System.Func<float> getIntensity, float interval = 0.3f)
    {
        if (!rollingCoroutines.ContainsKey(owner))
        {
            Coroutine loop = StartCoroutine(BallRollingLoop(owner, getIntensity, interval));
            rollingCoroutines.Add(owner, loop);
        }
    }
    private IEnumerator BallRollingLoop(GameObject owner, System.Func<float> getIntensity, float interval)
    {
        while (true)
        {
            float intensity = Mathf.Clamp01(getIntensity.Invoke());
            AudioClip clip = GetRandomClip(ballRollingClips);

            if (clip != null && intensity > 0.05f)
            {
                // dùng vị trí của viên bi (owner)
                PlaySfx3D(clip, owner.transform.position, intensity);
            }

            yield return new WaitForSeconds(interval);
        }
    }


    public void StopBallRollingLoop(GameObject owner)
    {
        if (rollingCoroutines.ContainsKey(owner))
        {
            StopCoroutine(rollingCoroutines[owner]);
            rollingCoroutines.Remove(owner);
        }
    }

    public void PlayShotBallGroundImpact(Vector3 position, float intensity = 1f)
    {
        if (shotBallGroundImpactClip == null)
            return;

        PlaySfx3D(shotBallGroundImpactClip, position, Mathf.Clamp01(intensity));
    }

    public void StartShotBallRollingLoop(GameObject owner, System.Func<float> getSpeed)
    {
        if (owner == null || shotBallRollingLoopClip == null || getSpeed == null)
            return;

        if (shotBallRollingCoroutines.ContainsKey(owner))
            return;

        AudioSource source = owner.AddComponent<AudioSource>();
        AudioSource template = sfxSource != null ? sfxSource : uiSource;
        if (template != null)
        {
            CopyAudioSourceSettings(template, source);
            source.volume = 0f;
        }

        source.clip = shotBallRollingLoopClip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.Play();

        shotBallRollingSources[owner] = source;
        shotBallRollingCoroutines[owner] = StartCoroutine(ShotBallRollingLoop(owner, getSpeed, source));
    }

    public void StopShotBallRollingLoop(GameObject owner)
    {
        if (owner == null)
            return;

        if (shotBallRollingCoroutines.TryGetValue(owner, out Coroutine routine) && routine != null)
        {
            StopCoroutine(routine);
        }
        shotBallRollingCoroutines.Remove(owner);

        if (shotBallRollingSources.TryGetValue(owner, out AudioSource source) && source != null)
        {
            source.Stop();
            Destroy(source);
        }
        shotBallRollingSources.Remove(owner);
    }

    private IEnumerator ShotBallRollingLoop(GameObject owner, System.Func<float> getSpeed, AudioSource source)
    {
        while (owner != null && source != null)
        {
            float speed = Mathf.Max(0f, getSpeed.Invoke());
            float volumeScale = speed <= ShotBallRollingMinSpeed
                ? 0f
                : Mathf.InverseLerp(ShotBallRollingMinSpeed, ShotBallRollingFullVolumeSpeed, speed);
            source.volume = (sfxSource != null ? sfxSource.volume : 1f) * Mathf.Clamp01(volumeScale);

            if (!source.isPlaying && source.clip != null)
            {
                source.Play();
            }

            yield return null;
        }

        if (owner != null)
        {
            shotBallRollingCoroutines.Remove(owner);
            shotBallRollingSources.Remove(owner);
        }
    }


    private IEnumerator FootstepLoop(float interval)
    {
        while (footstepLoopRequested || HasFootstepOwners())
        {
            AudioClip clip = GetRandomClip(footstepClips);
            AudioSource source = ResolveFootstepSource();
            if (clip != null && source != null)
            {
                source.Stop();
                source.clip = clip;
                source.loop = false;
                source.Play();
            }
            yield return new WaitForSeconds(interval);
        }

        StopFootstepSource();
        footstepCoroutine = null;
    }

    public void StopFootstepLoop()
    {
        footstepLoopRequested = false;
        StopFootstepLoopIfUnused();
    }

    public void StopFootstepLoop(GameObject owner)
    {
        if (owner == null)
        {
            StopFootstepLoop();
            return;
        }

        footstepLoopOwners.Remove(owner);
        CleanupFootstepOwners();
        StopFootstepLoopIfUnused();
    }

    private void EnsureFootstepLoop(float interval)
    {
        if (footstepCoroutine == null)
            footstepCoroutine = StartCoroutine(FootstepLoop(interval));
    }

    private bool HasFootstepOwners()
    {
        CleanupFootstepOwners();
        return footstepLoopOwners.Count > 0;
    }

    private void CleanupFootstepOwners()
    {
        footstepLoopOwners.RemoveWhere(owner => owner == null);
    }

    private void StopFootstepLoopIfUnused()
    {
        if (footstepLoopRequested || HasFootstepOwners())
            return;

        if (footstepCoroutine != null)
        {
            StopCoroutine(footstepCoroutine);
            footstepCoroutine = null;
        }

        StopFootstepSource();
    }

    private AudioSource ResolveFootstepSource()
    {
        AudioSource template = sfxSource != null ? sfxSource : uiSource;
        if (template == null)
            return null;

        if (footstepSource == null)
        {
            footstepSource = gameObject.AddComponent<AudioSource>();
        }

        CopyAudioSourceSettings(template, footstepSource);
        footstepSource.volume = template.volume;
        footstepSource.playOnAwake = false;
        footstepSource.loop = false;
        footstepSource.spatialBlend = 0f;
        return footstepSource;
    }

    private void StopFootstepSource()
    {
        if (footstepSource == null)
            return;

        footstepSource.Stop();
        footstepSource.clip = null;
    }
    // ------------------------
    // 🔵 LOOP BI LĂN (âm lực)
    // ------------------------


    // ------------------------
    // 💥 BI VA CHẠM (1 lần)
    // ------------------------
    public void PlayBallHit(float force, Vector3 position)
    {
        float volume = GetBallHitVolume(force);
        AudioClip clip = GetRandomClip(ballHitClips);
        if (clip != null)
            PlaySfx3D(clip, position, volume);
      //  DOShakePosition(force);
    }

    public void PlayBallHitRock(Vector3 position, float force = 1f)
    {
        if (BallHitRock != null)
        {
            float volume = GetBallHitVolume(force);
            PlaySfx3D(BallHitRock, position, volume);
        }
    }

    public void PlayBallHitTree(Vector3 position, float force = 1f)
    {
        if (BallHitTree != null)
        {
            float volume = GetBallHitVolume(force);
            PlaySfx3D(BallHitTree, position, volume);
        }
    }

    public void PlayBallPlayerHitKeng()
    {
        PlayOneShot(ballHitKengClip);
    }

    public void PlayNotifYourTurn()
    {
        if (TurnNotifi != null)
            voiceSource.PlayOneShot(TurnNotifi);
    }
    public void PlayExamTurn()
    {
        if (ExamTurn != null)
            voiceSource.PlayOneShot(ExamTurn);
    }
    public void PlayYourTurn()
    {
        if (YourTurn != null)
            voiceSource.PlayOneShot(YourTurn);
    }

    private void PlayVoiceOrSfx(AudioClip clip)
    {
        if (clip == null)
            return;

        if (voiceSource != null)
            voiceSource.PlayOneShot(clip);
        else if (sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    public void PlayKillCamShooterPredicted()
    {
        PlayVoiceOrSfx(killCamShooterPredictedClip);
    }

    public void PlayKillCamShooterHit()
    {
        PlayVoiceOrSfx(killCamShooterHitClip);
    }

    public void PlayKillCamVictimHit()
    {
        PlayVoiceOrSfx(killCamVictimHitClip);
    }

    public void PlayPlayerEliminated()
    {
        AudioClip clip = playerEliminatedClip != null ? playerEliminatedClip : killCamVictimHitClip;
        PlayVoiceOrSfx(clip);
    }

    public void PlayOpponentTurn()
    {
        if (OpponentTurn != null)
            voiceSource.PlayOneShot(OpponentTurn);
    }
    public void PlayShootAudio()
    {
        if (Shoot == null)
            return;

        if (sfxSource != null)
        {
            sfxSource.PlayOneShot(Shoot);
            return;
        }

        PlayUiOneShot(Shoot);
    }
    public void PlayBeepSound()
    {
        uiSource.PlayOneShot(beepSound);
    }

    public void PlayHuSkill()
    {
        if (huSkillClip != null && sfxSource != null)
            sfxSource.PlayOneShot(huSkillClip);
        else
            PlayBeepSound();
    }

    public void PlayUseSkill()
    {
        PlayUiOneShot(useSkillClip);
    }

    public void PlayBallBigSkillEffect(Vector3 position)
    {
        bool played = false;
        if (ballBigSkillPopClip != null)
        {
            PlaySfx3D(ballBigSkillPopClip, position, 1f);
            played = true;
        }

        if (ballBigSkillGlassClip != null)
        {
            StartCoroutine(PlaySfx3DDelayed(ballBigSkillGlassClip, position, 0.06f, 0.75f));
            played = true;
        }

        if (!played)
            PlayUseSkill();
    }

    public void PlayBallSmallSkillEffect(Vector3 position)
    {
        bool played = false;
        if (ballSmallSkillWhoopClip != null)
        {
            PlaySfx3D(ballSmallSkillWhoopClip, position, 0.95f);
            played = true;
        }

        if (ballSmallSkillPopClip != null)
        {
            StartCoroutine(PlaySfx3DDelayed(ballSmallSkillPopClip, position, 0.18f, 0.75f));
            played = true;
        }

        if (!played)
            PlayUseSkill();
    }

    private IEnumerator PlaySfx3DDelayed(AudioClip clip, Vector3 position, float delay, float volume)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        PlaySfx3D(clip, position, volume);
    }

    public void PlayButtonClick()
    {
        PlayButtonSfx(ButtonSfx.Default);
    }

    public void PlayNextBallButtonClick()
    {
        PlayUiOneShot(nextBallButtonClickClip);
    }

    public void PlayBallSwitchSelect()
    {
        PlayUiOneShot(ballSwitchSelectClip);
    }

    private void PlayUiOneShot(AudioClip clip)
    {
        if (clip == null)
            return;

        if (uiSource != null)
        {
            uiSource.PlayOneShot(clip);
            return;
        }

        if (sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    public void PlayButtonSfx(ButtonSfx type)
    {
        AudioClip clip = null;
        switch (type)
        {
            case ButtonSfx.FindMatch:
                clip = findMatchClip;
                break;
            case ButtonSfx.CancelMatch:
                clip = cancelMatchClip;
                break;
            default:
                clip = buttonClickClip;
                break;
        }

        if (clip != null)
            PlayUiOneShot(clip);
    }
    public void PlayAngryPower()
    {
        PlayUiOneShot(AngryPower);
    }

    public void StartShootChargeAudio()
    {
        if (AngryPower == null)
            return;

        AudioSource source = ResolveShootChargeSource();
        if (source == null)
            return;

        if (source.clip == AngryPower && source.isPlaying)
            return;

        source.clip = AngryPower;
        source.loop = true;
        source.Play();
    }

    public void StopShootChargeAudio()
    {
        if (shootChargeSource == null)
            return;

        shootChargeSource.Stop();
        shootChargeSource.clip = null;
    }

    private AudioSource ResolveShootChargeSource()
    {
        AudioSource template = uiSource != null ? uiSource : sfxSource;
        if (template == null)
            return null;

        if (shootChargeSource == null)
        {
            shootChargeSource = gameObject.AddComponent<AudioSource>();
        }

        CopyAudioSourceSettings(template, shootChargeSource);
        shootChargeSource.volume = template.volume;
        shootChargeSource.playOnAwake = false;
        shootChargeSource.loop = true;
        shootChargeSource.spatialBlend = 0f;
        return shootChargeSource;
    }

    public void PlayFloodWarning()
    {
        if (FloodWarning != null)
            voiceSource.PlayOneShot(FloodWarning);
    }

    public void PlayBallHitWater(Vector3 position, float force = 1f)
    {
        if (BallHitWater != null)
        {
            float volume = GetBallHitVolume(force);
            PlaySfx3D(BallHitWater, position, volume);
        }
    }

    public void PlayBallHitPuddle(Vector3 position, float force = 1f)
    {
        if (BallHitPuddle != null)
        {
            float volume = GetBallHitVolume(force);
            PlaySfx3D(BallHitPuddle, position, volume);
        }
    }

    public void PlayBallHitGrass(Vector3 position, float force = 1f)
    {
        if (BallHitGrass != null)
        {
            float volume = GetBallHitVolume(force);
            PlaySfx3D(BallHitGrass, position, volume);
        }
    }

    public void PlayBallHitSwamp(Vector3 position, float force = 1f)
    {
        if (BallHitSwamp != null)
        {
            float volume = GetBallHitVolume(force);
            PlaySfx3D(BallHitSwamp, position, volume);
        }
    }

    private float GetBallHitVolume(float impactForce)
    {
        float normalized = Mathf.InverseLerp(minImpactForce, maxImpactForce, impactForce);
        float volume = Mathf.Lerp(minBallHitVolume, maxBallHitVolume, Mathf.Clamp01(normalized));
        return Mathf.Clamp01(volume);
    }

    public void PlayStepInWater(Vector3 position)
    {
        if (StepInWater != null)
            PlaySfx3D(StepInWater, position);
    }

    public void PlayLuckyDrawClick()
    {
        if (LuckyDrawNormal != null)
        {
            PlayUiOneShot(LuckyDrawNormal);
        }
        else
        {
            PlayButtonSfx(ButtonSfx.Default);
        }
    }

    public void PlayLuckyDrawNormal()
    {
            sfxSource.PlayOneShot(LuckyDrawNormal);
    }

    public void PlayLuckyDrawEpic()
    {
            sfxSource.PlayOneShot(LuckyDrawEpic);
    }

    public void PlayHeartbeatLoop()
    {
        if (heartbeatSource != null && HeartbeatClip != null)
        {
            heartbeatSource.clip = HeartbeatClip;
            heartbeatSource.loop = true;
            if (!heartbeatSource.isPlaying)
                heartbeatSource.Play();
        }
    }

    public void StopHeartbeatLoop()
    {
        if (heartbeatSource != null && heartbeatSource.isPlaying)
            heartbeatSource.Stop();
    }

    public void PlayUpgradeSuccess()  => PlayOneShot(upgradeSuccessClip);
    public void PlayUpgradeFailure()  => PlayOneShot(upgradeFailureClip);
    public void PlayUpgradeExplosion()=> PlayOneShot(upgradeExplosionClip);
    public void PlayFusionSuccess()   => PlayOneShot(fusionSuccessClip);
    public void PlayFusionFailure()   => PlayOneShot(fusionFailureClip);
    public void PlayFusionExplosion() => PlayOneShot(fusionExplosionClip);

    public void PlayBananaSlip(Vector3 position)
    {
        PlaySfx3D(bananaSlipClip, position);
    }

    public void PlayOneShot(AudioClip clip)
    {
        if (clip != null)
            sfxSource.PlayOneShot(clip);
    }

    private void PlaySfx3D(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null || sfxSource == null)
            return;
        sfxSource.PlayOneShot(clip);
        GameObject tempObject = new GameObject("SFX3D");
        tempObject.transform.position = position;

        AudioSource tempSource = tempObject.AddComponent<AudioSource>();
        CopyAudioSourceSettings(sfxSource, tempSource);
        tempSource.spatialBlend = 1f;
        tempSource.volume = Mathf.Clamp01(volume) * sfxSource.volume;
        tempSource.clip = clip;
        tempSource.Play();

        float pitch = Mathf.Approximately(tempSource.pitch, 0f) ? 1f : Mathf.Abs(tempSource.pitch);
        Destroy(tempObject, clip.length / pitch);
    }

    private void CopyAudioSourceSettings(AudioSource from, AudioSource to)
    {
        if (from == null || to == null)
            return;

        to.outputAudioMixerGroup = from.outputAudioMixerGroup;
        to.mute = from.mute;
        to.bypassEffects = from.bypassEffects;
        to.bypassListenerEffects = from.bypassListenerEffects;
        to.bypassReverbZones = from.bypassReverbZones;
        to.priority = from.priority;
        to.pitch = from.pitch;
        to.panStereo = from.panStereo;
        to.spatialBlend = from.spatialBlend;
        to.reverbZoneMix = from.reverbZoneMix;
        to.dopplerLevel = from.dopplerLevel;
        to.spread = from.spread;
        to.rolloffMode = from.rolloffMode;
        to.minDistance = from.minDistance;
        to.maxDistance = from.maxDistance;
        to.playOnAwake = false;
        to.loop = false;
    }

    // Phát âm thanh combo tương ứng với giá trị combo
    public void PlayComboAudio(int combo)
    {
        if (comboClips == null || comboClips.Count == 0) return;

        int index = Mathf.Clamp(combo - 1, 0, comboClips.Count - 1);
        AudioClip clip = comboClips[index];
        if (clip != null)
        {
            voiceSource.PlayOneShot(clip);
        }
    }

    public void PlayTutorialBackground()
    {
        if (bgmSource != null && !_hasTutorialBgmVolumeOverride)
        {
            _tutorialPreviousBgmVolume = bgmSource.volume;
            _hasTutorialBgmVolumeOverride = true;
        }

        OverrideBgm(morningClip, 1f, true);
    }

    public void PlayTutorialStepVoice(int stepNumber)
    {
        if (tutorialStepVoiceClips == null)
            return;

        int index = stepNumber - 1;
        if (index < 0 || index >= tutorialStepVoiceClips.Count)
            return;

        AudioClip clip = tutorialStepVoiceClips[index];
        if (clip == null)
            return;

        if (voiceSource != null)
        {
            voiceSource.Stop();
            voiceSource.PlayOneShot(clip);
            return;
        }

        if (uiSource != null)
        {
            uiSource.PlayOneShot(clip);
            return;
        }

        if (sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    public void StopTutorialAudio(bool resumeBackground = false)
    {
        if (voiceSource != null)
        {
            voiceSource.Stop();
        }

        RestoreTutorialBgmVolume();
        ClearBgmOverride(resumeBackground);
    }

    private void RestoreTutorialBgmVolume()
    {
        if (!_hasTutorialBgmVolumeOverride)
            return;

        if (bgmSource != null)
        {
            bgmSource.volume = _tutorialPreviousBgmVolume;
        }

        _hasTutorialBgmVolumeOverride = false;
    }

    public void ApplyLoginAudioSettings()
    {
        float bgmVolume = PlayerPrefs.GetFloat("bgmVolume", 1f);
        float sfxVolume = PlayerPrefs.GetFloat("sfxVolume", 1f);

        if (bgmSource != null)
            bgmSource.volume = bgmVolume;

        if (sfxSource != null)
            sfxSource.volume = sfxVolume;

        if (uiSource != null)
            uiSource.volume = sfxVolume;

        if (voiceSource != null)
            voiceSource.volume = sfxVolume;

        if (heartbeatSource != null)
            heartbeatSource.volume = sfxVolume;

        PlayBackGroundSound();
    }
    public void PlayBackGroundSound()
    {
        if (_isBgmOverridden)
            return;

        if (bgmSource != null && BackGroundSound != null)
        {
            // Gán AudioClip vào AudioSource
            bgmSource.clip = BackGroundSound;

            // Bật lặp lại cho nhạc nền
            bgmSource.loop = true;

            // Bắt đầu phát nhạc
            bgmSource.Play();
        }
    }

    public void OverrideBgm(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (bgmSource == null || clip == null)
            return;

        _isBgmOverridden = true;

        bgmSource.volume = volume;
        bgmSource.loop = loop;

        if (bgmSource.clip == clip && bgmSource.isPlaying)
            return;

        bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void ClearBgmOverride(bool resumeBackground = true)
    {
        if (!_isBgmOverridden)
            return;

        _isBgmOverridden = false;

        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }

        if (resumeBackground)
        {
            PlayBackGroundSound();
        }
    }

    public void StopBackGroundSound()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
            if (!_isBgmOverridden)
            {
                bgmSource.clip = null;
            }
        }
    }
}
