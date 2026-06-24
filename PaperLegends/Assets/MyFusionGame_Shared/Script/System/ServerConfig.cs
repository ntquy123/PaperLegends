using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ServerConfig", menuName = "Config/Server Config")]
public class ServerConfig : ScriptableObject
{
    public string baseUrlPhoton = "https://example.com";
    public string baseUrl = "https://example.com";
    public string webSocketUrl = "ws://example.com:5001";
    public string baseUrlLocal = "http://localhost:5000/api";
    public string baseUrlPhotonCloud = "https://example.com";
    public string catalogUrl = "https://example.com/addressables/catalog.hash";
    [Header("Android HTTPS")]
    public bool usePinnedHttpsCertificates = false;
    public List<string> httpsCertificatePins = new List<string>();
}
