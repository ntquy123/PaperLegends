using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingGameOfflineController : MonoBehaviour
{
    public static LoadingGameOfflineController Instance;
 
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public void StartMatch()
    {
        StartCoroutine(ProcessLoadingGameOffline());
    }
    private IEnumerator ProcessLoadingGameOffline()
    {
        //Hiển thị thanh loading
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
        LoadingManager.Instance.StartLoadingLocal();
        // Ngắt kết nối khỏi phòng online
        GameManagerNetWork.Instance.CloseConnectToRunner();
        //Loading map game
        //var mapId = GameMapHelper.GetRandomMapId();
        var mapId = GameMapId.HometownHouse;
        var settings = new RpgRoomModel
        {
            gameScene = GameMapHelper.ToSceneName(mapId),
            timeOfDay = (TimeOfDay)Random.Range(0, 3),
            weatherType = (WeatherType)Random.Range(0, 2)
        };
        LoadingManager.LoadScene(settings.gameScene.ToString());
        yield return new WaitUntil(() => SceneManager.GetActiveScene().name == settings.gameScene.ToString());
        //Loading thời tiết
        DayNightWeatherManager.Instance?.ApplyEnvironment(settings.timeOfDay, settings.weatherType);
        //step Loading data bản đồ game
        yield return StartCoroutine(GameInitializer.Instance.InitializeGameOffline());
        //step loading thông tin nhân vật từ database
        yield return StartCoroutine(LoadPlayerInforOffline(settings));
        //ẩn thông báo loading
        LoadingManager.Instance.FinishLoading();
        //vào game sau khi load xong
        GameSessionOffline.Instance.ProcessPlayGame();
       
        yield break;
    }
    private IEnumerator LoadPlayerInforOffline(RpgRoomModel settings)
    {
        List<int> listUserId = new List<int>();
        listUserId.Add(GameManagerNetWork.Instance.loginUserModel.UserId);
        listUserId.Add(Random.Range(110,111));

        PlayerInfoStruct[] players = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetListPlayerGameById(listUserId),
            result => players = result));

        List<PlayerBallPhysics> physicsData = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetBallPhysicsAsync(listUserId),
            result => physicsData = result));

        var ballPhysics = new List<BallPhysicsStruct>();
        if (physicsData != null)
        {
            foreach (var pdata in physicsData)
            {
                foreach (var item in pdata.physics)
                {
                    var bp = new BallPhysicsStruct
                    {
                        playerId = pdata.playerId,
                        name = item?.name ?? string.Empty,
                        skillGenCode = item?.activeSkill?.GenCode ?? 0,
                        Mass = item?.Mass ?? 0f,
                        GravityScale = item?.GravityScale ?? 0f,
                        Drag = item?.Drag ?? 0f,
                        Bounciness = item?.Bounciness ?? 0f,
                        Elasticity = item?.Elasticity ?? 0f,
                        ImpactResistance = item?.ImpactResistance ?? 0f
                    };
                    ballPhysics.Add(bp);
                }
            }
        }

        OfflineObjectManager offline = FindObjectOfType<OfflineObjectManager>();
        if (offline == null)
        {
            GameObject obj = new GameObject("OfflineObjectManager");
            offline = obj.AddComponent<OfflineObjectManager>();
            DontDestroyOnLoad(obj);
        }



        offline.Initialize(players != null ? new List<PlayerInfoStruct>(players) : new List<PlayerInfoStruct>(), ballPhysics, settings);


        GameSessionOffline session = FindObjectOfType<GameSessionOffline>();
        if (session == null)
        {
            Debug.LogError("Error System");
        }

        session.Initialize(offline.players, offline.ballPhysicsList);

    }
   
}
