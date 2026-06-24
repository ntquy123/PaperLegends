using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Linq;
using TMPro;
 
public class EffectPlayerController : MonoBehaviour
{
    [Header("GAME PLAY CONFIG")]
    public static EffectPlayerController Instance;
    public TextMeshProUGUI TextDescription;
    public TextMeshProUGUI TextName;
    public TextMeshProUGUI TextLevel;
    public TextMeshProUGUI TextCharges;
    public TextMeshProUGUI TalenPointText;
    public Button UpLevelButton;
    public Button EquipSkillButton;
    public Transform EffectGridContent;
    public GameObject EffectButtonPrefab;
    [Header("STAR CONFIG")]
    public GameObject StarPrefab;
    public List<ButtonEffect> equippedSkillButtons = new List<ButtonEffect>();
    private readonly List<ButtonEffect> skillButtons = new List<ButtonEffect>();
    private readonly Dictionary<ButtonEffect, Color> defaultButtonColors = new Dictionary<ButtonEffect, Color>();
    [SerializeField]
    private List<EffectPlayer> lstEff = new List<EffectPlayer> ();
    public IReadOnlyList<EffectPlayer> Effects => lstEff;
    private int selectedEffectId;
    private int selectedOldEffectId;
    private ButtonEffect selectedEquipSlot;
    private ButtonEffect selectedEffectButton;


    private void Awake()
    {
        Instance = this;
        InitializeEquippedSkillButtons();
    }
 
   
    public IEnumerator GetlistEffect()
    {
        yield return StartCoroutine(GetListEffectCoroutine());
    }

