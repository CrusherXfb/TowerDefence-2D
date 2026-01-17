using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public enum TowerType { Slow, Damage, AOE, Support }

public class Tower : MonoBehaviour
{
    [Header("Основные параметры")]
    public TowerType type;

    private int buildCost = 50;
    private int upgradeCost = 30;
    private float range = 2f;
    private float attackSpeed = 1f;
    private float damage = 10f;
    private float slowAmount = 0.5f;
    private float aoeRadius = 1f;
    private float supportBoost = 0.2f;
    private float supportDuration = 5f;

    // Свойства для доступа из других классов
    public int BuildCost => buildCost;
    public int UpgradeCost => upgradeCost;
    public float Range => range;
    public float AttackSpeed => attackSpeed;
    public float Damage => damage;
    public float SlowAmount => slowAmount;
    public float AoeRadius => aoeRadius;
    public float SupportBoost => supportBoost;
    public float SupportDuration => supportDuration;

    // Публичные поля
    [Header("Визуальные эффекты")]
    public GameObject projectilePrefab;
    public GameObject aoeEffectPrefab;
    public GameObject supportEffectPrefab;
    public GameObject hitEffectPrefab;

    [Header("Состояние")]
    public NetworkNode node;
    private float attackTimer = 0f;
    private List<Virus> virusesInRange = new List<Virus>();
    private List<Tower> supportedTowers = new List<Tower>();

    [Header("Визуализация")]
    public SpriteRenderer spriteRenderer;
    public GameObject rangeIndicator;
    public bool isHovered = false;
    public bool isSelected = false;

    [Header("Баффы (для Support башни)")]
    public float damageBuff = 0f;
    public float attackSpeedBuff = 0f;

    public string desc;

    // Публичные свойства для level и maxLevel
    [Header("Уровень")]
    [SerializeField] private int level = 1;
    [SerializeField] private int maxLevel = 3;

    public int Level => level;
    public int MaxLevel => maxLevel;

    void Start()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        InitializeTowerStats();

        SetupRangeIndicator();
        UpdateVisual();

