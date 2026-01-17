using System.Collections.Generic;
using UnityEngine;

public class NetworkGeneratorSimple : MonoBehaviour
{
    [Header("Префабы")]
    public GameObject nodePrefab;
    public GameObject criticalNodePrefab;
    public GameObject spawnNodePrefab;

    [Header("Настройки")]
    [Range(13, 21)] public int totalNodes = 15;
    [Range(1, 3)] public int spawnNodeCount = 2;
    public float spacing = 3f;

    [Header("Размещение")]
    public int columns = 5;
    [HideInInspector] public int rows = 3;

    [Header("Вариативность (новое)")]
    public bool randomizeCriticalRow = true; 
    public bool randomSpawnPositions = true; 
    public bool shuffleRegularNodes = true; 

    // Кэшированный материал для линий
    private Material lineMaterial;
    private List<NetworkNode> generatedNodes = new List<NetworkNode>();

    void Start()
    {
        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        GenerateSimpleNetwork();
    }

    public void GenerateSimpleNetwork()
    {
        Debug.Log("Начинаем генерацию сети...");
        ClearOldNodes();
        generatedNodes.Clear();

        List<Vector2> positions = new List<Vector2>();
        rows = 3;

        float startX = -((columns - 1) * spacing) / 2f;
        float startY = -((rows - 1) * spacing) / 2f;

        // === КРИТИЧЕСКИЙ УЗЕЛ ===
        int criticalRow = 1; 

        if (randomizeCriticalRow && rows > 1)
        {
            // Случайная строка для критического узла (0, 1, 2)
            criticalRow = Random.Range(0, rows);
        }

        Vector2 criticalPos = new Vector2(startX, startY + criticalRow * spacing);
        GameObject criticalObj = Instantiate(criticalNodePrefab, criticalPos, Quaternion.identity, transform);
        NetworkNode criticalNode = criticalObj.GetComponent<NetworkNode>();
        criticalNode.isCritical = true;
        generatedNodes.Add(criticalNode);

        Debug.Log($"Критический узел создан в строке {criticalRow} (0-{rows - 1})");

        // === СПАВН-УЗЛЫ ===
        int actualSpawnCount = spawnNodeCount;

        // Если спавн-узлов больше чем строк - ограничиваем
        if (actualSpawnCount > rows)
        {
            actualSpawnCount = rows;
            Debug.LogWarning($"Спавн-узлов больше чем строк! Ограничено до {rows}");
        }

        // Создаём список доступных строк для спавнов
        List<int> availableSpawnRows = new List<int>();
        for (int i = 0; i < rows; i++)
        {
            availableSpawnRows.Add(i);
        }

        // Если не случайные позиции, используем фиксированные
        if (!randomSpawnPositions)
        {
            // Фиксированные позиции как раньше
            for (int i = 0; i < actualSpawnCount; i++)
            {
                int row = i;
                CreateSpawnNode(startX, startY, row);
            }
        }
        else
        {
            // СЛУЧАЙНЫЕ ПОЗИЦИИ ДЛЯ СПАВНОВ
            ShuffleList(availableSpawnRows);

            for (int i = 0; i < actualSpawnCount; i++)
            {
                int row = availableSpawnRows[i];
                CreateSpawnNode(startX, startY, row);
            }
        }

        // === ОБЫЧНЫЕ УЗЛЫ ===
        int regularNodesNeeded = totalNodes - generatedNodes.Count;
        List<Vector2> availablePositions = new List<Vector2>();

        // Собираем все доступные позиции для обычных узлов
        // Пропускаем крайние колонки (там крит/спавны) и строку с критическим узлом
        for (int col = 1; col < columns - 1; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                // Пропускаем позицию, если там уже есть спавн или крит
                Vector2 pos = new Vector2(startX + col * spacing, startY + row * spacing);
                bool positionOccupied = false;

                // Проверяем, не занята ли позиция (примерно)
                foreach (var node in generatedNodes)
                {
                    if (Vector2.Distance(pos, node.transform.position) < spacing * 0.5f)
                    {
                        positionOccupied = true;
                        break;
                    }
                }

                if (!positionOccupied)
                {
                    availablePositions.Add(pos);
                }
            }
        }

