using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(LoadScenesSequentially());
    }
    IEnumerator LoadScenesSequentially()
    {
        // Load Scene 1 trước (chờ nó hoàn tất)
        AsyncOperation asyncLoad1 = SceneManager.LoadSceneAsync("PersistentScene", LoadSceneMode.Additive);
        asyncLoad1.allowSceneActivation = true; // Đảm bảo Scene 1 active ngay khi load xong

        // Đợi Scene 1 load xong
        while (!asyncLoad1.isDone)
        {
            yield return null;
        }

        Debug.Log("✅ Scene 1 đã load xong, chạy Awake & Start...");

        // Load tiếp Scene 2 sau khi Scene 1 hoàn tất
        AsyncOperation asyncLoad2 = SceneManager.LoadSceneAsync("Menu", LoadSceneMode.Additive);
        asyncLoad2.allowSceneActivation = true;

        while (!asyncLoad2.isDone)
        {
            yield return null;
        }

        Debug.Log("✅ Scene 2 đã load xong!");
    }
}
