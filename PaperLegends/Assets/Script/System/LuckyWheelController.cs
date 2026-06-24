using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Điều khiển vòng quay may mắn 3D dựa trên thao tác chạm/kéo.
/// Người chơi chạm vào vòng quay, vuốt theo vòng tròn để tạo lực quay và vòng quay tự giảm tốc nhờ quán tính.
/// Khi dừng lại, ô được lá cờ chỉ vào sẽ được trả về qua sự kiện <see cref="onRewardSelected"/>.
/// </summary>
public class LuckyWheelController : MonoBehaviour
{
    [Header("Wheel References")]
    [Tooltip("Transform đại diện cho vòng quay (được quay quanh trục forward).")]
    public Transform wheelTransform;

    [Tooltip("Rigidbody gắn với vòng quay để áp dụng lực quay.")]
    public Rigidbody wheelRigidbody;

    [Tooltip("Camera dùng để raycast tới vòng quay.")]
    public Camera interactionCamera;

    [Header("Spin Settings")]
    [Tooltip("Hệ số nhân lực quay từ thao tác vuốt.")]
    public float spinForceMultiplier = 0.25f;

    [Tooltip("Giới hạn lực quay tối đa để tránh quay quá nhanh.")]
    public float maxSpinImpulse = 25f;

    [Tooltip("Lực quay tối thiểu để tránh những cú chạm nhẹ không cố ý.")]
    public float minSpinImpulse = 2.5f;

    [Tooltip("Ngưỡng tốc độ góc (rad/s) để xem như vòng quay đã dừng.")]
    public float spinStopThreshold = 0.25f;

    [Tooltip("Độ lệch góc (độ) giữa hướng 0° của vòng quay và hướng lá cờ chỉ.")]
    public float pointerOffset = 0f;

    [Header("Rewards")]
    [Tooltip("Danh sách các ô thưởng cùng dải góc tương ứng (theo chiều kim đồng hồ, 0° ở hướng lá cờ).")]
    public List<WheelReward> rewards = new();

    [Serializable]
    public class WheelReward
    {
        public string rewardId;
        [Range(0f, 360f)] public float startAngle;
        [Range(0f, 360f)] public float endAngle;
    }

    [Serializable]
    public class RewardSelectedEvent : UnityEvent<WheelReward> { }

    [Header("Events")]
    public RewardSelectedEvent onRewardSelected = new();

    private bool isDragging;
    private bool wasSpinning;
    private int activePointerId = -1;
    private float dragStartTime;
    private float previousAngle;
    private float draggedAngle;

    private void Reset()
    {
        interactionCamera = Camera.main;
        if (!wheelTransform)
            wheelTransform = transform;
    }

    private void Update()
    {
        HandleInput();
        DetectSpinStop();
    }

    private void HandleInput()
    {
        if (TryBeginInput(out Vector2 startPosition, out int pointerId))
        {
            if (IsWheelHit(startPosition))
            {
                BeginDrag(startPosition, pointerId);
            }
        }

        if (isDragging)
        {
            if (TryGetPointerPosition(activePointerId, out Vector2 currentPosition))
            {
                ContinueDrag(currentPosition);
            }

            if (TryEndInput(activePointerId, out Vector2 endPosition))
            {
                EndDrag(endPosition);
            }
        }
        else
        {
            // Cho phép người chơi thêm lực khi vòng quay gần dừng.
            if (!IsWheelSpinning() && TryBeginInput(out Vector2 quickStart, out pointerId))
            {
                if (IsWheelHit(quickStart))
                {
                    BeginDrag(quickStart, pointerId);
                }
            }
        }
    }

    private void BeginDrag(Vector2 startPosition, int pointerId)
    {
        isDragging = true;
        activePointerId = pointerId;
        dragStartTime = Time.time;
        draggedAngle = 0f;
        previousAngle = GetSignedAngle(startPosition);
    }

