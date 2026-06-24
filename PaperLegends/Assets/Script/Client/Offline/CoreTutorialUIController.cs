using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if !UNITY_SERVER
using DG.Tweening;
#endif

[DisallowMultipleComponent]
public sealed class CoreTutorialUIController : MonoBehaviour
{
    private const int TutorialBigBallSkillId = 11400005;
    private const string TutorialBigBallSkillNameKey = "tutorial_big_ball_skill_name";
    private const string TutorialBigBallSkillDescriptionKey = "tutorial_big_ball_skill_description";
    private const int TutorialVoiceStepOne = 1;
    private const int TutorialVoiceStepTwo = 2;
    private const int TutorialVoiceStepThree = 3;
    private const int TutorialVoiceStepFour = 4;
    private const int TutorialVoiceStepFive = 5;

    [Header("Tutorial UI References")]
    [SerializeField] private GameObject tutorialRoot;
    [SerializeField] private CanvasGroup instructionGroup;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private RectTransform shootHint;

    [Header("Tutorial Introduction - Step 1")]
    [SerializeField] private GameObject introductionRoot;
    [SerializeField] private Button understoodButton;

    [Header("Tutorial Introduction - Step 2")]
    [SerializeField, Tooltip("Panel huong dan nguoi choi cham va giu nut ban lan dau.")]
    private GameObject introductionStepTwoRoot;
    [SerializeField, Tooltip("Icon ban tay chi vao nut ban trong panel huong dan step 2.")]
    private RectTransform stepTwoHandIcon;

    [Header("Tutorial Introduction - Step 3")]
    [SerializeField, Tooltip("Panel huong dan nguoi choi cham icon ky nang Ban Manh.")]
    private GameObject introductionStepThreeRoot;
    [SerializeField, Tooltip("Icon ban tay chi vao icon ky nang trong panel huong dan step 3.")]
    private RectTransform stepThreeHandIcon;
    [SerializeField, Tooltip("Nut icon ky nang Ban Manh duoc dung rieng trong tutorial.")]
    private Button stepThreeSkillButton;
    [SerializeField, Tooltip("Anh icon ky nang Ban Manh; neu duoc gan se tu nap icon mac dinh.")]
    private Image stepThreeSkillImage;
    [SerializeField, Tooltip("Object thong tin vien bi/ky nang trong tutorial; hien tu step 3 tro ve sau.")]
    private GameObject tutorialBallInformationRoot;
    [SerializeField, Tooltip("Text ten ky nang Ban Manh trong panel huong dan step 3.")]
    private TextMeshProUGUI stepThreeSkillNameText;
    [SerializeField, Tooltip("Text mo ta ky nang Ban Manh trong panel huong dan step 3.")]
    private TextMeshProUGUI stepThreeSkillDescriptionText;

    [Header("Tutorial Introduction - Step 4")]
    [SerializeField, Tooltip("Panel huong dan nguoi choi ban trung mot vien bi doi thu.")]
    private GameObject introductionStepFourRoot;

    [Header("Tutorial Introduction - Step 5")]
    [SerializeField, Tooltip("Panel thong bao nguoi choi da hoan thanh huong dan.")]
    private GameObject introductionStepFiveRoot;
    [SerializeField, Tooltip("Nut da hieu de ket thuc tutorial va tro ve menu.")]
    private Button stepFiveUnderstoodButton;

    [Header("Animation")]
    [SerializeField] private float instructionFadeDuration = 0.24f;
    [SerializeField] private float shootHintPulseScale = 1.12f;
    [SerializeField] private float shootHintPulseDuration = 0.48f;
    [SerializeField, Range(1f, 1.25f)] private float understoodButtonPulseScale = 1.08f;
    [SerializeField, Min(0.1f)] private float understoodButtonPulseDuration = 0.55f;
    [SerializeField, Range(1f, 1.3f)] private float stepTwoHandPulseScale = 1.12f;
    [SerializeField, Min(0f)] private float stepTwoHandPressOffsetY = 7f;
    [SerializeField, Min(0.1f)] private float stepTwoHandPulseDuration = 0.38f;

