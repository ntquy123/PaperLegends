using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Fusion;

public class Player : MonoBehaviour
{
    public string tagPlyer;
    public string fullname;
    public int powerForce;
    public int exactRatio;
    public Sprite avatar; // Hình ảnh đại diện của người chơi
    // Xác định đây là AI hay người chơi thật
    public Rigidbody ball;  // Viên bi của người chơi này
    public Animator animator;
    public GameObject playerbody;

    public int score;
   
    public float distance; // điểm thi mức quyết định thứ tự 
    public StatusPlayer statusPlayer;
    public TextMeshProUGUI positionShowMess;
    public bool isHolding;
    public bool isAI;
    public bool isDestroy;
    public int combo; // số combo hiện tại

    // Treasure effect fields
    public bool canInstantKill;
    public bool isImmune;
    public float bounceMultiplier = 1f;
    public float fireRateMultiplier = 1f;

    public int bounceBoostTurns;
    public int fireRateBoostTurns;
    public int immunityTurns;
    public Player(string tagPlyer,
        string fullname,
        int powerForce,
        int exactRatio,
        Sprite avatar,
        bool isAI, 
        Rigidbody ball,
        Animator animator,
        GameObject playerbody,
        int score,
        bool isDestroy,
        int combo,
        float distance,
        StatusPlayer statusPlayer,
        bool isHolding,
        TextMeshProUGUI positionShowMess)
    {
        this.tagPlyer = tagPlyer;
        this.fullname = fullname;
        this.powerForce = powerForce;
        this.exactRatio = exactRatio;
        this.avatar = avatar;
        this.isAI = isAI;
        this.ball = ball;
        this.animator = animator;
        this.playerbody = playerbody;
        this.score = score;
        this.isDestroy = isDestroy;
        this.combo = combo;
        this.distance = distance;
        this.statusPlayer = statusPlayer;
        this.isHolding = isHolding;
        this.positionShowMess = positionShowMess;
    }

    // Called at the end of each turn to update temporary effects
    public void OnTurnEnd()
    {
        if (bounceBoostTurns > 0)
        {
            bounceBoostTurns--;
        }

        if (fireRateBoostTurns > 0)
        {
            fireRateBoostTurns--;
        }

        if (immunityTurns > 0)
        {
            immunityTurns--;
        }
    }
}
public class PlayerModelGameOnline : MonoBehaviour
{
    public int playerId;
    public int turnOrder;
    public int level;
    public string fullname;
    public int powerForce;
    public int exactRatio;
    public Sprite avatar; 
    public NetworkObject ball;  // Viên bi của người chơi này
    public Animator animator;
    public NetworkObject playerbody;
    public GameObject fPPPosition;
    public GameObject fingerPlayer;
    public int score;
    public int combo; // số combo hiện tại
    public float distance; // điểm thi mức quyết định thứ tự
    public StatusPlayer statusPlayer;
    public TextMeshProUGUI positionShowMess;
    public bool isHolding;
    public bool isDestroy;
}

 
 
