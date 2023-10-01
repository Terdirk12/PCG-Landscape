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

    //public int mountainOctaves = 6;
    //public float mountainLacunarity = 2.0f, mountainPersistence = 0.5f, mountainScale = 5.0f, mountainExponent = 2.0f, plainsScale, plainsHeight, plainsAmplitude;
    /*
     * xSize and zSize control the grid size.
     * 
     * mountainOctaves controls the number of layers to create the multifractal noise. more octaves is more complex terrain.
     * mountainLacunarity controls the scaling between octaves. more lacunarity is more fine detail in the terrain.
     * mountainPersistence controls the amplutide of the scaling between octaves. lower = smoother, higher is rougher terrain.
     * mountainScale controls the overal size and height of the terrain. more is more terrain.
     * mountainExponent controls the shape of the mountain, higher = sharper and more defined peaks.
     * 
     * plainsScale controls the scaling factor for the Perlin noise used to generate plains. higher = smoother, lower = more detailed
     * plainsHeight controls the base height of the plains. starting elevation point
     * plainsAmplitude controls the amplitude of the Perlin noise for plains. hight variation in the plains. lower = flatter plains.

     */

    private void Awake()
    {
        StartCoroutine(GenerateMountainous());
    }

    private IEnumerator GenerateMountainous()
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
                vertices[i] = new Vector3(x, baseHeight, z);
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

    /*

    private float CalculateMultifractalNoise(float x, float z)
    {
        float noise = 0;
        float frequency = 1;
        float amplitude = 1;

        for (int i = 0; i < mountainOctaves; i++)
        {
            noise += Mathf.PerlinNoise(x * frequency, z * frequency) * amplitude;
            frequency *= mountainLacunarity;
            amplitude *= mountainPersistence;
        }

        return Mathf.Pow(noise, mountainExponent) * mountainScale;
    }*/

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
