// Assets/Script/GamePlay/Culi/BallSkill_99000005.cs
using UnityEngine;

/// <summary>
/// Tăng gấp đôi kích thước (scale) của viên bi trong một số lượt.
/// Có số lần sử dụng giới hạn.
/// </summary>
public class BallSkill_99000005 : MonoBehaviour, IFBall
{
    public int level;
    public GameObject enlargeVFX; // Hiệu ứng khi bi phình to

    private Vector3 originalScale;
    private int maxCharges;
    private int currentCharges;
    private int remainingTurns;
    private bool isEffectActive = false;

    void Start()
    {
        // Lưu lại kích thước gốc của viên bi
        originalScale = transform.localScale;

        // Khởi tạo số lần sử dụng dựa trên level
        if (level >= 10) maxCharges = 3;
        else if (level >= 5) maxCharges = 2;
        else maxCharges = 1;
        currentCharges = maxCharges;
    }

    public void ShootUltimate()
    {
        // Kiểm tra xem còn lượt sử dụng không và hiệu ứng chưa được kích hoạt
        if (currentCharges > 0 && !isEffectActive)
        {
            currentCharges--;
            isEffectActive = true;

            // Thiết lập số lượt hiệu lực
            remainingTurns = (level >= 10) ? 3 : 1;

            // Phóng to viên bi
            transform.localScale = originalScale * 2f;

            // Kích hoạt hiệu ứng hình ảnh (nếu có)
            enlargeVFX?.SetActive(true);
        }
    }

    public void OnEndTurn()
    {
        if (!isEffectActive) return;

        remainingTurns--;

        // Nếu hết lượt hiệu lực, trả bi về kích thước cũ
        if (remainingTurns <= 0)
        {
            isEffectActive = false;
            transform.localScale = originalScale;
            enlargeVFX?.SetActive(false);
        }
    }

    // Các hàm khác từ Interface IFBall
    public void SetStatusUltimate() { }
    public void ShootEffect() { }
    public void StopShootEffect() { }
}