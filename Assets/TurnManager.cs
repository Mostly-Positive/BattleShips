using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class TurnManager : MonoBehaviour
{
    [Header("Настройки ходов")]
    public float turnTransitionDelay = 1f;
    public float enemyTurnDelay = 2f;
    
    [Header("Текущее состояние")]
    public Team currentTeam;
    public Ship currentShip;
    public int turnNumber = 1;
    
    // Списки кораблей
    private List<Ship> playerShips = new List<Ship>();
    private List<Ship> enemyShips = new List<Ship>();
    private List<Ship> currentTeamShips = new List<Ship>();
    
    // Индекс текущего корабля в команде
    private int currentShipIndex = 0;
    
    // Состояние
    private bool isTurnInProgress = false;
    private bool isGameOver = false;
    
    // Ссылки
    private Grid grid;
    private GameManager gameManager;
    
    // События для UI
    public System.Action<Team> OnTeamChanged;
    public System.Action<Ship> OnShipChanged;
    public System.Action<int> OnTurnNumberChanged;

    void Awake()
    {
        grid = FindObjectOfType<Grid>();
        gameManager = FindObjectOfType<GameManager>();
    }

    void Start()
    {
        StartCoroutine(StartGameRoutine());
    }

    void Update()
    {
        // Обработка завершения хода по клавише (для отладки и удобства)
        if (Input.GetKeyDown(KeyCode.Space) && currentTeam == Team.Player && currentShip != null)
        {
            EndCurrentShipTurn();
        }
    }

    // === ОСНОВНЫЕ МЕТОДЫ УПРАВЛЕНИЯ ХОДАМИ ===

    // Запуск игры
    private IEnumerator StartGameRoutine()
    {
        yield return new WaitForSeconds(1f); 
        
        FindAllShips();
        
        StartTeamTurn(Team.Player);
    }

    // Начать ход команды
    private void StartTeamTurn(Team team)
    {
        if (isGameOver) return;
        
        currentTeam = team;
        currentTeamShips = (team == Team.Player) ? playerShips : enemyShips;
        
        currentTeamShips.RemoveAll(ship => ship == null || ship.currentHealth <= 0);
        
        if (currentTeamShips.Count == 0)
        {
            Team winningTeam = (team == Team.Player) ? Team.Enemy : Team.Player;
            gameManager.EndGame(winningTeam);
            isGameOver = true;
            return;
        }
        
        currentShipIndex = 0;
        StartShipTurn(currentTeamShips[currentShipIndex]);
        
        OnTeamChanged?.Invoke(currentTeam);
        OnTurnNumberChanged?.Invoke(turnNumber);
        
        Debug.Log($"Ход команды: {currentTeam}, ход #{turnNumber}");
    }

    // Начать ход корабля
    private void StartShipTurn(Ship ship)
    {
        currentShip = ship;
        isTurnInProgress = true;
        
        ship.ResetActions();
        
        OnShipChanged?.Invoke(currentShip);
        
        Debug.Log($"Активный корабль: {ship.shipName}, действий: {ship.currentActions}");
        
        if (currentTeam == Team.Enemy)
        {
            StartCoroutine(EnemyTurnRoutine());
        }
    }

    public void EndCurrentShipTurn()
    {
        if (!isTurnInProgress || currentShip == null) return;
        
        Debug.Log($"Завершен ход корабля: {currentShip.shipName}");
        
        NextShip();
    }

    // Переход к следующему кораблю
    private void NextShip()
    {
        currentShipIndex++;
        
        if (currentShipIndex < currentTeamShips.Count)
        {
            StartShipTurn(currentTeamShips[currentShipIndex]);
        }
        else
        {
            StartCoroutine(NextTeamRoutine());
        }
    }

    // Переход к следующей команде
    private IEnumerator NextTeamRoutine()
    {
        isTurnInProgress = false;
        
        yield return new WaitForSeconds(turnTransitionDelay);
        
        Team nextTeam = (currentTeam == Team.Player) ? Team.Enemy : Team.Player;
        
        if (currentTeam == Team.Player)
        {
            turnNumber++;
        }
        
        StartTeamTurn(nextTeam);
    }

    // === МЕТОДЫ ДЛЯ ВРАЖЕСКОГО ИИ ===

    // Ход вражеского корабля
    private IEnumerator EnemyTurnRoutine()
    {
        yield return new WaitForSeconds(enemyTurnDelay);
        
        // Простой ИИ для вражеских кораблей
        Ship enemyShip = currentShip;
        
        // Пытаемся атаковать
        if (TryEnemyAttack(enemyShip))
        {
            yield return new WaitForSeconds(1f);
        }
        
        // Если остались действия, пытаемся двигаться
        if (enemyShip.CanAct() && TryEnemyMovement(enemyShip))
        {
            yield return new WaitForSeconds(1f);
        }
        
        // Завершаем ход
        EndCurrentShipTurn();
    }

    // Попытка атаки вражеским кораблем
    private bool TryEnemyAttack(Ship enemyShip)
    {
        ShipCombat combat = enemyShip.combat;
        
        List<Ship> boardingTargets = combat.GetPossibleBoardingTargets();
        if (boardingTargets.Count > 0 && enemyShip.CanAct())
        {
            combat.BoardingAttack(boardingTargets[0]);
            return true;
        }
        
        List<Ship> rangedTargets = combat.GetPossibleRangedTargets();
        if (rangedTargets.Count > 0 && enemyShip.CanAct())
        {
            combat.RangedAttack(rangedTargets[0]);
            return true;
        }
        
        return false;
    }

    private bool TryEnemyMovement(Ship enemyShip)
    {
        ShipMovement movement = enemyShip.movement;
        
        Ship nearestEnemy = FindNearestEnemy(enemyShip);
        if (nearestEnemy != null)
        {
            movement.Move(1);
            return true;
        }
        
        return false;
    }

    // Поиск ближайшего врага
    private Ship FindNearestEnemy(Ship searcher)
    {
        List<Ship> enemyList = (searcher.team == Team.Player) ? enemyShips : playerShips;
        Ship nearest = null;
        float nearestDistance = float.MaxValue;
        
        foreach (Ship enemy in enemyList)
        {
            if (enemy == null || enemy.currentHealth <= 0) continue;
            
            float distance = Vector3.Distance(searcher.transform.position, enemy.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = enemy;
            }
        }
        
        return nearest;
    }

    // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===

    // Поиск всех кораблей на сцене
    private void FindAllShips()
    {
        Ship[] allShips = FindObjectsOfType<Ship>();
        playerShips.Clear();
        enemyShips.Clear();
        
        foreach (Ship ship in allShips)
        {
            if (ship.team == Team.Player)
            {
                playerShips.Add(ship);
            }
            else
            {
                enemyShips.Add(ship);
            }
        }
        
        Debug.Log($"Найдено кораблей: игрок - {playerShips.Count}, враг - {enemyShips.Count}");
    }

    // Удаление уничтоженного корабля из списков
    public void RemoveShip(Ship ship)
    {
        if (playerShips.Contains(ship))
        {
            playerShips.Remove(ship);
            currentTeamShips.Remove(ship);
        }
        else if (enemyShips.Contains(ship))
        {
            enemyShips.Remove(ship);
            currentTeamShips.Remove(ship);
        }
        
        if (currentShip == ship)
        {
            NextShip();
        }
    }

    // === МЕТОДЫ ДЛЯ ВНЕШНЕГО ДОСТУПА ===

    public bool IsPlayerTurn()
    {
        return currentTeam == Team.Player && isTurnInProgress;
    }

    public Ship GetCurrentShip()
    {
        return currentShip;
    }

    public Team GetCurrentTeam()
    {
        return currentTeam;
    }

    public int GetRemainingShips(Team team)
    {
        return (team == Team.Player) ? playerShips.Count : enemyShips.Count;
    }

    // Принудительное завершение хода (для UI кнопки)
    public void EndTurnButton()
    {
        if (IsPlayerTurn() && currentShip != null)
        {
            EndCurrentShipTurn();
        }
    }
}