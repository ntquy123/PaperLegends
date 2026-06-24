using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IFBall  
{
    void SetStatusUltimate();  // Hàm chung để kích hoạt kỹ năng
    void ShootUltimate();
    void ShootEffect();
    void StopShootEffect();
}
