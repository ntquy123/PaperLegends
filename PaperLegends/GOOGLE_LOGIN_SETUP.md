# Paper Legends - Hướng Dẫn Cấu Hình Đăng Nhập Google

Tài liệu này hướng dẫn cấu hình đăng nhập Google cho bản Android Unity của `Paper Legends` bằng Firebase Authentication, Google Cloud OAuth và Google Play Games Services.

Mục tiêu cuối cùng:

- Unity client đăng nhập được Google Play Games.
- Lấy được `server auth code` từ Google Play Games.
- Dùng auth code đó đăng nhập Firebase bằng `PlayGamesAuthProvider`.
- Firebase tạo hoặc nhận diện tài khoản người chơi.

Package name đang dùng trong project:

```text
com.gamenhalam.paperlegends
```

## Tổng Quan Luồng Đăng Nhập

Luồng chuẩn gồm 4 bước:

1. Firebase sẵn sàng.
2. Google Play Games đăng nhập thành công.
3. Unity lấy `server auth code`.
4. Firebase dùng auth code để đăng nhập.

```text
Unity
|-- FirebaseApp.CheckAndFixDependenciesAsync()
|-- PlayGamesPlatform.Activate()
|-- PlayGamesPlatform.Instance.Authenticate()
|-- PlayGamesPlatform.Instance.GetServerAuthCode()
`-- FirebaseAuth.SignInAndRetrieveDataWithCredentialAsync()
```

## Giai Đoạn 1 - Khởi Tạo Firebase Và Google Cloud

### 1. Tạo Dự Án Firebase

Truy cập Firebase Console:

```text
https://console.firebase.google.com/
```

Tạo một project Firebase mới hoàn toàn cho `Paper Legends`.

Ở màn hình chính của project Firebase:

1. Click biểu tượng `Unity` để thêm ứng dụng.
2. Tick nền tảng `Android`.
3. Điền package name:

```text
com.gamenhalam.paperlegends
```

4. Tải file:

```text
google-services.json
```

5. Đặt file vào thư mục:

```text
PaperLegends/Assets/google-services.json
```

Lưu ý: `google-services.json` phải nằm trực tiếp trong `Assets`, không đặt trong thư mục con.

### 2. Khai Báo SHA-1

SHA-1 là phần rất quan trọng. Nếu thiếu hoặc sai SHA-1, đăng nhập Google Play Games có thể thành công nhưng Firebase không xác thực đúng.

Có thể lấy SHA-1 từ file APK test:

```bat
keytool -printcert -jarfile "E:\my project\PaperLegendsProject\apkfile\test_v1.apk"
```

Hoặc lấy từ keystore:

```bat
keytool -list -v -keystore "E:\path\to\user.keystore" -alias your_alias_name
```

Sau khi có SHA-1:

1. Vào `Firebase Console`.
2. Mở `Project Settings`.
3. Cuộn xuống phần app Android.
4. Bấm `Add fingerprint`.
5. Dán SHA-1 vào.
6. Save.

Thao tác này giúp Firebase tạo đúng Android OAuth Client ID ngầm cho app Android.

### 3. Cấu Hình OAuth Consent Screen

Truy cập Google Cloud Console:

```text
https://console.cloud.google.com/
```

Đảm bảo đang chọn đúng project Google Cloud được liên kết với Firebase vừa tạo.

Đi tới:

```text
APIs & Services > OAuth consent screen
```

Cấu hình:

- User Type: `External`
- App name: tên game, ví dụ `Paper Legends`
- User support email: email hỗ trợ
- Developer contact email: email nhà phát triển

Sau đó bấm `Save and Continue` qua các bước còn lại.

### 4. Lấy Web Client ID Và Client Secret

Trong `Firebase Console`:

1. Vào `Authentication`.
2. Vào tab `Sign-in method`.
3. Bật thử provider `Google`.
4. Bấm `Save`.
5. Có thể disable lại nếu chưa dùng trực tiếp provider Google.

Mục đích là ép hệ thống tự tạo OAuth Web Client.

Sau đó qua `Google Cloud Console`:

```text
APIs & Services > Credentials
```

Bấm F5 để tải lại trang.

Trong phần `OAuth 2.0 Client IDs`, tìm dòng:

```text
Web client (auto created by Google Service)
```

Bấm nút Edit hình cây bút chì, copy:

- `Client ID`
- `Client secret`

Quan trọng: đây phải là `Web client`, không phải `Android client`.

### 5. Bật Play Games Provider Trên Firebase

Quay lại:

```text
Firebase Console > Authentication > Sign-in method
```

Chọn provider:

```text
Play Games
```

Bật `Enable`.

Dán:

- `Client ID`: Web Client ID vừa copy.
- `Client secret`: Web Client Secret vừa copy.

Bấm `Save`.

## Giai Đoạn 2 - Cấu Hình Google Play Console

### 1. Tạo Play Games Services

Vào Google Play Console:

```text
https://play.google.com/console/
```

Chọn game `Paper Legends`.

Đi tới:

```text
Play Games Services > Setup and management > Configuration
```

Khi được hỏi game đã dùng Google APIs chưa, chọn:

```text
Yes, my game already uses Google APIs
```

Liên kết với đúng project Google Cloud/Firebase đã tạo ở Giai đoạn 1.

### 2. Thêm Credentials Cho Android

Trong trang cấu hình Play Games Services, cuộn tới phần:

```text
Credentials
```

Bấm:

```text
Add credential
```

Chọn:

- Type: `Android`
- OAuth Client ID: chọn đúng Android OAuth Client ID được tạo từ package name + SHA-1.

Nếu không thấy Android OAuth Client ID đúng, kiểm tra lại:

- Package name có đúng `com.gamenhalam.paperlegends` không.
- SHA-1 đã add trong Firebase chưa.
- Google Cloud Console đã refresh chưa.

### 3. Thêm Testers

Đi tới mục:

```text
Testers
```

Add email Google của các tài khoản sẽ test game.

Tài khoản không nằm trong danh sách tester thường sẽ không đăng nhập Play Games được ở giai đoạn chưa public.

### 4. Lấy Resources XML

Quay lại tab:

```text
Configuration
```

Góc trên bên phải bấm:

```text
Get resources
```

Copy toàn bộ XML:

```xml
<?xml version="1.0" encoding="utf-8"?>
<resources>
    ...
