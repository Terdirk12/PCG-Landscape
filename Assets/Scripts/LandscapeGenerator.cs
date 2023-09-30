using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LandscapeGenerator : MonoBehaviour
{
    public int xSize, zSize;
    private Vector3[] vertices;
    private Mesh mesh;
    public int octaves = 6;
    public float lacunarity = 2.0f, persistence = 0.5f, scale = 5.0f, exponent = 2.0f;
    /*
     * xSize and zSize control the grid size.
     * octaves controls the number of layers to create the multifractal noise. more octaves is more complex terrain.
     * lacunarity controls the scaling between octaves. more lacunarity is more fine detail in the terrain.
     * persistance controls the amplutide of the scaling between octaves. lower = smoother, higher is rougher terrain.
     * scale controls the overal size and height of the terrain. more is more terrain.
     * exponent controls the shape of the mountain, higher = sharper and more defined peaks.
     */

    private void Awake()
    {
        StartCoroutine(Generate());
    }

    private IEnumerator Generate()
    {
        WaitForSeconds wait = new WaitForSeconds(0.0005f);

        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Procedural Grid";

        vertices = new Vector3[(xSize + 1) * (zSize + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        Vector4[] tangents = new Vector4[vertices.Length];
        Vector4 tangent = new Vector4(1f, 0f, 0f, -1f);

        // Modify the vertices to create a mountain using Perlin noise
        // Adjust this scaling factor to control mountain height


        for (int i = 0, z = 0; z <= zSize; z++)
        {
            for (int x = 0; x <= xSize; x++, i++)
            {
                float xCoord = (float)x / xSize * scale;
                float zCoord = (float)z / zSize * scale;

                // Use Perlin noise to generate the mountain height
                float mountainHeight = CalculateMultifractalNoise(xCoord, zCoord);

                vertices[i] = new Vector3(x, mountainHeight, z);
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
                yield return wait;
            }
        }
    }
    private float CalculateMultifractalNoise(float x, float z)
    {
        float noise = 0;
        float frequency = 1;
        float amplitude = 1;

        for (int i = 0; i < octaves; i++)
        {
            noise += Mathf.PerlinNoise(x * frequency, z * frequency) * amplitude;
            frequency *= lacunarity;
            amplitude *= persistence;
        }

        return Mathf.Pow(noise, exponent) * scale;
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
