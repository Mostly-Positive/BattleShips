using UnityEngine;
using System.Collections;

public class ShipMovement : MonoBehaviour
{
    [Header("Настройки движения")]
    public float movementDuration = 0.5f;
    public float rotationDuration = 0.3f;
    
    private Ship ship;
    private Grid grid;
    private TurnManager turnManager;
    
    private bool isMoving = false;
    private bool isRotating = false;

    void Awake()
    {
        ship = GetComponent<Ship>();
    }

    void Start()
    {
        grid = FindObjectOfType<Grid>();
        turnManager = FindObjectOfType<TurnManager>();
    }

    void Update()
    {
        if (!CanProcessInput()) return;
        
        HandleRotationInput();
        HandleMovementInput();
    }

    public void Rotate(int directionChange)
    {
        if (!CanRotate()) return;
        
        int newDirection = (ship.direction + directionChange + 6) % 6;
        StartCoroutine(RotateCoroutine(newDirection));
    }

    public void Move(int distance)
    {
        if (!CanMove(distance)) return;
        
        Vector3Int targetCell = CalculateMovementTarget(distance);
        StartCoroutine(MoveCoroutine(targetCell));
    }

    private IEnumerator RotateCoroutine(int newDirection)
    {
        isRotating = true;
        
        float timer = 0f;
        Quaternion startRotation = transform.rotation;
        
        // ИСПРАВЛЕННЫЙ РАСЧЕТ ДЛЯ ГЕКСАГОНАЛЬНОЙ СЕТКИ
        // Для "pointy top" гексов каждое направление = 60 градусов
        float angle = -newDirection * 60f - 90;
        Quaternion targetRotation = Quaternion.Euler(0, 0, angle);
        
        while (timer < rotationDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / rotationDuration;
            transform.rotation = Quaternion.Lerp(startRotation, targetRotation, progress);
            yield return null;
        }
        
        ship.direction = newDirection;
        transform.rotation = targetRotation;
        
        isRotating = false;
    }

    private IEnumerator MoveCoroutine(Vector3Int targetCell)
    {
        isMoving = true;
        
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = grid.CellToWorld(targetCell);
        
        float timer = 0f;
        
        while (timer < movementDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / movementDuration;
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.Lerp(startPosition, targetPosition, easedProgress);
            
            yield return null;
        }
        
        transform.position = targetPosition;
        ship.UpdateGridPosition();
        
        isMoving = false;
    }

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

    private void HandleRotationInput()
    {
        if (Input.GetKeyDown(KeyCode.Q)) // Поворот налево (против часовой)
        {
            Rotate(-1);
            ship.UseAction();
        }
        else if (Input.GetKeyDown(KeyCode.E)) // Поворот направо (по часовой)
        {
            Rotate(1);
            ship.UseAction();
        }
    }

    private void HandleMovementInput()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
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
}