</resources>
```

Lưu ý dòng:

```xml
<string name="app_id">...</string>
```

`app_id` này phải khớp với project Play Games Services đang cấu hình.

## Giai Đoạn 3 - Kết Nối Plugin Trong Unity

### 1. Import Firebase SDK

Import các package cần thiết vào Unity:

- `FirebaseApp`
- `FirebaseAuthentication`
- `FirebaseDatabase` hoặc `FirebaseFirestore` nếu game cần lưu dữ liệu online

Tối thiểu để đăng nhập Firebase cần:

```text
FirebaseAuthentication.unitypackage
```

### 2. Import Google Play Games Plugin

Tải Google Play Games plugin dành cho Unity từ GitHub chính thức của Google:

```text
https://github.com/playgameservices/play-games-plugin-for-unity
```

Import package vào project Unity.

### 3. Setup Android Trong Unity

Trong Unity, mở menu:

```text
Window > Google Play Games > Setup > Android Setup
```

Điền:

#### Resources Definition

Dán toàn bộ XML lấy từ Play Console.

#### Web App Client ID

Dán đúng `Web Client ID`.

Lưu ý:

- Không dán Android Client ID vào ô này.
- Web Client ID thường có dạng:

```text
xxxxxxxxxxxx-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.apps.googleusercontent.com
```

- Phần số đầu thường phải khớp với `app_id` trong XML resources.

Bấm:

```text
Setup
```

Nếu thành công, plugin sẽ tạo file:

```text
GPGSIds.cs
```

## Giai Đoạn 4 - Viết Code Logic C#

Tạo một script quản lý đăng nhập, ví dụ:

```text
Assets/Script/System/GoogleLoginManager.cs
```

Luồng code nên chia rõ 4 bước:

1. Khởi tạo Firebase.
2. Activate Play Games.
3. Đăng nhập Play Games.
4. Đăng nhập Firebase bằng auth code.

Ví dụ khung code:

```csharp
using Firebase;
using Firebase.Auth;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;

