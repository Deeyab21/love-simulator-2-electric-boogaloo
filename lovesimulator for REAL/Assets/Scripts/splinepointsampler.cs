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

    [Header("Intersections")]
    [SerializeField] private bool buildIntersections = true;
    [SerializeField] private float junctionMergeDistance = 5f;
    [SerializeField] private float junctionPadForwardLength = 4f;
    [SerializeField] private float junctionCenterLift = 0.01f;
    [SerializeField] private int minimumEndpointsForJunction = 3;

    [Header("Debug Intersection Mesh")]
    [SerializeField] private bool showGreenIntersectionDebugMesh = true;
    [SerializeField] private Material greenIntersectionDebugMaterial;
    [SerializeField] private float greenIntersectionDebugLift = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool drawCenterPoints = true;
    [SerializeField] private bool drawEdgePoints = true;
    [SerializeField] private bool drawRungs = true;
    [SerializeField] private bool drawJunctionDebug = true;

    private readonly List<Vector3> m_vertsP1 = new();
    private readonly List<Vector3> m_vertsP2 = new();
    private readonly List<Vector3> m_centers = new();
    private readonly List<float> m_distances = new();
    private readonly List<int> m_pointsPerSpline = new();
    private readonly List<RoadJunction> m_junctions = new();

    private MeshFilter m_meshFilter;
    private Mesh m_mesh;

    private GameObject m_debugJunctionObject;
    private MeshFilter m_debugJunctionMeshFilter;
    private MeshRenderer m_debugJunctionMeshRenderer;
    private Mesh m_debugJunctionMesh;

    public IReadOnlyList<RoadJunction> Junctions => m_junctions;
    public float JunctionPadForwardLength => junctionPadForwardLength;

    [System.Serializable]
    public struct RoadEndpoint
    {
        public int splineIndex;
        public bool isStart;
        public float t;

        public Vector3 center;
        public Vector3 forward;
        public Vector3 up;
        public Vector3 right;

        public Vector3 edgeA;
        public Vector3 edgeB;

        public Vector3 OutwardDirection => isStart ? -forward : forward;
    }

    [System.Serializable]
    public class RoadJunction
    {
        public Vector3 center;
        public Vector3 averageUp;
        public readonly List<RoadEndpoint> endpoints = new();
    }

    private void Awake()
    {
        EnsureMesh();
        EnsureDebugIntersectionMeshObject();
    }

    private void OnEnable()
    {
        EnsureMesh();
        EnsureDebugIntersectionMeshObject();
        UpdateDebugIntersectionMesh();
    }

    private void OnDisable()
    {
        UpdateDebugIntersectionMesh();
    }

    private void Update()
    {
        if (!sampleContinuously)
        {
            UpdateDebugIntersectionMesh();
            return;
        }

        GetVerts();

        if (buildMeshContinuously)
            BuildMesh();
        else
            UpdateDebugIntersectionMesh();
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

    private void EnsureDebugIntersectionMeshObject()
    {
        if (m_debugJunctionObject == null)
        {
            Transform child = transform.Find("__JunctionDebugMesh");
            if (child != null)
                m_debugJunctionObject = child.gameObject;
        }

        if (m_debugJunctionObject == null)
        {
            m_debugJunctionObject = new GameObject("__JunctionDebugMesh");
            m_debugJunctionObject.hideFlags = HideFlags.DontSave;
            m_debugJunctionObject.transform.SetParent(transform, false);
            m_debugJunctionObject.transform.localPosition = Vector3.zero;
            m_debugJunctionObject.transform.localRotation = Quaternion.identity;
            m_debugJunctionObject.transform.localScale = Vector3.one;
        }

        m_debugJunctionMeshFilter = m_debugJunctionObject.GetComponent<MeshFilter>();
        if (m_debugJunctionMeshFilter == null)
            m_debugJunctionMeshFilter = m_debugJunctionObject.AddComponent<MeshFilter>();

        m_debugJunctionMeshRenderer = m_debugJunctionObject.GetComponent<MeshRenderer>();
        if (m_debugJunctionMeshRenderer == null)
            m_debugJunctionMeshRenderer = m_debugJunctionObject.AddComponent<MeshRenderer>();

        if (m_debugJunctionMesh == null)
        {
            m_debugJunctionMesh = m_debugJunctionMeshFilter.sharedMesh;

            if (m_debugJunctionMesh == null)
            {
                m_debugJunctionMesh = new Mesh();
                m_debugJunctionMesh.name = "Spline Junction Debug Mesh";
                m_debugJunctionMeshFilter.sharedMesh = m_debugJunctionMesh;
            }
        }
        else if (m_debugJunctionMeshFilter.sharedMesh != m_debugJunctionMesh)
        {
            m_debugJunctionMeshFilter.sharedMesh = m_debugJunctionMesh;
        }

        if (greenIntersectionDebugMaterial != null)
            m_debugJunctionMeshRenderer.sharedMaterial = greenIntersectionDebugMaterial;
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

        RebuildJunctionCache();
    }

    [ContextMenu("Build Mesh")]
    public void BuildMesh()
    {
        EnsureMesh();
        EnsureDebugIntersectionMeshObject();

        m_mesh.Clear();

        if (m_splineSampler == null)
        {
            UpdateDebugIntersectionMesh();
            return;
        }

        if (m_vertsP1.Count == 0 || m_vertsP2.Count == 0 || m_centers.Count == 0)
        {
            UpdateDebugIntersectionMesh();
            return;
        }

        if (m_vertsP1.Count != m_vertsP2.Count || m_vertsP1.Count != m_distances.Count)
        {
            UpdateDebugIntersectionMesh();
            return;
        }

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

                verts.Add(p1);
                verts.Add(p2);
                verts.Add(p3);
                verts.Add(p4);

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

        if (buildIntersections && m_junctions.Count > 0)
        {
            for (int i = 0; i < m_junctions.Count; i++)
                AddJunctionMesh(m_junctions[i], verts, tris, uvs);
        }

        if (verts.Count > 0 && tris.Count > 0)
        {
            m_mesh.SetVertices(verts);
            m_mesh.SetTriangles(tris, 0);

            if (generateUVs && uvs.Count == verts.Count)
                m_mesh.SetUVs(0, uvs);

            m_mesh.RecalculateNormals();
            m_mesh.RecalculateBounds();
        }

        UpdateDebugIntersectionMesh();
    }

    [ContextMenu("Refresh Road + Junctions")]
    public void RefreshNow()
    {
        GetVerts();
        BuildMesh();
    }

    private void RebuildJunctionCache()
    {
        m_junctions.Clear();

        if (!buildIntersections || m_splineSampler == null)
            return;

        List<RoadEndpoint> endpoints = GetRoadEndpoints();
        if (endpoints.Count < minimumEndpointsForJunction)
            return;

        bool[] used = new bool[endpoints.Count];

        for (int i = 0; i < endpoints.Count; i++)
        {
            if (used[i])
                continue;

            List<int> group = new();
            group.Add(i);

            Vector3 seedCenter = endpoints[i].center;

            for (int j = i + 1; j < endpoints.Count; j++)
            {
                if (used[j])
                    continue;

                if (Vector3.Distance(seedCenter, endpoints[j].center) <= junctionMergeDistance)
                    group.Add(j);
            }

            if (group.Count < minimumEndpointsForJunction)
                continue;

            RoadJunction junction = new RoadJunction();

            Vector3 avgCenter = Vector3.zero;
            Vector3 avgUp = Vector3.zero;

            for (int g = 0; g < group.Count; g++)
            {
                RoadEndpoint ep = endpoints[group[g]];
                junction.endpoints.Add(ep);
                avgCenter += ep.center;
                avgUp += ep.up;
            }

            avgCenter /= junction.endpoints.Count;
            avgUp /= Mathf.Max(1, junction.endpoints.Count);

            if (avgUp.sqrMagnitude < 0.000001f)
                avgUp = Vector3.up;
            else
                avgUp.Normalize();

            junction.center = avgCenter + avgUp * junctionCenterLift;
            junction.averageUp = avgUp;

            for (int g = 0; g < group.Count; g++)
                used[group[g]] = true;

            m_junctions.Add(junction);
        }
    }

    private List<RoadEndpoint> GetRoadEndpoints()
    {
        List<RoadEndpoint> endpoints = new();

        if (m_splineSampler == null)
            return endpoints;

        for (int splineIndex = 0; splineIndex < m_splineSampler.NumSplines; splineIndex++)
        {
            AddEndpoint(endpoints, splineIndex, true, 0f);
            AddEndpoint(endpoints, splineIndex, false, 1f);
        }

        return endpoints;
    }

    private void AddEndpoint(List<RoadEndpoint> endpoints, int splineIndex, bool isStart, float t)
    {
        if (!m_splineSampler.SampleFrame(splineIndex, t, out Vector3 center, out Vector3 forward, out Vector3 up, out Vector3 right))
            return;

        if (!m_splineSampler.TryGetEdgePoints(splineIndex, t, out _, out Vector3 p1, out Vector3 p2))
            return;

        endpoints.Add(new RoadEndpoint
        {
            splineIndex = splineIndex,
            isStart = isStart,
            t = t,
            center = center,
            forward = forward,
            up = up,
            right = right,
            edgeA = p1,
            edgeB = p2
        });
    }

    public void GetJunctionRingWorldPoints(RoadJunction junction, List<Vector3> ringPoints)
    {
        ringPoints.Clear();

        if (junction == null || junction.endpoints.Count < minimumEndpointsForJunction)
            return;

        for (int i = 0; i < junction.endpoints.Count; i++)
        {
            RoadEndpoint ep = junction.endpoints[i];

            Vector3 dir = ep.OutwardDirection;
            if (dir.sqrMagnitude < 0.000001f)
                dir = Vector3.forward;
            else
                dir.Normalize();

            Vector3 a = ep.edgeA + dir * junctionPadForwardLength;
            Vector3 b = ep.edgeB + dir * junctionPadForwardLength;

            ringPoints.Add(a);
            ringPoints.Add(b);
        }

        SortRingPointsAroundCenter(junction.center, junction.averageUp, ringPoints);
    }

    private void AddJunctionMesh(RoadJunction junction, List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        if (junction == null || junction.endpoints.Count < minimumEndpointsForJunction)
            return;

        List<Vector3> ringPoints = new();
        GetJunctionRingWorldPoints(junction, ringPoints);

        int centerIndex = verts.Count;
        Vector3 localCenter = transform.InverseTransformPoint(junction.center);
        verts.Add(localCenter);

        if (generateUVs)
            uvs.Add(new Vector2(0.5f, 0.5f));

        int ringStart = verts.Count;
        float uvScale = 1f / Mathf.Max(0.001f, m_splineSampler.Width * 4f);

        for (int i = 0; i < ringPoints.Count; i++)
        {
            Vector3 local = transform.InverseTransformPoint(ringPoints[i]);
            verts.Add(local);

            if (generateUVs)
            {
                Vector3 d = local - localCenter;
                uvs.Add(new Vector2(0.5f + d.x * uvScale, 0.5f + d.z * uvScale));
            }
        }

        for (int i = 0; i < ringPoints.Count; i++)
        {
            int a = ringStart + i;
            int b = ringStart + ((i + 1) % ringPoints.Count);

            tris.Add(centerIndex);
            tris.Add(a);
            tris.Add(b);
        }
    }

    private void UpdateDebugIntersectionMesh()
    {
        EnsureDebugIntersectionMeshObject();

        if (m_debugJunctionMesh == null)
            return;

        m_debugJunctionMesh.Clear();

        bool shouldShow =
            showGreenIntersectionDebugMesh &&
            buildIntersections &&
            m_junctions != null &&
            m_junctions.Count > 0 &&
            greenIntersectionDebugMaterial != null;

        if (m_debugJunctionMeshRenderer != null)
            m_debugJunctionMeshRenderer.enabled = shouldShow;

        if (!shouldShow)
            return;

        List<Vector3> verts = new();
        List<int> tris = new();

        for (int j = 0; j < m_junctions.Count; j++)
        {
            RoadJunction junction = m_junctions[j];
            if (junction == null || junction.endpoints.Count < minimumEndpointsForJunction)
                continue;

            List<Vector3> ringPoints = new();
            GetJunctionRingWorldPoints(junction, ringPoints);

            if (ringPoints.Count < 3)
                continue;

            Vector3 liftedCenter = junction.center + junction.averageUp * greenIntersectionDebugLift;
            int centerIndex = verts.Count;
            verts.Add(transform.InverseTransformPoint(liftedCenter));

            int ringStart = verts.Count;

            for (int i = 0; i < ringPoints.Count; i++)
            {
                Vector3 lifted = ringPoints[i] + junction.averageUp * greenIntersectionDebugLift;
                verts.Add(transform.InverseTransformPoint(lifted));
            }

            for (int i = 0; i < ringPoints.Count; i++)
            {
                int a = ringStart + i;
                int b = ringStart + ((i + 1) % ringPoints.Count);

                tris.Add(centerIndex);
                tris.Add(a);
                tris.Add(b);
            }
        }

        if (verts.Count == 0 || tris.Count == 0)
        {
            if (m_debugJunctionMeshRenderer != null)
                m_debugJunctionMeshRenderer.enabled = false;
            return;
        }

        m_debugJunctionMesh.SetVertices(verts);
        m_debugJunctionMesh.SetTriangles(tris, 0);
        m_debugJunctionMesh.RecalculateNormals();
        m_debugJunctionMesh.RecalculateBounds();
    }

    private static void SortRingPointsAroundCenter(Vector3 center, Vector3 planeUp, List<Vector3> ringPoints)
    {
        Vector3 planeRight = Vector3.Cross(Vector3.forward, planeUp);
        if (planeRight.sqrMagnitude < 0.000001f)
            planeRight = Vector3.right;
        else
            planeRight.Normalize();

        Vector3 planeForward = Vector3.Cross(planeUp, planeRight).normalized;
        if (planeForward.sqrMagnitude < 0.000001f)
            planeForward = Vector3.forward;

        ringPoints.Sort((a, b) =>
        {
            Vector3 da = a - center;
            Vector3 db = b - center;

            float ax = Vector3.Dot(da, planeRight);
            float az = Vector3.Dot(da, planeForward);

            float bx = Vector3.Dot(db, planeRight);
            float bz = Vector3.Dot(db, planeForward);

            float angleA = Mathf.Atan2(az, ax);
            float angleB = Mathf.Atan2(bz, bx);

            return angleA.CompareTo(angleB);
        });
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

        if (drawJunctionDebug && m_junctions != null)
        {
            for (int i = 0; i < m_junctions.Count; i++)
            {
                RoadJunction junction = m_junctions[i];
                if (junction == null)
                    continue;

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(junction.center, 0.35f);

                for (int e = 0; e < junction.endpoints.Count; e++)
                {
                    RoadEndpoint ep = junction.endpoints[e];

                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(junction.center, ep.center);

                    Gizmos.color = Color.magenta;
                    Gizmos.DrawRay(ep.center, ep.OutwardDirection.normalized * junctionPadForwardLength);
                }
            }
        }
    }

    private static bool IsFinite(Vector3 v)
    {
        return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}