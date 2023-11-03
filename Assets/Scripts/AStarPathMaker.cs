using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AStarPathmaker : MonoBehaviour
{
    private BiomeType[,] grid;
    private int gridSizeX, gridSizeY;

    public AStarPathmaker(BiomeType[,] grid)
    {
        this.grid = grid;
        gridSizeX = grid.GetLength(1);
        gridSizeY = grid.GetLength(0);
    }

    public List<Vector3> FindPath(Vector3 start, Vector3 target)
    {
        // Use Vector3 for start and target
        PriorityQueue<Vector3> openSet = new PriorityQueue<Vector3>();
        HashSet<Vector3> closedSet = new HashSet<Vector3>();
        Dictionary<Vector3, Vector3> cameFrom = new Dictionary<Vector3, Vector3>();
        Dictionary<Vector3, float> gScore = new Dictionary<Vector3, float>();
        Dictionary<Vector3, float> fScore = new Dictionary<Vector3, float>();

        openSet.Enqueue(start, 0);
        gScore[start] = 0;
        fScore[start] = Heuristic(start, target);
        Debug.Log("openSet: " + openSet.Count);

        while (!openSet.IsEmpty)
        {
            Vector3 current = openSet.Dequeue();

            if (current == target)
                return ReconstructPath(cameFrom, current);

            closedSet.Add(current);

            foreach (Vector3 neighbor in GetNeighbors(current))
            {
                if (closedSet.Contains(neighbor))
                    continue;

                float tentativeGScore = gScore[current] + Distance(current, neighbor);

                if (!openSet.Contains(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, target);

                    if (!openSet.Contains(neighbor))
                        openSet.Enqueue(neighbor, fScore[neighbor]);
                }
            }
        }
        Debug.Log("returning empty path");       
        return new List<Vector3>();
    }

    private float Heuristic(Vector3 a, Vector3 b)
    {
        // Calculate distance using Vector3.Distance
        return Vector3.Distance(a, b);
    }

    private float Distance(Vector3 a, Vector3 b)
    {
        float distance = 1.0f;

        if (grid[(int)b.y, (int)b.x] == BiomeType.River)
        {
            distance = 0.0f;
        }

        return distance;
    }

    private List<Vector3> GetNeighbors(Vector3 position)
    {
        List<Vector3> neighbors = new List<Vector3>();    

        Vector3[] possibleOffsets =
        {
            new Vector3(0, 0, 1),  // Forward
            new Vector3(0, 0, -1), // Backward
            new Vector3(-1, 0, 0), // Left
            new Vector3(1, 0, 0)   // Right
        };

        foreach (Vector3 offset in possibleOffsets)
        {
            Vector3 neighborPosition = position + offset;

            if (IsWithinGridBounds(neighborPosition) && IsWalkable(neighborPosition))
            {
                neighbors.Add(neighborPosition);
            }
        }

        return neighbors;
    }

    private bool IsWithinGridBounds(Vector3 position)
    {
        int x = (int)position.x;
        int y = (int)position.y;

        return x >= 0 && x < gridSizeX && y >= 0 && y < gridSizeY;
    }

    private bool IsWalkable(Vector3 position)
    {
        int x = (int)position.x;
        int y = (int)position.y;

        if (x < 0 || x >= gridSizeX || y < 0 || y >= gridSizeY)
        {
            return false;
        }

        return true;
    }

    private List<Vector3> ReconstructPath(Dictionary<Vector3, Vector3> cameFrom, Vector3 current)
    {
        List<Vector3> path = new List<Vector3>();

        while (cameFrom.ContainsKey(current))
        {
            path.Insert(0, current);
            current = cameFrom[current];
        }

        path.Insert(0, current);

        return path;
    }
}

public class PriorityQueue<T>
{
    private List<Tuple<T, float>> elements = new List<Tuple<T, float>>();

    public int Count { get { return elements.Count; } }

    public bool IsEmpty { get { return elements.Count == 0; } }

    public void Enqueue(T item, float priority)
    {
        elements.Add(new Tuple<T, float>(item, priority));
        int index = elements.Count - 1;

        while (index > 0)
        {
            int parentIndex = (index - 1) / 2;

            if (elements[index].Item2 >= elements[parentIndex].Item2)
                break;

            Swap(index, parentIndex);
            index = parentIndex;
        }
    }

    public T Dequeue()
    {
        if (IsEmpty)
            throw new InvalidOperationException("Queue is empty.");

        T frontItem = elements[0].Item1;
        elements[0] = elements[elements.Count - 1];
        elements.RemoveAt(elements.Count - 1);

        int index = 0;

        while (true)
        {
            int leftChildIndex = 2 * index + 1;
            int rightChildIndex = 2 * index + 2;
            int smallestIndex = index;

            if (leftChildIndex < elements.Count && elements[leftChildIndex].Item2 < elements[smallestIndex].Item2)
                smallestIndex = leftChildIndex;

            if (rightChildIndex < elements.Count && elements[rightChildIndex].Item2 < elements[smallestIndex].Item2)
                smallestIndex = rightChildIndex;

            if (smallestIndex == index)
                break;

            Swap(index, smallestIndex);
            index = smallestIndex;
        }

        return frontItem;
    }

    private void Swap(int a, int b)
    {
        Tuple<T, float> temp = elements[a];
        elements[a] = elements[b];
        elements[b] = temp;
    }

    public bool Contains(T item)
    {
        return elements.Any(t => t.Item1.Equals(item));
    }
}
