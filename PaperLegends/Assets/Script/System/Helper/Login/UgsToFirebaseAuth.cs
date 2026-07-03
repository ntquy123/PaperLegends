using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts; // <-- cần cho PlayerAccountService
#if UNITY_ANDROID
using Firebase;
using Firebase.Auth;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif
using Firebase.Extensions;
using UnityEngine.Networking;

public class UgsToFirebaseAuth : MonoBehaviour
{
    public static UgsToFirebaseAuth Instance;
    [SerializeField] private bool bypassGooglePlayGamesLoginForTesting = true;
    private FirebaseAuth _auth;
    private FirebaseUser _currentFirebaseUser;
    private AuthenticationProviderType _lastProviderType = AuthenticationProviderType.Anonymous;
    public string CurrentAvatarUrl { get; private set; }
 

    async void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        await InitFirebase();
        InitGooglePlayGames();
        //  await InitUgs();
    }

    private async Task InitFirebase()
    {
        var status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status == DependencyStatus.Available)
        {
            _auth = FirebaseAuth.DefaultInstance;
            Debug.Log("Firebase ready");
        }
        else
        {
            Debug.LogError($"Firebase deps: {status}");
        }
    }
    void InitGooglePlayGames()
    {
#if UNITY_ANDROID
        try
        {
            //NẾU CLASS NÀY BỊ LỖI LÀ DO BẠN CHƯA CHUYỂN PLAFROM VỀ ANDROID NHÉ
            // Cấu hình V2 SDK
            PlayGamesPlatform.DebugLogEnabled = true; // Bật log giúp dễ dàng gỡ lỗi
            PlayGamesPlatform.Activate(); // Kích hoạt Google Play Games

            Debug.Log("PlayGamesPlatform V2 ready");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Play Games init failed: {ex}");
        }
#else
        Debug.Log("Google Play Games is only initialized on Android.");
#endif
    }

    // Đăng nhập Google Play Games
    public void SignInWithGoogle()
    {
        if (bypassGooglePlayGamesLoginForTesting)
        {
            SignInWithLocalTestAccount();
            return;
        }

#if UNITY_EDITOR
        if (Application.isEditor)
        {
            //const string editorFirebaseUid = "Zsl7I0zlF7Te2bPzZi1cMhVdL9m1";
            //const string editorFirebaseUid = "XN0nBTCDW7eKq4d9RxvEtMA0Kgs2";// gao
            //const string editorFirebaseUid = "9nqVOGAyQrRMV7T56xVLs7b43Ol1"; // nga
            const string editorFirebaseUid = "Zsl7I0zlF7Te2bPzZi1cMhVdL9m1"; // quý
             //const string editorFirebaseUid = "HzAtFxnXLkhR3VuNWFrtThcANTj1"; // phú
            Debug.Log("UNITY_EDITOR detected - simulating Google sign-in flow.");
            CurrentAvatarUrl = null;
            _ = NotifyBackendLoginAsync(editorFirebaseUid, null, AuthenticationProviderType.GooglePlayGames, CurrentAvatarUrl);
            return;
        }
#endif

        if (_auth == null)
        {
            Debug.LogError("Firebase chưa khởi tạo!");
            HandleLoginFailure();
            return;
        }

        _ = SignInWithGoogleInternalAsync();
    }

    private void SignInWithLocalTestAccount()
    {
        string localUid = GetStableLocalTestUid();
        Debug.Log($"[AUTH TEST] Bypassing Google Play Games login. Using local test uid '{localUid}'.");
        CurrentAvatarUrl = null;
        _lastProviderType = AuthenticationProviderType.Anonymous;
        _ = NotifyBackendLoginAsync(localUid, string.Empty, AuthenticationProviderType.Anonymous, CurrentAvatarUrl);
    }

    private static string GetStableLocalTestUid()
    {
        const string prefsKey = "paper_legends_local_test_uid";
        string uid = PlayerPrefs.GetString(prefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(uid))
        {
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrWhiteSpace(deviceId) || deviceId == SystemInfo.unsupportedIdentifier)
            {
                deviceId = Guid.NewGuid().ToString("N");
            }

            uid = $"local-test-{deviceId}";
            PlayerPrefs.SetString(prefsKey, uid);
            PlayerPrefs.Save();
        }

        return uid;
    }

    public void HandleSocialLogin(AuthenticationProviderType providerType)
    {
        _lastProviderType = providerType;
        SetLoadingScreenVisible(true);
        switch (providerType)
        {
            case AuthenticationProviderType.GooglePlayGames:
            case AuthenticationProviderType.Google:
                SignInWithGoogle();
                break;
            default:
                Debug.LogWarning($"Provider '{providerType}' is not supported yet.");
                SetLoadingScreenVisible(false);
                break;
        }
    }

    private void SetLoadingScreenVisible(bool isVisible)
    {
        if (LoadingManager.Instance?.UILoadingScreenPrefab != null)
        {
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(isVisible);
        }
    }

    private async Task SignInWithGoogleInternalAsync()
    {
#if UNITY_ANDROID
        try
        {
            var status = await AuthenticateWithGoogleAsync();
            if (status != SignInStatus.Success)
            {
                Debug.LogError("Google Sign-In FAILED: " + status);
                HandleLoginFailure();
                return;
            }

            Debug.Log("Google Play Games OK!");
            await GetServerAuthCodeAndSignInFirebase(AuthenticationProviderType.GooglePlayGames);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Google sign-in flow failed: {ex}");
            HandleLoginFailure(ex.Message);
        }
#else
        await Task.Yield();
        Debug.LogWarning("Google Play Games sign-in is only supported on Android.");
        HandleLoginFailure("Google Play Games sign-in is only supported on Android.");
#endif
    }