    public event Action IntroductionDismissed;
    public event Action BallSkillPressed;
    public event Action CompletionAcknowledged;
    public bool IsIntroductionVisible { get; private set; }
    public bool IsBallSkillIntroductionVisible { get; private set; }
    public bool IsOpponentShotIntroductionVisible { get; private set; }
    public bool IsCompletionIntroductionVisible { get; private set; }

#if !UNITY_SERVER
    private Tween instructionTween;
    private Tween shootHintTween;
    private Tween understoodButtonTween;
    private Sequence stepTwoHandTween;
    private Sequence stepThreeHandTween;
#endif
    private Vector3 understoodButtonBaseScale = Vector3.one;
    private Vector3 stepTwoHandBaseScale = Vector3.one;
    private Vector2 stepTwoHandBasePosition;
    private bool hasStepTwoHandBasePose;
    private Vector3 stepThreeHandBaseScale = Vector3.one;
    private Vector2 stepThreeHandBasePosition;
    private bool hasStepThreeHandBasePose;
    private bool waitingForShootButtonPress;

    public bool Initialize(int requiredShots)
    {
        if (tutorialRoot != null)
        {
            tutorialRoot.SetActive(true);
        }

        SetBallInformationVisible(false);
        SetProgress(0, requiredShots);
        SetShootHintVisible(false);
        ResetStepThreeIntroduction();
        ResetStepFourIntroduction();
        ResetStepFiveIntroduction();

        if (instructionText == null)
        {
            Debug.LogWarning("CoreTutorialUIController: InstructionText is not assigned.");
        }

        if (progressText == null)
        {
            Debug.LogWarning("CoreTutorialUIController: ProgressText is not assigned.");
        }

        return ShowIntroduction();
    }

    public void ShowInstruction(string message)
    {
        if (instructionText == null)
        {
            return;
        }

        instructionText.text = message;
        if (instructionGroup == null)
        {
            return;
        }

#if !UNITY_SERVER
        instructionTween?.Kill();
        instructionGroup.alpha = 0f;
        instructionTween = instructionGroup
            .DOFade(1f, instructionFadeDuration)
            .SetUpdate(true);
#else
        instructionGroup.alpha = 1f;
#endif
    }

    public void SetProgress(int completedSteps, int requiredSteps)
    {
        if (progressText != null)
        {
            progressText.text = $"Buoc da hoan thanh: {completedSteps}/{requiredSteps}";
        }
    }

