using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SplinePointSampler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SplineSampler m_splineSampler;

    [Header("Sampling")]
    [SerializeField] private int resolution = 20;
    [SerializeField] private bool sampleContinuously = true;

    [Header("Mesh")]
    [SerializeField] private bool buildMeshContinuously = true;

    [Header("UVs")]
    [SerializeField] private bool generateUVs = true;
    [SerializeField] private float metersPerUVTile = 5f;

    [Header("Debug")]
    [SerializeField] private bool drawCenterPoints = true;
    [SerializeField] private bool drawEdgePoints = true;
    [SerializeField] private bool drawRungs = true;

    private readonly List<Vector3> m_vertsP1 = new();
    private readonly List<Vector3> m_vertsP2 = new();
    private readonly List<Vector3> m_centers = new();
    private readonly List<float> m_distances = new();
    private readonly List<int> m_pointsPerSpline = new();

    private MeshFilter m_meshFilter;
    private Mesh m_mesh;

    private void Awake()
    {
        EnsureMesh();
    }

    private void OnEnable()
    {
        EnsureMesh();
    }

    private void Update()
    {
        if (!sampleContinuously)
            return;

        GetVerts();

        if (buildMeshContinuously)
            BuildMesh();
    }

    private void EnsureMesh()
    {
        if (m_meshFilter == null)
            m_meshFilter = GetComponent<MeshFilter>();

        if (m_mesh == null)
        {
            m_mesh = new Mesh();
            m_mesh.name = "Spline Mesh";
            m_meshFilter.sharedMesh = m_mesh;
        }
    }

    [ContextMenu("Sample All Splines")]
    public void GetVerts()
    {
        m_vertsP1.Clear();
        m_vertsP2.Clear();
        m_centers.Clear();
        m_distances.Clear();
        m_pointsPerSpline.Clear();

        if (m_splineSampler == null)
            return;

        resolution = Mathf.Max(2, resolution);

        int splineCount = m_splineSampler.NumSplines;
        if (splineCount <= 0)
            return;

        float step = 1f / resolution;

        for (int splineIndex = 0; splineIndex < splineCount; splineIndex++)
        {
            int addedForThisSpline = 0;
            float runningDistance = 0f;
            Vector3 previousCenter = Vector3.zero;
            bool hasPrevious = false;

            for (int i = 0; i <= resolution; i++)
            {
                float t = Mathf.Clamp01(step * i);

                bool ok = m_splineSampler.TryGetEdgePoints(splineIndex, t, out Vector3 center, out Vector3 p1, out Vector3 p2);
                if (!ok)
                    continue;

                if (!IsFinite(center) || !IsFinite(p1) || !IsFinite(p2))
                    continue;

                if (hasPrevious)
                    runningDistance += Vector3.Distance(previousCenter, center);

                m_centers.Add(center);
                m_vertsP1.Add(p1);
                m_vertsP2.Add(p2);
                m_distances.Add(runningDistance);

                previousCenter = center;
                hasPrevious = true;
                addedForThisSpline++;
            }

            m_pointsPerSpline.Add(addedForThisSpline);
        }
    }

    [ContextMenu("Build Mesh")]
    public void BuildMesh()
    {
        EnsureMesh();
        m_mesh.Clear();

        if (m_splineSampler == null)
            return;

        if (m_vertsP1.Count == 0 || m_vertsP2.Count == 0 || m_centers.Count == 0)
            return;

        if (m_vertsP1.Count != m_vertsP2.Count || m_vertsP1.Count != m_distances.Count)
            return;

        List<Vector3> verts = new();
        List<int> tris = new();
        List<Vector2> uvs = new();

        float safeTileLength = Mathf.Max(0.0001f, metersPerUVTile);

        int runningPointOffset = 0;

        for (int splineIndex = 0; splineIndex < m_pointsPerSpline.Count; splineIndex++)
        {
            int pointCount = m_pointsPerSpline[splineIndex];

            if (pointCount < 2)
            {
                runningPointOffset += pointCount;
                continue;
            }

            for (int point = 1; point < pointCount; point++)
            {
                int indexA = runningPointOffset + point - 1;
                int indexB = runningPointOffset + point;

                if (indexA < 0 || indexA >= m_vertsP1.Count || indexB < 0 || indexB >= m_vertsP1.Count)
                    continue;

                Vector3 p1 = transform.InverseTransformPoint(m_vertsP1[indexA]);
                Vector3 p2 = transform.InverseTransformPoint(m_vertsP2[indexA]);
                Vector3 p3 = transform.InverseTransformPoint(m_vertsP1[indexB]);
                Vector3 p4 = transform.InverseTransformPoint(m_vertsP2[indexB]);

                if (!IsFinite(p1) || !IsFinite(p2) || !IsFinite(p3) || !IsFinite(p4))
                    continue;

                float vA = m_distances[indexA] / safeTileLength;
                float vB = m_distances[indexB] / safeTileLength;

                int offset = verts.Count;

                verts.Add(p1); // left A
                verts.Add(p2); // right A
                verts.Add(p3); // left B
                verts.Add(p4); // right B

                tris.Add(offset + 0);
                tris.Add(offset + 2);
                tris.Add(offset + 1);

                tris.Add(offset + 2);
                tris.Add(offset + 3);
                tris.Add(offset + 1);

                if (generateUVs)
                {
                    uvs.Add(new Vector2(0f, vA));
                    uvs.Add(new Vector2(1f, vA));
                    uvs.Add(new Vector2(0f, vB));
                    uvs.Add(new Vector2(1f, vB));
                }
            }

            runningPointOffset += pointCount;
        }

        if (verts.Count == 0 || tris.Count == 0)
            return;

        m_mesh.SetVertices(verts);
        m_mesh.SetTriangles(tris, 0);

        if (generateUVs && uvs.Count == verts.Count)
            m_mesh.SetUVs(0, uvs);

        m_mesh.RecalculateNormals();
        m_mesh.RecalculateBounds();
    }

    private void OnDrawGizmos()
    {
        if (m_vertsP1.Count == 0 || m_vertsP2.Count == 0)
            return;

        if (drawCenterPoints)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < m_centers.Count; i++)
            {
                if (IsFinite(m_centers[i]))
                    Gizmos.DrawSphere(m_centers[i], 0.12f);
            }
        }

        if (drawEdgePoints)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < m_vertsP1.Count; i++)
            {
                if (IsFinite(m_vertsP1[i]))
                    Gizmos.DrawSphere(m_vertsP1[i], 0.1f);
            }

            Gizmos.color = Color.blue;
            for (int i = 0; i < m_vertsP2.Count; i++)
            {
                if (IsFinite(m_vertsP2[i]))
                    Gizmos.DrawSphere(m_vertsP2[i], 0.1f);
            }
        }

        if (drawRungs)
        {
            int runningPointOffset = 0;

            for (int splineIndex = 0; splineIndex < m_pointsPerSpline.Count; splineIndex++)
            {
                int pointCount = m_pointsPerSpline[splineIndex];

                Gizmos.color = Color.white;
                for (int i = 0; i < pointCount; i++)
                {
                    int idx = runningPointOffset + i;
                    if (idx < m_vertsP1.Count && idx < m_vertsP2.Count &&
                        IsFinite(m_vertsP1[idx]) && IsFinite(m_vertsP2[idx]))
                    {
                        Gizmos.DrawLine(m_vertsP1[idx], m_vertsP2[idx]);
                    }
                }

                Gizmos.color = Color.red;
                for (int i = 0; i < pointCount - 1; i++)
                {
                    int a = runningPointOffset + i;
                    int b = runningPointOffset + i + 1;

                    if (a < m_vertsP1.Count && b < m_vertsP1.Count &&
                        IsFinite(m_vertsP1[a]) && IsFinite(m_vertsP1[b]))
                    {
                        Gizmos.DrawLine(m_vertsP1[a], m_vertsP1[b]);
                    }
                }

                Gizmos.color = Color.blue;
                for (int i = 0; i < pointCount - 1; i++)
                {
                    int a = runningPointOffset + i;
                    int b = runningPointOffset + i + 1;

                    if (a < m_vertsP2.Count && b < m_vertsP2.Count &&
                        IsFinite(m_vertsP2[a]) && IsFinite(m_vertsP2[b]))
                    {
                        Gizmos.DrawLine(m_vertsP2[a], m_vertsP2[b]);
                    }
                }

                runningPointOffset += pointCount;
            }
        }
    }

    private static bool IsFinite(Vector3 v)
    {
        return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}