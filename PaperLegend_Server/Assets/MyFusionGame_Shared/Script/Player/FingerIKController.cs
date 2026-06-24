using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    public Animator animator;  // Animator của bàn tay
    public Transform ball;     // Viên bi cần theo dõi
    public Transform finger;   // Ngón tay cần cong
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void OnAnimatorIK(int layerIndex)
    {
        // Kiểm tra nếu Animator có đang hoạt động
        if (animator)
        {
            // Đặt trọng số IK của ngón tay
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);  // Cài đặt trọng số cho ngón tay
            animator.SetIKPosition(AvatarIKGoal.RightHand, ball.position);  // Đưa vị trí tay về viên bi
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1f);    // Trọng số quay ngón tay
            animator.SetIKRotation(AvatarIKGoal.RightHand, ball.rotation);  // Quay ngón tay về hướng viên bi

            // Nếu muốn làm cho ngón tay chỉ cong thay vì di chuyển hoàn toàn
            float distance = Vector3.Distance(finger.position, ball.position);
            float bendFactor = Mathf.Clamp01(distance / 2f); // Điều chỉnh yếu tố cong tùy vào khoảng cách
            finger.localRotation = Quaternion.Lerp(finger.localRotation, Quaternion.Euler(-45f * bendFactor, 0f, 0f), Time.deltaTime * 5f);
        }
    }
}
