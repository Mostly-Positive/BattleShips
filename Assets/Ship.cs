using UnityEngine;

public enum Team
{
    Player,
    Enemy
}

public class Ship : MonoBehaviour
{
    [Header("Основные характеристики")]
    public Team team;
    public string shipName;
    
    [Header("Тактические характеристики")]
    public int maxActions = 2;
    public int currentActions;
    
    [Header("Боевые характеристики")]
    public int maxHealth = 100;
    public int currentHealth;
    public int armor = 5;
    public int movementSpeed = 3;
    public int attackRange = 3;
    
    [Header("Урон (строки в формате '2D6')")]
    public string rangedAttackDamage = "2D6";
    public string boardingDamage = "3D6";
    
    [Header("Текущее состояние")]
    public int direction = 0; // 0-5, где 0 - восток, по часовой стрелке
    public Vector3Int gridPosition;
    
    [Header("Связанные компоненты")]
    public ShipMovement movement;
    public ShipCombat combat;
    
    // Ссылки на другие системы
    private Grid grid;
    private TurnManager turnManager;

    void Awake()
    {
        // Получаем компоненты на этом же GameObject
        movement = GetComponent<ShipMovement>();
        combat = GetComponent<ShipCombat>();
        
        // Находим менеджеры в сцене
        grid = FindObjectOfType<Grid>();
        turnManager = FindObjectOfType<TurnManager>();
        
        // Инициализируем характеристики
        currentHealth = maxHealth;
        currentActions = maxActions;
    }

    void Start()
    {
        // Регистрируем начальную позицию на сетке
        UpdateGridPosition();
    }

    // Обновляет позицию корабля на сетке
    public void UpdateGridPosition()
    {
        Vector3Int newCellPosition = grid.WorldToCell(transform.position);
        
        // Если позиция изменилась, обновляем данные в сетке
        if (newCellPosition != gridPosition)
        {
            if (gridPosition != Vector3Int.zero) // Если это не начальная позиция
            {
                grid.RemoveShip(gridPosition);
            }
            grid.PlaceShip(this, newCellPosition);
            gridPosition = newCellPosition;
        }
    }

    // === МЕТОДЫ ДЛЯ УПРАВЛЕНИЯ СОСТОЯНИЕМ ===

    // Сброс действий в начале хода
    public void ResetActions()
    {
        currentActions = maxActions;
    }

    // Использование действия
    public bool UseAction(int cost = 1)
    {
        if (currentActions >= cost)
        {
            currentActions -= cost;
            return true;
        }
        return false;
    }

    // Получение урона
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log($"{shipName} получил {damage} урона. Осталось здоровья: {currentHealth}");
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // Уничтожение корабля
    private void Die()
    {
        Debug.Log($"{shipName} уничтожен!");
        
        // Убираем корабль с сетки
        grid.RemoveShip(gridPosition);
        
        // Оповещаем менеджер игры
        FindObjectOfType<GameManager>().OnShipDestroyed(this);
        
        // Уничтожаем GameObject
        Destroy(gameObject);
    }

    // === МЕТОДЫ ДЛЯ ПРОВЕРОК ===

    // Проверяет, может ли корабль выполнять действия
    public bool CanAct()
    {
        return currentActions > 0 && currentHealth > 0;
    }

    // Проверяет, является ли целевой корабль врагом
    public bool IsEnemy(Ship target)
    {
        return target != null && target.team != this.team;
    }

    // === МЕТОДЫ ДЛЯ РАСЧЕТОВ ===

    // Бросок кубиков по строке формата "2D6"
    public int RollDice(string diceNotation)
    {
        // Парсим строку типа "2D6"
        string[] parts = diceNotation.ToUpper().Split('D');
        if (parts.Length != 2)
        {
            Debug.LogError($"Неверный формат кубиков: {diceNotation}");
            return 0;
        }

        int numberOfDice = int.Parse(parts[0]);
        int diceSides = int.Parse(parts[1]);
        
        int total = 0;
        for (int i = 0; i < numberOfDice; i++)
        {
            total += Random.Range(1, diceSides + 1);
        }
        
        Debug.Log($"{shipName} бросает {diceNotation}: {total}");
        return total;
    }

    // Получить мировую позицию для визуализации
    public Vector3 GetWorldPosition()
    {
        return grid.CellToWorld(gridPosition);
    }

    // Визуальное представление направления (для отладки)
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        
        // Рисуем линию, показывающую направление корабля
        Vector3 forward = GetDirectionVector() * 2f;
        Gizmos.color = team == Team.Player ? Color.blue : Color.red;
        Gizmos.DrawLine(transform.position, transform.position + forward);
        
        // Рисуем радиус атаки
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange * 1.5f); // 1.5f - примерный множитель для гексов
    }

    // Вспомогательный метод для получения вектора направления
    private Vector3 GetDirectionVector()
    {
        float angle = direction * 60f; // 60 градусов на направление
        return Quaternion.Euler(0, 0, -angle) * Vector3.right;
    }
}