using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;
#if !UNITY_SERVER
using DG.Tweening;
#endif
 


public class NPCController : MonoBehaviour
{
    public static NPCController Instance;
    public TurnState currentState; // Biến trạng thái
    public GameObject PlayerMain; // người chơi
    public Transform shootingPosition; // Vị trí ngón tay để viên bi quay về
    private GameObject currentBall; // Viên bi hiện tại
    //public BallController ball;
    public List<Rigidbody> ringBalls = new List<Rigidbody>(); // toàn bộ cu li trong vòng
    public float checkDelay = 0.5f; // thời gian kiểm tra lại mỗi giây xem còn viên bi nào di chuyển nữa kh
    public bool isContinueTurn = false;
    public BoxCollider playArea; // Vùng vòng tròn, kiểm tra viên bi có nằm trong không
    public List<Player> players = new List<Player>();   // Danh sách tất cả người chơi
    public int currentPlayerIndex = 0; // Chỉ số của người chơi hiện tại
    public Collider StartPoint; // Điểm xuất phát
    public int Turn = 0;// Biến để theo dõi lượt chơi đầu tiên
    public Animator fingerAnimator;
    public Transform FPPPosition;
    public MysteryChestManager mysteryChestManager;


    public float moveSpeed = 3f; // Tốc độ di chuyển
    public GameObject RingBallPrefab; // Prefab viên bi đặt trong vòng
    public Transform ExamLine; // Vị trí mốc
    private Coroutine checkTurnCoroutine;
    private List<Vector3> occupiedPositions = new List<Vector3>();
    public PlayerModel playerData;
    private bool isGameOver = false;

    //vị trí di chuyển đến khi thi và ở mức
    public Transform ExamMain;
    public Transform StartPointMain;

    public List<Material> materialCateyes = new List<Material>();
    public string closestPlayername = null; // Biến lưu trữ player gần nhất
    //* note
    //
    private void Awake()
    {
        Instance = this;
    }

    //MAIN game 
    public void Start()
    {    
        // lấy thông tin người chơi từ database
        GetInforMationPlayer();
        // SettingGame();
    }
    void Update()
    {
        //foreach (var nameDisplay in players)
        //{
        //    if (nameDisplay != null)
        //    {
        //        TextMeshPro textComponent = nameDisplay.TextDisplay.GetComponent<TextMeshPro>();
        //        if (textComponent != null)
        //        {
        //            textComponent.text = nameDisplay.isAI ? nameDisplay.fullname : playerData.PlayerName;
        //        }

        //        // Giữ chữ luôn đối diện camera, nhưng không nghiêng
       
        //        nameDisplay.TextDisplay.transform.LookAt(Camera.main.transform);
        //        nameDisplay.TextDisplay.transform.rotation = Quaternion.Euler(0, nameDisplay.TextDisplay.transform.rotation.eulerAngles.y, 0);
            
        //    }
        //}
    }
    //Đặt cược bi
    public void PlayGame(int BetCount)
    {
        //step 1 cài đặt thông số cho game mới
        SettingGame(BetCount);
        //step 2 bắt đầu lượt thi xem ai đi trước
        STEP_EXAM();
        //step 3 kiểm tra xem đã thi xong chưa qua lượt state mới
        StartCheckingTurn();

        // bước bỏ qua thi cho debug
        // currentState = TurnState.StartFromPoint;
        // StartPointTurn();

    }
    public void STEP_EXAM()
    {
        Debug.Log($"Lượt Thi");
        //step tất cả di chuyển đến chỗ thi
        MoveForExam();
        StartCoroutine(StartPlayerTurn()); // Bắt đầu lượt chơi user
       //step AI tự động bắn
        AIController.Instance.StartAIShootforExam(players);
    }    
    //Play Game
    public void SettingGame(int BetCount)
    {
        Time.timeScale = 1;
        isGameOver = false;
        GetInforMationPlayer();
        // trộn đều danh sách người chơi lên
        FisherYatesShuffle();
        DeleteAllRingBalls();
        //step đăng ký toàn bộ ball ra list riêng để xử lýCheckBallsStopped
        int totalAmount = players.Count * BetCount;
        SpawnMarbles(totalAmount);

        //step hiển thị toàn bộ danh sách người chơi
        UIControllerOffline.Instance.ShowPlayerList();
        //step hiển thị infor
        UIControllerOffline.Instance.ShowInforList();
        //hiển thị kỹ năng
        SkillManager.Instance.ShowSkilldList();
    }
  

    public void StartCheckingTurn()
    {
        if (checkTurnCoroutine == null) // Đảm bảo chỉ chạy một coroutine duy nhất
        {
            checkTurnCoroutine = StartCoroutine(CheckNextTurn());
        }
    }

    private IEnumerator CheckNextTurn()
    {
        while (true) // Chạy vòng lặp vô hạn để kiểm tra liên tục
        {
            yield return new WaitForSeconds(0.5f); // Kiểm tra mỗi 0.5 giây (có thể điều chỉnh)
            if (currentState == TurnState.Exam && AllBallsStopped() && players.All(x => x.statusPlayer == StatusPlayer.StartPoint))
            {
                // xác định thứ tự đi
                yield return DetermineTurnOrder();
                yield return new WaitForSeconds(4f);
                // bước bắt đầu từ mốc
                currentState = TurnState.StartFromPoint;
                yield return StartPointTurn();
                yield break; // Dừng coroutine sau khi đã chuyển lượt

            }
        }
    }
    private IEnumerator StartPointTurn()
    {

        // di chuyển toàn bộ đến mốc
        foreach (Player item in players)
        {
            item.statusPlayer = StatusPlayer.StartPoint;
            MoveToFromStartPointForPlayer(item, occupiedPositions);

        //    if (item.isAI)
        //        MoveToFromStartPointForPlayer(item, occupiedPositions);
        //    else
        //    {   // di chuyển người chơi chính đến mức
        //        item.playerbody.transform.position = new Vector3(CameraRotation.Instance.StartPointMain.transform.position.x,
        //                                                       PlayerMain.transform.position.y,
        //CameraRotation.Instance.StartPointMain.transform.position.z);
        //        item.isHolding = true;
        //    }

        }
        //UIControllerOffline.Instance.ShowMessage("Bắt đầu", 300f, 2f);
        yield return StartCoroutine(StartTurn());
    }

    private IEnumerator StartTurn()
    {
        CameraRotation.Instance?.StopSlowMotion();
        if (isGameOver) yield break;

        UIControllerOffline.Instance.ShowPlayerList();
        UIControllerOffline.Instance.ShowInforList();

        Player currentPlayer = players[currentPlayerIndex];
        //UIControllerOffline.Instance.ShowMesByUser( "Đến lượt");
        if (currentPlayer.isDestroy)
        {
            yield return StartCoroutine(EndTurn());
            yield return StartCoroutine(StartTurn());
            yield break;  // 🚀 Thoát luôn nếu bị phá hủy
        }

        // step kiểm tra sử dụng kỹ năng
        //kiểm tra có thể tiêu diệt ngay kẻ địch ở gần kh
         CheckClickChamCat();
 
        if (currentPlayer.isAI)
        {
            PlayerMain.SetActive(true);
            UIControllerOffline.Instance.UIforView();
            yield return StartCoroutine(UIControllerOffline.Instance.ShowTurnIndicatorRunTime("Lượt của " + currentPlayer.fullname, 1, 1));

            // 🚀 Chờ 2 giây để thông báo hiện xong rồi mới chạy AI
            yield return new WaitForSeconds(1f);
            yield return StartCoroutine(OpponentTurnSequence(currentPlayer));
        }
        else
        {
            yield return StartCoroutine(UIControllerOffline.Instance.ShowTurnIndicatorRunTime("Lượt Của bạn", 1, 1));
            yield return new WaitForSeconds(1f);
            yield return StartCoroutine(StartPlayerTurn());
        }
    }

