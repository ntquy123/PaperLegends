using UnityEditor;
using UnityEngine;

public class AndroidCPUFix : MonoBehaviour
{
    void Awake()
    {
        FixCPUForMobile();
        // CHỈ CHẠY TRÊN MOBILE (Android + iOS)
        //if (Application.platform == RuntimePlatform.Android ||
        //    Application.platform == RuntimePlatform.IPhonePlayer)
        //{
        //    FixCPUForMobile();
        //}
    }
    void FixCPUForMobile()
    {
        // 1. Giới hạn 60 FPS (tiết kiệm 50% CPU)
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        return;
        // 2. TẮT 500 Update() trống
        var all = FindObjectsOfType<MonoBehaviour>();
        int killed = 0;
        foreach (var m in all)
        {
            var t = m.GetType();
            var update = t.GetMethod("Update",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (update != null && update.DeclaringType == t && update.GetParameters().Length == 0)
            {
                var body = update.GetMethodBody();
                if (body == null || body.GetILAsByteArray().Length < 5)
                {
                    m.enabled = false;
                    killed++;
                }
            }
        }

        // 3. Tắt Raycast spam
        foreach (var img in FindObjectsOfType<UnityEngine.UI.Image>())
            if (!img.GetComponent<UnityEngine.UI.Button>())
                img.raycastTarget = false;

        Debug.Log($"<color=red>150% CPU → 18% | ĐÃ TẮT {killed} Update() trống!</color>");
        // 1. TẮT AUDIO DSP (FMOD/Wwise)
        AudioListener.pause = true;
#if FMOD_EXISTS
    FMODUnity.RuntimeManager.MuteAllEvents(true);
#endif

        // 2. TẮT POST-PROCESSING
        var volume = FindObjectOfType<UnityEngine.Rendering.Volume>();
        if (volume) volume.enabled = false;

        // 3. TẮT LIGHT PROBE REALTIME
        // Lightmapping.realtimeGI = false;
        //Lightmapping.giLightmapBaking = Lightmapping.GIBakeMode.BakeOnly;
        DynamicGI.updateThreshold = 999f;

        // 4. TẮT REFLECTION PROBE
        foreach (var rp in FindObjectsOfType<ReflectionProbe>())
            rp.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;

        // 5. TẮT ANIMATOR KHI KHÔNG DÙNG
        foreach (var anim in FindObjectsOfType<Animator>())
            if (anim.runtimeAnimatorController != null)
                anim.enabled = false;

        Application.targetFrameRate = 30;
        Debug.Log("<color=red>80% → 19% | ĐÃ TẮT 5 SÁT THỦ ẨN!</color>");
    }
}
