using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemInfoPopup : MonoBehaviour
{
    [Header("CAMERA RENDER")]
    [SerializeField] public GameObject BallViewPanel;
    [SerializeField] public GameObject CateyeModel;
    [SerializeField] public GameObject BallVissualModel;
    [Header("Image")]
    [SerializeField] public RawImage RawImage;
    [Header("TEXT")]
    [SerializeField] public TMP_Text nameText;
    [SerializeField] public TMP_Text descriptionTextForBall;
    [SerializeField] public TMP_Text descriptionTextForItem;
    [SerializeField] public TMP_Text levelText;
    [SerializeField] public TMP_Text statText;
    [SerializeField] public TMP_Text shardRewardText;
    [SerializeField] public TMP_Text rarityText;
    [Header("INFO GROUP")]
    [SerializeField] public GameObject ballInfoGroup;
    [SerializeField] public GameObject itemInfoGroup;
    [SerializeField] public GameObject shardRepairContainer;
    [Header("SLIDER")]
    [SerializeField] public Slider massSlider;
    [SerializeField] public Slider speedSlider;
    [SerializeField] public Slider bounceSlider;
    [SerializeField] public Slider impactSlider;
    [SerializeField] public Slider damageSlider;
    [Header("ACTIVE SKILL")]
    [SerializeField] public Image activeSkillIconImage;
    [SerializeField] public Button activeSkillIconButton;
    [SerializeField] public GameObject activeSkillMiniPopup;
    [SerializeField] public TMP_Text activeSkillDescriptionText;
    [Header("VFX")]
    [SerializeField] public GameObject skillLevel10Vfx;
    [Header("BUTTON TAB 1")]
    [SerializeField] public Button equipButton;
    [SerializeField] public Button unequipButton;
    [SerializeField] public Button soldButton;
    [SerializeField] public Button unsaleButton;
    [SerializeField] public Button dismantleButton;
    [SerializeField] public Button repairButton;
    [SerializeField] public Button closeButton;
 
}
