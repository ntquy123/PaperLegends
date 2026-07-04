using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ScoreboardUI : MonoBehaviour
{
    [SerializeField] private Transform rowsRoot;
    [SerializeField] private ScoreboardRowUI rowPrefab;
    [SerializeField] private TextMeshProUGUI targetScoreText;
    [SerializeField] private GameObject winnerPanel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private DrumObjective drumObjective;
    [SerializeField] private Slider drumCaptureProgressBar;
    [SerializeField] private Image drumCaptureRadialFill;
    [SerializeField] private RectTransform drumCaptureSpinner;
    [SerializeField, Min(0f)] private float drumCaptureSpinnerDegreesPerSecond = 360f;

    private readonly List<ScoreboardRowUI> _rows = new List<ScoreboardRowUI>();
    private int _lastScoreRevision = -1;
    private int _lastWinnerPlayerId = -1;

    private void Update()
    {
        GameScoreManager scoreManager = GameScoreManager.Instance;
        if (scoreManager == null)
            return;

        if (_lastScoreRevision != scoreManager.ScoreRevision)
        {
            _lastScoreRevision = scoreManager.ScoreRevision;
            RefreshScoreboard(scoreManager);
        }

        RefreshDrumProgress(scoreManager);
        RefreshWinnerPanel(scoreManager);
    }

    private void RefreshScoreboard(GameScoreManager scoreManager)
    {
        if (targetScoreText != null)
            targetScoreText.text = $"{scoreManager.TargetScore}";

        List<PlayerScoreData> scores = scoreManager.GetOrderedScores();
        EnsureRowCount(scores.Count);

        for (int i = 0; i < _rows.Count; i++)
        {
            bool active = i < scores.Count;
            if (_rows[i] != null && _rows[i].gameObject.activeSelf != active)
                _rows[i].gameObject.SetActive(active);

            if (!active || _rows[i] == null)
                continue;

            PlayerScoreData data = scores[i];
            bool isLeader = data.PlayerId > 0 && data.PlayerId == scoreManager.CurrentLeaderPlayerId;
            _rows[i].Bind(data, isLeader);
        }
    }

    private void EnsureRowCount(int count)
    {
        if (rowPrefab == null || rowsRoot == null)
            return;

        while (_rows.Count < count)
        {
            ScoreboardRowUI row = Instantiate(rowPrefab, rowsRoot);
            _rows.Add(row);
        }
    }

    private void RefreshDrumProgress(GameScoreManager scoreManager)
    {
        if ((scoreManager == null || !scoreManager.DrumObjectiveIsActive) && drumObjective == null)
            drumObjective = FindFirstObjectByType<DrumObjective>(FindObjectsInactive.Include);

        if (drumCaptureProgressBar == null && drumCaptureRadialFill == null && drumCaptureSpinner == null)
            return;

        bool hasScoreManagerDrumState = scoreManager != null
            && scoreManager.DrumObjectiveIsActive
            && scoreManager.DrumObjectiveCapturingPlayerId > 0
            && scoreManager.DrumObjectiveCaptureProgress01 > 0f;

        bool hasLegacyDrumState = !hasScoreManagerDrumState
            && drumObjective != null
            && drumObjective.IsActive
            && drumObjective.CapturingPlayerId > 0
            && drumObjective.CaptureProgress01 > 0f;

        bool showProgress = hasScoreManagerDrumState || hasLegacyDrumState;

        if (drumCaptureProgressBar != null && drumCaptureProgressBar.gameObject.activeSelf != showProgress)
            drumCaptureProgressBar.gameObject.SetActive(showProgress);

        if (drumCaptureRadialFill != null && drumCaptureRadialFill.gameObject.activeSelf != showProgress)
            drumCaptureRadialFill.gameObject.SetActive(showProgress);

        if (drumCaptureSpinner != null && drumCaptureSpinner.gameObject.activeSelf != showProgress)
            drumCaptureSpinner.gameObject.SetActive(showProgress);

        if (showProgress)
        {
            float progress = hasScoreManagerDrumState
                ? Mathf.Clamp01(scoreManager.DrumObjectiveCaptureProgress01)
                : Mathf.Clamp01(drumObjective.CaptureProgress01);
            if (drumCaptureProgressBar != null)
                drumCaptureProgressBar.value = progress;

            if (drumCaptureRadialFill != null)
                drumCaptureRadialFill.fillAmount = progress;

            if (drumCaptureSpinner != null && drumCaptureSpinnerDegreesPerSecond > 0f)
                drumCaptureSpinner.Rotate(0f, 0f, -drumCaptureSpinnerDegreesPerSecond * Time.unscaledDeltaTime);
        }
        else
        {
            if (drumCaptureProgressBar != null)
                drumCaptureProgressBar.value = 0f;

            if (drumCaptureRadialFill != null)
                drumCaptureRadialFill.fillAmount = 0f;
        }
    }

    private void RefreshWinnerPanel(GameScoreManager scoreManager)
    {
        bool ended = scoreManager.IsGameEnded;
        if (winnerPanel != null && winnerPanel.activeSelf != ended)
            winnerPanel.SetActive(ended);

        if (!ended || winnerText == null || _lastWinnerPlayerId == scoreManager.WinnerPlayerId)
            return;

        _lastWinnerPlayerId = scoreManager.WinnerPlayerId;
        winnerText.text = $"Winner: Player {scoreManager.WinnerPlayerId}";
    }
}

[System.Serializable]
public class ScoreboardRowUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI killAssistDeathText;
    [SerializeField] private GameObject leaderIcon;

    public void Bind(PlayerScoreData data, bool isLeader)
    {
        if (playerNameText != null)
        {
            string crown = isLeader ? "\U0001F451 " : string.Empty;
            playerNameText.text = $"{crown}Player {data.PlayerId}";
        }

        if (scoreText != null)
            scoreText.text = data.Score.ToString();

        if (killAssistDeathText != null)
            killAssistDeathText.text = $"{data.Kills}/{data.Assists}/{data.Deaths}";

        if (leaderIcon != null && leaderIcon.activeSelf != isLeader)
            leaderIcon.SetActive(isLeader);
    }
}
