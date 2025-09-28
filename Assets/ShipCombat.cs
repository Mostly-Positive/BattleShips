using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ShipCombat : MonoBehaviour
{
    [Header("Боевые настройки")]
    public float attackAnimationDuration = 0.5f;
    public GameObject cannonballPrefab;
    public Transform cannonballSpawnPoint;
    
    // Ссылки на компоненты
    private Ship ship;
    private Grid grid;
    private TurnManager turnManager;
    
    // Состояние боя
    private bool isAttacking = false;
    private Ship selectedTarget;
    
    // Визуальные компоненты
    private Animator animator;
    private ParticleSystem attackParticles;

    void Awake()
    {
        ship = GetComponent<Ship>();
        animator = GetComponent<Animator>();
        attackParticles = GetComponentInChildren<ParticleSystem>();
    }

    void Start()
    {
        grid = FindObjectOfType<Grid>();
        turnManager = FindObjectOfType<TurnManager>();
    }

    void Update()
    {
        // Обрабатываем ввод только если это активный корабль в ходу игрока
        if (!CanProcessInput()) return;
        
        HandleCombatInput();
    }

    // === ОСНОВНЫЕ МЕТОДЫ БОЯ ===

    // Абордаж
    public void BoardingAttack(Ship target)
    {
        if (!CanBoardingAttack(target)) return;
        
        StartCoroutine(BoardingAttackCoroutine(target));
    }

    // Стрельба
    public void RangedAttack(Ship target)
    {
        if (!CanRangedAttack(target)) return;
        
        StartCoroutine(RangedAttackCoroutine(target));
    }

    // === КОРУТИНЫ ДЛЯ АТАК ===

    // Абордаж (ближний бой)
    private IEnumerator BoardingAttackCoroutine(Ship target)
    {
        isAttacking = true;
        OnAttackStart();
        
        // Анимация абордажа
        if (animator != null) animator.SetTrigger("Boarding");
        
        yield return new WaitForSeconds(attackAnimationDuration);
        
        // Расчет урона от абордажа
        int attackerDamage = CalculateBoardingDamage(ship);
        int defenderDamage = CalculateBoardingDamage(target);
        
        // Применение урона
        if (attackerDamage > defenderDamage)
        {
            int damage = attackerDamage - defenderDamage;
            target.TakeDamage(damage);
            Debug.Log($"{ship.shipName} успешно абордировал {target.shipName} и нанес {damage} урона!");
        }
        else if (defenderDamage > attackerDamage)
        {
            int damage = defenderDamage - attackerDamage;
            ship.TakeDamage(damage);
            Debug.Log($"{target.shipName} отбил абордаж и нанес {damage} урона {ship.shipName}!");
        }
        else
        {
            Debug.Log("Абордаж закончился ничьей! Оба корабля не получили урона.");
        }
        
        OnAttackComplete();
        isAttacking = false;
        
        // Используем действие
        ship.UseAction();
    }

    // Дальняя атака (стрельба)
    private IEnumerator RangedAttackCoroutine(Ship target)
    {
        isAttacking = true;
        OnAttackStart();
        
        // Анимация стрельбы
        if (animator != null) animator.SetTrigger("RangedAttack");
        
        // Визуальный эффект выстрела
        if (cannonballPrefab != null && cannonballSpawnPoint != null)
        {
            GameObject cannonball = Instantiate(cannonballPrefab, cannonballSpawnPoint.position, Quaternion.identity);
            StartCoroutine(MoveCannonball(cannonball, target.transform.position));
        }
        
        yield return new WaitForSeconds(attackAnimationDuration);
        
        // Расчет урона от стрельбы
        int attackRoll = ship.RollDice(ship.rangedAttackDamage);
        int damage = Mathf.Max(0, attackRoll - target.armor);
        
        if (damage > 0)
        {
            target.TakeDamage(damage);
            Debug.Log($"{ship.shipName} попал в {target.shipName} и нанес {damage} урона! (Бросок: {attackRoll} - Броня: {target.armor} = {damage})");
        }
        else
        {
            Debug.Log($"{ship.shipName} не пробил броню {target.shipName}! (Бросок: {attackRoll} - Броня: {target.armor} = {damage})");
        }
        
        OnAttackComplete();
        isAttacking = false;
        
        // Используем действие
        ship.UseAction();
    }

    // Анимация полета ядра
    private IEnumerator MoveCannonball(GameObject cannonball, Vector3 targetPosition)
    {
        float duration = 0.5f;
        float timer = 0f;
        Vector3 startPosition = cannonball.transform.position;
        
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;
            cannonball.transform.position = Vector3.Lerp(startPosition, targetPosition, progress);
            yield return null;
        }
        
        Destroy(cannonball);
    }

    // === МЕТОДЫ ПРОВЕРКИ ВОЗМОЖНОСТИ АТАК ===

    private bool CanProcessInput()
    {
        return turnManager.IsPlayerTurn() && 
               turnManager.GetCurrentShip() == ship && 
               ship.CanAct() && 
               !isAttacking;
    }

    public bool CanBoardingAttack(Ship target)
    {
        if (!ship.CanAct() || isAttacking) return false;
        if (target == null || !ship.IsEnemy(target)) return false;
        
        // Проверяем, что цель соседняя
        return IsTargetAdjacent(target);
    }

    public bool CanRangedAttack(Ship target)
    {
        if (!ship.CanAct() || isAttacking) return false;
        if (target == null || !ship.IsEnemy(target)) return false;
        
        // Проверяем, что цель в радиусе и в боковых направлениях
        return IsTargetInSideArc(target);
    }

    // === МЕТОДЫ РАСЧЕТА И ПРОВЕРКИ ===

    // Проверка, является ли цель соседней
    private bool IsTargetAdjacent(Ship target)
    {
        Vector3Int currentPos = ship.gridPosition;
        Vector3Int targetPos = target.gridPosition;
        
        // Проверяем все соседние клетки
        for (int direction = 0; direction < 6; direction++)
        {
            Vector3Int neighbor = grid.GetNeighborCell(currentPos, direction);
            if (neighbor == targetPos)
            {
                return true;
            }
        }
        
        return false;
    }

    // Проверка, находится ли цель в боковых направлениях в радиусе атаки
    private bool IsTargetInSideArc(Ship target)
    {
        Vector3Int currentPos = ship.gridPosition;
        Vector3Int targetPos = target.gridPosition;
        
        // Проверяем расстояние
        int distance = grid.GetDistance(currentPos, targetPos);
        if (distance > ship.attackRange) return false;
        
        // Получаем все клетки в боковых направлениях
        List<Vector3Int> sideCells = grid.GetSideCellsInRange(currentPos, ship.direction, ship.attackRange);
        
        return sideCells.Contains(targetPos);
    }

    // Расчет урона от абордажа
    private int CalculateBoardingDamage(Ship attackingShip)
    {
        // Атакующий бросает 3 кубика, защищающийся - 2
        int numberOfDice = (attackingShip == ship) ? 3 : 2;
        
        // Парсим нотацию кубиков (например, "D6")
        string diceNotation = attackingShip.boardingDamage;
        string[] parts = diceNotation.ToUpper().Split('D');
        if (parts.Length != 2)
        {
            Debug.LogError($"Неверный формат кубиков для абордажа: {diceNotation}");
            return 0;
        }

        int diceSides = int.Parse(parts[1]);
        int total = 0;
        
        for (int i = 0; i < numberOfDice; i++)
        {
            total += Random.Range(1, diceSides + 1);
        }
        
        Debug.Log($"{attackingShip.shipName} бросает {numberOfDice}D{diceSides} для абордажа: {total}");
        return total;
    }

    // === ПОИСК ЦЕЛЕЙ ===

    // Получить все возможные цели для абордажа
    public List<Ship> GetPossibleBoardingTargets()
    {
        List<Ship> targets = new List<Ship>();
        Vector3Int currentPos = ship.gridPosition;
        
        // Проверяем все соседние клетки
        for (int direction = 0; direction < 6; direction++)
        {
            Vector3Int neighbor = grid.GetNeighborCell(currentPos, direction);
            Ship target = grid.GetShipInCell(neighbor);
            
            if (target != null && ship.IsEnemy(target))
            {
                targets.Add(target);
            }
        }
        
        return targets;
    }

    // Получить все возможные цели для стрельбы
    public List<Ship> GetPossibleRangedTargets()
    {
        List<Ship> targets = new List<Ship>();
        
        // Получаем все клетки в боковых направлениях в радиусе атаки
        List<Vector3Int> sideCells = grid.GetSideCellsInRange(ship.gridPosition, ship.direction, ship.attackRange);
        
        foreach (Vector3Int cell in sideCells)
        {
            Ship target = grid.GetShipInCell(cell);
            if (target != null && ship.IsEnemy(target))
            {
                targets.Add(target);
            }
        }
        
        return targets;
    }

    // === ОБРАБОТКА ВВОДА ===

    private void HandleCombatInput()
    {
        // Абордаж (кнопка A)
        if (Input.GetKeyDown(KeyCode.A))
        {
            List<Ship> boardingTargets = GetPossibleBoardingTargets();
            if (boardingTargets.Count > 0)
            {
                // Для простоты берем первую доступную цель
                // В полной реализации можно добавить выбор цели через UI
                BoardingAttack(boardingTargets[0]);
            }
            else
            {
                Debug.Log("Нет соседних вражеских кораблей для абордажа!");
            }
        }
        
        // Стрельба (кнопка S)
        if (Input.GetKeyDown(KeyCode.S))
        {
            List<Ship> rangedTargets = GetPossibleRangedTargets();
            if (rangedTargets.Count > 0)
            {
                // Для простоты берем первую доступную цель
                RangedAttack(rangedTargets[0]);
            }
            else
            {
                Debug.Log("Нет вражеских кораблей в радиусе и секторе стрельбы!");
            }
        }
    }

    // === ВИЗУАЛЬНЫЕ ЭФФЕКТЫ ===

    private void OnAttackStart()
    {
        // Воспроизвести звук подготовки к атаке
        // Включить частицы
        if (attackParticles != null) attackParticles.Play();
    }

    private void OnAttackComplete()
    {
        // Остановить частицы
        if (attackParticles != null) attackParticles.Stop();
    }

    // === МЕТОДЫ ДЛЯ UI ===

    // Подсветка доступных целей
    public void ShowAttackRange()
    {
        // Подсветка целей для абордажа
        List<Ship> boardingTargets = GetPossibleBoardingTargets();
        foreach (Ship target in boardingTargets)
        {
            // target.Highlight(Color.red); // Подсветить красным для абордажа
        }
        
        // Подсветка целей для стрельбы
        List<Ship> rangedTargets = GetPossibleRangedTargets();
        foreach (Ship target in rangedTargets)
        {
            // target.Highlight(Color.yellow); // Подсветить желтым для стрельбы
        }
    }

    public void HideAttackRange()
    {
        // Снять подсветку со всех целей
    }

    // === GIZMOS ДЛЯ ОТЛАДКИ ===

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || ship == null) return;
        
        // Рисуем радиус абордажа (соседние клетки)
        Gizmos.color = Color.red;
        Vector3Int currentPos = ship.gridPosition;
        
        for (int direction = 0; direction < 6; direction++)
        {
            Vector3Int neighbor = grid.GetNeighborCell(currentPos, direction);
            Vector3 worldPos = grid.CellToWorld(neighbor);
            Gizmos.DrawWireCube(worldPos, new Vector3(0.6f, 0.6f, 0.1f));
        }
        
        // Рисуем сектор стрельбы
        Gizmos.color = Color.yellow;
        List<Vector3Int> sideCells = grid.GetSideCellsInRange(currentPos, ship.direction, ship.attackRange);
        foreach (Vector3Int cell in sideCells)
        {
            Vector3 worldPos = grid.CellToWorld(cell);
            Gizmos.DrawWireSphere(worldPos, 0.3f);
        }
        
        // Рисуем направление корабля и сектора
        DrawAttackSectorGizmo();
    }

    // Визуализация секторов атаки
    private void DrawAttackSectorGizmo()
    {
        Vector3 center = transform.position;
        float radius = ship.attackRange * 1.5f; // Множитель для визуализации
        
        // Определяем углы сектора стрельбы (4 боковых направления)
        int[] sideDirections = {
            (ship.direction + 1) % 6,
            (ship.direction + 2) % 6,
            (ship.direction + 4) % 6,
            (ship.direction + 5) % 6
        };
        
        Gizmos.color = new Color(1, 1, 0, 0.3f); // Полупрозрачный желтый
        
        foreach (int dir in sideDirections)
        {
            float angle = dir * 60f; // 60 градусов на направление
            Vector3 direction = Quaternion.Euler(0, 0, -angle) * Vector3.right;
            Vector3 endPoint = center + direction * radius;
            Gizmos.DrawLine(center, endPoint);
        }
        
        // Заливаем сектор (примерно)
        for (int i = 0; i < sideDirections.Length; i += 2)
        {
            float angle1 = sideDirections[i] * 60f;
            float angle2 = sideDirections[i + 1] * 60f;
            
            Vector3 point1 = center + Quaternion.Euler(0, 0, -angle1) * Vector3.right * radius;
            Vector3 point2 = center + Quaternion.Euler(0, 0, -angle2) * Vector3.right * radius;
            
            Gizmos.DrawLine(center, point1);
            Gizmos.DrawLine(center, point2);
            Gizmos.DrawLine(point1, point2);
        }
    }
}