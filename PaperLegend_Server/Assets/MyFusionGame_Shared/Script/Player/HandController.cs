using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class HandController : MonoBehaviour
{
    public float rotationSpeed = 5f; // Tốc độ xoay bàn tay
    private Vector2 startTouchPosition;
    private Vector2 currentTouchPosition;
    private bool isSwiping = false;

    void Update()
    {
        if (Input.touchCount > 0) // Kiểm tra nếu có chạm vào màn hình
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    startTouchPosition = touch.position;
                    isSwiping = true;
                    break;

                case TouchPhase.Moved:
                    if (isSwiping)
                    {
                        currentTouchPosition = touch.position;
                        float swipeDelta = currentTouchPosition.x - startTouchPosition.x;

                        // Xoay bàn tay dựa trên độ dài vuốt
                        transform.Rotate(Vector3.up, -swipeDelta * rotationSpeed * Time.deltaTime);

                        startTouchPosition = currentTouchPosition; // Cập nhật vị trí mới
                    }
                    break;

                case TouchPhase.Ended:
                    isSwiping = false;
                    break;
            }
        }
    }
}

