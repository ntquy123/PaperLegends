using UnityEngine;

public class VibrationManager : MonoBehaviour
{
    public const string VibrationPrefKey = "vibrationEnabled";

    private struct VibrationProfile
    {
        public int pulses;
        public int baseDurationMs;
        public int basePauseMs;
        public int minAmplitude;
        public int maxAmplitude;
    }

    private static VibrationManager _instance;

    public static VibrationManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<VibrationManager>();
                if (_instance == null)
                {
                    var go = new GameObject(nameof(VibrationManager));
                    _instance = go.AddComponent<VibrationManager>();
                }
            }

            return _instance;
        }
    }

    [Header("Impact Force Thresholds")]
    [SerializeField] private float minImpactForce = 0.35f;
    [SerializeField] private float maxImpactForce = 2.5f;

    private readonly VibrationProfile _ballProfile = new VibrationProfile
    {
        pulses = 2,
        baseDurationMs = 18,
        basePauseMs = 14,
        minAmplitude = 70,
        maxAmplitude = 210
    };

    private readonly VibrationProfile _rockProfile = new VibrationProfile
    {
        pulses = 3,
        baseDurationMs = 26,
        basePauseMs = 12,
        minAmplitude = 90,
        maxAmplitude = 255
    };

    private readonly VibrationProfile _treeProfile = new VibrationProfile
    {
        pulses = 2,
        baseDurationMs = 22,
        basePauseMs = 16,
        minAmplitude = 60,
        maxAmplitude = 190
    };

    private readonly VibrationProfile _huSkillProfile = new VibrationProfile
    {
        pulses = 4,
        baseDurationMs = 32,
        basePauseMs = 10,
        minAmplitude = 180,
        maxAmplitude = 255
    };

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

    public void PlayImpact(HitSurface surface, float force)
    {
        if (!IsVibrationEnabled)
            return;

        if (!Application.isMobilePlatform)
            return;

        if (!TryGetProfile(surface, out var profile))
            return;

        if (force < minImpactForce)
            return;

        float intensity = Mathf.Clamp01((force - minImpactForce) / Mathf.Max(0.01f, maxImpactForce - minImpactForce));
        BuildPattern(profile, intensity, out var timings, out var amplitudes);
        Vibrate(timings, amplitudes);
    }

    public void PlayHuSkillVibration(int skillLevel)
    {
        if (!IsVibrationEnabled)
            return;

        if (!Application.isMobilePlatform)
            return;

        int clampedLevel = Mathf.Clamp(skillLevel, 1, 3);
        float intensity = Mathf.InverseLerp(1f, 3f, clampedLevel);
        BuildPattern(_huSkillProfile, Mathf.Lerp(0.7f, 1f, intensity), out var timings, out var amplitudes);
        Vibrate(timings, amplitudes);
    }

    private bool TryGetProfile(HitSurface surface, out VibrationProfile profile)
    {
        switch (surface)
        {
            case HitSurface.Ball:
                profile = _ballProfile;
                return true;
            case HitSurface.Rock:
                profile = _rockProfile;
                return true;
            case HitSurface.Tree:
                profile = _treeProfile;
                return true;
            default:
                profile = default;
                return false;
        }
    }

    private void BuildPattern(VibrationProfile profile, float intensity, out int[] timings, out int[] amplitudes)
    {
        int pulseCount = Mathf.Max(1, profile.pulses);
        timings = new int[1 + pulseCount * 2];
        amplitudes = new int[timings.Length];

        int durationMs = Mathf.RoundToInt(profile.baseDurationMs * Mathf.Lerp(0.85f, 1.35f, intensity));
        int pauseMs = Mathf.RoundToInt(profile.basePauseMs * Mathf.Lerp(1.2f, 0.6f, intensity));
        int amplitude = Mathf.RoundToInt(Mathf.Lerp(profile.minAmplitude, profile.maxAmplitude, intensity));

        timings[0] = 0;
        amplitudes[0] = 0;

        for (int i = 0; i < pulseCount; i++)
        {
            int pulseIndex = 1 + i * 2;
            timings[pulseIndex] = durationMs;
            amplitudes[pulseIndex] = amplitude;

            timings[pulseIndex + 1] = pauseMs;
            amplitudes[pulseIndex + 1] = 0;
        }
    }

    private void Vibrate(int[] timings, int[] amplitudes)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var contextClass = new AndroidJavaClass("android.content.Context"))
        {
            string vibratorService = contextClass.GetStatic<string>("VIBRATOR_SERVICE");
            using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", vibratorService))
            using (var versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                int sdk = versionClass.GetStatic<int>("SDK_INT");
                long[] timingLongs = new long[timings.Length];
                for (int i = 0; i < timings.Length; i++)
                    timingLongs[i] = timings[i];

                if (sdk >= 26)
                {
                    using (var vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = vibrationEffect.CallStatic<AndroidJavaObject>("createWaveform", timingLongs, amplitudes, -1))
                    {
                        vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    vibrator.Call("vibrate", timingLongs, -1);
                }
            }
        }
#else
        Handheld.Vibrate();
#endif
    }

    public static bool IsVibrationEnabled => PlayerPrefs.GetInt(VibrationPrefKey, 1) == 1;

    public bool TryTestVibration()
    {
        if (!Application.isMobilePlatform)
            return true;

        if (!CanVibrate())
            return false;

        VibrateOnce();
        return true;
    }

    private void VibrateOnce()
    {
        int[] timings = { 0, 40 };
        int[] amplitudes = { 0, 220 };
        Vibrate(timings, amplitudes);
    }

    private bool CanVibrate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var contextClass = new AndroidJavaClass("android.content.Context"))
        {
            string vibratorService = contextClass.GetStatic<string>("VIBRATOR_SERVICE");
            using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", vibratorService))
            {
                if (vibrator == null)
                    return false;

                bool hasVibrator = vibrator.Call<bool>("hasVibrator");
                if (!hasVibrator)
                    return false;

                using (var settingsSystem = new AndroidJavaClass("android.provider.Settings$System"))
                using (var contentResolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                {
                    int enabled = settingsSystem.CallStatic<int>("getInt", contentResolver, "haptic_feedback_enabled", 1);
                    return enabled == 1;
                }
            }
        }
#else
        return true;
#endif
    }
}
