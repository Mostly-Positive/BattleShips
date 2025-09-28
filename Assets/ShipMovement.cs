using UnityEngine;
using System.Collections;

public class ShipMovement : MonoBehaviour
{
    [Header("Настройки движения")]
    public float movementDuration = 0.5f;
    public float rotationDuration = 0.3f;
    
    // Ссылки на компоненты
    private Ship ship;
    private Grid grid;
    private TurnManager turnManager;
    
    // Состояние движения
    private bool isMoving = false;
    private bool isRotating = false;
    
    // Визуальные компоненты (опционально)
    private Animator animator;
    private ParticleSystem movementParticles;

    void Awake()
    {
        ship = GetComponent<Ship>();
        animator = GetComponent<Animator>();
        movementParticles = GetComponentInChildren<ParticleSystem>();
    }

    void Start()
    {
        grid = FindObjectOfType<Grid>();
        turnManager = FindObjectOfType<TurnManager>();
        
        // Устанавливаем начальное визуальное направление
        UpdateRotationVisual();
    }

    void Update()
    {
        // Обрабатываем ввод только если это активный корабль в ходу игрока
        if (!CanProcessInput()) return;
        
        HandleRotationInput();
        HandleMovementInput();
    }

    // === ОСНОВНЫЕ МЕТОДЫ ДВИЖЕНИЯ ===

    // Поворот корабля
    public void Rotate(int directionChange)
    {
        if (!CanRotate()) return;
        
        int newDirection = (ship.direction + directionChange + 6) % 6;
        StartCoroutine(RotateCoroutine(newDirection));
    }

    // Перемещение корабля
    public void Move(int distance)
    {
        if (!CanMove(distance)) return;
        
        Vector3Int targetCell = CalculateMovementTarget(distance);
        StartCoroutine(MoveCoroutine(targetCell));
    }

    // === КОРУТИНЫ ДЛЯ АНИМАЦИЙ ===

    // Плавный поворот
    private IEnumerator RotateCoroutine(int newDirection)
    {
        isRotating = true;
        
        // Начинаем поворот
        OnRotationStart();
        
        float timer = 0f;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.Euler(0, 0, -newDirection * 60f);
        
        while (timer < rotationDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / rotationDuration;
            transform.rotation = Quaternion.Lerp(startRotation, targetRotation, progress);
            yield return null;
        }
        
        // Завершаем поворот
        ship.direction = newDirection;
        transform.rotation = targetRotation;
        
        OnRotationComplete();
        isRotating = false;
    }

    // Плавное перемещение
    private IEnumerator MoveCoroutine(Vector3Int targetCell)
    {
        isMoving = true;
        
        // Начинаем движение
        OnMovementStart();
        
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = grid.CellToWorld(targetCell);
        
        float timer = 0f;
        
        while (timer < movementDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / movementDuration;
            
            // Кривая для плавного движения (можно настроить)
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.Lerp(startPosition, targetPosition, easedProgress);
            
            yield return null;
        }
        
        // Завершаем движение
        transform.position = targetPosition;
        ship.UpdateGridPosition();
        
        OnMovementComplete();
        isMoving = false;
    }

    // === МЕТОДЫ ПРОВЕРКИ ВОЗМОЖНОСТИ ДЕЙСТВИЙ ===

    private bool CanProcessInput()
    {
        return turnManager.IsPlayerTurn() && 
               turnManager.GetCurrentShip() == ship && 
               ship.CanAct() && 
               !isMoving && 
               !isRotating;
    }

    private bool CanRotate()
    {
        return ship.CanAct() && !isMoving && !isRotating;
    }

    private bool CanMove(int distance)
    {
        if (!ship.CanAct() || isMoving || isRotating) return false;
        if (distance < 1 || distance > ship.movementSpeed) return false;
        
        Vector3Int targetCell = CalculateMovementTarget(distance);
        return grid.IsCellWalkable(targetCell);
    }

    // === РАСЧЕТЫ И ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===

    // Расчет целевой клетки для движения
    private Vector3Int CalculateMovementTarget(int distance)
    {
        Vector3Int currentCell = ship.gridPosition;
        Vector3Int targetCell = currentCell;
        
        // Двигаемся по прямой в текущем направлении
        for (int i = 0; i < distance; i++)
        {
            targetCell = grid.GetNeighborCell(targetCell, ship.direction);
        }
        
        return targetCell;
    }

