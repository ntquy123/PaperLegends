using UnityEngine;

[DisallowMultipleComponent]
public sealed class CoreTutorialMapConfig : MonoBehaviour
{
    [Header("Tutorial Area")]
    [SerializeField] private BoxCollider playArea;
    [SerializeField] private GameObject terrainGround;
    [SerializeField] private Transform shotTarget;

    [Header("Player And Ball Spawn")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private Transform cueBallSpawnPoint;
    [SerializeField] private Transform reserveBallSpawnPoint;

    [Header("Step 3 - Shoot Opponent Ball")]
    [SerializeField, Tooltip("Vi tri nhan vat se di chuyen den truoc khi bat dau buoc 3.")]
    private Transform stepThreePlayerSpawnPoint;
    [SerializeField, Tooltip("Vi tri X/Z cua mot vien bi doi thu o buoc 3. Y se tu can theo TerrainGround.")]
    private Transform stepThreeOpponentBallSpawnPoint;
    [SerializeField, Tooltip("Prefab marker 3D chua World Space Canvas, mui ten va text chi bi doi thu.")]
    private GameObject stepThreeTargetMarkerPrefab;
    [SerializeField, Tooltip("Khoang cach marker so voi tam bi doi thu trong toa do the gioi.")]
    private Vector3 stepThreeTargetMarkerOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField, Range(0f, 1f), Tooltip("Muc ho tro keo huong ban ve bi doi thu neu nguoi choi dang ngam gan dung huong.")]
    private float stepThreeAimAssistStrength = 1f;
    [SerializeField, Range(0f, 90f), Tooltip("Goc lech toi da van duoc ho tro ngam trung bi doi thu.")]
    private float stepThreeAimAssistMaxAngle = 70f;

    [Header("Runtime Ring Ball Spawn")]
    [SerializeField, Min(1)] private int ringBallCount = 10;
    [SerializeField, Min(0f)] private float ringBallGroundClearance = 0.005f;

    [Header("Tutorial Shot Tuning")]
    [SerializeField, Min(0.1f), Tooltip("He so luc ban rieng cho tutorial. 2 = luc ban manh gap doi gia tri co ban.")]
    private float playerShotImpulseMultiplier = 2f;

    public BoxCollider PlayArea => playArea;
    public GameObject TerrainGround => terrainGround;
    public Transform ShotTarget => shotTarget;
    public Transform PlayerSpawnPoint => playerSpawnPoint;
    public Transform CueBallSpawnPoint => cueBallSpawnPoint;
    public Transform ReserveBallSpawnPoint => reserveBallSpawnPoint;
    public Transform StepThreePlayerSpawnPoint => stepThreePlayerSpawnPoint;
    public Transform StepThreeOpponentBallSpawnPoint => stepThreeOpponentBallSpawnPoint;
    public GameObject StepThreeTargetMarkerPrefab => stepThreeTargetMarkerPrefab;
    public Vector3 StepThreeTargetMarkerOffset => stepThreeTargetMarkerOffset;
    public float StepThreeAimAssistStrength => Mathf.Clamp01(stepThreeAimAssistStrength);
    public float StepThreeAimAssistMaxAngle => Mathf.Clamp(stepThreeAimAssistMaxAngle, 0f, 90f);
    public int RingBallCount => Mathf.Max(1, ringBallCount);
    public float RingBallGroundClearance => Mathf.Max(0f, ringBallGroundClearance);
    public float PlayerShotImpulseMultiplier => Mathf.Max(0.1f, playerShotImpulseMultiplier);
    public bool TryValidate(out string error)
    {
        if (playArea == null)
        {
            error = "PlayArea is not assigned.";
            return false;
        }

        if (terrainGround == null)
        {
            error = "TerrainGround is not assigned.";
            return false;
        }

        if (shotTarget == null)
        {
            error = "ShotTarget is not assigned.";
            return false;
        }

        if (playerSpawnPoint == null)
        {
            error = "PlayerSpawnPoint is not assigned.";
            return false;
        }

        if (cueBallSpawnPoint == null)
        {
            error = "CueBallSpawnPoint is not assigned.";
            return false;
        }

        if (reserveBallSpawnPoint == null)
        {
            error = "ReserveBallSpawnPoint is not assigned.";
            return false;
        }

        if (stepThreePlayerSpawnPoint == null)
        {
            error = "StepThreePlayerSpawnPoint is not assigned.";
            return false;
        }

        if (stepThreeOpponentBallSpawnPoint == null)
        {
            error = "StepThreeOpponentBallSpawnPoint is not assigned.";
            return false;
        }

        if (stepThreeTargetMarkerPrefab == null)
        {
            error = "StepThreeTargetMarkerPrefab is not assigned.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
