using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class DiscSampling
{
    public static List<Vector2> GeneratePoints(LandscapeGenerator.BiomeType[,] biomeMap, float radius, Vector2 sampleRegionSize, int numSamplesBR = 30)
    {
        float cellSize = radius / Mathf.Sqrt(2);
        int[,] grid = new int[Mathf.CeilToInt(sampleRegionSize.x / cellSize), Mathf.CeilToInt(sampleRegionSize.y / cellSize)];
        List<Vector2> points = new List<Vector2>();
        List<Vector2> spawnPoints = new List<Vector2>();

        spawnPoints.Add(sampleRegionSize / 2);

        while (spawnPoints.Count > 0)
        {
            int spawnIndex = Random.Range(0, spawnPoints.Count);
            Vector2 spawnCentre = spawnPoints[spawnIndex];
            bool candidateAccepted = false;

            for (int i = 0; i < numSamplesBR; i++)
            {
                float angle = Random.value * Mathf.PI * 2;
                Vector2 dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                Vector2 candidatePoint = spawnCentre + dir * Random.Range(radius, 2 * radius);
                if (IsValid(candidatePoint, sampleRegionSize, cellSize, radius, points, grid, biomeMap))
                {
                    points.Add(candidatePoint);
                    spawnPoints.Add(candidatePoint);
                    grid[(int)(candidatePoint.x / cellSize), (int)(candidatePoint.y / cellSize)] = points.Count;
                    candidateAccepted = true;
                    break;
                }
            }
            if (!candidateAccepted)
            {
                spawnPoints.RemoveAt(spawnIndex);
            }
        }
        return points;
    }
    static bool IsValid(Vector2 candidate, Vector2 sampleRegionSize, float cellSize, float radius, List<Vector2> points, int[,] grid, LandscapeGenerator.BiomeType[,] biomeMap)
    {
        Debug.Log("Checking if Valid");
        if (candidate.x >= 0 && candidate.x < sampleRegionSize.x && candidate.y >= 0 && candidate.y < sampleRegionSize.y)
        {
            int cellX = (int)(candidate.x / cellSize);
            int cellY = (int)(candidate.y / cellSize);
            int searchStartX = Mathf.Max(0, cellX - 2);
            int searchEndX = Mathf.Min(cellX + 2, grid.GetLength(0) - 1);
            int searchStartY = Mathf.Max(0, cellY - 2);
            int searchEndY = Mathf.Min(cellY + 2, grid.GetLength(1) - 1);

            for (int x = searchStartX; x < searchEndX; x++)
            {
                for (int y = searchStartY; y < searchEndY; y++)
                {
                    int pointIndex = grid[x, y] - 1;
                    if (pointIndex != -1)
                    {
                        float dstSqr = (candidate - points[pointIndex]).sqrMagnitude;
                        // Check if the point is within the forest biome
                        int landscapeX = Mathf.FloorToInt(candidate.x);
                        int landscapeY = Mathf.FloorToInt(candidate.y);
                        if (biomeMap[landscapeX, landscapeY] != LandscapeGenerator.BiomeType.Forest)
                        {
                            Debug.Log("Not in Forest");
                            return false; // Not in the forest biome
                        }
                        if (dstSqr < radius * radius)
                        {
                            Debug.Log("Too Close");
                            return false;
                        }
                    }
                }
            }
            Debug.Log("success");
            return true;
        }
        Debug.Log("Out of Bounds");
        return false;
    }
}
