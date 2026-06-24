/*
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if !UNITY_SERVER
using DG.Tweening;
#endif

public class FloodController : MonoBehaviour
{
    public static FloodController Instance;

    /// <summary>
    /// Toggle to completely disable flooding behaviour at runtime.
    /// </summary>
    public bool FloodEnabled = false;

    [Header("Water Settings")]
    public Transform[] waterCorners; // 4 water objects at corners
    // Height amount added to the current water level each time flooding occurs
    public float targetHeight = 0.4f;
    // Maximum height that each water corner can reach
    public float maxFloodHeight = -0.2f;
 

    // track consecutive rounds a player's ball stays underwater
    private readonly Dictionary<int, int> _underWaterRounds = new();
    private int _lastRound = 0;

    private NetworkObjectManager Server => GameManagerNetWork.Instance.serverRPC;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (!FloodEnabled)
            return;
        if (Server == null || Server.rpgRoomModel.MaxPlayer == 0)
            return;

        int currentRound = (Server.TurnCount / Server.rpgRoomModel.MaxPlayer) + 1;

        if (Server.HasStateAuthority && currentRound > _lastRound)
        {
            _lastRound = currentRound;
            var available = waterCorners
                .Select((t, i) => new { t, i })
                .Where(x => x.t.position.y < maxFloodHeight)
                .ToList();

            if (available.Count == 0)
                return; // all corners reached max height

            int index = Random.Range(0, available.Count);
            var corner = available[index];
            float remaining = maxFloodHeight - corner.t.position.y;
            float raise = Mathf.Min(targetHeight, remaining);
            StartFlood(corner.i, raise);
            Server.RpcFloodUpdate(corner.i, raise);
            CheckPlayersUnderWater();
        }
    }

    public void StartFlood(int cornerIndex, float height)
    {
        if (!FloodEnabled)
            return;
        if (cornerIndex < 0 || cornerIndex >= waterCorners.Length)
            return;
        StartCoroutine(RaiseWaterRoutine(waterCorners[cornerIndex], height));
    }

    public void StartFloodNetwork(int cornerIndex, float height, int round)
    {
        if (!FloodEnabled)
            return;
        _lastRound = round;
        if (cornerIndex < 0 || cornerIndex >= waterCorners.Length)
            return;

        var water = waterCorners[cornerIndex];
        if (water.position.y >= maxFloodHeight)
            return;

        float remaining = maxFloodHeight - water.position.y;
        float raise = Mathf.Min(height, remaining);
        StartFlood(cornerIndex, raise);
        Server?.RpcFloodUpdate(cornerIndex, raise);
        //CheckPlayersUnderWater();
    }
    private IEnumerator RaiseWaterRoutine(Transform water, float height)
    {
        // Tính toán thời gian cần thiết để dâng lên trong 3 giây
        float targetY = water.position.y + height;

        // Tạo Tween với thời gian cố định là 3 giây
#if UNITY_SERVER
        water.position = new Vector3(water.position.x, targetY, water.position.z);
        yield return null;
#else
        Tween tween = water.DOMoveY(targetY, 3f).SetEase(Ease.OutQuad); // 3 giây để nước dâng lên
        yield return tween.WaitForCompletion();
#endif
    }

    private bool IsPointInPolygon(Vector2 point, IList<Vector2> polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private bool IsInsideFloodArea(Vector3 pos)
    {
        if (waterCorners == null || waterCorners.Length < 3)
            return false;

        var polygon = new List<Vector2>();
        foreach (var t in waterCorners)
        {
            polygon.Add(new Vector2(t.position.x, t.position.z));
        }

        // sort polygon around centroid to ensure proper winding
        Vector2 centroid = Vector2.zero;
        foreach (var p in polygon)
            centroid += p;
        centroid /= polygon.Count;
        polygon = polygon.OrderBy(p => Mathf.Atan2(p.y - centroid.y, p.x - centroid.x)).ToList();

        Vector2 point2D = new Vector2(pos.x, pos.z);
        if (!IsPointInPolygon(point2D, polygon))
            return false;

        float highest = waterCorners.Max(w => w.position.y);
        Vector3 origin = new Vector3(pos.x, highest + 1f, pos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, Mathf.Infinity))
        {
            if (hit.collider.CompareTag("Water"))
                return true;
        }
        return false;
    }

    public bool IsSubmerged(Vector3 worldPos)
    {
        if (!FloodEnabled)
            return false;
        if (waterCorners == null || waterCorners.Length == 0)
            return false;

        float highest = waterCorners.Max(w => w.position.y);
        return IsInsideFloodArea(worldPos) && worldPos.y < highest;
    }

    private void CheckPlayersUnderWater()
    {
        float highest = waterCorners.Max(w => w.position.y);
        if (NetworkObjectManager.Instance == null)
            return;

        foreach (var kvp in NetworkObjectManager.Instance.PlayerBalls)
        {
            int playerId = kvp.Key;
            var ball = NetworkObjectManager.Instance.GetActiveBallObject(playerId);
            if (ball == null) continue;

            bool under = IsInsideFloodArea(ball.transform.position) && ball.transform.position.y < highest;
            if (!_underWaterRounds.ContainsKey(playerId))
                _underWaterRounds[playerId] = 0;

            if (under)
            {
                _underWaterRounds[playerId]++;
                // Warn the player that staying in flood area will lead to elimination
                var indicator = ClientGameplayBridge.UI.ShowTurnIndicatorRunTime(
                    "Bạn đang trong vùng nước lũ mau rời đi nếu tiếp tục ở lại sẽ tính là thua cuộc",
                    1, 1);
                if (indicator != null)
                    StartCoroutine(indicator);
                ClientGameplayBridge.Sound.PlayFloodWarning();
            }
            else
            {
                _underWaterRounds[playerId] = 0;
            }

            if (_underWaterRounds[playerId] >= 2)
            {
                EliminatePlayer(playerId);
                _underWaterRounds[playerId] = 0;
            }
        }
    }

    private void EliminatePlayer(int playerId)
    {
        var playerGO = NetworkObjectManager.Instance.GetPlayerObject(playerId);
        if (playerGO == null) return;
        var handler = playerGO.GetComponent<PlayerNetworkHandler>();
        var model = handler.PlayerModel;
        if (model.statusPlayer == StatusPlayer.Destroy)
            return;

       // if (model.score > 0)
         //   GameSessionNetWork_Host.Instance.AddRingBalls(model.score);

        Server.RpcShowMesByUser($"{model.fullname} bị nhấn chìm!");
       // GameSessionNetWork_Host.Instance?.SetPlayerStatus(playerId, StatusPlayer.Destroy);
        //GameSessionNetWork_Host.Instance.CheckEndGame();
    }

    private void OnDisable()
    {
#if !UNITY_SERVER
        // Ensure all water tweens are killed when the controller is disabled
        if (waterCorners != null)
        {
            foreach (var t in waterCorners)
                if (t != null)
                    t.DOKill();
        }
#endif
    }
}
*/
