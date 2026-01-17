using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Ссылки на менеджеры (перетащи из сцены)")]
    [SerializeField] public ResourceManager resourceManager;
    [SerializeField] private WaveManager waveManager;

    [Header("Основные настройки игры")]
    public int startMoney = 180;
    public int startHealth = 30;
    public float preparationTime = 40f;

    [Header("Волны")]
    public WaveData[] waves => waveManager != null ? waveManager.waves : null;

    [Header("Сеть")]
    public List<NetworkNode> allNodes = new List<NetworkNode>();
    public NetworkNode criticalNode;
    public List<NetworkNode> spawnNodes = new List<NetworkNode>();

    [Header("Префабы")]
    public GameObject[] towerPrefabs;
    public GameObject[] virusPrefabs;

    [Header("UI - Основные")]
    public Text moneyText;
    public Text healthText;
    public Text waveText;
    public Text preparationText;
    public Button skipPreparationButton;

    [Header("UI - Панели окончания игры")]
    public GameObject gameOverPanel;
    public GameObject victoryPanel;
    public Button restartButtonGameOver;
    public Button menuButtonGameOver;
    public Button restartButtonVictory;
    public Button menuButtonVictory;

    [Header("Меню паузы")]
    public PauseMenu pauseMenu;

    [Header("Отладка")]
    public bool debugMode = false;

    // Текущее состояние игры
    public bool isGameActive { get; private set; } = true;

    // Свойства для доступа к данным менеджеров
    public int money => resourceManager != null ? resourceManager.Money : 0;
    public int health => resourceManager != null ? resourceManager.Health : 0;
    public int currentWave => waveManager != null ? waveManager.CurrentWave : 0;
    public bool isPreparationPhase => waveManager != null ? waveManager.IsPreparationPhase : true;
    public bool waveInProgress => waveManager != null ? waveManager.WaveInProgress : false;
    public int virusesRemaining => waveManager != null ? waveManager.VirusesRemaining : 0;

    void Awake()
    {
        // Реализация Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (debugMode) Debug.Log("GameManager: Awake()");
    }

    void Start()
    {
        if (debugMode) Debug.Log("GameManager: Start()");

        // Шаг 1: Находим менеджеры, если они не установлены в инспекторе
        FindAndValidateManagers();

        // Шаг 2: Настраиваем менеджеры
        SetupManagers();

        // Шаг 3: Включаем игровую музыку (ДО загрузки игры!)
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayGameMusic();
            Debug.Log("GameManager: Игровая музыка включена");
        }

        // Шаг 4: Запускаем игру
        Time.timeScale = 1f;
        StartCoroutine(InitializeGame());
    }

    void FindAndValidateManagers()
    {
        if (debugMode) Debug.Log("GameManager: Поиск менеджеров...");

        // Поиск ResourceManager
        if (resourceManager == null)
        {
            resourceManager = FindAnyObjectByType<ResourceManager>();
            if (resourceManager == null)
                Debug.LogError("GameManager: ResourceManager не найден в сцене!");
            else if (debugMode)
                Debug.Log("GameManager: ResourceManager найден автоматически");
        }

        // Поиск WaveManager
        if (waveManager == null)
        {
            waveManager = FindAnyObjectByType<WaveManager>();
            if (waveManager == null)
                Debug.LogError("GameManager: WaveManager не найден в сцене!");
            else if (debugMode)
                Debug.Log("GameManager: WaveManager найден автоматически");
        }

        // Проверяем наличие волн
        if (waves == null || waves.Length == 0)
            Debug.LogError("GameManager: Не заданы волны (waves array)!");
    }

    void SetupManagers()
    {
        if (debugMode) Debug.Log("GameManager: Настройка менеджеров...");

        // Настройка ResourceManager
        if (resourceManager != null)
        {
            resourceManager.startMoney = startMoney;
            resourceManager.startHealth = startHealth;

            // Подписываемся на события изменения ресурсов
            resourceManager.OnMoneyChanged += OnMoneyUpdated;
            resourceManager.OnHealthChanged += OnHealthUpdated;

            if (debugMode) Debug.Log($"GameManager: ResourceManager настроен (money={startMoney}, health={startHealth})");
        }

        // Настройка WaveManager
        if (waveManager != null)
        {
            waveManager.waves = waves;
            waveManager.preparationTime = preparationTime;

            // Подписываемся на события волн
            waveManager.OnPreparationStarted += OnPreparationStarted;
            waveManager.OnPreparationEnded += OnPreparationEnded;
            waveManager.OnWaveStarted += OnWaveStarted;
            waveManager.OnWaveCompleted += OnWaveCompleted;

            // НОВОЕ: Подписываемся на обновление таймера
            waveManager.OnPreparationTimerUpdated += OnPreparationTimerUpdated;

            if (debugMode) Debug.Log($"GameManager: WaveManager настроен (волн={waves.Length}, подготовка={preparationTime}сек)");
        }
    }

    IEnumerator InitializeGame()
    {
        if (debugMode) Debug.Log("GameManager: Инициализация игры...");

        // Ждем один кадр, чтобы все компоненты успели инициализироваться
        yield return new WaitForEndOfFrame();

        // Инициализируем ресурсы
        if (resourceManager != null)
            resourceManager.ResetResources();

        // Находим все узлы сети
        FindAllNodes();

        // Настраиваем UI кнопки
        SetupUIButtons();

        // Обновляем UI
        UpdateUI();

        // Скрываем панели окончания игры
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);

        if (debugMode) Debug.Log("GameManager: Инициализация завершена, запуск подготовки...");

        // Запускаем фазу подготовки через WaveManager
        waveManager?.StartPreparationPhase();
    }

    void FindAllNodes()
    {
        if (debugMode) Debug.Log("GameManager: Поиск узлов сети...");

        if (ObjectRegistry.Instance != null)
        {
            allNodes = ObjectRegistry.Instance.GetAllNodes();
            if (debugMode) Debug.Log($"GameManager: Найдено узлов через ObjectRegistry: {allNodes.Count}");
        }

        // Ищем критические и спавн-узлы
        criticalNode = null;
        spawnNodes.Clear();

        foreach (var node in allNodes)
        {
            if (node.isCritical)
            {
                criticalNode = node;
                if (debugMode) Debug.Log($"GameManager: Критический узел найден: {node.name}");
            }

            if (node.isSpawnPoint)
                spawnNodes.Add(node);
        }

        // Проверяем наличие необходимых узлов
        if (spawnNodes.Count == 0)
            Debug.LogError("GameManager: Спавн-узлы не найдены!");

        if (criticalNode == null)
            Debug.LogError("GameManager: Критический узел не найден!");

        if (debugMode)
            Debug.Log($"GameManager: Узлы найдены - всего: {allNodes.Count}, спавнов: {spawnNodes.Count}");
    }

    void SetupUIButtons()
    {
        if (debugMode) Debug.Log("GameManager: Настройка кнопок UI...");

        // Кнопки рестарта и меню
        if (restartButtonGameOver != null)
            restartButtonGameOver.onClick.AddListener(RestartGame);

        if (menuButtonGameOver != null)
            menuButtonGameOver.onClick.AddListener(ReturnToMainMenu);

        if (restartButtonVictory != null)
            restartButtonVictory.onClick.AddListener(RestartGame);

        if (menuButtonVictory != null)
            menuButtonVictory.onClick.AddListener(ReturnToMainMenu);

        // Кнопка пропуска подготовки
        if (skipPreparationButton != null)
        {
            skipPreparationButton.onClick.RemoveAllListeners();
            skipPreparationButton.onClick.AddListener(SkipPreparation);
            skipPreparationButton.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // Проверка паузы
        if (PauseMenu.Instance != null && PauseMenu.Instance.IsGamePaused())
            return;

        if (!isGameActive) return;
    }

    // ========== ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ ВНЕШНИХ СКРИПТОВ ==========

    public void SkipPreparation()
    {
        waveManager?.SkipPreparation();

        if (skipPreparationButton != null)
        {
            skipPreparationButton.interactable = false;
        }
    }

    public void StartNextWave() => waveManager?.StartNextWave();

    public void AddMoney(int amount) => resourceManager?.AddMoney(amount);

    public bool SpendMoney(int amount) => resourceManager?.SpendMoney(amount) ?? false;

    public void VirusDestroyed(int bounty)
    {
        if (debugMode) Debug.Log($"GameManager: Вирус уничтожен, награда: {bounty}");

        AddMoney(bounty);
        waveManager?.VirusDestroyed();
    }

    public void VirusReachedCritical()
    {
        if (debugMode) Debug.Log($"GameManager: Вирус достиг базы");

        resourceManager?.ChangeHealth(-1);
        waveManager?.VirusReachedCritical();

        if (health <= 0)
        {
            GameOver();
        }
    }

    void GameOver()
    {
        if (debugMode) Debug.Log("GameManager: Поражение!");

        isGameActive = false;
        Time.timeScale = 0f;

        // Снимаем паузу если она была
        if (PauseMenu.Instance != null)
        {
            PauseMenu.Instance.ContinueGame();
        }

        if (gameOverPanel != null)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayGameOver();

            gameOverPanel.SetActive(true);
        }
    }

    public void Victory()
    {
        if (debugMode) Debug.Log("GameManager: Победа!");

        isGameActive = false;
        Time.timeScale = 0f;

        // Снимаем паузу если она была
        if (PauseMenu.Instance != null)
        {
            PauseMenu.Instance.ContinueGame();

            if (victoryPanel != null)
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayVictory();
                }
                victoryPanel.SetActive(true);
            }
        }
    }

    public void RestartGame()
    {
        if (debugMode) Debug.Log("GameManager: Рестарт игры");

        Time.timeScale = 1f;

        if (ObjectRegistry.Instance != null)
        {
            ObjectRegistry.Instance.ClearAll();
        }

        // Сбрасываем выбранный узел
        NetworkNode.CurrentlySelectedNode = null;

        // Снимаем паузу
        if (PauseMenu.Instance != null)
        {
            PauseMenu.Instance.ContinueGame();
        }

        // Перезагружаем сцену
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMainMenu()
    {
        if (debugMode) Debug.Log("GameManager: Возврат в главное меню");

        Time.timeScale = 1f;

        // Очищаем объекты
        if (ObjectRegistry.Instance != null)
        {
            ObjectRegistry.Instance.ClearAll();
        }

        // Сбрасываем выбранный узел
        NetworkNode.CurrentlySelectedNode = null;

        // Снимаем паузу
        if (PauseMenu.Instance != null)
        {
            PauseMenu.Instance.ContinueGame();
        }

        // Загружаем главное меню
        SceneManager.LoadScene("MainMenu");
    }

    // ========== ОБРАБОТЧИКИ СОБЫТИЙ ==========

    public void OnMoneyUpdated(int newMoney)
    {
        if (moneyText != null)
            moneyText.text = $"Деньги: {newMoney}";

        if (debugMode) Debug.Log($"GameManager: Деньги обновлены: {newMoney}");
    }

    void OnHealthUpdated(int newHealth)
    {
        if (healthText != null)
            healthText.text = $"Здоровье: {newHealth}";

        if (debugMode) Debug.Log($"GameManager: Здоровье обновлено: {newHealth}");
    }

    void OnPreparationTimerUpdated(float timeRemaining)
    {
        // ВАЖНО: Обновляем текст только если preparationText существует и активен
        if (preparationText != null && preparationText.gameObject.activeSelf)
        {
            preparationText.text = $"Подготовка: {Mathf.CeilToInt(timeRemaining)} сек";
        }
    }

    void OnPreparationStarted()
    {
        if (debugMode) Debug.Log("GameManager: Подготовка началась");
        if (BattleLogManager.Instance != null)
            BattleLogManager.Instance.AddSystemMessage(
                $"Фаза подготовки началась"
            );

        // ВАЖНОЕ ИСПРАВЛЕНИЕ: Активируем текст подготовки!
        if (preparationText != null)
        {
            preparationText.gameObject.SetActive(true);
            // Устанавливаем начальное значение
            if (waveManager != null)
            {
                preparationText.text = $"Подготовка: {Mathf.CeilToInt(waveManager.PreparationTimer)} сек";
            }
        }

        // Показываем кнопку пропуска
        if (skipPreparationButton != null)
        {
            skipPreparationButton.gameObject.SetActive(true);
            skipPreparationButton.interactable = true;
        }
    }

    void OnPreparationEnded()
    {
        if (debugMode) Debug.Log("GameManager: Подготовка закончилась");
        if (BattleLogManager.Instance != null)
            BattleLogManager.Instance.AddSystemMessage(
                $"Фаза подготовки закончилась"
            );

        // ВАЖНОЕ ИСПРАВЛЕНИЕ: Скрываем текст подготовки
        if (preparationText != null)
        {
            preparationText.gameObject.SetActive(false);
        }

        // Деактивируем кнопку пропуска (но не скрываем)
        if (skipPreparationButton != null)
        {
            skipPreparationButton.interactable = false;
        }
    }

    void OnWaveStarted(int waveNumber)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWaveStart();
        }

        if (BattleLogManager.Instance != null)
            BattleLogManager.Instance.AddSystemMessage(
                $"Волна {currentWave} началась!"
            );

        if (debugMode) Debug.Log($"GameManager: Волна {waveNumber} началась");

        UpdateUI();
    }

    void OnWaveCompleted(int waveNumber)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWaveComplete();
        }
        if (BattleLogManager.Instance != null)
            BattleLogManager.Instance.AddSystemMessage(
                $"Волна {currentWave} закончилась!"
            );
        if (debugMode) Debug.Log($"GameManager: Волна {waveNumber} завершена");

        // Награда за волну
        int baseReward = 30;
        int waveBonus = waveManager.currentWave * 10;
        int waveReward = baseReward + waveBonus;

        if (BattleLogManager.Instance != null)
            BattleLogManager.Instance.AddMoneyMessage(
                $"+{waveReward}$"
            );

        resourceManager?.AddMoney(waveReward);

        if (skipPreparationButton != null && isGameActive && !isPreparationPhase)
        {
            skipPreparationButton.interactable = true;
        }

        UpdateUI();
    }

    // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========

    void UpdateUI()
    {
        // Обновляем отображение волны
        if (waveText != null)
            waveText.text = $"Волна: {currentWave}/{waves.Length}";
    }

    void OnDestroy()
    {
        if (debugMode) Debug.Log("GameManager: Уничтожение");

        // Отписываемся от всех событий
        if (resourceManager != null)
        {
            resourceManager.OnMoneyChanged -= OnMoneyUpdated;
            resourceManager.OnHealthChanged -= OnHealthUpdated;
        }

        if (waveManager != null)
        {
            waveManager.OnPreparationStarted -= OnPreparationStarted;
            waveManager.OnPreparationEnded -= OnPreparationEnded;
            waveManager.OnWaveStarted -= OnWaveStarted;
            waveManager.OnWaveCompleted -= OnWaveCompleted;
            waveManager.OnPreparationTimerUpdated -= OnPreparationTimerUpdated; // НОВОЕ: отписка
        }
    }
}


[System.Serializable]
public class WaveData
{
    public int virusCount = 10;
    public float spawnInterval = 1f;
    [Range(0, 1)] public float normalVirusChance = 0.6f;
    [Range(0, 1)] public float fastVirusChance = 0.3f;
    [Range(0, 1)] public float tankVirusChance = 0.1f;
}