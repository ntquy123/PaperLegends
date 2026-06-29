using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PaperLegendCharacterStatusIndicator : MonoBehaviour
{
    [SerializeField] private PaperLegendCharacterNetworkHandler target;
    [SerializeField] private bool autoCreateDefaultUi = true;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.95f, 0f);
    [SerializeField, Min(0.001f)] private float worldScale = 0.01f;
    [SerializeField] private Color stunDotColor = new Color(1f, 0.92f, 0.2f, 1f);
    [SerializeField, Min(0.05f)] private float bobAmplitude = 0.08f;
    [SerializeField, Min(0.1f)] private float bobSpeed = 5.5f;
    [SerializeField, Min(0.1f)] private float spinSpeed = 120f;

    private GameObject _generatedRoot;
    private RectTransform _dotsRoot;
    private Image[] _dots;
    private Camera _cachedCamera;
    private bool _wasStunned;

    public static PaperLegendCharacterStatusIndicator EnsureFor(PaperLegendCharacterNetworkHandler character)
    {
#if UNITY_SERVER
        return null;
#else
        if (character == null)
            return null;

        PaperLegendCharacterStatusIndicator indicator = character.GetComponentInChildren<PaperLegendCharacterStatusIndicator>(true);
        if (indicator == null)
            indicator = character.gameObject.AddComponent<PaperLegendCharacterStatusIndicator>();

        indicator.Bind(character);
        return indicator;
#endif
    }

    public void Bind(PaperLegendCharacterNetworkHandler character)
    {
        target = character;
        EnsureUi();
        RefreshImmediate();
    }

    private void Awake()
    {
        if (target == null)
            target = GetComponentInParent<PaperLegendCharacterNetworkHandler>();

        EnsureUi();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            SetVisible(false);
            return;
        }

        EnsureUi();
        bool stunned = target.IsStunned;
        SetVisible(stunned);
        if (!stunned)
        {
            _wasStunned = false;
            return;
        }

        UpdateWorldTransform();
        AnimateDots();
        _wasStunned = true;
    }

    private void OnDestroy()
    {
        if (_generatedRoot != null)
            Destroy(_generatedRoot);
    }

    public void RefreshImmediate()
    {
        if (target == null)
        {
            SetVisible(false);
            return;
        }

        SetVisible(target.IsStunned);
        UpdateWorldTransform();
    }

    private void EnsureUi()
    {
        if (!autoCreateDefaultUi || (_generatedRoot != null && _dots != null && _dots.Length > 0))
            return;

        if (_generatedRoot != null)
            return;

        _generatedRoot = new GameObject("PaperLegendStatusIndicator_Runtime", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = _generatedRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 320;

        RectTransform rootRect = _generatedRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(72f, 24f);
        rootRect.localScale = Vector3.one * worldScale;

        _dotsRoot = new GameObject("StunDots", typeof(RectTransform)).GetComponent<RectTransform>();
        _dotsRoot.SetParent(rootRect, false);
        _dotsRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _dotsRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _dotsRoot.pivot = new Vector2(0.5f, 0.5f);
        _dotsRoot.anchoredPosition = Vector2.zero;
        _dotsRoot.sizeDelta = new Vector2(72f, 24f);

        _dots = new Image[3];
        float[] xPositions = { -18f, 0f, 18f };
        for (int i = 0; i < _dots.Length; i++)
        {
            GameObject dotObject = new GameObject($"Dot_{i + 1}", typeof(RectTransform), typeof(Image));
            RectTransform dotRect = dotObject.GetComponent<RectTransform>();
            dotRect.SetParent(_dotsRoot, false);
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.anchoredPosition = new Vector2(xPositions[i], 0f);
            dotRect.sizeDelta = new Vector2(14f, 14f);

            Image dotImage = dotObject.GetComponent<Image>();
            dotImage.color = stunDotColor;
            dotImage.raycastTarget = false;
            _dots[i] = dotImage;
        }
    }

    private void SetVisible(bool visible)
    {
        if (_generatedRoot != null)
            _generatedRoot.SetActive(visible);
    }

    private void UpdateWorldTransform()
    {
        if (_generatedRoot == null || target == null)
            return;

        Camera cam = ResolveCamera();
        if (cam == null)
            return;

        Vector3 anchor = target.transform.position + worldOffset;
        _generatedRoot.transform.position = anchor;
        _generatedRoot.transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
    }

    private void AnimateDots()
    {
        if (_dotsRoot == null || _dots == null)
            return;

        float bob = Mathf.Sin(Time.unscaledTime * bobSpeed) * bobAmplitude;
        _dotsRoot.localPosition = new Vector3(0f, bob, 0f);
        _dotsRoot.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * spinSpeed);

        for (int i = 0; i < _dots.Length; i++)
        {
            if (_dots[i] == null)
                continue;

            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * bobSpeed + i * 0.8f);
            _dots[i].transform.localScale = Vector3.one * pulse;
        }
    }

    private Camera ResolveCamera()
    {
        if (_cachedCamera != null && _cachedCamera.isActiveAndEnabled)
            return _cachedCamera;

        _cachedCamera = Camera.main;
        return _cachedCamera;
    }
}
