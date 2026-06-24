using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Joystick bắn bi: giữ để nạp lực một chiều (0→1),
/// buông ra sẽ lấy lực hiện tại và gọi bắn.
/// Hỗ trợ chọn xoáy qua trục X/Z của joystick.
/// </summary>
public class ShootBallJoystick : Joystick
{
    private enum SpinDirection
    {
        None,
        Forward,
        Backward
    }

    [Header("Shoot Settings")]
    [Tooltip("Lực bắn tối đa (đơn vị tuỳ gameplay).")]
    public float maxPower = 10f;

    [Tooltip("Độ nhạy xoáy khi kéo joystick.")]
    public float spinFactor = 15f;

    [Header("Shot Accuracy")]
    [SerializeField, Range(0f, 1f), Tooltip("Từ mức lực này trở lên bắt đầu giảm chính xác.")]
    private float accuracyLossStartPower01 = 0.7f;

    [SerializeField, Range(0f, 1f), Tooltip("Từ mức lực này trở lên bắt đầu bắn loạn mạnh.")]
    private float wildShotStartPower01 = 0.9f;

    [SerializeField, Range(0f, 45f), Tooltip("Góc lệch tối đa trước vùng bắn loạn.")]
    private float normalMaxInaccuracyAngle = 8f;

    [SerializeField, Range(0f, 90f), Tooltip("Góc lệch tối đa khi lực gần hoặc bằng 100%.")]
    private float wildMaxInaccuracyAngle = 35f;

    [Header("Auto Shoot")]
    [SerializeField, Min(0f), Tooltip("Khi thanh lực đầy, giữ quá thời gian này sẽ tự bắn.")]
    private float autoShootDelayAfterFullPower = 0.8f;

    [Header("Press Feedback")]
    [Tooltip("Phần thân nút được scale và hạ xuống khi giữ. Nếu trống sẽ dùng chính ShootBallJoystick.")]
    [SerializeField] private RectTransform buttonBody;

    [Tooltip("Ảnh bóng dưới nút, sẽ đậm hơn khi giữ.")]
    [SerializeField] private Image shadowImage;

    [Tooltip("Ảnh glow quanh nút, sẽ sáng dần trong lúc giữ lực.")]
    [SerializeField] private Image glowImage;

    [Tooltip("Ảnh viên bi trang trí trong nút, sẽ xoay nhẹ trong lúc giữ. Nếu trống sẽ tìm Marble hoặc Handle.")]
    [SerializeField] private RectTransform marbleVisual;

    [SerializeField, Range(0.8f, 1f), Tooltip("Tỷ lệ thu nhỏ thân nút khi ngón tay đang giữ.")]
    private float pressedScale = 0.9f;

    [SerializeField, Min(0f), Tooltip("Số pixel hạ thân nút xuống khi giữ.")]
    private float pressedOffsetY = 8f;

    [SerializeField, Range(0.04f, 0.1f), Tooltip("Thời gian nút lõm xuống.")]
    private float pressDownDuration = 0.08f;

    [SerializeField, Range(0.1f, 0.15f), Tooltip("Thời gian nút nảy trở lại.")]
    private float releaseDuration = 0.12f;

    [SerializeField, Range(0f, 1f), Tooltip("Alpha của shadow trong lúc giữ nút.")]
    private float pressedShadowAlpha = 0.8f;

    [SerializeField, Range(0f, 1f), Tooltip("Alpha lớn nhất của glow khi đang giữ lực.")]
    private float heldGlowAlpha = 1f;

    [SerializeField, Min(0.05f), Tooltip("Thời gian glow tăng sáng khi đang giữ lực.")]
    private float heldGlowRiseDuration = 0.55f;

    [SerializeField, Min(0.1f), Tooltip("Thời gian viên bi trang trí quay một vòng khi giữ.")]
    private float marbleSpinDuration = 0.85f;

