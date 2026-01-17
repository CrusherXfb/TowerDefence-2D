using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public enum VirusType { Normal, Fast, Tank }

public class Virus : MonoBehaviour
{
    public static event Action<int> OnVirusDestroyed;
    public static event Action<int> OnVirusReachedCritical;


    [Header("Параметры вируса")]
    public VirusType type = VirusType.Normal;
    //public string name = "0";
    public float health = 100f;
    public float maxHealth = 100f;
    public float speed = 2f;
    public int bounty = 10;
    public int damageToBase = 1;

    [Header("Текущее состояние")]
    public NetworkNode currentNode;
    public NetworkNode targetNode;
    public NetworkNode startNode;
    public bool isDead = false;
    public bool reachedEnd = false;

    [Header("Эффекты")]
    public float slowMultiplier = 1f;
    private float slowTimer = 0f;

    [Header("Визуализация")]
    public SpriteRenderer spriteRenderer;

    [Header("Путь")]
    private List<NetworkNode> path = new List<NetworkNode>();
    private int currentPathIndex = 0;

    [Header("Настройки рандомизации пути")]
    public float randomPathChance = 0.3f; 
    public bool useSmartRandomization = true; 

    void Start()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        SetupVirusType();
        FindRandomizedPath();

        StartCoroutine(FollowPath());