    IEnumerator YourTurnSequence(Player currentPlayer)
    {
        currentPlayer.playerbody.SetActive(true);
        //mặc định vị trí viên bi
        Vector3 targetFristPosition = currentPlayer.ball.position;
        // nếu là bắt đầu từ điểm xuất phát thì vị trí cần đến là mức
        if (currentPlayer.statusPlayer == StatusPlayer.StartPoint)
        {
            targetFristPosition = StartPointMain.position;
           // currentPlayer.isHolding = true;
        }
        //else currentPlayer.isHolding = false;
        //di chuyển đến viên bi để bắn
        float distanceFrist = Vector3.Distance(currentPlayer.playerbody.transform.position, targetFristPosition);
        if (distanceFrist <= 1f)
        {
            // Gọi hàm SetWalkAnimation khi gần target
            moveSpeed = 0.5f;
            yield return StartCoroutine(SetWalkAnimation(currentPlayer, true));
        }
        else
        {
            // Gọi hàm SetMoveAnimation khi xa target
            moveSpeed = 3f;
            yield return StartCoroutine(SetMoveAnimation(currentPlayer, true));
        }
      
        yield return StartCoroutine(MovePlayerToPointWhenShootDOTween(currentPlayer, targetFristPosition));
        yield return StartCoroutine(SetWaitingAnimation(currentPlayer, true));
    }
    IEnumerator OpponentTurnSequence(Player currentPlayer)
    {
    
        //Đổi vị trí camera quan sát toàn bộ
        //CameraRotation.Instance.SwitchToOverview(currentPlayer.ball.position);
        BallAI ballScript = currentPlayer.ball.GetComponent<BallAI>();
        CameraRotation.Instance.StartFollowingAI(currentPlayer.playerbody.transform);
        //chọn vị đối tượng tốt nhất để bắn
        Rigidbody targetBall = FindBestTarget(currentPlayer);
        if (targetBall == null)
        {
            Debug.Log($"{currentPlayer.tagPlyer} không tìm thấy mục tiêu để bắn!");
            yield return StartCoroutine(CheckOverGameRunTime());
            yield break;
        }
        Vector3 targetFristPosition = currentPlayer.ball.position;
        if (currentPlayer.statusPlayer == StatusPlayer.StartPoint)
        {
            targetFristPosition = StartPointMain.transform.position;
            //lấy viên bi lên tay
            currentPlayer.isHolding = true;
        }

        isContinueTurn = false;

 

        // Di chuyển lại gần viên bi để bắn



        float distanceFrist = Vector3.Distance(currentPlayer.playerbody.transform.position, targetFristPosition);
        if (distanceFrist <= 1f)
        {
            // Gọi hàm SetWalkAnimation khi gần target
            moveSpeed = 0.5f;
            yield return StartCoroutine(SetWalkAnimation(currentPlayer, true));
        }
        else
        {
            // Gọi hàm SetMoveAnimation khi xa target
            moveSpeed = 3f;
            yield return StartCoroutine(SetMoveAnimation(currentPlayer, true));
        }
        //di chuyển đến viên bi để bắn
        yield return StartCoroutine(MovePlayerToPointWhenShootDOTween(currentPlayer, targetFristPosition));

        yield return StartCoroutine(LookPoint(currentPlayer, targetBall)); //nhìn vào địa điểm cần bắn
        // Step 1: Bật animation "isShoot"
        yield return StartCoroutine(SetShootAnimation(currentPlayer, true));
        //lấy viên bi lên tay
        currentPlayer.isHolding = true;
        if (playArea.bounds.Contains(currentPlayer.ball.position))
        {
            Vector3 ringBallPosition = playArea.transform.position;

            // Lấy bán kính vùng tránh từ BoxCollider
            float ringBallRadius = 0f;


            if (playArea != null)
            {
                Vector3 boxSize = Vector3.Scale(playArea.size, playArea.transform.localScale);
                ringBallRadius = Mathf.Max(boxSize.x, boxSize.z) / 2;
            }
            else
            {
                Debug.LogWarning("PlayArea không có BoxCollider.");
            }                                                      // Kiểm tra sau khi di chuyển xong, AI có nằm trong playArea không

            Debug.Log("AI vẫn đang nằm trong vùng PlayArea sau khi di chuyển, dịch ra ngoài...");

            Vector3 avoidDirection = (currentPlayer.playerbody.transform.position - ringBallPosition).normalized;
            currentPlayer.playerbody.transform.position = ringBallPosition + avoidDirection * ringBallRadius;
        }
        //step 2: camera theo dõi AI
        CameraRotation.Instance.StartFollowingBall(ballScript.CamFllowLocation.transform);
        // sử dụng kỹ năng
        yield return StartCoroutine(CheckSkillAI());

        //Giải phóng viên
        yield return StartCoroutine(AIController.Instance.ShootTurnAI(currentPlayer, targetBall));
        //step 3: Đợi tất cả viên bi ngừng
        yield return StartCoroutine(CheckBallsStopped());
        SoundManager.Instance.StopBallRollingLoop(gameObject);
        // kiểm tra xem có ra đạn không
        yield return StartCoroutine(CheckOutBall(currentPlayer));
        //kiểm tra xem có quậy không
        yield return StartCoroutine(CheckIfBallInRing(currentPlayer));
        //kiểm tra xem có người chơi nào bị loại kh để tăng điểm 
        yield return StartCoroutine(CheckRemovePlayer());

        // kiểm tra over game chưa 
        yield return StartCoroutine(CheckOverGameRunTime());
        // Thay đổi trạng thái
        currentPlayer.statusPlayer = StatusPlayer.Normal;
        UIControllerOffline.Instance.ShowInforList();
        UIControllerOffline.Instance.ShowPlayerList();
        if (isContinueTurn)
        {
            Debug.Log("♻️ Lượt tiếp tục...");
            yield return StartCoroutine(OpponentTurnSequence(currentPlayer)); // Gọi lại chính nó
        }
        else
        {
            // Step 4: Di chuyển đến chỗ viên bi
            // Kiểm tra khoảng cách giữa người chơi và target
            Vector3 targetPosition = currentPlayer.ball.position;
            float distance = Vector3.Distance(currentPlayer.playerbody.transform.position, targetPosition);
            CameraRotation.Instance.StartFollowingAI(currentPlayer.playerbody.transform);
            if (distance <= 3f)
            {
                // Gọi hàm SetWalkAnimation khi gần target
                moveSpeed = 0.5f;
                yield return StartCoroutine(SetWalkAnimation(currentPlayer, true));
            }
            else
            {
                // Gọi hàm SetMoveAnimation khi xa target
                moveSpeed = 3f;
                yield return StartCoroutine(SetMoveAnimation(currentPlayer, true));
            }

            yield return StartCoroutine(MovePlayerToPoint(currentPlayer, targetPosition)); // Chờ di chuyển hoàn tất
                                                                                           // Step 5: Đổi sang animation "isWaiting" khi đến nơi
            yield return StartCoroutine(SetWaitingAnimation(currentPlayer, true));
            // Bước 6: Nhìn vào địa điểm
            yield return StartCoroutine(LookPoint(currentPlayer, targetBall)); //nhìn vào địa điểm
            yield return StartCoroutine(EndTurn());
            yield return StartCoroutine(StartTurn());
        }

    }
   
