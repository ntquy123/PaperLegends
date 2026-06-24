using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerListItemUI : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text scoreExamText;
    [SerializeField] private TMP_Text turnOrderText;
    [SerializeField] private TMP_Text alwaysExamScoreText;
    [SerializeField] private TMP_Text eliminatedText;
    [SerializeField] private GameObject eliminatedRoot;

    [Header("Avatar")]
    [SerializeField] private RawImage avatarRawImage;
    [SerializeField] private Image avatarImage;

    [Header("Visuals")]
    [SerializeField] private Image comboImage;
    [SerializeField] private GameObject comboRoot;
    [SerializeField] private GameObject fireRoot;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject examUI;
    [SerializeField] private GameObject information;

    [Header("Skill List")]
    [SerializeField] private Transform skillListRoot;

    [Header("Interact")]
    [SerializeField] private Button button;

    public TMP_Text PlayerNameText => playerNameText;
    public TMP_Text LevelText => levelText;
    public TMP_Text ScoreText => scoreText;
    public TMP_Text ScoreExamText => scoreExamText;
    public TMP_Text TurnOrderText => turnOrderText;
    public TMP_Text AlwaysExamScoreText => alwaysExamScoreText;
    public TMP_Text EliminatedText => eliminatedText;
    public GameObject EliminatedRoot => eliminatedRoot;
    public RawImage AvatarRawImage => avatarRawImage;
    public Image AvatarImage => avatarImage;
    public Image ComboImage => comboImage;
    public GameObject ComboRoot => comboRoot != null ? comboRoot : comboImage != null ? comboImage.gameObject : null;
    public GameObject FireRoot => fireRoot;
    public Image BackgroundImage => backgroundImage;
    public Transform SkillListRoot => skillListRoot;
    public Button Button => button;
    public GameObject ExamUI => examUI;
    public GameObject Information => information;
}
