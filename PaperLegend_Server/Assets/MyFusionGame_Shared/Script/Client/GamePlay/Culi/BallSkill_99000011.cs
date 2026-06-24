// Assets/Script/GamePlay/Culi/BallSkill_99000011.cs
using Fusion;
using UnityEngine;

/// <summary>
/// Buff ngẫu nhiên một viên bi khác bằng cách tăng khối lượng
/// và hệ số ma sát trong một lượt.
/// </summary>
public class BallSkill_99000011 : NetworkBehaviour, IFBall
{
    public int level;
    public float[] massMultiplierByLevel = { 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13f };
    public float[] frictionMultiplierByLevel = { 1.5f, 1.6f, 1.7f, 1.8f, 1.9f, 2.0f, 2.2f, 2.4f, 2.6f, 2.8f, 3.0f };
    public GameObject buffVFX;

    NetworkId buffTargetId;
    float originalMass;
    float originalDynamicFriction;
    float originalStaticFriction;
    bool buffActive;

    public void SetStatusUltimate()
    {
        // Hiển thị trạng thái nếu cần
    }

    public void ShootUltimate()
    {
        if (!HasStateAuthority) return;
        var manager = NetworkObjectManager.Instance;
        if (manager == null || manager.ringBalls.Count == 0) return;

        int index = Random.Range(0, manager.ringBalls.Count);
        var target = manager.ringBalls[index];
        float massMul = GetValue(massMultiplierByLevel, level);
        float frictionMul = GetValue(frictionMultiplierByLevel, level);
        RpcApplyModifiers(target.Id, massMul, frictionMul);
    }

    public void ShootEffect()
    {
        if (buffActive)
            buffVFX?.SetActive(true);
    }

    public void StopShootEffect()
    {
        if (!buffActive)
            buffVFX?.SetActive(false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RpcApplyModifiers(NetworkId ballId, float massMultiplier, float frictionMultiplier)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null) return;
        var target = manager.ringBalls.Find(b => b.Id == ballId);
        if (target == null) return;

        var rb = target.GetComponent<Rigidbody>();
        var col = target.GetComponent<Collider>();
        if (rb == null || col == null) return;

        if (!buffActive || buffTargetId != ballId)
        {
            originalMass = rb.mass;
            if (col.material != null)
            {
                originalDynamicFriction = col.material.dynamicFriction;
                originalStaticFriction = col.material.staticFriction;
            }
            buffTargetId = ballId;
        }

        rb.mass = originalMass * massMultiplier;
        if (col.material != null)
        {
            col.material.dynamicFriction = originalDynamicFriction * frictionMultiplier;
            col.material.staticFriction = originalStaticFriction * frictionMultiplier;
        }
        buffVFX?.SetActive(true);
        buffActive = true;
    }

    public void OnEndTurn()
    {
        if (!buffActive) return;
        var manager = NetworkObjectManager.Instance;
        var target = manager?.ringBalls.Find(b => b.Id == buffTargetId);
        if (target != null)
        {
            var rb = target.GetComponent<Rigidbody>();
            var col = target.GetComponent<Collider>();
            if (rb != null) rb.mass = originalMass;
            if (col != null && col.material != null)
            {
                col.material.dynamicFriction = originalDynamicFriction;
                col.material.staticFriction = originalStaticFriction;
            }
        }
        buffVFX?.SetActive(false);
        buffActive = false;
    }

    float GetValue(float[] arr, int lvl)
    {
        if (arr == null || arr.Length == 0) return 1f;
        lvl = Mathf.Clamp(lvl, 0, arr.Length - 1);
        return arr[lvl];
    }
}
