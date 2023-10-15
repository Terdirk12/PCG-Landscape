using System.Collections;
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
    public GameObject pinetreePrefab, lushtreePrefab; // Reference to your tree prefab
    public float treeDensity = 0.3f, givenRadius = 0.5f; // Adjust the density of trees in the forest biome
    public LayerMask terrain;

    public enum BiomeType
    {
        Plains,
        Forest,
        Mountains,
        Ocean
    }

    private BiomeType[,] biomeMap; // Define a 2D biome map

    public float mountainScale = 15.0f, plainsScale = 2f, smoothingStrenght = 1f, transitionRange = 5.0f, persistence = 0.5f, lacunarity = 2.0f;
    public int octaves = 5, neighbors = 3; // Number of octaves in the fractal noise                  

    private void Awake()
    {
        InitializeBiomeMap(); // Initialize the biome map  
    }

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
                    if (treeHeight < 4.5f)
                    {
                        GameObject treeObject; // The instantiated tree object
                        if (treeHeight > 3.5f)
                        {
                            treeHeight += Random.Range(-0.05f, 0.05f);
                            treeObject = Instantiate(pinetreePrefab, new Vector3(position.x, treeHeight, position.y), Quaternion.identity);
                        }
                        else if (treeHeight > 3f)
                        {
                            float R = Random.Range(0, 2);
                            if(R == 1) treeObject = Instantiate(lushtreePrefab, new Vector3(position.x, treeHeight, position.y), Quaternion.identity);
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

    private float GenerateMountainousTerrain(int x, int z)
    {
        // Calculate the position in the noise field
        float sampleX = (float)x / xSize;
        float sampleZ = (float)z / zSize;

        float height = 0;
        float amplitude = 1;
        float frequency = 1;

        for (int i = 0; i < octaves; i++)
        {
            amplitude *= persistence;
            frequency *= lacunarity;

            float noiseValue = Mathf.PerlinNoise(sampleX * frequency, sampleZ * frequency);

            // Calculate ridged multifractal noise
            float ridgeValue = Mathf.Abs(2 * Mathf.PerlinNoise(sampleX * frequency, sampleZ * frequency) - 1);

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

    private void SmoothTerrainTransition()
    {
        for (int z = 0; z < zSize; z++)
        {
            for (int x = 0; x < xSize; x++)
            {
                int vertexIndex = z * xSize + x;
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
                        if (neighborX >= 0 && neighborX < xSize && neighborZ >= 0 && neighborZ < zSize)
                        {
                            int neighborIndex = neighborZ * xSize + neighborX;
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
        collider.sharedMesh = mesh;
        ColorTerrain(); // Color the terrain after it has been generated.
        GenerateTrees(); // Generate trees.
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

    private void OnDrawGizmos()
    {
        float sphereSize = 0.1f;

        if (biomeMap == null)
        {
            return;
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
