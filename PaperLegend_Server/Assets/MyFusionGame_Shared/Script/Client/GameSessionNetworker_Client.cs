// ========================
// GameSessionNetworker_Client.cs
// ========================
/*
using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameSessionNetworker_Client : MonoBehaviour
{
    public static GameSessionNetworker_Client Instance;
    public PlayerInfoStruct[] _cachedPlayers;
    public bool isGameOver = false;
    public BoxCollider playArea;
    public Transform ExamMain;
    public Transform StartPointMain;
    public List<Material> materialCateyes = new List<Material>();
    public List<Rigidbody> ringBalls = new List<Rigidbody>();
    public GameObject RingBallPrefab;
    public float spawnRadius = 10f;
    public float minDistanceBetweenPlayers = 2.5f;
    public LayerMask groundLayer;
    public Transform spawnParent;
    private List<Vector3> usedPositions = new List<Vector3>();
    public GameObject playerArrowPrefab;

    private void Awake()
    {
        Instance = this;
    }


    public void RequestUserList(string roomId)
    {
        Debug.Log($"📤 [CLIENT] Gửi yêu cầu danh sách user cho phòng: {roomId}");

        // Gọi RPC nếu có
#if FUSION_RPC_AVAILABLE
        RPC_RequestUserList(roomId); // Hàm này chỉ có trong server project
#else
        Debug.LogWarning("⚠️ RPC_RequestUserList không khả dụng trong project Client. Hãy chắc chắn đang kết nối đến Server thực sự.");
#endif
    }
    void SpawnMarbles(int totalAmount)
    {
        Collider areaCollider = playArea.GetComponent<Collider>();
        if (areaCollider == null)
        {
            Debug.LogError("⚠️ PlayArea cần có Collider để tính giới hạn spawn!");
            return;
        }

        Vector3 areaMin = areaCollider.bounds.min;
        Vector3 areaMax = areaCollider.bounds.max;
        float spawnHeight = areaMax.y + 0.2f;
        float width = areaMax.x - areaMin.x;
        float depth = areaMax.z - areaMin.z;
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(totalAmount));
        float cellSizeX = width / gridSize;
        float cellSizeZ = depth / gridSize;

        List<Vector3> spawnPositions = new List<Vector3>();
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                if (spawnPositions.Count >= totalAmount) break;

                float x = areaMin.x + (i + 0.5f) * cellSizeX;
                float z = areaMin.z + (j + 0.5f) * cellSizeZ;
                x = Mathf.Clamp(x, areaMin.x, areaMax.x);
                z = Mathf.Clamp(z, areaMin.z, areaMax.z);

                float randomOffsetX = Random.Range(-cellSizeX * 0.3f, cellSizeX * 0.3f);
                float randomOffsetZ = Random.Range(-cellSizeZ * 0.3f, cellSizeZ * 0.3f);

                spawnPositions.Add(new Vector3(x + randomOffsetX, spawnHeight, z + randomOffsetZ));
            }
        }

        spawnPositions = spawnPositions.OrderBy(p => Random.value).ToList();

        for (int i = 0; i < totalAmount; i++)
        {
            Material materialRandom = materialCateyes[Random.Range(0, materialCateyes.Count)];
            RingBallPrefab.transform.Find("Cateye").GetComponent<Renderer>().material = materialRandom;
            Quaternion randomRotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            Instantiate(RingBallPrefab, spawnPositions[i], randomRotation);
        }
    }

    void RegisterAllBalls()
    {
        ringBalls.Clear();
        Rigidbody[] allRigidbodies = FindObjectsOfType<Rigidbody>();
        foreach (Rigidbody rb in allRigidbodies)
        {
            if (rb.gameObject.CompareTag("RingBall"))
            {
                ringBalls.Add(rb);
            }
        }
        Debug.Log($"🔵 Đã đăng ký {ringBalls.Count} viên bi có Tag 'RingBall'");
    }

    void MoveForExam()
    {
        ClientGameplayBridge.UI.ShowTurnIndicator("Lượt thi", 1, 1);
        Vector3 examPosition = ExamMain.position;
        Vector3 startPosition = StartPointMain.transform.position;
        float spacing = 0.3f;
        List<float> usedXPositions = new List<float>();

        foreach (PlayerInfoStruct player in _cachedPlayers)
        {
            Vector3 finalPosition;
            float randomX;
            do
            {
                randomX = examPosition.x + Random.Range(-spacing * _cachedPlayers.Length, spacing * _cachedPlayers.Length);
            }
            while (usedXPositions.Exists(x => Mathf.Abs(x - randomX) < spacing));

            usedXPositions.Add(randomX);
          //  finalPosition = new Vector3(randomX, player.playerbody.transform.position.y, examPosition.z);
         //   StartCoroutine(MovePlayerToPosition(player, finalPosition, startPosition));
        }
    }

    private IEnumerator MovePlayerToPosition(Player player, Vector3 targetPosition, Vector3 lookAtPosition)
    {
        float moveDuration = 1.5f;
        float elapsedTime = 0f;
        Vector3 startPosition = player.playerbody.transform.position;

        while (elapsedTime < moveDuration)
        {
            player.playerbody.transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / moveDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        player.playerbody.transform.position = targetPosition;
        Vector3 directionToStart = (lookAtPosition - player.playerbody.transform.position).normalized;
        directionToStart.y = 0;
        player.playerbody.transform.rotation = Quaternion.LookRotation(directionToStart);
        player.statusPlayer = StatusPlayer.ShootExam;
        player.isHolding = true;
    }

 

    public IEnumerator SpawnPlayersFromServerData()
    {

        if (_cachedPlayers == null || _cachedPlayers.Length == 0)
        {
            Debug.LogWarning("⚠️ Không có người chơi để spawn");
            yield break;
        }

        usedPositions.Clear();
        foreach (PlayerInfoStruct user in _cachedPlayers)
        {
            string path = $"{AddressablePaths.Character.Root}/{user.playerbody}.prefab";
            GameObject prefab = null;
            yield return AddressablesHelper.LoadAsset<GameObject>(path, p => prefab = p);

            if (prefab == null)
            {
                Debug.LogError($"❌ Không tìm thấy prefab: {path}");
                continue;
            }

            Vector3 spawnPos = FindValidSpawnPosition();

            if (spawnPos == Vector3.zero)
            {
                Debug.LogWarning("⚠️ Không tìm được vị trí spawn hợp lệ!");
                continue;
            }

            GameObject player = Instantiate(prefab, spawnPos, Quaternion.identity, spawnParent);
            usedPositions.Add(spawnPos);
        }
    }

    private Vector3 FindValidSpawnPosition()
    {
        int maxAttempts = 50;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = ExamMain.position + new Vector3(randCircle.x, 10f, randCircle.y);

            if (Physics.Raycast(candidate, Vector3.down, out RaycastHit hit, 20f, groundLayer))
            {
                Vector3 groundPos = hit.point;
                bool tooClose = false;
                foreach (Vector3 pos in usedPositions)
                {
                    if (Vector3.Distance(pos, groundPos) < minDistanceBetweenPlayers)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (!tooClose)
                {
                    return groundPos;
                }
            }
        }
        return Vector3.zero;
    }
}

 */
