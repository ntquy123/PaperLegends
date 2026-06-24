using UnityEngine;

[System.Serializable]
public class RewardItem
{
    public string rewardType { get; set; }
    public int seq { get; set; }
    public int locationId { get; set; }
    public int? itemId { get; set; }
    public int rewardAmount { get; set; }
    public bool isUsed { get; set; }
    public bool isGiftReceived { get; set; }
    public bool isComplete { get; set; }
    public string updatedAt { get; set; }
    public int countGif { get; set; }

    public void UpdateFrom(RewardItem source)
    {
        if (source == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(source.rewardType))
        {
            rewardType = source.rewardType;
        }

        if (source.seq > 0)
        {
            seq = source.seq;
        }

        if (source.locationId > 0)
        {
            locationId = source.locationId;
        }

        if (source.itemId.HasValue)
        {
            itemId = source.itemId;
        }

        rewardAmount = source.rewardAmount;
        isUsed = source.isUsed;
        isGiftReceived = source.isGiftReceived;
        isComplete = source.isComplete;

        if (!string.IsNullOrEmpty(source.updatedAt))
        {
            updatedAt = source.updatedAt;
        }

        countGif = Mathf.Max(0, source.countGif);
    }
}