    private void ContinueDrag(Vector2 currentPosition)
    {
        float currentAngle = GetSignedAngle(currentPosition);
        float delta = Mathf.DeltaAngle(previousAngle, currentAngle);
        draggedAngle += delta;
        previousAngle = currentAngle;
    }

    private void EndDrag(Vector2 endPosition)
    {
        float dragDuration = Mathf.Max(Time.time - dragStartTime, 0.01f);
        float spinImpulse = Mathf.Abs(draggedAngle / dragDuration) * spinForceMultiplier;
        spinImpulse = Mathf.Clamp(spinImpulse, minSpinImpulse, maxSpinImpulse);

        float direction = Mathf.Sign(draggedAngle);
        ApplySpin(direction * spinImpulse);

        isDragging = false;
        activePointerId = -1;
        draggedAngle = 0f;
    }

    private void ApplySpin(float impulse)
    {
        if (wheelRigidbody == null)
        {
            Debug.LogWarning("LuckyWheelController: Thiếu Rigidbody để áp dụng lực quay.");
            return;
        }

        wheelRigidbody.AddTorque(-wheelTransform.forward * impulse, ForceMode.Impulse);
        wasSpinning = true;
    }

    private void DetectSpinStop()
    {
        bool spinningNow = IsWheelSpinning();

        if (wasSpinning && !spinningNow)
        {
            var reward = DetermineReward();
            if (reward != null)
            {
                onRewardSelected.Invoke(reward);
            }
        }

        wasSpinning = spinningNow;
    }

    private bool IsWheelSpinning()
    {
        if (wheelRigidbody == null)
            return false;

        return wheelRigidbody.angularVelocity.magnitude > spinStopThreshold;
    }

    private WheelReward DetermineReward()
    {
        if (wheelTransform == null || rewards.Count == 0)
            return null;

        float rawAngle = wheelTransform.localEulerAngles.z - pointerOffset;
        float normalizedAngle = rawAngle % 360f;
        if (normalizedAngle < 0f)
            normalizedAngle += 360f;

        foreach (var reward in rewards)
        {
            if (IsAngleInRange(normalizedAngle, reward.startAngle, reward.endAngle))
                return reward;
        }

        return null;
    }

    private bool IsAngleInRange(float angle, float start, float end)
    {
        angle = Mathf.Repeat(angle, 360f);
        start = Mathf.Repeat(start, 360f);
        end = Mathf.Repeat(end, 360f);

        if (start <= end)
            return angle >= start && angle <= end;

        // Khoảng góc bị cắt ngang qua 0°.
        return angle >= start || angle <= end;
    }

    private float GetSignedAngle(Vector2 screenPosition)
    {
        if (interactionCamera == null || wheelTransform == null)
            return 0f;

        Vector2 wheelScreenPos = interactionCamera.WorldToScreenPoint(wheelTransform.position);
        Vector2 direction = screenPosition - wheelScreenPos;
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    private bool IsWheelHit(Vector2 screenPosition)
    {
        if (interactionCamera == null)
            return false;

        Ray ray = interactionCamera.ScreenPointToRay(screenPosition);
        return Physics.Raycast(ray, out RaycastHit hit) && hit.transform == wheelTransform;
    }

    private bool TryBeginInput(out Vector2 position, out int pointerId)
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            pointerId = -1;
            return true;
        }
#endif
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                position = touch.position;
                pointerId = touch.fingerId;
                return true;
            }
        }

        position = default;
        pointerId = -1;
        return false;
    }

    private bool TryGetPointerPosition(int pointerId, out Vector2 position)
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (pointerId == -1)
        {
            position = Input.mousePosition;
            return true;
        }
#endif
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId == pointerId)
            {
                position = touch.position;
                return true;
            }
        }

        position = default;
        return false;
    }

    private bool TryEndInput(int pointerId, out Vector2 position)
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (pointerId == -1 && Input.GetMouseButtonUp(0))
        {
            position = Input.mousePosition;
            return true;
        }
#endif
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId == pointerId && (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
            {
                position = touch.position;
                return true;
            }
        }

        position = default;
        return false;
    }
}
