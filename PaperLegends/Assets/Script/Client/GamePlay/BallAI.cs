using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class BallAI : MonoBehaviour
{
    public static BallAI Instance;
    public Transform fingerAI;  // Vị trí của ngón tay
    private Rigidbody rb;
    public GameObject TextDisplay;
    private TextMeshPro textComponent;
    public GameObject CamFllowLocation;
    private void Awake()
    {
        Instance = this;
    }
    void Start()
    {
          textComponent = TextDisplay.GetComponent<TextMeshPro>();
        rb = GetComponent<Rigidbody>();
       
    }
    void LateUpdate()
    {


        // Cập nhật vị trí nhưng không xoay theo viên bi
        CamFllowLocation.transform.position = transform.position;
        CamFllowLocation.transform.rotation = Quaternion.Euler(0, 0, 0); // Giữ hướng cố định
    }
    void Update()
    {
 
            var modelIF = NPCController.Instance.GetInforPlayer(rb);
        if (modelIF != null) {
            textComponent.text = modelIF.fullname;
        } 
        //Giữ viên bi luôn theo ngón tay
        if (NPCController.Instance.GetIsHolding(rb))
        {// Vô hiệu hóa ảnh hưởng vật lý khi đang giữ viên bi
            //rb.velocity = Vector3.zero;
            //rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;  // Ngăn mọi tác động vật lý
            transform.position = fingerAI.position; // Viên bi đi theo ngón tay
        }
        //else
        //{
        //    rb.isKinematic = false;
        //}    
        // 🔹 Đặt lại vị trí trên đầu viên bi
        Vector3 fixedPosition = transform.position + Vector3.up * 0.5f;
        TextDisplay.transform.position = fixedPosition;
        // 🔹 Luôn hướng về camera nhưng chỉ xoay theo Y
        Vector3 cameraDirection = Camera.main.transform.position - TextDisplay.transform.position;
        cameraDirection.y = 0; // Xóa độ nghiêng (giữ chữ thẳng)
        TextDisplay.transform.rotation = Quaternion.LookRotation(cameraDirection);
    }
    private void OnCollisionEnter(Collision collision)
    {
        //SoundManager.Instance.PlayBallHit(collision.relativeVelocity.magnitude, collision.contacts[0].point);
       // SoundManager.Instance.StartBallRollingLoop(() => rb.velocity.magnitude, 0.25f);
        if (NPCController.Instance.currentState == TurnState.Exam)
            return;
        if (collision.gameObject.CompareTag("TreasureChest"))
        {
            //VFXController.Play("ChestReward");
            //AudioManager.Play("ChestReward");
            //Destroy(collision.gameObject);

            //TreasureType randomReward = MysteryChestManager.GetRandomReward();
            //Player currentPlayer = GameManager.Instance.GetInforPlayer(rb);
            //TreasureEffectApplier.Apply(currentPlayer, randomReward);
            return;
        }

        // Kiểm tra nếu bị viên bi của máy AI bắn trúng
        if (collision.gameObject.tag.StartsWith("BallAI") && !NPCController.Instance.IsYourTurn() )
        {

            Debug.Log("AI đã bắn trúng Ai khác");
            // xác nhận tiêu diệt viên bi đó
            string ballTag = collision.gameObject.tag;
            string aiTag = ballTag.Replace("BallAI", "AI");
            NPCController.Instance.RemoveAI(aiTag);
        }
    }
}
