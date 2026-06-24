using UnityEngine;
using UnityEngine.UI;

public class PlayerRankHistoryPopupUI : MonoBehaviour
{
    [Header("History UI")]
    [SerializeField]
    private Transform historyContent;
    [SerializeField]
    private ScrollRect historyScrollRect;

    public Transform HistoryContent => historyContent;
    public ScrollRect HistoryScrollRect => historyScrollRect;
}
