using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [Header("Волны")]
    public WaveData[] waves;
    public int currentWave = 0;
    private int currentWaveIndex = -1;
    private bool waveInProgress = false;
    private int virusesRemaining = 0;

    [Header("Подготовка")]
    public float preparationTime = 45f;
    public float timeBetweenWaves = 30f; 
    private bool isPreparationPhase = false;
    private float preparationTimer = 0f;

    // Флаг для предотвращения автоматического старта
    private bool isInitialized = false;

    // События
    public event Action<int> OnWaveStarted;
    public event Action<int> OnWaveCompleted;
    public event Action OnPreparationStarted;
    public event Action OnPreparationEnded;
    public event Action<int> OnVirusSpawned;
    public event Action<int> OnVirusDestroyed;
    public event Action<float> OnPreparationTimerUpdated;

    // Свойства для доступа
    public int CurrentWave => currentWaveIndex + 1;
    public bool WaveInProgress => waveInProgress;
    public int VirusesRemaining => virusesRemaining;
    public bool IsPreparationPhase => isPreparationPhase;
    public float PreparationTimer => preparationTimer;

    void Start()
    {
        Debug.Log($"WaveManager Start: waves={(waves != null ? waves.Length : 0)}, prepTime={preparationTime}");
        isPreparationPhase = false;
        preparationTimer = 0f;
    }

    void Update()
    {
        if (PauseMenu.Instance != null && PauseMenu.Instance.IsGamePaused())
            return;

        if (!isInitialized) return;
        if (GameManager.Instance == null || !GameManager.Instance.isGameActive) return;

        if (isPreparationPhase)
        {
            UpdatePreparationPhase();
        }
    }

    public void StartPreparationPhase()
    {
        Debug.Log($"WaveManager: Начало начальной подготовки. Времени: {preparationTime} сек");
        isInitialized = true;
        StartPreparation(preparationTime, true);
    }

    void StartPreparationBetweenWaves()
    {
        Debug.Log($"WaveManager: Подготовка между волнами. Времени: {timeBetweenWaves} сек");
        StartPreparation(timeBetweenWaves, false);
    }

    void StartPreparation(float duration, bool isInitial)
    {
        preparationTimer = duration;
        isPreparationPhase = true;
        waveInProgress = false;

        if (isInitial)
        {
            currentWaveIndex = -1;
            currentWave = 0;
            ClearAllViruses();
        }

        OnPreparationStarted?.Invoke();
    }

    void UpdatePreparationPhase()
    {
        if (!isPreparationPhase) return;

        preparationTimer -= Time.deltaTime;
        OnPreparationTimerUpdated?.Invoke(preparationTimer);

        if (preparationTimer <= 0)
        {
            EndPreparationPhase();
        }
    }

    void EndPreparationPhase()
    {
        Debug.Log("WaveManager: Фаза подготовки завершена");
        isPreparationPhase = false;
        OnPreparationEnded?.Invoke();

        if (currentWaveIndex == -1) // Начальная подготовка
        {
            StartCoroutine(StartFirstWaveAfterDelay(1f));
        }
        else // Подготовка между волнами
        {
            StartNextWave();
        }
    }

    IEnumerator StartFirstWaveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartNextWave();
    }

    public void SkipPreparation()
    {
        if (!isPreparationPhase) return;
        Debug.Log("WaveManager: Пропуск подготовки");
        preparationTimer = 0.1f;
    }

    public void ClearAllViruses()
    {
        if (ObjectRegistry.Instance != null)
        {
            List<Virus> allViruses = ObjectRegistry.Instance.GetAllViruses();
            foreach (Virus virus in allViruses)
            {
                if (virus != null) Destroy(virus.gameObject);
            }
        }
        virusesRemaining = 0;
        Debug.Log("WaveManager: Все вирусы очищены");
    }

    public void StartNextWave()
    {
        if (waves == null || waves.Length == 0)
        {
            Debug.LogError("WaveManager: Нет волн для запуска!");
            return;
        }

        if (currentWaveIndex >= waves.Length - 1)
        {
            if (virusesRemaining <= 0)
            {
                Debug.Log("WaveManager: Все волны пройдены, победа!");
                GameManager.Instance.Victory();
            }
            return;
        }

        currentWaveIndex++;
        currentWave = currentWaveIndex + 1;
        WaveData wave = waves[currentWaveIndex];
        Debug.Log($"WaveManager: Начало волны {currentWave} ({wave.virusCount} вирусов)");

        waveInProgress = true;
        StartCoroutine(SpawnWave(wave));
        OnWaveStarted?.Invoke(CurrentWave);
    }

    IEnumerator SpawnWave(WaveData wave)
    {
        virusesRemaining = wave.virusCount;
        Debug.Log($"WaveManager: Спавн {wave.virusCount} вирусов");

        for (int i = 0; i < wave.virusCount; i++)
        {
            if (GameManager.Instance == null ||
                !GameManager.Instance.isGameActive ||
                isPreparationPhase ||
                !waveInProgress)
            {
                Debug.Log($"WaveManager: Спавн прерван");
                yield break;
            }

            SpawnVirus(wave);
            yield return new WaitForSeconds(wave.spawnInterval);
        }

        Debug.Log($"WaveManager: Все вирусы волны {currentWave} заспавнены");
    }

    void SpawnVirus(WaveData wave)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("WaveManager: GameManager не найден!");
            return;
        }

        if (GameManager.Instance.spawnNodes == null || GameManager.Instance.spawnNodes.Count == 0)
        {
            Debug.LogError("WaveManager: Нет спавн-узлов!");
            return;
        }

        NetworkNode spawnNode = GameManager.Instance.spawnNodes[
            UnityEngine.Random.Range(0, GameManager.Instance.spawnNodes.Count)];

        VirusType virusType = GetRandomVirusType(wave);
        GameObject virusPrefab = GetVirusPrefab(virusType);

        if (virusPrefab == null)
        {
            Debug.LogError($"WaveManager: Не найден префаб вируса типа {virusType}!");
            return;
        }

        GameObject virus = Instantiate(virusPrefab, spawnNode.transform.position, Quaternion.identity);
        Virus virusScript = virus.GetComponent<Virus>();

        if (virusScript != null)
        {
            virusScript.startNode = spawnNode;
            virusScript.targetNode = GameManager.Instance.criticalNode;
            Debug.Log($"WaveManager: Создан вирус типа {virusType}");
        }
        else
        {
            Debug.LogError("WaveManager: Скрипт Virus не найден на префабе!");
        }

        OnVirusSpawned?.Invoke(virusesRemaining);
    }

    VirusType GetRandomVirusType(WaveData wave)
    {
        float rand = UnityEngine.Random.value;
        if (rand < wave.normalVirusChance)
            return VirusType.Normal;
        else if (rand < wave.normalVirusChance + wave.fastVirusChance)
            return VirusType.Fast;
        else
            return VirusType.Tank;
    }

    GameObject GetVirusPrefab(VirusType type)
    {
        if (GameManager.Instance == null || GameManager.Instance.virusPrefabs == null)
        {
            Debug.LogError("WaveManager: GameManager или префабы вирусов не найдены!");
            return null;
        }

        foreach (var prefab in GameManager.Instance.virusPrefabs)
        {
            if (prefab == null) continue;
            Virus virus = prefab.GetComponent<Virus>();
            if (virus != null && virus.type == type)
                return prefab;
        }

        if (GameManager.Instance.virusPrefabs.Length > 0)
            return GameManager.Instance.virusPrefabs[0];

        return null;
    }

    public void VirusDestroyed()
    {
        if (virusesRemaining <= 0) return;

        virusesRemaining--;
        OnVirusDestroyed?.Invoke(virusesRemaining);

        if (virusesRemaining <= 0 && waveInProgress)
        {
            EndWave();
        }
    }

    public void VirusReachedCritical()
    {
        if (virusesRemaining <= 0) return;

        virusesRemaining--;
        OnVirusDestroyed?.Invoke(virusesRemaining);

        if (virusesRemaining <= 0 && waveInProgress)
        {
            EndWave();
        }
    }

    void EndWave()
    {
        waveInProgress = false;

        if (currentWaveIndex >= waves.Length - 1)
        {
            Debug.Log($"WaveManager: Последняя волна {currentWave} завершена");
            GameManager.Instance.Victory();
            return;
        }

        // Запускаем подготовку между волнами
        OnWaveCompleted?.Invoke(CurrentWave);
        StartPreparationBetweenWaves();

    }
}