    [Header("Spin Direction Icons - Forward / Back")]
    [Tooltip("Icon sáng khi kéo joystick lên, SpinZ duong: bi co xu huong chay ve truoc.")]
    public Image straightShotIcon;

    [Tooltip("Icon sáng khi kéo joystick xuống, SpinZ am: bi co xu huong giat lui.")]
    public Image backShotIcon;

    [HideInInspector, SerializeField] private Image spinLeftIcon;
    [HideInInspector, SerializeField] private Image spinRightIcon;

    [SerializeField, Tooltip("Truc doc phai lech toi thieu bao nhieu moi sang icon tien/lui.")]
    private float directionIconDeadZone = 0.22f;

    [SerializeField, Tooltip("Tu an 2 icon tien/lui khi tha joystick.")]
    private bool hideDirectionIconsOnRelease = true;

    [SerializeField, Tooltip("Độ mờ icon không được chọn.")]
    private float idleIconAlpha = 0.42f;

    [SerializeField, Tooltip("Độ phóng to icon không được chọn.")]
    private float idleIconScale = 0.92f;

    [SerializeField, Tooltip("Độ phóng to icon đang được chọn.")]
    private float activeIconScale = 1.18f;

    [SerializeField, Tooltip("Thời gian hiệu ứng hiện icon.")]
    private float iconAppearDuration = 0.22f;

    [SerializeField, Tooltip("Độ trễ xuất hiện giữa từng icon.")]
    private float iconAppearStagger = 0.035f;

    [SerializeField, Tooltip("Thời gian đổi trạng thái sáng/tối icon.")]
    private float iconHighlightDuration = 0.12f;

    [SerializeField, Tooltip("Màu tint khi icon đang được chọn.")]
    private Color activeIconTint = Color.white;

    /// <summary>Đang giữ joystick hay không (tiện cho nơi khác tham chiếu).</summary>
    public static bool isDraggingJoystick = false;

    /// <summary>Phát khi người chơi vừa chạm nút bắn; tutorial UI dùng để đóng chỉ dẫn thao tác.</summary>
    public static event System.Action ShootButtonPressed;

    /// <summary>Xoáy trái/phải (lấy từ trục X của joystick) – đơn vị tự chọn.</summary>
    public float SpinX { get; private set; }

    /// <summary>Xoáy trước/sau (lấy từ trục Y của joystick) – đơn vị tự chọn.</summary>
    public float SpinZ { get; private set; }

    /// <summary>Góc lệch thực tế của lần bắn gần nhất, tính bằng độ quanh trục Y.</summary>
    public float LastShotAccuracyOffsetAngle { get; private set; }

    /// <summary>Biên độ sai số tối đa của lần bắn gần nhất, tính bằng độ.</summary>
    public float LastShotInaccuracySpreadAngle { get; private set; }

    /// <summary>Instance riêng, tách khỏi Joystick.Instance.</summary>
    public new static ShootBallJoystick Instance;

    private Quaternion lastShotAccuracyRotation = Quaternion.identity;
    private readonly System.Collections.Generic.Dictionary<Image, CanvasGroup> iconCanvasGroups = new System.Collections.Generic.Dictionary<Image, CanvasGroup>();
    private readonly System.Collections.Generic.Dictionary<Image, Vector3> iconBaseScales = new System.Collections.Generic.Dictionary<Image, Vector3>();
    private readonly System.Collections.Generic.Dictionary<Image, Color> iconBaseColors = new System.Collections.Generic.Dictionary<Image, Color>();
    private Sequence directionIconAppearSequence;
    private SpinDirection activeDirection = SpinDirection.None;
    private Vector3 buttonBodyRestScale;
    private Vector2 buttonBodyRestPosition;
    private Quaternion marbleRestRotation;
    private float shadowRestAlpha;
    private float glowRestAlpha;
    private bool pressFeedbackResolved;
    private Coroutine fullPowerAutoShootRoutine;
    private bool ignorePointerUpAfterAutoShoot;
    private bool shotReleaseStarted;