        if (ObjectRegistry.Instance != null)
        {
            ObjectRegistry.Instance.RegisterTower(this);
        }
    }

    void InitializeTowerStats()
    {
        switch (type)
        {
            case TowerType.Damage:
                buildCost = 70;
                upgradeCost = 100;
                range = 3.5f;
                attackSpeed = 0.8f;
                damage = 15f;
                desc = "Наносит урон по одной цели в зоне поражения, повышенный урон по танкам";
                break;

            case TowerType.Slow:
                buildCost = 60;
                upgradeCost = 40;
                range = 3.0f;
                attackSpeed = 0.7f;
                damage = 5f;
                slowAmount = 0.45f;
                desc = "Наносит небольшой урон по целям в зоне поражения и замедляет их";
                break;

            case TowerType.AOE:
                buildCost = 95;
                upgradeCost = 70;
                range = 4.8f;
                attackSpeed = 0.3f;
                damage = 15f;
                aoeRadius = 1.8f;
                desc = "Наносит урон по группе целей в зоне поражения, пониженный урон по танкам";
                break;

            case TowerType.Support:
                buildCost = 85;
                upgradeCost = 100;
                range = 4.5f;
                supportBoost = 0.12f;
                desc = "Увеличивает характеристики башен в области действия";
                break;
        }
    }

    void SetupRangeIndicator()
    {
        if (rangeIndicator != null)
        {
            UpdateRangeIndicatorSize();

            SpriteRenderer rangeRenderer = rangeIndicator.GetComponent<SpriteRenderer>();
            if (rangeRenderer != null)
            {
                Color rangeColor = rangeRenderer.color;
                rangeColor.a = 0.3f;
                rangeRenderer.color = rangeColor;
            }

            rangeIndicator.SetActive(false);
        }
        else
        {
            Debug.LogWarning("RangeIndicator не назначен для башни!");
        }
    }

    void UpdateRangeIndicatorSize()
    {
        if (rangeIndicator != null)
        {
            float visualRange = range;
            rangeIndicator.transform.localScale = new Vector3(visualRange * 2, visualRange * 2, 1);
        }
    }

    void OnDestroy()
    {
        if (ObjectRegistry.Instance != null)
        {
            ObjectRegistry.Instance.UnregisterTower(this);
        }

        if (type == TowerType.Support)
        {
            RemoveSupportFromAll();
        }
    }

    void Update()
    {
        if (PauseMenu.Instance != null && PauseMenu.Instance.IsGamePaused())
            return;

        if (!GameManager.Instance.isGameActive || GameManager.Instance.isPreparationPhase) return;

        attackTimer -= Time.deltaTime;

        UpdateTargets();

        if (type == TowerType.Support)
        {
            ApplySupportToNearbyTowers();
        }

        if (attackTimer <= 0f && virusesInRange.Count > 0)
        {
            Attack();
            attackTimer = 1f / attackSpeed;
        }
    }

    void UpdateTargets()
    {
        virusesInRange.Clear();

        if (ObjectRegistry.Instance != null)
        {
            List<Virus> allViruses = ObjectRegistry.Instance.GetAllViruses();
            foreach (var virus in allViruses)
            {
                if (virus == null || virus.isDead || virus.reachedEnd) continue;

                float distance = Vector2.Distance(transform.position, virus.transform.position);
                if (distance <= range)
                {
                    virusesInRange.Add(virus);
                }
            }
        }
    }

    void Attack()
    {
        switch (type)
        {
            case TowerType.Slow:
                AttackSlow();
                break;
            case TowerType.Damage:
                AttackDamage();
                break;
            case TowerType.AOE:
                AttackAOE();
                break;
            case TowerType.Support:
                break;
        }
    }

    void AttackSlow()
    {
        foreach (var virus in virusesInRange)
        {
            if (virus != null && !virus.isDead)
            {
                float actualSlowAmount = slowAmount;
                if (virus.type == VirusType.Tank)
                {
                    actualSlowAmount *= 0.25f;
                }

                virus.ApplySlow(actualSlowAmount, 3f);

                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayTowerShoot(TowerType.Slow);

                ShowSlowEffect(virus.transform.position);
            }
        }
    }

    void AttackDamage()
    {
        Virus target = GetClosestVirus();
        if (target == null) return;

        float totalDamage = damage * (1f + damageBuff);

        switch (target.type)
        {
            case VirusType.Normal:
                totalDamage *= 1.0f;
                break;
            case VirusType.Fast:
                totalDamage *= 1.2f;
                break;
            case VirusType.Tank:
                totalDamage *= 1.5f;
                break;
        }

        target.TakeDamage(totalDamage);

        if (BattleLogManager.Instance != null)
        {
            BattleLogManager.Instance.AddDamageMessage(
                $"{name} → {target.gameObject.name} ({totalDamage:F1} урона)"
            );

            if (target.isDead)
            {
                BattleLogManager.Instance.AddDamageMessage(
                    $"{target.gameObject.name} уничтожен"
                );
            }
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayTowerShoot(TowerType.Damage);

        if (projectilePrefab != null)
        {
            StartCoroutine(ShootProjectile(target.transform.position));
        }
        else
        {
            StartCoroutine(ShowDamageEffect(target.transform.position));
        }
    }

    void AttackAOE()
    {
        if (virusesInRange.Count == 0) return;

        Vector3 center = Vector3.zero;
        int count = 0;
        List<Virus> localViruses = new List<Virus>();

        foreach (var virus in virusesInRange)
        {
            if (virus != null && !virus.isDead)
            {
                center += virus.transform.position;
                count++;
                localViruses.Add(virus);
            }
        }
        if (count == 0) return;

        center /= count;


        float baseDamage = damage * (1f + damageBuff);

        foreach (var virus in localViruses)
        {
            float distance = Vector3.Distance(center, virus.transform.position);
            if (distance <= aoeRadius)
            {
                float finalDamage = baseDamage;

                if (virus.type == VirusType.Tank)
                {
                    finalDamage *= 0.5f;
                }

                if (virus.slowMultiplier > 0f)
                {
                    finalDamage *= 1.8f;
                }

                virus.TakeDamage(finalDamage);
            }
        }

        if (aoeEffectPrefab != null)
        {
            GameObject effect = Instantiate(aoeEffectPrefab, center, Quaternion.identity);
            effect.transform.localScale = new Vector3(aoeRadius * 2, aoeRadius * 2, 1);
            Destroy(effect, 0.5f);
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayTowerShoot(TowerType.AOE);
    }

    public void ApplySupportToNearbyTowers()
    {
        if (ObjectRegistry.Instance != null)
        {
            List<Tower> allTowers = ObjectRegistry.Instance.GetAllTowers();
            List<Tower> towersInRange = new List<Tower>();

            foreach (var tower in allTowers)
            {
                if (tower == null || tower == this || tower.type == TowerType.Support) continue;

                float distance = Vector2.Distance(transform.position, tower.transform.position);
                if (distance <= range)
                {
                    towersInRange.Add(tower);
                }
            }

            for (int i = supportedTowers.Count - 1; i >= 0; i--)
            {
                Tower tower = supportedTowers[i];
                if (tower == null || !towersInRange.Contains(tower))
                {
                    RemoveSupport(tower);
                    supportedTowers.RemoveAt(i);
                }
            }

            foreach (var tower in towersInRange)
            {
                if (!supportedTowers.Contains(tower))
                {
                    ApplySupport(tower);
                    supportedTowers.Add(tower);
                }
            }

            if (supportEffectPrefab != null && supportedTowers.Count > 0)
            {
                if (!supportEffectPrefab.activeSelf)
                    supportEffectPrefab.SetActive(true);
            }
            else if (supportEffectPrefab != null)
            {
                supportEffectPrefab.SetActive(false);
            }
        }
    }

    void ApplySupport(Tower tower)
    {
        if (tower == null) return;

        float newDamageBuff = supportBoost * level;
        float newSpeedBuff = supportBoost * level;
        float newMultiplier = 1f + (supportBoost * level);

        tower.damageBuff += newDamageBuff;
        tower.attackSpeedBuff += newSpeedBuff;
        tower.attackSpeed *= newMultiplier;

        if (BattleLogManager.Instance != null)
            BattleLogManager.Instance.AddSystemMessage(
                $"{tower.name} получила новый бафф: урон +{newDamageBuff * 100:F0}%, скорость +{newSpeedBuff * 100:F0}%"
            );

        Debug.Log($"Башня {tower.type} получила бафф: урон +{newDamageBuff * 100:F0}%, скорость +{newSpeedBuff * 100:F0}%");
    }

    void RemoveSupport(Tower tower)
    {
        if (tower == null) return;

        tower.damageBuff -= supportBoost * level;
        tower.attackSpeedBuff -= supportBoost * level;

        float currentMultiplier = 1f + (supportBoost * level);
        if (currentMultiplier != 0f)
        {
            tower.attackSpeed /= currentMultiplier;
        }
    }

    void RemoveSupportFromAll()
    {
        foreach (var tower in supportedTowers)
        {
            RemoveSupport(tower);
        }
        supportedTowers.Clear();
    }

    IEnumerator ShootProjectile(Vector3 targetPosition)
    {
        if (projectilePrefab == null) yield break;

        GameObject projectile = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        float journeyTime = 0.2f;
        float elapsed = 0f;
        Vector3 startPosition = transform.position;

        while (elapsed < journeyTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / journeyTime;
            projectile.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        if (hitEffectPrefab != null)
        {
            GameObject hitEffect = Instantiate(hitEffectPrefab, targetPosition, Quaternion.identity);
            Destroy(hitEffect, 0.5f);
        }

        Destroy(projectile);
    }

    IEnumerator ShowDamageEffect(Vector3 position)
    {
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
            effect.transform.localScale = Vector3.one * 0.5f;
            Destroy(effect, 0.2f);
        }
        yield return null;
    }

    void ShowSlowEffect(Vector3 position)
    {
        GameObject effect = new GameObject("SlowEffect");
        effect.transform.position = position;

        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.color = new Color(0, 0.5f, 1f, 0.5f);
        renderer.sortingLayerName = "Effects";

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        renderer.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        effect.transform.localScale = new Vector3(0.5f, 0.5f, 1);

        Destroy(effect, 0.3f);
    }

    Virus GetClosestVirus()
    {
        Virus closest = null;
        float closestDistance = float.MaxValue;

        foreach (var virus in virusesInRange)
        {
            if (virus == null || virus.isDead || virus.reachedEnd) continue;

            float distance = Vector3.Distance(transform.position, virus.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = virus;
            }
        }

        return closest;
    }

    public bool Upgrade()
    {
        if (level >= maxLevel) return false;

        int oldLevel = level;
        float oldSupportBoost = supportBoost; 

        level++;
        upgradeCost = Mathf.RoundToInt(upgradeCost * 1.5f);

        switch (type)
        {
            case TowerType.Slow:
                if (level == 2)
                {
                    range = 3.3f;
                    attackSpeed = 0.7f;
                    damage = 4f;
                    slowAmount = 0.45f;
                }
                else if (level == 3)
                {
                    range = 3.6f;
                    attackSpeed = 0.8f;
                    damage = 6f;
                    slowAmount = 0.55f;
                }
                break;

            case TowerType.Damage:
                if (level == 2)
                {
                    range = 4.0f;
                    attackSpeed = 1.0f;
                    damage = 19f;
                }
                else if (level == 3)
                {
                    range = 4.5f;
                    attackSpeed = 1.1f;
                    damage = 28f;
                }
                break;

            case TowerType.AOE:
                if (level == 2)
                {
                    range = 5.1f;
                    attackSpeed = 0.5f;
                    damage = 20f;
                    aoeRadius = 2.1f;
                }
                else if (level == 3)
                {
                    range = 6.4f;
                    attackSpeed = 0.7f;
                    damage = 25f;
                    aoeRadius = 2.4f;
                }
                break;

            case TowerType.Support:
                if (level == 2)
                {
                    range = 5.0f;
                    supportBoost = 0.15f; 
                }
                else if (level == 3)
                {
                    range = 5.5f;
                    supportBoost = 0.20f; 
                }

                UpdateSupportBuffsOnUpgrade(oldLevel, oldSupportBoost);
                break;
        }

        UpdateRangeIndicatorSize();
        UpdateVisual();

        Debug.Log($"Башня {type} улучшена до уровня {level}");
        if (BattleLogManager.Instance != null)
            BattleLogManager.Instance.AddSystemMessage(
                $"{name} улучшена до {level} lvl!"
            );
        return true;
    }

    void UpdateSupportBuffsOnUpgrade(int oldLevel, float oldSupportBoost)
    {
        // Проходим по всем поддержанным башням
        for (int i = supportedTowers.Count - 1; i >= 0; i--)
        {
            Tower tower = supportedTowers[i];
            if (tower == null)
            {
                supportedTowers.RemoveAt(i);
                continue;
            }

            RemoveOldSupport(tower, oldLevel, oldSupportBoost);

            ApplySupport(tower);

            Debug.Log($"Обновлён бафф для {tower.name}: {oldSupportBoost * 100}% → {supportBoost * 100}%");
        }
    }

    void RemoveOldSupport(Tower tower, int oldLevel, float oldSupportBoost)
    {
        if (tower == null) return;

        tower.damageBuff -= oldSupportBoost * oldLevel;
        tower.attackSpeedBuff -= oldSupportBoost * oldLevel;

        float oldMultiplier = 1f + (oldSupportBoost * oldLevel);
        if (oldMultiplier != 0f)
        {
            tower.attackSpeed /= oldMultiplier;
        }

        if (BattleLogManager.Instance != null)
            BattleLogManager.Instance.AddSystemMessage(
                $"{tower.name} потеряла старый бафф (-{oldSupportBoost * oldLevel * 100:F0}%)"
            );
    }

    void UpdateVisual()
    {
        if (spriteRenderer != null)
        {
            float scale = 1f + (level - 1) * 0.1f;
            transform.localScale = Vector3.one * scale;
        }
    }

    public int GetSellPrice()
    {
        int totalCost = buildCost;
        for (int i = 1; i < level; i++)
        {
            totalCost += upgradeCost;
        }
        return Mathf.RoundToInt(totalCost * 0.7f);
    }

    void OnMouseEnter()
    {
        if (!GameManager.Instance.isGameActive || GameManager.Instance.isPreparationPhase) return;

        isHovered = true;
        UpdateRangeIndicatorVisibility();
    }

    void OnMouseExit()
    {
        isHovered = false;
        UpdateRangeIndicatorVisibility();
    }

    void OnMouseDown()
    {
        if (!GameManager.Instance.isGameActive || GameManager.Instance.isPreparationPhase) return;

        if (node != null && BuildMenu.Instance != null)
        {
            BuildMenu.Instance.ShowForNode(node);
        }
    }

    public void UpdateRangeIndicatorVisibility()
    {
        if (rangeIndicator == null)
        {
            Transform indicator = transform.Find("RangeIndicator");
            if (indicator != null)
            {
                rangeIndicator = indicator.gameObject;
            }
            else
            {
                return;
            }
        }

        bool shouldShow = isHovered || isSelected;
        rangeIndicator.SetActive(shouldShow);
    }

    public void Deselect()
    {
        isSelected = false;
        UpdateRangeIndicatorVisibility();
    }
}