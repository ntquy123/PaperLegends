using UnityEngine;
using UnityEngine.UI;

public class PowerBarController : MonoBehaviour
{
    public static PowerBarController Instance;

    [Header("UI")]
    public Slider powerSlider;          // min=0, max=1 (khuyên)
    [Header("Charge")]
    [Tooltip("Giữ lại để không mất config cũ. Logic mới không dùng ping-pong nữa.")]
    public float cycleDuration = 1.2f;
    [SerializeField, Range(0.1f, 0.95f), Tooltip("Từ 0 đến mốc này thanh lực chạy chậm để dễ kiểm soát.")]
    private float slowControlEndPower01 = 0.6f;
    [SerializeField, Min(0.1f), Tooltip("Thời gian nạp từ 0 đến mốc dễ kiểm soát.")]
    private float slowControlDuration = 1f;
    [SerializeField, Min(0.01f), Tooltip("Multiplier applied to all charge speeds (use >1 to speed up).")]
    private float globalSpeedMultiplier = 4f;
    [SerializeField, Min(1f), Tooltip("Hệ số tốc độ khi vừa vượt khỏi vùng dễ kiểm soát.")]
    private float highPowerStartSpeedMultiplier = 1.35f;
    [SerializeField, Min(1f), Tooltip("Hệ số tốc độ khi gần đầy lực.")]
    private float highPowerMaxSpeedMultiplier = 3f;
    public bool isShootting = false;

    float _lastValue;         // lưu giá trị cuối
    bool _isFullPower;
    float _fullPowerReachedTime;

    public bool IsFullPower => isShootting && _isFullPower;
    public float FullPowerElapsed => IsFullPower ? Time.time - _fullPowerReachedTime : 0f;

    void Awake()
    {
        Instance = this;
        if (powerSlider != null)
        {
            // Khuyên dùng slider 0..1 để logic đơn giản
            powerSlider.minValue = 0f;
            powerSlider.maxValue = 1f;
        }
    }

    void Update()
    {
        if (!isShootting || powerSlider == null) return;

        if (_isFullPower)
        {
            _lastValue = 1f;
            powerSlider.value = 1f;
            return;
        }

        _lastValue = Mathf.Clamp01(_lastValue + GetChargeSpeed(_lastValue) * Time.deltaTime);
        if (_lastValue >= 1f)
        {
            _lastValue = 1f;
            _isFullPower = true;
            _fullPowerReachedTime = Time.time;
        }

        powerSlider.value = _lastValue;
    }

    // Gọi khi bắt đầu giữ joystick. Giữ tên cũ để không phải đổi các call site.
    public void StartPingPong()
    {
        isShootting = true;
        _lastValue = 0f;
        _isFullPower = false;
        _fullPowerReachedTime = 0f;
        if (powerSlider) powerSlider.value = 0f;
    }

    // Gọi khi buông → dừng và trả về lực 0..1
    public float StopPingPongAndGet01()
    {
        isShootting = false;
        SoundManager.Instance?.StopShootChargeAudio();
        // giữ nguyên _lastValue trên thanh
        return Mathf.Clamp01(_lastValue);
    }

    public void ResetBar()
    {
        isShootting = false;
        SoundManager.Instance?.StopShootChargeAudio();
        _lastValue = 0f;
        _isFullPower = false;
        _fullPowerReachedTime = 0f;
        if (powerSlider) powerSlider.value = 0f;
    }

    private float GetChargeSpeed(float currentPower01)
    {
        float slowEnd = Mathf.Clamp01(slowControlEndPower01);
        float slowSpeed = slowEnd / Mathf.Max(0.1f, slowControlDuration);
        float globalMultiplier = Mathf.Max(0.0001f, globalSpeedMultiplier);
        if (currentPower01 < slowEnd)
            return slowSpeed * globalMultiplier;

        float high01 = Mathf.InverseLerp(slowEnd, 1f, currentPower01);
        float speedMultiplier = Mathf.Lerp(
            highPowerStartSpeedMultiplier,
            highPowerMaxSpeedMultiplier,
            Smooth01(high01));

        return slowSpeed * speedMultiplier * globalMultiplier;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void OnDisable()
    {
        isShootting = false;
        _isFullPower = false;
        SoundManager.Instance?.StopShootChargeAudio();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SoundManager.Instance?.StopShootChargeAudio();
    }
}