    private void Awake()
    {
        Instance = this;
        Joystick.Instance = this;
        ResolvePressFeedbackReferences();
        ResolveDirectionIconReferences();
        PrepareDirectionIcons();
        SetDirectionIconsVisible(false, true);
    }

    private void OnDisable()
    {
        isDraggingJoystick = false;
        SpinX = 0f;
        SpinZ = 0f;
        ResetShotAccuracyOffset();
        StopFullPowerAutoShootRoutine();
        ignorePointerUpAfterAutoShoot = false;
        shotReleaseStarted = false;
        SoundManager.Instance?.StopShootChargeAudio();
        activeDirection = SpinDirection.None;
        RestorePressFeedbackImmediately();
        KillDirectionIconTweens();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SoundManager.Instance?.StopShootChargeAudio();
        StopFullPowerAutoShootRoutine();
        KillPressFeedbackTweens();
        KillDirectionIconTweens();
    }

    /// <summary>
    /// Khi kéo joystick: vẫn gọi base để joystick hoạt động,
    /// sau đó cập nhật giá trị xoáy.
    /// </summary>
    public override void OnDrag(PointerEventData eventData)
    {
        base.OnDrag(eventData);
        SpinX = Horizontal * spinFactor;
        SpinZ = Vertical * spinFactor;
        UpdateDirectionIconHighlight();
    }

    /// <summary>
    /// Bắt đầu giữ: bật nạp lực trên PowerBar.
    /// </summary>
    public override void OnPointerDown(PointerEventData eventData)
    {
        ShootButtonPressed?.Invoke();
        isDraggingJoystick = true;
        ignorePointerUpAfterAutoShoot = false;
        shotReleaseStarted = false;
        SpinX = 0f;
        SpinZ = 0f;
        ResetShotAccuracyOffset();
        StopFullPowerAutoShootRoutine();
        ResolveDirectionIconReferences();
        PrepareDirectionIcons();
        ShowDirectionIcons();
        base.OnPointerDown(eventData);
        PlayPressDownFeedback();

        //SoundManager.Instance?.PlayButtonClick();

        // Bắt đầu nạp lực một chiều; khi full quá lâu sẽ tự bắn.
        if (PowerBarController.Instance != null)
        {
            PowerBarController.Instance.StartPingPong();
            fullPowerAutoShootRoutine = StartCoroutine(AutoShootWhenFullPowerRoutine());
            SoundManager.Instance?.StartShootChargeAudio();
        }
    }

    /// <summary>
    /// Buông: dừng nạp lực, lấy lực 0..1, quy đổi ra lực thật và bắn.
    /// </summary>
    public override void OnPointerUp(PointerEventData eventData)
    {
        if (ignorePointerUpAfterAutoShoot)
        {
            ignorePointerUpAfterAutoShoot = false;
            return;
        }

        ReleaseShot(eventData);
    }

    private void ReleaseShot(PointerEventData eventData)
    {
        if (shotReleaseStarted)
            return;

        shotReleaseStarted = true;
        StopFullPowerAutoShootRoutine();
        isDraggingJoystick = false;
        base.OnPointerUp(eventData); // (trước đây bạn gọi nhầm OnDrag)
        PlayReleaseFeedback();
        ResetDirectionIconHighlight();

        if (hideDirectionIconsOnRelease)
            SetDirectionIconsVisible(false, false);

        float power01 = 0f;
        if (PowerBarController.Instance != null)
        {
            power01 = PowerBarController.Instance.StopPingPongAndGet01(); // 0..1
        }
        SoundManager.Instance?.StopShootChargeAudio();

        power01 = Mathf.Clamp01(power01);
        PrepareShotAccuracyOffset(power01);
        float power = power01 * maxPower;

        if (CoreShootingTutorialController.Instance != null && CoreShootingTutorialController.Instance.IsRunning)
        {
            bool tutorialShotStarted = CoreShootingTutorialController.Instance.TryShoot(power, SpinX, SpinZ);
            if (tutorialShotStarted)
            {
                SoundManager.Instance?.PlayShootAudio();
            }

            SpinX = 0f;
            SpinZ = 0f;
            shotReleaseStarted = false;
            return;
        }

        // TODO: truyền thêm SpinX, SpinZ vào hệ thống bắn nếu cần.
        // Ví dụ:
        // GameSessionNetWork_Host.Instance.onShootBallByPlayer(power, SpinX, SpinZ);

        if (GameSessionClientLocal.Instance != null)
        {
            // Giữ API cũ: nếu hàm onShootBallByPlayer chỉ nhận power
            GameSessionClientLocal.Instance.onShootBallByPlayer(power);
        }

        SoundManager.Instance?.PlayShootAudio();

        SpinX = 0f;
        SpinZ = 0f;
        shotReleaseStarted = false;

        // Tuỳ chọn: reset thanh lực sau khi bắn (nếu muốn)
        // PowerBarController.Instance?.ResetBar();
    }

