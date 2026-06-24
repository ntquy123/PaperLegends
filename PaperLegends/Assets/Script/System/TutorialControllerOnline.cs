#if !UNITY_SERVER
using System.Collections;
using TMPro;
using UnityEngine;

public class TutorialControllerOnline : MonoBehaviour
{
    public enum TutorialStep
    {
        Intro,
        ForceBar,
        Finished
    }

    [Header("UI References")]
    [SerializeField] private GameObject overlayPanel;
    [SerializeField] private RectTransform arrow;
    [SerializeField] private TextMeshProUGUI tutorialText;
    [SerializeField] private RectTransform forceBarTarget;

    [Header("Timing")]
    [SerializeField] private float introDuration = 2f;
    [SerializeField] private float forceBarDuration = 3f;

    [Header("Arrow Movement")]
    [SerializeField] private Vector3 arrowOffset = new Vector3(0f, 150f, 0f);
    [SerializeField] private float arrowFloatAmplitude = 6f;
    [SerializeField] private float arrowFloatSpeed = 5f;

    public TutorialStep CurrentStep { get; private set; } = TutorialStep.Finished;

    private Coroutine tutorialRoutine;
    private Vector3 arrowBaseLocalPosition;
    private bool animateArrow;
    private bool hasShownExamTutorial;

    private void Awake()
    {
        SetVisible(false);
    }

    private void Update()
    {
        if (!animateArrow || arrow == null)
        {
            return;
        }

        float offset = Mathf.Sin(Time.unscaledTime * arrowFloatSpeed) * arrowFloatAmplitude;
        arrow.localPosition = arrowBaseLocalPosition + new Vector3(0f, offset, 0f);
    }

    public void BeginTutorialIfLevelOne(int playerLevel)
    {
        if (playerLevel != 1)
        {
            return;
        }

        BeginTutorial();
    }

    public void UpdateTutorialForExam(int playerLevel, StatusLoadingGame status)
    {
        if (status != StatusLoadingGame.isExam)
        {
            if (tutorialRoutine != null || CurrentStep != TutorialStep.Finished)
            {
                HideTutorial();
            }
            return;
        }

        if (hasShownExamTutorial)
            return;

        if (playerLevel == 1)
        {
            BeginTutorial();
            hasShownExamTutorial = true;
        }
    }

    public void BeginTutorial()
    {
        if (tutorialRoutine != null)
        {
            return;
        }

        hasShownExamTutorial = true;
        tutorialRoutine = StartCoroutine(RunTutorial());
    }

    public void HideTutorial()
    {
        if (tutorialRoutine != null)
        {
            StopCoroutine(tutorialRoutine);
            tutorialRoutine = null;
        }

        CurrentStep = TutorialStep.Finished;
        SetVisible(false);
    }

    private IEnumerator RunTutorial()
    {
        SetVisible(true);
        SetStepIntro();
        yield return new WaitForSecondsRealtime(introDuration);

        SetStepForceBar();
        yield return new WaitForSecondsRealtime(forceBarDuration);

        CurrentStep = TutorialStep.Finished;
        SetVisible(false);
        tutorialRoutine = null;
    }

    private void SetStepIntro()
    {
        CurrentStep = TutorialStep.Intro;
        string tutorialTextTrans = LocalizationManager.Instance.GetText("shooting_instructions");
        if (tutorialText != null)
        {
            tutorialText.text = tutorialTextTrans;
        }

        if (arrow != null)
        {
            arrow.gameObject.SetActive(false);
        }

        animateArrow = false;
    }

    private void SetStepForceBar()
    {
        CurrentStep = TutorialStep.ForceBar;
        string tutorialTextTrans = LocalizationManager.Instance.GetText("bar_power_instructions");
        if (tutorialText != null)
        {
            tutorialText.text = tutorialTextTrans;
        }

        if (arrow != null)
        {
            arrow.gameObject.SetActive(true);
            PositionArrow();
            animateArrow = true;
        }
    }

    private void PositionArrow()
    {
        if (arrow == null || forceBarTarget == null)
        {
            return;
        }

        arrow.position = forceBarTarget.position + arrowOffset;
        arrowBaseLocalPosition = arrow.localPosition;
    }

    private void SetVisible(bool visible)
    {
        if (overlayPanel != null)
        {
            overlayPanel.SetActive(visible);
        }

        if (tutorialText != null)
        {
            tutorialText.gameObject.SetActive(visible);
        }

        if (arrow != null)
        {
            arrow.gameObject.SetActive(visible && CurrentStep == TutorialStep.ForceBar);
        }

        if (!visible)
        {
            animateArrow = false;
        }
    }
}
#endif