public sealed class GoogleLoginManager : MonoBehaviour
{
    private FirebaseAuth auth;

    private async void Start()
    {
        await InitializeFirebase();
        InitializePlayGames();
    }

    private async System.Threading.Tasks.Task InitializeFirebase()
    {
        DependencyStatus status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status != DependencyStatus.Available)
        {
            Debug.LogError($"Firebase dependencies are not available: {status}");
            return;
        }

        auth = FirebaseAuth.DefaultInstance;
        Debug.Log("Firebase is ready.");
    }

    private void InitializePlayGames()
    {
        PlayGamesPlatform.Activate();
        Debug.Log("Google Play Games activated.");
    }

    public void Login()
    {
        if (auth == null)
        {
            Debug.LogError("FirebaseAuth is not ready.");
            return;
        }

        PlayGamesPlatform.Instance.Authenticate(status =>
        {
            if (status != SignInStatus.Success)
            {
                Debug.LogError($"Play Games login failed: {status}");
                return;
            }

            string authCode = PlayGamesPlatform.Instance.GetServerAuthCode();
            if (string.IsNullOrEmpty(authCode))
            {
                Debug.LogError("Server auth code is empty.");
                return;
            }

            LoginFirebaseWithPlayGames(authCode);
        });
    }

    private async void LoginFirebaseWithPlayGames(string authCode)
    {
        Credential credential = PlayGamesAuthProvider.GetCredential(authCode);
        AuthResult result = await auth.SignInAndRetrieveDataWithCredentialAsync(credential);

        FirebaseUser user = result.User;
        Debug.Log($"Firebase login success. uid={user.UserId}, name={user.DisplayName}");
    }
}
```

## Checklist Trước Khi Build APK Test

Kiểm tra các mục sau trước khi build:

- `google-services.json` nằm trong `PaperLegends/Assets`.
- Package name Unity đúng `com.gamenhalam.paperlegends`.
- SHA-1 của APK/keystore đã add trong Firebase.
- OAuth Consent Screen đã cấu hình.
- Firebase Authentication đã bật provider `Play Games`.
- Play Games provider dùng đúng `Web Client ID` và `Client secret`.
- Google Play Console đã link đúng Google Cloud project.
- Play Games Services có Android credential đúng.
- Email tester đã được add.
- Unity GPGS Android Setup đã chạy thành công.
- `GPGSIds.cs` đã được tạo.

## Lỗi Thường Gặp

### Đăng nhập Play Games không hiện gì

Kiểm tra:

- Email test đã nằm trong danh sách testers chưa.
- Play Games Services đã publish configuration hoặc save draft chưa.
- App đang dùng đúng package name chưa.

### Firebase báo lỗi credential

Kiểm tra:

- Firebase provider `Play Games` đã bật chưa.
- Dán đúng `Web Client ID`, không phải Android Client ID.
- Dán đúng `Client secret`.
- SHA-1 đã add đúng chưa.

### `GetServerAuthCode()` trả về rỗng

Kiểm tra:

- Unity GPGS Android Setup có dùng đúng `Web App Client ID` không.
- XML resources có đúng app id của Play Games Services không.
- Thiết bị test đang đăng nhập Google Play Games bằng email tester không.

### Đăng nhập chạy trên máy này nhưng fail trên máy khác

Kiểm tra:

- Email máy kia đã add vào testers chưa.
- APK đó được ký bằng đúng keystore có SHA-1 đã khai báo chưa.
- Nếu đổi keystore hoặc build bằng key khác, cần add thêm SHA-1 mới vào Firebase.

## Ghi Chú Bảo Mật

- Không hard-code `Client secret` trong code Unity client.
- `Client secret` chỉ nên nhập trong Firebase Console hoặc backend/server nếu thật sự cần.
- Unity client chỉ cần `Web Client ID` để cấu hình Google Play Games plugin.
- Nếu dùng backend riêng, Firebase UID nên được server verify bằng Firebase Admin SDK trước khi cấp session game.
