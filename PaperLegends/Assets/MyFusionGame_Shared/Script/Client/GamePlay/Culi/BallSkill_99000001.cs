// Assets/Script/GamePlay/Skills/BallSkill_909000001.cs
using UnityEngine;

/// <summary>
/// Kỹ năng ví dụ cho viên bi ID 99000001.
/// Mỗi viên bi gắn script này qua Addressables.
/// </summary>
public class BallSkill_99000001 : MonoBehaviour, IFBall
{
    [Header("VFX")]
    public GameObject rollVFXPrefab;       // Hiệu ứng khi viên bi lăn
    public GameObject hitVFXPrefab;        // Hiệu ứng khi chạm
    public GameObject specialVFXPrefab;    // Hiệu ứng khi dùng kỹ năng

    [Header("Số lần dùng kỹ năng")]
    public int maxSkillUses = 3;
    private int remainingSkillUses;

    Rigidbody rb;
    GameObject rollVFXInstance;

    void Awake()
    {
        remainingSkillUses = maxSkillUses;
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Hiệu ứng VFX viên bi lăn
        bool isRolling = rb.linearVelocity.sqrMagnitude > 0.1f;
        if (isRolling)
        {
            if (rollVFXInstance == null && rollVFXPrefab != null)
                rollVFXInstance = Instantiate(rollVFXPrefab, transform);

            rollVFXInstance?.SetActive(true);
        }
        else
        {
            rollVFXInstance?.SetActive(false);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Hiệu ứng VFX khi viên bi bắn trúng va chạm
        if (hitVFXPrefab != null)
            Instantiate(hitVFXPrefab, collision.contacts[0].point, Quaternion.identity);
    }

    // --- IFBall implementation ---

    // Gọi khi UI kích hoạt trạng thái kỹ năng
    public void SetStatusUltimate()
    {
        // Có thể hiển thị biểu tượng trạng thái hoặc chuẩn bị VFX
    }

    // Gọi khi viên bi bắt đầu được bắn
    public void ShootEffect()
    {
        // Nếu muốn bật hiệu ứng lăn ngay khi bắn
        rollVFXInstance?.SetActive(true);
    }

    public void StopShootEffect()
    {
        rollVFXInstance?.SetActive(false);
    }

    // Kỹ năng đặc biệt, bị giới hạn bởi remainingSkillUses
    public void ShootUltimate()
    {
        if (remainingSkillUses <= 0) return;
        remainingSkillUses--;

        // Ví dụ: bung nổ xung quanh viên bi
        if (specialVFXPrefab != null)
            Instantiate(specialVFXPrefab, transform.position, Quaternion.identity);

        // Thêm hiệu ứng gameplay khác (tăng tốc, sát thương, v.v.) ở đây
    }
}
