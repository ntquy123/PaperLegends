using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RadialSkills
{
    [Header("References")]
    [SerializeField] private RectTransform container;
    [SerializeField] private RectTransform centerTarget;

    [Header("Layout")]
    [SerializeField]  private float radius = 300f;
    private bool useTargetSize = true;
    private float radiusMultiplier = 1.2f;
    [SerializeField] private float minRadius = 180f;
    [SerializeField] private float startAngle = 190f;
    [SerializeField] private float arcAngle = 90f;
    [SerializeField] private bool clockwise = true;

    private readonly List<RectTransform> items = new List<RectTransform>();

    public void Initialize(RectTransform fallbackContainer, RectTransform fallbackCenter)
    {
        if (container == null)
            container = fallbackContainer;

        if (centerTarget == null)
            centerTarget = fallbackCenter;
    }

    public void UpdateCenterTarget(RectTransform target)
    {
        if (target != null)
            centerTarget = target;
    }

    public IReadOnlyList<RectTransform> PrepareItems(int count, GameObject prefab)
    {
        if (container == null || prefab == null)
            return items;

        RemoveNullItems();

        while (items.Count < count)
        {
            var instance = Object.Instantiate(prefab, container);
            var rect = instance.GetComponent<RectTransform>();
            items.Add(rect);
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                items[i].gameObject.SetActive(i < count);
        }

        return items;
    }

    public void Layout(int count)
    {
        if (container == null || centerTarget == null || count <= 0)
            return;

        RemoveNullItems();

        Vector2 center = container.InverseTransformPoint(centerTarget.position);
        float computedRadius = radius;

        if (useTargetSize)
        {
            float baseSize = Mathf.Max(centerTarget.rect.width, centerTarget.rect.height);
            computedRadius = Mathf.Max(minRadius, baseSize * radiusMultiplier);
        }

        float normalizedArc = Mathf.Clamp(arcAngle, 0f, 360f);
        float angleStep = count == 1
            ? 0f
            : normalizedArc >= 360f
                ? normalizedArc / count
                : normalizedArc / Mathf.Max(1, count - 1);
        float direction = clockwise ? -1f : 1f;

        for (int i = 0; i < count; i++)
        {
            var rect = items[i];
            if (rect == null)
                continue;

            float angle = startAngle + direction * angleStep * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * computedRadius;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center + offset;
        }
    }

    public void HideAll()
    {
        RemoveNullItems();

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                items[i].gameObject.SetActive(false);
        }
    }

    private void RemoveNullItems()
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] == null)
                items.RemoveAt(i);
        }
    }
}
