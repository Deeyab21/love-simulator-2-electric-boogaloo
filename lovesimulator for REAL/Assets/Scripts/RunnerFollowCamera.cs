using UnityEngine;

public class RunnerFollowCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public HighSpeedRunnerController runner;

    [Header("Follow Distance")]
    public float followDistance = 8f;
    public float followHeight = 3.5f;
    public float lookHeightOffset = 1.0f;

    [Header("Position Smoothing")]
    public float positionSmoothTime = 0.12f;

    [Header("Rotation / Recenter")]
    public float yawSmoothSpeed = 6f;
    public float idleRecenteringSpeed = 3f;
    public float minHeadingMagnitude = 0.1f;

    [Header("Look Ahead")]
    public float lookAheadDistance = 2.0f;
    public float lookAheadSmoothSpeed = 5f;

    [Header("Pitch")]
    public float fixedPitch = 18f;

    [Header("Debug")]
    public bool drawDebug = true;

    private Vector3 positionVelocity;
    private Vector3 smoothedHeading = Vector3.forward;
    private Vector3 smoothedLookAhead = Vector3.zero;
    private float currentYaw;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("RunnerFollowCamera: No target assigned.");
            enabled = false;
            return;
        }

        if (runner == null)
            runner = target.GetComponentInParent<HighSpeedRunnerController>();

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
            desiredHeading = runner.GetTravelDirection();
            desiredHeading.y = 0f;
        }
        else
        {
            desiredHeading = target.forward;
            desiredHeading.y = 0f;
        }

        if (desiredHeading.sqrMagnitude < (minHeadingMagnitude * minHeadingMagnitude))
        {
            desiredHeading = smoothedHeading;
        }
        else
        {
            desiredHeading.Normalize();
        }

        float headingSmooth = yawSmoothSpeed;

        // When almost idle / low direction confidence, recenter more gently.
        if (runner == null || desiredHeading.sqrMagnitude < 0.01f)
            headingSmooth = idleRecenteringSpeed;

        smoothedHeading = Vector3.Slerp(
            smoothedHeading,
            desiredHeading,
            headingSmooth * Time.deltaTime
        ).normalized;

        currentYaw = Mathf.Atan2(smoothedHeading.x, smoothedHeading.z) * Mathf.Rad2Deg;
    }

    private void UpdateCameraPositionAndRotation()
    {
        Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);

        Vector3 lookAhead = smoothedHeading * lookAheadDistance;
        smoothedLookAhead = Vector3.Lerp(
            smoothedLookAhead,
            lookAhead,
            lookAheadSmoothSpeed * Time.deltaTime
        );

        Vector3 targetLookPoint = target.position + Vector3.up * lookHeightOffset + smoothedLookAhead;

        Vector3 desiredOffset =
            (yawRotation * Vector3.back * followDistance) +
            (Vector3.up * followHeight);

        Vector3 desiredPosition = target.position + desiredOffset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime
        );

        Quaternion lookRotation = Quaternion.LookRotation(targetLookPoint - transform.position, Vector3.up);

        Vector3 euler = lookRotation.eulerAngles;
        transform.rotation = Quaternion.Euler(fixedPitch, euler.y, 0f);
    }

    private void SnapImmediately()
    {
        Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);

        Vector3 desiredOffset =
            (yawRotation * Vector3.back * followDistance) +
            (Vector3.up * followHeight);

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
}