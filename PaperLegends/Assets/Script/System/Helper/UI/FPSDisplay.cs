using System.Collections;
using System.Security.Cryptography;
using TMPro;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using UnityEngine.UI;

public class FPSDisplay : MonoBehaviour
{
    // ****** Cách tối ưu game
    // Tổng triangles hiển thị trên màn trong 1 frame (không phải toàn map):
    //1 An toàn: ≤ 70–80k tris
    //    Gợi ý chia cho từng loại:
    //Nhân vật chính(nếu có) : 3–5k tris
    //NPC đơn giản: 1–2k tris mỗi con
    //Ngôi nhà: 5–10k tris
    //Cây: 500–1.5k tris / cây(dùng billboard / LOD cho cây xa)
    //Đồ trang trí nhỏ(bàn ghế, thùng, đá, bụi cỏ…): 100–500 tris / cái
    //2 ) Material / Draw call
    //< 50–70 draw calls cho khung cảnh bình thường
    //3 Texture size
    // 1024×1024 là đủ đẹp
    [SerializeField] private TMP_Text fpsText;
    [SerializeField] private TMP_Text mobileFpsText;
    [SerializeField] private TMP_Text pingText;
    [SerializeField] private Image[] signalBars;
    [SerializeField] private Color inactiveSignalColor = Color.gray;

    private float deltaTime = 0.0f;
    private const float NetworkCheckInterval = 2.0f;
    private float _currentPing = -1f;
    private SignalStrength _signalStrength = SignalStrength.Disconnected;
    private ProfilerRecorder drawCallsRecorder;
    private ProfilerRecorder setPassCallsRecorder;
    private enum SignalStrength
    {
        Excellent,
        Good,
        Fair,
        Poor,
        Disconnected
    }

    private void OnEnable()
    {
        UpdateNetworkUI();
        StartCoroutine(UpdateNetworkStatusCoroutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private void Update()
    {
        // Cập nhật deltaTime
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        UpdateFpsDisplay();
    }

    private void UpdateFpsDisplay()
    {
        if (fpsText == null)
        {
            return;
        }

        if (deltaTime <= Mathf.Epsilon)
        {
            fpsText.text = "FPS: --";
            UpdateMobileFpsText("FPS: --");
            return;
        }

        float fps = 1.0f / deltaTime;
        string fpsString = $"FPS: {Mathf.CeilToInt(fps)}";
        fpsText.text = fpsString;
        UpdateMobileFpsText(fpsString);
    }

    private void UpdateMobileFpsText(string fpsString)
    {
        if (mobileFpsText != null)
        {
            mobileFpsText.text = fpsString;
        }
    }

    private IEnumerator UpdateNetworkStatusCoroutine()
    {
        var wait = new WaitForSecondsRealtime(NetworkCheckInterval);
        while (true)
        {
            yield return CheckNetworkStatus();
            yield return wait;
        }
    }

    private IEnumerator CheckNetworkStatus()
    {

        // Sử dụng tên miền mới thay cho IP
        string url = "https://gamenhalam.com";

        if (string.IsNullOrEmpty(url))
        {
            SetDisconnected();
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            float startTime = Time.realtimeSinceStartup;
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                SetDisconnected();
            }
            else
            {
                _currentPing = (Time.realtimeSinceStartup - startTime) * 1000f;
                _signalStrength = EvaluateSignalStrength(_currentPing);
            }

            UpdateNetworkUI();
        }
    }

    private void SetDisconnected()
    {
        _currentPing = -1f;
        _signalStrength = SignalStrength.Disconnected;
        UpdateNetworkUI();
    }

    private SignalStrength EvaluateSignalStrength(float ping)
    {
        if (ping < 50f) return SignalStrength.Excellent;
        if (ping < 100f) return SignalStrength.Good;
        if (ping < 200f) return SignalStrength.Fair;
        if (ping < 400f) return SignalStrength.Poor;
        return SignalStrength.Disconnected;
    }

    private void UpdateNetworkUI()
    {
        if (pingText != null)
        {
            pingText.text = _currentPing >= 0
                ? $"Ping: {Mathf.RoundToInt(_currentPing)} ms"
                : "Ping: --";
        }

        if (signalBars == null || signalBars.Length == 0)
        {
            return;
        }

        int activeBars = GetActiveBars(_signalStrength);
        Color activeColor = GetSignalColor(_signalStrength);

        for (int i = 0; i < signalBars.Length; i++)
        {
            if (signalBars[i] == null)
            {
                continue;
            }

            bool isActive = i < activeBars;
            signalBars[i].color = isActive ? activeColor : inactiveSignalColor;
        }
    }

    private int GetActiveBars(SignalStrength strength)
    {
        switch (strength)
        {
            case SignalStrength.Excellent:
                return 4;
            case SignalStrength.Good:
                return 3;
            case SignalStrength.Fair:
                return 2;
            case SignalStrength.Poor:
                return 1;
            default:
                return 0;
        }
    }

    private Color GetSignalColor(SignalStrength strength)
    {
        switch (strength)
        {
            case SignalStrength.Excellent:
                return new Color(0f, 0.8f, 0.2f);
            case SignalStrength.Good:
                return new Color(0.4f, 0.8f, 0.2f);
            case SignalStrength.Fair:
                return new Color(0.95f, 0.75f, 0.2f);
            case SignalStrength.Poor:
                return new Color(0.95f, 0.4f, 0.2f);
            default:
                return Color.red;
        }
    }
 
}
