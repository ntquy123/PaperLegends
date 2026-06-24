using Fusion;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class KillReplayManager : NetworkBehaviour
{
    public static KillReplayManager Instance;

    [Header("Cinematic Config")]
    public Cinemachine.CinemachineVirtualCamera replayCamera;
    public GameObject skipButtonPrefab;
    public float slowMotionScale = 0.3f;
    public float replayDuration = 2f;

    private bool isReplaying = false;
    private GameObject skipButtonInstance;

    private void Awake()
    {
        Instance = this;
    }

    public void SkipReplay()
    {
        if (isReplaying)
            StopReplay();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayKillReplay(Vector3 hitPosition, Vector3 shotDirection)
    {
        if (!isReplaying)
            StartCoroutine(ReplayRoutine(hitPosition, shotDirection));
    }

    private IEnumerator ReplayRoutine(Vector3 hitPos, Vector3 dir)
    {
        isReplaying = true;
        Time.timeScale = slowMotionScale;

        if (replayCamera != null)
        {
            replayCamera.Priority = 40;
            Vector3 camPos = hitPos - dir.normalized * 4f + Vector3.up * 2f;
            replayCamera.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(hitPos - camPos));
        }

        if (skipButtonPrefab != null && skipButtonInstance == null)
        {
            skipButtonInstance = Instantiate(skipButtonPrefab);
            var btn = skipButtonInstance.GetComponentInChildren<Button>();
            if (btn != null)
                btn.onClick.AddListener(SkipReplay);
        }

        float t = 0f;
        while (t < replayDuration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        StopReplay();
    }

    private void StopReplay()
    {
        Time.timeScale = 1f;
        if (replayCamera != null)
            replayCamera.Priority = 0;
        if (skipButtonInstance != null)
            Destroy(skipButtonInstance);
        skipButtonInstance = null;
        isReplaying = false;
    }
}
