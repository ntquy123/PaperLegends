using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomPopupUI : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField betInputField;
    [SerializeField]
    private Toggle maxPlayer2Toggle;
    [SerializeField]
    private Toggle maxPlayer3Toggle;
    [SerializeField]
    private TextMeshProUGUI roundCountText;
    [SerializeField]
    private Button decreaseRoundButton;
    [SerializeField]
    private Button increaseRoundButton;
    [SerializeField]
    private Transform mapGridParent;
    [SerializeField]
    private MapSelectionItem mapItemPrefab;
    [SerializeField]
    private Button createButton;
    [SerializeField]
    private Button closeButton;

    private readonly List<MapSelectionItem> spawnedItems = new();
    private Action<int, int, GameMapId, int> onCreateRoom;
    private Action onClose;
    private GameMapId selectedMapId = GameMapId.HometownHouse;
    private int fallbackBet;
    private int selectedRoundCount = MinRoundCount;
    private const int MinRoundCount = 5;
    private const int MaxRoundCount = 10;

    public void Initialize(IEnumerable<MapOptionData> maps, int defaultBet, GameMapId defaultMapId, int defaultRoundCount, Action<int, int, GameMapId, int> onCreate, Action onCloseCallback)
    {
        onCreateRoom = onCreate;
        onClose = onCloseCallback;
        fallbackBet = defaultBet > 0 ? defaultBet : 1;
        selectedMapId = defaultMapId;
        selectedRoundCount = Mathf.Clamp(defaultRoundCount, MinRoundCount, MaxRoundCount);

        if (betInputField != null)
            betInputField.text = fallbackBet.ToString();

        if (maxPlayer2Toggle != null)
            maxPlayer2Toggle.isOn = false;

        if (maxPlayer3Toggle != null)
            maxPlayer3Toggle.isOn = true;

        BuildMapGrid(maps);
        BindButtons();
        RefreshRoundControls();
    }

    private void BuildMapGrid(IEnumerable<MapOptionData> maps)
    {
        if (mapGridParent == null || mapItemPrefab == null || maps == null)
            return;

        foreach (Transform child in mapGridParent)
            Destroy(child.gameObject);

        spawnedItems.Clear();

        foreach (var map in maps)
        {
            var item = Instantiate(mapItemPrefab, mapGridParent);
            item.Bind(map, OnMapItemSelected);
            spawnedItems.Add(item);
        }

        if (spawnedItems.Count > 0)
        {
            var defaultItem = spawnedItems.Find(i => i.MapId == selectedMapId) ?? spawnedItems[0];
            OnMapItemSelected(defaultItem);
        }
    }

    private void BindButtons()
    {
        if (createButton != null)
        {
            createButton.onClick.RemoveAllListeners();
            createButton.onClick.AddListener(HandleCreateClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePopup);
        }

        if (decreaseRoundButton != null)
        {
            decreaseRoundButton.onClick.RemoveAllListeners();
            decreaseRoundButton.onClick.AddListener(() => ChangeRoundCount(-1));
        }

        if (increaseRoundButton != null)
        {
            increaseRoundButton.onClick.RemoveAllListeners();
            increaseRoundButton.onClick.AddListener(() => ChangeRoundCount(1));
        }
    }

    private void ChangeRoundCount(int delta)
    {
        int nextValue = Mathf.Clamp(selectedRoundCount + delta, MinRoundCount, MaxRoundCount);
        if (nextValue == selectedRoundCount)
            return;

        selectedRoundCount = nextValue;
        RefreshRoundControls();
    }

    private void RefreshRoundControls()
    {
        if (roundCountText != null)
            roundCountText.text = selectedRoundCount.ToString();

        if (decreaseRoundButton != null)
        {
            bool canDecrease = selectedRoundCount > MinRoundCount;
            decreaseRoundButton.interactable = canDecrease;
            decreaseRoundButton.gameObject.SetActive(true);
        }

        if (increaseRoundButton != null)
        {
            bool canIncrease = selectedRoundCount < MaxRoundCount;
            increaseRoundButton.interactable = canIncrease;
            increaseRoundButton.gameObject.SetActive(canIncrease);
        }
    }

    private void OnMapItemSelected(MapSelectionItem item)
    {
        if (item == null)
            return;

        selectedMapId = item.MapId;

        foreach (var spawnedItem in spawnedItems)
            spawnedItem.SetSelected(spawnedItem == item);
    }

    private void HandleCreateClicked()
    {
        int betCount = fallbackBet;
        if (betInputField != null && int.TryParse(betInputField.text, out var parsedBet) && parsedBet > 0)
            betCount = parsedBet;

        int maxPlayer = GetSelectedMaxPlayer();
        onCreateRoom?.Invoke(betCount, maxPlayer, selectedMapId, selectedRoundCount);
    }

    private int GetSelectedMaxPlayer()
    {
        if (maxPlayer3Toggle != null && maxPlayer3Toggle.isOn)
            return 3;

        if (maxPlayer2Toggle != null && maxPlayer2Toggle.isOn)
            return 2;

        return 3;
    }

    private void ClosePopup()
    {
        if (onClose != null)
            onClose.Invoke();
        else
            Destroy(gameObject);
    }
}
