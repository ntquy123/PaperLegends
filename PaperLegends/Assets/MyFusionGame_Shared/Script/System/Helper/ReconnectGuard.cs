#if !UNITY_SERVER
using System.Collections.Generic;
using UnityEngine;

public static class ReconnectGuard
{
    private const float LogCooldownSeconds = 2f;
    private static readonly Dictionary<int, float> NextLogTimes = new();

    public static bool TryGetNameArrowContext(
        BallServerController controller,
        out int loginUserId,
        out int currentIndex,
        out PlayerNetworkHandler ownerHandler)
    {
        loginUserId = 0;
        currentIndex = 0;
        ownerHandler = null;

        if (controller == null)
            return false;

        if (GameManagerNetWork.Instance == null)
            return LogAndFail(controller, "GameManagerNetWork.Instance is null");

        var loginModel = GameManagerNetWork.Instance.loginUserModel;
        if (loginModel == null)
            return LogAndFail(controller, "loginUserModel is null");

        if (GameManagerNetWork.Instance.serverRPC == null)
        {
            if (!GameManagerNetWork.Instance.TryResolveServerRPC())
                return LogAndFail(controller, "serverRPC is null");
        }

        if (!controller.TryResolveOwnerHandlerForReconnect(out ownerHandler))
            return LogAndFail(controller, "ownerHandler is null or unresolved");

        // PlayerModel là struct, không so sánh được với null.
        // Kiểm tra playerId == 0 để xác định đối tượng chưa khởi tạo.
        if (ownerHandler.PlayerModel.playerId == 0)
            return LogAndFail(controller, "ownerHandler.PlayerModel is uninitialized or has zero playerId");


        if (GameSessionClientLocal.Instance == null)
            return LogAndFail(controller, "GameSessionClientLocal.Instance is null");

        if (GameSessionClientLocal.Instance.playerArrowPrefab == null)
            return LogAndFail(controller, "playerArrowPrefab is null");

        loginUserId = loginModel.UserId;
        currentIndex = GameManagerNetWork.Instance.serverRPC.currentPlayerIndex;
        return true;
    }

    private static bool LogAndFail(BallServerController controller, string reason)
    {
        int instanceId = controller.GetInstanceID();
        float now = Time.unscaledTime;
        if (!NextLogTimes.TryGetValue(instanceId, out float nextTime) || now >= nextTime)
        {
            NextLogTimes[instanceId] = now + LogCooldownSeconds;
            Debug.LogWarning($"[ReconnectGuard] {reason}. playerId={controller.playerId} active={controller.IsActive}");
        }

        return false;
    }
}
#endif