    private IEnumerator AutoShootWhenFullPowerRoutine()
    {
        while (isDraggingJoystick && PowerBarController.Instance != null && PowerBarController.Instance.isShootting)
        {
            if (PowerBarController.Instance.IsFullPower
                && PowerBarController.Instance.FullPowerElapsed >= autoShootDelayAfterFullPower)
            {
                fullPowerAutoShootRoutine = null;
                ignorePointerUpAfterAutoShoot = true;
                ReleaseShot(null);
                yield break;
            }

            yield return null;
        }

        fullPowerAutoShootRoutine = null;
    }

    private void StopFullPowerAutoShootRoutine()
    {
        if (fullPowerAutoShootRoutine == null)
            return;

        StopCoroutine(fullPowerAutoShootRoutine);
        fullPowerAutoShootRoutine = null;
    }

    public Vector3 ApplyShotAccuracy(Vector3 direction)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return direction;

        Vector3 adjustedDirection = lastShotAccuracyRotation * direction;
        return adjustedDirection.sqrMagnitude > Mathf.Epsilon
            ? adjustedDirection.normalized
            : direction.normalized;
    }

    private void PrepareShotAccuracyOffset(float power01)
    {
        float startPower = Mathf.Clamp01(accuracyLossStartPower01);
        float wildPower = Mathf.Clamp01(wildShotStartPower01);
        if (wildPower < startPower)
            wildPower = startPower;

        float loss01 = InverseLerp01(startPower, 1f, power01);
        float normalLoss01 = Smooth01(loss01);
        float spreadAngle = Mathf.Lerp(0f, normalMaxInaccuracyAngle, normalLoss01);

        if (power01 >= wildPower)
        {
            float wild01 = InverseLerp01(wildPower, 1f, power01);
            spreadAngle = Mathf.Lerp(spreadAngle, wildMaxInaccuracyAngle, Smooth01(wild01));
        }

        LastShotInaccuracySpreadAngle = Mathf.Max(0f, spreadAngle);
        LastShotAccuracyOffsetAngle = LastShotInaccuracySpreadAngle > 0f
            ? Random.Range(-LastShotInaccuracySpreadAngle, LastShotInaccuracySpreadAngle)
            : 0f;
        lastShotAccuracyRotation = Quaternion.AngleAxis(LastShotAccuracyOffsetAngle, Vector3.up);
    }

    private void ResetShotAccuracyOffset()
    {
        LastShotAccuracyOffsetAngle = 0f;
        LastShotInaccuracySpreadAngle = 0f;
        lastShotAccuracyRotation = Quaternion.identity;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static float InverseLerp01(float from, float to, float value)
    {
        if (Mathf.Abs(to - from) <= 0.0001f)
            return value >= to ? 1f : 0f;

        return Mathf.InverseLerp(from, to, value);
    }

    private void ResolvePressFeedbackReferences()
    {
        if (pressFeedbackResolved)
            return;

        if (buttonBody == null)
            buttonBody = ResolveRectTransformByName("ButtonBody") ?? transform as RectTransform;

        if (shadowImage == null)
            shadowImage = ResolveImageByName("Shadow");

        if (glowImage == null)
            glowImage = ResolveImageByName("Glow");

        if (marbleVisual == null)
            marbleVisual = ResolveRectTransformByName("Marble") ?? ResolveRectTransformByName("Handle");

        if (buttonBody != null)
        {
            buttonBodyRestScale = buttonBody.localScale;
            buttonBodyRestPosition = buttonBody.anchoredPosition;
        }

        if (marbleVisual != null)
            marbleRestRotation = marbleVisual.localRotation;

        if (shadowImage != null)
            shadowRestAlpha = shadowImage.color.a;

        if (glowImage != null)
            glowRestAlpha = glowImage.color.a;

        pressFeedbackResolved = true;
    }

    private RectTransform ResolveRectTransformByName(string objectName)
    {
        RectTransform[] rectTransforms = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];
            if (rectTransform != null
                && string.Equals(rectTransform.gameObject.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return rectTransform;
            }
        }

        return null;
    }

    private Image ResolveImageByName(string objectName)
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null
                && string.Equals(image.gameObject.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return image;
            }
        }

        return null;
    }

    private void PlayPressDownFeedback()
    {
        ResolvePressFeedbackReferences();
        KillPressFeedbackTweens();

        float duration = Mathf.Clamp(pressDownDuration, 0.04f, 0.1f);
        if (buttonBody != null)
        {
            buttonBody.DOScale(buttonBodyRestScale * pressedScale, duration).SetEase(Ease.OutQuad);
            buttonBody.DOAnchorPosY(buttonBodyRestPosition.y - pressedOffsetY, duration).SetEase(Ease.OutQuad);
        }

        if (shadowImage != null)
            shadowImage.DOFade(pressedShadowAlpha, duration).SetEase(Ease.OutQuad);

        if (glowImage != null)
            glowImage.DOFade(heldGlowAlpha, heldGlowRiseDuration).SetEase(Ease.OutQuad);

        if (marbleVisual != null)
        {
            marbleVisual
                .DOLocalRotate(new Vector3(0f, 0f, -360f), marbleSpinDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental);
        }
    }

    private void PlayReleaseFeedback()
    {
        ResolvePressFeedbackReferences();
        KillPressFeedbackTweens();

        float duration = Mathf.Clamp(releaseDuration, 0.1f, 0.15f);
        if (buttonBody != null)
        {
            buttonBody.DOScale(buttonBodyRestScale, duration).SetEase(Ease.OutBack);
            buttonBody.DOAnchorPos(buttonBodyRestPosition, duration).SetEase(Ease.OutBack);
        }

        if (shadowImage != null)
            shadowImage.DOFade(shadowRestAlpha, duration).SetEase(Ease.OutQuad);

        if (glowImage != null)
            glowImage.DOFade(glowRestAlpha, duration).SetEase(Ease.OutQuad);

        if (marbleVisual != null)
            marbleVisual.DOLocalRotateQuaternion(marbleRestRotation, duration).SetEase(Ease.OutQuad);
    }

    private void RestorePressFeedbackImmediately()
    {
        ResolvePressFeedbackReferences();
        KillPressFeedbackTweens();

        if (buttonBody != null)
        {
            buttonBody.localScale = buttonBodyRestScale;
            buttonBody.anchoredPosition = buttonBodyRestPosition;
        }

        if (shadowImage != null)
        {
            Color color = shadowImage.color;
            color.a = shadowRestAlpha;
            shadowImage.color = color;
        }

        if (glowImage != null)
        {
            Color color = glowImage.color;
            color.a = glowRestAlpha;
            glowImage.color = color;
        }

        if (marbleVisual != null)
            marbleVisual.localRotation = marbleRestRotation;
    }

    private void KillPressFeedbackTweens()
    {
        buttonBody?.DOKill();
        shadowImage?.DOKill();
        glowImage?.DOKill();
        marbleVisual?.DOKill();
    }

    private void ResolveDirectionIconReferences()
    {
        Transform searchRoot = GetIconSearchRoot();
        if (searchRoot == null)
            return;

        spinLeftIcon = ResolveDirectionIconReference(spinLeftIcon, "spinLeftIcon", searchRoot);
        spinRightIcon = ResolveDirectionIconReference(spinRightIcon, "spinRightIcon", searchRoot);
        straightShotIcon = ResolveDirectionIconReference(straightShotIcon, "straightShotIcon", searchRoot);
        backShotIcon = ResolveDirectionIconReference(backShotIcon, "backShotIcon", searchRoot);
        HideLegacyHorizontalIcons();
    }

    private Transform GetIconSearchRoot()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
            return parentCanvas.transform;

        return transform.root != null ? transform.root : transform;
    }

    private Image ResolveDirectionIconReference(Image current, string iconObjectName, Transform searchRoot)
    {
        if (current != null && string.Equals(current.gameObject.name, iconObjectName, System.StringComparison.OrdinalIgnoreCase))
            return current;

        Image[] images = searchRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && string.Equals(image.gameObject.name, iconObjectName, System.StringComparison.OrdinalIgnoreCase))
                return image;
        }

        return current;
    }


    private void PrepareDirectionIcons()
    {
        PrepareDirectionIcon(straightShotIcon);
        PrepareDirectionIcon(backShotIcon);
        HideLegacyHorizontalIcons();
    }

    private void PrepareDirectionIcon(Image icon)
    {
        if (icon == null)
            return;

        icon.raycastTarget = false;

        CanvasGroup group = icon.GetComponent<CanvasGroup>();
        if (group == null)
            group = icon.gameObject.AddComponent<CanvasGroup>();

        group.interactable = false;
        group.blocksRaycasts = false;

        if (!iconBaseScales.ContainsKey(icon))
            iconBaseScales[icon] = icon.rectTransform.localScale;

        if (!iconBaseColors.TryGetValue(icon, out Color baseColor))
        {
            baseColor = icon.color;
            baseColor.a = 1f;
            iconBaseColors[icon] = baseColor;
        }

        iconCanvasGroups[icon] = group;
        icon.color = baseColor;
    }

    private void ShowDirectionIcons()
    {
        SetDirectionIconsVisible(true, false);
        activeDirection = SpinDirection.None;
    }

    private void SetDirectionIconsVisible(bool visible, bool instant)
    {
        KillDirectionIconTweens();

        Image[] icons = GetDirectionIcons();
        if (visible)
        {
            directionIconAppearSequence = DOTween.Sequence();
            for (int i = 0; i < icons.Length; i++)
            {
                Image icon = icons[i];
                if (icon == null)
                    continue;

                CanvasGroup group = GetIconCanvasGroup(icon);
                Vector3 baseScale = GetIconBaseScale(icon);
                icon.gameObject.SetActive(true);
                icon.color = GetIconBaseColor(icon);
                icon.rectTransform.localScale = baseScale * 0.55f;
                group.alpha = 0f;

                if (instant)
                {
                    icon.rectTransform.localScale = baseScale * idleIconScale;
                    group.alpha = idleIconAlpha;
                    continue;
                }

                float delay = i * Mathf.Max(0f, iconAppearStagger);
                directionIconAppearSequence.Insert(delay, group.DOFade(idleIconAlpha, iconAppearDuration).SetEase(Ease.OutQuad));
                directionIconAppearSequence.Insert(delay, icon.rectTransform.DOScale(baseScale * idleIconScale, iconAppearDuration).SetEase(Ease.OutBack));
            }
        }
        else
        {
            foreach (Image icon in icons)
            {
                if (icon == null)
                    continue;

                CanvasGroup group = GetIconCanvasGroup(icon);
                Vector3 baseScale = GetIconBaseScale(icon);

                if (instant)
                {
                    group.alpha = 0f;
                    icon.rectTransform.localScale = baseScale * 0.55f;
                    icon.gameObject.SetActive(false);
                    continue;
                }

                group.DOFade(0f, 0.14f).SetEase(Ease.InQuad);
                icon.rectTransform.DOScale(baseScale * 0.65f, 0.14f)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        if (icon != null)
                            icon.gameObject.SetActive(false);
                    });
            }
        }
    }

    private void UpdateDirectionIconHighlight()
    {
        SpinDirection direction = ResolveSpinDirection();
        if (direction == activeDirection)
            return;

        activeDirection = direction;
        ApplyDirectionIconState(straightShotIcon, direction == SpinDirection.Forward);
        ApplyDirectionIconState(backShotIcon, direction == SpinDirection.Backward);
    }

    private SpinDirection ResolveSpinDirection()
    {
        float vertical = Vertical;
        if (Mathf.Abs(vertical) < directionIconDeadZone)
            return SpinDirection.None;

        return vertical > 0f ? SpinDirection.Forward : SpinDirection.Backward;
    }

    private void ResetDirectionIconHighlight()
    {
        activeDirection = SpinDirection.None;
        ApplyDirectionIconState(straightShotIcon, false);
        ApplyDirectionIconState(backShotIcon, false);
    }

    private void ApplyDirectionIconState(Image icon, bool active)
    {
        if (icon == null || !icon.gameObject.activeInHierarchy)
            return;

        CanvasGroup group = GetIconCanvasGroup(icon);
        Vector3 baseScale = GetIconBaseScale(icon);
        Color targetColor = active ? Color.Lerp(GetIconBaseColor(icon), activeIconTint, 0.7f) : GetIconBaseColor(icon);
        float targetAlpha = active ? 1f : idleIconAlpha;
        float targetScale = active ? activeIconScale : idleIconScale;

        group.DOKill();
        icon.rectTransform.DOKill();
        icon.DOKill();

        group.DOFade(targetAlpha, iconHighlightDuration).SetEase(Ease.OutQuad);
        icon.rectTransform.DOScale(baseScale * targetScale, iconHighlightDuration).SetEase(active ? Ease.OutBack : Ease.OutQuad);
        icon.DOColor(targetColor, iconHighlightDuration).SetEase(Ease.OutQuad);
    }

    private CanvasGroup GetIconCanvasGroup(Image icon)
    {
        if (iconCanvasGroups.TryGetValue(icon, out CanvasGroup group) && group != null)
            return group;

        group = icon.GetComponent<CanvasGroup>();
        if (group == null)
            group = icon.gameObject.AddComponent<CanvasGroup>();

        group.interactable = false;
        group.blocksRaycasts = false;
        iconCanvasGroups[icon] = group;
        return group;
    }

    private Vector3 GetIconBaseScale(Image icon)
    {
        return iconBaseScales.TryGetValue(icon, out Vector3 scale) ? scale : Vector3.one;
    }

    private Color GetIconBaseColor(Image icon)
    {
        return iconBaseColors.TryGetValue(icon, out Color color) ? color : Color.white;
    }

    private Image[] GetDirectionIcons()
    {
        return new[] { straightShotIcon, backShotIcon };
    }

    private void HideLegacyHorizontalIcons()
    {
        if (spinLeftIcon != null)
            spinLeftIcon.gameObject.SetActive(false);

        if (spinRightIcon != null)
            spinRightIcon.gameObject.SetActive(false);
    }

    private void KillDirectionIconTweens()
    {
        directionIconAppearSequence?.Kill();
        directionIconAppearSequence = null;

        foreach (Image icon in GetDirectionIcons())
        {
            if (icon == null)
                continue;

            CanvasGroup group = icon.GetComponent<CanvasGroup>();
            if (group != null)
                group.DOKill();

            icon.rectTransform.DOKill();
            icon.DOKill();
        }
    }
}
