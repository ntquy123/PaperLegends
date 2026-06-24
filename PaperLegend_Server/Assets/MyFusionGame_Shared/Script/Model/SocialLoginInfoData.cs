using System;

/// <summary>
/// Represents the information displayed inside the social login popup.
/// </summary>
[Serializable]
public readonly struct SocialLoginInfoData
{
    public SocialLoginInfoData(string displayName, string friendCode, string email, AuthenticationProviderType providerType)
    {
        DisplayName = displayName;
        FriendCode = friendCode;
        Email = email;
        ProviderType = providerType;
    }

    public string DisplayName { get; }
    public string FriendCode { get; }
    public string Email { get; }
    public AuthenticationProviderType ProviderType { get; }
}