    // Получить все возможные клетки для движения
    public Vector3Int[] GetPossibleMovementCells()
    {
        System.Collections.Generic.List<Vector3Int> possibleCells = new System.Collections.Generic.List<Vector3Int>();
        
        for (int distance = 1; distance <= ship.movementSpeed; distance++)
        {
            Vector3Int targetCell = CalculateMovementTarget(distance);
            if (grid.IsCellWalkable(targetCell))
            {
                possibleCells.Add(targetCell);
            }
            else
            {
                // Если встретили препятствие, дальше двигаться нельзя
                break;
            }
        }
        
        return possibleCells.ToArray();
    }

    // === ОБРАБОТКА ВВОДА ===

    private void HandleRotationInput()
    {
        if (Input.GetKeyDown(KeyCode.Q)) // Поворот налево
        {
            Rotate(-1);
            ship.UseAction();
        }
        else if (Input.GetKeyDown(KeyCode.E)) // Поворот направо
        {
            Rotate(1);
            ship.UseAction();
        }
    }

    private void HandleMovementInput()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            // Здесь можно реализовать выбор расстояния через UI
            // Пока что двигаемся на максимально возможное расстояние
            int distanceToMove = CalculateMaxPossibleDistance();
            if (distanceToMove > 0)
            {
                Move(distanceToMove);
                ship.UseAction();
            }
            else
            {
                Debug.Log("Невозможно двигаться в этом направлении!");
            }
        }
    }

    // Расчет максимального расстояния, на которое можно переместиться
    private int CalculateMaxPossibleDistance()
    {
        int maxDistance = 0;
        
        for (int distance = 1; distance <= ship.movementSpeed; distance++)
        {
            Vector3Int targetCell = CalculateMovementTarget(distance);
            if (grid.IsCellWalkable(targetCell))
            {
                maxDistance = distance;
            }
            else
            {
                break;
            }
        }
        
        return maxDistance;
    }

    // === ВИЗУАЛЬНЫЕ ЭФФЕКТЫ ===

    private void UpdateRotationVisual()
    {
        transform.rotation = Quaternion.Euler(0, 0, -ship.direction * 60f);
    }

    private void OnRotationStart()
    {
        // Воспроизвести звук поворота
        // Включить анимацию поворота
        if (animator != null) animator.SetTrigger("Rotate");
    }

    private void OnRotationComplete()
    {
        // Дополнительные эффекты после поворота
    }

    private void OnMovementStart()
    {
        // Воспроизвести звук движения
        // Включить анимацию движения
        if (animator != null) animator.SetBool("IsMoving", true);
        if (movementParticles != null) movementParticles.Play();
    }

    private void OnMovementComplete()
    {
        // Остановить анимации
        if (animator != null) animator.SetBool("IsMoving", false);
        if (movementParticles != null) movementParticles.Stop();
    }

    // === МЕТОДЫ ДЛЯ UI ===

    // Подсветка доступных для движения клеток
    public void ShowMovementRange()
    {
        Vector3Int[] possibleCells = GetPossibleMovementCells();
        
        // Здесь можно реализовать подсветку клеток через Grid
        // Например: grid.HighlightCells(possibleCells, Color.blue);
    }

    public void HideMovementRange()
    {
        // grid.ClearHighlights();
    }

    // === GIZMOS ДЛЯ ОТЛАДКИ ===

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || ship == null) return;
        
        // Рисуем возможные пути движения
        Gizmos.color = Color.blue;
        Vector3Int[] possibleCells = GetPossibleMovementCells();
        
        foreach (Vector3Int cell in possibleCells)
        {
            Vector3 worldPos = grid.CellToWorld(cell);
            Gizmos.DrawWireCube(worldPos, new Vector3(0.8f, 0.8f, 0.1f));
        }
        
        // Подсвечиваем текущее направление
        Gizmos.color = Color.green;
        Vector3 directionEnd = transform.position + GetDirectionVector() * 2f;
        Gizmos.DrawLine(transform.position, directionEnd);
    }

    private Vector3 GetDirectionVector()
    {
        float angle = ship.direction * 60f;
        return Quaternion.Euler(0, 0, -angle) * Vector3.right;
    }
}