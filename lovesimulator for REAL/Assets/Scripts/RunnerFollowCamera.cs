using UnityEngine;

public class RunnerFollowCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public HamsterBallController runner;

    [Header("Camera")]
    [Tooltip("Camera whose FOV will be adjusted. If null, uses Camera on this object or children.")]
    public Camera targetCamera;

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
    [Tooltip("Small sustained camera changes based on current speed. You can turn this off if you want only FOV kick.")]
    public bool useSpeedEffects = true;
    public float speedForMaxEffect = 35f;
    public float extraDistanceAtMaxSpeed = 1.0f;
    public float extraHeightAtMaxSpeed = 0.2f;
    public float extraLookAheadAtMaxSpeed = 0.8f;

    [Header("FOV")]
    public float baseFov = 60f;

    [Header("Default Event FOV Kick")]
    [Tooltip("Default extra FOV added by a kick event.")]
    public float defaultKickAmount = 10f;

    [Tooltip("Default time the kick stays near full strength before releasing.")]
    public float defaultKickHoldTime = 0.20f;

    [Tooltip("Default speed when expanding into the kick.")]
    public float defaultKickInSpeed = 14f;

    [Tooltip("Default speed when settling back down after the hold.")]
    public float defaultKickOutSpeed = 5f;

    [Header("Debug")]
    public bool drawDebug = true;

    private Vector3 positionVelocity;
    private Vector3 smoothedHeading = Vector3.forward;
    private Vector3 smoothedLookAhead = Vector3.zero;
    private float currentYaw;
    private float currentSlopePitchOffset;

    private float currentKickAmount;
    private float targetKickAmount;
    private float kickHoldTimer;
    private float kickInSpeed;
    private float kickOutSpeed;

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

        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();

            if (targetCamera == null)
                targetCamera = GetComponentInChildren<Camera>();
        }

        Vector3 initialForward = target.forward;
        initialForward.y = 0f;

        if (initialForward.sqrMagnitude < 0.0001f)
            initialForward = Vector3.forward;

        smoothedHeading = initialForward.normalized;
        currentYaw = Mathf.Atan2(smoothedHeading.x, smoothedHeading.z) * Mathf.Rad2Deg;

        if (targetCamera != null)
            targetCamera.fieldOfView = baseFov;

        SnapImmediately();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        UpdateHeading();
        UpdateCameraPositionAndRotation();
        UpdateFov();
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

    private void UpdateFov()
    {
        if (targetCamera == null)
            return;

        if (kickHoldTimer > 0f)
        {
            kickHoldTimer -= Time.deltaTime;
            targetKickAmount = Mathf.Max(targetKickAmount, currentKickAmount);
        }
        else
        {
            targetKickAmount = 0f;
        }

        float lerpSpeed = currentKickAmount < targetKickAmount ? kickInSpeed : kickOutSpeed;

        currentKickAmount = Mathf.Lerp(
            currentKickAmount,
            targetKickAmount,
            lerpSpeed * Time.deltaTime
        );

        targetCamera.fieldOfView = baseFov + currentKickAmount;
    }

    public void TriggerFovKick(float extraFov, float holdTime, float inSpeed, float outSpeed)
    {
        targetKickAmount = Mathf.Max(targetKickAmount, extraFov);
        kickHoldTimer = Mathf.Max(kickHoldTimer, holdTime);
        kickInSpeed = Mathf.Max(0.01f, inSpeed);
        kickOutSpeed = Mathf.Max(0.01f, outSpeed);
    }

    public void TriggerFovKick()
    {
        TriggerFovKick(
            defaultKickAmount,
            defaultKickHoldTime,
            defaultKickInSpeed,
            defaultKickOutSpeed
        );
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

        Vector3 projectedUpOnGroundPlane = Vector3.ProjectOnPlane(Vector3.up, groundNormal);

        if (projectedUpOnGroundPlane.sqrMagnitude < 0.0001f)
            return 0f;

        float slopeAmount = Vector3.Dot(forward, projectedUpOnGroundPlane.normalized);
        float targetOffset = -slopeAmount * maxSlopePitchOffset;

        return targetOffset;
    }

    private void OnGUI()
    {
        if (!drawDebug)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.normal.textColor = Color.white;

        if (targetCamera != null)
            GUI.Label(new Rect(20, 140, 500, 30), $"FOV: {targetCamera.fieldOfView:F1}", style);

        GUI.Label(new Rect(20, 165, 500, 30), $"Kick: {currentKickAmount:F1}", style);
        GUI.Label(new Rect(20, 190, 500, 30), $"Kick Hold: {kickHoldTimer:F2}", style);
    }
}