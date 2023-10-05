using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LandscapeGenerator : MonoBehaviour
{
    public int xSize, zSize;
    private Vector3[] vertices;
    private Mesh mesh;
    public float baseHeight;
    public Gradient gradient;
    private float plainsHeight;

    public enum BiomeType
    {
        Plains,
        Forest,
        Mountains,
        Ocean
    }

    private BiomeType[,] biomeMap; // Define a 2D biome map

    public float mountainScale = 15.0f, plainsScale = 2f;
    public float noiseScale = 0.1f, persistence = 0.5f, lacunarity = 2.0f; // Adjust this to control the noise scale
    public int octaves = 5; // Number of octaves in the fractal noise

    /*
     * xSize and zSize control the grid size.
     * 
     * mountainOctaves controls the number of layers to create the multifractal noise. more octaves is more complex terrain.
     * mountainLacunarity controls the scaling between octaves. more lacunarity is more fine detail in the terrain.
     * mountainPersistence controls the amplutide of the scaling between octaves. lower = smoother, higher is rougher terrain.
     * mountainScale controls the overal size and height of the terrain. more is more terrain.
     * mountainExponent controls the shape of the mountain, higher = sharper and more defined peaks.
     * 
     */

    private void Awake()
    {
        InitializeBiomeMap(); // Initialize the biome map
        StartCoroutine(GenerateTerrain());
    }

    private void InitializeBiomeMap()
    {
        biomeMap = new BiomeType[xSize + 1, zSize + 1];
        // Initialize the entire biome map as Plains by default
        for (int x = 0; x <= xSize; x++)
        {
            for (int z = 0; z <= zSize; z++)
            {
                biomeMap[x, z] = BiomeType.Ocean;
            }
        }

        int numMountainRegions = Random.Range(5, 10); // Adjust the number of mountain regions
        for (int i = 0; i < numMountainRegions; i++)
        {
            int startX = Random.Range(0, xSize);
            int startZ = Random.Range(0, zSize);
            int radius = Random.Range(10, 20); // Adjust the maximum radius

            SetCircularBiomeRegion(startX, startZ, radius, BiomeType.Mountains);
        }
    }

    private void SetBiomeRegion(int startX, int startZ, int width, int height, BiomeType biomeType)
    {
        for (int x = startX; x < startX + width; x++)
        {
            for (int z = startZ; z < startZ + height; z++)
            {
                if (x >= 0 && x <= xSize && z >= 0 && z <= zSize)
                {
                    biomeMap[x, z] = biomeType;
                }
            }
        }
    }

    private void SetCircularBiomeRegion(int centerX, int centerZ, int radius, BiomeType biomeType)
    {
        for (int x = 0; x <= xSize; x++)
        {
            for (int z = 0; z <= zSize; z++)
            {
                int dx = x - centerX;
                int dz = z - centerZ;
                if (dx * dx + dz * dz <= radius * radius)
                {
                    // Check if the point is inside the circular region
                    if (x >= 0 && x <= xSize && z >= 0 && z <= zSize)
                    {
                        biomeMap[x, z] = biomeType;
                    }
                }
            }
        }
    }

    private IEnumerator GenerateTerrain()
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
                yield return new WaitForSeconds(0.0000005f);
            }
        }
        StartCoroutine(SmoothTerrainTransition()); // Color the terrain after it has been generated.
    }

    private float GeneratePlainsTerrain(int x, int z)
    {
        // Scale the coordinates to control the feature size
        float xCoord = (float)x / xSize * plainsScale;
        float zCoord = (float)z / zSize * plainsScale;

        // Use Perlin noise to generate terrain
        plainsHeight = Mathf.PerlinNoise(xCoord, zCoord);

        // Apply scaling and offset to the height
        plainsHeight *= plainsScale;
        plainsHeight += baseHeight;

        return plainsHeight;
    }

    private float GenerateForestTerrain(int x, int z)
    {
        int oceanHeight = -1;
        return oceanHeight;
    }

    private float GenerateMountainousTerrain(int x, int z)
    {
        // Calculate the position in the noise field
        float sampleX = (float)x / xSize * noiseScale;
        float sampleZ = (float)z / zSize * noiseScale;

        float height = 0;
        float amplitude = 1;
        float frequency = 1;

        for (int i = 0; i < octaves; i++)
        {
            float perlinValue = Mathf.PerlinNoise(sampleX * frequency, sampleZ * frequency);
            // Apply a power function to accentuate the peaks
            height += Mathf.Pow(perlinValue, 3) * amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return height * mountainScale;
    }

    private float GenerateOceanTerrain(int x, int z)
    {
        // Define your Forest terrain generation logic here
        // Example: Randomize tree placement, add variation in elevation, etc.
        return baseHeight;
    }

    private IEnumerator SmoothTerrainTransition()
    {
        // Define a range for the transition between plains and mountains
        float transitionRange = 5.0f; // Adjust as needed

        for (int z = 0; z < zSize; z++)
        {
            for (int x = 0; x < xSize; x++)
            {
                int vertexIndex = z * xSize + x;
                Vector3 vertexPosition = vertices[vertexIndex];
                float vertexHeight = vertexPosition.y;

                // Check the neighboring vertices within the transition range
                float totalHeight = vertexHeight;
                int neighborCount = 3;

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
                                totalHeight += neighborHeight;
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
        StartCoroutine(ColorTerrain()); // Color the terrain after it has been generated.
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator ColorTerrain()
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
        yield return new WaitForSeconds(0.5f);
    }

    private void OnDrawGizmos()
    {
        if (vertices == null)
        {
            return;
        }

        Gizmos.color = Color.black;
        for (int i = 0; i < vertices.Length; i++)
        {
            Gizmos.DrawSphere(vertices[i], 0.1f);
        }
    }
}