#if UNITY_ANDROID
    private Task<SignInStatus> AuthenticateWithGoogleAsync()
    {
        var tcs = new TaskCompletionSource<SignInStatus>();
        PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
        {
            tcs.TrySetResult(status);
        });
        return tcs.Task;
    }
#endif

    // Hàm mới: Lấy Server Auth Code và Đăng nhập vào Firebase
#if UNITY_ANDROID
    private async Task GetServerAuthCodeAndSignInFirebase(AuthenticationProviderType providerType)
    {
        string serverAuthCode;
        try
        {
            serverAuthCode = await RequestServerAuthCodeAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError("Không thể lấy server auth code: " + ex.Message);
            HandleLoginFailure(ex.Message);
            return;
        }

        try
        {
            var cred = PlayGamesAuthProvider.GetCredential(serverAuthCode);
            var user = await FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(cred);

            Debug.Log($"Firebase OK! UID: {user.UserId}, Name: {user.DisplayName}");

            var firebaseUid = user.UserId;
            var email = user.Email;

            Debug.Log($"Email: {email ?? "(null)"}");

            _currentFirebaseUser = user;
            ProcessFirebaseUserProfile(user);
            await NotifyBackendLoginAsync(firebaseUid, email, providerType, CurrentAvatarUrl);
        }
        catch (Exception ex)
        {
            Debug.LogError("Firebase sign-in failed: " + ex);
            HandleLoginFailure(ex.Message);
        }
    }


#endif

    public void RefreshCurrentUserAvatar()
    {
        try
        {
            var currentUser = FirebaseAuth.DefaultInstance?.CurrentUser;
            if (currentUser == null)
            {
                return;
            }

            ProcessFirebaseUserProfile(currentUser);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Unable to refresh current user avatar: {ex.Message}");
        }
    }

#if UNITY_ANDROID
    private Task<string> RequestServerAuthCodeAsync()
    {
        var tcs = new TaskCompletionSource<string>();
        PlayGamesPlatform.Instance.RequestServerSideAccess(true, serverAuthCode =>
        {
            if (string.IsNullOrEmpty(serverAuthCode))
            {
                tcs.TrySetException(new InvalidOperationException("Server Auth Code null"));
            }
            else
            {
                tcs.TrySetResult(serverAuthCode);
            }
        });
        return tcs.Task;
    }
