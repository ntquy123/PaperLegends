using Fusion;
using UnityEngine;

/// <summary>
/// Điều khiển hành vi của từng vỏ chuối trong sân.
/// Lớp này chịu trách nhiệm vô hiệu hoá bẫy và phát âm thanh khi có người trúng bẫy.
/// </summary>
public class BananaPeel : MonoBehaviour
{


    // Hàm được gọi trên mọi client khi ConsumeVersion thay đổi.
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Va chạm");
        if (!IsBananaCollider(other))
            return;
        gameObject.SetActive(false);
        SoundManager.Instance?.PlayBananaSlip(transform.position);
    }
    private static bool IsBananaCollider(Collider other)
    {
        if (other == null)
            return false;

        return other.CompareTag("Player");
    }
}
