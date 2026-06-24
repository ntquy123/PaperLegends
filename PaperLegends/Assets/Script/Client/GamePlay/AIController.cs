using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
public class AIController : MonoBehaviour
{
    public static AIController Instance;
    public float maxShootForce = 5f; // Lực bắn tối đa
    private void Awake()
    {
        Instance = this;
    }

    public void StartAIShootforExam(List<Player> players)
    {
        StartCoroutine(AI_ShootTurns(players));
    }
    private IEnumerator AI_ShootTurns(List<Player> players)
    {
        var aiPlayers = players.Where(x => x.isAI).ToList();
        // Lần lượt bắn từng AI
        for (int i = 0; i < aiPlayers.Count; i++)
        {
            Player currentPlayer = aiPlayers[i];

            float delay = Random.Range(4f, 8f); // Ngẫu nhiên thời gian bắn
            yield return new WaitForSeconds(delay);

            // Xác định vị trí đích bắn
            Vector3 targetPosition = GetTargetPosition(currentPlayer, aiPlayers, i);

            // Xác định hướng bắn
            Vector3 shootDirection = (targetPosition - currentPlayer.ball.position).normalized;
            float shootForce = Random.Range(2f, maxShootForce);

            // Quay viên bi về hướng bắn
            Quaternion targetRotation = Quaternion.LookRotation(shootDirection);
            currentPlayer.ball.transform.rotation = Quaternion.Slerp(currentPlayer.ball.transform.rotation, targetRotation, 0.5f);

            // Bắn viên bi
            currentPlayer.isHolding = false;
            currentPlayer.ball.isKinematic = false;
            currentPlayer.ball.AddForce(shootDirection * shootForce, ForceMode.Impulse);
            //SoundManager.Instance.StartBallRollingLoop(gameObject, () => rb.velocity.magnitude);
            Debug.Log($"{currentPlayer.name} bắn với lực {shootForce}");

            currentPlayer.statusPlayer = StatusPlayer.StartPoint;
            yield return new WaitForSeconds(0.5f);
        }
        NPCController.Instance.StartCheckingTurn();
    }

    private Vector3 GetTargetPosition(Player currentPlayer, List<Player> distances, int currentIndex)
    {
        float randomValue = Random.value;
        Vector3 targetPosition = NPCController.Instance.StartPoint.transform.position;

        // 80% cơ hội bắn gần mức
        if (randomValue < 0.8f)
        {
            targetPosition.z += Random.Range(-0.3f, 0.3f);
        }
        // 10% cơ hội bắn ngay mức
        else if (randomValue < 0.9f)
        {
            targetPosition.z = NPCController.Instance.StartPoint.transform.position.z;
        }
        // 10% cơ hội bắn dưới mức
        else
        {
            targetPosition.z -= Random.Range(0.3f, 0.8f);
        }

        // Nếu có người bắn trước, 80% bắn vào người đó để đẩy họ xuống mức
        if (currentIndex > 0 && Random.value < 0.8f)
        {
            Player previousPlayer = distances[currentIndex - 1];
            targetPosition = previousPlayer.ball.position;
        }

        return targetPosition;
    }

    public IEnumerator ShootTurnAI(Player aiPlayer, Rigidbody targetBall)
    {
        // Tính toán hướng quay về mục tiêu
        Vector3 aimPosition = targetBall.position;
        Vector3 directionToTarget = (aimPosition - aiPlayer.playerbody.transform.position).normalized;

        // Chỉ quay theo trục Y (giữ nguyên trục X, Z)
        Quaternion targetRotation = Quaternion.LookRotation(new Vector3(directionToTarget.x, 0, directionToTarget.z));

        // Quay từ từ về hướng target
        float elapsedTime = 0f;
        float rotationDuration = 1f; // Thời gian quay về hướng mục tiêu

        while (elapsedTime < rotationDuration)
        {
            aiPlayer.playerbody.transform.rotation = Quaternion.Slerp(
                aiPlayer.playerbody.transform.rotation,
                targetRotation,
                elapsedTime / rotationDuration
            );

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        aiPlayer.playerbody.transform.rotation = targetRotation; // Đảm bảo quay đúng góc

        // Xác suất bắn chính xác
        float deviation = aiPlayer.exactRatio / 10f;
        if (Random.value > deviation)
        {
            float ratioCheck = 1 - deviation;
            aimPosition += new Vector3(Random.Range(-ratioCheck, ratioCheck), 0, Random.Range(-ratioCheck, ratioCheck));
        }

        // Bắn bi
        Vector3 shootDirection = (aimPosition - aiPlayer.ball.position).normalized;
        float shootForce = Random.Range(1f, aiPlayer.powerForce);

        aiPlayer.isHolding = false;
        aiPlayer.ball.isKinematic = false;
        aiPlayer.ball.AddForce(shootDirection * shootForce, ForceMode.Impulse);
       // SoundManager.Instance.StartBallRollingLoop(() => gameObject, aiPlayer.ball.velocity.magnitude, 0.25f);
    }
}