#endif

    private async Task NotifyBackendLoginAsync(string firebaseUid, string email, AuthenticationProviderType providerType, string avatarUrl)
    {
        if (string.IsNullOrEmpty(firebaseUid))
        {
            Debug.LogWarning("Firebase UID is empty; skip backend login");
            MenuController.Instance?.HandleLoginFailure();
            SetLoadingScreenVisible(false);
            return;
        }

        if (APIManager.Instance == null)
        {
            Debug.LogWarning("APIManager.Instance is not initialized");
            MenuController.Instance?.HandleLoginFailure();
            SetLoadingScreenVisible(false);
            return;
        }

        try
        {
            var apiManager = APIManager.Instance;
            var result = await apiManager.LoginOrCreateSocialAccount(firebaseUid, email, providerType, avatarUrl);
            if (result != null)
            {
                MenuController.Instance.OnLoginComplete(result);
            }
            else
            {
                Debug.LogWarning("Backend did not return a login result");
                MenuController.Instance?.HandleLoginFailure(apiManager.LastErrorMessage, apiManager.LastErrorCode);
                SetLoadingScreenVisible(false);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Backend social login failed: " + ex.Message);
            MenuController.Instance?.HandleLoginFailure(ex.Message);
            SetLoadingScreenVisible(false);
        }
    }


    private void ProcessFirebaseUserProfile(FirebaseUser user)
    {
        if (user == null)
        {
            return;
        }

        _currentFirebaseUser = user;

        var avatarUrl = user.PhotoUrl?.ToString();
        CurrentAvatarUrl = avatarUrl;
        if (string.IsNullOrEmpty(avatarUrl))
        {
            Debug.Log("Firebase user does not provide a photo URL.");
            MenuController.Instance?.ClearPlayerAvatar();
            return;
        }

        var avatarService = AvatarService.EnsureInstance();
        avatarService.LoadAvatar(new AvatarService.AvatarRequest(_lastProviderType, avatarUrl, user.UserId),
            texture => MenuController.Instance?.SetPlayerAvatarTexture(texture),
            error => Debug.LogWarning($"Failed to load avatar from '{avatarUrl}': {error}"));
    }

    private void HandleLoginFailure(string errorMessage = null, long errorCode = 0)
    {
        MenuController.Instance?.HandleLoginFailure(errorMessage, errorCode);
        SetLoadingScreenVisible(false);
    }

    public async Task<Texture2D> DownloadPlayerAvatarAsync(string firebaseGuid, CancellationToken cancellationToken = default, string relativeFolder = "avatars")
    {
        if (string.IsNullOrWhiteSpace(firebaseGuid))
        {
            return null;
        }

        var app = FirebaseApp.DefaultInstance ?? _auth?.App;
        var bucket = app?.Options?.StorageBucket;
        if (string.IsNullOrEmpty(bucket))
        {
            Debug.LogWarning("[Firebase] Storage bucket is not configured. Unable to download player avatar.");
            return null;
        }

        foreach (var path in BuildAvatarPathCandidates(firebaseGuid, relativeFolder))
        {
            var url = BuildFirebaseStorageUrl(bucket, path);

            try
            {
                var texture = await DownloadTextureAsync(url, cancellationToken);
                if (texture != null)
                {
                    return texture;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Firebase] Failed to download avatar from '{url}': {ex.Message}");
            }
        }

        Debug.LogWarning($"[Firebase] Unable to locate avatar for guid '{firebaseGuid}'.");
        return null;
    }

    private static IEnumerable<string> BuildAvatarPathCandidates(string firebaseGuid, string relativeFolder)
    {
        var candidates = new List<string>();
        if (string.IsNullOrWhiteSpace(firebaseGuid))
        {
            return candidates;
        }

        string sanitized = firebaseGuid.Trim();
        sanitized = sanitized.TrimStart('/');

        bool hasExtension = sanitized.Contains('.');
        bool hasDirectory = sanitized.Contains('/');

        if (hasDirectory)
        {
            candidates.Add(sanitized);

            if (!hasExtension)
            {
                candidates.Add($"{sanitized}.png");
                candidates.Add($"{sanitized}.jpg");
                candidates.Add($"{sanitized}.jpeg");
            }
        }
        else
        {
            string basePath = sanitized;
            if (!string.IsNullOrEmpty(relativeFolder))
            {
                basePath = $"{relativeFolder.TrimEnd('/')}/{basePath}";
            }

            if (hasExtension)
            {
                candidates.Add(basePath);
            }
            else
            {
                candidates.Add($"{basePath}.png");
                candidates.Add($"{basePath}.jpg");
                candidates.Add($"{basePath}.jpeg");
                candidates.Add(basePath);
            }
        }

        return candidates;
    }

    private static string BuildFirebaseStorageUrl(string bucket, string objectPath)
    {
        var escapedPath = Uri.EscapeDataString(objectPath);
        return $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{escapedPath}?alt=media";
    }

    private static async Task<Texture2D> DownloadTextureAsync(string url, CancellationToken cancellationToken)
    {
        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                await Task.Yield();
            }

#if UNITY_2020_1_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                return null;
            }

            return DownloadHandlerTexture.GetContent(request);
        }
    }

    public void ShowLoggedInSocialUserInfo()
    {
        if (PopupHelper.Instance == null)
        {
            Debug.LogWarning("PopupHelper is not available to show social login info.");
            return;
        }

        var networkManager = GameManagerNetWork.Instance;
        var loginModel = networkManager != null ? networkManager.loginUserModel : null;

        if (loginModel == null || loginModel.UserId <= 0)
        {
            Debug.LogWarning("No authenticated user available for displaying social login info.");
            return;
        }

        var providerType = ResolveProviderType(loginModel?.ProviderType);
        if (providerType != AuthenticationProviderType.GooglePlayGames && providerType != AuthenticationProviderType.Google)
        {
            Debug.LogWarning($"Provider '{providerType}' is not supported for the social info popup yet.");
            return;
        }

        string displayName = !string.IsNullOrWhiteSpace(loginModel.Username)
            ? loginModel.Username
            : _currentFirebaseUser?.DisplayName ?? FirebaseAuth.DefaultInstance?.CurrentUser?.DisplayName ?? string.Empty;

        string friendCode = loginModel.FriendCode;

        string email = !string.IsNullOrWhiteSpace(loginModel.Email)
            ? loginModel.Email
            : _currentFirebaseUser?.Email ?? FirebaseAuth.DefaultInstance?.CurrentUser?.Email ?? string.Empty;

        var popupInfo = new SocialLoginInfoData(displayName, friendCode, email, providerType);
        PopupHelper.Instance.ShowSocialLoginInfo(popupInfo, SignOutSocialAccount);
    }

    public void SignOutSocialAccount()
    {
        try
        {
            FirebaseAuth.DefaultInstance.SignOut();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to sign out from Google Play Games: {ex.Message}");
        }

        try
        {
            _auth?.SignOut();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to sign out from Firebase: {ex.Message}");
        }

        _currentFirebaseUser = null;
        _lastProviderType = AuthenticationProviderType.Anonymous;
        CurrentAvatarUrl = null;

        WebSocketHelper.Instance?.Disconnect();

        if (GameManagerNetWork.Instance != null)
        {
            GameManagerNetWork.Instance.loginUserModel = new LoginUserModel();
        }

        MenuController.Instance?.ClearPlayerAvatar();
        MenuController.Instance?.ShowLoginPanelAfterLogout();
        SoundManager.Instance?.StopBackGroundSound();
        AvatarService.Instance?.ClearCache();

        SetLoadingScreenVisible(false);

        Debug.Log("User signed out from social account.");
    }

    private AuthenticationProviderType ResolveProviderType(string providerTypeString)
    {
        if (!string.IsNullOrWhiteSpace(providerTypeString) &&
            Enum.TryParse(providerTypeString, true, out AuthenticationProviderType parsed))
        {
            return parsed;
        }

        return _lastProviderType;
    }

