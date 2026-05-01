using System.Collections.Generic;
using UnityEngine;
using Dreamteck.Splines;

[ExecuteInEditMode]
[RequireComponent(typeof(SplineComputer))]
public class MagneticBarrierSplineDreamteck : MonoBehaviour
{
    public enum PushMode
    {
        AwayFromSpline,
        AlwaysPushPositiveRight,
        AlwaysPushNegativeRight
    }

    [System.Serializable]
    public struct BarrierInfluence
    {
        public MagneticBarrierSplineDreamteck barrier;

        public Vector3 closestPoint;
        public Vector3 pushDirection;
        public Vector3 barrierForward;
        public Vector3 barrierUp;
        public Vector3 barrierRight;

        public float distance;
        public float signedDistance;
        public float softStrength01;
        public float hardStrength01;
        public float hardCorrectionDistance;
    }

    public static readonly List<MagneticBarrierSplineDreamteck> ActiveBarriers = new List<MagneticBarrierSplineDreamteck>();

    [Header("References")]
    public SplineComputer spline;

    [Header("Barrier Shape")]
    [Tooltip("How far sideways from the spline the magnetic influence begins.")]
    public float softRadius = 3.0f;

    [Tooltip("Inside this sideways distance, the barrier starts doing stronger anti-phase correction.")]
    public float hardRadius = 1.15f;

    [Tooltip("How far above and below the spline the barrier influence reaches.")]
    public float verticalHalfHeight = 2.0f;

    [Tooltip("Extra vertical padding added when checking the player's sphere.")]
    public float verticalPlayerPadding = 0.15f;

    [Tooltip("If true, the player radius is added to the barrier radius and height checks.")]
    public bool includePlayerRadius = true;

    [Tooltip("Additional sideways padding added on top of the player's radius.")]
    public float playerRadiusPadding = 0.15f;



    [Header("Push Direction")]
    [Tooltip("AwayFromSpline pushes away based on the player's current side. AlwaysPush modes force one consistent safe side.")]
    public PushMode pushMode = PushMode.AwayFromSpline;

    [Tooltip("If true, the push ignores vertical difference and mostly pushes sideways.")]
    public bool flattenPushDirection = true;

    [Tooltip("If flattening push direction, this is the fallback up direction.")]
    public Vector3 fallbackWorldUp = Vector3.up;

    [Header("Response")]
    [Tooltip("Acceleration applied away from the barrier while inside the soft radius.")]
    public float pushAcceleration = 85f;

    [Tooltip("How strongly velocity moving into the barrier is removed.")]
    [Range(0f, 1f)]
    public float removeIntoBarrierVelocity = 0.95f;

    [Tooltip("How much steering into the barrier is blocked.")]
    [Range(0f, 1f)]
    public float blockSteeringIntoBarrier = 1.0f;

    [Tooltip("How quickly hard penetration is position-corrected.")]
    public float hardCorrectionSpeed = 18f;

    [Tooltip("Maximum outward speed created by the barrier. Prevents huge launches.")]
    public float maxOutwardSpeed = 16f;

    [Tooltip("How much speed is preserved along the barrier / forward flow after correction.")]
    [Range(0f, 1f)]
    public float forwardSpeedRetention = 0.92f;

    [Header("Falloff")]
    [Tooltip("Controls how the magnetic strength ramps up from the soft radius toward the spline.")]
    public AnimationCurve softFalloff = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Controls how hard correction ramps up inside the hard radius.")]
    public AnimationCurve hardFalloff = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Debug")]
    public bool drawDebug = true;

    [Tooltip("How many segments to draw for the spline line.")]
    public int debugDrawResolution = 48;

    [Tooltip("How many wire spheres to draw along the spline to show influence.")]
    public int debugVolumeSamples = 18;

    [Tooltip("Draws the soft magnetic influence radius.")]
    public bool drawSoftRadius = true;

    [Tooltip("Draws the hard correction radius.")]
    public bool drawHardRadius = true;

    [Tooltip("Draws local forward/up/right axes at debugPercent.")]
    public bool drawSampleAxes = true;

    [Tooltip("Debug sample percent for local axes.")]
    [Range(0f, 1f)]
    public double debugPercent = 0.5;

    public float debugLineLift = 0.03f;
    public float debugAxisLength = 1.25f;
    public float debugPointRadius = 0.16f;

    private void Reset()
    {
        spline = GetComponent<SplineComputer>();
    }

    private void OnEnable()
    {
        if (spline == null)
            spline = GetComponent<SplineComputer>();

        if (!ActiveBarriers.Contains(this))
            ActiveBarriers.Add(this);
    }

    private void OnDisable()
    {
        ActiveBarriers.Remove(this);
    }

    private void Awake()
    {
        if (spline == null)
            spline = GetComponent<SplineComputer>();
    }

