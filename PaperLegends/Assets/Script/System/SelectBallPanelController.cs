using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SelectBallPanelController : MonoBehaviour
{
    [System.Serializable]
    public class MarbleOption
    {
        public Button button;
        public int itemId;
    }

    public List<MarbleOption> marbleOptions = new List<MarbleOption>();
    public Button confirmButton;

    private int _selectedItemId;

    private void Start()
    {
        foreach (var opt in marbleOptions)
        {
            if (opt.button != null)
            {
                int id = opt.itemId;
                opt.button.onClick.AddListener(() => OnSelect(id));
            }
        }
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);
    }

    public void OnSelect(int itemId)
    {
        _selectedItemId = itemId;
    }

    public void OnConfirm()
    {
        if (_selectedItemId == 0)
            return;
        StartCoroutine(EquipSelectedBallCoroutine());
    }

    private IEnumerator EquipSelectedBallCoroutine()
    {
        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        PlayerInventorySchema data = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.EquipBallAsync(playerId, _selectedItemId),
            result => data = result));

        if (data != null)
        {
            if (UserInfoHandler.Instance != null)
            {
                UserInfoHandler.Instance.RefreshPlayerInfo(data);
            }
           // MenuController.Instance.SelectBallPanel.SetActive(false);
            MenuController.Instance.SetMainMenuActive(true);
        }
    }
}
