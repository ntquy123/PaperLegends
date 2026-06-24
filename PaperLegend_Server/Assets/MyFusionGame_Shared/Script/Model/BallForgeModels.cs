using System;
using System.Text;
using UnityEngine;

[Serializable]
public class BallForgeSkillRequest
{
    public int playerId;
    public int itemId;
    public int seq;
    public int slotIndex;
    public int materialItemId;
    public int materialSeq;
    public int ringballCost;
    public BallForgeSkillData skill;
}

[Serializable]
public class BallForgeActivationRequest
{
    public int playerId;
    public int itemId;
    public int seq;
    public int slotIndex;
}
[Serializable]
public class BallForgeSkillData
{
    public BallForgeSkillType SkillType = BallForgeSkillType.BallShootingTechnique;
    public BallTechniqueBonuses BallShootingTechnique;
    public SupportSkillBonus SupportBonus;

    public bool HasAnyBonus()
    {
        return SkillType switch
        {
            BallForgeSkillType.BallShootingTechnique => BallShootingTechnique != null && BallShootingTechnique.HasAnyBonus(),
            BallForgeSkillType.Support => SupportBonus != null && SupportBonus.AdditionalCharges > 0 && SupportBonus.SupportedSkillId > 0,
            _ => false
        };
    }

    public string BuildDescription()
    {
        if (SkillType == BallForgeSkillType.Support && SupportBonus != null)
        {
            return $"Hỗ trợ: +{SupportBonus.AdditionalCharges} lần sử dụng cho kỹ năng #{SupportBonus.SupportedSkillId}";
        }

        var sb = new StringBuilder();
        if (BallShootingTechnique != null)
        {
            AppendStat(sb, "Khối lượng", BallShootingTechnique.MassBonus);
            AppendStat(sb, "Hệ số trọng lực", BallShootingTechnique.GravityScaleBonus);
            AppendStat(sb, "Lực cản", BallShootingTechnique.DragBonus);
            AppendStat(sb, "Độ nảy", BallShootingTechnique.BouncinessBonus);
            AppendStat(sb, "Độ đàn hồi", BallShootingTechnique.ElasticityBonus);
            AppendStat(sb, "Kháng va chạm", BallShootingTechnique.ImpactResistanceBonus);
        }
        return sb.Length == 0 ? "Không có chỉ số được tăng." : sb.ToString();
    }

    private void AppendStat(StringBuilder sb, string label, float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        if (sb.Length > 0)
            sb.Append(" | ");

        sb.Append($"{label}: +{value:F3}");
    }
}
[Serializable]
public class BallTechniqueBonuses
{
    public float MassBonus;
    public float GravityScaleBonus;
    public float DragBonus;
    public float BouncinessBonus;
    public float ElasticityBonus;
    public float ImpactResistanceBonus;

    public bool HasAnyBonus()
    {
        return MassBonus != 0f || GravityScaleBonus != 0f || DragBonus != 0f || BouncinessBonus != 0f || ElasticityBonus != 0f || ImpactResistanceBonus != 0f;
    }
}

[Serializable]
public class SupportSkillBonus
{
    public int SupportedSkillId;
    public int AdditionalCharges;
}