    public bool TryGetInfluence(Vector3 playerPosition, float playerRadius, out BarrierInfluence influence)
    {
        influence = default;

        if (spline == null)
            return false;

        float radiusBonus = includePlayerRadius ? Mathf.Max(0f, playerRadius + playerRadiusPadding) : 0f;
        float heightBonus = includePlayerRadius ? Mathf.Max(0f, playerRadius + verticalPlayerPadding) : 0f;

        float effectiveSoftRadius = Mathf.Max(0.01f, softRadius + radiusBonus);
        float effectiveHardRadius = Mathf.Clamp(hardRadius + radiusBonus, 0.01f, effectiveSoftRadius);
        float effectiveVerticalHalfHeight = Mathf.Max(0.01f, verticalHalfHeight + heightBonus);

        SplineSample projected = spline.Project(playerPosition);

        Vector3 point = projected.position;
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

        Vector3 toPlayer = playerPosition - point;

        // Split the player's offset into local barrier-space pieces.
        float verticalOffset = Vector3.Dot(toPlayer, up);
        float absVerticalOffset = Mathf.Abs(verticalOffset);

        // If player is clearly above/below the barrier volume, ignore it.
        if (absVerticalOffset > effectiveVerticalHalfHeight)
            return false;

        Vector3 lateralOffset = toPlayer - up * verticalOffset;

        Vector3 pushDirection;
        float distance;
        float signedDistance;

        if (pushMode == PushMode.AlwaysPushPositiveRight)
        {
            pushDirection = right;
            signedDistance = Vector3.Dot(lateralOffset, pushDirection);
            distance = signedDistance;
        }
        else if (pushMode == PushMode.AlwaysPushNegativeRight)
        {
            pushDirection = -right;
            signedDistance = Vector3.Dot(lateralOffset, pushDirection);
            distance = signedDistance;
        }
        else
        {
            if (lateralOffset.sqrMagnitude < 0.000001f)
                lateralOffset = right;

            pushDirection = lateralOffset.normalized;
            distance = lateralOffset.magnitude;
            signedDistance = distance;
        }

        if (flattenPushDirection)
        {
            Vector3 flatPush = Vector3.ProjectOnPlane(
                pushDirection,
                fallbackWorldUp.sqrMagnitude > 0.001f ? fallbackWorldUp.normalized : Vector3.up
            );

            if (flatPush.sqrMagnitude > 0.000001f)
                pushDirection = flatPush.normalized;
        }

        // For one-sided barriers, negative distance means the player crossed through the forbidden side.
        // That should count as maximum influence, not inactive.
        float distanceForSoftCheck = pushMode == PushMode.AwayFromSpline
            ? distance
            : Mathf.Max(0f, distance);

        float softStrengthRaw = Mathf.InverseLerp(effectiveSoftRadius, 0f, distanceForSoftCheck);

        if (pushMode != PushMode.AwayFromSpline && signedDistance < 0f)
            softStrengthRaw = 1f;

        softStrengthRaw = Mathf.Clamp01(softStrengthRaw);

        if (softStrengthRaw <= 0f)
            return false;

        // Fade influence near the top/bottom of the volume instead of hard popping.
        float verticalFade = Mathf.InverseLerp(effectiveVerticalHalfHeight, effectiveVerticalHalfHeight * 0.65f, absVerticalOffset);
        verticalFade = Mathf.Clamp01(verticalFade);

        softStrengthRaw *= verticalFade;

        if (softStrengthRaw <= 0f)
            return false;

        float hardStrengthRaw = 0f;
        float hardCorrectionDistance = 0f;

        if (pushMode == PushMode.AwayFromSpline)
        {
            if (distance < effectiveHardRadius)
            {
                hardStrengthRaw = Mathf.InverseLerp(effectiveHardRadius, 0f, distance);
                hardCorrectionDistance = effectiveHardRadius - distance;
            }
        }
        else
        {
            if (signedDistance < effectiveHardRadius)
            {
                hardStrengthRaw = Mathf.InverseLerp(effectiveHardRadius, 0f, Mathf.Max(0f, signedDistance));
                hardCorrectionDistance = effectiveHardRadius - signedDistance;
            }
        }

        hardStrengthRaw *= verticalFade;

        float softStrength = softFalloff != null
            ? Mathf.Clamp01(softFalloff.Evaluate(softStrengthRaw))
            : softStrengthRaw;

        float hardStrength = hardFalloff != null
            ? Mathf.Clamp01(hardFalloff.Evaluate(Mathf.Clamp01(hardStrengthRaw)))
            : Mathf.Clamp01(hardStrengthRaw);

        influence = new BarrierInfluence
        {
            barrier = this,
            closestPoint = point,
            pushDirection = pushDirection.normalized,
            barrierForward = forward,
            barrierUp = up,
            barrierRight = right,
            distance = distance,
            signedDistance = signedDistance,
            softStrength01 = softStrength,
            hardStrength01 = hardStrength,
            hardCorrectionDistance = Mathf.Max(0f, hardCorrectionDistance)
        };

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

        DrawSplineLine();
        DrawDebugVolumes();
        DrawSampleAxes();
    }

