using UnityEngine;

/// <summary>
/// Helper for spawning an UI arrow with a label that points to a world transform.
/// </summary>
public static class ArrowTextHelper
{
    /// <summary>
    /// Create an arrow UI element that follows a target and shows a label.
    /// </summary>
    /// <param name="target">World transform to follow.</param>
    /// <param name="text">Text to display next to the arrow.</param>
    /// <returns>The instantiated arrow object or null if creation failed.</returns>
    public static GameObject ShowArrow(Transform target, string text, GameObject prefab)
    {
        if (target == null || !ClientGameplayBridge.UI.HasInstance()  )
            return null;

        if (prefab == null)
            return null;

        var canvasTransform = ClientGameplayBridge.UI.GetCanvasTransform();
        if (canvasTransform == null)
            return null;

        var arrowObj = Object.Instantiate(prefab, canvasTransform);
        var arrowUI = arrowObj.GetComponent<PlayerArrowUI>();
        if (arrowUI != null)
        {
            arrowUI.target = target;
            // Try to offset the arrow to appear above the object. This is
            // especially useful for round objects like balls where the
            // origin is at the center.
            var renderer = target.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                float yOffset = renderer.bounds.extents.y;
                arrowUI.offset = new Vector3(0f, yOffset, 0f);
            }

            if (arrowUI.label != null)
                arrowUI.label.text = text;
        }

        return arrowObj;
    }
}

