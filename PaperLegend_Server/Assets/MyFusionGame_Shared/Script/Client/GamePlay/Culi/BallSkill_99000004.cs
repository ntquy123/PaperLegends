using System.Collections;
using UnityEngine;

public class BallSkill_99000004 : MonoBehaviour, IFBall
{
    public int level;
    public GameObject trapPreviewPrefab, trapPrefab;
    public float[] trapRadiusByLevel, launchForceByLevel;

    int remainingTurns;
    bool trapActive;
    GameObject placedTrap;
    Coroutine placingRoutine;

    public void SetStatusUltimate() { }

    public void ShootEffect() { }

    public void StopShootEffect() { }

    public void ShootUltimate()
    {
        if (trapPreviewPrefab == null || trapPrefab == null)
            return;

        if (placingRoutine != null)
            StopCoroutine(placingRoutine);
        placingRoutine = StartCoroutine(PlaceTrapRoutine());
    }

    IEnumerator PlaceTrapRoutine()
    {
        GameObject preview = Instantiate(trapPreviewPrefab);
        ApplyOwnerLayer(preview);

        while (!Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
                preview.transform.position = hit.point;
            yield return null;
        }

        placedTrap = Instantiate(trapPrefab, preview.transform.position, Quaternion.identity);
        ApplyOwnerLayer(placedTrap);
        Destroy(preview);

        Collider col = placedTrap.GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
            if (col is SphereCollider sc && trapRadiusByLevel != null && level < trapRadiusByLevel.Length)
                sc.radius = trapRadiusByLevel[level];
        }

        var trigger = placedTrap.GetComponent<BallSkill_99000004_Trap>();
        if (trigger == null)
            trigger = placedTrap.AddComponent<BallSkill_99000004_Trap>();
        trigger.Init(this);

        remainingTurns = level >= 10 ? 3 : level >= 5 ? 2 : 1;
        trapActive = true;
    }

    void ApplyOwnerLayer(GameObject obj)
    {
        // Đảm bảo chỉ hiển thị cho người sở hữu, ví dụ bằng cách set layer
        // Layer cụ thể có thể được gán từ bên ngoài nếu cần
        if (obj == null) return;
        foreach (Transform t in obj.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = gameObject.layer;
    }

    public void OnEndTurn()
    {
        if (!trapActive)
            return;

        remainingTurns--;
        if (remainingTurns <= 0)
        {
            if (placedTrap != null)
                Destroy(placedTrap);
            trapActive = false;
        }
    }

    public bool IsTrapActive => trapActive;

    public void DeactivateTrap()
    {
        if (placedTrap != null)
            Destroy(placedTrap);
        trapActive = false;
    }

    public GameObject PlacedTrap => placedTrap;
}
