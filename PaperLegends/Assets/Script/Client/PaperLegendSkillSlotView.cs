using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PaperLegendSkillSlotView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    private const int BallSkillIdMin = 11400000;
    private const int BallSkillIdMaxExclusive = 11500000;

    [Header("Skill")]
    [SerializeField] private Button skillButton;
    [SerializeField] private Image skillIconImage;
    [SerializeField] private GameObject unavailableOverlay;
    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private TMP_Text levelText;

    [Header("Upgrade")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private CanvasGroup upgradeButtonCanvasGroup;
    [SerializeField, Min(0.01f)] private float upgradeShowScale = 1.18f;
    [SerializeField, Min(0.01f)] private float upgradePulseSeconds = 0.55f;
    [SerializeField, Min(0.01f)] private float upgradeClickSeconds = 0.16f;

    private Action<int> _onSkillClicked;
    private Action<int> _onUpgradeClicked;
    private int _slot;
    private int _skillId;
    private int _loadVersion;
    private bool _upgradeVisible;
    private bool _targetedSkillPointerActive;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
    }

    private void OnDisable()
    {
        if (_targetedSkillPointerActive)
        {
            _targetedSkillPointerActive = false;
            PaperLegendFlickInputCollector.EndTargetedSkillUse(Vector2.zero, canceled: true);
        }

        transform.DOKill(true);
        if (skillIconImage != null)
            skillIconImage.DOKill(true);
        if (upgradeButton != null)
            upgradeButton.transform.DOKill(true);
        if (upgradeButtonCanvasGroup != null)
            upgradeButtonCanvasGroup.DOKill(true);
    }

    public void Configure(
        int slot,
        int skillId,
        string skillName,
        int level,
        int maxLevel,
        bool canUse,
        bool canUpgrade,
        Action<int> onSkillClicked,
        Action<int> onUpgradeClicked,
        bool isPassive = false)
    {
        ResolveReferences();
        BindButtons();

        _slot = Mathf.Clamp(slot, 1, 4);
        _onSkillClicked = onSkillClicked;
        _onUpgradeClicked = onUpgradeClicked;

        if (skillNameText != null)
            skillNameText.text = string.IsNullOrWhiteSpace(skillName) ? $"Skill {_slot}" : skillName;

        if (levelText != null)
            levelText.text = $"{Mathf.Max(0, level)}/{Mathf.Max(1, maxLevel)}";

        bool skillInteractable = canUse && !isPassive;

        if (unavailableOverlay != null)
            unavailableOverlay.SetActive(!canUse && !isPassive);

        if (skillButton != null)
            skillButton.interactable = skillInteractable;

        if (_skillId != skillId)
        {
            _skillId = skillId;
            StartCoroutine(LoadSkillIconRoutine(skillId, ++_loadVersion));
        }

        SetUpgradeVisible(canUpgrade, true);
    }

    public void Hide()
    {
        SetUpgradeVisible(false, false);
        gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (skillButton == null)
            skillButton = GetComponent<Button>();

        if (skillIconImage == null)
        {
            Transform icon = transform.Find("SkillIcon") ?? transform.Find("Icon") ?? transform.Find("Image");
            if (icon != null)
                skillIconImage = icon.GetComponent<Image>();
            if (skillIconImage == null)
                skillIconImage = GetComponent<Image>();
        }

        if (upgradeButton == null)
        {
            Transform upgrade = transform.Find("UpgradeButton")
                ?? transform.Find("LevelUpButton")
                ?? transform.Find("PlusButton")
                ?? transform.Find("ButtonPlus");
            if (upgrade != null)
                upgradeButton = upgrade.GetComponent<Button>();
        }

        if (upgradeButton != null && upgradeButtonCanvasGroup == null)
        {
            upgradeButtonCanvasGroup = upgradeButton.GetComponent<CanvasGroup>();
            if (upgradeButtonCanvasGroup == null)
                upgradeButtonCanvasGroup = upgradeButton.gameObject.AddComponent<CanvasGroup>();
        }

        if (unavailableOverlay == null)
        {
            Transform overlay = transform.Find("UnavailableOverlay")
                ?? transform.Find("NotUseImage")
                ?? transform.Find("LockedOverlay");
            if (overlay != null)
                unavailableOverlay = overlay.gameObject;
        }

        if (levelText == null)
        {
            Transform label = transform.Find("LevelText") ?? transform.Find("Level") ?? transform.Find("Charges");
            if (label != null)
                levelText = label.GetComponent<TMP_Text>();
        }

        if (skillNameText == null)
        {
            Transform label = transform.Find("SkillNameText") ?? transform.Find("NameText") ?? transform.Find("Name");
            if (label != null)
                skillNameText = label.GetComponent<TMP_Text>();
        }
    }

    private void BindButtons()
    {
        if (skillButton != null)
        {
            skillButton.onClick.RemoveListener(HandleSkillClicked);
            skillButton.onClick.AddListener(HandleSkillClicked);
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(HandleUpgradeClicked);
            upgradeButton.onClick.AddListener(HandleUpgradeClicked);
        }
    }

    private void HandleSkillClicked()
    {
        if (IsTargetedSkill())
            return;

        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * 0.08f, 0.18f, 8, 0.65f);
        _onSkillClicked?.Invoke(_slot);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsTargetedSkill() || skillButton == null || !skillButton.interactable)
            return;

        _targetedSkillPointerActive = true;
        PaperLegendFlickInputCollector.BeginTargetedSkillUse(_slot, _skillId, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_targetedSkillPointerActive || !IsTargetedSkill())
            return;

        PaperLegendFlickInputCollector.UpdateTargetedSkillUse(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_targetedSkillPointerActive || !IsTargetedSkill())
            return;

        _targetedSkillPointerActive = false;
        PaperLegendFlickInputCollector.EndTargetedSkillUse(eventData.position, canceled: false);
    }

    private bool IsTargetedSkill()
    {
        return _skillId == (int)PaperLegendHeroSkillId.Hero10000005ThunderStorm;
    }

    private void HandleUpgradeClicked()
    {
        if (upgradeButton != null)
            upgradeButton.interactable = false;

        if (upgradeButton != null)
        {
            upgradeButton.transform.DOKill();
            upgradeButton.transform
                .DOPunchScale(Vector3.one * 0.18f, upgradeClickSeconds, 8, 0.8f)
                .OnComplete(() => SetUpgradeVisible(false, true));
        }
        else
        {
            SetUpgradeVisible(false, true);
        }

        _onUpgradeClicked?.Invoke(_slot);
    }

    private void SetUpgradeVisible(bool visible, bool animated)
    {
        if (upgradeButton == null)
            return;

        if (visible == _upgradeVisible && upgradeButton.gameObject.activeSelf == visible)
            return;

        _upgradeVisible = visible;
        upgradeButton.gameObject.SetActive(true);
        upgradeButton.interactable = visible;
        upgradeButton.transform.SetAsLastSibling();
        upgradeButton.transform.DOKill();

        if (upgradeButtonCanvasGroup != null)
            upgradeButtonCanvasGroup.DOKill();

        if (!animated)
        {
            upgradeButton.gameObject.SetActive(visible);
            upgradeButton.transform.localScale = Vector3.one;
            if (upgradeButtonCanvasGroup != null)
                upgradeButtonCanvasGroup.alpha = visible ? 1f : 0f;
            return;
        }

        if (visible)
        {
            upgradeButton.transform.localScale = Vector3.one * 0.82f;
            if (upgradeButtonCanvasGroup != null)
                upgradeButtonCanvasGroup.alpha = 0f;

            Sequence sequence = DOTween.Sequence();
            if (upgradeButtonCanvasGroup != null)
                sequence.Join(upgradeButtonCanvasGroup.DOFade(1f, 0.12f));
            sequence.Join(upgradeButton.transform.DOScale(upgradeShowScale, 0.18f).SetEase(Ease.OutBack));
            sequence.Append(upgradeButton.transform.DOScale(1f, 0.1f).SetEase(Ease.OutSine));
            sequence.Append(upgradeButton.transform
                .DOScale(1.08f, upgradePulseSeconds)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo));
        }
        else
        {
            Sequence sequence = DOTween.Sequence();
            if (upgradeButtonCanvasGroup != null)
                sequence.Join(upgradeButtonCanvasGroup.DOFade(0f, 0.12f));
            sequence.Join(upgradeButton.transform.DOScale(0.72f, 0.12f).SetEase(Ease.InBack));
            sequence.OnComplete(() =>
            {
                if (upgradeButton != null)
                    upgradeButton.gameObject.SetActive(false);
            });
        }
    }

    private IEnumerator LoadSkillIconRoutine(int skillId, int version)
    {
        if (skillIconImage == null)
            yield break;

        skillIconImage.enabled = false;
        skillIconImage.DOKill();

        Sprite loadedSprite = null;
        yield return PaperLegendHeroAddressables.LoadSkillIconSpriteRoutine(skillId, sprite => loadedSprite = sprite);
        if (loadedSprite == null)
        {
            string legacyPath = GetLegacySkillIconPath(skillId);
            yield return AddressablesHelper.LoadSprite(legacyPath, sprite => loadedSprite = sprite);
        }

        string primaryPath = GetLegacySkillIconPath(skillId);
        string fallbackPath = IsBallSkillId(skillId)
            ? $"{AddressablePaths.Root}/Skills/{skillId}.png"
            : $"{AddressablePaths.Root}/Skills/Ball/{skillId}.png";

        if (loadedSprite == null && primaryPath != fallbackPath)
            yield return AddressablesHelper.LoadSprite(fallbackPath, sprite => loadedSprite = sprite);

        ApplyLoadedSkillIcon(loadedSprite, version);
    }

    private void ApplyLoadedSkillIcon(Sprite loadedSprite, int version)
    {
        if (version != _loadVersion || skillIconImage == null || loadedSprite == null)
            return;

        skillIconImage.sprite = loadedSprite;
        skillIconImage.color = Color.white;
        skillIconImage.enabled = true;
        skillIconImage.transform.localScale = Vector3.one * 0.92f;
        skillIconImage.transform.DOScale(1f, 0.18f).SetEase(Ease.OutBack);
    }

    private static bool IsBallSkillId(int skillId)
    {
        return skillId >= BallSkillIdMin && skillId < BallSkillIdMaxExclusive;
    }

    private static string GetLegacySkillIconPath(int skillId)
    {
        string skillFolder = IsBallSkillId(skillId) ? "Skills/Ball" : "Skills";
        return $"{AddressablePaths.Root}/{skillFolder}/{skillId}.png";
    }
}
