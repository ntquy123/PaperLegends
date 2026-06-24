 
using UnityEngine;

/// <summary>
/// Khi bắn, viên bi sẽ để lại một vệt khói (sử dụng Trail Renderer).
/// Vệt khói tồn tại trong vài lượt. Kỹ năng có số lần sử dụng giới hạn.
/// </summary>
public class BallSkill_99000003 : MonoBehaviour, IFBall
{
    public int level;
    public TrailRenderer smokeTrailVFX; // Gắn component Trail Renderer vào đây

    private int maxCharges;
    private int currentCharges;
    private int remainingTurns;
    private bool isTrailActive = false;

    void Start()
    {
        // Khởi tạo số lần sử dụng dựa trên level
        if (level >= 10)
        {
            maxCharges = 3;
        }
        else if (level >= 5)
        {
            maxCharges = 2;
        }
        else
        {
            maxCharges = 1;
        }
        currentCharges = maxCharges;

        // Đảm bảo vệt khói đã tắt khi bắt đầu
        if (smokeTrailVFX != null)
        {
            smokeTrailVFX.emitting = false;
        }
    }

    // Hàm này sẽ được gọi khi bắt đầu lượt bắn ultimate
    public void ShootUltimate()
    {
        // Kiểm tra xem còn lượt sử dụng không và hiệu ứng chưa được kích hoạt
        if (currentCharges > 0 && !isTrailActive)
        {
            // Trừ 1 lần sử dụng
            currentCharges--;
            isTrailActive = true;

            // Thiết lập số lượt tồn tại của vệt khói
            remainingTurns = (level >= 10) ? 3 : 1;

            // Bật vệt khói
            if (smokeTrailVFX != null)
            {
                smokeTrailVFX.emitting = true;
            }
            
            // Cập nhật UI hiển thị số charge còn lại (nếu có)
            // Ví dụ: UIManager.Instance.UpdateSmokeBallCharges(currentCharges);
        }
    }

    public void OnEndTurn()
    {
        if (!isTrailActive) return;

        // Giảm số lượt tồn tại
        remainingTurns--;

        // Nếu hết lượt tồn tại, cho vệt khói tan biến
        if (remainingTurns <= 0)
        {
            isTrailActive = false;
            if (smokeTrailVFX != null)
            {
                // Xóa vệt khói cũ đi để chuẩn bị cho lần bắn tiếp theo
                smokeTrailVFX.Clear(); 
                smokeTrailVFX.emitting = false;
            }
        }
    }

    // Các hàm khác từ Interface IFBall
    public void SetStatusUltimate()
    {
        // Có thể dùng để hiển thị UI số charge còn lại
    }

    public void ShootEffect() { }
    public void StopShootEffect() { }
}