        // Перемешиваем позиции для большей вариативности
        if (shuffleRegularNodes)
        {
            ShuffleList(availablePositions);
        }

        int nodesToCreate = Mathf.Min(regularNodesNeeded, availablePositions.Count);

        for (int i = 0; i < nodesToCreate; i++)
        {
            GameObject nodeObj = Instantiate(nodePrefab, availablePositions[i], Quaternion.identity, transform);
            NetworkNode node = nodeObj.GetComponent<NetworkNode>();
            generatedNodes.Add(node);
        }

        UpdateNodeColors(generatedNodes);

        Debug.Log($"Создано узлов: {generatedNodes.Count} (1 крит + {actualSpawnCount} спавн + {nodesToCreate} обычных)");

        // === СОЕДИНЕНИЯ ===
        CreateSimpleConnections(generatedNodes);

        Debug.Log("Генерация завершена!");
    }

    // Вспомогательный метод для создания спавн-узла
    void CreateSpawnNode(float startX, float startY, int row)
    {
        Vector2 spawnPos = new Vector2(startX + (columns - 1) * spacing, startY + row * spacing);
        GameObject spawnObj = Instantiate(spawnNodePrefab, spawnPos, Quaternion.identity, transform);
        NetworkNode spawnNode = spawnObj.GetComponent<NetworkNode>();
        spawnNode.isSpawnPoint = true;
        generatedNodes.Add(spawnNode);

        Debug.Log($"Спавн-узел создан в строке {row}");
    }

    // ===== ОСТАЛЬНЫЕ МЕТОДЫ БЕЗ ИЗМЕНЕНИЙ =====

    void UpdateNodeColors(List<NetworkNode> nodes)
    {
        foreach (NetworkNode node in nodes)
        {
            if (node.spriteRenderer == null)
                node.spriteRenderer = node.GetComponent<SpriteRenderer>();

            if (node.isCritical)
                node.spriteRenderer.color = node.criticalColor;
            else if (node.isSpawnPoint)
                node.spriteRenderer.color = node.spawnColor;
            else
                node.spriteRenderer.color = node.normalColor;
        }
    }

    void CreateSimpleConnections(List<NetworkNode> nodes)
    {
        // ... существующий код без изменений ...
        if (nodes == null || nodes.Count == 0) return;

        foreach (var node in nodes)
        {
            node.connectedNodes.Clear();
        }

        List<Edge> allEdges = new List<Edge>();
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                NetworkNode a = nodes[i];
                NetworkNode b = nodes[j];
                float dist = Vector2.Distance(a.transform.position, b.transform.position);

                if (dist <= spacing * 1.5f)
                {
                    if (!WouldCreateIntersection(a, b, nodes))
                    {
                        allEdges.Add(new Edge(a, b, dist));
                    }
                }
            }
        }

        allEdges.Sort((x, y) => x.distance.CompareTo(y.distance));

        var uf = new UnionFind(nodes);
        int mstEdgeCount = 0;

        foreach (var edge in allEdges)
        {
            if (uf.Find(edge.a) != uf.Find(edge.b))
            {
                uf.Union(edge.a, edge.b);
                edge.a.connectedNodes.Add(edge.b);
                edge.b.connectedNodes.Add(edge.a);
                mstEdgeCount++;

                if (mstEdgeCount == nodes.Count - 1) break;
            }
        }

        ShuffleList(allEdges);
        int extraCount = 0;

        foreach (var edge in allEdges)
        {
            if (edge.a.connectedNodes.Contains(edge.b)) continue;

            if (edge.a.connectedNodes.Count >= 4 || edge.b.connectedNodes.Count >= 4) continue;

            edge.a.connectedNodes.Add(edge.b);
            edge.b.connectedNodes.Add(edge.a);
            extraCount++;

            if (extraCount > nodes.Count) break;
        }

        Debug.Log($"Соединения: MST = {mstEdgeCount}, доп. = {extraCount}");

        UpdateConnectionsVisual(nodes);
    }

    bool WouldCreateIntersection(NetworkNode a, NetworkNode b, List<NetworkNode> allNodes)
    {
        Vector2 p1 = a.transform.position;
        Vector2 p2 = b.transform.position;

        foreach (var node in allNodes)
        {
            foreach (var conn in node.connectedNodes)
            {
                if ((node == a && conn == b) || (node == b && conn == a)) continue;

                Vector2 p3 = node.transform.position;
                Vector2 p4 = conn.transform.position;

                if (LinesIntersect(p1, p2, p3, p4))
                    return true;
            }
        }
        return false;
    }

    bool LinesIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        float denom = (p4.y - p3.y) * (p2.x - p1.x) - (p4.x - p3.x) * (p2.y - p1.y);
        if (Mathf.Abs(denom) < 1e-6f) return false;

        float ua = ((p4.x - p3.x) * (p1.y - p3.y) - (p4.y - p3.y) * (p1.x - p3.x)) / denom;
        float ub = ((p2.x - p1.x) * (p1.y - p3.y) - (p2.y - p1.y) * (p1.x - p3.x)) / denom;

        return ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1;
    }

    void UpdateConnectionsVisual(List<NetworkNode> nodes)
    {
        foreach (NetworkNode node in nodes)
        {
            var children = new List<Transform>();
            foreach (Transform child in node.transform)
                if (child.name == "ConnectionLine")
                    children.Add(child);

            foreach (var child in children)
                DestroyImmediate(child.gameObject);

            foreach (NetworkNode connected in node.connectedNodes)
            {
                if (node.GetInstanceID() > connected.GetInstanceID()) continue;

                GameObject lineObj = new GameObject("ConnectionLine");
                lineObj.transform.SetParent(node.transform, false);

                LineRenderer line = lineObj.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.SetPosition(0, node.transform.position);
                line.SetPosition(1, connected.transform.position);

                line.startWidth = line.endWidth = 0.15f;
                line.material = lineMaterial;
                line.startColor = line.endColor = Color.gray;
            }
        }
    }

    void ClearOldNodes()
    {
        List<NetworkNode> oldNodes = ObjectRegistry.Instance.GetAllNodes();
        foreach (NetworkNode node in oldNodes)
        {
            if (node != null)
                Destroy(node.gameObject);
        }
        Debug.Log($"Удалено {oldNodes.Count} старых узлов");
    }

    void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    [ContextMenu("Сгенерировать сеть")]
    public void GenerateNetworkEditor()
    {
        if (lineMaterial == null)
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
        GenerateSimpleNetwork();
    }

    // === Вспомогательные классы ===

    [System.Serializable]
    private class Edge
    {
        public NetworkNode a, b;
        public float distance;
        public Edge(NetworkNode a, NetworkNode b, float distance)
        {
            this.a = a;
            this.b = b;
            this.distance = distance;
        }
    }

    private class UnionFind
    {
        private Dictionary<NetworkNode, NetworkNode> parent = new Dictionary<NetworkNode, NetworkNode>();
        private Dictionary<NetworkNode, int> rank = new Dictionary<NetworkNode, int>();

        public UnionFind(List<NetworkNode> nodes)
        {
            foreach (var node in nodes)
            {
                parent[node] = node;
                rank[node] = 0;
            }
        }

        public NetworkNode Find(NetworkNode x)
        {
            if (parent[x] != x)
                parent[x] = Find(parent[x]);
            return parent[x];
        }

        public void Union(NetworkNode x, NetworkNode y)
        {
            NetworkNode rx = Find(x), ry = Find(y);
            if (rx == ry) return;

            if (rank[rx] < rank[ry])
                parent[rx] = ry;
            else if (rank[rx] > rank[ry])
                parent[ry] = rx;
            else
            {
                parent[ry] = rx;
                rank[rx]++;
            }
        }
    }
}