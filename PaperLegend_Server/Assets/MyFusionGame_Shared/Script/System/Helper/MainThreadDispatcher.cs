using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher instance;
    private readonly Queue<Action> actions = new Queue<Action>();
    private static int mainThreadId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeBeforeSceneLoad()
    {
        mainThreadId = Thread.CurrentThread.ManagedThreadId;
        EnsureInstance();
    }

    public static MainThreadDispatcher Instance()
    {
        return EnsureInstance();
    }

    private static MainThreadDispatcher EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        if (mainThreadId == 0)
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
        {
            Debug.LogWarning("MainThreadDispatcher chưa được khởi tạo trên main thread.");
            return instance;
        }

        var go = new GameObject(nameof(MainThreadDispatcher));
        instance = go.AddComponent<MainThreadDispatcher>();
        Debug.Log($"[MainThreadDispatcher] Auto-created bootstrap instance: {Describe(instance)}");
        return instance;
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[MainThreadDispatcher] Registered instance in Awake: {Describe(this)}");
            return;
        }

        if (instance == this)
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[MainThreadDispatcher] Awake called on current instance: {Describe(this)}");
            return;
        }

        if (ShouldReplaceExistingInstance(instance, this))
        {
            var previousInstance = instance;
            instance = this;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            DontDestroyOnLoad(gameObject);
            Debug.LogWarning($"[MainThreadDispatcher] Replacing bootstrap instance to keep scene singleton alive. old={Describe(previousInstance)} | new={Describe(this)}");
            if (previousInstance != null && previousInstance.gameObject != null)
            {
                Destroy(previousInstance.gameObject);
            }
            return;
        }

        Debug.LogWarning($"[MainThreadDispatcher] Duplicate component detected; removing component only to avoid destroying shared singleton host. existing={Describe(instance)} | duplicate={Describe(this)}");
        Destroy(this);
    }

    private static bool ShouldReplaceExistingInstance(MainThreadDispatcher currentInstance, MainThreadDispatcher candidate)
    {
        if (currentInstance == null || candidate == null)
        {
            return false;
        }

        return IsBootstrapOnlyObject(currentInstance.gameObject) && !IsBootstrapOnlyObject(candidate.gameObject);
    }

    private static bool IsBootstrapOnlyObject(GameObject go)
    {
        if (go == null)
        {
            return false;
        }

        return go.name == nameof(MainThreadDispatcher)
            && go.transform.parent == null
            && go.GetComponents<Component>().Count(component => component != null) <= 2;
    }

    private static string Describe(MainThreadDispatcher dispatcher)
    {
        if (dispatcher == null || dispatcher.gameObject == null)
        {
            return "instance=null";
        }

        string sceneName = dispatcher.gameObject.scene.IsValid()
            ? dispatcher.gameObject.scene.name
            : "<invalid-scene>";

        return $"name={dispatcher.name}, activeSelf={dispatcher.gameObject.activeSelf}, activeInHierarchy={dispatcher.gameObject.activeInHierarchy}, enabled={dispatcher.enabled}, scene={sceneName}";
    }

    public static bool IsMainThread => mainThreadId != 0 && mainThreadId == Thread.CurrentThread.ManagedThreadId;

    public void Enqueue(Action action)
    {
        if (action == null) return;
        lock (actions)
        {
            actions.Enqueue(action);
        }
    }

    private void Update()
    {
        while (true)
        {
            Action action = null;
            lock (actions)
            {
                if (actions.Count > 0)
                    action = actions.Dequeue();
            }

            if (action == null)
                break;

            action.Invoke();
        }
    }
}
