using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
    [Header("Настройки игры")]
    public int playerShipsCount = 1;
    public int enemyShipsCount = 3;
    public GameObject gameUI; // Ссылка на UI канвас

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
    //private CameraController cameraController;

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
        //cameraController = FindObjectOfType<CameraController>();
    }

    void Start()
    {
        StartCoroutine(InitializeGame());
    }

    void Update()
    {
        // Обработка рестарта игры
        if (gameState == GameState.GameOver && Input.GetKeyDown(KeyCode.R))
        {
            RestartGame();
        }
    }

    // === ОСНОВНЫЕ МЕТОДЫ УПРАВЛЕНИЯ ИГРОЙ ===

    // Инициализация игры
    private IEnumerator InitializeGame()
    {
        gameState = GameState.Loading;
        Debug.Log("Инициализация игры...");

        // Ждем инициализации всех систем
        yield return new WaitForSeconds(0.1f);

        // Спавним корабли
        yield return StartCoroutine(SpawnShips());

        // Начинаем игру
        gameState = GameState.Playing;
        OnGameStateChanged?.Invoke(gameState);

        Debug.Log("Игра началась!");
    }

    // Спавн всех кораблей
    private IEnumerator SpawnShips()
    {
        // Спавн кораблей игрока
        for (int i = 0; i < playerShipsCount; i++)
        {
            SpawnShip(Team.Player, GetPlayerSpawnPosition());
            yield return new WaitForSeconds(spawnDelay);
        }

        // Спавн кораблей противника
        for (int i = 0; i < enemyShipsCount; i++)
        {
            SpawnShip(Team.Enemy, GetEnemySpawnPosition());
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    // Спавн одного корабля
    private void SpawnShip(Team team, Vector3Int gridPosition)
    {
        GameObject shipPrefab = GetRandomShipPrefab(team);
        if (shipPrefab == null)
        {
            Debug.LogError($"Не найден префаб корабля для команды: {team}");
            return;
        }

        // Находим валидную позицию для спавна
        Vector3Int spawnPosition = FindValidSpawnPosition(gridPosition);
        if (spawnPosition == Vector3Int.zero)
        {
            Debug.LogError($"Не удалось найти валидную позицию для спавна корабля команды: {team}");
            return;
        }

        // Создаем корабль
        Vector3 worldPosition = grid.CellToWorld(spawnPosition);
        GameObject shipObject = Instantiate(shipPrefab, worldPosition, Quaternion.identity);
        
        // Настраиваем корабль
        Ship ship = shipObject.GetComponent<Ship>();
        if (ship != null)
        {
            ship.team = team;
            ship.shipName = $"{team} Ship {(team == Team.Player ? playerShips.Count + 1 : enemyShips.Count + 1)}";
            ship.UpdateGridPosition();

            // Добавляем в соответствующий список
            if (team == Team.Player)
                playerShips.Add(ship);
            else
                enemyShips.Add(ship);

            Debug.Log($"Создан корабль: {ship.shipName} в позиции {spawnPosition}");
        }

        // Настраиваем направление корабля
        SetupShipDirection(shipObject, team);
    }

    // Поиск валидной позиции для спавна
    private Vector3Int FindValidSpawnPosition(Vector3Int preferredPosition)
    {
        // Пытаемся использовать предпочитаемую позицию
        if (grid.IsCellWalkable(preferredPosition))
        {
            return preferredPosition;
        }

        // Ищем соседние валидные позиции
        for (int attempt = 0; attempt < spawnRetryAttempts; attempt++)
        {
            int randomDirection = Random.Range(0, 6);
            Vector3Int neighbor = grid.GetNeighborCell(preferredPosition, randomDirection);
            
            if (grid.IsCellWalkable(neighbor))
            {
                return neighbor;
            }
        }

        // Если не нашли подходящую позицию, возвращаем нулевую
        return Vector3Int.zero;
    }

    // Настройка направления корабля при спавне
    private void SetupShipDirection(GameObject shipObject, Team team)
    {
        Ship ship = shipObject.GetComponent<Ship>();
        if (ship != null)
        {
            // Корабли игрока смотрят на восток, враги - на запад
            ship.direction = (team == Team.Player) ? 0 : 3;
            
            // Применяем поворот визуально
            shipObject.transform.rotation = Quaternion.Euler(0, 0, -ship.direction * 60f);
        }
    }

    // === МЕТОДЫ ДЛЯ ПОЗИЦИЙ СПАВНА ===

    // Получить позицию спавна для игрока (левая треть поля)
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

    // Получить позицию спавна для врага (правая треть поля)
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

    // Получить случайный префаб корабля для команды
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

    // === ОБРАБОТКА СОБЫТИЙ ИГРЫ ===

    // Вызывается когда корабль уничтожен
    public void OnShipDestroyed(Ship ship)
    {
        Debug.Log($"Корабль уничтожен: {ship.shipName}");

        // Удаляем корабль из списков
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

        // Уведомляем TurnManager
        turnManager.RemoveShip(ship);
    }

    // Проверка условий окончания игры
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

    // Завершение игры
    public void EndGame(Team winningTeam)
    {
        if (gameState == GameState.GameOver) return;

        gameState = GameState.GameOver;
        this.winningTeam = winningTeam;

        Debug.Log($"Игра окончена! Победила команда: {winningTeam}");

        // Уведомляем все системы о конце игры
        OnGameEnded?.Invoke(winningTeam);
        OnGameStateChanged?.Invoke(gameState);

        // Останавливаем или приостанавливаем игровые процессы
        Time.timeScale = 0.5f; // Замедляем время для драматического эффекта

        // Показываем UI окончания игры
        ShowGameOverUI();
    }

    // === UI МЕТОДЫ ===

    // Показать экран окончания игры
    private void ShowGameOverUI()
    {
        // Здесь можно активировать UI элементы
        if (gameUI != null)
        {
            // gameUI.GetComponent<GameUI>().ShowGameOverScreen(winningTeam);
        }

        // В консоли выводим сообщение
        string message = winningTeam == Team.Player ? 
            "Поздравляем! Вы победили!" : 
            "Вы проиграли! Попробуйте еще раз!";
        Debug.Log(message);
        Debug.Log("Нажмите R для рестарта");
    }

    // Перезапуск игры
    public void RestartGame()
    {
        Debug.Log("Перезапуск игры...");
        
        // Восстанавливаем нормальную скорость времени
        Time.timeScale = 1f;
        
        // Перезагружаем сцену
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    // === МЕТОДЫ ДЛЯ ВНЕШНЕГО ДОСТУПА ===

    public GameState GetGameState()
    {
        return gameState;
    }

    public Team GetWinningTeam()
    {
        return winningTeam;
    }

    public int GetPlayerShipsCount()
    {
        return playerShips.Count;
    }

    public int GetEnemyShipsCount()
    {
        return enemyShips.Count;
    }

    public List<Ship> GetPlayerShips()
    {
        return new List<Ship>(playerShips);
    }

    public List<Ship> GetEnemyShips()
    {
        return new List<Ship>(enemyShips);
    }

    // === GIZMOS ДЛЯ ОТЛАДКИ ===

    /*void OnDrawGizmos()
    {
        if (!Application.isPlaying || grid?.Tilemap == null) return;

        // Рисуем зоны спавна
        DrawSpawnZones();

        // Отображаем информацию о состоянии игры
        DrawGameInfo();
    }

    // Рисуем зоны спавна
    private void DrawSpawnZones()
    {
        BoundsInt gridBounds = grid.Tilemap.cellBounds;
        
        // Зона спавна игрока (левая треть)
        Gizmos.color = new Color(0, 1, 0, 0.1f);
        Vector3 playerZoneCenter = grid.CellToWorld(new Vector3Int(
            gridBounds.xMin + gridBounds.size.x / 6,
            gridBounds.center.y,
            0
        ));
        Vector3 playerZoneSize = new Vector3(gridBounds.size.x / 3, gridBounds.size.y, 0.1f);
        Gizmos.DrawCube(playerZoneCenter, playerZoneSize);
        Gizmos.DrawWireCube(playerZoneCenter, playerZoneSize);

        // Зона спавна врага (правая треть)
        Gizmos.color = new Color(1, 0, 0, 0.1f);
        Vector3 enemyZoneCenter = grid.CellToWorld(new Vector3Int(
            gridBounds.xMax - gridBounds.size.x / 6,
            gridBounds.center.y,
            0
        ));
        Vector3 enemyZoneSize = new Vector3(gridBounds.size.x / 3, gridBounds.size.y, 0.1f);
        Gizmos.DrawCube(enemyZoneCenter, enemyZoneSize);
        Gizmos.DrawWireCube(enemyZoneCenter, enemyZoneSize);
    }

    // Отображаем информацию об игре
    private void DrawGameInfo()
    {
        #if UNITY_EDITOR
        string gameInfo = $"Состояние: {gameState}\n";
        gameInfo += $"Корабли игрока: {playerShips.Count}\n";
        gameInfo += $"Корабли врага: {enemyShips.Count}\n";
        
        if (gameState == GameState.GameOver)
        {
            gameInfo += $"ПОБЕДИТЕЛЬ: {winningTeam}\n";
            gameInfo += "Нажмите R для рестарта";
        }

        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;
        style.normal.background = Texture2D.grayTexture;

        UnityEditor.Handles.Label(new Vector3(10, 100, 0), gameInfo, style);
        #endif
    }*/
}