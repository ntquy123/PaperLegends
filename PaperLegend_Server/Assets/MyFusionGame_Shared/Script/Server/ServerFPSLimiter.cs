using UnityEngine;

public class ServerFPSLimiter : MonoBehaviour
{
    void Awake()
    {
        // Chỉ chạy trên server Linux
        if (Application.isBatchMode || SystemInfo.operatingSystem.Contains("Linux"))
        {
            // 60 tick = 60 FPS (đủ mượt, CPU ~10–20%)
            Application.targetFrameRate = 60;
            // Hoặc dùng chính xác tick rate của Fusion
            // Application.targetFrameRate = Runner?.Simulation.Config.TickRate ?? 60;
        }
    }
}
