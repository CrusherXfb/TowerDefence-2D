using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BattleLogManager : MonoBehaviour
{
    public static BattleLogManager Instance;

    [Header("UI References")]
    [SerializeField] private Transform logContent; 
    [SerializeField] private GameObject messagePrefab; 
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private int maxMessages = 20; 

    [Header("÷‚ÂÚ‡ ÒÓÓ·˘ÂÌËÈ")]
    public Color damageColor = Color.white;
    public Color effectColor = new Color(0.3f, 0.6f, 1f);
    public Color systemColor = Color.green;
    public Color warningColor = new Color(1f, 0.6f, 0f); 
    public Color moneyColor = Color.yellow;

    private Queue<GameObject> messageQueue = new Queue<GameObject>();
    private bool isPaused = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }


    public void AddMessage(string text, Color color, string prefix = "")
    {
        if (isPaused) return;

        GameObject messageObj;
        if (messageQueue.Count >= maxMessages)
        {
            messageObj = messageQueue.Dequeue();
            messageObj.transform.SetAsLastSibling();
        }
        else
        {
            messageObj = Instantiate(messagePrefab, logContent);
        }

        Text textComp = messageObj.GetComponentInChildren<Text>();
        if (!string.IsNullOrEmpty(prefix))
            text = $"[{prefix}] {text}";

        textComp.text = text;
        textComp.color = color;
        textComp.gameObject.SetActive(true);

        messageQueue.Enqueue(messageObj);
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;

        Debug.Log($"BattleLog: {text}");
    }

    public void AddDamageMessage(string text) => AddMessage(text, damageColor, "”–ŒÕ");
    public void AddEffectMessage(string text) => AddMessage(text, effectColor, "›‘‘≈ “");
    public void AddSystemMessage(string text) => AddMessage(text, systemColor, "—»—“≈Ã¿");
    public void AddWarningMessage(string text) => AddMessage(text, warningColor, "!");
    public void AddMoneyMessage(string text) => AddMessage(text, moneyColor, "+$");

    public void ClearLog()
    {
        foreach (var msg in messageQueue)
            Destroy(msg);
        messageQueue.Clear();
    }

    public void SetPaused(bool paused) => isPaused = paused;
}