    public void GetInforMationPlayer()
    {
        if (playerData == null)
        {
            playerData = new PlayerModel();
        }
    }
    private IEnumerator SetShootAnimation(Player currentPlayer, bool value)
    {
        if (currentPlayer.playerbody.activeSelf)
        {
            currentPlayer.animator.CrossFade("idle_shoot", 0.1f);
            yield return new WaitForSeconds(3.5f); // Đợi một frame để animation bắt đầu chạy
        }    

    }

    private IEnumerator SetMoveAnimation(Player currentPlayer, bool value)
    {
        if (currentPlayer.playerbody.activeSelf && currentPlayer.animator != null)
        {
            currentPlayer.animator.CrossFade("locom_f_running_20f", 0.1f);
            yield return new WaitForSeconds(0.5f);
        }
    }
    private IEnumerator SetWalkAnimation(Player currentPlayer, bool value)
    {
        if(currentPlayer.playerbody.activeSelf && currentPlayer.animator != null)
        {
            currentPlayer.animator.CrossFade("locom_m_basicWalk_30f", 0.1f);
            yield return new WaitForSeconds(0.5f);
        }    
     

    }

    private IEnumerator SetWaitingAnimation(Player currentPlayer, bool value)
    {
        if (currentPlayer.playerbody.activeSelf)
        {
            currentPlayer.animator.CrossFade("idle_f_1_150f", 0.1f);
            yield return new WaitForSeconds(0.5f);
        }    

    }
    public IEnumerator EndTurn()
    {
        CameraRotation.Instance?.StopSlowMotion();
        Player currentPlayer = players[currentPlayerIndex];
        if (!isContinueTurn)
            currentPlayer.combo = 0;
        if (isGameOver) yield break;
        if (currentState == TurnState.StartFromPoint && currentPlayerIndex == players.Count - 1)
        {

            currentState = TurnState.Playing;
            Debug.Log("Chuyển sang state playing");
        }

        if (mysteryChestManager != null)
        {
            mysteryChestManager.OnTurnEnd(isContinueTurn);
        }

        players[currentPlayerIndex].OnTurnEnd();
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count; // Chuyển sang người tiếp theo
    }


    void DeleteAllRingBalls()
    {
        GameObject[] ringBalls = GameObject.FindObjectsOfType<GameObject>();

        foreach (GameObject obj in ringBalls)
        {
            if (obj.name == "RingBall")
            {
                Destroy(obj);
            }
        }
    }
    // đặt ngẫu nhiên bi vào vòng
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

        // Xác định kích thước vùng spawn
        float width = areaMax.x - areaMin.x;
        float depth = areaMax.z - areaMin.z;

