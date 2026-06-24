using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;
using System;
#if !UNITY_SERVER
using DG.Tweening;
#endif
using UnityEngine.UIElements;
public class BallController : MonoBehaviour
{
    public static BallController Instance;
    public Transform finger;  // Vị trí của ngón tay
    public Transform fingerPlayer;  // Vị trí của ngón tay khi ở chế độ zoom toàn cảnh
    public Transform CameraViewPlay;
    private Rigidbody rb;
    //public float maxForce = 1;
    public GameObject TextDisplay;
    private TextMeshPro textComponent;
    public GameObject CamFllowLocation;
    private Player modelIF;

    private void Awake()
    {
        Instance = this;
    }    
    void Start()
    {
        textComponent = TextDisplay.GetComponent<TextMeshPro>();
        rb = GetComponent<Rigidbody>();
        NPCController.Instance.SetCurrentBall(gameObject);
    }
    void LateUpdate()
    {
        //Giữ viên bi luôn theo ngón tay
        if (NPCController.Instance.GetIsHolding(rb))
        { 
            rb.isKinematic = true;  // Ngăn mọi tác động vật lý
            modelIF = NPCController.Instance.GetInforPlayer(rb);
            //kiểm tra xem nếu đang lược ở mức thì viên bi hướng về ngón tay nhân vật. ngược lại hướng về bàn tay của camera
            //if (modelIF.statusPlayer == StatusPlayer.StartPoint && !UIControllerOffline.Instance.Hand.activeSelf)
            //    transform.position = fingerPlayer.position;
            //else
            //    transform.position = finger.position;
        }

        // Cập nhật vị trí nhưng không xoay theo viên bi
        CamFllowLocation.transform.position = transform.position;
        CamFllowLocation.transform.rotation = Quaternion.Euler(0, 0, 0); // Giữ hướng cố định
    }
    void Update()
    {
 

        //if (modelIF != null)
        //{
        //    textComponent.text = modelIF.fullname;
        //}

        // 🔹 Đặt lại vị trí trên đầu viên bi
        Vector3 fixedPosition = transform.position + Vector3.up * 1.5f;
        TextDisplay.transform.position = fixedPosition;
        // 🔹 Luôn hướng về camera nhưng chỉ xoay theo Y
        Vector3 cameraDirection = Camera.main.transform.position - TextDisplay.transform.position;
        cameraDirection.y = 0; // Xóa độ nghiêng (giữ chữ thẳng)
        TextDisplay.transform.rotation = Quaternion.LookRotation(cameraDirection);
    }
    #if !UNITY_SERVER
    public void ReleaseBall()
    {
        float power = PowerBarController.Instance.powerSlider.value;
        // Lấy giá trị xoáy từ Joystick:
        // SpinX: xác định lệch hướng ngang (trái/phải)
        // SpinZ: xác định xoáy trước/sau (topspin/backspin)
        float spinX = ShootBallJoystick.Instance.SpinX;
        float spinZ = ShootBallJoystick.Instance.SpinZ;

        float normalizedPower = (power - 1) / (10 - 1); // Chuyển power về khoảng 0 - 1
        float force = Mathf.Lerp(0, modelIF.powerForce, normalizedPower);
        //kiểm tra xem đã sử dụng nộ hay kh
        if(modelIF.statusPlayer == StatusPlayer.Power)
        {
            force = force * 2;
        }    
        Debug.Log($"🔥 Bắn bi với lực: {force}, SpinX: {spinX}, SpinZ: {spinZ}");

        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;     
        NPCController.Instance.UpdateIsHolding(rb, false);
        rb.isKinematic = false;

        // Xác định hướng bắn cơ bản dựa trên finger.forward
        Vector3 shootDirection = -finger.forward;
        if (ShootBallJoystick.Instance != null)
            shootDirection = ShootBallJoystick.Instance.ApplyShotAccuracy(shootDirection);
        shootDirection.Normalize();

        // Áp dụng lực bắn ban đầu
        rb.AddForce(shootDirection * force, ForceMode.Impulse);


        // Tính toán lực xoáy dựa trên spinX và spinZ, nhân với force và một hệ số điều chỉnh (0.5f)
        // Hệ số này có thể điều chỉnh thêm tùy theo cảm nhận "tự nhiên" bạn muốn.
        // Điều chỉnh lực xoáy theo hướng ngón tay để trùng khớp với thao tác
        // Joystick. Sử dụng rotation của ngón tay để biến đổi vector xoáy ra
        // tọa độ thế giới.
        Quaternion fingerRot = finger.rotation;
        Vector3 localSpin = new Vector3(spinX, 0, spinZ);
        Vector3 spinTorque = fingerRot * localSpin * force * 0.5f;
        rb.AddTorque(spinTorque, ForceMode.VelocityChange);
       // SoundManager.Instance.StartBallRollingLoop(() => rb.velocity.magnitude, 0.25f);


 
  

        //quay camera theo viên bi
        CameraRotation.Instance.StartFollowingBall(CamFllowLocation.transform);
          //check có sử dụng ulti hay kh
            IFBall ballScript = rb.GetComponent<IFBall>();
            if (UIControllerOffline.Instance.selectedSkills.Any(x=>x == SkillType.Ultimate) )
                {

            ballScript.ShootUltimate();
                }
            else
            {
              // ballScript.ShootEffect();
            }
    }
#endif



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
        if ( collision.gameObject.tag.StartsWith("BallAI") && !NPCController.Instance.IsYourTurn())
        {
            Debug.Log("Viên bi của người chơi bị viên bi AI bắn trúng! Game Over!");
            NPCController.Instance.ConfirmDestroy("Player");
            // UIControllerOffline.Instance.GameOver(); // Gọi hàm Game Over

        }
        // Kiểm tra nếu viên bi của người chơi bắn trúng viên bi máy trong lượt của họ
        else if ( collision.gameObject.tag.StartsWith("BallAI") && NPCController.Instance.IsYourTurn())
        {
            Debug.Log("Viên bi của người chơi bắn trúng viên bi AI! Nhận điểm!");
            // xác nhận tiêu diệt viên bi đó
            string ballTag = collision.gameObject.tag;
            string aiTag = ballTag.Replace("BallAI", "AI");
            NPCController.Instance.ConfirmDestroy(aiTag);

            // Lấy điểm của viên bi AI (giả sử mỗi viên bi AI có điểm)
            // int aiBallScore = GameManager.Instance.GetAIScore();

            // Cộng điểm cho người chơi
            // GameManager.Instance.AddScoreToPlayer(aiBallScore);

            // Kiểm tra nếu tất cả bi đã dừng mới hủy viên bi AI
            //StartCoroutine(DestroyWhenStopped(collision.gameObject));
        }
        
    }
    // Coroutine chờ đến khi tất cả bi dừng mới hủy viên bi AI
    //private IEnumerator DestroyWhenStopped(GameObject aiBall)
    //{
    //    // Chờ đến khi tất cả bi dừng lại
    //    yield return new WaitUntil(() => GameManager.Instance.AllBallsStopped());

    //    Debug.Log("Tất cả bi đã dừng! Loại bỏ AI Player.");
    //    GameManager.Instance.EliminateAIPlayer(aiBall);
    //    // Kiểm tra nếu không còn viên bi AI nào, người chơi thắng
    //    yield return new WaitForSeconds(0.5f); // Chờ 1 chút để đảm bảo bi bị hủy trước khi kiểm tra
    //    if (!AnyBallAILeft())
    //    {
    //        Debug.Log("Không còn viên bi AI nào! Người chơi thắng!");
    //        GameManager.Instance.GameOver("Win"); // Gọi Game Over với trạng thái thắng
    //    }
    //    else
    //    {
    //        // tiếp tục lượt của mình
    //        GameManager.Instance.isContinueTurn = true;
    //    }    

    //}
}

