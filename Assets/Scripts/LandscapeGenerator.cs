using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LandscapeGenerator : MonoBehaviour
{
    public int xSize, zSize;
    private Vector3[] vertices;
    private Mesh mesh;
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
    public MeshCollider collider;
#pragma warning restore CS0108 // Member hides inherited member; missing new keyword
    public float baseHeight;
    public Gradient gradient;
    public GameObject pinetreePrefab, lushtreePrefab, bushPrefab; // Reference to your tree prefab
    public float treeDensity = 0.3f, givenRadius = 0.5f, riverStartWidth = 5f, riverStartDepth = 0.5f; // Adjust the density of trees in the forest biome
    public LayerMask terrain;
    private int mountainCount;
    private List<Vector3> riverStartPoints = new List<Vector3>();
    public Material riverMaterial, terrainMaterial;
    private Vector3[] initialVertices;

    public enum BiomeType
    {
        Plains,
        Forest,
        Mountains,
        Ocean,
        River
    }

    public static BiomeType[,] biomeMap; // Define a 2D biome map

    public float mountainScale = 15.0f, plainsScale = 2f, heightPlainsScale, smoothingStrenght = 1f, transitionRange = 5.0f, persistence = 0.5f, lacunarity = 2.0f;
    public int octaves = 5, neighbors = 3; // Number of octaves in the fractal noise                  

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
                    biomeMap[x, z] = BiomeType.Ocean;
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
                    Debug.Log("i need more mountains");
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
                    if (IsNearBiome(x, z, BiomeType.Mountains)) biomeMap[x, z] = BiomeType.Forest;
                }
            }
        }
        GenerateTerrain();
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
                    case BiomeType.Forest:
                        // Generate Forest terrain
                        height = GenerateForestTerrain(x, z);
                        break;
                    case BiomeType.Mountains:
                        // Generate Mountainous terrain
                        height = GenerateMountainousTerrain(x, z);
                        break;
                    case BiomeType.Ocean:
                        // Generate Mountainous terrain
                        height = GenerateOceanTerrain(x, z);
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
    private bool IsNearBiome(int x, int z, BiomeType locationBiome)
    {
        // Define a range within which a point is considered near mountains
        float Range = 3.0f; // Adjust this range as needed

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

        return closestBiomeDistance <= Range;
    }

    #endregion

    #region Plains, Mountain & Ocean
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

    private float GenerateOceanTerrain(int x, int z)
    {
        int oceanHeight = -1;
        return oceanHeight;
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
                }
                else
                {
                    float bushHeight = SampleTerrainHeight(position);
                    GameObject bushObject; // The instantiated tree object
                    if (bushHeight < 3.5f)
                    {

                        float treeHeight = SampleTerrainHeight(position);
                        bushObject = Instantiate(bushPrefab, new Vector3(position.x, treeHeight, position.y), Quaternion.identity);

                        // Set the bush object as a child of the treeContainer
                        bushObject.transform.parent = treeContainer.transform;
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
                if (neighborX >= 0 && neighborX < xSize && neighborZ >= 0 && neighborZ < zSize)
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
        Vector3 riverStart = FindRiverStartPoint();
        Vector3 riverGoal = FindNearestBiomePoint(riverStart, BiomeType.Ocean);
        // Generate the path of the river using an algorithm
        List<Vector3> riverPath = FindPath(riverStart, riverGoal);

        // Create a river mesh along the river path
        GenerateRiverMesh(riverPath, initialVertices);
    }

    public List<Vector3> FindPath(Vector3 startPosition, Vector3 endPosition)
    {
        int xStart = Mathf.FloorToInt(startPosition.x);
        int zStart = Mathf.FloorToInt(startPosition.z);
        int xEnd = Mathf.FloorToInt(endPosition.x);
        int zEnd = Mathf.FloorToInt(endPosition.z);

        if (xStart < 0 || xStart > xSize || zStart < 0 || zStart > zSize)
        {
            // Start position is out of bounds.
            Debug.Log("start out of bounds");
            return null;
        }

        if (xEnd < 0 || xEnd > xSize || zEnd < 0 || zEnd > zSize)
        {
            // End position is out of bounds.
            Debug.Log("end out of bounds");
            return null;
        }

        List<Vector3> openList = new List<Vector3>();
        List<Vector3> closedList = new List<Vector3>();

        openList.Add(startPosition);

        while (openList.Count > 0)
        {
            Debug.Log("in the while loop.");
            Vector3 currentNode = openList[0];

            for (int i = 1; i < openList.Count; i++)
            {
                if (CalculateFCost(openList[i], endPosition) < CalculateFCost(currentNode, endPosition))
                {
                    Debug.Log("current node");
                    currentNode = openList[i];;
                }
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode);

            if (currentNode == endPosition)
            {
                Debug.Log("path found retracing");
                return RetracePath(startPosition, endPosition, closedList);
            }

            int xCurrent = Mathf.FloorToInt(currentNode.x);
            int zCurrent = Mathf.FloorToInt(currentNode.z);

            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Debug.Log("checking neighbours");
                    int xNeighbor = xCurrent + x;
                    int zNeighbor = zCurrent + z;

                    if (xNeighbor >= 0 && xNeighbor <= xSize && zNeighbor >= 0 && zNeighbor <= zSize)
                    {
                        Vector3 neighbor = vertices[zNeighbor * (xSize + 1) + xNeighbor];

                        if (!closedList.Contains(neighbor))
                        {
                            if (!openList.Contains(neighbor))
                            {
                                openList.Add(neighbor);
                                Debug.Log("neighbours found");                              
                            }
                        }
                    }
                }
            }
        }
        Debug.Log("no path found");
        // No path found
        return null;
    }

    static List<Vector3> RetracePath(Vector3 start, Vector3 end, List<Vector3> path)
    {
        List<Vector3> finalPath = new List<Vector3>();
        Vector3 currentNode = end;

        while (currentNode != start)
        {
            finalPath.Add(currentNode);
            int xCurrent = Mathf.FloorToInt(currentNode.x);
            int zCurrent = Mathf.FloorToInt(currentNode.z);
            bool neighborFound = false;

            for (int x = -1; x <= 1 && !neighborFound; x++)
            {
                for (int z = -1; z <= 1 && !neighborFound; z++)
                {
                    int xNeighbor = xCurrent + x;
                    int zNeighbor = zCurrent + z;
                    Vector3 neighbor = new Vector3(xNeighbor, 0, zNeighbor);

                    if (path.Contains(neighbor) && CalculateFCost(neighbor, end) == CalculateFCost(currentNode, end) - 1)
                    {
                        currentNode = neighbor;
                        neighborFound = true;
                    }
                }
            }
        }

        finalPath.Reverse();
        return finalPath;
    }

    static float CalculateFCost(Vector3 position, Vector3 endPosition)
    {
        float gCost = Vector3.Distance(position, endPosition);
        float hCost = Mathf.Abs(position.x - endPosition.x) + Mathf.Abs(position.z - endPosition.z);
        return gCost + hCost;
    }

    private void GenerateRiverMesh(List<Vector3> riverPath, Vector3[] initialVertices)
    {
        // Create a new GameObject to hold the river mesh
        GameObject riverObject = new GameObject("River");

        // Add a MeshFilter component to the river object
        MeshFilter meshFilter = riverObject.AddComponent<MeshFilter>();

        // Create a new Mesh for the river
        Mesh riverMesh = new Mesh();

        // Use the initial landscape's vertices for the river mesh
        riverMesh.vertices = initialVertices; // Use the initial landscape vertices

        // Create a list to store the river triangles
        List<int> triangles = new List<int>();

        // Loop through the vertices in the river path
        for (int i = 0; i < riverPath.Count - 1; i++)
        {
            int vertexIndex1 = Array.IndexOf(initialVertices, riverPath[i]);
            int vertexIndex2 = Array.IndexOf(initialVertices, riverPath[i + 1]);

            if (vertexIndex1 != -1 && vertexIndex2 != -1)
            {
                // Define triangles connecting river vertices to initial vertices
                // Triangle 1
                triangles.Add(vertexIndex1);
                triangles.Add(vertexIndex1 + 1);
                triangles.Add(vertexIndex2);

                // Triangle 2
                triangles.Add(vertexIndex2);
                triangles.Add(vertexIndex1 + 1);
                triangles.Add(vertexIndex2 + 1);
            }
        }

        // Set the river mesh's triangles
        riverMesh.triangles = triangles.ToArray();

        // Calculate UVs for the river mesh
        Vector2[] uvs = new Vector2[initialVertices.Length];
        for (int i = 0; i < initialVertices.Length; i++)
        {
            // Calculate UV coordinates based on the vertex position
            uvs[i] = new Vector2(initialVertices[i].x, initialVertices[i].z);
        }
        riverMesh.uv = uvs;

        // Calculate normals for the river mesh
        riverMesh.RecalculateNormals();
        riverMesh.RecalculateTangents();

        // Modify the terrain height along the river path
        AdjustTerrainHeight(riverPath);

        // Assign the river mesh to the MeshFilter
        meshFilter.mesh = riverMesh;

        // Create and assign a river material to the MeshRenderer component
        MeshRenderer meshRenderer = riverObject.AddComponent<MeshRenderer>();
        meshRenderer.name = "river";
        meshRenderer.material = riverMaterial;
    }
    private void AdjustTerrainHeight(List<Vector3> riverPath)
    {
        foreach (Vector3 riverPoint in riverPath)
        {

            int xIndex = Mathf.FloorToInt(riverPoint.x);
            int zIndex = Mathf.FloorToInt(riverPoint.z);

            if (xIndex >= 0 && xIndex <= xSize && zIndex >= 0 && zIndex <= zSize)
            {
                float terrainHeight = vertices[zIndex * (xSize + 1) + xIndex].y;

                if (vertices[zIndex * (xSize + 1) + xIndex].y > -1)
                {
                    if (biomeMap[xIndex, zIndex] == BiomeType.Mountains)
                    {
                        // Increase the river depth as the terrain height increases
                        float riverDepth = Mathf.Max(0.0f, terrainHeight);
                        vertices[zIndex * (xSize + 1) + xIndex].y -= riverDepth;
                    }
                    else if (biomeMap[xIndex, zIndex] == BiomeType.Forest)
                    {
                        vertices[zIndex * (xSize + 1) + xIndex].y -= 1.0f;
                    }
                    else if (biomeMap[xIndex, zIndex] == BiomeType.Plains)
                    {
                        vertices[zIndex * (xSize + 1) + xIndex].y -= 0.5f;
                    }
                    else if (biomeMap[xIndex, zIndex] == BiomeType.River)
                    {

                    }
                    else
                    {
                        vertices[zIndex * (xSize + 1) + xIndex].y -= 0.2f;
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
        Vector3 startRiverPoint = Vector3.zero;

        // Iterate through your landscape data (e.g., biomeMap) to find a suitable starting point
        for (int x = 0; x < xSize; x++)
        {
            for (int z = 0; z < zSize; z++)
            {
                // Check if this location is suitable for a river start based on your rules
                if (biomeMap[x, z] == BiomeType.Mountains && HasEnoughMountainBiomeNeighbors(x, z) && IsFarFromOtherRiverStarts(x, z))
                {
                    startRiverPoint = new Vector3(x, z);
                    return startRiverPoint;
                }
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
                if (neighborX >= 0 && neighborX < xSize && neighborZ >= 0 && neighborZ < zSize)
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

    private Vector3 FindNearestBiomePoint(Vector3 startGridPoint, BiomeType biome)
    {
        Vector3 nearestGridPoint = Vector3.zero;
        float nearestDistance = float.MaxValue;

        for (int x = 0; x <= xSize; x++)
        {
            for (int z = 0; z <= zSize; z++)
            {
                if (biomeMap[x, z] == biome)
                {
                    Vector3 biomeGridPoint = new Vector3(x, z);
                    float distance = Vector3.Distance(startGridPoint, biomeGridPoint);

                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestGridPoint = biomeGridPoint;
                    }
                }
            }
        }

        return nearestGridPoint;
    }

    #endregion

    #region smoothing & coloring
    private void SmoothTerrainTransition()
    {
        // Store the initial landscape vertices
        initialVertices = vertices;

        if (mountainCount > 100)
        {
            if (mountainCount > 1000) mountainCount = 1000; // make sure we dont get an overload of rivers
            for (int m = 0; m <= mountainCount; m += 100)
            {
                GenerateRiver(); // Generate rivers
                Debug.Log("making a river");
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
                        if (neighborX >= 0 && neighborX <= xSize && neighborZ >= 0 && neighborZ <= zSize)
                        {
                            int neighborIndex = neighborZ * (xSize + 1) + neighborX;
                            float neighborHeight = vertices[neighborIndex].y;

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

        // Update the mesh with the smoothed heights
        RecalcTerrain();
        ColorTerrain(); // Color the terrain after it has been generated.
        GenerateTrees(); // Generate trees.
    }


    private void ColorTerrain()
    {
        Color[] colors = new Color[vertices.Length];

        float highestHeight = float.MinValue; // Initialize with a very low value

        for (int i = 0; i < vertices.Length; i++)
        {
            if (vertices[i].y > highestHeight)
            {
                highestHeight = vertices[i].y;
            }
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            float colorHeight = Mathf.InverseLerp(0, highestHeight, vertices[i].y);
            colors[i] = gradient.Evaluate(colorHeight);
        }

        mesh.colors = colors;
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
                    case BiomeType.Ocean:
                        Gizmos.color = Color.blue;
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