    private IEnumerator GetListEffectCoroutine()
    {
        List<EffectPlayerSchema> schemas = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetEffectPlayersAsync(GameManagerNetWork.Instance.loginUserModel.UserId),
            result => schemas = result));

            lstEff = schemas != null ?
                schemas.Select(x => new EffectPlayer
            {
                ID = x.effectId,
                Level = x.level,
                Name = x.sysMasGeneral.GenName,
                ParentId = x.sysMasGeneral.ParentCode,
                IsPassive = x.isPassive,
                Charges = x.charges,
                Description = x.sysMasGeneral.description,
                TalentPoint = x.player.TalentPoint,
                IsActive = x.IsActive,
                IsEquiped = x.IsEquiped
                }).ToList() : new List<EffectPlayer>();

        var firstEffect = lstEff.FirstOrDefault();
        if (firstEffect != null)
            TalenPointText.text = firstEffect.TalentPoint.ToString();

        //if (EquipSkillButton != null)
        //{
        //    EquipSkillButton.interactable = false;
        //    EquipSkillButton.image.DOFade(0.5f, 0.5f);
        //    EquipSkillButton.onClick.RemoveAllListeners();
        //}

        var equippedEffects = lstEff.Where(x => x.IsEquiped).ToList();

        LoadEquippedSkillButtonVisuals(equippedEffects);
        PopulateEffectGrid();
        RefreshEquippedSkillButtons();
        RestoreSelection();
    }
    void UpdateLevelButtonEffect(int level , ButtonEffect eff )
    {
        if (eff == null || eff.skillButton == null)
            return;


        // Kiểm tra mức độ level và làm sáng ảnh con tương ứng
        for (int i = 1; i <= level; i++)
        {
            Transform child = eff.skillButton.transform.Find(i.ToString());
            if (child != null)
            {
                Image childImage = child.GetComponent<Image>();
                if (childImage != null)
                {
                    childImage.DOFade(1f, 0.5f);  // Làm sáng ảnh con với tên mức hiện tại
                }
            }
        }
    }
    public void OnClickEffect(ButtonEffect buttonEffect)
    {
        if (buttonEffect == null)
            return;

        var modelDetail = lstEff.FirstOrDefault(x => x.ID == (int)buttonEffect.efftype);

        if (modelDetail == null)
            return;

        SelectEffect(buttonEffect, modelDetail);
    }

    //public void OnClickEffect(EffectPlayerType efftype)
    //{
    // var modelDetail =   lstEff.FirstOrDefault(x => x.ID == (int)efftype);
    //    if(modelDetail != null )
    //    {
    //        bool checkParent = false;
    //        if(modelDetail.Description != null)
    //            TextDescription.text = LocalizationManager.Instance.GetText(modelDetail.Description.ToString());
    //        var modelParent = lstEff.FirstOrDefault(x => x.ID == modelDetail.ParentId);
    //        if (modelParent != null)
    //        {
    //            checkParent = modelParent.Level > 0 ? true : false;
    //        }
    //        if (modelDetail.Level < 3 && checkParent)
    //        {
    //            UpLevelButton.interactable = true;
    //            UpLevelButton.image.DOFade(1f, 0.5f);
    //            UpLevelButton.onClick.RemoveAllListeners();
    //            UpLevelButton.onClick.AddListener(() => UnlockSkill(modelDetail.ID));
    //        }
    //        else
    //        {
    //            UpLevelButton.interactable = false;
    //            UpLevelButton.image.DOFade(0.5f, 0.5f);
    //        }

    //        if (EquipSkillButton != null)
    //        {
    //            EquipSkillButton.onClick.RemoveAllListeners();
    //            if (modelDetail.Level > 0 && !modelDetail.IsPassive)
    //            {
    //                EquipSkillButton.interactable = true;
    //                EquipSkillButton.image.DOFade(1f, 0.5f);
    //                EquipSkillButton.onClick.AddListener(() => EquipSkill(modelDetail.ID));
    //            }
    //            else
    //            {
    //                EquipSkillButton.interactable = false;
    //                EquipSkillButton.image.DOFade(0.5f, 0.5f);
    //            }
    //        }

    //    }
    //}
    public void UnlockSkill(int ID)
    {
        StartCoroutine(UnlockSkillRoutine(ID));
    }

    public void EquipSkill(int newEffectId)
    {
        StartCoroutine(EquipSkillRoutine(selectedOldEffectId, newEffectId));
    }

    private IEnumerator UnlockSkillRoutine(int ID)
    {
        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        LevelUpEffectResponse resp = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.LevelUpEffectPlayer(playerId, ID),
            result => resp = result));

        if (resp == null)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_uplevel_false"), false);
            yield break;
        }

        TalenPointText.text = resp.TalentPoint.ToString();
        NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_uplevel_true"), true);

        var modelCheck = skillButtons.FirstOrDefault(x => (int)x.efftype == ID);
        if (modelCheck != null && modelCheck.skillButton != null)
            modelCheck.skillButton.image.DOFade(1f, 0.5f);

        yield return null;
        yield return GetlistEffect();
        RefreshEquippedSkillButtons();
        //UpdateLevelButtonEffect(modelCheck.Level, modelCheck);


        yield break;
    }

    private IEnumerator EquipSkillRoutine(int oldEffectId, int newEffectId)
    {
        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        EquipEffectResponse resp = null;

        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.EquipEffectPlayer(playerId, oldEffectId, newEffectId),
            result => resp = result));

        if (resp == null)
        {
            NotificationHelper.Instance.ShowNotification("Trang bị kỹ năng thất bại", false);
            yield break;
        }

        NotificationHelper.Instance.ShowNotification("Trang bị kỹ năng thành công", true);
        if (selectedEquipSlot != null)
        {
            selectedEquipSlot.efftype = (EffectPlayerType)newEffectId;
            UpdateEquippedSlotVisuals(selectedEquipSlot, newEffectId);
        }

        selectedOldEffectId = newEffectId;
        selectedEffectId = newEffectId;

        yield return GetlistEffect();
    }

    private void InitializeEquippedSkillButtons()
    {
        foreach (var equipButton in equippedSkillButtons.Where(x => x != null ))
        {
            if (equipButton.skillButton == null)
                equipButton.skillButton = equipButton.GetComponent<Button>();

            CacheDefaultButtonColor(equipButton);
            if (equipButton.LineBanner != null)
                equipButton.LineBanner.SetActive(false);

            if (equipButton.skillButton != null)
            {
                equipButton.skillButton.onClick.RemoveAllListeners();
                equipButton.skillButton.onClick.AddListener(() => SelectOldEffect(equipButton));
            }

            if (selectedOldEffectId == 0)
            {
                selectedOldEffectId = (int)equipButton.efftype;
                selectedEquipSlot = equipButton;
                selectedEffectId = selectedOldEffectId;
            }
        }
    }

    private void SelectOldEffect(ButtonEffect equipButton)
    {
        selectedEquipSlot = equipButton;
        selectedOldEffectId = (int)equipButton.efftype;
        var modelDetail = lstEff.FirstOrDefault(x => x.ID == selectedOldEffectId);
        if (modelDetail != null)
            SelectEffect(equipButton, modelDetail);
    }

    private void PopulateEffectGrid()
    {
        if (EffectGridContent == null || EffectButtonPrefab == null)
            return;

        foreach (Transform child in EffectGridContent)
            Destroy(child.gameObject);

        skillButtons.Clear();
        defaultButtonColors.Clear();
        selectedEffectButton = null;

        foreach (var eff in lstEff.OrderByDescending(x=>x.IsActive).Where(x => !x.IsEquiped && !x.IsPassive))
        {
            var obj = Instantiate(EffectButtonPrefab, EffectGridContent);
            var buttonEffect = obj.GetComponent<ButtonEffect>() ?? obj.AddComponent<ButtonEffect>();
            buttonEffect.efftype = (EffectPlayerType)eff.ID;
            buttonEffect.Level = eff.Level;
            buttonEffect.skillButton = buttonEffect.skillButton ?? obj.GetComponent<Button>();

            CacheDefaultButtonColor(buttonEffect);
            if (buttonEffect.LineBanner != null)
                buttonEffect.LineBanner.SetActive(false);

            SetupEffectButtonVisuals(obj.transform, eff);

            var lockIcon = obj.transform.Find("ImageLock");
            var lockIconImage = lockIcon != null ? lockIcon.GetComponent<Image>() : null;

            if (buttonEffect.skillButton != null)
            {
                buttonEffect.skillButton.onClick.RemoveAllListeners();
                buttonEffect.skillButton.interactable = true;
                buttonEffect.skillButton.onClick.AddListener(() => OnClickEffect(buttonEffect));

            }

            UpdateLevelButtonEffect(eff.Level, buttonEffect);

            if (lockIcon != null)
                lockIcon.gameObject.SetActive(!eff.IsActive);
            if (lockIconImage != null)
                lockIconImage.raycastTarget = false;

            skillButtons.Add(buttonEffect);
        }

        var rect = EffectGridContent.GetComponent<RectTransform>();
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private void SetupEffectButtonVisuals(Transform buttonTransform, EffectPlayer eff)
    {
        var skillImage = buttonTransform.Find("SkillImage")?.GetComponent<Image>();
        if (skillImage != null)
        {
            StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Root}/Skills/{eff.ID}.png", sprite =>
            {
                if (sprite != null)
                    skillImage.sprite = sprite;
            }));
        }
       
        var starContainer = buttonTransform.Find("Stars");
        if (starContainer != null)
        {
            UpdateStarVisuals(starContainer, eff.Level);
        }
    }

    private void LoadEquippedSkillButtonVisuals(List<EffectPlayer> equippedEffects)
    {
        if (equippedEffects == null || equippedEffects.Count == 0)
            return;

        for (int i = 0; i < equippedSkillButtons.Count; i++)
        {
            var equipButton = equippedSkillButtons[i];
            var effectData = equippedEffects.ElementAtOrDefault(i);

            if (equipButton == null || effectData == null)
                continue;

            equipButton.efftype = (EffectPlayerType)effectData.ID;
            equipButton.Level = effectData.Level;

            SetupEffectButtonVisuals(equipButton.transform, effectData);
            UpdateLevelButtonEffect(effectData.Level, equipButton);
        }
    }

    private void UpdateStarVisuals(Transform starContainer, int level)
    {
        if (StarPrefab == null)
            return;

        foreach (Transform child in starContainer)
            Destroy(child.gameObject);

        if (level <= 0)
            return;

        for (int i = 0; i < level; i++)
            Instantiate(StarPrefab, starContainer);
    }

    private void RefreshEquippedSkillButtons()
    {
        var equippedEffects = lstEff.Where(x => x.IsEquiped).ToList();

        for (int i = 0; i < equippedSkillButtons.Count; i++)
        {
            var equipButton = equippedSkillButtons[i];
            if (equipButton == null)
                continue;

            if (equipButton.skillButton == null)
                equipButton.skillButton = equipButton.GetComponent<Button>();

            CacheDefaultButtonColor(equipButton);
            if (equipButton.LineBanner != null)
                equipButton.LineBanner.SetActive(false);

            if (equipButton.skillButton != null)
            {
                equipButton.skillButton.onClick.RemoveAllListeners();
                equipButton.skillButton.onClick.AddListener(() => SelectOldEffect(equipButton));
            }

            if (i < equippedEffects.Count)
            {
                var effectData = equippedEffects[i];
                equipButton.efftype = (EffectPlayerType)effectData.ID;
                equipButton.Level = effectData.Level;

                if (selectedEquipSlot == null)
                {
                    selectedEquipSlot = equipButton;
                    selectedOldEffectId = effectData.ID;
                    selectedEffectId = selectedOldEffectId;
                }

                UpdateEquippedSlotVisuals(equipButton, effectData.ID);
            }
        }
    }

    private void RestoreSelection()
    {
        if (selectedEffectId == 0)
            return;

        var selectedEffect = lstEff.FirstOrDefault(x => x.ID == selectedEffectId);
        if (selectedEffect == null)
            return;

        var button = skillButtons.FirstOrDefault(x => (int)x.efftype == selectedEffectId)
            ?? equippedSkillButtons.FirstOrDefault(x => x != null && (int)x.efftype == selectedEffectId);

        if (button != null)
            SelectEffect(button, selectedEffect);
    }

    private void UpdateEquippedSlotVisuals(ButtonEffect equipButton, int effectId)
    {
        var effectData = lstEff.FirstOrDefault(x => x.ID == effectId);
        if (effectData == null)
            return;

        equipButton.Level = effectData.Level;
        SetupEffectButtonVisuals(equipButton.transform, effectData);
        UpdateLevelButtonEffect(effectData.Level, equipButton);
    }

    private void CacheDefaultButtonColor(ButtonEffect buttonEffect)
    {
        if (buttonEffect == null || buttonEffect.skillButton == null)
            return;

        var image = buttonEffect.skillButton.targetGraphic as Image;
        if (image != null && !defaultButtonColors.ContainsKey(buttonEffect))
            defaultButtonColors[buttonEffect] = image.color;
    }

    private void UpdateSelectedEffectHighlight(ButtonEffect buttonEffect)
    {
        if (buttonEffect == null)
            return;

        if (selectedEffectButton != null)
            SetButtonHighlight(selectedEffectButton, false);

        selectedEffectButton = buttonEffect;
        SetButtonHighlight(selectedEffectButton, true);
    }

    private void SetButtonHighlight(ButtonEffect buttonEffect, bool isHighlighted)
    {
        if (buttonEffect == null || buttonEffect.skillButton == null)
            return;

        var image = buttonEffect.skillButton.targetGraphic as Image;
        if (image == null)
            return;

        if (!defaultButtonColors.TryGetValue(buttonEffect, out var baseColor))
        {
            baseColor = image.color;
            defaultButtonColors[buttonEffect] = baseColor;
        }

        var highlightColor = new Color(0.9f, 0.9f, 0.9f, baseColor.a);
        image.color = isHighlighted ? highlightColor : baseColor;
        ToggleSelectedBackground(buttonEffect, isHighlighted);
    }

    private void ToggleSelectedBackground(ButtonEffect buttonEffect, bool isSelected)
    {
        if (buttonEffect == null)
            return;

        if (buttonEffect.LineBanner != null)
        {
            buttonEffect.LineBanner.SetActive(isSelected);
            return;
        }

        var selectionTransform = FindSelectionBackground(buttonEffect.transform);
        if (selectionTransform != null)
            selectionTransform.gameObject.SetActive(isSelected);
    }

    private Transform FindSelectionBackground(Transform root)
    {
        if (root == null)
            return null;

        string[] names =
        {
            "SelectedBackground",
            "SelectedBg",
            "SelectedBG",
            "Selected",
            "Select",
            "BackgroundSelected",
            "BgSelected",
            "Highlight",
            "BgHighlight"
        };

        foreach (var name in names)
        {
            var child = root.Find(name);
            if (child != null)
                return child;
        }

        return null;
    }

    private void SelectEffect(ButtonEffect buttonEffect, EffectPlayer modelDetail)
    {
        if (buttonEffect == null || modelDetail == null)
            return;

        selectedEffectId = modelDetail.ID;
        UpdateSelectedEffectHighlight(buttonEffect);
        UpdateEffectDetails(modelDetail);
        UpdateActionButtons(modelDetail);
    }

    private void UpdateActionButtons(EffectPlayer modelDetail)
    {
        if (modelDetail == null)
            return;

        bool isActive = modelDetail.IsActive;
        bool canUpgrade = isActive && modelDetail.Level < 3;

        if (UpLevelButton != null)
        {
            UpLevelButton.onClick.RemoveAllListeners();
            UpLevelButton.gameObject.SetActive(canUpgrade);
            if (canUpgrade)
                UpLevelButton.onClick.AddListener(() => UnlockSkill(modelDetail.ID));
        }

        if (EquipSkillButton != null)
        {
            if (!isActive)
            {
                EquipSkillButton.onClick.RemoveAllListeners();
                EquipSkillButton.gameObject.SetActive(false);
                return;
            }

            bool showEquipButton = !modelDetail.IsEquiped && !modelDetail.IsPassive;
            EquipSkillButton.onClick.RemoveAllListeners();
            EquipSkillButton.gameObject.SetActive(showEquipButton);
            if (!showEquipButton)
                return;

            if (modelDetail.IsActive)
            {
                EquipSkillButton.interactable = true;
                EquipSkillButton.image.DOFade(1f, 0.5f);
                EquipSkillButton.onClick.AddListener(() => EquipSkill(modelDetail.ID));
            }
            else
            {
                EquipSkillButton.interactable = false;
                EquipSkillButton.image.DOFade(0.5f, 0.5f);
            }
        }
    }

    private void UpdateEffectDetails(EffectPlayer modelDetail)
    {
        if (modelDetail == null)
            return;

        if (modelDetail.Description != null && TextDescription != null)
        {
            string localizedText = LocalizationManager.Instance.GetText(modelDetail.Description.ToString());
            TextDescription.text = ItemVisualHelper.ConvertSimpleHtmlToTmp(localizedText);
        }

        if (TextName != null)
            TextName.text = LocalizationManager.Instance.GetText(modelDetail.Name);

        if (TextLevel != null)
            TextLevel.text = modelDetail.Level.ToString();

        if (TextCharges != null)
            TextCharges.text = modelDetail.Charges.ToString();
    }


}