    public void SetShootHintVisible(bool visible)
    {
        if (shootHint == null)
        {
            return;
        }

#if !UNITY_SERVER
        shootHintTween?.Kill();
        shootHintTween = null;
#endif
        shootHint.localScale = Vector3.one;
        shootHint.gameObject.SetActive(visible);

#if !UNITY_SERVER
        if (visible)
        {
            shootHintTween = shootHint
                .DOScale(shootHintPulseScale, shootHintPulseDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }
#endif
    }

    private bool ShowIntroduction()
    {
        CacheStepTwoHandBasePose();
        ResetStepTwoIntroduction();
        if (introductionRoot == null)
        {
            Debug.LogWarning("CoreTutorialUIController: IntroductionRoot is not assigned.");
            return false;
        }

        if (understoodButton == null)
        {
            Debug.LogWarning("CoreTutorialUIController: UnderstoodButton is not assigned. The introduction cannot be dismissed.");
        }
        else
        {
            understoodButton.onClick.RemoveListener(ShowShootButtonIntroduction);
            understoodButton.onClick.AddListener(ShowShootButtonIntroduction);
            understoodButtonBaseScale = understoodButton.transform.localScale;
        }

        IsIntroductionVisible = true;
        introductionRoot.SetActive(true);
        PlayTutorialStepVoice(TutorialVoiceStepOne);
        StartUnderstoodButtonPulse();
        return true;
    }

    private void ShowShootButtonIntroduction()
    {
        if (!IsIntroductionVisible)
        {
            return;
        }

        StopUnderstoodButtonPulse();
        if (introductionRoot != null)
        {
            introductionRoot.SetActive(false);
        }

        if (introductionStepTwoRoot == null)
        {
            Debug.LogWarning("CoreTutorialUIController: IntroductionStepTwoRoot is not assigned.");
            CompleteIntroduction();
            return;
        }

        introductionStepTwoRoot.SetActive(true);
        PlayTutorialStepVoice(TutorialVoiceStepTwo);
        waitingForShootButtonPress = true;
        ShootBallJoystick.ShootButtonPressed -= HandleShootButtonPressed;
        ShootBallJoystick.ShootButtonPressed += HandleShootButtonPressed;
        StartStepTwoHandPulse();
    }

    private void HandleShootButtonPressed()
    {
        if (!IsIntroductionVisible || !waitingForShootButtonPress)
        {
            return;
        }

        CompleteIntroduction();
    }

    private void CompleteIntroduction()
    {
        IsIntroductionVisible = false;
        ResetStepTwoIntroduction();
        IntroductionDismissed?.Invoke();
    }

    public bool ShowBallSkillIntroduction(ActiveSkillSchema activeSkill)
    {
        ResetStepThreeIntroduction();
        if (introductionStepThreeRoot == null)
        {
            Debug.LogWarning("CoreTutorialUIController: IntroductionStepThreeRoot is not assigned.");
            return false;
        }

        if (stepThreeSkillButton == null)
        {
            Debug.LogWarning("CoreTutorialUIController: StepThreeSkillButton is not assigned.");
            return false;
        }

        introductionStepThreeRoot.SetActive(true);
        PlayTutorialStepVoice(TutorialVoiceStepThree);
        SetBallInformationVisible(true);
        stepThreeSkillButton.gameObject.SetActive(true);
        stepThreeSkillButton.onClick.RemoveListener(HandleBallSkillPressed);
        stepThreeSkillButton.onClick.AddListener(HandleBallSkillPressed);
        SetStepThreeSkillTexts(activeSkill);

        if (stepThreeSkillImage != null)
        {
            stepThreeSkillImage.gameObject.SetActive(true);
            StartCoroutine(AddressablesHelper.LoadSprite(
                $"{AddressablePaths.Root}/Skills/Ball/{TutorialBigBallSkillId}.png",
                sprite =>
                {
                    if (sprite != null && stepThreeSkillImage != null)
                    {
                        stepThreeSkillImage.sprite = sprite;
                    }
                }));
        }

        IsBallSkillIntroductionVisible = true;
        StartStepThreeHandPulse();
        return true;
    }

    public void HideBallSkillIntroduction()
    {
        ResetStepThreeIntroduction();
    }

    private void HandleBallSkillPressed()
    {
        if (IsBallSkillIntroductionVisible)
        {
            BallSkillPressed?.Invoke();
        }
    }

    public bool ShowOpponentShotIntroduction()
    {
        ResetStepFourIntroduction();
        if (introductionStepFourRoot == null)
        {
            Debug.LogWarning("CoreTutorialUIController: IntroductionStepFourRoot is not assigned.");
            return false;
        }

        IsOpponentShotIntroductionVisible = true;
        introductionStepFourRoot.SetActive(true);
        PlayTutorialStepVoice(TutorialVoiceStepFour);
        SetBallInformationVisible(true);
        return true;
    }

    public void HideOpponentShotIntroduction()
    {
        ResetStepFourIntroduction();
    }

    public bool ShowCompletionIntroduction()
    {
        ResetStepFiveIntroduction();
        if (introductionStepFiveRoot == null)
        {
            Debug.LogWarning("CoreTutorialUIController: IntroductionStepFiveRoot is not assigned.");
            return false;
        }

        if (stepFiveUnderstoodButton == null)
        {
            Debug.LogWarning("CoreTutorialUIController: StepFiveUnderstoodButton is not assigned.");
            return false;
        }

        IsCompletionIntroductionVisible = true;
        introductionStepFiveRoot.SetActive(true);
        PlayTutorialStepVoice(TutorialVoiceStepFive);
        SetBallInformationVisible(true);
        stepFiveUnderstoodButton.gameObject.SetActive(true);
        stepFiveUnderstoodButton.onClick.RemoveListener(HandleCompletionAcknowledged);
        stepFiveUnderstoodButton.onClick.AddListener(HandleCompletionAcknowledged);
        return true;
    }

    private void HandleCompletionAcknowledged()
    {
        if (!IsCompletionIntroductionVisible)
        {
            return;
        }

        ResetStepFiveIntroduction();
        CompletionAcknowledged?.Invoke();
    }

    private static void PlayTutorialStepVoice(int stepNumber)
    {
        SoundManager.Instance?.PlayTutorialStepVoice(stepNumber);
    }

    public void Shutdown()
    {
#if !UNITY_SERVER
        instructionTween?.Kill();
        instructionTween = null;
        shootHintTween?.Kill();
        shootHintTween = null;
#endif
        StopUnderstoodButtonPulse();
        ResetStepTwoIntroduction();
        ResetStepThreeIntroduction();
        ResetStepFourIntroduction();
        ResetStepFiveIntroduction();

        if (understoodButton != null)
        {
            understoodButton.onClick.RemoveListener(ShowShootButtonIntroduction);
        }

        IsIntroductionVisible = false;
        if (introductionRoot != null)
        {
            introductionRoot.SetActive(false);
        }

        if (tutorialRoot != null)
        {
            tutorialRoot.SetActive(false);
        }

        SetBallInformationVisible(false);
    }

    private void OnDestroy()
    {
#if !UNITY_SERVER
        instructionTween?.Kill();
        shootHintTween?.Kill();
        understoodButtonTween?.Kill();
        stepTwoHandTween?.Kill();
        stepThreeHandTween?.Kill();
#endif
        ShootBallJoystick.ShootButtonPressed -= HandleShootButtonPressed;

        if (understoodButton != null)
        {
            understoodButton.transform.localScale = understoodButtonBaseScale;
            understoodButton.onClick.RemoveListener(ShowShootButtonIntroduction);
        }

        if (stepTwoHandIcon != null && hasStepTwoHandBasePose)
        {
            stepTwoHandIcon.localScale = stepTwoHandBaseScale;
            stepTwoHandIcon.anchoredPosition = stepTwoHandBasePosition;
        }

        if (stepThreeSkillButton != null)
        {
            stepThreeSkillButton.onClick.RemoveListener(HandleBallSkillPressed);
        }

        if (stepFiveUnderstoodButton != null)
        {
            stepFiveUnderstoodButton.onClick.RemoveListener(HandleCompletionAcknowledged);
        }

        if (stepThreeHandIcon != null && hasStepThreeHandBasePose)
        {
            stepThreeHandIcon.localScale = stepThreeHandBaseScale;
            stepThreeHandIcon.anchoredPosition = stepThreeHandBasePosition;
        }
    }

    private void StartUnderstoodButtonPulse()
    {
        if (understoodButton == null)
        {
            return;
        }

        StopUnderstoodButtonPulse();
#if !UNITY_SERVER
        understoodButtonTween = understoodButton.transform
            .DOScale(understoodButtonBaseScale * understoodButtonPulseScale, understoodButtonPulseDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);
#endif
    }

    private void StartStepTwoHandPulse()
    {
        if (stepTwoHandIcon == null)
        {
            Debug.LogWarning("CoreTutorialUIController: StepTwoHandIcon is not assigned.");
            return;
        }

        CacheStepTwoHandBasePose();
        StopStepTwoHandPulse();
#if !UNITY_SERVER
        stepTwoHandTween = DOTween.Sequence()
            .Append(stepTwoHandIcon
                .DOScale(stepTwoHandBaseScale * stepTwoHandPulseScale, stepTwoHandPulseDuration)
                .SetEase(Ease.OutQuad))
            .Join(stepTwoHandIcon
                .DOAnchorPosY(stepTwoHandBasePosition.y - stepTwoHandPressOffsetY, stepTwoHandPulseDuration)
                .SetEase(Ease.OutQuad))
            .Append(stepTwoHandIcon
                .DOScale(stepTwoHandBaseScale, stepTwoHandPulseDuration)
                .SetEase(Ease.OutBack))
            .Join(stepTwoHandIcon
                .DOAnchorPos(stepTwoHandBasePosition, stepTwoHandPulseDuration)
                .SetEase(Ease.OutBack))
            .SetLoops(-1)
            .SetUpdate(true);
#endif
    }

    private void StopStepTwoHandPulse()
    {
#if !UNITY_SERVER
        stepTwoHandTween?.Kill();
        stepTwoHandTween = null;
#endif
        if (stepTwoHandIcon != null && hasStepTwoHandBasePose)
        {
            stepTwoHandIcon.localScale = stepTwoHandBaseScale;
            stepTwoHandIcon.anchoredPosition = stepTwoHandBasePosition;
        }
    }

    private void StartStepThreeHandPulse()
    {
        if (stepThreeHandIcon == null)
        {
            Debug.LogWarning("CoreTutorialUIController: StepThreeHandIcon is not assigned.");
            return;
        }

        CacheStepThreeHandBasePose();
        StopStepThreeHandPulse();
#if !UNITY_SERVER
        stepThreeHandTween = DOTween.Sequence()
            .Append(stepThreeHandIcon
                .DOScale(stepThreeHandBaseScale * stepTwoHandPulseScale, stepTwoHandPulseDuration)
                .SetEase(Ease.OutQuad))
            .Join(stepThreeHandIcon
                .DOAnchorPosY(stepThreeHandBasePosition.y - stepTwoHandPressOffsetY, stepTwoHandPulseDuration)
                .SetEase(Ease.OutQuad))
            .Append(stepThreeHandIcon
                .DOScale(stepThreeHandBaseScale, stepTwoHandPulseDuration)
                .SetEase(Ease.OutBack))
            .Join(stepThreeHandIcon
                .DOAnchorPos(stepThreeHandBasePosition, stepTwoHandPulseDuration)
                .SetEase(Ease.OutBack))
            .SetLoops(-1)
            .SetUpdate(true);
#endif
    }

    private void StopStepThreeHandPulse()
    {
#if !UNITY_SERVER
        stepThreeHandTween?.Kill();
        stepThreeHandTween = null;
#endif
        if (stepThreeHandIcon != null && hasStepThreeHandBasePose)
        {
            stepThreeHandIcon.localScale = stepThreeHandBaseScale;
            stepThreeHandIcon.anchoredPosition = stepThreeHandBasePosition;
        }
    }

    private void CacheStepTwoHandBasePose()
    {
        if (stepTwoHandIcon == null || hasStepTwoHandBasePose)
        {
            return;
        }

        stepTwoHandBaseScale = stepTwoHandIcon.localScale;
        stepTwoHandBasePosition = stepTwoHandIcon.anchoredPosition;
        hasStepTwoHandBasePose = true;
    }

    private void CacheStepThreeHandBasePose()
    {
        if (stepThreeHandIcon == null || hasStepThreeHandBasePose)
        {
            return;
        }

        stepThreeHandBaseScale = stepThreeHandIcon.localScale;
        stepThreeHandBasePosition = stepThreeHandIcon.anchoredPosition;
        hasStepThreeHandBasePose = true;
    }

    private void ResetStepTwoIntroduction()
    {
        waitingForShootButtonPress = false;
        ShootBallJoystick.ShootButtonPressed -= HandleShootButtonPressed;
        StopStepTwoHandPulse();
        if (introductionStepTwoRoot != null)
        {
            introductionStepTwoRoot.SetActive(false);
        }
    }

    private void ResetStepThreeIntroduction()
    {
        IsBallSkillIntroductionVisible = false;
        StopStepThreeHandPulse();

        if (stepThreeSkillButton != null)
        {
            stepThreeSkillButton.onClick.RemoveListener(HandleBallSkillPressed);
            stepThreeSkillButton.gameObject.SetActive(false);
        }

        if (stepThreeSkillImage != null)
        {
            stepThreeSkillImage.gameObject.SetActive(false);
        }

        if (stepThreeSkillNameText != null)
        {
            stepThreeSkillNameText.text = string.Empty;
            stepThreeSkillNameText.gameObject.SetActive(false);
        }

        if (stepThreeSkillDescriptionText != null)
        {
            stepThreeSkillDescriptionText.text = string.Empty;
            stepThreeSkillDescriptionText.gameObject.SetActive(false);
        }

        if (introductionStepThreeRoot != null)
        {
            introductionStepThreeRoot.SetActive(false);
        }
    }

    private void ResetStepFourIntroduction()
    {
        IsOpponentShotIntroductionVisible = false;
        if (introductionStepFourRoot != null)
        {
            introductionStepFourRoot.SetActive(false);
        }
    }

    private void ResetStepFiveIntroduction()
    {
        IsCompletionIntroductionVisible = false;
        SetBallInformationVisible(false);
        if (stepFiveUnderstoodButton != null)
        {
            stepFiveUnderstoodButton.onClick.RemoveListener(HandleCompletionAcknowledged);
            stepFiveUnderstoodButton.gameObject.SetActive(false);
        }

        if (introductionStepFiveRoot != null)
        {
            introductionStepFiveRoot.SetActive(false);
        }
    }

    private void SetBallInformationVisible(bool visible)
    {
        if (tutorialBallInformationRoot != null)
        {
            tutorialBallInformationRoot.SetActive(visible);
        }
    }

    private void StopUnderstoodButtonPulse()
    {
#if !UNITY_SERVER
        understoodButtonTween?.Kill();
        understoodButtonTween = null;
#endif
        if (understoodButton != null)
        {
            understoodButton.transform.localScale = understoodButtonBaseScale;
        }
    }

    private void SetStepThreeSkillTexts(ActiveSkillSchema activeSkill)
    {
        string nameKey = activeSkill != null
            && activeSkill.GenCode == TutorialBigBallSkillId
            && !string.IsNullOrWhiteSpace(activeSkill.GenName)
                ? activeSkill.GenName
                : TutorialBigBallSkillNameKey;
        string skillName = ResolveLocalizedText(nameKey);
        if (stepThreeSkillNameText != null)
        {
            stepThreeSkillNameText.richText = true;
            stepThreeSkillNameText.enableWordWrapping = true;
            stepThreeSkillNameText.text = skillName;
            stepThreeSkillNameText.gameObject.SetActive(!string.IsNullOrWhiteSpace(skillName));
        }

        TextMeshProUGUI descriptionText = ResolveStepThreeSkillDescriptionText();
        if (descriptionText == null)
        {
            return;
        }

        string descriptionKey = activeSkill != null
            && activeSkill.GenCode == TutorialBigBallSkillId
            && !string.IsNullOrWhiteSpace(activeSkill.description)
                ? activeSkill.description
                : TutorialBigBallSkillDescriptionKey;
        string localizedDescription = ResolveLocalizedText(descriptionKey);
        string description = ItemVisualHelper.ConvertSimpleHtmlToTmp(localizedDescription);

        descriptionText.richText = true;
        descriptionText.enableWordWrapping = true;
        descriptionText.text = description;
        descriptionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(description));
    }

