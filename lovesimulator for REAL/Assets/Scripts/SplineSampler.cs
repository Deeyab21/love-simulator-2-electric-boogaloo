using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Splines;

[ExecuteInEditMode]
public class SplineSampler : MonoBehaviour
{
    [SerializeField] private SplineContainer m_splineContainer;
    [SerializeField] private float m_width = 4f;

    [Header("Debug")]
    [SerializeField] private int debugSplineIndex = 0;
    [SerializeField, Range(0f, 1f)] private float debugTime = 0f;

    private Vector3 debugCenter;
    private Vector3 debugP1;
    private Vector3 debugP2;
    private bool debugValid;

    public int NumSplines
    {
        get
        {
            if (m_splineContainer == null || m_splineContainer.Splines == null)
                return 0;

            return m_splineContainer.Splines.Count;
        }
    }

    public float Width => m_width;

    [System.Serializable]
    public struct ClosestRoadSample
    {
        public int splineIndex;
        public float t;
        public Vector3 center;
        public Vector3 forward;
        public Vector3 up;
        public Vector3 right;
        public float distanceToCenterline;
    }

    public bool IsValidSplineIndex(int splineIndex)
    {
        return m_splineContainer != null &&
               m_splineContainer.Splines != null &&
               splineIndex >= 0 &&
               splineIndex < m_splineContainer.Splines.Count;
    }

    public bool SampleCenter(int splineIndex, float t, out Vector3 center)
    {
        center = Vector3.zero;

        if (!IsValidSplineIndex(splineIndex))
            return false;

        t = math.clamp(t, 0f, 1f);
        m_splineContainer.Evaluate(splineIndex, t, out float3 pos, out _, out _);

        center = (Vector3)pos;
        return IsFinite(center);
    }

    public bool SampleFrame(int splineIndex, float t, out Vector3 center, out Vector3 forward, out Vector3 up, out Vector3 right)
    {
        center = Vector3.zero;
        forward = Vector3.forward;
        up = Vector3.up;
        right = Vector3.right;

        if (!IsValidSplineIndex(splineIndex))
            return false;

        t = Mathf.Clamp01(t);

        m_splineContainer.Evaluate(splineIndex, t, out float3 pos, out float3 tangent, out float3 upVec);

        center = (Vector3)pos;
        forward = ((Vector3)tangent).normalized;
        up = ((Vector3)upVec).normalized;

        if (!IsFinite(center))
            return false;

        if (forward.sqrMagnitude < 0.000001f)
            forward = Vector3.forward;

        if (up.sqrMagnitude < 0.000001f)
            up = Vector3.up;

        right = Vector3.Cross(forward, up).normalized;

        if (right.sqrMagnitude < 0.000001f)
            right = Vector3.Cross(forward, Vector3.up).normalized;

        if (right.sqrMagnitude < 0.000001f)
            right = Vector3.right;

        return true;
    }

    public bool TryGetEdgePoints(int splineIndex, float t, out Vector3 center, out Vector3 p1, out Vector3 p2)
    {
        center = Vector3.zero;
        p1 = Vector3.zero;
        p2 = Vector3.zero;

        if (!SampleFrame(splineIndex, t, out center, out _, out _, out Vector3 right))
            return false;

        p1 = center + right * m_width;
        p2 = center - right * m_width;

        return IsFinite(p1) && IsFinite(p2);
    }

    public bool TryFindClosestRoadSample(Vector3 worldPos, int segmentResolution, out ClosestRoadSample sample)
    {
        sample = default;

        if (m_splineContainer == null || m_splineContainer.Splines == null || m_splineContainer.Splines.Count == 0)
            return false;

        segmentResolution = Mathf.Max(4, segmentResolution);

        bool found = false;
        float bestDistSqr = float.PositiveInfinity;

        for (int splineIndex = 0; splineIndex < NumSplines; splineIndex++)
        {
            float prevT = 0f;
            if (!SampleCenter(splineIndex, prevT, out Vector3 prevCenter))
                continue;

            for (int i = 1; i <= segmentResolution; i++)
            {
                float nextT = (float)i / segmentResolution;

                if (!SampleCenter(splineIndex, nextT, out Vector3 nextCenter))
                {
                    prevCenter = nextCenter;
                    prevT = nextT;
                    continue;
                }

                Vector3 closestPoint = ClosestPointOnSegment(worldPos, prevCenter, nextCenter, out float seg01);
                float distSqr = (worldPos - closestPoint).sqrMagnitude;

                if (distSqr < bestDistSqr)
                {
                    float bestT = Mathf.Lerp(prevT, nextT, seg01);

                    if (SampleFrame(splineIndex, bestT, out Vector3 center, out Vector3 forward, out Vector3 up, out Vector3 right))
                    {
                        bestDistSqr = distSqr;
                        found = true;

                        sample = new ClosestRoadSample
                        {
                            splineIndex = splineIndex,
                            t = bestT,
                            center = center,
                            forward = forward,
                            up = up,
                            right = right,
                            distanceToCenterline = Mathf.Sqrt(distSqr)
                        };
                    }
                }

                prevCenter = nextCenter;
                prevT = nextT;
            }
        }

        return found;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b, out float t01)
    {
        Vector3 ab = b - a;
        float abSqr = ab.sqrMagnitude;

        if (abSqr <= 0.000001f)
        {
            t01 = 0f;
            return a;
        }

        t01 = Mathf.Clamp01(Vector3.Dot(point - a, ab) / abSqr);
        return a + ab * t01;
    }

    private void Update()
    {
        debugValid = TryGetEdgePoints(debugSplineIndex, debugTime, out debugCenter, out debugP1, out debugP2);
    }

    private void OnDrawGizmos()
    {
        if (!debugValid)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(debugCenter, 0.2f);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(debugP1, 0.15f);

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(debugP2, 0.15f);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(debugP1, debugP2);
    }

    private static bool IsFinite(Vector3 v)
    {
        return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}