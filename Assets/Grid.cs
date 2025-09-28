using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class Grid : MonoBehaviour
{
    // Ссылки на компоненты Unity
    public Tilemap Tilemap { get; private set; }
    private Dictionary<Vector3Int, Ship> cellToShipMap; // Словарь для быстрого поиска корабля по клетке

    // Направления для гексагональной сетки (шестигранники "pointy top")
    // Каждое направление — это вектор смещения для соседней клетки
    private static readonly Vector3Int[] Directions =
    {
        new Vector3Int(1, 0, 0),   // Восток
        new Vector3Int(0, -1, 0),  // Юго-Восток
        new Vector3Int(-1, -1, 0), // Юго-Запад
        new Vector3Int(-1, 0, 0),  // Запад
        new Vector3Int(0, 1, 0),   // Северо-Запад
        new Vector3Int(1, 1, 0)    // Северо-Восток
    };

    void Awake()
    {
        Tilemap = GetComponentInChildren<Tilemap>();
        if (Tilemap == null)
        {
            Debug.LogError("Tilemap не найден в дочерних объектах Grid!");
        }
        cellToShipMap = new Dictionary<Vector3Int, Ship>();
    }

    // === ОСНОВНЫЕ МЕТОДЫ ДЛЯ РАБОТЫ С ПОЗИЦИЯМИ ===

    // Конвертирует мировые координаты в координаты клетки сетки
    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        return Tilemap.WorldToCell(worldPosition);
    }

    // Конвертирует координаты клетки сетки в мировые координаты (центр клетки)
    public Vector3 CellToWorld(Vector3Int cellPosition)
    {
        return Tilemap.GetCellCenterWorld(cellPosition);
    }

    // Проверяет, является ли клетка проходимой (свободна от препятствий и других кораблей)
    public bool IsCellWalkable(Vector3Int cellPosition)
    {
        // 1. Проверяем, есть ли плитка (такой ли клетка вообще существует на карте)
        if (!Tilemap.HasTile(cellPosition))
        {
            return false;
        }

        // 2. Проверяем, не занята ли клетка другим кораблем
        if (cellToShipMap.ContainsKey(cellPosition))
        {
            return false;
        }

        // Здесь можно добавить проверку на другие препятствия (например, мели, острова)

        return true;
    }

    // === МЕТОДЫ ДЛЯ РАБОТЫ С КОРАБЛЯМИ ===

    // Привязывает корабль к клетке
    public void PlaceShip(Ship ship, Vector3Int cellPosition)
    {
        if (cellToShipMap.ContainsKey(cellPosition))
        {
            Debug.LogWarning($"Клетка {cellPosition} уже занята другим кораблем!");
            return;
        }
        cellToShipMap[cellPosition] = ship;
    }

    // Убирает корабль с клетки (при перемещении или уничтожении)
    public void RemoveShip(Vector3Int cellPosition)
    {
        cellToShipMap.Remove(cellPosition);
    }

    // Перемещает корабль с одной клетки на другую
    public void MoveShip(Ship ship, Vector3Int fromCell, Vector3Int toCell)
    {
        if (cellToShipMap.ContainsKey(fromCell) && cellToShipMap[fromCell] == ship)
        {
            RemoveShip(fromCell);
        }
        PlaceShip(ship, toCell);
    }

    // Возвращает корабль в указанной клетке (или null, если клетка пуста)
    public Ship GetShipInCell(Vector3Int cellPosition)
    {
        cellToShipMap.TryGetValue(cellPosition, out Ship ship);
        return ship;
    }

    // === МЕТОДЫ ДЛЯ НАВИГАЦИИ И РАСЧЕТОВ ===

    // Получить соседнюю клетку в заданном направлении (0-5)
    public Vector3Int GetNeighborCell(Vector3Int cellPosition, int direction)
    {
        direction = (direction % 6 + 6) % 6; // Нормализуем направление в диапазон 0-5
        return cellPosition + Directions[direction];
    }

    // Рассчитать расстояние между двумя клетками в гексах (алгоритм "шестиугольного" расстояния)
    public int GetDistance(Vector3Int fromCell, Vector3Int toCell)
    {
        Vector3Int diff = toCell - fromCell;
        return (Mathf.Abs(diff.x) + Mathf.Abs(diff.y) + Mathf.Abs(diff.z)) / 2;
    }

    // Найти все клетки в пределах радиуса (для движения и атаки)
    public List<Vector3Int> GetCellsInRange(Vector3Int centerCell, int range)
    {
        List<Vector3Int> results = new List<Vector3Int>();

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = Mathf.Max(-range, -dx - range); dy <= Mathf.Min(range, -dx + range); dy++)
            {
                int dz = -dx - dy;
                if (Mathf.Abs(dz) <= range)
                {
                    Vector3Int offset = new Vector3Int(dx, dy, 0);
                    // В гекс-координатах z = -x-y, но в нашем случае мы используем только x, y
                    // и подразумеваем, что z = 0 для всех наших клеток в Tilemap
                    Vector3Int cell = centerCell + offset;
                    if (Tilemap.HasTile(cell)) // Проверяем, что клетка существует на карте
                    {
                        results.Add(cell);
                    }
                }
            }
        }

        return results;
    }

    // === МЕТОД ДЛЯ ПОЛУЧЕНИЯ "БОКОВЫХ" КЛЕТОК (ДЛЯ СТРЕЛЬБЫ) ===
    // Возвращает список клеток в 4 боковых направлениях от корабля (исключая направление "вперед" и "назад")
    // в пределах заданной дальности.
    public List<Vector3Int> GetSideCellsInRange(Vector3Int centerCell, int shipDirection, int range)
    {
        List<Vector3Int> sideCells = new List<Vector3Int>();
        // Боковые направления относительно текущего направления корабля.
        // Для direction = 0 (Восток) это будут направления: 1, 2, 4, 5 (ЮВ, ЮЗ, СЗ, СВ).
        int[] sideDirections = {
            (shipDirection + 1) % 6,
            (shipDirection + 2) % 6,
            (shipDirection + 4) % 6,
            (shipDirection + 5) % 6
        };

        foreach (int dir in sideDirections)
        {
            // Для каждого бокового направления получаем все клетки по прямой до пределов дальности.
            Vector3Int currentCell = centerCell;
            for (int i = 1; i <= range; i++)
            {
                currentCell = GetNeighborCell(currentCell, dir);
                if (!Tilemap.HasTile(currentCell)) break; // Прерываем, если вышли за пределы карты.
                sideCells.Add(currentCell);
            }
        }
        return sideCells;
    }
}