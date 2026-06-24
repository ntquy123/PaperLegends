using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using System.Text.RegularExpressions;

public static class ItemVisualHelper
{
    public enum BallDamageStage
    {
        Pristine,
        Chipped,
        Cracked,
        Shattered
    }

    public const float DamageChippedThreshold = 0.7f;
    public const float DamageCrackedThreshold = 0.4f;
    public const float DamageShatteredThreshold = 0.15f;

    // Sửa \" thành ""
    private static readonly Regex SpanStyleRegex = new(@"<span\s+style\s*=\s*""([^""]*)""\s*>(.*?)</span>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex FontWeightRegex = new(@"font-weight\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
    private static readonly Regex ColorRegex = new(@"color\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
    private static readonly Regex RgbMatchRegex = new(@"rgb\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.IgnoreCase);



    public static int GetRarityRank(int rarityGid)
    {
        return rarityGid switch
        {
            (int)ItemRarity.Lowest => 1,
            (int)ItemRarity.Valuable => 2,
            (int)ItemRarity.Rare => 3,
            (int)ItemRarity.Epic => 4,
            (int)ItemRarity.Legendary => 5,
            _ => 1
        };
    }

    public static int CalculateBallDismantleShardReward(int level, int rarityGid, float damagePercent = 0f)
    {
        int levelValue = Mathf.Max(level, 1);
        int rarityRank = GetRarityRank(rarityGid);
        float baseValue = 4f + (levelValue * levelValue * 0.5f);

        float clampedDamagePercent = Mathf.Clamp01(damagePercent);
        float remainingPercent = 1f - clampedDamagePercent;

        // Giảm thưởng lũy tiến theo độ bền còn lại, bi hư nặng sẽ nhận ít mảnh hơn rõ rệt.
        float progressiveMultiplier = Mathf.Pow(remainingPercent, 1.35f);
        float minimumMultiplier = 0.2f;
        float rewardMultiplier = Mathf.Lerp(minimumMultiplier, 1f, progressiveMultiplier);

        return Mathf.Max(1, Mathf.RoundToInt(baseValue * rarityRank * rewardMultiplier));
    }

    public static int CalculateBallRepairShardCost(int level, int rarityGid)
    {
        int levelValue = Mathf.Max(level, 1);
        int rarityRank = GetRarityRank(rarityGid);
        float baseValue = 3f + (levelValue * levelValue * 0.35f);
        // Đồng bộ công thức chung:
        // cost = round((3 + level^2 * 0.35) * rarityRank * 0.5)
        return Mathf.Max(1, Mathf.RoundToInt(baseValue * rarityRank * 0.5f));
    }

    private static readonly Dictionary<Image, Color> RarityBaseColors = new();
    public class ItemGroup
    {
        public readonly List<int> seqList = new();
        public readonly List<GameObject> objects = new();
        public readonly List<TextMeshProUGUI> quantityLabels = new();
    }

    private static readonly Dictionary<int, ItemGroup> _itemGroups = new();
    private static readonly Dictionary<int, HashSet<int>> _selectedUpgradeSeqs = new();

    public static IReadOnlyDictionary<int, ItemGroup> ItemGroups => _itemGroups;

    public static string FormatRelativeTime(string timestamp)
    {
        if (string.IsNullOrWhiteSpace(timestamp))
            return string.Empty;

        DateTime parsedTime;
        // Thử phân tích chuỗi ngày giờ với các định dạng phổ biến
        if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces, out parsedTime))
        {
            // Nếu không thành công, trả về chuỗi gốc
            return timestamp.Trim();
        }

        // Chuyển đổi sang giờ địa phương nếu chưa phải
        DateTime localTime = parsedTime.Kind == DateTimeKind.Utc ? parsedTime.ToLocalTime() : parsedTime;
        DateTime now = DateTime.Now;

        TimeSpan difference = now - localTime;

        if (difference.TotalSeconds < 0)
        {
            // Nếu thời gian ở tương lai, chỉ hiển thị ngày/giờ
            return localTime.ToString("dd/MM/yyyy HH:mm", new CultureInfo("vi-VN"));
        }

        if (difference.TotalSeconds < 60)
        {
            int seconds = Math.Max(1, (int)Math.Round(difference.TotalSeconds));
            return $"{seconds} "+ LocalizationManager.Instance.GetText("seconds_ago");
        }

        if (difference.TotalMinutes < 60)
        {
            int minutes = Math.Max(1, (int)Math.Round(difference.TotalMinutes));
            return $"{minutes} " + LocalizationManager.Instance.GetText("minutes_ago");
        }

        if (difference.TotalHours < 24)
        {
            int hours = Math.Max(1, (int)Math.Round(difference.TotalHours));
            return $"{hours} " + LocalizationManager.Instance.GetText("hours_ago");
        }

        if (difference.TotalDays < 7)
        {
            int days = Math.Max(1, (int)Math.Round(difference.TotalDays));
            return $"{days} " + LocalizationManager.Instance.GetText("days_ago");
        }

        // Nếu quá 7 ngày, hiển thị ngày/tháng/năm
        return localTime.ToString("dd/MM/yyyy", new CultureInfo("vi-VN"));
    }

    public static Sprite LoadSpriteByID(int itemID)
    {
        string path = "Items/" + itemID.ToString(); // Ví dụ: "Items/1001"

        Sprite sprite = Resources.Load<Sprite>(path);

        if (sprite == null)
        {
            string pathDf = "Items/3"; // Ví dụ: "Items/1001"
            sprite = Resources.Load<Sprite>(pathDf);

        }
        return sprite;
    }

    public static Color GetRarityColor(int rarityGid)
    {
        return rarityGid switch
        {
            (int)ItemRarity.Lowest => GradientHSV(0.08f, 0.62f, 0.52f),
            (int)ItemRarity.Valuable => GradientHSV(0.32f, 0.55f, 0.58f),
            (int)ItemRarity.Rare => GradientHSV(0.56f, 0.6f, 0.62f),
            (int)ItemRarity.Epic => GradientHSV(0.78f, 0.62f, 0.6f),
            (int)ItemRarity.Legendary => GradientHSV(0.12f, 0.7f, 0.75f),
            _ => Color.white
        };
    }

    public static void ApplyRarityBackground(Transform itemTransform, int rarityGid)
    {
        if (itemTransform == null)
            return;

        Image backgroundImage = null;
        if (itemTransform.TryGetComponent<ItemPrefabView>(out var view) && view.BackgroundImage != null)
            backgroundImage = view.BackgroundImage;
        else
            backgroundImage = itemTransform.GetComponent<Image>();

        if (backgroundImage == null)
            return;

        ApplyRarityBackground(backgroundImage, rarityGid);
    }

    public static void ApplyRarityBackground(Image backgroundImage, int rarityGid)
    {
        if (backgroundImage == null)
            return;

        var color = GetRarityColor(rarityGid);
        backgroundImage.color = color;
        RarityBaseColors[backgroundImage] = color;
    }

    public static bool TryGetRarityBaseColor(Image image, out Color color)
    {
        return RarityBaseColors.TryGetValue(image, out color);
    }
    public static void ClearGroups()
    {
        _itemGroups.Clear();
        _selectedUpgradeSeqs.Clear();
    }

    public static ItemGroup GetGroup(int itemId)
    {
        if (!_itemGroups.TryGetValue(itemId, out var group))
        {
            group = new ItemGroup();
            _itemGroups[itemId] = group;
        }
        return group;
    }

    public static ItemGroup InitGroup(int itemId, IEnumerable<int> seqs)
    {
        var group = GetGroup(itemId);
        group.seqList.Clear();
        if (seqs != null)
            group.seqList.AddRange(seqs);
        group.objects.Clear();
        group.quantityLabels.Clear();
        return group;
    }

    private static Color ColorFromHex(string hex, Color fallback)
    {
        return ColorUtility.TryParseHtmlString(hex, out var color) ? color : fallback;
    }

    private static Color GradientHSV(float hue, float saturation, float value, float highlightBoost = 0.18f, float highlightBlend = 0.35f)
    {
        var baseColor = Color.HSVToRGB(hue, saturation, value);
        var highlight = Color.HSVToRGB(hue, Mathf.Clamp01(saturation * 0.75f), Mathf.Clamp01(value + highlightBoost));
        return Color.Lerp(baseColor, highlight, highlightBlend);
    }

    private static readonly int[] LevelExpRequirements = new int[]
    {
        100, 150, 200, 250, 300, 350, 400, 450, 500, 550,
        600, 650, 700, 750, 800, 850, 900, 950, 1000, 1050,
        1100, 1150, 1200, 1250
    };

    public static int GetExpForNextLevel(int level)
    {
        if (level < 1 || level > LevelExpRequirements.Length)
            return 0;
        return LevelExpRequirements[level - 1];
    }

    public static float CalculateGemSuccessRate(int level, IEnumerable<int> gemRarities)
    {
        if (gemRarities == null)
            return 0f;

        var rarityFactors = new Dictionary<int, float>
        {
            {1, 5f},
            {2, 8f},
            {3, 12f}
        };

        float levelFactor = Mathf.Clamp(level, 0, 10) / 10f;
        float totalRate = 0f;
        foreach (var rarity in gemRarities)
        {
            float rarityFactor;
            if (!rarityFactors.TryGetValue(rarity, out rarityFactor))
                continue;
            totalRate += rarityFactor * levelFactor;
        }

        return Mathf.Clamp01(totalRate / 100f) * 100f;
    }

    //hiển thị level lên icon của item kèm màu nền
    public static void SetLevelVisual(Transform itemTransform, int level, TypeItemGid itemType)
    {
        if (itemTransform == null)
            return;

        if (!itemTransform.TryGetComponent<ItemPrefabView>(out var view))
            return;

        var levelText = view.LevelText;
        var banner = view.LevelBanner;

        bool showLevel = itemType == TypeItemGid.Culi;

        if (levelText != null)
            levelText.gameObject.SetActive(showLevel);
        if (banner != null)
            banner.gameObject.SetActive(showLevel);

        if (!showLevel)
            return;

        if (levelText != null)
            levelText.text = "Lv." + level.ToString();

        if (banner != null)
        {
            Color color;
            if (level < 5)
                color = new Color(1f, 0.5f, 0f, 1f);
            else if (level <= 7)
                color = new Color(1f, 1f, 1f, 1f);
            else if (level <= 9)
                color = new Color(1f, 1f, 0f, 1f);
            else
                color = new Color(0.5f, 0f, 0.5f, 1f);

            banner.color = color;
        }
    }

    public static GameObject InstantiateGroupedItem(GameObject prefab, Transform parent, ItemSchema item, Dictionary<(int, int), List<GameObject>> map)
    {
        var group = GetGroup(item.id);
        var obj = UnityEngine.Object.Instantiate(prefab, parent);
        SetLevelVisual(obj.transform, item.level, (TypeItemGid)item.typeGid);

        group.objects.Add(obj);
        foreach (var seq in group.seqList)
        {
            if (!map.ContainsKey((item.id, seq)))
                map[(item.id, seq)] = group.objects;
        }

        TextMeshProUGUI qty = null;
        if (obj.TryGetComponent<ItemPrefabView>(out var view))
            qty = view.QuantityText;
        bool show = group.seqList.Count > 1;
        if (qty != null)
        {
            qty.gameObject.SetActive(show);
            if (show)
                qty.text = group.seqList.Count.ToString();
            group.quantityLabels.Add(qty);
        }

        return obj;
    }

    public static void UpdateGroupedItemQuantity(int itemId)
    {
        if (_itemGroups.TryGetValue(itemId, out var group))
        {
            bool show = group.seqList.Count > 1;
            foreach (var label in group.quantityLabels)
            {
                if (label != null)
                {
                    label.gameObject.SetActive(show);
                    if (show)
                        label.text = group.seqList.Count.ToString();
                }
            }
        }
    }

    public static void SetItemActive(Dictionary<(int, int), List<GameObject>> map, int itemId, int seq, bool active)
    {
        if (_itemGroups.TryGetValue(itemId, out var group))
        {
            if (active)
            {
                if (!group.seqList.Contains(seq))
                    group.seqList.Add(seq);
                if (!map.ContainsKey((itemId, seq)))
                    map[(itemId, seq)] = group.objects;
            }
            else
            {
                group.seqList.Remove(seq);
            }

            bool show = group.seqList.Count > 1;
            foreach (var label in group.quantityLabels)
            {
                label.gameObject.SetActive(show);
                if (show)
                    label.text = group.seqList.Count.ToString();
            }

            bool activeAny = group.seqList.Count > 0;
            foreach (var obj in group.objects)
                if (obj != null)
                    obj.SetActive(activeAny);
        }
        else if (map.TryGetValue((itemId, seq), out var objs))
        {
            foreach (var obj in objs)
                if (obj != null)
                    obj.SetActive(active);
        }
    }

    private static HashSet<int> GetSelectedUpgradeSet(int itemId)
    {
        if (!_selectedUpgradeSeqs.TryGetValue(itemId, out var set))
        {
            set = new HashSet<int>();
            _selectedUpgradeSeqs[itemId] = set;
        }
        return set;
    }

    private static void ApplyDim(GameObject obj, bool dim)
    {
        if (obj == null)
            return;

        var canvasGroup = obj.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = obj.AddComponent<CanvasGroup>();

        canvasGroup.alpha = dim ? 0.45f : 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public static void SetItemSelectedForUpgrade(Dictionary<(int, int), List<GameObject>> map, int itemId, int seq, bool selected)
    {
        if (_itemGroups.TryGetValue(itemId, out var group))
        {
            var selectedSet = GetSelectedUpgradeSet(itemId);
            if (selected)
                selectedSet.Add(seq);
            else
                selectedSet.Remove(seq);

            if (selected)
                group.seqList.Remove(seq);
            else if (!group.seqList.Contains(seq))
                group.seqList.Add(seq);

            if (selectedSet.Count == 0)
                _selectedUpgradeSeqs.Remove(itemId);

            bool show = group.seqList.Count > 1;
            foreach (var label in group.quantityLabels)
            {
                if (label != null)
                {
                    label.gameObject.SetActive(show);
                    if (show)
                        label.text = group.seqList.Count.ToString();
                }
            }

            bool activeAny = group.seqList.Count > 0 || selectedSet.Count > 0;
            bool dim = selectedSet.Count > 0;
            foreach (var obj in group.objects)
            {
                if (obj == null)
                    continue;
                obj.SetActive(activeAny);
                ApplyDim(obj, dim);
            }
            return;
        }

        if (map.TryGetValue((itemId, seq), out var objs))
        {
            foreach (var obj in objs)
            {
                if (obj == null)
                    continue;
                obj.SetActive(true);
                ApplyDim(obj, selected);
            }
        }
    }

    //public static void ShowItemActions(Transform itemTransform, ItemSchema item)
    //{
    //    var actionList = FindActionList(itemTransform);
    //    if (actionList == null)
    //        return;

    //    if (itemTransform.parent != null)
    //    {
    //        foreach (Transform sibling in itemTransform.parent)
    //        {
    //            if (sibling == itemTransform) continue;
    //            var siblingList = FindActionList(sibling);
    //            if (siblingList != null)
    //                siblingList.gameObject.SetActive(false);
    //        }
    //    }

    //    actionList.gameObject.SetActive(true);

    //    var equipBtn = actionList.Find("Equip")?.GetComponent<Button>();
    //    var detailBtn = actionList.Find("Detail")?.GetComponent<Button>();
    //    var sellBtn = actionList.Find("Sell")?.GetComponent<Button>();

    //    if (equipBtn != null)
    //    {
    //        equipBtn.onClick.RemoveAllListeners();
    //        equipBtn.onClick.AddListener(InventoryController.Instance.onClickEquip);
    //    }

    //    if (sellBtn != null)
    //    {
    //        sellBtn.onClick.RemoveAllListeners();
    //        sellBtn.onClick.AddListener(InventoryController.Instance.onClickSellMarket);
    //    }

    //    if (detailBtn != null)
    //    {
    //        detailBtn.onClick.RemoveAllListeners();
    //        detailBtn.onClick.AddListener(() => PopupHelper.Instance.ShowItemInfoPopup(item,1));
    //    }
    //}

    public static IEnumerator ApplyMaterial(Renderer charRenderer, Renderer ballRenderer, SkinnedMeshRenderer hairRenderer, int id, int type, int? currentHairId, bool hasCateye = true)
    {
        if (type == (int)TypeItemGid.Culi)
        {
            SetRendererActive(charRenderer, false);
            SetRendererActive(hairRenderer, false);
            yield return ApplyCuliMaterial(ballRenderer, id, hasCateye);
        }
        else if (type != (int)TypeItemGid.Other)
        {
            yield return ApplyCharacterMaterial(charRenderer, ballRenderer, hairRenderer, id, type, currentHairId);
        }
    }

    private static void SetRendererActive(Renderer renderer, bool active)
    {
        if (renderer != null)
            renderer.gameObject.SetActive(active);
    }

    private static IEnumerator LoadHairMesh(SkinnedMeshRenderer hairRenderer, int hairId)
    {
        if (hairRenderer == null)
            yield break;

        yield return LoadMeshWithFallback(
            $"{AddressablePaths.Items.HairMeshes}/{hairId}.mat",
            AddressablePaths.Items.DefaultHairMesh,
            mesh =>
            {
                if (mesh != null)
                    hairRenderer.sharedMesh = mesh;
            }
        );
    }

    private static IEnumerator ApplyCuliMaterial(Renderer ballRenderer, int id, bool hasCateye)
    {
        if (ballRenderer == null)
            yield break;

        SetRendererActive(ballRenderer, true);

        Material newMaterial = null;
        yield return LoadMaterialWithFallback(
            $"{AddressablePaths.Items.Culi}/{id}.mat",
            AddressablePaths.Items.DefaultCuliMaterial,
            mat => { newMaterial = mat; }
        );

        if (newMaterial != null)
        {
            ballRenderer.enabled = true;
            ballRenderer.material = newMaterial;

            Transform cateye = ballRenderer.transform.Find("Cateye");
            if (cateye != null)
            {
                if (!hasCateye)
                {
                    cateye.gameObject.SetActive(false);
                    yield break;
                }

                cateye.gameObject.SetActive(true);
                Material cateyeMat = null;
                yield return LoadMaterialWithFallback(
                    $"{AddressablePaths.Items.CuliCateye}/{id}.mat",
                    AddressablePaths.Items.DefaultCateyeCuliMaterial,
                    mat => { cateyeMat = mat; }
                );

                var cateyeRenderer = cateye.GetComponent<Renderer>();
                if (cateyeMat != null && cateyeRenderer != null)
                {
                    cateyeRenderer.enabled = true;
                    cateyeRenderer.material = cateyeMat;
                }
            }
        }
    }

    private static IEnumerator ApplyCharacterMaterial(Renderer charRenderer, Renderer ballRenderer, SkinnedMeshRenderer hairRenderer, int id, int type, int? currentHairId)
    {
        SetRendererActive(charRenderer, true);
        SetRendererActive(hairRenderer, true);
        SetRendererActive(ballRenderer, false);

        Material newMaterial = null;
        yield return LoadMaterialWithFallback(
            $"{AddressablePaths.Items.Culi}/{id}.mat",
            AddressablePaths.Items.DefaultCuliMaterial,
            mat => { newMaterial = mat; }
        );

        if (newMaterial != null && charRenderer != null)
        {
            charRenderer.enabled = true;
            charRenderer.material = newMaterial;
        }

        if (hairRenderer != null)
        {
            if (currentHairId.HasValue)
                yield return LoadHairMesh(hairRenderer, currentHairId.Value);

            if (type == (int)TypeItemGid.Hair)
                yield return LoadHairMesh(hairRenderer, id);
        }
    }

    public static IEnumerator LoadMaterialWithFallback(string mainPath, string fallbackPath, System.Action<Material> callback)
    {
        Material result = null;

        yield return AddressablesHelper.LoadAsset<Material>(mainPath, mat => result = mat);

        if (result == null)
        {
            Debug.LogWarning($"Fallback to default material: {fallbackPath}");
            yield return AddressablesHelper.LoadAsset<Material>(fallbackPath, mat => result = mat);
        }

        callback?.Invoke(result);
    }


    public static IEnumerator LoadMeshWithFallback(string mainPath, string fallbackPath, System.Action<Mesh> callback)
    {
        Mesh result = null;

        yield return AddressablesHelper.LoadAsset<Mesh>(mainPath, mesh => result = mesh);

        if (result == null)
        {
            Debug.LogWarning($"Fallback to default mesh: {fallbackPath}");
            yield return AddressablesHelper.LoadAsset<Mesh>(fallbackPath, mesh => result = mesh);
        }

        callback?.Invoke(result);
    }


    public static string BuildStatInfo(float mass, float speed, float bounce, float impact, float damagePercent = -1f)
    {
        float massPct = GetMassPercent(mass);
        float speedPct = GetSpeedPercent(speed);
        float bouncePct = GetBouncePercent(bounce);
        float impactPct = GetImpactPercent(impact);

        string result = LocalizationManager.Instance.GetText("Mas_des") + ": " + Mathf.RoundToInt(massPct * 100f) + "%"
            + "\n" + LocalizationManager.Instance.GetText("speed") + ": " + Mathf.RoundToInt(speedPct * 100f) + "%"
            + "\n" + LocalizationManager.Instance.GetText("Bounce_des") + ": " + Mathf.RoundToInt(bouncePct * 100f) + "%"
            + "\n" + LocalizationManager.Instance.GetText("Impact_des") + ": " + Mathf.RoundToInt(impactPct * 100f) + "%";
        if (damagePercent >= 0f)
        {
            float damagePct = Mathf.Clamp01(damagePercent);
            result += "\n" + LocalizationManager.Instance.GetText("Damage_des") + ": " + Mathf.RoundToInt(damagePct * 100f) + "%";
        }

        return result;
    }

    public static string BuildScaledStatInfo(float mass, float speed, float bounce, float impact, float damagePercent = -1f)
    {
        string result = LocalizationManager.Instance.GetText("Mas_des") + ": " + FormatScaledStatValueText(mass)
            + "\n" + LocalizationManager.Instance.GetText("speed") + ": " + FormatScaledStatValueText(speed)
            + "\n" + LocalizationManager.Instance.GetText("Bounce_des") + ": " + FormatScaledStatValueText(bounce)
            + "\n" + LocalizationManager.Instance.GetText("Impact_des") + ": " + FormatScaledStatValueText(impact);
        if (damagePercent >= 0f)
        {
            result += "\n" + LocalizationManager.Instance.GetText("Damage_des") + ": " + FormatScaledStatValueText(Mathf.Clamp01(damagePercent));
        }

        return result;
    }

    private static string FormatScaledStatValueText(float value)
    {
        return $"<color=#00FF00>{Mathf.RoundToInt(value * 100f)}</color>";
    }

    public static void SetSliderValueAndColor(Slider slider, float normalizedValue)
    {
        if (slider == null)
            return;

        normalizedValue = Mathf.Clamp01(normalizedValue);
        slider.value = normalizedValue;

        Image fillImage = null;
        if (slider.fillRect != null)
            fillImage = slider.fillRect.GetComponent<Image>();

        if (fillImage == null)
        {
            var fillTransform = slider.transform.Find("Fill");
            if (fillTransform != null)
                fillImage = fillTransform.GetComponent<Image>();
        }

        if (fillImage != null)
        {
            float t = Mathf.Clamp01((normalizedValue - 0.1f) / 0.9f);
            fillImage.color = Color.Lerp(Color.yellow, Color.red, t);
        }
    }

    public static void UpdateStatSliders(Slider massS, Slider speedS, Slider bounceS, Slider impactS,
                                         float mass, float speed, float bounce, float impact)
    {
        SetSliderValueAndColor(massS, GetMassPercent(mass));
        SetSliderValueAndColor(speedS, GetSpeedPercent(speed));
        SetSliderValueAndColor(bounceS, GetBouncePercent(bounce));
        SetSliderValueAndColor(impactS, GetImpactPercent(impact));
    }

    public static BallDamageStage GetDamageStage(float remainingPercent)
    {
        if (remainingPercent <= DamageShatteredThreshold)
            return BallDamageStage.Shattered;
        if (remainingPercent <= DamageCrackedThreshold)
            return BallDamageStage.Cracked;
        if (remainingPercent <= DamageChippedThreshold)
            return BallDamageStage.Chipped;

        return BallDamageStage.Pristine;
    }

    public static float GetDamagePercent(float maxImpact, float damage)
    {
        if (maxImpact <= 0f)
            return 0f;

        return Mathf.Clamp01(damage / maxImpact);
    }

    public static float GetRemainingImpactPercent(float maxImpact, float damage)
    {
        if (maxImpact <= 0f)
            return 1f;

        return Mathf.Clamp01((maxImpact - damage) / maxImpact);
    }

    public static void UpdateDamageSlider(Slider slider, float damagePercent, BallDamageStage stage)
    {
        if (slider == null)
            return;

        slider.value = Mathf.Clamp01(damagePercent);

        var fillImage = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        if (fillImage == null)
        {
            var fillTransform = slider.transform.Find("Fill");
            if (fillTransform != null)
                fillImage = fillTransform.GetComponent<Image>();
        }

        if (fillImage == null)
            return;

        switch (stage)
        {
            case BallDamageStage.Chipped:
                fillImage.color = new Color(0.95f, 0.85f, 0.2f);
                break;
            case BallDamageStage.Cracked:
                fillImage.color = new Color(0.98f, 0.55f, 0.2f);
                break;
            case BallDamageStage.Shattered:
                fillImage.color = new Color(0.92f, 0.25f, 0.25f);
                break;
            default:
                fillImage.color = new Color(0.25f, 0.85f, 0.4f);
                break;
        }
    }

    public static float CalculateStatByLevel(float baseValue, int level)
    {
        if (level <= 1)
            return baseValue;

        const float percentPerLevel = 1f / 9f; // ensure level 10 ~= 2x base
        float bonusPercent = Mathf.Min(level - 1, 9) * percentPerLevel;
        float multiplier = Mathf.Min(1f + bonusPercent, 2f);
        return Mathf.Round(baseValue * multiplier * 100f) / 100f;
    }

    private static float CalculateDragValueByLevel(float baseValue, int level)
    {
        if (level <= 1)
            return baseValue;

        const float percentPerLevel = 1f / 9f; // ensure level 10 ~= 0.5x base
        float bonusPercent = Mathf.Min(level - 1, 9) * percentPerLevel;
        float multiplier = Mathf.Max(1f - 0.5f * bonusPercent, 0.5f);
        return Mathf.Round(baseValue * multiplier * 100f) / 100f;
    }

    public static float CalculateDragByLevel(float mass, float gravityScale, float drag, float bounciness, float elasticity, float impactResistance, int level)
    {
        float scaledMass = CalculateStatByLevel(mass, level);
        float scaledGravity = CalculateStatByLevel(gravityScale, level);
        float scaledDrag = CalculateDragValueByLevel(drag, level);
        float scaledBounce = CalculateStatByLevel(bounciness, level);
        float scaledElasticity = CalculateStatByLevel(elasticity, level);
        float scaledImpact = CalculateStatByLevel(impactResistance, level);

        return CalculateSpeedFromStats(scaledMass, scaledGravity, scaledDrag, scaledBounce, scaledElasticity, scaledImpact);
    }

    public static float GetMassPercent(float mass)
    {
        return Mathf.InverseLerp(0.1f, 2f, mass);
    }

    public static float GetDragPercent(float drag)
    {
        return Mathf.InverseLerp(0.05f, 0.4f, drag);
    }
    public static float GetSpeedPercent(float speed)
    {
        return Mathf.Clamp01(speed);
    }
    public static float GetBouncePercent(float bounce)
    {
        return Mathf.InverseLerp(0.1f, 1f, bounce);
    }

    public static float GetImpactPercent(float impact)
    {
        return Mathf.InverseLerp(0.1f, 2f, impact);
    }

    public static float CalculateBallStatIndex(ItemSchema item)
    {
        if (item == null)
            return 0f;

        float mass = CalculateStatByLevel(item.Mass, item.level);
        float speed = CalculateDragByLevel(item.Mass, item.GravityScale, item.Drag, item.Bounciness, item.Elasticity, item.ImpactResistance, item.level);
        float bounce = CalculateStatByLevel(item.Bounciness, item.level);
        float impact = CalculateStatByLevel(item.ImpactResistance, item.level);

        float massPct = GetMassPercent(mass);
        float speedPct = GetSpeedPercent(speed);
        float bouncePct = GetBouncePercent(bounce);
        float impactPct = GetImpactPercent(impact);

        float avgPct = (massPct + speedPct + bouncePct + impactPct) / 4f;
        return Mathf.Clamp(avgPct * 100f, 0f, 100f);
    }

    public static float CalculateSpeedFromStats(float mass, float gravityScale, float drag, float bounciness, float elasticity, float impactResistance)
    {
        float massPercent = GetMassPercent(mass);
        float dragPercent = GetDragPercent(drag);
        float gravityPercent = Mathf.InverseLerp(0.5f, 2f, gravityScale);
        float bouncePercent = GetBouncePercent(bounciness);
        float elasticityPercent = Mathf.InverseLerp(0.1f, 1f, elasticity);
        float impactPercent = GetImpactPercent(impactResistance);

        float speed = massPercent * (1f - dragPercent);
        speed *= Mathf.Lerp(0.9f, 1.1f, gravityPercent);
        speed *= Mathf.Lerp(0.95f, 1.05f, bouncePercent);
        speed *= Mathf.Lerp(0.95f, 1.05f, elasticityPercent);
        speed *= Mathf.Lerp(0.95f, 1.05f, impactPercent);

        return Mathf.Clamp01(speed);
    }
    //public static void BindIngredientRemove(GameObject slotObj, BallUpgradeController controller, UnityAction onRemove)
    //{
    //    if (slotObj == null)
    //        return;

    //    var btn = slotObj.transform.Find("Remove")?.GetComponent<Button>();
    //    if (btn == null)
    //        return;

    //    bool belongs = controller != null && controller.IsSlotItem(slotObj);

    //    bool show = onRemove != null && belongs;
    //    btn.gameObject.SetActive(show);
    //    btn.onClick.RemoveAllListeners();
    //    if (show)
    //        btn.onClick.AddListener(onRemove);
    //}
    public static void SetButtonState(Button btn, bool enabled)
    {
        if (btn == null)
            return;

        if (!btn.gameObject.activeSelf)
            btn.gameObject.SetActive(true);

        btn.interactable = enabled;
        var canvasGroup = btn.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = enabled ? 1f : 0.5f;
        }
        else if (btn.image != null)
        {
            var color = btn.image.color;
            color.a = enabled ? 1f : 0.5f;
            btn.image.color = color;
        }
    }
    public static string ConvertSimpleHtmlToTmp(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        string output = input
            .Replace("<br>", "\n")
            .Replace("<br/>", "\n")
            .Replace("<br />", "\n")
            .Replace("<p>", "\n")
            .Replace("</p>", "\n")
            .Replace("<strong>", "<b>")
            .Replace("</strong>", "</b>")
            .Replace("<em>", "<i>")
            .Replace("</em>", "</i>")
            .Replace("&nbsp;", " ");

        int safety = 0;
        while (SpanStyleRegex.IsMatch(output) && safety++ < 25)
        {
            output = SpanStyleRegex.Replace(output, match =>
            {
                string style = match.Groups[1].Value;
                string content = match.Groups[2].Value;

                bool isBold = false;
                string color = null;

                // Xử lý Bold
                Match weightMatch = FontWeightRegex.Match(style);
                if (weightMatch.Success)
                {
                    string weightValue = weightMatch.Groups[1].Value.Trim();
                    if (weightValue.Equals("bold", StringComparison.OrdinalIgnoreCase))
                        isBold = true;
                    else if (int.TryParse(weightValue, out int weightNumber))
                        isBold = weightNumber >= 600;
                }

                // Xử lý Color (Chuyển RGB -> Hex)
                Match colorMatch = ColorRegex.Match(style);
                if (colorMatch.Success)
                {
                    color = ProcessColorValue(colorMatch.Groups[1].Value);
                }

                string result = content;

                // Appy tag theo thứ tự của TextMeshPro
                if (!string.IsNullOrEmpty(color))
                    result = $"<color={color}>{result}</color>";

                if (isBold)
                    result = $"<b>{result}</b>";

                return result;
            });
        }

        return output.Trim();
    }

    private static string ProcessColorValue(string colorValue)
    {
        if (string.IsNullOrEmpty(colorValue)) return null;

        // Kiểm tra nếu là định dạng rgb(r, g, b)
        var match = RgbMatchRegex.Match(colorValue);
        if (match.Success)
        {
            int r = int.Parse(match.Groups[1].Value);
            int g = int.Parse(match.Groups[2].Value);
            int b = int.Parse(match.Groups[3].Value);
            // Chuyển sang định dạng #RRGGBB
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        // Nếu đã là Hex hoặc tên màu thì giữ nguyên (nhưng xóa khoảng trắng)
        return colorValue.Trim();
    }
}
