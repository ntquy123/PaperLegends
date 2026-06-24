using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class DisplayText : MonoBehaviour
{
    public string characterName = "Anh Hùng";
    private TMP_Text nameText;

    void Start()
    {
        nameText = GetComponent<TMP_Text>();
        nameText.text = characterName;
    }

    void Update()
    {
        // Luôn xoay về phía camera để dễ đọc
        transform.LookAt(Camera.main.transform);
        transform.Rotate(0, 180, 0);
    }
}
