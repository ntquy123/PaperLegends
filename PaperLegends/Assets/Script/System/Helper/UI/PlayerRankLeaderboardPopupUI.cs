using TMPro;
using UnityEngine;

public class PlayerRankLeaderboardPopupUI : MonoBehaviour
{
    [Header("Leaderboard UI")]
    [SerializeField]
    private Transform leaderboardContent;
    [SerializeField]
    private GameObject leaderboardItemPrefab;
    [SerializeField]
    private TMP_Text remainingPointsText;
    [SerializeField]
    private Sprite goldMedalSprite;
    [SerializeField]
    private Sprite silverMedalSprite;
    [SerializeField]
    private Sprite bronzeMedalSprite;
    [SerializeField]
    private TMP_Text playerRankText;

    public Transform LeaderboardContent => leaderboardContent;
    public GameObject LeaderboardItemPrefab => leaderboardItemPrefab;
    public TMP_Text RemainingPointsText => remainingPointsText;
    public Sprite GoldMedalSprite => goldMedalSprite;
    public Sprite SilverMedalSprite => silverMedalSprite;
    public Sprite BronzeMedalSprite => bronzeMedalSprite;
    public TMP_Text PlayerRankText => playerRankText;
}
