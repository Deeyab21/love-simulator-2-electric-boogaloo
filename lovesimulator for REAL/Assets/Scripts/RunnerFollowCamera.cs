using UnityEngine;

public class RunnerFollowCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public HamsterBallController runner;

    [Header("Base Follow")]
    public float followDistance = 8f;
    public float followHeight = 3.5f;
    public float lookHeightOffset = 1.0f;

    [Header("Position Smoothing")]
    public float positionSmoothTime = 0.12f;

    [Header("Heading")]
    public float yawSmoothSpeed = 6f;
    public float minHeadingMagnitude = 0.1f;

    [Header("Look Ahead")]
    public float lookAheadDistance = 2.0f;
    public float lookAheadSmoothSpeed = 5f;

    [Header("Pitch")]
    public float fixedPitch = 18f;

    [Header("Slope Pitch")]
    public bool useSlopePitch = true;
    public float maxSlopePitchOffset = 12f;
    public float slopePitchSmoothSpeed = 5f;

    [Header("Speed Effects")]
    public bool useSpeedEffects = true;
    public float speedForMaxEffect = 35f;
    public float extraDistanceAtMaxSpeed = 3.0f;
    public float extraHeightAtMaxSpeed = 0.75f;
    public float extraLookAheadAtMaxSpeed = 2.5f;

    [Header("Debug")]
    public bool drawDebug = true;

    private Vector3 positionVelocity;
    private Vector3 smoothedHeading = Vector3.forward;
    private Vector3 smoothedLookAhead = Vector3.zero;
    private float currentYaw;
    private float currentSlopePitchOffset;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("RunnerFollowCamera: No target assigned.");
            enabled = false;
            return;
        }

        if (runner == null)
            runner = target.GetComponentInParent<HamsterBallController>();

        Vector3 initialForward = target.forward;
        initialForward.y = 0f;

        if (initialForward.sqrMagnitude < 0.0001f)
            initialForward = Vector3.forward;

        smoothedHeading = initialForward.normalized;
        currentYaw = Mathf.Atan2(smoothedHeading.x, smoothedHeading.z) * Mathf.Rad2Deg;

        SnapImmediately();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        UpdateHeading();
        UpdateCameraPositionAndRotation();
    }

    private void UpdateHeading()
    {
        Vector3 desiredHeading = smoothedHeading;

        if (runner != null)
        {
            desiredHeading = runner.GetHorizontalVelocity();

            if (desiredHeading.sqrMagnitude < (minHeadingMagnitude * minHeadingMagnitude))
                desiredHeading = target.forward;
        }
        else
        {
            desiredHeading = target.forward;
        }

        desiredHeading.y = 0f;

        if (desiredHeading.sqrMagnitude < (minHeadingMagnitude * minHeadingMagnitude))
        {
            desiredHeading = smoothedHeading;
        }
        else
        {
            desiredHeading.Normalize();
        }

        smoothedHeading = Vector3.Slerp(
            smoothedHeading,
            desiredHeading,
            yawSmoothSpeed * Time.deltaTime
        ).normalized;

        currentYaw = Mathf.Atan2(smoothedHeading.x, smoothedHeading.z) * Mathf.Rad2Deg;
    }

    private void UpdateCameraPositionAndRotation()
    {
        float speed01 = GetSpeedPercent();

        float currentFollowDistance = followDistance;
        float currentFollowHeight = followHeight;
        float currentLookAheadDistance = lookAheadDistance;

        if (useSpeedEffects)
        {
            currentFollowDistance += extraDistanceAtMaxSpeed * speed01;
            currentFollowHeight += extraHeightAtMaxSpeed * speed01;
            currentLookAheadDistance += extraLookAheadAtMaxSpeed * speed01;
        }

        Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);

        Vector3 lookAhead = smoothedHeading * currentLookAheadDistance;
        smoothedLookAhead = Vector3.Lerp(
            smoothedLookAhead,
            lookAhead,
            lookAheadSmoothSpeed * Time.deltaTime
        );

        Vector3 targetLookPoint = target.position + Vector3.up * lookHeightOffset + smoothedLookAhead;

        Vector3 desiredOffset =
            (yawRotation * Vector3.back * currentFollowDistance) +
            (Vector3.up * currentFollowHeight);

        Vector3 desiredPosition = target.position + desiredOffset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime
        );

        Quaternion lookRotation = Quaternion.LookRotation(targetLookPoint - transform.position, Vector3.up);
        Vector3 euler = lookRotation.eulerAngles;

        float targetSlopePitch = GetSlopePitchOffset();

        currentSlopePitchOffset = Mathf.Lerp(
            currentSlopePitchOffset,
            targetSlopePitch,
            slopePitchSmoothSpeed * Time.deltaTime
        );

        transform.rotation = Quaternion.Euler(fixedPitch + currentSlopePitchOffset, euler.y, 0f);
    }

    private float GetSpeedPercent()
    {
        if (!useSpeedEffects || runner == null || speedForMaxEffect <= 0.001f)
            return 0f;

        float speed = runner.GetHorizontalVelocity().magnitude;
        return Mathf.Clamp01(speed / speedForMaxEffect);
    }

    private void SnapImmediately()
    {
        float speed01 = GetSpeedPercent();

        float currentFollowDistance = followDistance;
        float currentFollowHeight = followHeight;

        if (useSpeedEffects)
        {
            currentFollowDistance += extraDistanceAtMaxSpeed * speed01;
            currentFollowHeight += extraHeightAtMaxSpeed * speed01;
        }

        Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);

        Vector3 desiredOffset =
            (yawRotation * Vector3.back * currentFollowDistance) +
            (Vector3.up * currentFollowHeight);

        transform.position = target.position + desiredOffset;

        Vector3 lookPoint = target.position + Vector3.up * lookHeightOffset;
        Quaternion lookRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);

        Vector3 euler = lookRotation.eulerAngles;
        transform.rotation = Quaternion.Euler(fixedPitch, euler.y, 0f);
    }

    private void OnDrawGizmos()
    {
        if (!drawDebug || target == null)
            return;

        Gizmos.color = Color.white;
        Gizmos.DrawLine(target.position, target.position + Vector3.up * lookHeightOffset);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(target.position, target.position + smoothedHeading * 3f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(target.position + Vector3.up * lookHeightOffset + smoothedLookAhead, 0.12f);
    }

    private float GetSlopePitchOffset()
    {
        if (!useSlopePitch || runner == null || !runner.IsGrounded())
            return 0f;

        Vector3 groundNormal = runner.GetGroundNormal().normalized;
        Vector3 forward = smoothedHeading;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return 0f;

        forward.Normalize();

        // Positive when moving uphill, negative when moving downhill.
        float slopeAmount = Vector3.Dot(forward, Vector3.ProjectOnPlane(Vector3.up, groundNormal).normalized);

        // Convert into a pitch offset.
        float targetOffset = -slopeAmount * maxSlopePitchOffset;

        return targetOffset;
    }
}