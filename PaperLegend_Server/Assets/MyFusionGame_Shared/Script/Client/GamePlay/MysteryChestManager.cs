using System.Collections.Generic;
using UnityEngine;

public class MysteryChestManager : MonoBehaviour
{
    public List<Transform> spawnPoints = new List<Transform>();
    public GameObject chestPrefab;
    public int turnCounter;
    public GameObject currentChest;

    public void Init()
    {
        SpawnChest();
    }

    public void SpawnChest()
    {
        if (currentChest != null)
        {
            Destroy(currentChest);
        }

        if (spawnPoints == null || spawnPoints.Count == 0 || chestPrefab == null)
        {
            Debug.LogWarning("Missing spawn points or chest prefab in MysteryChestManager.");
            return;
        }

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
        currentChest = Instantiate(chestPrefab, spawnPoint.position, spawnPoint.rotation);
    }

    public void OnTurnEnd(bool isContinue)
    {
        if (!isContinue)
        {
            turnCounter++;
        }

        if (turnCounter >= 3)
        {
            turnCounter = 0;
            SpawnChest();
        }
    }
}
