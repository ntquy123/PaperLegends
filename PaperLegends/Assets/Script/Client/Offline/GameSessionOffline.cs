using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameSessionOffline : MonoBehaviour
{
    public static GameSessionOffline Instance;

    [Header("PLAYER CONFIG")]
    public Dictionary<int, GameObject> playerDict = new();
    public Dictionary<int, List<GameObject>> playerBalls = new();
    public Dictionary<int, int> activeBallIndex = new();
    [Header("Prefab CONFIG")]
    public GameObject BallPlayerPrefab;
    public GameObject PlayerPrefab;
    [Header("GAME CONFIG")]
    public BoxCollider playArea;
    public Transform SpawnPlayerPoint;
    public Transform SpawnBallPoint;// vùng sapwm culi lúc ban đầu
   
    public bool isGameOver = false;
    public bool isContinueTurn = false;

    public List<OverGameRequest> LastOverGameResults = new();

    public Transform ExamMain;
    public Transform StartPointMain;
    public Transform StartPoint;
    public List<Transform> LstLocationExam = new();
    public List<Transform> LstLocationStartPoint = new();
    public Terrain TerrainGround; // Mặt đất để đảm bảo người chơi lúc nào cũng di chuyển trên mặt đất
    public List<TurnOrderEntry> TurnOrderList = new();
    private int currentTurn;

    private void Awake()
    {
        Instance = this;
    }
    public void ProcessPlayGame()
    {
        StartCoroutine(ProcessPlayGameRUN());
    }
    IEnumerator ProcessPlayGameRUN()
    {
        //lấy người chơi 
        var playerGO = GetDictObject(GameManagerNetWork.Instance.loginUserModel.UserId, playerDict);
        var handler = playerGO.GetComponent<PlayerOfflineHandler>();
        //di chuyển toàn bộ người chơi đến vị trí thi
        UIControllerOffline.Instance.baseTime = 60f;
        var PositionTarget = LstLocationExam[handler.PlayerModel.turnOrder];
        //dùng foreach di chuyển người chơi và máy đến vị trí
        yield return StartCoroutine(handler.MoveTo(PositionTarget.position, StartPointMain.transform));
        yield return new WaitForSeconds(2f);
        //thông báo bắt đầu thi
        UIControllerOffline.Instance.ShowTurnIndicator("Lượt Thi", 1, 1);
        //sau khi thi xong các viên bi ngưng lại thì kiểm tra xác định thứ tự lượt đi
        yield return StartCoroutine(HandleExam());
    
        // 🚀 sau khi xác định thứ tự lượt đi xong bắt đầu lượt đầu tiên sau khi sắp xếp lại vị trí
        StartCoroutine(BeginFirstTurn());
        yield break;
    }

    // Online: RpcSyncBallPhysics -> call Initialize with physics
    public void Initialize(List<PlayerInfoStruct> players, List<BallPhysicsStruct> physics)
    {
        SpawnPlayers(players);
        SpawnBalls(players, physics);
        TurnOrderList = players.Select((p, i) => new TurnOrderEntry(p.playerId, i)).ToList();
        currentTurn = 0;
    }

    private void SpawnPlayers(IEnumerable<PlayerInfoStruct> players)
    {
        foreach (var info in players)
        {
            //var prefab = Resources.Load<GameObject>("Character/" + (int)info.playerbody);
            var obj = Instantiate(PlayerPrefab, SpawnPlayerPoint.position, Quaternion.identity);
            obj.name = "Player_" + info.playerId;
            var handler = obj.GetComponent<PlayerOfflineHandler>();
            if (handler != null)
                handler.Initialize(info);
            RegisterDict(info.playerId, obj, playerDict);
        }
    }

    private void SpawnBalls(IEnumerable<PlayerInfoStruct> players, List<BallPhysicsStruct> physics)
    {
        foreach (var info in players)
        {
            var list = new List<GameObject>();
            var phys = physics.Where(p => p.playerId == info.playerId).ToList();
            for (int i = 0; i < phys.Count; i++)
            {
                var obj = Instantiate(BallPlayerPrefab, SpawnPlayerPoint.position, Quaternion.identity);
                var ctrl = obj.GetComponent<BallOfflineController>();
                ctrl.playerId = info.playerId;
                ctrl.BallIndex = i;
                ctrl.ApplyPhysics(phys[i]);
                list.Add(obj);
                if (i > 0) ctrl.SetBallActive(false);
            }
            playerBalls[info.playerId] = list;
            activeBallIndex[info.playerId] = 0;
        }
    }

    public IEnumerator StartTurn()
    {
        if (TurnOrderList.Count == 0) yield break;

        var entry = TurnOrderList[currentTurn % TurnOrderList.Count];
        int myId = GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.loginUserModel.UserId : -1;
        var playerGO = GetDictObject(entry.playerId, playerDict);
        if (playerGO == null) yield break;

        var handler = playerGO.GetComponent<PlayerOfflineHandler>();
        if (handler == null) yield break;

        if (entry.playerId == myId)
        {
            CameraRotation.Instance?.MoveCameraToFPP(handler.FPPPosition.position, handler.PointPosition.position);
            yield break; // Chờ người chơi thực hiện thao tác
        }
        else
        {
            CameraRotation.Instance?.StartFollowingAI(playerGO.transform);
            yield return StartCoroutine(AIShootRoutine(handler));
            NextTurn();
            yield return StartCoroutine(StartTurn());
        }
    }

    private IEnumerator AIShootRoutine(PlayerOfflineHandler handler)
    {
        var ball = GetActiveBallObject(handler.PlayerModel.playerId);
        if (ball == null) yield break;

        handler.RotateSightingPoint(StartPoint.position);

        float level = Mathf.Max(handler.PlayerModel.level, 1);
        float acc = Mathf.Lerp(0.5f, 0.1f, Mathf.InverseLerp(1f, 10f, level));
        Vector3 target = StartPoint.position;
        target.x += Random.Range(-acc, acc);
        target.z += Random.Range(-acc, acc);

        Vector3 dir = (target - ball.transform.position).normalized;
        float distance = Vector3.Distance(ball.transform.position, target);
        float force = Mathf.Clamp(distance * 0.8f, 0.5f, 1.3f);

        var ctrl = ball.GetComponent<BallOfflineController>();
        ctrl.ShootBall(dir, force, Vector3.zero);

        yield return new WaitForSeconds(1.5f);
    }

    private IEnumerator BeginFirstTurn()
    {
        if (TurnOrderList.Count == 0) yield break;

        var entry = TurnOrderList[currentTurn % TurnOrderList.Count];
        int myId = GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.loginUserModel.UserId : -1;
        var playerGO = GetDictObject(entry.playerId, playerDict);
        if (playerGO == null) yield break;
        var handler = playerGO.GetComponent<PlayerOfflineHandler>();
        if (handler == null) yield break;

        if (entry.playerId == myId)
        {
            CameraRotation.Instance?.MoveCameraToFPP(handler.FPPPosition.position, handler.PointPosition.position);
            yield break;
        }
        else
        {
            CameraRotation.Instance?.StartFollowingAI(playerGO.transform);
            yield return StartCoroutine(AIShootRoutine(handler));
            NextTurn();
            yield return StartCoroutine(StartTurn());
        }
    }

    public void NextTurn()
    {
        currentTurn = (currentTurn + 1) % TurnOrderList.Count;
    }

    public void RegisterDict(int playerId, GameObject obj, Dictionary<int, GameObject> dict)
    {
        if (!dict.ContainsKey(playerId))
            dict[playerId] = obj;
    }

    public GameObject GetDictObject(int playerId, Dictionary<int, GameObject> dict)
    {
        dict.TryGetValue(playerId, out var obj);
        return obj;
    }

    public GameObject GetActiveBallObject(int playerId)
    {
        if (!playerBalls.TryGetValue(playerId, out var list) || list.Count == 0)
            return null;
        int idx = GetActiveBallIndex(playerId);
        idx = Mathf.Clamp(idx, 0, list.Count - 1);
        return list[idx];
    }

    public int GetActiveBallIndex(int playerId)
    {
        if (activeBallIndex.TryGetValue(playerId, out var idx))
            return idx;
        return 0;
    }

    public void SwitchActiveBall(int playerId, int index)
    {
        if (!playerBalls.TryGetValue(playerId, out var list)) return;
        if (index < 0 || index >= list.Count) return;
        var current = GetActiveBallObject(playerId);
        if (current != null)
        {
            var ctrl = current.GetComponent<BallOfflineController>();
            ctrl.SetBallActive(false);
        }
        var nextBall = list[index];
        var nextCtrl = nextBall.GetComponent<BallOfflineController>();
        nextCtrl.SetBallActive(true);
        activeBallIndex[playerId] = index;
    }

    public void SwitchToNextBall(int playerId)
    {
        if (!playerBalls.TryGetValue(playerId, out var list) || list.Count == 0)
            return;
        int currentIdx = GetActiveBallIndex(playerId);
        int next = (currentIdx + 1) % list.Count;
        SwitchActiveBall(playerId, next);
    }

    public bool CheckEndGame()
    {
        if (isGameOver)
        {
            StartCoroutine(HandleEndGameRoutine());
            return true;
        }

        bool isOutOfRingBall = GameObject.FindGameObjectsWithTag("RingBall").Length == 0;

        int alive = TurnOrderList.Count(t =>
        {
            var go = GetDictObject(t.playerId, playerDict);
            if (go == null) return false;
            var h = go.GetComponent<PlayerOfflineHandler>();
            var m = h.PlayerModel;
            return !m.isDestroy && m.statusPlayer != StatusPlayer.Destroy && m.statusPlayer != StatusPlayer.WaitingDestroy;
        });

        if (alive <= 1)
        {
            if (alive == 1)
            {
                var survivor = TurnOrderList.First(t =>
                {
                    var go = GetDictObject(t.playerId, playerDict);
                    var h = go.GetComponent<PlayerOfflineHandler>();
                    var m = h.PlayerModel;
                    return !m.isDestroy && m.statusPlayer != StatusPlayer.Destroy && m.statusPlayer != StatusPlayer.WaitingDestroy;
                });
                var go = GetDictObject(survivor.playerId, playerDict);
                var h = go.GetComponent<PlayerOfflineHandler>();
                var m = h.PlayerModel;
                int remain = GameObject.FindGameObjectsWithTag("RingBall").Length;
                if (remain > 0)
                {
                    m.score += remain;
                    h.PlayerModel = m;
                }
                var offline = FindObjectOfType<OfflineObjectManager>();
                int target = offline != null ? offline.rpgRoomModel.betCount : 0;
                if (m.score < target)
                {
                    m.score += target - m.score;
                    h.PlayerModel = m;
                }
            }

            isGameOver = true;
        }
        else if (isOutOfRingBall)
        {
            if (!isContinueTurn)
            {
                isGameOver = true;
            }
            else
            {
                bool hasScore = TurnOrderList.Any(t =>
                {
                    var go = GetDictObject(t.playerId, playerDict);
                    var h = go.GetComponent<PlayerOfflineHandler>();
                    var m = h.PlayerModel;
                    return !m.isDestroy && m.statusPlayer != StatusPlayer.Destroy && m.statusPlayer != StatusPlayer.WaitingDestroy && m.score > 0;
                });

                if (!hasScore)
                {
                    isGameOver = true;
                }
            }
        }

        if (isGameOver)
            StartCoroutine(HandleEndGameRoutine());

        return isGameOver;
    }

    public IEnumerator HandleExam()
    {
        if (LstLocationExam.Count == 0 || LstLocationStartPoint.Count == 0)
            yield break;

        foreach (var kvp in playerDict)
        {
            int pid = kvp.Key;
            var handler = kvp.Value.GetComponent<PlayerOfflineHandler>();
            var ball = GetActiveBallObject(pid);
            if (handler == null || ball == null) continue;

            int index = TurnOrderList.FindIndex(e => e.playerId == pid);
            index = Mathf.Clamp(index, 0, LstLocationExam.Count - 1);
            Transform shootPos = LstLocationExam[index];

            kvp.Value.transform.position = shootPos.position;
            ball.transform.position = shootPos.position;
            handler.RotateSightingPoint(StartPointMain.position);

            float level = Mathf.Max(handler.PlayerModel.level, 1);
            float acc = Mathf.Lerp(0.5f, 0.1f, Mathf.InverseLerp(1f, 10f, level));
            Vector3 target = StartPoint.position;
            target.x += Random.Range(-acc, acc);
            target.z += Random.Range(-acc, acc);

            Vector3 dir = (target - ball.transform.position).normalized;
            float distance = Vector3.Distance(ball.transform.position, target);
            float force = Mathf.Clamp(distance * 0.8f, 0.5f, 1.3f);

            var ctrl = ball.GetComponent<BallOfflineController>();
            ctrl.ShootBall(dir, force, Vector3.zero);

            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(2f);

        foreach (var kvp in playerDict)
        {
            var handler = kvp.Value.GetComponent<PlayerOfflineHandler>();
            var ball = GetActiveBallObject(kvp.Key);
            if (handler == null || ball == null) continue;
            handler.PlayerModel.distance = ball.transform.position.z - StartPointMain.position.z;
        }

        var sampleHandler = playerDict.Values.FirstOrDefault()?.GetComponent<PlayerOfflineHandler>();
        if (sampleHandler != null)
        {
            sampleHandler.DetermineTurnOrder();
        }

        currentTurn = 0;

        int myId = GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.loginUserModel.UserId : -1;
        if (playerDict.ContainsKey(myId))
        {
            var handler = playerDict[myId].GetComponent<PlayerOfflineHandler>();
            string turnOrder = (handler.PlayerModel.turnOrder + 1).ToString();
            yield return StartCoroutine(UIControllerOffline.Instance.ShowTurnIndicatorRunTime("Bạn đi thứ " + turnOrder, 1, 1));
        }

        for (int i = 0; i < TurnOrderList.Count && i < LstLocationStartPoint.Count; i++)
        {
            int pid = TurnOrderList[i].playerId;
            var obj = GetDictObject(pid, playerDict);
            if (obj == null) continue;
            obj.transform.position = LstLocationStartPoint[i].position;
        }

    }

    public void HandleAfterShoot(int playerId)
    {
        isContinueTurn = false;
        var obj = GetDictObject(playerId, playerDict);
        var handler = obj != null ? obj.GetComponent<PlayerOfflineHandler>() : null;
        var ball = GetActiveBallObject(playerId);
        if (handler == null || ball == null) return;

        bool wasPower = handler.PlayerModel.statusPlayer == StatusPlayer.Power;
        handler.PlayerModel.statusPlayer = StatusPlayer.Normal;

        bool isQuay = CheckIfBallInRing2P(playerId);
        if (isQuay)
        {
            if (handler.PlayerModel.score > 0)
                AddRingBalls(handler.PlayerModel.score);
            handler.PlayerModel.score = 0;
            handler.PlayerModel.isDestroy = true;
            handler.PlayerModel.statusPlayer = StatusPlayer.Destroy;
            UIControllerOffline.Instance?.ShowMesByUser($"{handler.PlayerModel.fullname} đã quậy");
            CheckEndGame();
            return;
        }

        int scoreTotal = 0;
        if (wasPower || handler.PlayerModel.score > 0)
            scoreTotal += CheckRemovePlayer();

        scoreTotal += CheckOutBall();

        if (scoreTotal > 0)
        {
            handler.PlayerModel.score += scoreTotal;
            handler.PlayerModel.combo += 1;
            UIControllerOffline.Instance?.ShowMesByUser("+" + scoreTotal);
        }
        else
        {
            handler.PlayerModel.combo = 0;
        }

        CheckEndGame();
        if (isGameOver)
            return;

        if (isContinueTurn)
            return;

        NextTurn();
    }

    private bool CheckIfBallInRing2P(int pid)
    {
        var ballObj = GetActiveBallObject(pid);
        if (ballObj == null) return false;
        return IsInsidePlayArea(playArea, ballObj.transform.position);
    }

    private bool IsInsidePlayArea(BoxCollider area, Vector3 pos)
    {
        Vector3 local = area.transform.InverseTransformPoint(pos);
        Vector3 half = area.size / 2f;
        return (local.x >= -half.x && local.x <= half.x) &&
               (local.y >= -half.y && local.y <= half.y) &&
               (local.z >= -half.z && local.z <= half.z);
    }

    private bool IsInsideCube(Vector3 position)
    {
        Vector3 center = playArea.transform.position;
        Vector3 size = playArea.GetComponent<Renderer>().bounds.size;
        return (position.x >= center.x - size.x / 2 && position.x <= center.x + size.x / 2) &&
               (position.y >= center.y - size.y / 2 && position.y <= center.y + size.y / 2) &&
               (position.z >= center.z - size.z / 2 && position.z <= center.z + size.z / 2);
    }

    private int CheckOutBall()
    {
        int score = 0;
        var ringBallsObj = GameObject.FindGameObjectsWithTag("RingBall");
        foreach (var ringBall in ringBallsObj)
        {
            if (!IsInsideCube(ringBall.transform.position))
            {
                Destroy(ringBall);
                score++;
                isContinueTurn = true;
            }
        }
        return score;
    }

    private int CheckRemovePlayer()
    {
        int total = 0;
        var snapshot = TurnOrderList.ToList();
        foreach (var entry in snapshot)
        {
            var obj = GetDictObject(entry.playerId, playerDict);
            var h = obj.GetComponent<PlayerOfflineHandler>();
            var model = h.PlayerModel;
            if (model.statusPlayer == StatusPlayer.WaitingDestroy)
            {
                if (model.score > 0)
                    total += model.score;
                model.score = 0;
                model.isDestroy = true;
                model.statusPlayer = StatusPlayer.Destroy;
                h.PlayerModel = model;
                isContinueTurn = true;
                UIControllerOffline.Instance?.ShowMesByUser($"{model.fullname} Đã bị loại");
            }
        }
        return total;
    }

    private void AddRingBalls(int amount)
    {
        if (amount <= 0) return;
        var area = playArea.GetComponent<Collider>();
        if (area == null) return;
        Vector3 min = area.bounds.min;
        Vector3 max = area.bounds.max;
        float y = max.y + 0.2f;
        for (int i = 0; i < amount; i++)
        {
            Vector3 pos = new Vector3(Random.Range(min.x, max.x), y, Random.Range(min.z, max.z));
            var prefab = Resources.Load<GameObject>("Prefab/CuliRing/RingBallPrefab");
            if (prefab != null)
                Object.Instantiate(prefab, pos, Random.rotation);
        }
    }

    public IEnumerator HandleEndGameRoutine()
    {
        LastOverGameResults.Clear();
        var postData = new List<OverGameRequest>();
        var offline = FindObjectOfType<OfflineObjectManager>();
        var snapshot = TurnOrderList.ToList();

        int rounds = 1;
        int betByPlayer = 0;
        int maxPlayer = snapshot.Count;
        string mapName = string.Empty;
        int typeMatch = 0;

        if (offline != null)
        {
            maxPlayer = offline.rpgRoomModel.MaxPlayer;
            rounds = (offline.TurnCount / Mathf.Max(offline.rpgRoomModel.MaxPlayer, 1)) + 1;
            betByPlayer = offline.rpgRoomModel.betCount / Mathf.Max(offline.rpgRoomModel.MaxPlayer, 1);
            mapName = offline.rpgRoomModel.gameScene.Value;
            typeMatch = (int)offline.rpgRoomModel.TypeMatch;
        }

        float maxLevel = snapshot.Max(x => GetDictObject(x.playerId, playerDict).GetComponent<PlayerOfflineHandler>().PlayerModel.level);
        float minLevel = snapshot.Min(x => GetDictObject(x.playerId, playerDict).GetComponent<PlayerOfflineHandler>().PlayerModel.level);
        float levelGap = maxLevel - minLevel;

        float winCoef = 1.5f + levelGap * 0.05f;
        float loseCoef = 0.5f + levelGap * 0.02f;

        foreach (var item in snapshot)
        {
            var go = GetDictObject(item.playerId, playerDict);
            var handler = go.GetComponent<PlayerOfflineHandler>();
            var current = handler.PlayerModel;

            bool isWin = current.score > betByPlayer;
            bool isDraw = current.score == betByPlayer;

            int expGain = Mathf.RoundToInt(betByPlayer * (isWin ? winCoef : loseCoef));
            if (isDraw) expGain = Mathf.RoundToInt(betByPlayer);

            var data = new OverGameRequest
            {
                playerId = current.playerId,
                tunrOrder = item.turnOrder,
                typeMatchGid = typeMatch,
                StatusWin = isWin ? (int)StatusWin.Win : (isDraw ? (int)StatusWin.Dickens : (int)StatusWin.Lose),
                rounds = rounds,
                MapGame = mapName,
                MaxPlayer = maxPlayer,
                marbBet = betByPlayer,
                marblesWon = current.score,
                marblesLost = Mathf.Max(betByPlayer - current.score, 0),
                expGained = expGain,
                playerName = current.fullname.ToString(),
                description = "AI match",
                avatarUrl = current.avatarUrl.ToString()
            };

            postData.Add(data);
            LastOverGameResults.Add(data);
        }

        var postTask = APIManager.Instance.PostOverGame(postData);
        yield return StartCoroutine(APIManager.Instance.RunTask(postTask, null));

        GameOverManager.Instance.ShowGameOverResults(LastOverGameResults);
    }
}
