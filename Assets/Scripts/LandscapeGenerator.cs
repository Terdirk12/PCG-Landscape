using System.Collections;
using System.Collections.Generic;
using UnityEditor;
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
    public float treeDensity = 0.3f, givenRadius = 0.5f, riverStartWidth = 5f, riverStartDepth = 2f; // Adjust the density of trees in the forest biome
    public LayerMask terrain;
    private int mountainCount;
    private List<Vector3> riverStartPoints = new List<Vector3>();

    public enum BiomeType
    {
        Plains,
        Forest,
        Mountains,
        Ocean,
        River
    }

    private BiomeType[,] biomeMap; // Define a 2D biome map

    public float mountainScale = 15.0f, plainsScale = 2f, smoothingStrenght = 1f, transitionRange = 5.0f, persistence = 0.5f, lacunarity = 2.0f;
    public int octaves = 5, neighbors = 3; // Number of octaves in the fractal noise                  

    private void Awake()
    {
        InitializeBiomeMap(); // Initialize the biome map  
    }

    #region biomes

    private void InitializeBiomeMap()
    {
        biomeMap = new BiomeType[xSize + 1, zSize + 1];
        int xOffset = Random.Range(0, 10000);
        int zOffset = Random.Range(0, 10000);

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
        float xCoord = (float)x / xSize * plainsScale;
        float zCoord = (float)z / zSize * plainsScale;

        // Use Perlin noise to generate terrain
        // Apply scaling and offset to the height
        float plainsHeight = baseHeight + Mathf.PerlinNoise(xCoord, zCoord);

        return plainsHeight;
    }


    private float GenerateMountainousTerrain(int x, int z)
    {
        // Calculate the position in the noise field
        float sampleX = (float)x / xSize;
        float sampleZ = (float)z / zSize;

        float height = baseHeight;
        float amplitude = 1;
        float frequency = 1;

        for (int i = 0; i < octaves; i++)
        {
            amplitude = Mathf.Pow(persistence, i);
            frequency *= lacunarity;

            float noiseValue = Mathf.PerlinNoise(sampleX * frequency, sampleZ * frequency);

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


                if (Random.value < density)
                {
                    // Use the height at the tree's grid position
                    //float treeHeight = vertices[Mathf.FloorToInt(position.y) * (xSize + 1) + Mathf.FloorToInt(position.x)].y;
                    float treeHeight = SampleTerrainHeight(position);
                    GameObject treeObject; // The instantiated tree object
                    if (treeHeight < 4.5f)
                    {
                        if (treeHeight > 3.5f)
                        {
                            treeHeight += Random.Range(-0.05f, 0.05f);
                            treeObject = Instantiate(pinetreePrefab, new Vector3(position.x, treeHeight, position.y), Quaternion.identity);
                        }
                        else if (treeHeight > 3f)
                        {
                            float R = Random.Range(0, 2);
                            if (R == 1) treeObject = Instantiate(lushtreePrefab, new Vector3(position.x, treeHeight, position.y), Quaternion.identity);
                            else treeObject = Instantiate(pinetreePrefab, new Vector3(position.x, treeHeight, position.y), Quaternion.identity);
                        }
                        else
                        {
                            treeHeight += Random.Range(-0.05f, 0.05f);
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
        // Generate the path of the river using an algorithm
        List<Vector3> riverPath = GenerateRiverPath();

        // Create a depression in the terrain for the riverbed
        ModifyTerrainForRiver(riverPath);

        // Create a river mesh along the river path
        CreateRiverMesh(riverPath);
    }

    private List<Vector3> GenerateRiverPath()
    {
        List<Vector3> riverPath = new List<Vector3>();

        // Find a starting point for the river
        Vector3 startPoint = FindRiverStartPoint();

        // Find the nearest ocean biome point to the starting point
        Vector3 endPoint = FindNearestOceanPoint(startPoint);

        // Sample points between the start and end to create a smooth path
        int numPoints = 100; // Adjust as needed
        for (int i = 0; i < numPoints; i++)
        {
            float t = i / (float)(numPoints - 1);
            Vector3 point = Vector3.Lerp(startPoint, endPoint, t);

            // Apply Perlin noise to create a winding river path
            float perlinX = point.x * 0.1f;
            float perlinZ = point.z * 0.1f;
            float yOffset = riverStartDepth * Mathf.PerlinNoise(perlinX, perlinZ);

            point.y = yOffset;

            riverPath.Add(point);

            // Set the biome along the river path to "River"
            int xIndex = Mathf.FloorToInt(point.x);
            int zIndex = Mathf.FloorToInt(point.z);
            if (xIndex >= 0 && xIndex <= xSize && zIndex >= 0 && zIndex <= zSize)
            {
                biomeMap[xIndex, zIndex] = BiomeType.River;
            }
        }

        return riverPath;
    }

    private void ModifyTerrainForRiver(List<Vector3> riverPath)
    {

    }

    private void CreateRiverMesh(List<Vector3> riverPath)
    {

    }

    private Vector3 FindRiverStartPoint()
    {
        Vector3 startRiverPoint = Vector3.zero;

        while (true)
        {
            // Randomly select a point in the mountains
            int x = Random.Range(0, xSize);
            int z = Random.Range(0, zSize);

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
        int requiredMountainNeighbors = 8;

        int mountainNeighbors = 0;

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
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

    private Vector3 FindNearestOceanPoint(Vector3 startPoint)
    {
        Vector3 nearestPoint = Vector3.zero;
        float nearestDistance = float.MaxValue;

        for (int x = 0; x <= xSize; x++)
        {
            for (int z = 0; z <= zSize; z++)
            {
                if (biomeMap[x, z] == BiomeType.Ocean)
                {
                    Vector3 oceanPoint = new Vector3(x, 0, z);
                    float distance = Vector3.Distance(startPoint, oceanPoint);

                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestPoint = oceanPoint;
                    }
                }
            }
        }

        return nearestPoint;
    }
    #endregion

    #region smoothing & coloring
    private void SmoothTerrainTransition()
    {
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
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        collider.sharedMesh = mesh;
        if (mountainCount > 100)
        {
            for (int m = 0; m <= mountainCount; m += 100)
            {
                GenerateRiver(); // Generate rivers
                Debug.Log("making una riveare");
            }
        }
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
