/*
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public class ButtonShootPowerBarController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public static ButtonShootPowerBarController Instance;
    public Animator fingerAnimator;
    public bool isShootting =false;
    private void Awake()
    {
        Instance = this;
    }
    // Start is called before the first frame update
    public void OnPointerDown(PointerEventData eventData)
    {
        isShootting = true; // Khi nhấn giữ
        Debug.Log("IN");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log("OUT");
        isShootting = false; // Khi thả nút
        fingerAnimator.SetTrigger("HandShoot"); // Gọi animation bắn
        //StartCoroutine(GameManager.Instance.onShootBallByPlayer());
    }
} */