        if (ObjectRegistry.Instance != null)
        {
            ObjectRegistry.Instance.RegisterVirus(this);
        }
    }

    void OnDestroy()
    {
        if (ObjectRegistry.Instance != null)
        {
            ObjectRegistry.Instance.UnregisterVirus(this);
        }
    }

    void SetupVirusType()
    {
        if (GameManager.Instance == null) return;

        int currentWave = GameManager.Instance.currentWave;
        float healthMultiplier = CalculateHealthMultiplier(currentWave);

        switch (type)
        {
            case VirusType.Normal:
                maxHealth = 30f * healthMultiplier;
                health = maxHealth;
                speed = 2.0f * (1f + (currentWave * 0.02f)); 
                bounty = 7 + Mathf.RoundToInt(currentWave * 0.5f);
                break;

            case VirusType.Fast:
                maxHealth = 17f * healthMultiplier;
                health = maxHealth;
                speed = 3.8f * (1f + (currentWave * 0.01f)); 
                bounty = 5 + Mathf.RoundToInt(currentWave * 0.7f);
                break;

            case VirusType.Tank:
                maxHealth = 120f * healthMultiplier;
                health = maxHealth;
                speed = 0.9f;
                bounty = 15 + Mathf.RoundToInt(currentWave * 1.2f);
                break;
        }

        // Отладка
        Debug.Log($"{type} на волне {currentWave}: HP={maxHealth}, Награда={bounty}");
    }

    float CalculateHealthMultiplier(int wave)
    {
        if (wave <= 1) return 1.0f;
        if (wave <= 3) return 1f + ((wave - 1) * 0.3f);  
        if (wave <= 6) return 1.6f + ((wave - 3) * 0.4f); 
        return 2.8f + ((wave - 6) * 1.0f); 
    }

    void FindRandomizedPath()
    {
        if (startNode == null || GameManager.Instance == null || GameManager.Instance.criticalNode == null)
        {
            Debug.LogError("Virus: Не задан стартовый или целевой узел!");
            return;
        }

        path = FindRandomizedShortestPath(startNode, GameManager.Instance.criticalNode);

        if (path.Count > 0)
        {
            currentNode = path[0];
            transform.position = currentNode.transform.position;
            currentPathIndex = 1;
            Debug.Log($"Вирус {type} создан с путем из {path.Count} узлов");
        }
        else
        {
            Debug.LogWarning("Не удалось найти путь для вируса!");
        }
    }

    List<NetworkNode> FindRandomizedShortestPath(NetworkNode start, NetworkNode end)
    {
        List<NetworkNode> path = new List<NetworkNode>();
        NetworkNode current = start;
        HashSet<NetworkNode> visited = new HashSet<NetworkNode>();
        int maxSteps = 50; 

        path.Add(current);
        visited.Add(current);

        int step = 0;
        while (current != end && current != null && step < maxSteps)
        {
            NetworkNode next = GetNextNodeWithRandomness(current, end, visited);
            if (next == null) break;

            path.Add(next);
            visited.Add(next);
            current = next;
            step++;
        }

        if (current != end)
        {
            return FindDirectPath(start, end);
        }

        return path;
    }

    List<NetworkNode> FindDirectPath(NetworkNode start, NetworkNode end)
    {
        List<NetworkNode> path = new List<NetworkNode>();
        NetworkNode current = start;
        HashSet<NetworkNode> visited = new HashSet<NetworkNode>();

        path.Add(current);
        visited.Add(current);

        while (current != end && current != null)
        {
            NetworkNode next = GetNextNodeTowardsTarget(current, end, visited);
            if (next == null) break;

            path.Add(next);
            visited.Add(next);
            current = next;
        }

        return path;
    }

    NetworkNode GetNextNodeWithRandomness(NetworkNode current, NetworkNode target, HashSet<NetworkNode> visited)
    {
        List<NetworkNode> availableNeighbors = new List<NetworkNode>();
        foreach (var neighbor in current.connectedNodes)
        {
            if (neighbor == null || visited.Contains(neighbor)) continue;
            availableNeighbors.Add(neighbor);
        }

        if (availableNeighbors.Count == 0) return null;

        bool useRandom = UnityEngine.Random.value < randomPathChance;

        if (useRandom && useSmartRandomization)
        {
            return GetSmartRandomNeighbor(current, target, availableNeighbors);
        }
        else if (useRandom)
        {
            return availableNeighbors[UnityEngine.Random.Range(0, availableNeighbors.Count)];
        }
        else
        {
            return GetBestNodeTowardsTarget(current, target, availableNeighbors);
        }
    }

    NetworkNode GetSmartRandomNeighbor(NetworkNode current, NetworkNode target, List<NetworkNode> neighbors)
    {
        if (neighbors.Count == 0) return null;

        List<NodeScore> scoredNodes = new List<NodeScore>();

        foreach (var neighbor in neighbors)
        {
            float distanceToTarget = Vector3.Distance(neighbor.transform.position, target.transform.position);
            float distanceFromCurrent = Vector3.Distance(neighbor.transform.position, current.transform.position);

            float score = 1f / (distanceToTarget + 0.1f); 

            score *= UnityEngine.Random.Range(0.8f, 1.2f);

            scoredNodes.Add(new NodeScore(neighbor, score));
        }

        scoredNodes.Sort((a, b) => b.score.CompareTo(a.score));

        int topChoices = Mathf.Min(3, scoredNodes.Count);
        return scoredNodes[UnityEngine.Random.Range(0, topChoices)].node;
    }

    NetworkNode GetBestNodeTowardsTarget(NetworkNode current, NetworkNode target, List<NetworkNode> neighbors)
    {
        NetworkNode bestNode = null;
        float bestDistance = float.MaxValue;

        foreach (var neighbor in neighbors)
        {
            float distance = Vector3.Distance(neighbor.transform.position, target.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestNode = neighbor;
            }
        }

        return bestNode;
    }

    NetworkNode GetNextNodeTowardsTarget(NetworkNode current, NetworkNode target, HashSet<NetworkNode> visited)
    {
        return GetBestNodeTowardsTarget(current, target, new List<NetworkNode>(current.connectedNodes));
    }

    IEnumerator FollowPath()
    {
        while (currentPathIndex < path.Count && !isDead && !reachedEnd)
        {
            NetworkNode nextNode = path[currentPathIndex];
            Vector3 startPos = transform.position;
            Vector3 endPos = nextNode.transform.position;

            float distance = Vector3.Distance(startPos, endPos);
            float duration = distance / (speed * slowMultiplier);
            float elapsed = 0f;

            while (elapsed < duration && !isDead)
            {
                if (PauseMenu.Instance != null && PauseMenu.Instance.IsGamePaused())
                {
                    yield return null;
                    continue;
                }

                if (GameManager.Instance == null || !GameManager.Instance.isGameActive) yield break;

                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                transform.position = Vector3.Lerp(startPos, endPos, smoothT);

                if (slowTimer > 0)
                {
                    slowTimer -= Time.deltaTime;
                    if (slowTimer <= 0)
                    {
                        slowMultiplier = 1f;
                        if (spriteRenderer != null)
                        {
                            ReturnToOriginalColor();
                        }
                    }
                }

                yield return null;
            }

            if (!isDead)
            {
                currentNode = nextNode;
                currentPathIndex++;

                if (currentNode == GameManager.Instance.criticalNode)
                {
                    ReachedCritical();
                    yield break;
                }
            }
        }

        if (!reachedEnd && !isDead)
        {
            Debug.LogWarning("Virus не смог найти путь к цели!");
            Destroy(gameObject);
        }
    }

    void ReturnToOriginalColor()
    {
        if (spriteRenderer == null) return;

        switch (type)
        {
            case VirusType.Normal:
                spriteRenderer.color = Color.white;
                break;
            case VirusType.Fast:
                spriteRenderer.color = Color.yellow;
                break;
            case VirusType.Tank:
                spriteRenderer.color = Color.red;
                break;
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        health -= amount;
        health = Mathf.Clamp(health, 0, maxHealth);

        StartCoroutine(DamageFlash());

        if (health <= 0)
        {

            Die();
            
        }
        else
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayVirusDamaged();

        }
    }

    IEnumerator DamageFlash()
    {
        if (spriteRenderer == null) yield break;

        Color original = spriteRenderer.color;
        spriteRenderer.color = Color.white;

        yield return new WaitForSeconds(0.1f);

        if (spriteRenderer != null)
            spriteRenderer.color = original;
    }

    public void ApplySlow(float amount, float duration)
    {
        if (type == VirusType.Tank) return;

        slowMultiplier = Mathf.Max(0.3f, 1f - amount);
        slowTimer = duration;

        if (spriteRenderer != null)
        {
            Color original = spriteRenderer.color;
            Color slowColor = Color.Lerp(original, Color.blue, 0.3f);
            spriteRenderer.color = slowColor;

            StartCoroutine(ReturnColorAfterDelay(original, duration));
        }
    }

    IEnumerator ReturnColorAfterDelay(Color originalColor, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
    }

    void ReachedCritical()
    {
        reachedEnd = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.VirusReachedCritical();
        }
        //
        OnVirusReachedCritical?.Invoke(damageToBase);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayVirusReachBase();
        if (BattleLogManager.Instance != null)
            BattleLogManager.Instance.AddWarningMessage(
        $"{name} атакует критический узел!"
            );

        Destroy(gameObject, 0.1f);
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayVirusDeath();
        

        if (GameManager.Instance != null)
        {
            GameManager.Instance.VirusDestroyed(bounty);
        }

        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false;

        StartCoroutine(DeathAnimation());
        //
        OnVirusDestroyed?.Invoke(bounty);
        Destroy(gameObject, 1f);

        
    }

    IEnumerator DeathAnimation()
    {
        float duration = 0.5f;
        float elapsed = 0f;
        Vector3 originalScale = transform.localScale;
        Color originalColor = spriteRenderer.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.Rotate(0, 0, 180f * Time.deltaTime);

            transform.localScale = originalScale * (1 - t * 0.5f);

            spriteRenderer.color = new Color(
                originalColor.r,
                originalColor.g,
                originalColor.b,
                1 - t
            );

            yield return null;
        }
    }

    // Вспомогательный класс для оценки узлов
    private class NodeScore
    {
        public NetworkNode node;
        public float score;

        public NodeScore(NetworkNode node, float score)
        {
            this.node = node;
            this.score = score;
        }
    }
}