//private async Task InitUgs()
    //{
    //    try
    //    {
    //        await UnityServices.InitializeAsync();
    //        Debug.Log("UGS initialized");
    //    }
    //    catch (Exception e)
    //    {
    //        Debug.LogError("UGS init failed: " + e);
    //    }
    //}
    ///// <summary>
    ///// Hàm đăng nhập Google thông qua kênh UPA để test không dùng đến nữa
    ///// </summary>
    //public async void SignInWithGoogleViaUPA()
    //{
    //    try
    //    {
    //        // 1) Mở flow UPA (Google nằm trong UPA)
    //        await PlayerAccountService.Instance.StartSignInAsync();

    //        // 2) Lấy UPA access token
    //        var upaToken = PlayerAccountService.Instance.AccessToken; // string

    //        // 3) Đăng nhập UGS bằng token UPA
    //        await AuthenticationService.Instance.SignInWithGoogleAsync(upaToken);

    //        string playerId = AuthenticationService.Instance.PlayerId;
    //        string ugsAccessToken = AuthenticationService.Instance.AccessToken; // property, không phải async

    //        Debug.Log($"UGS signed in. playerId={playerId}..., token len={ugsAccessToken?.Length}");

    //        // 4) Gọi backend đổi UGS access token -> Firebase custom token
    //        if (APIManager.Instance == null)
    //        {
    //            throw new InvalidOperationException("APIManager.Instance is not initialized");
    //        }

    //        string customToken = await APIManager.Instance.ExchangeUgsForFirebaseCustomTokenAsync(ugsAccessToken);

    //        // 5) Sign-in Firebase bằng custom token
    //        var result = await _auth.SignInWithCustomTokenAsync(customToken);
    //        Debug.Log($"Firebase sign-in OK: {result.User.UserId} / {result.User.DisplayName}");
    //    }
    //    catch (RequestFailedException rfe)
    //    {
    //        Debug.LogError($"UGS sign-in failed: {rfe.ErrorCode} - {rfe.Message}");
    //    }
    //    catch (Exception e)
    //    {
    //        Debug.LogError("SignIn error: " + e);
    //    }
    //}

    //public void SignOutAll()
    //{
    //    try
    //    {
    //        if (AuthenticationService.Instance.IsSignedIn)
    //            AuthenticationService.Instance.SignOut(); // <-- đúng API

    //        // Đăng xuất khỏi UPA (để lần sau không tự nhớ phiên UPA)
    //        PlayerAccountService.Instance.SignOut();
    //    }
    //    catch { /* ignore */ }

    //    _auth?.SignOut();
    //    Debug.Log("Signed out UGS + UPA + Firebase");
    //}

}
