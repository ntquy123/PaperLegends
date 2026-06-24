using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class OpponentController : MonoBehaviour
{
    public Animator opponentAnimator; // Animator để điều khiển animation
    public Transform opponentBall; // Viên bi của đối thủ
    public float moveSpeed = 2f; // Tốc độ di chuyển

    private bool isMoving = false;

    void Start()
    {
        opponentAnimator.SetBool("isWaiting", true); // Mặc định là trạng thái ngồi chờ
    }

    public void MoveToBall()
    {
        if (!isMoving)
        {
            StartCoroutine(MoveOpponentToBall());
        }
    }

    private IEnumerator MoveOpponentToBall()
    {
        isMoving = true;
        opponentAnimator.SetBool("isWaiting", false);
        opponentAnimator.SetBool("isMoving", true);

        Vector3 targetPosition = opponentBall.position; // Lấy vị trí viên bi của đối thủ
        targetPosition.y = transform.position.y; // Giữ nguyên chiều cao

        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            yield return null;
        }

        // Khi đến nơi, đổi animation sang trạng thái chờ
        opponentAnimator.SetBool("isMoving", false);
        opponentAnimator.SetBool("isWaiting", true);
        isMoving = false;
    }
}
