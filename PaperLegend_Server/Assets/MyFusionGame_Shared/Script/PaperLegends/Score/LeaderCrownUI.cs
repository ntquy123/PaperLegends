using UnityEngine;

[DisallowMultipleComponent]
public class LeaderCrownUI : MonoBehaviour
{
    [SerializeField] private PaperLegendCharacterNetworkHandler character;
    [SerializeField] private GameObject crownRoot;
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.25f, 0f);
    [SerializeField] private bool billboardToCamera = true;

    private Camera _camera;

    private void Awake()
    {
        if (character == null)
            character = GetComponentInParent<PaperLegendCharacterNetworkHandler>();

        if (followTarget == null && character != null)
            followTarget = character.transform;
    }

    private void LateUpdate()
    {
        bool isLeader = character != null
            && GameScoreManager.Instance != null
            && character.PlayerId > 0
            && GameScoreManager.Instance.CurrentLeaderPlayerId == character.PlayerId;

        if (crownRoot != null && crownRoot.activeSelf != isLeader)
            crownRoot.SetActive(isLeader);

        if (!isLeader || crownRoot == null || followTarget == null)
            return;

        crownRoot.transform.position = followTarget.position + worldOffset;

        if (!billboardToCamera)
            return;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera != null)
            crownRoot.transform.rotation = Quaternion.LookRotation(crownRoot.transform.position - _camera.transform.position);
    }
}
