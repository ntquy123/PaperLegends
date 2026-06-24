// Assets/Script/GamePlay/Culi/BallSkill_99000013.cs
using UnityEngine;

/// <summary>
/// Kích hoạt trạng thái miễn nhiễm mọi hiệu ứng kỹ năng của đối thủ trong một số lượt.
/// </summary>
public class BallSkill_99000013 : MonoBehaviour, IFBall
{
    public int level;
    public GameObject immunityVFX;
    public bool isImmune;

    int remainingTurns;

    public void SetStatusUltimate()
    {
        // Hiển thị trạng thái nếu cần
    }

    public void ShootUltimate()
    {
        remainingTurns = level >= 10 ? 3 : level >= 5 ? 2 : 1;
        isImmune = true;
        immunityVFX?.SetActive(true);
    }

    public void ShootEffect()
    {
        if (isImmune)
            immunityVFX?.SetActive(true);
    }

    public void StopShootEffect()
    {
        if (!isImmune)
            immunityVFX?.SetActive(false);
    }

    public void OnEndTurn()
    {
        if (!isImmune) return;
        remainingTurns--;
        if (remainingTurns <= 0)
        {
            isImmune = false;
            immunityVFX?.SetActive(false);
        }
    }
}

