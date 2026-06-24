using UnityEngine;
//using GooglePlayGames;
//using GooglePlayGames.BasicApi;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine.Networking;
using UnityEngine.UI;

public class LoginManager : MonoBehaviour
{
    public Text userInfoText;
    public Button signInButton;  // Nút để đăng nhập Google
                                 // Client ID từ Google Developer Console
    private string webClientId = "YOUR_WEB_CLIENT_ID";

    //async void Start()
    //{
    //    //await UnityServices.InitializeAsync();

    //    //// Khởi tạo Google Play Games Platform
    //    //PlayGamesPlatform.Activate();

    //    //if (signInButton != null)
    //    //    signInButton.onClick.AddListener(() => SignInWithGoogle(null));
    //}
    //void OnAuthenticationFinished(Task<GoogleSignInUser> task)
    //{
    //    if (task.IsFaulted || task.IsCanceled)
    //    {
    //        Debug.LogError("Google Sign-In failed: " + task.Exception);
    //        return;
    //    }

    //    Debug.Log("Google Sign-In successful!");
    //    OnGoogleLoginSuccess(task.Result.UserId, task.Result.DisplayName);

    //    if (!string.IsNullOrEmpty(task.Result.IdToken))
    //        StartCoroutine(SendTokenToBackend(task.Result.IdToken));
    //}

    void OnGoogleLoginSuccess(string userId, string displayName)
    {
        if (userInfoText != null)
            userInfoText.text = $"Signed in as {displayName}";

        Debug.Log($"User {displayName} logged in with ID {userId}");
    }
//    public void SignInWithGoogle(System.Action<LoginUserModel> onComplete)
//    {
//#if UNITY_EDITOR
//        StartCoroutine(APIManager.Instance.RunTask(APIManager.Instance.LoginOrCreateAccount("editor_fake_token"), onComplete));
//#else
//        PlayGamesPlatform.Instance.Authenticate(SignInInteractivity.CanPromptOnce, result =>
//        {
//            if (result == SignInStatus.Success)
//            {
//                OnGoogleLoginSuccess(Social.localUser.id, Social.localUser.userName);
//                PlayGamesPlatform.Instance.RequestServerSideAccess(false, authCode =>
//                {
//                    if (!string.IsNullOrEmpty(authCode))
//                    {
//                        StartCoroutine(APIManager.Instance.RunTask(APIManager.Instance.LoginOrCreateAccount(authCode), onComplete));
//                    }
//                    else
//                    {
//                        onComplete?.Invoke(null);
//                    }
//                });
//            }
//            else
//            {
//                Debug.LogError("Google Play Games sign-in failed: " + result);
//                onComplete?.Invoke(null);
//            }
//        });
//#endif
//    }
    IEnumerator SendTokenToBackend(string token)
    {
        UnityWebRequest request = new UnityWebRequest(ApiConfig.BaseUrl + "login", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes("{\"accessToken\":\"" + token + "\"}");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("✅ Backend login successful: " + request.downloadHandler.text);
        }
        else
        {
            Debug.LogError("❌ Backend login failed: " + request.error);
        }
    }
}
