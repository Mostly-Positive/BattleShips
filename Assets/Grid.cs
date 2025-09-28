using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class Grid : MonoBehaviour
{
    public Tilemap Tilemap { get; private set; }
    private Dictionary<Vector3Int, Ship> cellToShipMap;

    // ПРАВИЛЬНЫЕ НАПРАВЛЕНИЯ ДЛЯ ГЕКСАГОНАЛЬНОЙ СЕТКИ "POINTY TOP"
    // Для гексагональной сетки "pointy top" направления:
    // 0: Восток  (1, 0)
    // 1: Юго-Восток (1, -1) - ТОЛЬКО ДЛЯ ЧЕТНЫХ СТРОК, (0, -1) для нечетных
    // 2: Юго-Запад (0, -1) - ТОЛЬКО ДЛЯ ЧЕТНЫХ СТРОК, (-1, -1) для нечетных  
    // 3: Запад (-1, 0)
    // 4: Северо-Запад (0, 1) - ТОЛЬКО ДЛЯ ЧЕТНЫХ СТРОК, (-1, 1) для нечетных
    // 5: Северо-Восток (1, 1) - ТОЛЬКО ДЛЯ ЧЕТНЫХ СТРОК, (0, 1) для нечетных
    private static readonly Vector3Int[] DirectionsOdd = {
        new Vector3Int(1, 0, 0),    // 0: Восток
        new Vector3Int(1, -1, 0),   // 1: Юго-Восток
        new Vector3Int(0, -1, 0),   // 2: Юго-Запад
        new Vector3Int(-1, 0, 0),   // 3: Запад
        new Vector3Int(0, 1, 0),    // 4: Северо-Запад
        new Vector3Int(1, 1, 0)     // 5: Северо-Восток
    };

    private static readonly Vector3Int[] DirectionsEven  = {
        new Vector3Int(1, 0, 0),    // 0: Восток
        new Vector3Int(0, -1, 0),   // 1: Юго-Восток
        new Vector3Int(-1, -1, 0),  // 2: Юго-Запад
        new Vector3Int(-1, 0, 0),   // 3: Запад
        new Vector3Int(-1, 1, 0),   // 4: Северо-Запад
        new Vector3Int(0, 1, 0)     // 5: Северо-Восток
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

    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        return Tilemap.WorldToCell(worldPosition);
    }

    public Vector3 CellToWorld(Vector3Int cellPosition)
    {
        return Tilemap.GetCellCenterWorld(cellPosition);
    }

    public bool IsCellWalkable(Vector3Int cellPosition)
    {
        if (!Tilemap.HasTile(cellPosition))
        {
            return false;
        }

        if (cellToShipMap.ContainsKey(cellPosition))
        {
            return false;
        }

        return true;
    }

    // === МЕТОДЫ ДЛЯ РАБОТЫ С КОРАБЛЯМИ ===

    public void PlaceShip(Ship ship, Vector3Int cellPosition)
    {
        if (cellToShipMap.ContainsKey(cellPosition))
        {
            Debug.LogWarning($"Клетка {cellPosition} уже занята другим кораблем!");
            return;
        }
        cellToShipMap[cellPosition] = ship;
    }

    public void RemoveShip(Vector3Int cellPosition)
    {
        cellToShipMap.Remove(cellPosition);
    }

    public void MoveShip(Ship ship, Vector3Int fromCell, Vector3Int toCell)
    {
        if (cellToShipMap.ContainsKey(fromCell) && cellToShipMap[fromCell] == ship)
        {
            RemoveShip(fromCell);
        }
        PlaceShip(ship, toCell);
    }

    public Ship GetShipInCell(Vector3Int cellPosition)
    {
        cellToShipMap.TryGetValue(cellPosition, out Ship ship);
        return ship;
    }

    // === ИСПРАВЛЕННЫЕ МЕТОДЫ ДЛЯ НАВИГАЦИИ ===

    // Получить соседнюю клетку в заданном направлении (0-5)
    public Vector3Int GetNeighborCell(Vector3Int cellPosition, int direction)
    {
        direction = (direction % 6 + 6) % 6; // Нормализуем направление в диапазон 0-5
        
        // Определяем четность строки для правильного смещения
        Vector3Int[] directions = (cellPosition.y % 2 == 0) ? DirectionsEven : DirectionsOdd;
        
        return cellPosition + directions[direction];
    }

    // Рассчитать расстояние между двумя клетками в гексах
    public int GetDistance(Vector3Int fromCell, Vector3Int toCell)
    {
        // Конвертируем offset coordinates в axial coordinates
        Vector3Int axialFrom = OffsetToAxial(fromCell);
        Vector3Int axialTo = OffsetToAxial(toCell);
        
        Vector3Int diff = axialTo - axialFrom;
        return (Mathf.Abs(diff.x) + Mathf.Abs(diff.y) + Mathf.Abs(diff.x + diff.y)) / 2;
    }

    // Конвертация offset coordinates в axial coordinates
    private Vector3Int OffsetToAxial(Vector3Int offset)
    {
        int q = offset.x;
        int r = offset.y - (offset.x + (offset.x & 1)) / 2;
        return new Vector3Int(q, r, 0);
    }

    // Найти все клетки в пределах радиуса
    public List<Vector3Int> GetCellsInRange(Vector3Int centerCell, int range)
    {
        List<Vector3Int> results = new List<Vector3Int>();

        for (int q = -range; q <= range; q++)
        {
            int r1 = Mathf.Max(-range, -q - range);
            int r2 = Mathf.Min(range, -q + range);
            for (int r = r1; r <= r2; r++)
            {
                Vector3Int axialCell = new Vector3Int(q, r, 0);
                Vector3Int offsetCell = AxialToOffset(axialCell, centerCell);
                
                if (Tilemap.HasTile(offsetCell))
                {
                    results.Add(offsetCell);
                }
            }
        }

        return results;
    }

    // Конвертация axial coordinates в offset coordinates
    private Vector3Int AxialToOffset(Vector3Int axial, Vector3Int center)
    {
        Vector3Int centerAxial = OffsetToAxial(center);
        Vector3Int targetAxial = centerAxial + axial;
        
        int x = targetAxial.x;
        int y = targetAxial.y + (targetAxial.x + (targetAxial.x & 1)) / 2;
        
        return new Vector3Int(x, y, 0);
    }

    // === ИСПРАВЛЕННЫЙ МЕТОД ДЛЯ ПОЛУЧЕНИЯ "БОКОВЫХ" КЛЕТОК ===
    public List<Vector3Int> GetSideCellsInRange(Vector3Int centerCell, int shipDirection, int range)
    {
        List<Vector3Int> sideCells = new List<Vector3Int>();
        
        // Боковые направления относительно текущего направления корабля
        // Для direction = 0 (Восток) это будут направления: 1, 2, 4, 5
        int[] sideDirections = {
            (shipDirection + 1) % 6,
            (shipDirection + 2) % 6,
            (shipDirection + 4) % 6,
            (shipDirection + 5) % 6
        };

        foreach (int dir in sideDirections)
        {
            Vector3Int currentCell = centerCell;
            for (int i = 1; i <= range; i++)
            {
                currentCell = GetNeighborCell(currentCell, dir);
                if (!Tilemap.HasTile(currentCell)) break;
                sideCells.Add(currentCell);
            }
        }
        
        return sideCells;
    }

    // === ДОПОЛНИТЕЛЬНЫЙ МЕТОД ДЛЯ ОТЛАДКИ ===
    public void DebugNeighbors(Vector3Int cellPosition)
    {
        Debug.Log($"Клетка {cellPosition} (y четный: {cellPosition.y % 2 == 0})");
        for (int i = 0; i < 6; i++)
        {
            Vector3Int neighbor = GetNeighborCell(cellPosition, i);
            Debug.Log($"Направление {i}: {neighbor}");
        }
    }
}