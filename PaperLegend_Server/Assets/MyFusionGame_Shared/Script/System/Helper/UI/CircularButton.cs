#if UNITY_SERVER
using UnityEngine;

public class CircularButton : MonoBehaviour
{
    public static CircularButton Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void SetPower(float power) { }

    public void ResetEffectPower() { }

    public void onClickPower() { }
}
#else
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CircularButton : MonoBehaviour
{
    public static CircularButton Instance;
    public Button button; // Button hình tròn
    public Image buttonImage; // Hình ảnh của button (để thay đổi màu sắc)
    private float currentFillAmount = 0f; // Phần trăm hiện tại
    private const float maxFillAmount = 1f; // Giá trị tối đa (100%)

    // Thêm Particle System cho hiệu ứng cháy
    public Image fireEffect; // Particle System để tạo hiệu ứng cháy

 
    public GameObject handObject;
    public float scaleUpFactor = 1.2f;  // Tăng kích thước
    public float effectDuration = 1f;   // Thời gian hiệu ứng
    private void Awake()
    {
        Instance = this;
    }
    void Start()
    {
        // Lấy tham chiếu đến Image của button
        //buttonImage = button.GetComponent<Image>();
        UpdateButtonColor();
    }

    public void SetPower(float power)
    {
        currentFillAmount += power;

        // Cập nhật màu của button dựa trên phần trăm
        UpdateButtonColor();

        // Kiểm tra xem button đã đầy chưa
        if (currentFillAmount >= maxFillAmount)
        {
            fireEffect.gameObject.SetActive(true);
        }
    }

    void UpdateButtonColor()
    {
        // Tính toán phần trăm màu đỏ chiếm trong button từ dưới lên
        float lerpValue = Mathf.Clamp01(currentFillAmount); // Giá trị lerp từ 0 đến 1

        // Đổi màu đỏ từ dưới lên khi phần trăm tăng
        // Tạo hiệu ứng màu đỏ chiếm dần từ dưới lên
        buttonImage.type = Image.Type.Filled;
        buttonImage.fillMethod = Image.FillMethod.Vertical;
        buttonImage.fillOrigin = (int)Image.OriginVertical.Bottom; // Đặt điểm gốc ở dưới cùng
        buttonImage.fillAmount = lerpValue; // Tăng dần màu đỏ
                                            // Tạo màu với alpha điều chỉnh (121 là giá trị alpha bạn muốn)
        float alphaValue = 121f / 255f; // Alpha phải nằm trong khoảng từ 0 đến 1
        Color targetColor = Color.red; // Màu đỏ

        // Thay đổi alpha của màu đỏ
        buttonImage.color = new Color(targetColor.r, targetColor.g, targetColor.b, alphaValue);
    }

    public void onClickPower()
    {
        if (!GameManagerNetWork.Instance.CheckServerConnection())
            return;
        if (currentFillAmount >= maxFillAmount)
        {
            fireEffect.gameObject.SetActive(false);
            currentFillAmount = 0f;
            UpdateButtonColor(); // Cập nhật lại màu thanh nộ về ban đầu
            var playerGO = NetworkObjectManager.Instance?.GetPlayerObject(GameManagerNetWork.Instance.loginUserModel.UserId);
            if (playerGO == null)
                return;

            var handler = playerGO.GetComponent<PlayerNetworkHandler>();
            var currentPlayer =  handler.PlayerModel;
            currentPlayer.statusPlayer = StatusPlayer.Power;
            GameManagerNetWork.Instance.serverRPC.RpcTogglePowerVFX(currentPlayer.playerId, true);
            handler.PlayerModel = currentPlayer;
            GameManagerNetWork.Instance.serverRPC.RpcPlayPowerEffect(currentPlayer.playerId);
           
            // Thay đổi màu sắc để tạo hiệu ứng sáng vàng và đỏ
           // ChangeColorEffect();

            // Tăng kích thước của đối tượng (gồng lên)
            //ScaleUpEffect();
        }    
    }
    private void ChangeColorEffect()
    {
        Renderer handRenderer = handObject.GetComponent<Renderer>();  // handMaterial là GameObject
        Material handMaterial = handRenderer.material;  // Lấy material từ Renderer
        // Sử dụng DOTween để thay đổi màu sắc ánh sáng (emission) từ vàng sang đỏ
        handMaterial.DOColor(Color.yellow, "_EmissionColor", effectDuration / 2) // Sáng vàng
                    .OnComplete(() => handMaterial.DOColor(Color.red, "_EmissionColor", effectDuration / 2)); // Sau đó sáng đỏ
    }
    private void ScaleUpEffect()
    {
        // Sử dụng DOTween để phóng to bàn tay (gồng lên)
        transform.DOScale(transform.localScale * scaleUpFactor, effectDuration).SetEase(Ease.OutBack);
    }
    public void ResetEffectPower()
    {
        Renderer handRenderer = handObject.GetComponent<Renderer>();  // handMaterial là GameObject
        Material handMaterial = handRenderer.material;  // Lấy material từ Renderer
        // Đợi sau khi hoàn tất hiệu ứng (có thể giữ hiệu ứng sáng trong một khoảng thời gian ngắn)
        DOTween.Sequence()
            .AppendInterval(effectDuration)
            .OnComplete(() =>
            {
                // Reset lại màu sắc và kích thước
                handMaterial.DOColor(Color.black, "_EmissionColor", 0.5f);  // Tắt hiệu ứng ánh sáng
                transform.DOScale(transform.localScale / scaleUpFactor, 0.5f).SetEase(Ease.InBack);  // Quay lại kích thước ban đầu;
            });
    }

    private void OnDisable()
    {
        // Kill any button tweens when disabled
        transform.DOKill();
        if (handObject != null)
            handObject.transform.DOKill();
    }
}
#endif