    private TextMeshProUGUI ResolveStepThreeSkillDescriptionText()
    {
        if (stepThreeSkillDescriptionText != null)
        {
            return stepThreeSkillDescriptionText;
        }

        if (introductionStepThreeRoot == null)
        {
            return null;
        }

        Transform parent = stepThreeSkillButton != null && stepThreeSkillButton.transform.parent != null
            ? stepThreeSkillButton.transform.parent
            : introductionStepThreeRoot.transform;
        GameObject textObject = new GameObject("TutorialBallSkillDescription", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        RectTransform buttonRect = stepThreeSkillButton != null
            ? stepThreeSkillButton.GetComponent<RectTransform>()
            : null;
        if (buttonRect != null)
        {
            textRect.anchorMin = buttonRect.anchorMin;
            textRect.anchorMax = buttonRect.anchorMax;
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.sizeDelta = new Vector2(460f, 96f);
            float buttonHeight = Mathf.Max(buttonRect.rect.height, buttonRect.sizeDelta.y);
            textRect.anchoredPosition = buttonRect.anchoredPosition - new Vector2(0f, buttonHeight * 0.5f + 16f);
        }
        else
        {
            textRect.anchorMin = new Vector2(0.5f, 0f);
            textRect.anchorMax = new Vector2(0.5f, 0f);
            textRect.pivot = new Vector2(0.5f, 0f);
            textRect.sizeDelta = new Vector2(460f, 96f);
            textRect.anchoredPosition = new Vector2(0f, 96f);
        }

        stepThreeSkillDescriptionText = textObject.GetComponent<TextMeshProUGUI>();
        stepThreeSkillDescriptionText.alignment = TextAlignmentOptions.Center;
        stepThreeSkillDescriptionText.fontSize = 22f;
        stepThreeSkillDescriptionText.color = Color.white;
        stepThreeSkillDescriptionText.raycastTarget = false;
        if (instructionText != null && instructionText.font != null)
        {
            stepThreeSkillDescriptionText.font = instructionText.font;
        }

        return stepThreeSkillDescriptionText;
    }

    private static string ResolveLocalizedText(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText(key)
            : key;
    }
}
