using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ObjectRegistry : MonoBehaviour
{
    public static ObjectRegistry Instance;

    private List<NetworkNode> networkNodes = new List<NetworkNode>();
    private List<Virus> viruses = new List<Virus>();
    private List<Tower> towers = new List<Tower>();
    private int virusCounter = 0;
    private int towersCounter = 0;
    private int nodesCounter = 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RegisterNode(NetworkNode node)
    {
        if (node == null) return;
        if (!networkNodes.Contains(node))
        {
            nodesCounter++;
            node.gameObject.name = $"Node_#{nodesCounter:000}";
            networkNodes.Add(node);
        }
    }

    public void UnregisterNode(NetworkNode node)
    {
        networkNodes.Remove(node);
    }

    public void RegisterVirus(Virus virus)
    {
        if (virus == null) return;
        if (!viruses.Contains(virus))
        {
            virusCounter++;
            virus.gameObject.name = $"Virus_{virus.type}_#{virusCounter:000}";           
            viruses.Add(virus);
        }
    }

    public void UnregisterVirus(Virus virus)
    {
        viruses.Remove(virus);
    }

    public void RegisterTower(Tower tower)
    {
        if (tower == null) return;
        if (!towers.Contains(tower))
        {
            towersCounter++;
            tower.gameObject.name = $"Tower_{tower.type}_#{towersCounter:000}";
            towers.Add(tower);
        }
    }

    public void UnregisterTower(Tower tower)
    {
        towers.Remove(tower);
    }

    public List<NetworkNode> GetAllNodes()
    {
        return new List<NetworkNode>(networkNodes);
    }

    public List<Virus> GetAllViruses()
    {
        return new List<Virus>(viruses);
    }

    public List<Tower> GetAllTowers()
    {
        return new List<Tower>(towers);
    }

    public void ResetCounters()
    {
        virusCounter = 0;
        towersCounter = 0;
        nodesCounter = 0;
        Debug.Log("ObjectRegistry: Счетчики сброшены");
    }

    public void ClearAll()
    {
        ResetCounters();
        foreach (var virus in viruses.ToArray())
        {
            if (virus != null)
                Destroy(virus.gameObject);
        }

        foreach (var tower in towers.ToArray())
        {
            if (tower != null)
                Destroy(tower.gameObject);
        }

        foreach (var node in networkNodes.ToArray())
        {
            if (node != null && node.gameObject != null)
                Destroy(node.gameObject);
        }

        networkNodes.Clear();
        viruses.Clear();
        towers.Clear();

        Debug.Log("ObjectRegistry: Все объекты очищены");
    }
}