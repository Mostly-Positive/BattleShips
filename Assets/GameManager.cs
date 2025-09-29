using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    [Header("Настройки игры")]
    public int playerShipsCount = 1;
    public int enemyShipsCount = 3;

    [Header("Префабы кораблей")]
    public GameObject[] playerShipPrefabs;
    public GameObject[] enemyShipPrefabs;

    [Header("Настройки спавна")]
    public float spawnDelay = 0.5f;
    public int spawnRetryAttempts = 10;

    // Состояние игры
    private GameState gameState = GameState.Playing;
    private Team winningTeam;

    // Ссылки на системы
    private Grid grid;
    private TurnManager turnManager;

    // Списки кораблей
    private List<Ship> playerShips = new List<Ship>();
    private List<Ship> enemyShips = new List<Ship>();

    // События для UI и других систем
    public System.Action<GameState> OnGameStateChanged;
    public System.Action<Team> OnGameEnded;

    public enum GameState
    {
        Loading,
        Playing,
        GameOver
    }

    void Awake()
    {
        grid = FindObjectOfType<Grid>();
        turnManager = FindObjectOfType<TurnManager>();
    }

    void Start()
    {
        StartCoroutine(InitializeGame());
    }

    void Update()
    {
        if (gameState == GameState.GameOver && Input.GetKeyDown(KeyCode.R))
        {
            RestartGame();
        }
    }

    private IEnumerator InitializeGame()
    {
        gameState = GameState.Loading;
        Debug.Log("Инициализация игры...");

        yield return new WaitForSeconds(0.1f);
        yield return StartCoroutine(SpawnShips());

        gameState = GameState.Playing;
        OnGameStateChanged?.Invoke(gameState);
        Debug.Log("Игра началась!");
    }

    private IEnumerator SpawnShips()
    {
        for (int i = 0; i < playerShipsCount; i++)
        {
            SpawnShip(Team.Player, GetPlayerSpawnPosition());
            yield return new WaitForSeconds(spawnDelay);
        }

        for (int i = 0; i < enemyShipsCount; i++)
        {
            SpawnShip(Team.Enemy, GetEnemySpawnPosition());
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    private void SpawnShip(Team team, Vector3Int gridPosition)
    {
        GameObject shipPrefab = GetRandomShipPrefab(team);
        if (shipPrefab == null) return;

        Vector3Int spawnPosition = FindValidSpawnPosition(gridPosition);
        if (spawnPosition == Vector3Int.zero) return;

        Vector3 worldPosition = grid.CellToWorld(spawnPosition);
        GameObject shipObject = Instantiate(shipPrefab, worldPosition, Quaternion.identity);
        
        Ship ship = shipObject.GetComponent<Ship>();
        if (ship != null)
        {
            ship.team = team;
            ship.shipName = $"{team} Ship {(team == Team.Player ? playerShips.Count + 1 : enemyShips.Count + 1)}";
            ship.UpdateGridPosition();

            if (team == Team.Player)
                playerShips.Add(ship);
            else
                enemyShips.Add(ship);

            Debug.Log($"Создан корабль: {ship.shipName} в позиции {spawnPosition}");
        }

        SetupShipDirection(shipObject, team);
    }

    private Vector3Int FindValidSpawnPosition(Vector3Int preferredPosition)
    {
        if (grid.IsCellWalkable(preferredPosition))
        {
            return preferredPosition;
        }

        for (int attempt = 0; attempt < spawnRetryAttempts; attempt++)
        {
            int randomDirection = Random.Range(0, 6);
            Vector3Int neighbor = grid.GetNeighborCell(preferredPosition, randomDirection);
            
            if (grid.IsCellWalkable(neighbor))
            {
                return neighbor;
            }
        }

        return Vector3Int.zero;
    }

    private void SetupShipDirection(GameObject shipObject, Team team)
    {
        Ship ship = shipObject.GetComponent<Ship>();
        if (ship != null)
        {
            // Корабли игрока смотрят на восток (вправо), враги - на запад (влево)
            ship.direction = (team == Team.Player) ? 0 : 3;
            
            // ПРАВИЛЬНЫЙ РАСЧЕТ ДЛЯ ГЕКСАГОНАЛЬНОЙ СЕТКИ "POINT TOP"
            // Для "pointy top" гексов направления:
            // 0: Восток (вправо) -> 0°
            // 1: Юго-Восток -> 60°
            // 2: Юго-Запад -> 120°
            // 3: Запад (влево) -> 180°
            // 4: Северо-Запад -> 240°
            // 5: Северо-Восток -> 300°
            float angle = -ship.direction * 60f - 90;
            shipObject.transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private Vector3Int GetPlayerSpawnPosition()
    {
        BoundsInt gridBounds = grid.Tilemap.cellBounds;
        int leftThird = gridBounds.xMin + (gridBounds.size.x / 3);
        
        return new Vector3Int(
            Random.Range(gridBounds.xMin, leftThird),
            Random.Range(gridBounds.yMin, gridBounds.yMax),
            0
        );
    }

    private Vector3Int GetEnemySpawnPosition()
    {
        BoundsInt gridBounds = grid.Tilemap.cellBounds;
        int rightThird = gridBounds.xMax - (gridBounds.size.x / 3);
        
        return new Vector3Int(
            Random.Range(rightThird, gridBounds.xMax),
            Random.Range(gridBounds.yMin, gridBounds.yMax),
            0
        );
    }

    private GameObject GetRandomShipPrefab(Team team)
    {
        GameObject[] prefabs = (team == Team.Player) ? playerShipPrefabs : enemyShipPrefabs;
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogError($"Нет префабов кораблей для команды: {team}");
            return null;
        }
        
        return prefabs[Random.Range(0, prefabs.Length)];
    }

    public void OnShipDestroyed(Ship ship)
    {
        Debug.Log($"Корабль уничтожен: {ship.shipName}");

        if (playerShips.Contains(ship))
        {
            playerShips.Remove(ship);
            CheckGameEndCondition();
        }
        else if (enemyShips.Contains(ship))
        {
            enemyShips.Remove(ship);
            CheckGameEndCondition();
        }

        turnManager.RemoveShip(ship);
    }

    private void CheckGameEndCondition()
    {
        if (gameState != GameState.Playing) return;

        if (playerShips.Count == 0)
        {
            EndGame(Team.Enemy);
        }
        else if (enemyShips.Count == 0)
        {
            EndGame(Team.Player);
        }
    }

    public void EndGame(Team winningTeam)
    {
        if (gameState == GameState.GameOver) return;

        gameState = GameState.GameOver;
        this.winningTeam = winningTeam;

        Debug.Log($"Игра окончена! Победила команда: {winningTeam}");

        OnGameEnded?.Invoke(winningTeam);
        OnGameStateChanged?.Invoke(gameState);

        // Показываем сообщение о конце игры
        string message = winningTeam == Team.Player ? 
            "Поздравляем! Вы победили!" : 
            "Вы проиграли! Попробуйте еще раз!";
        Debug.Log(message);
        Debug.Log("Нажмите R для рестарта");
    }

    public void RestartGame()
    {
        Debug.Log("Перезапуск игры...");
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    public GameState GetGameState() => gameState;
    public Team GetWinningTeam() => winningTeam;
    public int GetPlayerShipsCount() => playerShips.Count;
    public int GetEnemyShipsCount() => enemyShips.Count;
}