using System;
using System.Collections.Generic;
using System.Linq;

namespace Fub.Implementations.Map;

/// <summary>
/// A* pathfinding implementation for grid-based maps
/// </summary>
public class Pathfinder
{
    private readonly Func<int, int, bool> _isWalkable;
    private readonly int _mapWidth;
    private readonly int _mapHeight;

    public Pathfinder(int mapWidth, int mapHeight, Func<int, int, bool> isWalkable)
    {
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _isWalkable = isWalkable ?? throw new ArgumentNullException(nameof(isWalkable));
    }

    /// <summary>
    /// Finds a path from start to goal using A* algorithm
    /// </summary>
    public List<(int x, int y)>? FindPath(int startX, int startY, int goalX, int goalY, int maxDistance = 100)
    {
        if (!IsValid(startX, startY) || !IsValid(goalX, goalY))
            return null;

        if (!_isWalkable(goalX, goalY))
            return null;

        var openSet = new PriorityQueue<Node, float>();
        var closedSet = new HashSet<(int, int)>();
        var cameFrom = new Dictionary<(int, int), (int, int)>();
        var gScore = new Dictionary<(int, int), float>();
        var fScore = new Dictionary<(int, int), float>();

        var start = (startX, startY);
        var goal = (goalX, goalY);

        gScore[start] = 0;
        fScore[start] = Heuristic(startX, startY, goalX, goalY);
        openSet.Enqueue(new Node(startX, startY), fScore[start]);

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            var currentPos = (current.X, current.Y);

            if (currentPos == goal)
            {
                return ReconstructPath(cameFrom, currentPos);
            }

            closedSet.Add(currentPos);

            // Check all 4 cardinal directions
            var neighbors = GetNeighbors(current.X, current.Y);
            foreach (var neighbor in neighbors)
            {
                if (closedSet.Contains(neighbor))
                    continue;

                var tentativeGScore = gScore[currentPos] + 1;

                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = currentPos;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + Heuristic(neighbor.Item1, neighbor.Item2, goalX, goalY);

                    // Prevent paths that are too long
                    if (gScore[neighbor] > maxDistance)
                        continue;

                    openSet.Enqueue(new Node(neighbor.Item1, neighbor.Item2), fScore[neighbor]);
                }
            }
        }

        return null; // No path found
    }

    /// <summary>
    /// Gets a single step towards the goal (useful for real-time movement)
    /// </summary>
    public (int x, int y)? GetNextStep(int startX, int startY, int goalX, int goalY)
    {
        var path = FindPath(startX, startY, goalX, goalY);
        if (path == null || path.Count < 2)
            return null;

        return path[1]; // Return first step (index 0 is current position)
    }

    private List<(int x, int y)> ReconstructPath(Dictionary<(int, int), (int, int)> cameFrom, (int, int) current)
    {
        var path = new List<(int x, int y)> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }

    private List<(int, int)> GetNeighbors(int x, int y)
    {
        var neighbors = new List<(int, int)>();
        var directions = new[] { (0, -1), (1, 0), (0, 1), (-1, 0) }; // Up, Right, Down, Left

        foreach (var (dx, dy) in directions)
        {
            var nx = x + dx;
            var ny = y + dy;
            if (IsValid(nx, ny) && _isWalkable(nx, ny))
            {
                neighbors.Add((nx, ny));
            }
        }

        return neighbors;
    }

    private float Heuristic(int x1, int y1, int x2, int y2)
    {
        // Manhattan distance for grid-based movement
        return Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
    }

    private bool IsValid(int x, int y)
    {
        return x >= 0 && x < _mapWidth && y >= 0 && y < _mapHeight;
    }

    private class Node
    {
        public int X { get; }
        public int Y { get; }

        public Node(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}

