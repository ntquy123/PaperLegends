using UnityEngine;
using UnityEngine.EventSystems;

public class ChamCatHoldButton : MonoBehaviour, IPointerClickHandler
{
    private BallServerController _target;

    public void SetTarget(BallServerController target)
    {
        _target = target;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_target == null)
            return;

        SkillManager.Instance?.BeginChamCatHold(_target);
    }
}
