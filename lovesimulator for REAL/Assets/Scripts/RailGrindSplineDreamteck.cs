using UnityEngine;
using Dreamteck.Splines;

[ExecuteInEditMode]
[RequireComponent(typeof(SplineComputer))]
public class RailGrindSplineDreamteck : MonoBehaviour
{
    [Header("References")]
    public SplineComputer spline;

    [Header("Hover")]
    [Tooltip("Base hover offset measured along the rail's local up direction.")]
    public float grindHoverHeight = 0.08f;

    [Tooltip("Extra clearance added on top of the player radius while grinding.")]
    public float extraHoverClearance = 0.05f;

    [Tooltip("If true, the player radius is included in the final hover offset.")]
    public bool includePlayerRadiusInHover = true;

    [Header("Rail Shape")]
    [Tooltip("Used for rail switching / side checks / forgiving targeting.")]
    public float railWidth = 0.35f;

    [Header("Start Point Attack Lock-On")]
    [Tooltip("Maximum distance allowed to lock onto the START point with attack.")]
    public float endpointLockRadius = 3.0f;

    [Tooltip("Maximum angle between player aim and direction toward the start point.")]
    [Range(1f, 180f)] public float targetMaxAngle = 55f;

    [Header("Full Rail Proximity Catch")]
    [Tooltip("If the player gets this close to any point on the rail, they can auto-catch onto it.")]
    public float proximityCatchRadius = 1.25f;

    [Header("Debug")]
    public bool drawDebug = true;

    [Tooltip("How many segments to use when drawing the full rail.")]
    public int debugDrawResolution = 40;

    [Tooltip("Percent used for the local sample debug marker.")]
    [Range(0f, 1f)] public double debugPercent = 0.0;

    [Tooltip("Draw the full spline path.")]
    public bool drawFullRail = true;

    [Tooltip("Draw start and end markers.")]
    public bool drawStartEndPoints = true;

    [Tooltip("Draw the start-point attack lock hitbox.")]
    public bool drawEndpointLockRadius = true;

    [Tooltip("Draw the full-rail proximity catch hitboxes.")]
    public bool drawProximityCatchRadius = true;

    [Tooltip("Draw the local sample marker and axes.")]
    public bool drawSampleAxes = true;

    [Tooltip("Radius of the start/end point spheres.")]
    public float endpointSphereRadius = 0.22f;

    [Tooltip("Radius of the debug sample sphere.")]
    public float sampleSphereRadius = 0.14f;

    [Tooltip("Length of forward/up/right debug axes.")]
    public float axisLength = 1.2f;

    [Tooltip("Vertical lift applied to the full rail debug line so it doesn't z-fight.")]
    public float debugLineLift = 0.02f;

    [Tooltip("How many wire spheres are drawn along the rail for the proximity catch debug.")]
    public int proximityDebugSamples = 20;

    [System.Serializable]
    public struct RailSample
    {
        public double percent;
        public Vector3 point;
        public Vector3 forward;
        public Vector3 up;
        public Vector3 right;
        public float distance;
    }

    private void Reset()
    {
        spline = GetComponent<SplineComputer>();
    }

    private void Awake()
    {
        if (spline == null)
            spline = GetComponent<SplineComputer>();
    }

    public float GetLength()
    {
        if (spline == null)
            return 0f;

        return Mathf.Max(0.001f, spline.CalculateLength());
    }

    public bool IsClosed()
    {
        return spline != null && spline.isClosed;
    }

    public bool SampleAtPercent(double percent, out RailSample railSample)
    {
        railSample = default;

        if (spline == null)
            return false;

        percent = (double)Mathf.Clamp01((float)percent);

        SplineSample sample = spline.Evaluate(percent);

        Vector3 forward = sample.forward;
        Vector3 up = sample.up;

        if (forward.sqrMagnitude < 0.000001f)
            forward = transform.forward;

        if (up.sqrMagnitude < 0.000001f)
            up = Vector3.up;

        forward.Normalize();
        up.Normalize();

        Vector3 right = Vector3.Cross(forward, up).normalized;
        if (right.sqrMagnitude < 0.000001f)
            right = Vector3.Cross(forward, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.000001f)
            right = Vector3.right;

        railSample = new RailSample
        {
            percent = percent,
            point = sample.position,
            forward = forward,
            up = up,
            right = right,
            distance = 0f
        };

        return true;
    }

    public bool TryProject(Vector3 worldPos, out RailSample railSample)
    {
        railSample = default;

        if (spline == null)
            return false;

        SplineSample projected = spline.Project(worldPos);

        Vector3 forward = projected.forward;
        Vector3 up = projected.up;

        if (forward.sqrMagnitude < 0.000001f)
            forward = transform.forward;

        if (up.sqrMagnitude < 0.000001f)
            up = Vector3.up;

        forward.Normalize();
        up.Normalize();

        Vector3 right = Vector3.Cross(forward, up).normalized;
        if (right.sqrMagnitude < 0.000001f)
            right = Vector3.Cross(forward, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.000001f)
            right = Vector3.right;

        railSample = new RailSample
        {
            percent = projected.percent,
            point = projected.position,
            forward = forward,
            up = up,
            right = right,
            distance = Vector3.Distance(worldPos, projected.position)
        };

        return true;
    }

    public bool TryFindStartAttackTarget(
        Vector3 origin,
        Vector3 aimForward,
        out RailSample sample,
        out float score)
    {
        return TryScoreStartPoint(origin, aimForward, out sample, out score);
    }

