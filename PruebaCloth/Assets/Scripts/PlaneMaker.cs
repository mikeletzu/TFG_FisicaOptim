using UnityEngine;

public class PlaneMaker : MonoBehaviour
{
    [SerializeField] Vector3[] newVertices;
    [SerializeField] Vector2[] newUV;
    [SerializeField] int[] newTriangles;

    void Start()
    {
        // 1. Define the 6 vertices (forming two stacked quads)
        // Layout:
        // 4---5
        // |   |
        // 2---3
        // |   |
        // 0---1
        newVertices = new Vector3[]
        {
            new Vector3(0, 0, 0), // 0: Bottom-Left
            new Vector3(1, 0, 0), // 1: Bottom-Right
            new Vector3(0, 1, 0), // 2: Middle-Left
            new Vector3(1, 1, 0), // 3: Middle-Right
            new Vector3(0, 2, 0), // 4: Top-Left
            new Vector3(1, 2, 0)  // 5: Top-Right
        };

        // 2. Define UV coordinates (0 to 1 range)
        newUV = new Vector2[]
        {
            new Vector2(0, 0),     // 0
            new Vector2(1, 0),     // 1
            new Vector2(0, 0.5f),   // 2
            new Vector2(1, 0.5f),   // 3
            new Vector2(0, 1),     // 4
            new Vector2(1, 1)      // 5
        };

        // 3. Define Triangles (Clockwise winding order)
        // Two triangles per quad = 4 triangles total = 12 indices
        newTriangles = new int[]
        {
            // Lower Quad
            0, 2, 1,
            1, 2, 3,
            // Upper Quad
            2, 4, 3,
            3, 4, 5
        };

        // Create a new Mesh and set its data properties (vertices, UV coordinates, and triangles).
        Mesh mesh = new Mesh
        {
            vertices = newVertices,
            uv = newUV,
            triangles = newTriangles
        };

        // After updating the Mesh data, recalculate the normals. 
        // If the Mesh uses shaders with normal maps, also call RecalculateTangents for proper lighting.
        mesh.RecalculateNormals();

        // This assignment is temporary and will reset to the initial Mesh when exiting Play mode.
        GetComponent<MeshFilter>().mesh = mesh;
    }
}