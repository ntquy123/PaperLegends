// Assets/Script/GamePlay/Culi/BallSkill_99000002.cs
using UnityEngine;

/// <summary>
/// Giảm hệ số ma sát của viên bi trong một số lượt.
/// </summary>
public class BallSkill_99000002 : MonoBehaviour, IFBall
{
    public int level;
    public float[] frictionReductionByLevel = { 0f, 5f, 10f, 15f, 20f, 30f, 40f, 50f, 60f, 65f, 70f };
    public GameObject frictionVFX;
    float originalDyn, originalStatic;
    int remainingTurns;
    bool active;
    Collider col;

    void Awake()
    {
        col = GetComponent<Collider>();
        if (col != null && col.material != null)
        {
            originalDyn = col.material.dynamicFriction;
            originalStatic = col.material.staticFriction;
        }
    }

    public void SetStatusUltimate()
    {
        // Hiển thị trạng thái nếu cần
    }

    public void ShootUltimate()
    {
        if (frictionReductionByLevel == null || level < 0 || level >= frictionReductionByLevel.Length)
            return;

        float percent = frictionReductionByLevel[level];
        if (col != null && col.material != null)
        {
            col.material.dynamicFriction = originalDyn * (1f - percent / 100f);
            col.material.staticFriction = originalStatic * (1f - percent / 100f);
        }
        remainingTurns = level >= 10 ? 3 : level >= 5 ? 2 : 1;
        frictionVFX?.SetActive(true);
        active = true;
    }

    public void ShootEffect()
    {
        if (active)
            frictionVFX?.SetActive(true);
    }

    public void StopShootEffect()
    {
        if (!active)
            frictionVFX?.SetActive(false);
    }

    public void OnEndTurn()
    {
        if (!active) return;
        remainingTurns--;
        if (remainingTurns <= 0)
        {
            if (col != null && col.material != null)
            {
                col.material.dynamicFriction = originalDyn;
                col.material.staticFriction = originalStatic;
            }
            frictionVFX?.SetActive(false);
            active = false;
        }
    }
}
