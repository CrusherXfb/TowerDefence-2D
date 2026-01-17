using System.Collections.Generic;
using UnityEngine;

public class NetworkNode : MonoBehaviour
{
    [Header("Свойства узла")]
    public bool isCritical = false;
    public bool isSpawnPoint = false;
    public bool hasTower = false;
    public Tower currentTower;
    public bool canBuildTower = false;

    [Header("Связи")]
    public List<NetworkNode> connectedNodes = new List<NetworkNode>();

    [Header("Визуализация")]
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.HSVToRGB(0, 0, 39);
    public Color spawnColor = Color.HSVToRGB(349, 74, 75);
    public Color criticalColor = Color.HSVToRGB(63, 70, 78);
    public Color selectedColor = Color.HSVToRGB(8, 85, 100);
    public Color buildAllowedColor = Color.HSVToRGB(120, 70, 60);
    public Color buildBlockedColor = Color.HSVToRGB(0, 70, 60);

    [Header("Настройки строительства")]
    public int maxBuildDistance = 2;

    public static NetworkNode CurrentlySelectedNode = null;

    void Start()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        UpdateVisual();
        DrawConnections();

        if (ObjectRegistry.Instance != null)
        {
            ObjectRegistry.Instance.RegisterNode(this);
        }

        UpdateBuildStatus();
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isPreparationPhase)
        {
            UpdateBuildStatus();
        }
    }

    void OnDestroy()
    {
        if (ObjectRegistry.Instance != null)
        {
            ObjectRegistry.Instance.UnregisterNode(this);
        }

        if (CurrentlySelectedNode == this)
        {
            CurrentlySelectedNode = null;
        }
    }

    public void UpdateVisual()
    {
        if (spriteRenderer == null) return;

        if (isCritical)
            spriteRenderer.color = criticalColor;
        else if (isSpawnPoint)
            spriteRenderer.color = spawnColor;
        else if (canBuildTower && !hasTower)
            spriteRenderer.color = buildAllowedColor;
        else
            spriteRenderer.color = normalColor;
    }

    void UpdateBuildStatus()
    {
        if (isCritical || isSpawnPoint || hasTower)
        {
            canBuildTower = false;
            return;
        }

        canBuildTower = IsWithinBuildDistance();
        UpdateVisual();
    }

    bool IsWithinBuildDistance()
    {
        List<NetworkNode> sourceNodes = new List<NetworkNode>();

        if (GameManager.Instance != null && GameManager.Instance.criticalNode != null)
        {
            sourceNodes.Add(GameManager.Instance.criticalNode);
        }

        if (ObjectRegistry.Instance != null)
        {
            List<Tower> towers = ObjectRegistry.Instance.GetAllTowers();
            foreach (Tower tower in towers)
            {
                if (tower.node != null && !sourceNodes.Contains(tower.node))
                {
                    sourceNodes.Add(tower.node);
                }
            }
        }

        if (sourceNodes.Count == 0)
        {
            return IsNearCriticalNode();
        }

        foreach (NetworkNode source in sourceNodes)
        {
            if (source == null) continue;

            int distance = CalculateNodeDistance(this, source, new HashSet<NetworkNode>());
            if (distance <= maxBuildDistance && distance >= 0)
            {
                return true;
            }
        }

        return false;
    }

    bool IsNearCriticalNode()
    {
        if (GameManager.Instance == null || GameManager.Instance.criticalNode == null)
            return false;

        int distance = CalculateNodeDistance(this, GameManager.Instance.criticalNode, new HashSet<NetworkNode>());
        return distance <= maxBuildDistance && distance >= 0;
    }

    int CalculateNodeDistance(NetworkNode start, NetworkNode end, HashSet<NetworkNode> visited)
    {
        if (start == end) return 0;

        Queue<NetworkNode> queue = new Queue<NetworkNode>();
        Dictionary<NetworkNode, int> distances = new Dictionary<NetworkNode, int>();

        queue.Enqueue(start);
        distances[start] = 0;
        visited.Add(start);

        while (queue.Count > 0)
        {
            NetworkNode current = queue.Dequeue();
            int currentDistance = distances[current];

            if (current == end)
            {
                return currentDistance;
            }

            foreach (NetworkNode neighbor in current.connectedNodes)
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    distances[neighbor] = currentDistance + 1;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return -1; // Не найдено
    }

    void DrawConnections()
    {
        foreach (Transform child in transform)
        {
            if (child.name == "ConnectionLine")
                Destroy(child.gameObject);
        }

        foreach (NetworkNode connectedNode in connectedNodes)
        {
            if (connectedNode == null) continue;

            GameObject lineObj = new GameObject("ConnectionLine");
            lineObj.transform.parent = transform;

            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, transform.position);
            line.SetPosition(1, connectedNode.transform.position);

            line.startWidth = 0.1f;
            line.endWidth = 0.1f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = Color.gray;
            line.endColor = Color.gray;
        }
    }

    void OnMouseDown()
    {
        // Используем Raycast только на слой Nodes, игнорируя вирусы
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, 0f, LayerMask.GetMask("Nodes"));

        // Проверяем, что кликнули именно на этот узел
        if (hit.collider == null || hit.collider.gameObject != gameObject) return;

        if (!GameManager.Instance.isGameActive) return;
        if (isCritical || isSpawnPoint) return;

        if (CurrentlySelectedNode == this && BuildMenu.Instance != null)
        {
            BuildMenu.Instance.CloseMenu();
            return;
        }

        if (!hasTower && !canBuildTower)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayNotEnoughMoney();

            Debug.Log($"Нельзя строить на этом узле! Он дальше чем {maxBuildDistance} узлов от существующих башен или критического узла.");
            return;
        }

        SetSelected(true);

        if (BuildMenu.Instance != null)
        {
            BuildMenu.Instance.ShowForNode(this);
        }
    }

    void OnMouseEnter()
    {
        if (!isCritical && !isSpawnPoint && CurrentlySelectedNode != this)
        {
            if (!canBuildTower && !hasTower)
                spriteRenderer.color = buildBlockedColor;
            else
                spriteRenderer.color = Color.Lerp(spriteRenderer.color, selectedColor, 0.3f);

            if (currentTower != null && !currentTower.isSelected)
            {
                currentTower.isHovered = true;
                currentTower.UpdateRangeIndicatorVisibility();
            }
        }
    }

    void OnMouseExit()
    {
        if (CurrentlySelectedNode != this)
            UpdateVisual();

        if (currentTower != null && !currentTower.isSelected)
        {
            currentTower.isHovered = false;
            currentTower.UpdateRangeIndicatorVisibility();
        }
    }

    public void SetSelected(bool selected)
    {
        if (selected && CurrentlySelectedNode != null && CurrentlySelectedNode != this)
        {
            CurrentlySelectedNode.SetSelected(false);
        }

        Transform highlight = transform.Find("SetHighlight");
        if (highlight != null)
        {
            highlight.gameObject.SetActive(selected);
        }

        if (selected)
        {
            CurrentlySelectedNode = this;

            if (currentTower != null)
            {
                currentTower.isSelected = selected;
                currentTower.UpdateRangeIndicatorVisibility();
            }
        }
        else
        {
            UpdateVisual();
            if (CurrentlySelectedNode == this)
            {
                CurrentlySelectedNode = null;
            }

            if (currentTower != null)
            {
                currentTower.isSelected = false;
                currentTower.UpdateRangeIndicatorVisibility();
            }
        }
    }

    public bool BuildTower(GameObject towerPrefab)
    {
        if (hasTower || !canBuildTower) return false;

        GameObject towerObj = Instantiate(towerPrefab, transform.position, Quaternion.identity);
        currentTower = towerObj.GetComponent<Tower>();

        if (currentTower != null)
        {
            currentTower.node = this;
            hasTower = true;
            canBuildTower = false;

            UpdateNeighborBuildStatus();

            return true;
        }
        else
        {
            Destroy(towerObj);
            return false;
        }
    }

    void UpdateNeighborBuildStatus()
    {
        HashSet<NetworkNode> allNodes = new HashSet<NetworkNode>();
        GetAllNodesInRange(this, maxBuildDistance, allNodes);

        foreach (NetworkNode node in allNodes)
        {
            if (node != this)
            {
                node.UpdateBuildStatus();
            }
        }
    }

    void GetAllNodesInRange(NetworkNode start, int range, HashSet<NetworkNode> result)
    {
        if (range < 0) return;

        Queue<NetworkNode> queue = new Queue<NetworkNode>();
        Dictionary<NetworkNode, int> distances = new Dictionary<NetworkNode, int>();

        queue.Enqueue(start);
        distances[start] = 0;

        while (queue.Count > 0)
        {
            NetworkNode current = queue.Dequeue();
            int currentDistance = distances[current];

            result.Add(current);

            if (currentDistance < range)
            {
                foreach (NetworkNode neighbor in current.connectedNodes)
                {
                    if (!distances.ContainsKey(neighbor))
                    {
                        distances[neighbor] = currentDistance + 1;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }
    }

    public void SellTower()
    {
        if (currentTower != null)
        {
            int refund = currentTower.GetSellPrice();
            if (GameManager.Instance != null)
                GameManager.Instance.AddMoney(refund);

            if (BattleLogManager.Instance != null)
            {
                BattleLogManager.Instance.AddSystemMessage($"{currentTower.name} продана");
                BattleLogManager.Instance.AddMoneyMessage($"+{refund}$");
            }
            Destroy(currentTower.gameObject);
            currentTower = null;
            hasTower = false;

            UpdateBuildStatus();
            UpdateNeighborBuildStatus();
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        foreach (var node in connectedNodes)
        {
            if (node != null)
                Gizmos.DrawLine(transform.position, node.transform.position);
        }

        if (!isCritical && !isSpawnPoint && !hasTower)
        {
            if (canBuildTower)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, 0.3f);
            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, 0.3f);
            }
        }
    }
}