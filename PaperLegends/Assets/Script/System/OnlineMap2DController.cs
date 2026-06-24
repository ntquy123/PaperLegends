#if !UNITY_SERVER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OnlineMap2DController : MonoBehaviour
{
    [Header("MAP UI CONFIG")]
    [SerializeField, Tooltip("Panel chua ban do 2D. De trong neu component duoc gan truc tiep len panel.")]
    private GameObject mapPanel;
    [SerializeField, Tooltip("Vung RectTransform hien thi pham vi PlayArea. De trong de dung RectTransform cua object nay.")]
    private RectTransform mapRect;
    [SerializeField, Tooltip("Nut dong tren panel map, neu co.")]
    private Button closeButton;
    [SerializeField] private bool hideOnStart = true;

    [Header("WORLD AREA CONFIG")]
    [SerializeField, Tooltip("Collider PlayArea trong map 3D, dung tam collider lam tam ban do.")]
    private BoxCollider playArea;
    [SerializeField, Tooltip("Pham vi the gioi hien thi tren map, lay tam tai PlayArea. X ung voi world X, Y ung voi world Z.")]
    private Vector2 mapWorldSize = new Vector2(10f, 10f);
    [SerializeField, Tooltip("Bat tuy chon nay neu UI map chi mo ta pham vi nam ben trong vong PlayArea.")]
    private bool usePlayAreaBoundsAsMapSize;
    [SerializeField] private bool invertHorizontal;
    [SerializeField] private bool invertVertical;
    [SerializeField, Tooltip("Giu marker o mep ban do neu bi ra ngoai PlayArea.")]
    private bool clampMarkersToMap = true;

    [Header("PLAYER MARKER CONFIG")]
    [SerializeField, Tooltip("Image cham danh dau bi cua nguoi choi hien tai.")]
    private Image localPlayerMarker;
    [SerializeField, Tooltip("Cac Image cham danh dau bi cua doi thu, theo thu tu luot.")]
    private List<Image> opponentMarkers = new List<Image>();
    [SerializeField] private bool applyMarkerColors = true;
    [SerializeField] private Color localPlayerColor = Color.green;
    [SerializeField] private Color opponentColor = Color.red;

    private bool hasExplicitVisibilityRequest;
    private bool hasPreparedMarkers;

    public bool IsVisible
    {
        get
        {
            var panel = ResolvePanel();
            return panel != null && panel.activeSelf;
        }
    }

    private void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }

        PrepareMarkers();

        if (hideOnStart && !hasExplicitVisibilityRequest)
            SetVisibleInternal(false);
        else if (IsVisible)
            RefreshMarkers();
    }

    private void LateUpdate()
    {
        if (IsVisible)
            RefreshMarkers();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Hide);
    }

    public void Toggle()
    {
        SetVisible(!IsVisible);
    }

    public void Show()
    {
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        hasExplicitVisibilityRequest = true;
        SetVisibleInternal(visible);
    }

    private void SetVisibleInternal(bool visible)
    {
        var panel = ResolvePanel();
        if (panel == null)
            return;

        if (panel.activeSelf != visible)
            panel.SetActive(visible);

        if (visible)
        {
            PrepareMarkers();
            RefreshMarkers();
        }
        else
        {
            SetAllMarkersActive(false);
        }
    }

    private void RefreshMarkers()
    {
        var area = ResolvePlayArea();
        var displayRect = ResolveMapRect();
        var manager = NetworkObjectManager.Instance;
        if (area == null || displayRect == null || manager == null || !manager.IsNetworkStateReady)
        {
            SetAllMarkersActive(false);
            return;
        }

        PrepareMarkers();
        SetAllMarkersActive(false);

        int localPlayerId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        int opponentMarkerIndex = 0;

        foreach (var player in manager.GetOrderedPlayerInfos())
        {
            var ball = manager.GetActiveBallObject(player.playerId);
            if (ball == null)
                continue;

            if (player.playerId == localPlayerId)
            {
                UpdateMarker(localPlayerMarker, ball.transform.position, area, displayRect);
                continue;
            }

            if (opponentMarkerIndex >= opponentMarkers.Count)
                continue;

            UpdateMarker(opponentMarkers[opponentMarkerIndex], ball.transform.position, area, displayRect);
            opponentMarkerIndex++;
        }
    }

    private void UpdateMarker(Image marker, Vector3 worldPosition, BoxCollider area, RectTransform displayRect)
    {
        if (marker == null)
            return;

        Bounds areaBounds = area.bounds;
        Vector3 relativePosition = worldPosition - areaBounds.center;
        float width = usePlayAreaBoundsAsMapSize ? areaBounds.size.x : mapWorldSize.x;
        float height = usePlayAreaBoundsAsMapSize ? areaBounds.size.z : mapWorldSize.y;
        width = Mathf.Abs(width);
        height = Mathf.Abs(height);
        if (width <= Mathf.Epsilon || height <= Mathf.Epsilon)
        {
            marker.gameObject.SetActive(false);
            return;
        }

        float normalizedX = relativePosition.x / width + 0.5f;
        float normalizedY = relativePosition.z / height + 0.5f;
        if (invertHorizontal)
            normalizedX = 1f - normalizedX;
        if (invertVertical)
            normalizedY = 1f - normalizedY;
        if (clampMarkersToMap)
        {
            normalizedX = Mathf.Clamp01(normalizedX);
            normalizedY = Mathf.Clamp01(normalizedY);
        }

        Rect rect = displayRect.rect;
        marker.rectTransform.anchoredPosition = new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, normalizedX),
            Mathf.Lerp(rect.yMin, rect.yMax, normalizedY));
        marker.gameObject.SetActive(true);
    }

    private void PrepareMarkers()
    {
        if (hasPreparedMarkers)
            return;

        var displayRect = ResolveMapRect();
        if (displayRect == null)
            return;

        PrepareMarker(localPlayerMarker, displayRect, localPlayerColor);
        foreach (var marker in opponentMarkers)
            PrepareMarker(marker, displayRect, opponentColor);

        hasPreparedMarkers = true;
    }

    private void PrepareMarker(Image marker, RectTransform displayRect, Color color)
    {
        if (marker == null)
            return;

        RectTransform markerRect = marker.rectTransform;
        if (markerRect.parent != displayRect)
            markerRect.SetParent(displayRect, false);

        markerRect.anchorMin = displayRect.pivot;
        markerRect.anchorMax = displayRect.pivot;
        if (applyMarkerColors)
            marker.color = color;
    }

    private void SetAllMarkersActive(bool active)
    {
        if (localPlayerMarker != null)
            localPlayerMarker.gameObject.SetActive(active);

        foreach (var marker in opponentMarkers)
        {
            if (marker != null)
                marker.gameObject.SetActive(active);
        }
    }

    private GameObject ResolvePanel()
    {
        return mapPanel != null ? mapPanel : gameObject;
    }

    private RectTransform ResolveMapRect()
    {
        if (mapRect != null)
            return mapRect;

        return transform as RectTransform;
    }

    private BoxCollider ResolvePlayArea()
    {
        if (playArea != null)
            return playArea;

        return GameSessionClientLocal.Instance != null ? GameSessionClientLocal.Instance.playArea : null;
    }
}
#endif
