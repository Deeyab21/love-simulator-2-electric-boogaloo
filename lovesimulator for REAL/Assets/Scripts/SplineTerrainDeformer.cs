using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class SplineTerrainDeformer : MonoBehaviour
{
    [Header("Road Source")]
    [SerializeField] private SplinePointSampler pointSampler;
    [SerializeField] private MeshFilter sourceMeshFilter;
    [SerializeField] private MeshCollider sourceMeshCollider;

    [Header("Terrains")]
    [SerializeField] private List<Terrain> targetTerrains = new List<Terrain>();

    [Header("Stamp Settings")]
    [SerializeField] private float heightOffset = -0.05f;
    [SerializeField, Range(0f, 1f)] private float strength = 1f;
    [SerializeField] private bool lowerOnly = false;
    [SerializeField] private bool raiseOnly = false;

    [Header("Triangle Filtering")]
    [SerializeField, Range(-1f, 1f)] private float minTriangleUpDot = 0.3f;
    [SerializeField] private bool forceWorldUp = false;

    [Header("Edge Feather")]
    [SerializeField] private float outerFeather = 1.5f;
    [SerializeField] private float boundsPadding = 0.5f;

    [Header("Live Update")]
    [SerializeField] private bool rebuildMeshBeforeDeform = true;
    [SerializeField] private bool deformContinuouslyInEditor = false;

    [Header("Smoothing")]
    [SerializeField] private bool smoothAfterDeform = true;
    [SerializeField] private int smoothIterations = 1;
    [SerializeField] private float smoothStrength = 0.25f;

    private class TerrainWorkData
    {
        public Terrain terrain;
        public TerrainData data;
        public Vector3 pos;
        public Vector3 size;
        public int res;
        public float[,] heights;
        public Bounds bounds;
    }

    private struct Triangle
    {
        public Vector3 a, b, c;
        public Vector3 normal;
        public Vector2 aXZ, bXZ, cXZ;
        public Bounds bounds;
    }

    private void Update()
    {
        if (deformContinuouslyInEditor)
            DeformTerrain();
    }

    [ContextMenu("Auto Fill Terrains")]
    public void AutoFillTerrains()
    {
        targetTerrains.Clear();
        targetTerrains.AddRange(Terrain.activeTerrains);
    }

    [ContextMenu("Deform Terrain")]
    public void DeformTerrain()
    {
        Mesh mesh = GetMesh();
        Transform meshTransform = GetMeshTransform();

        if (mesh == null || meshTransform == null)
        {
            Debug.LogWarning("No mesh source found.");
            return;
        }

        List<TerrainWorkData> terrains = BuildTerrainData();
        if (terrains.Count == 0)
            return;

#if UNITY_EDITOR
        foreach (var t in terrains)
            Undo.RegisterCompleteObjectUndo(t.data, "Spline Terrain Deform");
#endif

        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = meshTransform.TransformPoint(verts[tris[i]]);
            Vector3 b = meshTransform.TransformPoint(verts[tris[i + 1]]);
            Vector3 c = meshTransform.TransformPoint(verts[tris[i + 2]]);

            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

            if (Vector3.Dot(normal, Vector3.up) < minTriangleUpDot)
                continue;

            Triangle tri = BuildTriangle(a, b, c, normal);

            foreach (var t in terrains)
            {
                if (!t.bounds.Intersects(tri.bounds))
                    continue;

                StampTriangle(t, tri);
            }
        }

        if (smoothAfterDeform)
        {
            foreach (var t in terrains)
            {
                for (int i = 0; i < smoothIterations; i++)
                    Smooth(t.heights, smoothStrength);
            }
        }

        foreach (var t in terrains)
            t.data.SetHeights(0, 0, t.heights);
    }

    private Mesh GetMesh()
    {
        if (rebuildMeshBeforeDeform && pointSampler != null)
        {
            pointSampler.GetVerts();
            pointSampler.BuildMesh();
        }

        if (sourceMeshCollider != null)
            return sourceMeshCollider.sharedMesh;

        if (sourceMeshFilter != null)
            return sourceMeshFilter.sharedMesh;

        if (pointSampler != null)
        {
            var mf = pointSampler.GetComponent<MeshFilter>();
            if (mf) return mf.sharedMesh;
        }

        return null;
    }

    private Transform GetMeshTransform()
    {
        if (sourceMeshCollider) return sourceMeshCollider.transform;
        if (sourceMeshFilter) return sourceMeshFilter.transform;
        if (pointSampler) return pointSampler.transform;
        return null;
    }

    private List<TerrainWorkData> BuildTerrainData()
    {
        List<TerrainWorkData> list = new();

        foreach (var t in targetTerrains)
        {
            if (!t) continue;

            var d = t.terrainData;

            list.Add(new TerrainWorkData
            {
                terrain = t,
                data = d,
                pos = t.transform.position,
                size = d.size,
                res = d.heightmapResolution,
                heights = d.GetHeights(0, 0, d.heightmapResolution, d.heightmapResolution),
                bounds = new Bounds(
                    t.transform.position + d.size * 0.5f,
                    d.size
                )
            });
        }

        return list;
    }

    private Triangle BuildTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
    {
        float minX = Mathf.Min(a.x, b.x, c.x) - boundsPadding;
        float maxX = Mathf.Max(a.x, b.x, c.x) + boundsPadding;
        float minZ = Mathf.Min(a.z, b.z, c.z) - boundsPadding;
        float maxZ = Mathf.Max(a.z, b.z, c.z) + boundsPadding;

        return new Triangle
        {
            a = a,
            b = b,
            c = c,
            normal = forceWorldUp ? Vector3.up : normal,
            aXZ = new Vector2(a.x, a.z),
            bXZ = new Vector2(b.x, b.z),
            cXZ = new Vector2(c.x, c.z),
            bounds = new Bounds(
                new Vector3((minX + maxX) * 0.5f, 0, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, 99999f, maxZ - minZ)
            )
        };
    }

    private void StampTriangle(TerrainWorkData t, Triangle tri)
    {
        int minX = WorldToX(t, tri.bounds.min.x);
        int maxX = WorldToX(t, tri.bounds.max.x);
        int minZ = WorldToZ(t, tri.bounds.min.z);
        int maxZ = WorldToZ(t, tri.bounds.max.z);

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float wx = t.pos.x + (x / (float)(t.res - 1)) * t.size.x;
                float wz = t.pos.z + (z / (float)(t.res - 1)) * t.size.z;

                Vector2 p = new(wx, wz);

                bool inside = PointInTriangle(p, tri.aXZ, tri.bXZ, tri.cXZ);

                float blend = 0f;

                if (inside)
                {
                    blend = 1f;
                }
                else if (outerFeather > 0f)
                {
                    float dist = DistToTri(p, tri);
                    if (dist < outerFeather)
                        blend = 1f - (dist / outerFeather);
                }

                if (blend <= 0f)
                    continue;

                if (!SolveHeight(tri.a, tri.normal, wx, wz, out float y))
                    continue;

                Apply(t, x, z, y + heightOffset, blend * strength);
            }
        }
    }

    private void Apply(TerrainWorkData t, int x, int z, float targetY, float blend)
    {
        float current = t.heights[z, x];
        float worldY = t.pos.y + current * t.size.y;

        float newY = Mathf.Lerp(worldY, targetY, blend);

        if (lowerOnly) newY = Mathf.Min(worldY, newY);
        if (raiseOnly) newY = Mathf.Max(worldY, newY);

        t.heights[z, x] = Mathf.InverseLerp(
            t.pos.y,
            t.pos.y + t.size.y,
            newY
        );
    }

    private bool SolveHeight(Vector3 p, Vector3 n, float x, float z, out float y)
    {
        y = p.y;

        if (Mathf.Abs(n.y) < 0.0001f)
            return false;

        y = p.y - ((n.x * (x - p.x)) + (n.z * (z - p.z))) / n.y;
        return true;
    }

    private int WorldToX(TerrainWorkData t, float x)
    {
        float nx = Mathf.InverseLerp(t.pos.x, t.pos.x + t.size.x, x);
        return Mathf.Clamp(Mathf.RoundToInt(nx * (t.res - 1)), 0, t.res - 1);
    }

    private int WorldToZ(TerrainWorkData t, float z)
    {
        float nz = Mathf.InverseLerp(t.pos.z, t.pos.z + t.size.z, z);
        return Mathf.Clamp(Mathf.RoundToInt(nz * (t.res - 1)), 0, t.res - 1);
    }

    private bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float s1 = Sign(p, a, b);
        float s2 = Sign(p, b, c);
        float s3 = Sign(p, c, a);

        bool neg = s1 < 0 || s2 < 0 || s3 < 0;
        bool pos = s1 > 0 || s2 > 0 || s3 > 0;

        return !(neg && pos);
    }

    private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private float DistToTri(Vector2 p, Triangle tri)
    {
        return Mathf.Min(
            DistToSegment(p, tri.aXZ, tri.bXZ),
            DistToSegment(p, tri.bXZ, tri.cXZ),
            DistToSegment(p, tri.cXZ, tri.aXZ)
        );
    }

    private float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    private void Smooth(float[,] h, float s)
    {
        int w = h.GetLength(1);
        int d = h.GetLength(0);

        float[,] copy = (float[,])h.Clone();

        for (int z = 1; z < d - 1; z++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                float avg =
                    copy[z, x] +
                    copy[z + 1, x] +
                    copy[z - 1, x] +
                    copy[z, x + 1] +
                    copy[z, x - 1];

                avg /= 5f;

                h[z, x] = Mathf.Lerp(copy[z, x], avg, s);
            }
        }
    }
}