    private bool TryScoreStartPoint(
        Vector3 origin,
        Vector3 aimForward,
        out RailSample sample,
        out float score)
    {
        sample = default;
        score = float.NegativeInfinity;

        if (!SampleAtPercent(0.0, out RailSample startPoint))
            return false;

        Vector3 toPoint = startPoint.point - origin;
        float distance = toPoint.magnitude;

        if (distance > endpointLockRadius || distance <= 0.0001f)
            return false;

        aimForward.y = 0f;
        if (aimForward.sqrMagnitude < 0.0001f)
            aimForward = Vector3.forward;
        aimForward.Normalize();

        Vector3 dir = toPoint / distance;
        float angle = Vector3.Angle(aimForward, dir);

        if (angle > targetMaxAngle)
            return false;

        float angleScore = 1f - (angle / targetMaxAngle);
        float distanceScore = 1f - Mathf.Clamp01(distance / endpointLockRadius);

        Vector3 flatRailForward = startPoint.forward;
        flatRailForward.y = 0f;
        if (flatRailForward.sqrMagnitude > 0.0001f)
            flatRailForward.Normalize();

        float alignmentScore = flatRailForward.sqrMagnitude > 0.0001f
            ? Mathf.Abs(Vector3.Dot(aimForward, flatRailForward))
            : 0f;

        startPoint.distance = distance;
        sample = startPoint;
        score = angleScore * 3f + distanceScore * 2f + alignmentScore;
        return true;
    }

    public bool TryGetProximityCatch(Vector3 worldPos, out RailSample sample)
    {
        sample = default;

        if (!TryProject(worldPos, out RailSample projected))
            return false;

        if (projected.distance > proximityCatchRadius)
            return false;

        sample = projected;
        return true;
    }

    public bool Travel(
        double startPercent,
        float distance,
        Spline.Direction direction,
        out RailSample railSample)
    {
        railSample = default;

        if (spline == null)
            return false;

        double newPercent = spline.Travel(startPercent, distance, direction);
        return SampleAtPercent(newPercent, out railSample);
    }

    public bool TryFindSwitchTarget(
        RailSample current,
        Vector3 currentWorldPos,
        float desiredSideSign,
        float maxDistance,
        out RailSample sample)
    {
        sample = default;

        if (!TryProject(currentWorldPos, out RailSample projected))
            return false;

        if (projected.distance > maxDistance)
            return false;

        Vector3 toCandidate = projected.point - current.point;
        Vector3 flatOffset = Vector3.ProjectOnPlane(toCandidate, current.up);

        if (flatOffset.sqrMagnitude < 0.0001f)
            return false;

        flatOffset.Normalize();

        float sideDot = Vector3.Dot(flatOffset, current.right);
        if (Mathf.Sign(sideDot) != Mathf.Sign(desiredSideSign))
            return false;

        sample = projected;
        return true;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebug)
            return;

        if (spline == null)
            spline = GetComponent<SplineComputer>();

        if (spline == null)
            return;

        DrawFullRailDebug();
        DrawStartEndDebug();
        DrawStartPointLockDebug();
        DrawProximityCatchDebug();
        DrawSampleDebug();
    }

    private void DrawFullRailDebug()
    {
        if (!drawFullRail)
            return;

        int safeResolution = Mathf.Max(2, debugDrawResolution);

        if (!SampleAtPercent(0.0, out RailSample prev))
            return;

        Gizmos.color = Color.white;

        for (int i = 1; i <= safeResolution; i++)
        {
            double percent = (double)i / safeResolution;

            if (!SampleAtPercent(percent, out RailSample current))
                continue;

            Vector3 a = prev.point + prev.up * debugLineLift;
            Vector3 b = current.point + current.up * debugLineLift;

            Gizmos.DrawLine(a, b);
            prev = current;
        }

        if (IsClosed())
        {
            if (SampleAtPercent(0.0, out RailSample start) &&
                SampleAtPercent(1.0, out RailSample end))
            {
                Gizmos.DrawLine(
                    end.point + end.up * debugLineLift,
                    start.point + start.up * debugLineLift
                );
            }
        }
    }

    private void DrawStartEndDebug()
    {
        if (!drawStartEndPoints)
            return;

        if (SampleAtPercent(0.0, out RailSample start))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(start.point, endpointSphereRadius);
            Gizmos.DrawLine(start.point, start.point + start.forward * axisLength);
        }

        if (SampleAtPercent(1.0, out RailSample end))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(end.point, endpointSphereRadius);
            Gizmos.DrawLine(end.point, end.point + end.forward * axisLength);
        }
    }

    private void DrawStartPointLockDebug()
    {
        if (!drawEndpointLockRadius)
            return;

        if (SampleAtPercent(0.0, out RailSample start))
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.8f);
            Gizmos.DrawWireSphere(start.point, endpointLockRadius);
        }
    }

    private void DrawProximityCatchDebug()
    {
        if (!drawProximityCatchRadius)
            return;

        int samples = Mathf.Max(2, proximityDebugSamples);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.45f);

        for (int i = 0; i <= samples; i++)
        {
            double percent = (double)i / samples;
            if (!SampleAtPercent(percent, out RailSample sample))
                continue;

            Gizmos.DrawWireSphere(sample.point, proximityCatchRadius);
        }
    }

    private void DrawSampleDebug()
    {
        if (!drawSampleAxes)
            return;

        if (!SampleAtPercent(debugPercent, out RailSample s))
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(s.point, sampleSphereRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(s.point, s.point + s.forward * axisLength);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(s.point, s.point + s.up * axisLength);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(s.point, s.point + s.right * railWidth);
        Gizmos.DrawLine(s.point, s.point - s.right * railWidth);
    }
}