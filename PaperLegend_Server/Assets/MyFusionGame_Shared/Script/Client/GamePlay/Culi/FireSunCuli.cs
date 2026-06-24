using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireSunCuli : MonoBehaviour, IFBall
{
    public static FireSunCuli Instance;
    public GameObject StatusPower; //Trạng thái khi sử dụng kỹ năng
    public GameObject StatusShooting;
    public GameObject StatusExplode; // khi sử dụng ultimate va chạm vật thể
    public GameObject fireTrailVFXPrefab; // Gán hiệu ứng lửa bắn ra khi sử dụng ulti
    private GameObject fireTrailInstance;
    private TrailRenderer trailRenderer;
    public GameObject ShootEffectVFX;
    public GameObject fireTrailPrefab; // Prefab hiệu ứng vệt lửa
    private bool isShooted = false;
    private void Awake()
    {
        Instance = this;
    }
    void Start()
    {
        // Nếu Prefab có TrailRenderer, bật nó lên
        if (fireTrailVFXPrefab != null)
        {
            fireTrailInstance = Instantiate(fireTrailVFXPrefab, transform);
            trailRenderer = fireTrailInstance.GetComponent<TrailRenderer>();

            // Mặc định ẩn hiệu ứng vệt lửa
            fireTrailInstance.SetActive(false);
        }
    }
    void OnCollisionEnter(Collision collision)
    {
        //SoundManager.Instance.PlayBallHit(collision.relativeVelocity.magnitude, collision.contacts[0].point);
        //SoundManager.Instance.StartBallRollingLoop(gameObject, () => rb.velocity.magnitude);
        if (((1 << collision.gameObject.layer)) != 0 && isShooted)
        {
            CreateFireTrail(collision.contacts[0].point);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (((1 << collision.gameObject.layer)) != 0 && isShooted)
        {
            CreateFireTrail(collision.contacts[0].point);
        }
    }

    void CreateFireTrail(Vector3 position)
    {
        if (fireTrailPrefab != null)
        {
            GameObject fireTrail = Instantiate(fireTrailPrefab, position, Quaternion.identity);

            // Xoay theo mặt đất để bám dính tự nhiên hơn
            fireTrail.transform.rotation = Quaternion.LookRotation(Vector3.up);

            // Hủy vệt lửa sau 2 giây để tránh quá tải
            Destroy(fireTrail, 10f);
        }
    }

    public void ShootEffect()
    {
        //hiệu ứng bắn bình thường
        ShootEffectVFX.SetActive(true);
        isShooted = true;
    }
    public void StopShootEffect()
    {
        //hiệu ứng bắn bình thường
        ShootEffectVFX.SetActive(false);
        isShooted = false;
    }
    public void SetStatusUltimate()
    {
        StatusPower.SetActive(true);
    }
    public void ShootUltimate()
    {
        // Khi bắn viên bi, bật hiệu ứng vệt lửa

        if (fireTrailInstance != null)
        {
            fireTrailInstance.SetActive(true);
        }
    }

    public void ResetStatusUltimate()
    {
        StatusPower.SetActive(false);
        if (fireTrailInstance != null)
        {
            fireTrailInstance.SetActive(false);
        }
    }
}
