using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PaperLegendWorldHealthBar : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private PaperLegendCharacterNetworkHandler target;

    [Header("Layout")]
    [SerializeField] private bool autoCreateDefaultUi = true;
    [SerializeField] private bool useTargetBoundsHeight = true;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField, Min(0.001f)] private float worldScale = 0.01f;
    [SerializeField] private Vector2 canvasSize = new Vector2(170f, 42f);
    [SerializeField] private int sortingOrder = 300;
    [SerializeField] private bool hideWhenEliminated = true;
    [SerializeField] private bool showName = true;

    [Header("References")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Image hpFillImage;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("Colors")]
    [SerializeField] private Color localPlayerHpColor = new Color(0.18f, 0.95f, 0.32f, 1f);
    [SerializeField] private Color otherPlayerHpColor = new Color(0.95f, 0.22f, 0.16f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color textColor = Color.white;

    private GameObject _generatedRoot;
    private Camera _cachedCamera;

    public PaperLegendCharacterNetworkHandler Target => target;

    public void Bind(PaperLegendCharacterNetworkHandler character)
    {
        target = character;
        showName = true;
        EnsureUi();
        RefreshImmediate();
    }

    private void Awake()
    {
        if (target == null)
            target = GetComponentInParent<PaperLegendCharacterNetworkHandler>();

        EnsureUi();
    }

    private void OnEnable()
    {
        EnsureUi();
        RefreshImmediate();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            SetVisible(false);
            return;
        }

        EnsureUi();
        UpdateVisibility();
        UpdateWorldTransform();
        UpdateHealthAndLevel();
    }

    private void OnDestroy()
    {
        if (_generatedRoot != null)
            Destroy(_generatedRoot);
    }

    public void RefreshImmediate()
    {
        UpdateVisibility();
        UpdateWorldTransform();
        UpdateHealthAndLevel();
    }

    private void EnsureUi()
    {
        if (!autoCreateDefaultUi || (worldCanvas != null && hpSlider != null && levelText != null))
        {
            CacheOptionalReferences();
            return;
        }

        if (_generatedRoot != null)
            return;

        _generatedRoot = new GameObject("PaperLegendWorldHealthBar_Runtime", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        worldCanvas = _generatedRoot.GetComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.sortingOrder = sortingOrder;

        canvasGroup = _generatedRoot.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform rootRect = _generatedRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = canvasSize;
        rootRect.localScale = Vector3.one * worldScale;

        CreateDefaultLayout(rootRect);
        CacheOptionalReferences();
    }

    private void CacheOptionalReferences()
    {
        if (worldCanvas == null)
            worldCanvas = GetComponentInChildren<Canvas>(true);

        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (hpFillImage == null && hpSlider != null && hpSlider.fillRect != null)
            hpFillImage = hpSlider.fillRect.GetComponent<Image>();
    }

    private void CreateDefaultLayout(RectTransform rootRect)
    {
        Image panel = CreateImage("Background", rootRect, backgroundColor);
        RectTransform panelRect = panel.rectTransform;
        Stretch(panelRect, Vector2.zero, Vector2.zero);
        panel.raycastTarget = false;

        Image badge = CreateImage("LevelBadge", rootRect, new Color(0.04f, 0.05f, 0.06f, 0.88f));
        RectTransform badgeRect = badge.rectTransform;
        badgeRect.anchorMin = new Vector2(0f, 0.5f);
        badgeRect.anchorMax = new Vector2(0f, 0.5f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(22f, -3f);
        badgeRect.sizeDelta = new Vector2(38f, 22f);
        badge.raycastTarget = false;

        levelText = CreateText("LevelText", badgeRect, "Lv 1", 13f, FontStyles.Bold);
        Stretch(levelText.rectTransform, Vector2.zero, Vector2.zero);

        hpSlider = CreateHealthSlider(rootRect);

        nameText = CreateText("NameText", rootRect, string.Empty, 12f, FontStyles.Bold);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -2f);
        nameRect.sizeDelta = new Vector2(-8f, 16f);
        nameText.gameObject.SetActive(showName);
    }

    private Slider CreateHealthSlider(RectTransform rootRect)
    {
        GameObject sliderObject = CreateUiObject("HpSlider", rootRect);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0.5f);
        sliderRect.anchorMax = new Vector2(1f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(22f, -3f);
        sliderRect.sizeDelta = new Vector2(-58f, 14f);

        Image sliderBg = CreateImage("Background", sliderRect, new Color(0.05f, 0.05f, 0.05f, 0.9f));
        Stretch(sliderBg.rectTransform, Vector2.zero, Vector2.zero);
        sliderBg.raycastTarget = false;

        GameObject fillAreaObject = CreateUiObject("Fill Area", sliderRect);
        RectTransform fillArea = fillAreaObject.GetComponent<RectTransform>();
        Stretch(fillArea, new Vector2(2f, 2f), new Vector2(-2f, -2f));

        hpFillImage = CreateImage("Fill", fillArea, localPlayerHpColor);
        RectTransform fillRect = hpFillImage.rectTransform;
        Stretch(fillRect, Vector2.zero, Vector2.zero);
        hpFillImage.raycastTarget = false;

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.fillRect = fillRect;
        slider.targetGraphic = sliderBg;
        return slider;
    }

    private void UpdateVisibility()
    {
        bool visible = target != null
            && target.gameObject.activeInHierarchy
            && (!hideWhenEliminated || target.IsAlive);

        SetVisible(visible);
    }

    private void SetVisible(bool visible)
    {
        GameObject root = ResolveVisualRoot();
        if (root != null && root.activeSelf != visible)
            root.SetActive(visible);

        if (canvasGroup != null)
            canvasGroup.alpha = visible ? 1f : 0f;
    }

    private void UpdateWorldTransform()
    {
        GameObject root = ResolveVisualRoot();
        if (root == null || target == null)
            return;

        Camera camera = ResolveCamera();
        if (worldCanvas != null && camera != null)
            worldCanvas.worldCamera = camera;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.sizeDelta = canvasSize;
            rootRect.localScale = Vector3.one * worldScale;
        }
        else
        {
            root.transform.localScale = Vector3.one * worldScale;
        }

        root.transform.position = ResolveWorldPosition();
        if (camera != null)
            root.transform.rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
    }

    private Vector3 ResolveWorldPosition()
    {
        if (target == null)
            return transform.position + worldOffset;

        if (!useTargetBoundsHeight)
            return target.transform.position + worldOffset;

        Bounds bounds = target.GetWorldBounds();
        return bounds.center + new Vector3(worldOffset.x, bounds.extents.y + worldOffset.y, worldOffset.z);
    }

    private void UpdateHealthAndLevel()
    {
        if (target == null)
            return;

        float maxHealth = Mathf.Max(1f, target.MaxHealth);
        float currentHealth = target.IsAlive
            ? Mathf.Clamp(target.CurrentHealth, 0f, maxHealth)
            : 0f;

        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = 1f;
            hpSlider.SetValueWithoutNotify(Mathf.Clamp01(currentHealth / maxHealth));
        }

        if (hpFillImage != null)
            hpFillImage.color = target.HasInputAuthority ? localPlayerHpColor : otherPlayerHpColor;

        if (levelText != null)
        {
            levelText.color = textColor;
            levelText.text = $"Lv {Mathf.Max(1, target.Level)}";
        }

        if (nameText != null)
        {
            nameText.gameObject.SetActive(showName);
            if (showName)
            {
                nameText.color = textColor;
                nameText.text = ResolveLocalizedHeroName();
            }
        }
    }

    private string ResolveLocalizedHeroName()
    {
        if (target == null)
            return string.Empty;

        int heroId = target.CharacterModelId;
        var selectionClient = PaperLegendCharacterSelectionClient.Instance;
        if (selectionClient != null &&
            selectionClient.HeroDataByModelId.TryGetValue(heroId, out PaperLegendHeroData heroData) &&
            heroData != null &&
            !string.IsNullOrWhiteSpace(heroData.name))
        {
            return LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetText(heroData.name)
                : heroData.name;
        }

        HeroConfig heroConfig = heroId > 0 ? HeroConfigCatalog.ResolveHero(heroId) : null;

        string nameKey = null;
        if (heroConfig != null)
        {
            if (!string.IsNullOrWhiteSpace(heroConfig.nameKey))
                nameKey = heroConfig.nameKey;
            else if (!string.IsNullOrWhiteSpace(heroConfig.heroName))
                nameKey = heroConfig.heroName;
        }

        if (string.IsNullOrWhiteSpace(nameKey))
            nameKey = heroId > 0 ? $"hero_{heroId}_name" : "paper_legend_hero_unknown";

        return LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText(nameKey)
            : nameKey;
    }

    private Camera ResolveCamera()
    {
        if (_cachedCamera != null && _cachedCamera.isActiveAndEnabled)
            return _cachedCamera;

        _cachedCamera = Camera.main;
        if (_cachedCamera != null)
            return _cachedCamera;

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled)
            {
                _cachedCamera = cameras[i];
                return _cachedCamera;
            }
        }

        return null;
    }

    private GameObject ResolveVisualRoot()
    {
        return _generatedRoot != null ? _generatedRoot : gameObject;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = CreateUiObject(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, FontStyles style)
    {
        GameObject go = CreateUiObject(name, parent);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.color = textColor;
        label.raycastTarget = false;
        return label;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
