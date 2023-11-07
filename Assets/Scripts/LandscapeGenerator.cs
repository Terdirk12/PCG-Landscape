using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LandscapeGenerator : MonoBehaviour
{
    private Vector3[] vertices;
    private Mesh mesh;
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
    public MeshCollider collider;
#pragma warning restore CS0108 // Member hides inherited member; missing new keyword
    private float baseHeight = 0;
    public Gradient gradient;
    public GameObject pinetreePrefab, lushtreePrefab, bushPrefab; // Reference to your tree prefab
    public LayerMask terrain;
    private int mountainCount;
    private List<Vector3> riverStartPoints = new List<Vector3>();
    // Create an empty set for visited points
    HashSet<Vector3> visitedPoints = new HashSet<Vector3>();
    bool reachedOcean = false; // A flag to track if the river has reached an ocean
    private BiomeType[,] biomeMap; // Define a 2D biome map
    private float plainsScale = 2f, heightPlainsScale = 0, smoothingStrenght = 2f, transitionRange = 5.0f, persistence = 0.5f, lacunarity = 8f, mountainScale = 6;
    private int octaves = 10, neighbors = 5; // Number of octaves in the fractal noise        

    [Range(50, 150)]
    public int xSize, zSize;
    [Range(0.15f, 0.2f)]
    public float givenRadius = 0.15f;
    [Range(0, 3)]
    public int riverStartWidth = 1;
    [Range(0.2f, 0.4f)]
    public float treeDensity = 0.3f; // Adjust the density of trees in the forest biome
    [Range(1, 5)]
    public float minCurveAmt, maxCurveAmt, maxAngleModify;

    public enum BiomeType
    {
        Plains,
        Forest,
        Mountains,
        ShallowOcean,
        DeepOcean,
        River,
        Beach,
        Lowlands
    }

    Dictionary<BiomeType, float> terrainMovementCosts = new Dictionary<BiomeType, float>
{
    { BiomeType.Beach, 0.0f },   // Low cost for beach
    { BiomeType.ShallowOcean, 0.0f },   // Low cost for ocean
    { BiomeType.DeepOcean, 0.0f },   // Low cost for ocean
    { BiomeType.River, 0.0f },   // Low cost for river
    { BiomeType.Plains, 1.0f },  // Default cost for plains
    { BiomeType.Lowlands, 1.0f }, // Default cost for lowlands
    { BiomeType.Forest, 1.5f },  // Higher cost for forest
    { BiomeType.Mountains, 2.0f } // Even higher cost for mountains
};



    private void Awake()
    {
        InitializeBiomeMap(); // Initialize the biome map  
    }

    #region biomes

    private void InitializeBiomeMap()
    {
        biomeMap = new BiomeType[xSize + 1, zSize + 1];
        int xOffset = UnityEngine.Random.Range(0, 10000);
        int zOffset = UnityEngine.Random.Range(0, 10000);

        // Initialize the biome map using multiple Perlin noise layers
        for (int x = 0; x <= xSize; x++)
        {
            for (int z = 0; z <= zSize; z++)
            {
                // Scale the coordinates for each biome type
                float plainsXCoord = (float)x / xSize * plainsScale + xOffset;
                float plainsZCoord = (float)z / zSize * plainsScale + zOffset;

                float forestXCoord = (float)x / xSize + xOffset;
                float forestZCoord = (float)z / zSize + zOffset;

                float mountainsXCoord = (float)x / xSize * mountainScale + xOffset;
                float mountainsZCoord = (float)z / zSize * mountainScale + zOffset;


                // Generate Perlin noise values for each biome type
                float plainsNoise = Mathf.PerlinNoise(plainsXCoord, plainsZCoord);
                float forestNoise = Mathf.PerlinNoise(forestXCoord, forestZCoord);
                float mountainsNoise = Mathf.PerlinNoise(mountainsXCoord, mountainsZCoord);

                // You can adjust these thresholds to determine the biome distribution
                if (plainsNoise < 0.4f)
                {
                    biomeMap[x, z] = BiomeType.DeepOcean;
                }
                else if (plainsNoise < 0.6f)
                {
                    biomeMap[x, z] = BiomeType.Plains;
                }
                else if (forestNoise < 0.4f)
                {
                    biomeMap[x, z] = BiomeType.Forest;
                }
                else if (mountainsNoise < 0.5f)
                {
                    biomeMap[x, z] = BiomeType.Mountains;
                    mountainCount++;
                }
                else
                {
                    biomeMap[x, z] = BiomeType.Plains; // Default to Plains if none of the conditions match

                }
            }
        }
        MakeBiomeRings();
    }

    private void MakeBiomeRings()
    {
        for (int x = 0; x <= xSize; x++)
        {
            for (int z = 0; z <= zSize; z++)
            {
                if (biomeMap[x, z] != BiomeType.Mountains)
                {
                    if (IsNearBiome(x, z, BiomeType.Mountains, 3)) biomeMap[x, z] = BiomeType.Forest;
                }
                if (biomeMap[x, z] != BiomeType.DeepOcean)
                {
                    if (IsNearBiome(x, z, BiomeType.DeepOcean, 3)) biomeMap[x, z] = BiomeType.Beach;
                }
                if (biomeMap[x, z] != BiomeType.Beach)
                {
                    if (IsNearBiome(x, z, BiomeType.Beach, 3) && IsNearBiome(x, z, BiomeType.DeepOcean, 3)) biomeMap[x, z] = BiomeType.ShallowOcean;
                }

            }
        }
        GenerateTerrain();
    }

    private void MakeRiverBanks()
    {
        for (int x = 0; x <= xSize; x++)
        {
            for (int z = 0; z <= zSize; z++)
            {
                if (biomeMap[x, z] != BiomeType.DeepOcean && biomeMap[x, z] != BiomeType.ShallowOcean && biomeMap[x, z] != BiomeType.River)
                {
                    if (biomeMap[x, z] == BiomeType.Mountains)
                    {
                        if (IsNearBiome(x, z, BiomeType.River, 3))
                        {
                            biomeMap[x, z] = BiomeType.Lowlands;
                            vertices[z * (xSize + 1) + x].y = GenerateLowlandsTerrain(x, z);
                        }
                    }
                    if (biomeMap[x, z] == BiomeType.Forest)
                    {
                        if (IsNearBiome(x, z, BiomeType.River, 1))
                        {
                            biomeMap[x, z] = BiomeType.Lowlands;
                            vertices[z * (xSize + 1) + x].y = GenerateLowlandsTerrain(x, z);
                        }
                    }
                }
            }
        }
        RecalcTerrain();
    }

    private void GenerateTerrain()
    {
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Procedural Grid";

        vertices = new Vector3[(xSize + 1) * (zSize + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        Vector4[] tangents = new Vector4[vertices.Length];
        Vector4 tangent = new Vector4(1f, 0f, 0f, -1f);

        // Generate terrain based on the biome
        float height = 0f;
        for (int i = 0, z = 0; z <= zSize; z++)
        {
            for (int x = 0; x <= xSize; x++, i++)
            {
                // Get the biome type for this point
                BiomeType biome = biomeMap[x, z];

                switch (biome)
                {
                    case BiomeType.Plains:
                        // Generate Plains terrain (you can define this function)
                        height = GeneratePlainsTerrain(x, z);
                        break;
                    case BiomeType.Lowlands:
                        // Generate Plains terrain (you can define this function)
                        height = GenerateLowlandsTerrain(x, z);
                        break;
                    case BiomeType.Forest:
                        // Generate Forest terrain
                        height = GenerateForestTerrain(x, z);
                        break;
                    case BiomeType.Mountains:
                        // Generate Mountainous terrain
                        height = GenerateMountainousTerrain(x, z);
                        break;
                    case BiomeType.DeepOcean:
                        // Generate Ocean terrain
                        height = GenerateDeepOceanTerrain(x, z);
                        break;
                    case BiomeType.ShallowOcean:
                        // Generate Ocean terrain
                        height = GenerateShallowOceanTerrain(x, z);
                        break;
                    case BiomeType.Beach:
                        // Generate Beach terrain
                        height = GenerateBeachTerrain(x, z);
                        break;
                }

                vertices[i] = new Vector3(x, height, z);
                uv[i] = new Vector2((float)x / xSize, (float)z / zSize);
                tangents[i] = tangent;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.tangents = tangents;

        int[] triangles = new int[xSize * zSize * 6];
        for (int tri = 0, ver = 0, z = 0; z < zSize; z++, ver++)
        {
            for (int x = 0; x < xSize; x++, tri += 6, ver++)
            {
                triangles[tri] = ver;
                triangles[tri + 3] = triangles[tri + 2] = ver + 1;
                triangles[tri + 4] = triangles[tri + 1] = ver + xSize + 1;
                triangles[tri + 5] = ver + xSize + 2;

                mesh.triangles = triangles;
            }
        }
        SmoothTerrainTransition(); // Color the terrain after it has been generated.
    }

    // Function to check if a point is near mountains
    private bool IsNearBiome(int x, int z, BiomeType locationBiome, float range)
    {
        // Check the biome type for the specified point
        BiomeType biome = biomeMap[x, z];

        // If the biome is Mountains or within the specified range of Mountains, return true
        if (biome == locationBiome)
        {
            return true;
        }

        // Iterate through the entire terrain and find the closest mountain
        float closestBiomeDistance = float.MaxValue;

        for (int mx = 0; mx <= xSize; mx++)
        {
            for (int mz = 0; mz <= zSize; mz++)
            {
                if (biomeMap[mx, mz] == locationBiome)
                {
                    // Calculate the distance between the current point and the mountain
                    float distance = Vector2.Distance(new Vector2(x, z), new Vector2(mx, mz));

                    if (distance < closestBiomeDistance)
                    {
                        closestBiomeDistance = distance;
                    }
                }
            }
        }

        return closestBiomeDistance <= range;
    }

    #endregion

    #region Plains, Mountain, Ocean & Beach
    private float GeneratePlainsTerrain(int x, int z)
    {
        // Scale the coordinates to control the feature size
        float xCoord = (float)x / xSize * heightPlainsScale;
        float zCoord = (float)z / zSize * heightPlainsScale;

        // Use Perlin noise to generate terrain
        // Apply scaling and offset to the height
        float plainsHeight = baseHeight + Mathf.PerlinNoise(xCoord, zCoord);

        return plainsHeight;
    }
    private float GenerateLowlandsTerrain(int x, int z)
    {
        // Scale the coordinates to control the feature size
        float xCoord = (float)x / xSize * heightPlainsScale;
        float zCoord = (float)z / zSize * heightPlainsScale;

        // Use Perlin noise to generate terrain
        // Apply scaling and offset to the height
        float plainsHeight = (baseHeight - 0.35f) + Mathf.PerlinNoise(xCoord, zCoord);

        return plainsHeight;
    }

    private float GenerateMountainousTerrain(int x, int z)
    {
        // Calculate the position in the noise field
        float sampleX = (float)x / xSize * 12;
        float sampleZ = (float)z / zSize * 12;

        float height = baseHeight;
        float amplitude = 1;
        float frequency = 1;

        for (int i = 0; i < octaves; i++)
        {
            amplitude = Mathf.Pow(persistence, i);
            frequency *= lacunarity;

            float noiseValue = Mathf.PerlinNoise(sampleX, sampleZ);

            // Calculate ridged multifractal noise
            float ridgeValue = Mathf.Abs(2 * noiseValue - 1);

            // Apply the ridgeValue to accentuate peaks
            height += ridgeValue + noiseValue * amplitude;
        }
        return height + 2;
    }

    private float GenerateDeepOceanTerrain(int x, int z)
    {
        float oceanHeight = -1;
        return oceanHeight;
    }

    private float GenerateShallowOceanTerrain(int x, int z)
    {
        float oceanHeight = -0.25f;
        return oceanHeight;
    }

    private float GenerateBeachTerrain(int x, int z)
    {
        float beachHeight = 0.15f;
        return beachHeight;
    }

    #endregion

    #region Trees & Forest

    private void GenerateTrees()
    {
        // Create an empty GameObject to serve as the container for the trees
        GameObject treeContainer = new GameObject("TreeContainer");

        // Generate Poisson disc samples for the forest biome at this specific point
        List<Vector2> treePositions = DiscSampling.GeneratePoints(givenRadius, new Vector2(xSize, zSize), Mathf.FloorToInt(treeDensity * 10)); // Adjust the density

        // Iterate through the tree placement points and instantiate trees
        foreach (Vector2 position in treePositions)
        {
            // Get the biome type at the tree's grid position
            BiomeType biome = biomeMap[Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y)];

            if (biome == BiomeType.Forest)
            {
                // Count the number of forest neighbors
                int forestNeighbors = CountForestNeighbors(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));

                // Calculate the tree density based on the number of forest neighbors
                float density = Mathf.Lerp(0.25f, 1.0f, forestNeighbors / 8.0f); // Adjust these values as needed


                if (UnityEngine.Random.value < density)
                {
                    // Use the height at the tree's grid position
                    //float treeHeight = vertices[Mathf.FloorToInt(position.y) * (xSize + 1) + Mathf.FloorToInt(position.x)].y;
                    float treeHeight = SampleTerrainHeight(position);
                    GameObject treeObject; // The instantiated tree object
                    if (treeHeight > 1f)
                    {
                        if (treeHeight < 6.5f)
                        {
                            if (treeHeight > 3.5f)
                            {
                                treeHeight += UnityEngine.Random.Range(-0.05f, 0.05f);
                                treeObject = Instantiate(pinetreePrefab, new Vector3(position.x, treeHeight, position.y), Quaternion.identity);
                            }
                            else if (treeHeight > 3f)
                            {
                                float R = UnityEngine.Random.Range(0, 2);
                                treeHeight += UnityEngine.Random.Range(-0.05f, 0.05f);
                                if (R == 1) treeObject = Instantiate(lushtreePrefab, new Vector3(position.x, treeHeight, position.y), Quaternion.identity);
                                else treeObject = Instantiate(pinetreePrefab, new Vector3(position.x, treeHeight, position.y), Quaternion.identity);
                            }
                            else
                            {
                                treeHeight += UnityEngine.Random.Range(-0.05f, 0.05f);
                                treeObject = Instantiate(lushtreePrefab, new Vector3(position.x, treeHeight, position.y), Quaternion.identity);
                            }

                            // Set the tree object as a child of the treeContainer
                            treeObject.transform.parent = treeContainer.transform;
                        }
                        else
                        {
                            float bushHeight = SampleTerrainHeight(position);
                            GameObject bushObject; // The instantiated tree object
                            if (bushHeight < 3.5f)
                            {

                                
                                treeHeight = SampleTerrainHeight(position);
                                bushObject = Instantiate(bushPrefab, new Vector3(position.x, treeHeight, position.y), Quaternion.identity);

                                // Set the bush object as a child of the treeContainer
                                bushObject.transform.parent = treeContainer.transform;
                            }
                        }
                    }
                }
            }
        }
    }

    private float SampleTerrainHeight(Vector2 position)
    {
        Vector3 placementPoint = new Vector3(position.x, 0, position.y);

        // Trilinear interpolation
        float height = 0f;

        RaycastHit hit;
        if (Physics.Raycast(placementPoint + Vector3.up * 100, Vector3.down, out hit, Mathf.Infinity, terrain))
        {
            height = hit.point.y;
        }

        return height;
    }

    private int CountForestNeighbors(int x, int z)
    {
        int forestCount = 0;

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int neighborX = x + dx;
                int neighborZ = z + dz;

                // Ensure the neighbor is within bounds
                if (IsWithinBounds(neighborX, neighborZ))
                {
                    if (biomeMap[neighborX, neighborZ] == BiomeType.Forest)
                    {
                        forestCount++;
                    }
                }
            }
        }

        return forestCount;
    }

    private float GenerateForestTerrain(int x, int z)
    {
        float forestHeight;
        // Scale the coordinates to control the feature size
        float xCoord = (float)x / xSize * plainsScale;
        float zCoord = (float)z / zSize * plainsScale;

        // Use Perlin noise to generate terrain
        forestHeight = baseHeight + 1 + Mathf.PerlinNoise(xCoord, zCoord);
        return forestHeight;
    }

    #endregion

    #region riverGeneration
    private void GenerateRiver()
    {
        // Generate the path of the river using an algorithm
        List<Vector3> riverPath = GenerateRiverPath();

        // Create a river mesh along the river path
        GenerateRiverMesh(riverPath);

        //make river banks
        MakeRiverBanks();
    }

    private List<Vector3> GenerateRiverPath()
    {
        reachedOcean = false;
        List<Vector3> riverPath = new List<Vector3>();

        // Find a starting point for the river
        Vector3 startPoint = FindRiverStartPoint();

        // Add the new river start point to the list
        riverStartPoints.Add(startPoint);

        float curveAmt = UnityEngine.Random.Range(minCurveAmt, maxCurveAmt);
        float distance = Vector3.Distance(startPoint, FindNearestBiomePoint(startPoint, BiomeType.DeepOcean));
        float step = distance / curveAmt * MathF.PI * 2;

        // Sample points along the river path
        int numPoints = 100; // Adjust as needed

        for (int i = 0; i < numPoints; i++)
        {
            // Check if the river has reached an ocean
            if (reachedOcean)
            {
                break; // Exit the loop if the river has reached an ocean
            }

            float angleModifier = MathF.Sin(step * i);
            float angleModification = angleModifier * (maxAngleModify * 1);

            // Calculate the next point based on the current position and direction
            Vector3 currentPoint = riverPath.Count > 0 ? riverPath[riverPath.Count - 1] : startPoint;
            Vector3 nextPoint = FindNextPoint(currentPoint, visitedPoints, angleModification);

            int xIndex = Mathf.FloorToInt(nextPoint.x);
            int zIndex = Mathf.FloorToInt(nextPoint.z);

            if (!IsWithinBounds(xIndex, zIndex))
            {
                continue;
            }

            if (biomeMap[xIndex, zIndex] == BiomeType.DeepOcean)
            {
                reachedOcean = true; // Set the flag to true
            }
            riverPath.Add(nextPoint);
        }
        // Clear the visited points set for the next river
        visitedPoints.Clear();
        return riverPath;

    }
    private void GenerateRiverMesh(List<Vector3> riverPath)
    {
        // Modify the terrain height along the river path
        AdjustTerrainHeight(riverPath);
    }
    private void AdjustTerrainHeight(List<Vector3> riverPath)
    {
        foreach (Vector3 riverPoint in riverPath)
        {
            int xIndex = Mathf.FloorToInt(riverPoint.x);
            int zIndex = Mathf.FloorToInt(riverPoint.z);

            if (IsWithinBounds(xIndex, zIndex))
            {
                vertices[zIndex * (xSize + 1) + xIndex].y = -1f;
                // Expand the river to its neighbors based on river width
                for (int i = -riverStartWidth; i <= riverStartWidth; i++)
                {
                    int neighborX = xIndex + i;
                    int neighborZ = zIndex;

                    if (IsWithinBounds(neighborX, neighborZ))
                    {
                        // Lower the terrain at the neighbor point
                        vertices[neighborZ * (xSize + 1) + neighborX].y = -1;
                        biomeMap[neighborX, neighborZ] = BiomeType.River;
                    }
                }
                biomeMap[xIndex, zIndex] = BiomeType.River;
            }
        }
        // Update the terrain mesh with the modified vertices       
        RecalcTerrain();
    }

    private Vector3 FindRiverStartPoint()
    {
        Vector3 startRiverPoint;

        while (true)
        {
            // Randomly select a point in the mountains
            int x = UnityEngine.Random.Range(0, xSize);
            int z = UnityEngine.Random.Range(0, zSize);

            if (biomeMap[x, z] == BiomeType.Mountains && HasEnoughMountainBiomeNeighbors(x, z) && IsFarFromOtherRiverStarts(x, z))
            {
                startRiverPoint = new Vector3(x, 0, z);
                break;
            }
        }

        return startRiverPoint;
    }
    private bool HasEnoughMountainBiomeNeighbors(int x, int z)
    {
        int requiredMountainNeighbors = 16;

        int mountainNeighbors = 0;

        for (int dz = -2; dz <= 2; dz++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                int neighborX = x + dx;
                int neighborZ = z + dz;

                // Ensure the neighbor is within bounds
                if (IsWithinBounds(neighborX, neighborZ))
                {
                    if (biomeMap[neighborX, neighborZ] == BiomeType.Mountains)
                    {
                        mountainNeighbors++;
                    }
                }
            }
        }

        return mountainNeighbors >= requiredMountainNeighbors;
    }

    private bool IsFarFromOtherRiverStarts(int x, int z)
    {
        float minDistance = 10f; // Minimum distance between river starts

        foreach (var riverStart in riverStartPoints)
        {
            float distance = Vector3.Distance(new Vector3(x, 0, z), riverStart);
            if (distance < minDistance)
            {
                return false;
            }
        }

        return true;
    }

    private Vector3 FindNearestBiomePoint(Vector3 startPoint, BiomeType biome)
    {
        Vector3 nearestPoint = Vector3.zero;
        float nearestDistance = float.MaxValue;

        for (int x = 0; x <= xSize; x++)
        {
            for (int z = 0; z <= zSize; z++)
            {
                if (biomeMap[x, z] == biome)
                {
                    Vector3 biomePoint = new Vector3(x, SampleTerrainHeight(new Vector2(x, z)), z);
                    float distance = Vector3.Distance(startPoint, biomePoint);

                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestPoint = biomePoint;
                    }
                }
            }
        }

        return nearestPoint;
    }

    Vector3 AngleConvert(Vector3 direction, float angle)
    {
        Vector3 test = new Vector3(1, 0, 0);

        float a = Mathf.Sqrt(test.x * test.x + test.z + test.z);
        float b = Mathf.Sqrt(direction.x * direction.x + direction.z * direction.z);
        float c = test.x * direction.x + test.z * direction.z;
        float directionAngle = Mathf.Acos(c / (a * b));
        directionAngle += angle;
        direction = new Vector3(Mathf.Cos(directionAngle), 0, Mathf.Sin(directionAngle));

        return direction;
    }

    Vector3 FindNextPoint(Vector3 currentPosition, HashSet<Vector3> visitedPoints, float angle)
    {
        // Find the nearest ocean biome point to the starting point
        Vector3 endOceanPoint = FindNearestBiomePoint(currentPosition, BiomeType.DeepOcean);
        // Find the nearest ocean biome point to the starting point
        Vector3 nearestRiverPoint = FindNearestBiomePoint(currentPosition, BiomeType.River);

        Vector3 direction;

        if (nearestRiverPoint != Vector3.zero)
        {
            // Calculate distances to the ocean and river points
            float distanceToOcean = Vector3.Distance(currentPosition, endOceanPoint);
            float distanceToRiver = Vector3.Distance(currentPosition, nearestRiverPoint);

            if (distanceToRiver < distanceToOcean)
            {
                // Set the initial direction toward the endpoint
                direction = (nearestRiverPoint - currentPosition).normalized;
            }
            else
            {
                // If the ocean is closer or at the same distance, go towards the ocean
                direction = AngleConvert((endOceanPoint - currentPosition).normalized, angle);
            }
        }
        else
        {
            // If there is no nearby river point, go directly towards the ocean
            direction = AngleConvert((endOceanPoint - currentPosition).normalized, angle);
        }


        Vector3 nextPoint = currentPosition + direction;

        // Calculate the cost for the next point based on terrain type and height
        float nextPointCost = terrainMovementCosts[GetLocalBiome(nextPoint)] + CalculateHeightCost(nextPoint);

        // Check neighboring points and select the one with the lowest cost
        foreach (Vector3 neighbor in GetNeighbors(currentPosition))
        {
            float neighborCost = terrainMovementCosts[GetLocalBiome(neighbor)] + CalculateHeightCost(neighbor);

            if (neighborCost < nextPointCost)
            {
                nextPoint = neighbor + direction;
                nextPointCost = neighborCost;
            }
            // Mark the selected point as visited
            visitedPoints.Add(nextPoint);
        }

        return nextPoint;
    }

    float CalculateHeightCost(Vector3 position)
    {
        return SampleTerrainHeight(position);
    }

    List<Vector3> GetNeighbors(Vector3 position)
    {
        List<Vector3> neighbors = new List<Vector3>();

        int x = Mathf.FloorToInt(position.x);
        int z = Mathf.FloorToInt(position.z);

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int neighborX = x + dx;
                int neighborZ = z + dz;

                // Ensure the neighbor is within bounds
                if (IsWithinBounds(neighborX, neighborZ))
                {
                    Vector3 neighbor = new Vector3(neighborX, 0, neighborZ);
                    // Skip the current position itself
                    if (neighbor != position)
                    {
                        // Skip points that have already been visited in this river path
                        if (visitedPoints.Contains(neighbor))
                        {
                            // Add the neighboring point to the list
                            neighbors.Add(new Vector3(neighborX, 0, neighborZ));
                        }

                    }
                }
            }
        }

        return neighbors;
    }

    #endregion

    #region smoothing & coloring
    private void SmoothTerrainTransition()
    {
        if (mountainCount > 100)
        {
            if (mountainCount > 1000) mountainCount = 1000; // make sure we dont get an overload of rivers
            for (int m = 0; m <= mountainCount; m += 100)
            {
                GenerateRiver(); // Generate rivers
            }
        }

        for (int i = 0, z = 0; z <= zSize; z++)
        {
            for (int x = 0; x <= xSize; x++, i++)
            {

                int vertexIndex = z * (xSize + 1) + x;
                Vector3 vertexPosition = vertices[vertexIndex];
                float vertexHeight = vertexPosition.y;

                // Check the neighboring vertices within the transition range
                float totalHeight = vertexHeight;
                int neighborCount = neighbors;

                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int neighborX = x + dx;
                        int neighborZ = z + dz;

                        // Ensure the neighbor is within bounds
                        if (IsWithinBounds(neighborX, neighborZ))
                        {
                            int neighborIndex = neighborZ * (xSize + 1) + neighborX;
                            float neighborHeight = vertices[neighborIndex].y;

                            if (biomeMap[x, z] == BiomeType.Beach)
                            {
                                // Check if the neighbor is within the transition range
                                if (Mathf.Abs(neighborHeight - vertexHeight) <= 1)
                                {
                                    totalHeight += neighborHeight;
                                    neighborCount++;
                                }
                            }
                            else
                            {
                                // Check if the neighbor is within the transition range
                                if (Mathf.Abs(neighborHeight - vertexHeight) <= transitionRange)
                                {
                                    totalHeight += neighborHeight * smoothingStrenght;
                                    neighborCount++;
                                }
                            }


                        }

                    }

                    // Calculate the average height within the transition range
                    float averageHeight = totalHeight / neighborCount;

                    // Update the vertex height
                    vertices[vertexIndex] = new Vector3(vertexPosition.x, averageHeight, vertexPosition.z);
                }
            }
        }

        // Update the mesh with the smoothed heights
        RecalcTerrain();


        ColorTerrain(); // Color the terrain after it has been generated.
        GenerateTrees(); // Generate trees.
    }

    private BiomeType GetLocalBiome(Vector3 pos)
    {
        int xIndex = Mathf.FloorToInt(pos.x);
        int zIndex = Mathf.FloorToInt(pos.z);
        if (IsWithinBounds(xIndex, zIndex)) return biomeMap[xIndex, zIndex];
        return BiomeType.DeepOcean;
    }

    private void ColorTerrain()
    {
        Color[] colors = new Color[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            float colorHeight = Mathf.InverseLerp(-1, 10, vertices[i].y);
            colors[i] = gradient.Evaluate(colorHeight);
        }

        mesh.colors = colors;
    }
    bool IsWithinBounds(int x, int z)
    {
        return x >= 0 && x <= xSize && z >= 0 && z <= zSize;
    }

    private void RecalcTerrain()
    {
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        collider.sharedMesh = mesh;
    }
    #endregion

    private void OnDrawGizmos()
    {
        float sphereSize = 0.1f;

        if (biomeMap == null)
        {
            return;
        }

        Gizmos.color = Color.black;
        for (int i = 0; i < vertices.Length; i++)
        {
            //Handles.Label(vertices[i], $"x: {vertices[i].x}, y: {vertices[i].y}, z: {vertices[i].z}");
            //Gizmos.DrawSphere(vertices[i], sphereSize);
        }

        for (int x = 0; x <= xSize; x++)
        {
            for (int z = 0; z <= zSize; z++)
            {
                Vector3 position = new Vector3(x, 0f, z);
                BiomeType biome = biomeMap[x, z];

                switch (biome)
                {
                    case BiomeType.Plains:
                        Gizmos.color = Color.green;
                        break;
                    case BiomeType.Forest:
                        Gizmos.color = Color.red;
                        break;
                    case BiomeType.Mountains:
                        Gizmos.color = Color.gray;
                        break;
                    case BiomeType.DeepOcean:
                        Gizmos.color = Color.blue;
                        break;
                    case BiomeType.Beach:
                        Gizmos.color = Color.yellow;
                        break;
                    case BiomeType.River:
                        Gizmos.color = Color.cyan;
                        break;
                    default:
                        Gizmos.color = Color.white;
                        break;
                }

                Gizmos.DrawSphere(position, sphereSize);
            }
        }
    }
}
