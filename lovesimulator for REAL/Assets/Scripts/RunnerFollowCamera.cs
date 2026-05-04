using UnityEngine;

public class RunnerFollowCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public HamsterBallController runner;

    [Header("Camera")]
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

    [Header("Turning Juice - Camera Bank")]
    public bool useTurnBank = true;
    public float maxBankAngle = 10f;
    public float bankSmoothSpeed = 8f;
    public float bankReturnSpeed = 6f;

    [Header("Jump Juice")]
    public bool useJumpCamera = true;
    public float airborneHeightBonus = 0.55f;
    public float airborneLookAheadBonus = 0.4f;
    public float jumpSmoothSpeed = 5f;

    [Header("Landing Bounce")]
    public bool useLandingBounce = true;

    [Tooltip("Minimum downward landing speed needed to trigger bounce.")]
    public float landingMinFallSpeed = 8f;

    [Tooltip("Downward speed that counts as maximum landing impact.")]
    public float landingMaxFallSpeed = 45f;

    [Tooltip("How much the camera dips down on a light landing.")]
    public float landingMinDip = 0.08f;

    [Tooltip("How much the camera dips down on a hard landing.")]
    public float landingMaxDip = 0.65f;

    [Tooltip("How much extra shake a hard landing adds.")]
    public float landingMaxShake = 0.18f;

    [Tooltip("How quickly the landing dip happens.")]
    public float landingDipInSpeed = 28f;

    [Tooltip("How quickly the camera returns after the dip.")]
    public float landingRecoverSpeed = 10f;

    [Header("Speed Effects")]
    public bool useSpeedEffects = true;
    public float speedForMaxEffect = 35f;
    public float extraDistanceAtMaxSpeed = 1.0f;
    public float extraHeightAtMaxSpeed = 0.2f;
    public float extraLookAheadAtMaxSpeed = 0.8f;

    [Header("FOV")]
    public float baseFov = 60f;

    [Header("Default Event FOV Kick")]
    public float defaultKickAmount = 10f;
    public float defaultKickHoldTime = 0.20f;
    public float defaultKickInSpeed = 14f;
    public float defaultKickOutSpeed = 5f;

    [Header("Dash Camera Juice")]
    public float dashFovKickAmount = 9f;
    public float dashFovHoldTime = 0.22f;
    public float dashFovInSpeed = 14f;
    public float dashFovOutSpeed = 5f;
    public float dashShakeIntensity = 0.12f;
    public float dashShakeDuration = 0.12f;

    [Header("Attack Attach Camera Juice")]
    public float attackAttachFovKickAmount = 14f;
    public float attackAttachFovHoldTime = 0.08f;
    public float attackAttachFovInSpeed = 22f;
    public float attackAttachFovOutSpeed = 9f;
    public float attackAttachShakeIntensity = 0.08f;
    public float attackAttachShakeDuration = 0.08f;

    [Header("Attack Hit Stop Camera")]
    public float attackStopCameraDuration = 0.16f;
    public float attackStopFollowDistance = 6.2f;
    public float attackStopFollowHeight = 3.0f;
    public float attackStopLookAheadDistance = 1.0f;
    public float attackStopPositionSmoothTime = 0.08f;
    public bool lockHeadingDuringAttackStop = true;

    [Header("Chain Launch Camera Juice")]
    public float chainLaunchFovKickAmount = 11f;
    public float chainLaunchFovHoldTime = 0.22f;
    public float chainLaunchFovInSpeed = 14f;
    public float chainLaunchFovOutSpeed = 5f;
    public float chainLaunchShakeIntensity = 0.28f;
    public float chainLaunchShakeDuration = 0.18f;

    [Header("Camera Shake")]
    public bool useCameraShake = true;

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

    private float currentBank;
    private float currentJumpOffset;
    private float currentJumpLookAheadOffset;

    private float shakeTimer;
    private float shakeDuration;
    private float shakeIntensity;

    private float attackStopTimer;
    private float lockedAttackYaw;

    private float landingBounceOffset;
    private float landingBounceTarget;
    private float landingRecoverTimer;

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

        if (attackStopTimer > 0f)
        {
            attackStopTimer -= Time.deltaTime;

            if (!lockHeadingDuringAttackStop)
                UpdateHeading();
            else
                currentYaw = lockedAttackYaw;
        }
        else
        {
            UpdateHeading();
        }

        UpdateCameraPositionAndRotation();
        UpdateFov();
    }

    private void UpdateHeading()
    {
        Vector3 desiredHeading;

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
        float currentSmoothTime = positionSmoothTime;

        if (attackStopTimer > 0f)
        {
            currentFollowDistance = attackStopFollowDistance;
            currentFollowHeight = attackStopFollowHeight;
            currentLookAheadDistance = attackStopLookAheadDistance;
            currentSmoothTime = attackStopPositionSmoothTime;
        }

        if (useSpeedEffects)
        {
            currentFollowDistance += extraDistanceAtMaxSpeed * speed01;
            currentFollowHeight += extraHeightAtMaxSpeed * speed01;
            currentLookAheadDistance += extraLookAheadAtMaxSpeed * speed01;
        }

        UpdateJumpJuice(ref currentFollowHeight, ref currentLookAheadDistance);
        UpdateLandingBounce(ref currentFollowHeight);
        UpdateBanking();

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
            currentSmoothTime
        );

        ApplyCameraShake();

        Quaternion lookRotation = Quaternion.LookRotation(targetLookPoint - transform.position, Vector3.up);
        Vector3 euler = lookRotation.eulerAngles;

        float targetSlopePitch = GetSlopePitchOffset();

        currentSlopePitchOffset = Mathf.Lerp(
            currentSlopePitchOffset,
            targetSlopePitch,
            slopePitchSmoothSpeed * Time.deltaTime
        );

        transform.rotation = Quaternion.Euler(
            fixedPitch + currentSlopePitchOffset,
            euler.y,
            currentBank
        );
    }

    public void PlayLandingBounce(float downwardSpeed)
    {
        if (!useLandingBounce)
            return;

        if (downwardSpeed < landingMinFallSpeed)
            return;

        float impact01 = Mathf.InverseLerp(
            landingMinFallSpeed,
            landingMaxFallSpeed,
            downwardSpeed
        );

        impact01 = Mathf.Clamp01(impact01);

        float dip = Mathf.Lerp(landingMinDip, landingMaxDip, impact01);

        landingBounceTarget = -dip;
        landingRecoverTimer = 0.08f;

        if (useCameraShake && landingMaxShake > 0f)
            TriggerShake(landingMaxShake * impact01, 0.10f + impact01 * 0.08f);
    }

    private void UpdateLandingBounce(ref float followHeight)
    {
        if (!useLandingBounce)
            return;

        if (landingRecoverTimer > 0f)
        {
            landingRecoverTimer -= Time.deltaTime;

            landingBounceOffset = Mathf.Lerp(
                landingBounceOffset,
                landingBounceTarget,
                landingDipInSpeed * Time.deltaTime
            );
        }
        else
        {
            landingBounceTarget = 0f;

            landingBounceOffset = Mathf.Lerp(
                landingBounceOffset,
                0f,
                landingRecoverSpeed * Time.deltaTime
            );
        }

        followHeight += landingBounceOffset;
    }

    public void PlayAttackHitStopCamera(float duration)
    {
        attackStopTimer = Mathf.Max(attackStopTimer, duration);

        lockedAttackYaw = currentYaw;
        positionVelocity = Vector3.zero;
    }

    private void UpdateJumpJuice(ref float height, ref float lookAhead)
    {
        if (!useJumpCamera || runner == null)
            return;

        bool grounded = runner.IsGrounded();

        float targetHeightOffset = grounded ? 0f : airborneHeightBonus;
        float targetLookAheadOffset = grounded ? 0f : airborneLookAheadBonus;

        currentJumpOffset = Mathf.Lerp(
            currentJumpOffset,
            targetHeightOffset,
            jumpSmoothSpeed * Time.deltaTime
        );

        currentJumpLookAheadOffset = Mathf.Lerp(
            currentJumpLookAheadOffset,
            targetLookAheadOffset,
            jumpSmoothSpeed * Time.deltaTime
        );

        height += currentJumpOffset;
        lookAhead += currentJumpLookAheadOffset;
    }

    private void UpdateBanking()
    {
        if (!useTurnBank || runner == null)
        {
            currentBank = Mathf.Lerp(currentBank, 0f, bankReturnSpeed * Time.deltaTime);
            return;
        }

        float steer = runner.GetSteerInput();
        float targetBank = -steer * maxBankAngle;

        float smooth = Mathf.Abs(steer) > 0.01f ? bankSmoothSpeed : bankReturnSpeed;

        currentBank = Mathf.Lerp(
            currentBank,
            targetBank,
            smooth * Time.deltaTime
        );
    }

    private void ApplyCameraShake()
    {
        if (!useCameraShake || shakeTimer <= 0f)
            return;

        shakeTimer -= Time.deltaTime;

        float life01 = shakeDuration <= 0.001f
            ? 0f
            : Mathf.Clamp01(shakeTimer / shakeDuration);

        float currentIntensity = shakeIntensity * life01;

        Vector3 shakeOffset = Random.insideUnitSphere * currentIntensity;
        shakeOffset.y *= 0.5f;

        transform.position += shakeOffset;

        if (shakeTimer <= 0f)
        {
            shakeTimer = 0f;
            shakeDuration = 0f;
            shakeIntensity = 0f;
        }
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

    public void TriggerShake(float intensity, float duration)
    {
        if (!useCameraShake)
            return;

        shakeIntensity = Mathf.Max(shakeIntensity, intensity);
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeTimer = Mathf.Max(shakeTimer, duration);
    }

    public void PlayDashCameraJuice()
    {
        TriggerFovKick(
            dashFovKickAmount,
            dashFovHoldTime,
            dashFovInSpeed,
            dashFovOutSpeed
        );

        TriggerShake(dashShakeIntensity, dashShakeDuration);
    }

    public void PlayAttackAttachCameraJuice()
    {
        TriggerFovKick(
            attackAttachFovKickAmount,
            attackAttachFovHoldTime,
            attackAttachFovInSpeed,
            attackAttachFovOutSpeed
        );

        TriggerShake(attackAttachShakeIntensity, attackAttachShakeDuration);
    }

    public void PlayChainLaunchCameraJuice()
    {
        TriggerFovKick(
            chainLaunchFovKickAmount,
            chainLaunchFovHoldTime,
            chainLaunchFovInSpeed,
            chainLaunchFovOutSpeed
        );

        TriggerShake(chainLaunchShakeIntensity, chainLaunchShakeDuration);
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
        return -slopeAmount * maxSlopePitchOffset;
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
        GUI.Label(new Rect(20, 215, 500, 30), $"Bank: {currentBank:F1}", style);
        GUI.Label(new Rect(20, 240, 500, 30), $"Shake: {shakeTimer:F2}", style);
    }
}