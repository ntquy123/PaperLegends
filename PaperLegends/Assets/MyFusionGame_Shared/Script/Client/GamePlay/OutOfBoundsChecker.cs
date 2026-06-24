using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OutOfBoundsChecker : MonoBehaviour
{
    private void OnTriggerExit(Collider other)
    {
   
        if (other.CompareTag("RingBall")) // Nếu viên bi ra khỏi vòng
        {
           // GameManager.Instance.BallOutOfBounds(other.gameObject);
        }
    }
}