        // Tính toán số hàng và cột tối ưu
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(totalAmount));
        float cellSizeX = width / gridSize;
        float cellSizeZ = depth / gridSize;

        List<Vector3> spawnPositions = new List<Vector3>();

        // Tạo lưới vị trí spawn
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                if (spawnPositions.Count >= totalAmount) break;

                // Tính toán vị trí trung tâm của ô lưới
                float x = areaMin.x + (i + 0.5f) * cellSizeX;
                float z = areaMin.z + (j + 0.5f) * cellSizeZ;

                // Đảm bảo không vượt quá ranh giới vùng
                x = Mathf.Clamp(x, areaMin.x, areaMax.x);
                z = Mathf.Clamp(z, areaMin.z, areaMax.z);

                // Thêm một độ lệch ngẫu nhiên nhỏ để trông tự nhiên hơn
                float randomOffsetX = Random.Range(-cellSizeX * 0.3f, cellSizeX * 0.3f);
                float randomOffsetZ = Random.Range(-cellSizeZ * 0.3f, cellSizeZ * 0.3f);

                spawnPositions.Add(new Vector3(x + randomOffsetX, spawnHeight, z + randomOffsetZ));
            }
        }

        // Xáo trộn danh sách vị trí để tạo sự ngẫu nhiên
        spawnPositions = spawnPositions.OrderBy(p => Random.value).ToList();

        // Spawn bi theo vị trí hợp lệ
        for (int i = 0; i < totalAmount; i++)
        {
            // Lấy một material ngẫu nhiên từ danh sách materialCateye
            Material materialRandom = materialCateyes[Random.Range(0, materialCateyes.Count)];

            // Tìm đối tượng con "Cateye" và gán material ngẫu nhiên vào
            RingBallPrefab.transform.Find("Cateye").GetComponent<Renderer>().material = materialRandom;

            // Tạo góc quay ngẫu nhiên cho viên bi (quay trên mọi trục)
            Quaternion randomRotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));

            // Instantiate đối tượng RingBallPrefab tại vị trí spawnPositions[i] với góc quay ngẫu nhiên
            GameObject newBall = Instantiate(RingBallPrefab, spawnPositions[i], randomRotation);
            Rigidbody rb = newBall.GetComponent<Rigidbody>();
            if (rb != null)
            {
                RegisterBall(rb);
            }
        }
    }





    // Hàm đăng ký người chơi vào danh sách
    public void RegisterPlayer(Player player)
    {
        if (players.Exists(p => p.tagPlyer == player.tagPlyer))  // So sánh theo tên thay vì object
        {
            Debug.LogWarning($"⚠ Người chơi {player.tagPlyer} đã tồn tại trong danh sách!");
            return;
        }

        players.Add(player);
        Debug.Log($"✅ Đăng ký {player.tagPlyer} vào danh sách! (AI: {player.isAI}) - Tổng số: {players.Count}");
    }
    public bool IsPlayerRegistered(string playerName)
    {
        return players.Exists(p => p.tagPlyer == playerName);
    }
    public void RegisterBall(Rigidbody ball)
    {
        if (!ringBalls.Contains(ball))
            ringBalls.Add(ball);
    }
    public bool AllBallsStopped()
    {
        var lstBallPlayer = players.Where(x=>x.isDestroy == false).Select(x => x.ball).ToList();
        var lstBallCheck = ringBalls.Concat(lstBallPlayer);
        foreach (Rigidbody ball in lstBallCheck)
        {
            Player playerToUpdate = players.Find(p => p.ball == ball);
            if (playerToUpdate != null)
            {
                if (playerToUpdate.isHolding)
                    continue;
                // Nếu viên bi vẫn còn di chuyển
                if (ball.linearVelocity.magnitude > 0.05f || ball.angularVelocity.magnitude > 0.05f)
                {
                    return false;
                }
            }

            //
            //if(!ball.gameObject.CompareTag("RingBall") || !ball.gameObject.CompareTag("Player"))
            // Lấy script gắn trên viên bi
            //BallAI ballScript = ball.GetComponent<BallAI>();
            //if(ballScript != null)
            //{
            //    // Kiểm tra nếu không có script hoặc isHoldingAI vẫn đang true thì bỏ qua
            //    if (ballScript.isHoldingAI)
            //        continue;
            //}


        }
        return true; // Tất cả viên bi thỏa điều kiện đã dừng
    }


    private IEnumerator CheckBallsStopped()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkDelay); // Chờ một khoảng thời gian để kiểm tra lại

            if (AllBallsStopped()) // Nếu tất cả viên bi đều đứng yên                  
            {
                Debug.Log("🛑 Tất cả viên bi đã dừng!");
                yield break; // Kết thúc Coroutine
            }
        }
    }
    #region SKILL
    private void CheckClickChamCat()
    {
   
        Player currentPlayer = players[currentPlayerIndex];
        if (currentPlayer.statusPlayer != StatusPlayer.Normal || currentPlayer.score <= 0)
        {
            closestPlayername = null;
            return;
        }
        Transform currentPlayerBall = currentPlayer.ball.transform;

        float minDistance = Mathf.Infinity; // Khởi tạo khoảng cách tối thiểu
       
        float detectionRadius = 0.75f; // Bán kính ~1.5 gang tay

        // Lặp qua tất cả các player
        foreach (Player player in players.Where(x=> !x.isDestroy && x.statusPlayer == StatusPlayer.Normal))
        {
            // Bỏ qua player hiện tại
            if (player == currentPlayer) continue;

            // Tính khoảng cách giữa currentPlayer và player
            float distance = Vector3.Distance(currentPlayerBall.position, player.ball.transform.position);

            // Kiểm tra nếu player nằm trong bán kính 3f
            if (distance <= detectionRadius)
            {
                // Nếu nằm trong vùng tìm kiếm và khoảng cách nhỏ hơn khoảng cách hiện tại, cập nhật player gần nhất
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPlayername = player.name;
                }
            }
        }
        SkillManager.Instance.ShowSkilldList();
    }

    private IEnumerator CheckSkillAI()
    {
        if (!string.IsNullOrEmpty(closestPlayername))
        {
            Player currentPlayer = players[currentPlayerIndex];
            var modelCloest = players.FirstOrDefault(x => x.name == closestPlayername);
            modelCloest.isDestroy = true;
            modelCloest.playerbody.gameObject.SetActive(false);
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_skill_chamcat"), true);
            isContinueTurn = true;
            if (modelCloest.score > 0)
                AddScoreToPlayer(currentPlayer.tagPlyer, modelCloest.score, modelCloest.isAI);
            SkillManager.Instance.ShowSkilldList();
            UIControllerOffline.Instance.ShowPlayerList();
            closestPlayername = null;
            // kiểm tra over game chưa 
            yield return StartCoroutine(CheckOverGameRunTime());
            yield return StartCoroutine(OpponentTurnSequence(currentPlayer));
        }
    }    
    #endregion
    #region SHOOT
    // Khi Bắn khi người chơi bắn bi
    public void onShootBallByPlayer()
    {
        fingerAnimator.SetTrigger("HandShoot"); // Gọi animation bắn
        //bắt đầu chạy kiểm tra khi viên bi ngừng lại rồi xử lý
        StartCoroutine(RunTimeShootByPlayer());
    }
    private IEnumerator RunTimeShootByPlayer()
    {
        // tắt nút di chuyển trái phải
       // UIControllerOffline.Instance.UIforView();
        // tắt tiếp tục lượt
        isContinueTurn = false;
        // hàm kiểm tra khi tất cả viên bi trên sân chơi ngừng lại, lúc này giải quyết toàn bộ sự kiện
        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(CheckBallsStopped());
        SoundManager.Instance.StopBallRollingLoop(gameObject);
        // ngưng hiệu ứng
        //CameraRotation.Instance.StopFollowingBall();
        Player playerToUpdate = players.Find(p => p.tagPlyer == "Player");
        IFBall ballScript = playerToUpdate.ball.GetComponent<IFBall>();
        ballScript.StopShootEffect();
        // kiểm tra có phải đang thi hay không ?
        if (playerToUpdate.statusPlayer == StatusPlayer.ShootExam)
        {
            playerToUpdate.statusPlayer = StatusPlayer.StartPoint;
            yield break;
        }
        else if (playerToUpdate.statusPlayer == StatusPlayer.Power)
        {
            playerToUpdate.statusPlayer = StatusPlayer.Normal;
            CircularButton.Instance.ResetEffectPower();
        }    
        //else if (playerToUpdate.statusPlayer == StatusPlayer.StartPoint)
        //{
        //        playerToUpdate.statusPlayer = StatusPlayer.Normal;
        //}
        else
        {
            CircularButton.Instance.SetPower(0.1f);
            playerToUpdate.statusPlayer = StatusPlayer.Normal;
        }
        //CameraRotation.Instance.StopFollow();

        yield return StartCoroutine(CheckIfBallInRing(playerToUpdate));

        //kiểm tra xem có bắn bi ra khỏi vòng kh
        yield return StartCoroutine(CheckOutBall(playerToUpdate));
        //kiểm tra xem có người chơi nào bị loại kh
        yield return StartCoroutine(CheckRemovePlayer());
        //kiểm tra các điều kiện overgame
        yield return StartCoroutine(CheckOverGameRunTime());
        // di chuyển người chơi chính đến viên bi  
        //PlayerMain.SetActive(true);
      //  PlayerMain.transform.position = new Vector3(currentBall.transform.position.x,
                                                    //  PlayerMain.transform.position.y,
                                                   //  currentBall.transform.position.z);
        UIControllerOffline.Instance.ShowPlayerList();

        if (isContinueTurn)
            yield return StartCoroutine(StartPlayerTurn()); // Gọi tiếp tục lượt
        else
        {
            yield return StartCoroutine(EndTurn());
            yield return StartCoroutine(StartTurn());
        }    
           
    }
    #endregion

    public void SetCurrentBall(GameObject ball)
    {
        currentBall = ball;
    }

    //public void BallOutOfBounds(GameObject ball)
    //{
    //    Debug.Log("Ghi điểm");

    //    // Tìm Rigidbody của viên bi để loại bỏ khỏi danh sách
    //    Rigidbody ballRb = ball.GetComponent<Rigidbody>();
    //    if (ballRb != null && ringBalls.Contains(ballRb))
    //    {
    //        ringBalls.Remove(ballRb); // Xóa viên bi khỏi danh sách balls
    //        Debug.Log("Đã xóa viên bi khỏi danh sách.");
    //        Player currentPlayer = players[currentPlayerIndex];
    //        string playerName = currentPlayer.tagPlyer;
    //        AddScoreToPlayer(playerName, 1, currentPlayer.isAI);
    //        isContinueTurn = true;
    //    }
    //    else
    //    {
    //        Debug.LogWarning("Viên bi không tồn tại trong danh sách!");
    //    }
    //    Destroy(ball); // Xóa viên bi đã rời khỏi vòng
    //}
    private IEnumerator CheckOutBall(Player currentPlayer)
    {
        string playerName = currentPlayer.tagPlyer;
        int score = 0;
        // Lấy tất cả các object có tag "RingBall"
        GameObject[] ringBallsObj = GameObject.FindGameObjectsWithTag("RingBall");
            foreach (GameObject ringBall in ringBallsObj)
            {
                if (!IsInsideCube(ringBall.transform.position))
                {
                    Rigidbody ballRb = ringBall.GetComponent<Rigidbody>();
                    ringBalls.Remove(ballRb);
                    Destroy(ringBall);
                    isContinueTurn = true;
                    score++;
                }
            }
            if(score > 0)
            {
                if(currentPlayer.isAI)
                CircularButton.Instance.SetPower(score*0.3f);
                else
                CircularButton.Instance.SetPower(score * 0.1f);
            AddScoreToPlayer(playerName, score, currentPlayer.isAI);
            }    
                 
        yield return null;

    }
    bool IsInsideCube(Vector3 position)
    {
        // Lấy kích thước và vị trí của cube
        Vector3 cubeCenter = playArea.transform.position;
        Vector3 cubeSize = playArea.GetComponent<Renderer>().bounds.size;

        // Kiểm tra xem vị trí có nằm trong giới hạn của cube không
        return (position.x >= cubeCenter.x - cubeSize.x / 2 && position.x <= cubeCenter.x + cubeSize.x / 2) &&
               (position.y >= cubeCenter.y - cubeSize.y / 2 && position.y <= cubeCenter.y + cubeSize.y / 2) &&
               (position.z >= cubeCenter.z - cubeSize.z / 2 && position.z <= cubeCenter.z + cubeSize.z / 2);
    }

    //bắt đầu từ điểm xuất phát cho người chơi
    public void MoveToFromStartPointForPlayer(Player aiPlayer, List<Vector3> occupiedPositions, float minDistance = 0.5f)
    {
        Vector3 startPointPos = StartPoint.transform.position; // Vị trí gốc

        // Giới hạn di chuyển dưới mức StartPoint (Z nhỏ hơn StartPoint)
        float minZOffset = -0.5f;
        float maxZOffset = -0.5f;

        Vector3 newPosition;
        bool isValidPosition = false;
        int maxAttempts = 20; // Giới hạn số lần thử để tránh vòng lặp vô hạn

        while (!isValidPosition && maxAttempts > 0)
        {
            maxAttempts--;

            // Random vị trí X trong phạm vi quanh StartPoint
            float randomX = Random.Range(startPointPos.x - 5f, startPointPos.x + 5f);
            float randomZ = startPointPos.z + Random.Range(minZOffset, maxZOffset); // Dưới mức StartPoint

            newPosition = new Vector3(randomX, aiPlayer.playerbody.transform.position.y, randomZ);

            // Kiểm tra khoảng cách an toàn với các vị trí đã chiếm
            isValidPosition = true;
            foreach (var pos in occupiedPositions)
            {
                if (Vector3.Distance(newPosition, pos) < minDistance)
                {
                    isValidPosition = false;
                    break;
                }
            }

            // Nếu vị trí hợp lệ, đặt vị trí mới cho AI
            if (isValidPosition)
            {
                occupiedPositions.Add(newPosition);
                aiPlayer.playerbody.transform.position = newPosition;
            }
        }

        // Cầm viên bi lên hết 
        aiPlayer.isHolding = true;

        // ✅ Cách 1: Quay ngay lập tức về playArea
        //aiPlayer.playerbody.transform.LookAt(new Vector3(playArea.transform.position.x, aiPlayer.playerbody.transform.position.y, playArea.transform.position.z));

        // ✅ Cách 2: Quay mượt bằng DOTween (nếu bạn có DOTween)
#if !UNITY_SERVER
        aiPlayer.playerbody.transform.DORotateQuaternion(
            Quaternion.LookRotation(playArea.transform.position - aiPlayer.playerbody.transform.position),
            0.5f);
#else
        aiPlayer.playerbody.transform.rotation = Quaternion.LookRotation(
            playArea.transform.position - aiPlayer.playerbody.transform.position);
#endif
    }

    // Coroutine di chuyển, quay mặt về playArea và bật animation ngẫu nhiên

    //bắt đầu từ điểm xuất phát cho lượt của bạn
    //public void MoveToFromStartPointYourTurn(Player aiPlayer)
    //{
    //    UIControllerOffline.Instance.UIforPlayExamOrFromStartPoint();

    //    Vector3 targetPosition = StartPoint.transform.position;


    //    // Đặt vị trí nhân vật vào giữa
    //    aiPlayer.playerbody.transform.position = new Vector3(targetPosition.x,
    //                                                         aiPlayer.playerbody.transform.position.y,
    //                                                         targetPosition.z);
    //}

    public void MoveForExam()
    {
        // Hiển thị thông báo
        UIControllerOffline.Instance.ShowTurnIndicator("Lượt thi", 1, 1);
        currentState = TurnState.Exam;

        Vector3 examPosition = ExamLine.position; // Vị trí thi đấu
        Vector3 startPosition = StartPointMain.transform.position; // Vị trí xuất phát
        Vector3 mainExam = ExamMain.transform.position; // Vị trí người chơi chính

        float spacing = 0.3f; // Khoảng cách tối thiểu giữa các người chơi
        List<float> usedXPositions = new List<float> { mainExam.x }; // Lưu các vị trí X đã dùng, tránh trùng ExamMain.x

        foreach (Player player in players)
        {
            Vector3 finalPosition;

            if (player.isAI)
            {
                float randomX;

                // Tạo vị trí hợp lệ, tránh va chạm giữa AI và người chơi chính
                do
                {
                    randomX = examPosition.x + Random.Range(-spacing * players.Count, spacing * players.Count);
                }
                while (usedXPositions.Exists(x => Mathf.Abs(x - randomX) < spacing));

                usedXPositions.Add(randomX);

                // Xác định vị trí mới nhưng chỉ thay đổi X, giữ nguyên Y và Z
                finalPosition = new Vector3(randomX, player.playerbody.transform.position.y, examPosition.z);
            }
            else
            {
                // Người chơi chính sẽ đứng tại ExamMain
                finalPosition = new Vector3(mainExam.x, player.playerbody.transform.position.y, mainExam.z);
                player.playerbody.SetActive(true);
 
            }

            // Gọi coroutine di chuyển nhân vật mượt
            StartCoroutine(MovePlayerToPosition(player, finalPosition, startPosition));
        }
    }


    // Coroutine di chuyển và quay mượt
    private IEnumerator MovePlayerToPosition(Player player, Vector3 targetPosition, Vector3 lookAtPosition)
    {
        float moveDuration = 1.5f; // Thời gian di chuyển
        float elapsedTime = 0f;
        Vector3 startPosition = player.playerbody.transform.position;

        while (elapsedTime < moveDuration)
        {
            // Di chuyển nhân vật dần dần đến vị trí mới
            player.playerbody.transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / moveDuration);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        player.playerbody.transform.position = targetPosition; // Đảm bảo đến đúng vị trí

        // Xoay mặt về hướng startPosition (chỉ quay theo trục Y)
        Vector3 directionToStart = (lookAtPosition - player.playerbody.transform.position).normalized;
        directionToStart.y = 0; // Không thay đổi góc nhìn lên/xuống
        player.playerbody.transform.rotation = Quaternion.LookRotation(directionToStart);

        // Cập nhật trạng thái
        player.statusPlayer = StatusPlayer.ShootExam;
        player.isHolding = true;
    }


    //public void StartPlayerExamTurn()
    //{
    //    Player playerToUpdate = players.Find(p => p.tagPlyer == "Player");
    //    // bật menu cho chọn
    //    UIJoystick.SetActive(false);
    //    PlayerMain.SetActive(false);
    //    // DI chuyển camera về chỗ người chơi
    //    CameraRotation.Instance.SwitchToPlayerBall();
    //    PowerBarController.Instance.ResetBar();
    //    // Giữ viên bi lên ngón tay
    //    playerToUpdate.isHolding = true;
    //}
    // Lượt người chơi
    private IEnumerator StartPlayerTurn()
    {   //hiển thị danh sách menu phụ
        UIControllerOffline.Instance.ShowInforList();
        Player playerToUpdate = players.Find(p => p.tagPlyer == "Player");
        playerToUpdate.playerbody.SetActive(true);
        // lấy vị trí ban đầu để giới hạn di chuyển
        CameraRotation.Instance.startX = playerToUpdate.playerbody.transform.position.x;
        yield return null;
 
        if  ( playerToUpdate.statusPlayer != StatusPlayer.ShootExam)
        {
            // Di chuyển nhân vật đến vị trí cần thiết
            CameraRotation.Instance.StartFollowingAI(playerToUpdate.playerbody.transform);
            yield return StartCoroutine(YourTurnSequence(playerToUpdate));

        }

        //step Xử lý riêng lượt thi
        if (playerToUpdate.statusPlayer == StatusPlayer.ShootExam)
        {
            Debug.Log($"📷 Chuyển cảnh góc nhìn thứ 1: Lượt Thi của bạn");
            // đứng thi
            CameraRotation.Instance.MoveCameraToFPPForExam(ExamMain.position, StartPointMain.position);
            UIControllerOffline.Instance.UIforPlayExamOrFromStartPoint();
        } //step xử lý riêng lượt bắn từ mức   
        else if (playerToUpdate.statusPlayer == StatusPlayer.StartPoint)
        {
            Debug.Log($"📷 Chuyển cảnh góc nhìn thứ 1: Lượt bắn từ mức");
            CameraRotation.Instance.MoveCameraToFPP(FPPPosition.position, playArea.transform.position);
            UIControllerOffline.Instance.UIforPlayExamOrFromStartPoint();
        }    
        else
        {
            Debug.Log($"📷 Chuyển cảnh góc nhìn thứ 1: Lượt bình thường");
            CameraRotation.Instance.MoveCameraToFPP(FPPPosition.position, playArea.transform.position);
            UIControllerOffline.Instance.UIforPlay();
        }    

 
        playerToUpdate.playerbody.SetActive(false);
        yield return null;
        // rest thanh lực và lượt
        PowerBarController.Instance.ResetBar();
        isContinueTurn = false;
        // Giữ viên bi lên ngón tay
        playerToUpdate.isHolding = true;

    }
    // di chuyển đến 1 nơi khhác
    private IEnumerator LookPoint(Player aiPlayer, Rigidbody targetBall)
    {
        if (aiPlayer == null || aiPlayer.playerbody == null || targetBall == null)
        {
            Debug.LogError("Player hoặc targetBall chưa được gán.");
            yield break;
        }

        // Tính toán hướng cần quay
        Vector3 lookDirection = targetBall.transform.position - aiPlayer.playerbody.transform.position;
        lookDirection.y = 0; // Giữ nguyên chiều cao để tránh quay lên/xuống

        if (lookDirection != Vector3.zero) // Tránh lỗi khi hướng quay là (0,0,0)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

            // Sử dụng DOTween để quay đối tượng
            aiPlayer.playerbody.transform
                .DORotateQuaternion(targetRotation, 0.5f) // 0.5s để quay xong
                .SetEase(Ease.OutExpo); // Hiệu ứng mượt mà
        }

        yield return new WaitForSeconds(0.5f); // Đợi quay xong
    }

    //private IEnumerator LookPoint(Player aiPlayer, Rigidbody targetBall)
    //{
    //    // 🌟 Sau khi di chuyển xong, quay mặt về hướng playArea
    //    if (playArea != null)
    //    { 

    //        Vector3 lookDirection = targetBall.transform.position - aiPlayer.playerbody.transform.position;
    //        lookDirection.y = 0; // Giữ nguyên chiều cao để tránh quay lên/xuống

    //        if (lookDirection != Vector3.zero) // Tránh lỗi khi hướng quay là (0,0,0)
    //        {
    //            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
    //            float rotationSpeed = 5f; // Tốc độ quay (điều chỉnh tùy ý)

    //            while (Quaternion.Angle(aiPlayer.playerbody.transform.rotation, targetRotation) > 1f)
    //            {
    //                aiPlayer.playerbody.transform.rotation = Quaternion.Slerp(aiPlayer.playerbody.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    //                yield return null;
    //            }

    //            // Đảm bảo nhân vật quay đúng góc
    //            aiPlayer.playerbody.transform.rotation = targetRotation;
    //        }

    //    }
    //}
    private IEnumerator MovePlayerToPointWhenShootDOTween(Player aiPlayer, Vector3 targetPosition)
    {
        if (aiPlayer == null || aiPlayer.animator == null || playArea == null)
        {
            Debug.LogError("Player, Animator hoặc PlayArea chưa được gán.");
            yield break;
        }

        Debug.Log($"{aiPlayer.tagPlyer} Đang di chuyển");

        // Giữ nguyên chiều cao của người chơi
        float fixedY = aiPlayer.playerbody.transform.position.y;
        targetPosition.y = fixedY;

        // Kích thước vùng cấm
        float playAreaRadius = 0.5f; // Điều chỉnh bán kính tùy theo kích thước PlayArea

        // Nếu target nằm trong vùng playArea, di chuyển nó ra ngoài một chút
        Vector3 playAreaPos = playArea.transform.position;
        playAreaPos.y = fixedY; // Giữ nguyên trục Y của vùng cấm để so sánh chính xác
        float distanceToPlayArea = Vector3.Distance(new Vector3(targetPosition.x, fixedY, targetPosition.z),
                                                    new Vector3(playAreaPos.x, fixedY, playAreaPos.z));

        if (distanceToPlayArea < playAreaRadius)
        {
            // Tính toán vị trí né
            Vector3 directionAway = (targetPosition - playAreaPos).normalized;
            targetPosition = playAreaPos + directionAway * playAreaRadius;
            targetPosition.y = fixedY; // Đảm bảo trục Y không thay đổi
        }

        // Tính toán hướng quay về vị trí di chuyển
        Vector3 direction = (targetPosition - aiPlayer.playerbody.transform.position).normalized;
        direction.y = 0; // Tránh quay lên/xuống

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            // Quay đối tượng về hướng di chuyển
#if UNITY_SERVER
            aiPlayer.playerbody.transform.rotation = targetRotation;
#else
            aiPlayer.playerbody.transform.DORotateQuaternion(targetRotation, 0.5f)
                .SetEase(Ease.OutExpo);
#endif
        }

        // Di chuyển đến target với hiệu ứng mượt
        float duration = Vector3.Distance(aiPlayer.playerbody.transform.position, targetPosition) / moveSpeed;
        var finalPosition = new Vector3(targetPosition.x, fixedY, targetPosition.z);
