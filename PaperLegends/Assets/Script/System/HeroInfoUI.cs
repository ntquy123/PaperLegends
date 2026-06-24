using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroInfoUI : MonoBehaviour
{
    [Header("Avatar")]
    public Image avatarImage;
    [Tooltip("Optional addressable path or id for avatar. Can be full path or formatted.")]
    public string avatarAddressablePathFormat = "heroes/{0}.png";

    [Header("Identity")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI levelText;

    [Header("Experience")]
    public Slider expSlider;
    public TextMeshProUGUI expText;

    [Header("Stats")]
    public Slider hpSlider;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI defenseText;
    public TextMeshProUGUI speedText;

    [Header("Effects")]
    public Transform effectsContainer;
    public GameObject effectIconPrefab;

    private int currentAvatarItemId = -1;

    public void Populate(HeroInfoModel model)
    {
        if (model == null)
            return;

        SetName(model.nameKey);
        SetLevel(model.level);
        SetExp(model.currentExp, model.maxExp);
        SetStats(model.hp, model.maxHp, model.attack, model.defense, model.speed);
        SetEffects(model.effects);

        if (!string.IsNullOrEmpty(model.avatarAddressable))
        {
            SetAvatarFromAddressable(model.avatarAddressable);
        }
        else if (model.avatarItemId > 0)
        {
            SetAvatarById(model.avatarItemId);
        }
    }

    public void SetName(string nameKey)
    {
        if (nameText == null)
            return;
        if (string.IsNullOrWhiteSpace(nameKey))
        {
            nameText.text = string.Empty;
            return;
        }

        nameText.text = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetText(nameKey) : nameKey;
    }

    public void SetLevel(int level)
    {
        if (levelText == null) return;
        levelText.text = level > 0 ? level.ToString() : "-";
    }

    public void SetExp(float current, float max)
    {
        if (expSlider != null)
        {
            expSlider.maxValue = Mathf.Max(1f, max);
            expSlider.value = Mathf.Clamp(current, 0f, expSlider.maxValue);
        }

        if (expText != null)
            expText.text = $"{current:0}/{max:0}";
    }

    public void SetStats(float hp, float attack, float defense, float speed)
    {
        SetStats(hp, 0f, attack, defense, speed);
    }

    public void SetStats(float hp, float maxHp, float attack, float defense, float speed)
    {
        if (hpSlider != null)
        {
            float sliderMax = maxHp > 0f ? maxHp : Mathf.Max(1f, hp);
            hpSlider.minValue = 0f;
            hpSlider.maxValue = Mathf.Max(1f, sliderMax);
            hpSlider.value = Mathf.Clamp(hp, 0f, hpSlider.maxValue);
        }

        if (hpText != null)
        {
            hpText.text = maxHp > 0f && Mathf.Abs(maxHp - hp) > 0.5f
                ? $"{hp:0}/{maxHp:0}"
                : hp.ToString("0");
        }

        if (attackText != null) attackText.text = attack.ToString("0");
        if (defenseText != null) defenseText.text = defense.ToString("0");
        if (speedText != null) speedText.text = speed.ToString("0");
    }

    public void SetAvatarById(int itemId)
    {
        if (avatarImage == null)
            return;

        currentAvatarItemId = itemId;
        avatarImage.gameObject.SetActive(true);
        avatarImage.preserveAspect = true;
        avatarImage.sprite = ItemVisualHelper.LoadSpriteByID(itemId);

        // try load higher-res via Addressables if available
        var path = string.Format(avatarAddressablePathFormat, itemId);
        if (!string.IsNullOrEmpty(path))
            StartCoroutine(AddressablesHelper.LoadSprite(path, sprite =>
            {
                if (avatarImage != null && currentAvatarItemId == itemId && sprite != null)
                    avatarImage.sprite = sprite;
            }));
    }

    public void SetAvatarFromAddressable(string addressablePath)
    {
        if (avatarImage == null || string.IsNullOrWhiteSpace(addressablePath))
            return;

        currentAvatarItemId = -1;
        avatarImage.gameObject.SetActive(true);
        avatarImage.preserveAspect = true;
        avatarImage.sprite = null;
        StartCoroutine(AddressablesHelper.LoadSprite(addressablePath, sprite =>
        {
            if (avatarImage != null && sprite != null)
                avatarImage.sprite = sprite;
        }));
    }

    public void SetEffects(List<EffectInfo> effects)
    {
        if (effectsContainer == null || effectIconPrefab == null)
            return;

        // clear
        for (int i = effectsContainer.childCount - 1; i >= 0; --i)
            Destroy(effectsContainer.GetChild(i).gameObject);

        if (effects == null || effects.Count == 0)
            return;

        foreach (var ef in effects)
        {
            var go = Instantiate(effectIconPrefab, effectsContainer);
            var img = go.GetComponentInChildren<Image>();
            if (img != null)
            {
                if (!string.IsNullOrEmpty(ef.iconAddressable))
                {
                    StartCoroutine(AddressablesHelper.LoadSprite(ef.iconAddressable, sprite =>
                    {
                        if (img != null && sprite != null)
                            img.sprite = sprite;
                    }));
                }
                else if (ef.iconSprite != null)
                {
                    img.sprite = ef.iconSprite;
                }
            }

            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = !string.IsNullOrEmpty(ef.nameKey) ? (LocalizationManager.Instance != null ? LocalizationManager.Instance.GetText(ef.nameKey) : ef.nameKey) : string.Empty;
            }
        }
    }
}

[Serializable]
public class HeroInfoModel
{
    public int avatarItemId;
    public string avatarAddressable;
    public string nameKey;
    public int level;
    public float currentExp;
    public float maxExp;

    public float hp;
    public float maxHp;
    public float attack;
    public float defense;
    public float speed;

    public List<EffectInfo> effects;
}

[Serializable]
public class EffectInfo
{
    public string nameKey;
    public string iconAddressable;
    public Sprite iconSprite;
    public float modifier; // + or - value
}
