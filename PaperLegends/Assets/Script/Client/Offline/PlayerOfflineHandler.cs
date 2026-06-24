using System.Collections;
using System.Linq;
using UnityEngine;

public class PlayerOfflineHandler : MonoBehaviour
{
    public PlayerInfoStruct PlayerModel;
    [SerializeField] private float moveSpeed = 0.5f;

    public Transform FingerPosition;
    public Transform FPPPosition;
    public Transform PointPosition;

    private Vector3 _initialLookOffset;
    private Animator currentAnimator;
    private string lastPlayedAnim;

    private void Start()
    {
        if (FPPPosition != null && PointPosition != null)
            _initialLookOffset = PointPosition.localPosition - FPPPosition.localPosition;
        currentAnimator = GetComponent<Animator>();
    }

    public void Initialize(PlayerInfoStruct info)
    {
        PlayerModel = info;
    }

    public IEnumerator MoveTo(Vector3 targetPosition, Transform lookAtTarget = null)
    {
        if (targetPosition != null)
            RotateSightingPoint(targetPosition);
        yield return new WaitForSeconds(1f);

        targetPosition.y = transform.position.y;
        Vector3 dir = (targetPosition - transform.position).normalized;

        while (Vector3.Distance(transform.position, targetPosition) > 0.05f)
        {
            Vector3 step = dir * moveSpeed * Time.deltaTime;
            transform.position += step;
            yield return null;
        }

        if (lookAtTarget != null)
            RotateSightingPoint(lookAtTarget.position);
    }

    public void MoveHorizontal(int direction)
    {
        if (direction == 0) return;
        transform.position += Vector3.right * direction * moveSpeed * Time.deltaTime;
    }

    public void RotateSightingPoint(Vector3 lookAtTarget)
    {
        Vector3 lookDirection = lookAtTarget - transform.position;
        lookDirection.y = 0;
        if (lookDirection != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(lookDirection);
    }

    public void AddScoreToPlayer(int score)
    {
        PlayerModel.score += score;
    }

    public void DetermineTurnOrder()
    {
        var host = GameSessionOffline.Instance;
        var sorted = host.playerDict.Keys
            .Select(id => host.playerDict[id].GetComponent<PlayerOfflineHandler>())
            .OrderBy(p => p.PlayerModel.distance >= 0 ? p.PlayerModel.distance : -p.PlayerModel.distance)
            .ToList();
        for (int order = 0; order < sorted.Count; order++)
        {
            var handler = sorted[order];
            handler.PlayerModel.turnOrder = order;
            var entry = host.TurnOrderList.FirstOrDefault(e => e.playerId == handler.PlayerModel.playerId);
            if (entry != null) entry.turnOrder = order;
        }
    }
}
