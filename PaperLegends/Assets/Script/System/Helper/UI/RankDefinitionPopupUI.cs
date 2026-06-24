using UnityEngine;

public class RankDefinitionPopupUI : MonoBehaviour
{
    [Header("Rank Definition UI")]
    [SerializeField]
    private Transform rankDefinitionContent;

    public Transform RankDefinitionContent => rankDefinitionContent;
}
