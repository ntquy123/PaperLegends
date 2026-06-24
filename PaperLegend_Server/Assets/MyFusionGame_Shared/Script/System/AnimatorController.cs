using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AnimatorController : MonoBehaviour
{
    public static AnimatorController Instance;
    private string lastIdle = "";
    private static readonly List<string> idleAnimations = new List<string>
    {
        "DwarfIdle",
       // "Dancing",
        "Angry",
        "Sitting Idle",
       // "Laying"
    };
    private void Awake()
    {
        Instance = this;
    }

    public int IdleAnimationCount => idleAnimations.Count;
    public IEnumerator SetWaitAnimation(NetworkObject currentPlayer, bool value)
    {
        var ani = currentPlayer.GetComponent<Animator>();
        if (ani != null)
        {
            ani.CrossFade("DwarfIdle", 0.1f);
            yield return new WaitForSeconds(0.5f);
        }
    }
    public IEnumerator SetMoveAnimation(NetworkObject currentPlayer, bool value)
    {
        var ani = currentPlayer.GetComponent<Animator>();
        if (ani != null)
        {
            ani.CrossFade("GoofyRunning", 0.1f);
            yield return new WaitForSeconds(0.5f);
        }
    }
    public IEnumerator SetWalkAnimation(NetworkObject currentPlayer, bool value)
    {
        var ani = currentPlayer.GetComponent<Animator>();
        if ( ani != null)
        {
            ani.CrossFade("GoofyRunning", 0.1f);
            yield return new WaitForSeconds(0.5f);
        }

    }

    public void SetWaitingAnimation(Animator ani)
    {
        if (ani != null)
        {
            List<string> options = idleAnimations
                .Where(anim => anim != lastIdle)
                .ToList();

            string newIdle = options[Random.Range(0, options.Count)];

            lastIdle = newIdle;
            ani.CrossFade(newIdle, 0.1f);
        }
    }

    public void SetWaitingAnimation(Animator ani, int index)
    {
        if (ani != null && idleAnimations.Count > 0)
        {
            index = Mathf.Clamp(index, 0, idleAnimations.Count);
            string newIdle = idleAnimations[index];
            lastIdle = newIdle;
            ani.CrossFade(newIdle, 0.1f);
        }
    }
    public IEnumerator SetDanceAnimation(NetworkObject currentPlayer,bool value)
    {
        var ani = currentPlayer.GetComponent<Animator>();
        if ( ani != null)
        {
            ani.CrossFade("OldManIdle", 0.1f);
            yield return new WaitForSeconds(0.5f);
        }
 

    }
    public IEnumerator SetSitForShootAnimation(NetworkObject currentPlayer, bool value)
    {
        var ani = currentPlayer.GetComponent<Animator>();
        if (ani != null)
        {
           // ani.CrossFade("DwarfIdle", 0.1f);
            ani.CrossFade("Standing To Crouched", 0.1f);
            yield return new WaitForSeconds(0.5f);
        }
    }


}
