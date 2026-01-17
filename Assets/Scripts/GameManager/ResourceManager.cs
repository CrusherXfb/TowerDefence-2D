using System;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance; 

    public int startMoney = 180;
    public int startHealth = 30;

    private int _money;
    private int _health;

    public event Action<int> OnMoneyChanged;
    public event Action<int> OnHealthChanged;

    // Свойства для доступа
    public int Money => _money;
    public int Health => _health;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        _money = startMoney;
        _health = startHealth;

        // Вызываем события при старте, чтобы UI обновился
        OnMoneyChanged?.Invoke(_money);
        OnHealthChanged?.Invoke(_health);
    }

    public void AddMoney(int amount)
    {
        _money += amount;
        OnMoneyChanged?.Invoke(_money);
    }

    public bool SpendMoney(int amount)
    {
        if (_money >= amount)
        {
            _money -= amount;
            OnMoneyChanged?.Invoke(_money);
            return true;
        }
        return false;
    }

    public void ChangeHealth(int amount)
    {
        _health += amount;
        OnHealthChanged?.Invoke(_health);
    }

    public void ResetResources()
    {
        _money = startMoney;
        _health = startHealth;
        OnMoneyChanged?.Invoke(_money);
        OnHealthChanged?.Invoke(_health);
    }
}