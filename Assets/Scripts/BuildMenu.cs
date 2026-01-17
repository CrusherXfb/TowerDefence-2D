using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BuildMenu : MonoBehaviour
{
    public static BuildMenu Instance;

    [Header("UI Elements")]
    public GameObject menuPanel;
    public GameObject optionsMenuPanel;
    public Transform buttonContainer;
    public GameObject towerButtonPrefab;
    public Text nodeInfoText;
    public Text optionsNodeInfoText;
    public Text towerInfoText;
    public Button upgradeButton;
    public Button sellButton;
    public Button closeOptionsButton;
    public Button closeButton;

    [Header("Background")]
    public GameObject backgroundPanel;

    private NetworkNode selectedNode;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Instance = this;

        menuPanel.SetActive(false);
        optionsMenuPanel.SetActive(false);

        if (backgroundPanel == null)
        {
            CreateBackgroundPanel();
        }
        else
        {
            backgroundPanel.SetActive(false);
        }

        closeButton.onClick.AddListener(CloseMenu);
        closeOptionsButton.onClick.AddListener(CloseMenu);
        upgradeButton.onClick.AddListener(OnUpgradeClick);
        sellButton.onClick.AddListener(OnSellClick);
    }

    void Update()
    {
        if ((menuPanel.activeSelf || optionsMenuPanel.activeSelf) && GameManager.Instance != null)
        {
            UpdateAllButtonsState();
        }
    }

    void CreateBackgroundPanel()
    {
        backgroundPanel = new GameObject("BackgroundPanel");
        backgroundPanel.transform.SetParent(transform);

        RectTransform rect = backgroundPanel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localPosition = Vector3.zero;

        Image bgImage = backgroundPanel.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0);

        Button bgButton = backgroundPanel.AddComponent<Button>();
        bgButton.onClick.AddListener(CloseMenu);

        backgroundPanel.transform.SetAsFirstSibling();
        backgroundPanel.SetActive(false);
    }

    public void ShowForNode(NetworkNode node)
    {
        if (selectedNode != null && selectedNode != node && selectedNode.currentTower != null)
        {
            selectedNode.currentTower.isSelected = false;
            selectedNode.currentTower.rangeIndicator.SetActive(false);
        }

        selectedNode = node;

        if (backgroundPanel != null)
            backgroundPanel.SetActive(true);

        if (selectedNode.currentTower == null)
        {
            menuPanel.SetActive(true);
            optionsMenuPanel.SetActive(false);
        }
        else
        {
            if (selectedNode.currentTower != null)
            {
                selectedNode.currentTower.isSelected = true;
                selectedNode.currentTower.UpdateRangeIndicatorVisibility();
            }

            optionsMenuPanel.SetActive(true);
            menuPanel.SetActive(false);
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMenuOpen();

        UpdateUI();
        CreateTowerButtons();
        UpdateAllButtonsState(); // Первоначальное обновление
    }

    void UpdateAllButtonsState()
    {
        if (GameManager.Instance == null) return;

        int currentMoney = GameManager.Instance.money;

        foreach (Transform child in buttonContainer)
        {
            if (child.gameObject.activeSelf)
            {
                Button button = child.GetComponent<Button>();
                if (button != null)
                {
                    Text costText = null;
                    foreach (Text text in child.GetComponentsInChildren<Text>())
                    {
                        if (text.name == "CostText")
                        {
                            costText = text;
                            break;
                        }
                    }

                    if (costText != null)
                    {
                        string costString = costText.text.Replace(" $", "").Trim();
                        if (int.TryParse(costString, out int cost))
                        {
                            bool canAfford = currentMoney >= cost;
                            button.interactable = canAfford;

                            if (costText != null)
                            {
                                costText.color = canAfford ? Color.white : Color.gray;
                            }
                        }
                    }
                }
            }
        }

        UpdateUpgradeButtonState(currentMoney);
    }

    void UpdateUpgradeButtonState(int currentMoney)
    {
        if (upgradeButton == null) return;

        // Если кнопка неактивна в иерархии, выходим
        if (!upgradeButton.gameObject.activeSelf) return;

        if (selectedNode != null && selectedNode.hasTower && selectedNode.currentTower != null)
        {
            Tower tower = selectedNode.currentTower;

            bool canUpgrade = (tower.Level < tower.MaxLevel) && (currentMoney >= tower.UpgradeCost);

            upgradeButton.interactable = canUpgrade;

            Text upgradeButtonText = upgradeButton.GetComponentInChildren<Text>();
        }
        else
        {
            upgradeButton.interactable = false;

            Text upgradeButtonText = upgradeButton.GetComponentInChildren<Text>();
            if (upgradeButtonText != null)
            {
                upgradeButtonText.color = Color.gray;
            }
        }
    }

    void UpdateUI()
    {
        if (selectedNode == null) return;

        string info = $"Узел: {selectedNode.name}\n";

        if (selectedNode.hasTower)
        {
            Tower tower = selectedNode.currentTower;
            info += $"Башня: {tower.name}\n";
            info += $"Уровень: {tower.Level}/{tower.MaxLevel}\n";

            if (tower.type != TowerType.Support)
            {
                info += $"Урон: {tower.Damage}";
                if (tower.damageBuff > 0)
                {
                    info += $" +{tower.damageBuff * 100:F0}% ({tower.Damage * (1 + tower.damageBuff)})";
                }
                info += $"\nСкорость: {tower.AttackSpeed}";
                if (tower.attackSpeedBuff > 0)
                {
                    info += $" +{tower.attackSpeedBuff * 100}% ({tower.AttackSpeed * (1 + tower.attackSpeedBuff)})";
                }
            }
            else
            {
                info += $"Улучшение: {tower.SupportBoost * 100:F0}%\n";
            }

            info += $"\nСтоимость улучшения: {tower.UpgradeCost}";

            upgradeButton.gameObject.SetActive(tower.Level < tower.MaxLevel);
            sellButton.gameObject.SetActive(true);
            optionsNodeInfoText.text = info;

            if (towerInfoText != null)
                towerInfoText.text = selectedNode.currentTower.desc;
        }
        else
        {
            info += "Пустой узел";
            nodeInfoText.text = info;
            upgradeButton.gameObject.SetActive(false);
            sellButton.gameObject.SetActive(false);
        }
    }

    void CreateTowerButtons()
    {
        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        if (selectedNode.hasTower) return;

        CreateTowerButton("Замедлитель", TowerType.Slow, 70, new Color(77f / 255f, 124f / 255f, 201f / 255f));
        CreateTowerButton("Урон", TowerType.Damage, 80, Color.HSVToRGB(2f / 360f, 62f / 100f, 71f / 100f));
        CreateTowerButton("Область", TowerType.AOE, 110, Color.HSVToRGB(143f / 360f, 62f / 100f, 60f / 100f));
        CreateTowerButton("Поддержка", TowerType.Support, 90, Color.HSVToRGB(34f / 360f, 70f / 100f, 100f / 100f));
    }

    void CreateTowerButton(string name, TowerType type, int cost, Color color)
    {
        GameObject buttonObj = Instantiate(towerButtonPrefab, buttonContainer);
        Button button = buttonObj.GetComponent<Button>();
        Text[] texts = buttonObj.GetComponentsInChildren<Text>();

        foreach (Text text in texts)
        {
            if (text.name == "NameText")
                text.text = name;
            else if (text.name == "CostText")
                text.text = $"{cost} $";
        }

        Image buttonImage = buttonObj.GetComponent<Image>();
        if (buttonImage != null)
            buttonImage.color = color;

        bool canAfford = GameManager.Instance != null && GameManager.Instance.money >= cost;
        button.interactable = canAfford;

        foreach (Text text in texts)
        {
            if (text.name == "CostText")
            {
                text.color = canAfford ? Color.white : Color.gray;
            }
        }

        button.onClick.AddListener(() => OnTowerSelected(type, cost));
    }

    void OnTowerSelected(TowerType type, int cost)
    {
        if (selectedNode == null || selectedNode.hasTower) return;

        if (GameManager.Instance == null || !GameManager.Instance.SpendMoney(cost))
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayNotEnoughMoney();
            return;
        }

        GameObject towerPrefab = GetTowerPrefab(type);
        if (towerPrefab == null) return;

        bool success = selectedNode.BuildTower(towerPrefab);
        if (success)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBuildSound();
            if (BattleLogManager.Instance != null)
                BattleLogManager.Instance.AddSystemMessage(
                    $"Башня {type} размещена на узле {selectedNode.name}"
                );
            CloseMenu();
        }
    }

    GameObject GetTowerPrefab(TowerType type)
    {
        if (GameManager.Instance == null || GameManager.Instance.towerPrefabs == null)
            return null;

        foreach (var prefab in GameManager.Instance.towerPrefabs)
        {
            if (prefab == null) continue;

            Tower tower = prefab.GetComponent<Tower>();
            if (tower != null && tower.type == type)
                return prefab;
        }
        Debug.LogError($"Не найден префаб для башни типа: {type}");
        return null;
    }

    void OnUpgradeClick()
    {
        if (selectedNode == null || !selectedNode.hasTower) return;

        Tower tower = selectedNode.currentTower;
        if (tower == null) return;

        if (GameManager.Instance == null || !GameManager.Instance.SpendMoney(tower.UpgradeCost))
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayNotEnoughMoney();
            return;
        }

        bool success = tower.Upgrade();
        if (success)
        {
            UpdateUI(); // Обновим UI чтобы показать новый уровень
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUpgradeSound();
            if (tower.type == TowerType.Support)
            {
                tower.ApplySupportToNearbyTowers();
            }

            UpdateAllButtonsState();
        }
    }

    void OnSellClick()
    {
        if (selectedNode == null || !selectedNode.hasTower) return;

        selectedNode.SellTower();
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySellSound();

        CloseMenu();
    }

    public void CloseMenu()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMenuClose();

        menuPanel.SetActive(false);
        optionsMenuPanel.SetActive(false);

        if (selectedNode != null && selectedNode.currentTower != null)
        {
            selectedNode.currentTower.isSelected = false;
            selectedNode.currentTower.UpdateRangeIndicatorVisibility();
        }

        if (backgroundPanel != null)
            backgroundPanel.SetActive(false);

        if (selectedNode != null)
        {
            selectedNode.SetSelected(false);
            selectedNode = null;
        }
    }
}