    private void DrawSplineLine()
    {
        int resolution = Mathf.Max(2, debugDrawResolution);

        SplineSample prev = spline.Evaluate(0.0);
        Gizmos.color = Color.white;

        for (int i = 1; i <= resolution; i++)
        {
            double percent = (double)i / resolution;
            SplineSample current = spline.Evaluate(percent);

            Vector3 prevUp = prev.up.sqrMagnitude > 0.001f ? prev.up.normalized : Vector3.up;
            Vector3 currentUp = current.up.sqrMagnitude > 0.001f ? current.up.normalized : Vector3.up;

            Gizmos.DrawLine(
                prev.position + prevUp * debugLineLift,
                current.position + currentUp * debugLineLift
            );

            prev = current;
        }
    }

    private void DrawDebugVolumes()
    {
        int samples = Mathf.Max(2, debugVolumeSamples);

        for (int i = 0; i <= samples; i++)
        {
            double percent = (double)i / samples;
            SplineSample sample = spline.Evaluate(percent);

            GetSampleFrame(sample, out Vector3 position, out Vector3 forward, out Vector3 up, out Vector3 right);

            DrawBarrierCrossSection(position, up, right, softRadius, verticalHalfHeight, new Color(0f, 0.8f, 1f, 0.55f));

            if (drawHardRadius)
                DrawBarrierCrossSection(position, up, right, hardRadius, verticalHalfHeight, new Color(1f, 0.25f, 0f, 0.75f));

            if (pushMode != PushMode.AwayFromSpline)
            {
                Vector3 push = pushMode == PushMode.AlwaysPushPositiveRight ? right : -right;

                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(position, push * Mathf.Min(softRadius, 2.0f));
            }
        }

        DrawBarrierRails(samples, softRadius, verticalHalfHeight, new Color(0f, 0.8f, 1f, 0.45f));

        if (drawHardRadius)
            DrawBarrierRails(samples, hardRadius, verticalHalfHeight, new Color(1f, 0.25f, 0f, 0.65f));
    }

    private void DrawBarrierCrossSection(Vector3 center, Vector3 up, Vector3 right, float radius, float halfHeight, Color color)
    {
        Gizmos.color = color;

        Vector3 top = center + up * halfHeight;
        Vector3 bottom = center - up * halfHeight;

        Vector3 topRight = top + right * radius;
        Vector3 topLeft = top - right * radius;
        Vector3 bottomRight = bottom + right * radius;
        Vector3 bottomLeft = bottom - right * radius;

        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(topLeft, bottomLeft);
    }

    private void DrawBarrierRails(int samples, float radius, float halfHeight, Color color)
    {
        Gizmos.color = color;

        Vector3 previousTopRight = Vector3.zero;
        Vector3 previousTopLeft = Vector3.zero;
        Vector3 previousBottomRight = Vector3.zero;
        Vector3 previousBottomLeft = Vector3.zero;

        bool hasPrevious = false;

        for (int i = 0; i <= samples; i++)
        {
            double percent = (double)i / samples;
            SplineSample sample = spline.Evaluate(percent);

            GetSampleFrame(sample, out Vector3 center, out _, out Vector3 up, out Vector3 right);

            Vector3 top = center + up * halfHeight;
            Vector3 bottom = center - up * halfHeight;

            Vector3 topRight = top + right * radius;
            Vector3 topLeft = top - right * radius;
            Vector3 bottomRight = bottom + right * radius;
            Vector3 bottomLeft = bottom - right * radius;

            if (hasPrevious)
            {
                Gizmos.DrawLine(previousTopRight, topRight);
                Gizmos.DrawLine(previousTopLeft, topLeft);
                Gizmos.DrawLine(previousBottomRight, bottomRight);
                Gizmos.DrawLine(previousBottomLeft, bottomLeft);
            }

            previousTopRight = topRight;
            previousTopLeft = topLeft;
            previousBottomRight = bottomRight;
            previousBottomLeft = bottomLeft;

            hasPrevious = true;
        }
    }

    private void GetSampleFrame(SplineSample sample, out Vector3 position, out Vector3 forward, out Vector3 up, out Vector3 right)
    {
        position = sample.position;
        forward = sample.forward;
        up = sample.up;

        if (forward.sqrMagnitude < 0.000001f)
            forward = transform.forward;

        if (up.sqrMagnitude < 0.000001f)
            up = Vector3.up;

        forward.Normalize();
        up.Normalize();

        right = Vector3.Cross(forward, up).normalized;

        if (right.sqrMagnitude < 0.000001f)
            right = Vector3.Cross(forward, Vector3.up).normalized;

        if (right.sqrMagnitude < 0.000001f)
            right = Vector3.right;

        position += up * debugLineLift;
    }

    private void DrawSampleAxes()
    {
        if (!drawSampleAxes)
            return;

        SplineSample s = spline.Evaluate(debugPercent);

        Vector3 position = s.position;
        Vector3 forward = s.forward.sqrMagnitude > 0.001f ? s.forward.normalized : transform.forward;
        Vector3 up = s.up.sqrMagnitude > 0.001f ? s.up.normalized : Vector3.up;
        Vector3 right = Vector3.Cross(forward, up).normalized;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(position, debugPointRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(position, forward * debugAxisLength);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(position, up * debugAxisLength);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(position, right * debugAxisLength);
    }
}