#if UNITY_SERVER
        aiPlayer.playerbody.transform.position = finalPosition;
        Debug.Log($"{aiPlayer.tagPlyer} đã đến vị trí (server immediate move)!");
        yield return null;
#else
        aiPlayer.playerbody.transform.DOMove(finalPosition, duration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                Debug.Log($"{aiPlayer.tagPlyer} đã đến vị trí!");
            });

        yield return new WaitForSeconds(duration);
#endif
    }



    /* private IEnumerator MovePlayerToPointWhenShoot(Player aiPlayer, Vector3 targetPosition)
     {
         if (aiPlayer == null || aiPlayer.animator == null || playArea == null)
         {
             Debug.LogError("Player, Animator hoặc PlayArea chưa được gán.");
             yield break;
         }

         Debug.Log($"{aiPlayer.tagPlyer} Đang di chuyển");

         targetPosition.y = aiPlayer.playerbody.transform.position.y; // Giữ nguyên chiều cao

         // Lấy vị trí của PlayArea (RingBall)
         Vector3 ringBallPosition = playArea.transform.position;

         // Lấy bán kính vùng tránh từ BoxCollider
         float ringBallRadius = 0f;


         if (playArea != null)
         {
             Vector3 boxSize = Vector3.Scale(playArea.size, playArea.transform.localScale);
             ringBallRadius = Mathf.Max(boxSize.x, boxSize.z) / 2;
         }
         else
         {
             Debug.LogWarning("PlayArea không có BoxCollider.");
         }

         // Di chuyển đến target
         while (Vector3.Distance(aiPlayer.playerbody.transform.position, targetPosition) > 0.1f)
         {
             aiPlayer.playerbody.transform.position = Vector3.MoveTowards(aiPlayer.playerbody.transform.position, targetPosition, moveSpeed * Time.deltaTime);

             // Quay đối tượng về hướng di chuyển
             Vector3 direction = (targetPosition - aiPlayer.playerbody.transform.position).normalized;
             Quaternion targetRotation = Quaternion.LookRotation(direction);
             aiPlayer.playerbody.transform.rotation = Quaternion.Slerp(aiPlayer.playerbody.transform.rotation, targetRotation, 5f * Time.deltaTime);

             yield return null;
         }


         //if (Vector3.Distance(aiPlayer.playerbody.transform.position, ringBallPosition) < ringBallRadius)
         //{
         //    Debug.Log("AI vẫn đang nằm trong vùng PlayArea sau khi di chuyển, dịch ra ngoài...");

         //    Vector3 avoidDirection = (aiPlayer.playerbody.transform.position - ringBallPosition).normalized;
         //    aiPlayer.playerbody.transform.position = ringBallPosition + avoidDirection * ringBallRadius;
         //}
     } */



    private IEnumerator MovePlayerToPoint(Player aiPlayer, Vector3 targetPosition)
    {
        if (aiPlayer == null || aiPlayer.animator == null)
        {
            Debug.LogError("Player or Animator is not assigned.");
            yield break;
        }

        Debug.Log($"{aiPlayer.tagPlyer} Đang di chuyển");

        float safeDistance = 1.5f; // Khoảng cách an toàn để dừng trước target
        targetPosition.y = aiPlayer.playerbody.transform.position.y; // Giữ nguyên chiều cao hiện tại

        // Xác định điểm dừng cách targetPosition một khoảng
        Vector3 stopPosition = targetPosition - (targetPosition - aiPlayer.playerbody.transform.position).normalized * safeDistance;

        // Kiểm tra xem điểm đến có nằm trên layer "Ball" không
        RaycastHit hit;
        if (Physics.Raycast(stopPosition + Vector3.up * 2f, Vector3.down, out hit, 5f))
        {
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Ball"))
            {
                Debug.Log("⚠️ Vị trí đến nằm trên Ball! Điều chỉnh hướng đi...");

                // Dời vị trí sang bên cạnh (tránh chồng lên Ball)
                stopPosition += Vector3.right * 1.5f;
            }
        }

        // Bắt đầu di chuyển đến điểm stopPosition
        while (Vector3.Distance(aiPlayer.playerbody.transform.position, stopPosition) > 0.1f)
        {
            aiPlayer.playerbody.transform.position = Vector3.MoveTowards(aiPlayer.playerbody.transform.position, stopPosition, moveSpeed * Time.deltaTime);

            // Quay đối tượng về hướng di chuyển
            Vector3 direction = (stopPosition - aiPlayer.playerbody.transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                aiPlayer.playerbody.transform.rotation = Quaternion.Slerp(aiPlayer.playerbody.transform.rotation, targetRotation, 5f * Time.deltaTime);
            }

            yield return null;
        }

        Debug.Log($"{aiPlayer.tagPlyer} Đã đến vị trí cách target an toàn.");
    }





    public Rigidbody FindBestTarget(Player aiPlayer)
    {
        Rigidbody bestTarget = null;
        float minDistance = Mathf.Infinity;
        var lstBallPlayer = players.Where(x => x.isDestroy == false).Select(x => x.ball).ToList();
        var lstBallCheck = ringBalls.Concat(lstBallPlayer);
        foreach (Rigidbody ball in lstBallCheck)
        {
            if (ball != aiPlayer.ball) // Không chọn chính mình
            {
                // chỉ chọn mục tiêu có tag "RingBall" khi ở giai đoạn bắt đầu
                if (aiPlayer.statusPlayer == StatusPlayer.StartPoint && ball.tag != "RingBall")
                {
                    continue; // Bỏ qua mục tiêu không phải "RingBall"
                }

                float distance = Vector3.Distance(aiPlayer.ball.transform.position, ball.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestTarget = ball;
                }
            }
        }

        return bestTarget;
    }


    // Kiểm tra điều kiện thua 
    //1. Kiểm tra nếu viên bi của người chơi bị loại
    private IEnumerator CheckIfBallInRing(Player currentPlayer)
    {
        // Nếu viên bi nằm trong vòng => thua
        if (IsInsidePlayArea(playArea, currentPlayer.ball.position))
        {
            if (currentPlayer.isAI)
            {
                Debug.Log(currentPlayer.ball.name + " đã thua vì nằm trong vòng!");
                isContinueTurn = false;
                currentPlayer.score = 0;
                currentPlayer.statusPlayer = StatusPlayer.Destroy;
                currentPlayer.isDestroy = true;
                //Trả bi lại cho vòng.
                SpawnMarbles(currentPlayer.score);
                UIControllerOffline.Instance.ShowMesByUser($"{currentPlayer.fullname} Đã quậy");
                //yield return StartCoroutine(UIControllerOffline.Instance.ShowTurnIndicatorRunTime(currentPlayer.fullname + " Đã quậy", 1, 1));
                //yield return new WaitForSeconds(4f);
                // Tìm object có tag là tên của player và ẩn nó đi
                GameObject aiObject = GameObject.FindGameObjectWithTag(currentPlayer.tagPlyer);
                aiObject.SetActive(false);
                yield return StartCoroutine(CheckOverGameRunTime());
                if (isGameOver)
                    yield break;
                yield return StartCoroutine(EndTurn());
                yield return StartCoroutine(StartTurn());
                yield break;
            }
            else {
                currentPlayer.score = 0;
                UIControllerOffline.Instance.GameOver();
            }
            
        }
    }
    private bool IsInsidePlayArea(BoxCollider playArea, Vector3 ballPosition)
    {
        // Chuyển vị trí viên bi về không gian local của playArea
        Vector3 localPos = playArea.transform.InverseTransformPoint(ballPosition);

        // Lấy kích thước của BoxCollider
        Vector3 halfSize = playArea.size / 2;

        // Kiểm tra xem vị trí có nằm trong phạm vi không
        return (localPos.x >= -halfSize.x && localPos.x <= halfSize.x) &&
               (localPos.y >= -halfSize.y && localPos.y <= halfSize.y) &&
               (localPos.z >= -halfSize.z && localPos.z <= halfSize.z);
    }
    public bool IsYourTurn()
    {
        Player currentPlayer = players[currentPlayerIndex];
        if (currentPlayer.isAI)
        {
            return false;
        }
        else return true;
    }
    // loại bỏ người chơi nếu viên bi của họ bị bắn trúng
    public void ConfirmDestroy(string playerName)
    {
        Player playerToUpdate = players.Find(p => p.tagPlyer == playerName);
        if (playerToUpdate != null)
        {
            playerToUpdate.isDestroy = true;
            playerToUpdate.statusPlayer = StatusPlayer.WaitingDestroy;
        }
    }
   
    private IEnumerator CheckRemovePlayer()
    {
        // Tìm danh sách tất cả người chơi bị thua cuộc
        var lstDestroy = players.FindAll(p => p.statusPlayer == StatusPlayer.WaitingDestroy);
        if (lstDestroy.Count > 0)
        {   isContinueTurn= true;
            CircularButton.Instance.SetPower(0.3f);
            Player currentPlayer = players[currentPlayerIndex];
            //Lấy toàn bộ điểm của ngươi chơi đó + cho người chơi hiện tại
            int ScoreTotal = lstDestroy.Sum(x => x.score);
            if (ScoreTotal > 0)
            {
                AddScoreToPlayer(currentPlayer.tagPlyer, ScoreTotal, currentPlayer.isAI);
            }
            foreach (var item in lstDestroy)
            {
                item.statusPlayer = StatusPlayer.Destroy;
                item.score = 0;
                item.isDestroy = true;
                if (item.isAI)
                {
                    // Tìm object có tag là tên của player và ẩn nó đi
                    GameObject aiObject = GameObject.FindGameObjectWithTag(item.tagPlyer);
                    if (aiObject != null)
                    {
                        aiObject.SetActive(false);
                       // UIControllerOffline.Instance.ShowMesByUser( "Thua cuộc");
                        Debug.Log($"Đã xóa AI có tag: {item.tagPlyer}");
                        UIControllerOffline.Instance.ShowMesByUser($"{item.fullname} Đã bị loại");
                    }
                    else
                    {
                        Debug.LogWarning($"Không tìm thấy AI có tag: {item.tagPlyer}");
                    }
                }
                else
                {
                    yield return StartCoroutine(CheckOverGameRunTime());
                }
                
                
            }   

        }
        yield return null;
    }
    public void RemoveAI(string Tag)
    {
        Player currentPlayer = players[currentPlayerIndex];
        if (currentPlayer.tagPlyer != Tag)
            ConfirmDestroy(Tag);
    }
    void FisherYatesShuffle()
    {
        System.Random rng = new System.Random();
        int n = players.Count;
        for (int i = n - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (players[i], players[j]) = (players[j], players[i]); // Hoán đổi vị trí
        }
    }
    private IEnumerator DetermineTurnOrder()
    {

        foreach (Player player in players)
        {
            float distanceToLine = player.ball.position.z - StartPoint.transform.position.z; // So sánh theo trục Z
            player.distance = distanceToLine;
        }
        // ✅ Sắp xếp theo quy tắc:
        // 1. Ai trên mức (distance >= 0) được ưu tiên trước
        // 2. Ai gần mức hơn (Mathf.Abs(distance) nhỏ hơn) sẽ đi trước
        players = players.OrderBy(d => d.distance < 0)             // Dưới mức đi cuối
                             .ThenBy(d => Mathf.Abs(d.distance))       // Ai gần mức hơn sẽ đi trước
                             .ToList();

        int rank = players.FindIndex(p => p.CompareTag("Player")) + 1;
        UIControllerOffline.Instance.ShowTurnIndicator("Bạn đi thứ " + rank, 1, 1);
        yield return null;
    }

    public bool GetIsHolding(Rigidbody ball)
    {
        if (players.Count() > 0)
        {
            Player playerToUpdate = players.Find(p => p.ball == ball);
            return playerToUpdate.isHolding;

        }
        else return false;

    }
    public Player GetInforPlayer(Rigidbody ball)
    {
        if (players.Count() > 0)
        {
            Player playerToUpdate = players.Find(p => p.ball == ball);
            return playerToUpdate;

        }
        else return null;

    }
    public void UpdateIsHolding(Rigidbody ball, bool value)
    {
        Player playerToUpdate = players.Find(p => p.ball == ball);
        playerToUpdate.isHolding = value;
    }
    #region [===================== POINT ========================]


    public void AddScoreToPlayer(string playerName, int score, bool isAI)
    {
        //step 1: cộng điểm từ bắn vi ra khỏi vòng

        Player playerToUpdate = players.Find(p => p.tagPlyer == playerName);
        if (playerToUpdate != null)
        {
            playerToUpdate.score += score;
            playerToUpdate.combo += 1;
            string mess = "+" + score;
            UIControllerOffline.Instance.ShowMesByUser(mess);
            SoundManager.Instance.PlayComboAudio(playerToUpdate.combo);
            //UpdateScoreUI(playerToUpdate.score);
            // Tìm và cập nhật UI điểm số
            //if (!isAI)
            //{
            //  //  UIControllerOffline.Instance.ShowScore(playerToUpdate.ball.transform.position, score);
            //    //UIControllerOffline.Instance.ShowPlayerList();
              
            //    UpdateScoreUI(playerToUpdate.score);
            //}    
            //else
            //{
            //    UpdateScoreUI(playerToUpdate.score);
            //    //  UIControllerOffline.Instance.ShowScore(playerToUpdate.playerbody.transform.position, score);
            //   // UIControllerOffline.Instance.ShowPlayerList();
               
            //}    
               
        }
    }

    private void UpdateScoreUI(int newScore)
    {
        GameObject scoreTextObject = GameObject.FindGameObjectWithTag("Score");

        if (scoreTextObject != null)
        {
            TMP_Text scoreText = scoreTextObject.GetComponent<TMP_Text>();
            if (scoreText != null)
            {
                scoreText.text = newScore.ToString();
                
            }
            else
            {
                Debug.LogError("⚠️ Không tìm thấy component Text trên Score UI.");
            }
        }
        else
        {
            Debug.LogError("⚠️ Không tìm thấy GameObject có tag 'Score'.");
        }
    }

    #endregion

    #region [=============== GAME OVER ====================]

    private IEnumerator CheckOverGameRunTime()
    {
        // Lấy tất cả các GameObject trong scene
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();

        // Kiểm tra nếu có ít nhất một đối tượng có tag bắt đầu bằng "RingBall"
        bool hasRingBall = allObjects.Any(obj => obj.tag.StartsWith("RingBall"));

        // Kiểm tra nếu có ít nhất một đối tượng có tag bắt đầu bằng "BallAI"
        bool hasBallAI = players.Any(p => p.isAI == true && p.isDestroy == false);
        bool hasBallPlayer = players.Any(p => p.isAI == false && p.isDestroy == false);
        if (hasRingBall)
        {
            if (!hasBallPlayer)
            {
                isGameOver = true;
                UIControllerOffline.Instance.GameOver();

            }
            if (!hasBallAI)
            {
                isGameOver = true;
                // lấy toàn bộ bi trong vòng + cho bạn
                int Scorce = ringBalls.Count();
                if(Scorce > 0)
                AddScoreToPlayer("Player", Scorce, false);
                yield return new WaitForSeconds(2f);
                UIControllerOffline.Instance.GameOver();
            }

        }
        else
        {
            isGameOver = true;
            UIControllerOffline.Instance.GameOver();
        }
        yield break; // Kết thúc Coroutine
    }
    #endregion


}
