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
    [Tooltip("How quickly the camera heading follows the runner's forward direction.")]
    public float yawSmoothSpeed = 6f;

    [Tooltip("Minimum heading strength before the camera keeps its previous heading.")]
    public float minHeadingMagnitude = 0.1f;

    [Header("Look Ahead")]
    public float lookAheadDistance = 2.0f;
    public float lookAheadSmoothSpeed = 5f;

    [Header("Banked Camera Frame")]
    [Tooltip("If true, the camera follow frame uses the runner/ground up direction instead of world up.")]
    public bool useRoadBankFollow = true;

    [Tooltip("When grounded, use the ground normal from the runner. This is best for banking with the road.")]
    public bool useGroundNormalWhenGrounded = true;

    [Tooltip("When not grounded / off the road, smoothly return the camera to normal world up.")]
    public bool returnToWorldUpWhenAirborne = true;

    [Header("Banked Camera Smoothing")]
    [Tooltip("How quickly the camera up direction follows the runner/ground up.")]
    public float cameraUpSmoothSpeed = 8f;

    [Tooltip("How quickly the camera returns to world up / target up when leaving ground.")]
    public float cameraUpReturnSpeed = 5f;

    [Tooltip("Tiny up-direction changes below this angle are ignored to prevent micro-jitter.")]
    public float cameraUpDeadZoneDegrees = 0.1f;

    [Tooltip("How quickly the banked camera forward direction stabilizes.")]
    public float cameraForwardSmoothSpeed = 10f;

    [Header("Turning Juice - Camera Bank")]
    [Tooltip("Optional extra camera roll from steering. Turn this off while testing the pure banked camera.")]
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

    private Vector3 smoothedCameraUp = Vector3.up;
    private Vector3 smoothedCameraForward = Vector3.forward;
    private Vector3 currentFrameCameraUp = Vector3.up;

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
    private Vector3 lockedAttackHeading = Vector3.forward;

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

        Vector3 cameraUp = GetCameraUpRaw();

        Vector3 initialSourceForward = target.forward;

        if (runner != null)
        {
            Vector3 runnerVelocity = runner.GetHorizontalVelocity();

            if (runnerVelocity.sqrMagnitude > 0.001f)
                initialSourceForward = runnerVelocity.normalized;
            else
                initialSourceForward = runner.transform.forward;
        }

        Vector3 initialForward = Vector3.ProjectOnPlane(initialSourceForward, cameraUp);

        if (initialForward.sqrMagnitude < 0.0001f)
            initialForward = Vector3.ProjectOnPlane(Vector3.forward, cameraUp);

        if (initialForward.sqrMagnitude < 0.0001f)
            initialForward = Vector3.forward;

        smoothedHeading = initialForward.normalized;
        smoothedCameraForward = smoothedHeading;
        smoothedCameraUp = cameraUp.sqrMagnitude > 0.0001f ? cameraUp.normalized : Vector3.up;
        lockedAttackHeading = smoothedHeading;

        if (targetCamera != null)
            targetCamera.fieldOfView = baseFov;

        SnapImmediately();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        // IMPORTANT:
        // Calculate the banked/world up ONCE per camera frame.
        // Before this, UpdateHeading() and UpdateCameraPositionAndRotation()
        // both updated smoothedCameraUp separately, which could create micro jitter.
        currentFrameCameraUp = GetSmoothedCameraUp();

        if (attackStopTimer > 0f)
        {
            attackStopTimer -= Time.deltaTime;

            if (lockHeadingDuringAttackStop)
                smoothedHeading = lockedAttackHeading;
            else
                UpdateHeading(currentFrameCameraUp);
        }
        else
        {
            UpdateHeading(currentFrameCameraUp);
        }

        UpdateCameraPositionAndRotation(currentFrameCameraUp);
        UpdateFov();
    }

    private float ExpLerp(float speed)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0.001f, speed) * Time.deltaTime);
    }

    private void UpdateHeading(Vector3 cameraUp)
    {
        Vector3 desiredHeading = Vector3.zero;

        // IMPORTANT:
        // Do NOT use target.forward as the main camera heading.
        // Your target is now on the visual/model, which can lean/rotate during banked turns.
        // That makes the camera slide into an over-the-shoulder angle.
        //
        // Instead, use the runner's actual movement direction first.
        if (runner != null)
        {
            Vector3 velocity = runner.GetHorizontalVelocity();
            desiredHeading = Vector3.ProjectOnPlane(velocity, cameraUp);
        }

        // If velocity is too small, use the player/root forward as a fallback.
        // This is more stable than the visual target's forward.
        if (desiredHeading.sqrMagnitude < (minHeadingMagnitude * minHeadingMagnitude) && runner != null)
            desiredHeading = Vector3.ProjectOnPlane(runner.transform.forward, cameraUp);

        // Only use target.forward as a last resort.
        if (desiredHeading.sqrMagnitude < (minHeadingMagnitude * minHeadingMagnitude) && target != null)
            desiredHeading = Vector3.ProjectOnPlane(target.forward, cameraUp);

        if (desiredHeading.sqrMagnitude < (minHeadingMagnitude * minHeadingMagnitude))
            desiredHeading = Vector3.ProjectOnPlane(smoothedHeading, cameraUp);

        if (desiredHeading.sqrMagnitude < (minHeadingMagnitude * minHeadingMagnitude))
            desiredHeading = Vector3.ProjectOnPlane(Vector3.forward, cameraUp);

        if (desiredHeading.sqrMagnitude < 0.0001f)
            desiredHeading = Vector3.Cross(cameraUp, Vector3.right);

        if (desiredHeading.sqrMagnitude < 0.0001f)
            desiredHeading = Vector3.forward;

        desiredHeading.Normalize();

        smoothedHeading = Vector3.Slerp(
            smoothedHeading,
            desiredHeading,
            ExpLerp(yawSmoothSpeed)
        ).normalized;
    }

    private void UpdateCameraPositionAndRotation(Vector3 cameraUp)
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
        UpdateTurnBanking();

        Vector3 cameraForward = GetCameraForwardOnBankedPlane(cameraUp);
        smoothedCameraForward = cameraForward;

        Vector3 rawLookAhead = cameraForward * currentLookAheadDistance;

        smoothedLookAhead = rawLookAhead;

        Vector3 targetLookPoint =
            target.position +
            cameraUp * lookHeightOffset +
            smoothedLookAhead;

        Vector3 desiredPosition =
            target.position -
            cameraForward * currentFollowDistance +
            cameraUp * currentFollowHeight;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            currentSmoothTime
        );

        ApplyCameraShake(cameraUp);

        Vector3 lookDirection = targetLookPoint - transform.position;

        if (lookDirection.sqrMagnitude < 0.0001f)
            lookDirection = cameraForward;

        Quaternion baseRotation = Quaternion.LookRotation(
            lookDirection.normalized,
            cameraUp
        );

        Quaternion turnBankRotation = Quaternion.AngleAxis(
            currentBank,
            Vector3.forward
        );

        transform.rotation = baseRotation * turnBankRotation;
    }

    private Vector3 GetCameraUpRaw()
    {
        if (!useRoadBankFollow)
            return Vector3.up;

        bool groundedOnRoad = runner != null && runner.IsGrounded();

        if (groundedOnRoad && useGroundNormalWhenGrounded)
        {
            Vector3 groundUp = runner.GetGroundNormal();

            if (groundUp.sqrMagnitude > 0.0001f)
                return groundUp.normalized;
        }

        // When airborne or no longer grounded on the spline/road,
        // return to clean world up instead of inheriting the player's banked up.
        if (returnToWorldUpWhenAirborne)
            return Vector3.up;

        // Optional fallback if you ever want the old behavior back.
        if (target != null)
        {
            Vector3 targetUp = target.up;

            if (targetUp.sqrMagnitude > 0.0001f)
                return targetUp.normalized;
        }

        return Vector3.up;
    }

    private Vector3 GetSmoothedCameraUp()
    {
        Vector3 targetUp = GetCameraUpRaw();

        if (targetUp.sqrMagnitude < 0.0001f)
            targetUp = Vector3.up;

        targetUp.Normalize();

        if (smoothedCameraUp.sqrMagnitude < 0.0001f)
            smoothedCameraUp = targetUp;

        bool groundedOnRoad = runner != null && runner.IsGrounded();

        float angle = Vector3.Angle(smoothedCameraUp, targetUp);

        // Dead zone prevents tiny ground-normal changes from constantly rolling the camera.
        if (angle <= cameraUpDeadZoneDegrees)
            return smoothedCameraUp.normalized;

        float smoothSpeed = groundedOnRoad ? cameraUpSmoothSpeed : cameraUpReturnSpeed;

        smoothedCameraUp = Vector3.Slerp(
            smoothedCameraUp,
            targetUp,
            ExpLerp(smoothSpeed)
        ).normalized;

        return smoothedCameraUp;
    }

    private Vector3 GetCameraForwardOnBankedPlane(Vector3 cameraUp)
    {
        Vector3 desiredHeading = Vector3.ProjectOnPlane(smoothedHeading, cameraUp);

        if (desiredHeading.sqrMagnitude < 0.0001f && target != null)
            desiredHeading = Vector3.ProjectOnPlane(target.forward, cameraUp);

        if (desiredHeading.sqrMagnitude < 0.0001f)
            desiredHeading = Vector3.ProjectOnPlane(Vector3.forward, cameraUp);

        if (desiredHeading.sqrMagnitude < 0.0001f)
            desiredHeading = Vector3.Cross(cameraUp, Vector3.right);

        if (desiredHeading.sqrMagnitude < 0.0001f)
            desiredHeading = Vector3.forward;

        desiredHeading.Normalize();

        Vector3 cameraRight = Vector3.Cross(cameraUp, desiredHeading);

        if (cameraRight.sqrMagnitude < 0.0001f)
            cameraRight = Vector3.right;

        cameraRight.Normalize();

        Vector3 cleanForward = Vector3.Cross(cameraRight, cameraUp);

        if (cleanForward.sqrMagnitude < 0.0001f)
            cleanForward = desiredHeading;

        return cleanForward.normalized;
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
                ExpLerp(landingDipInSpeed)
            );
        }
        else
        {
            landingBounceTarget = 0f;

            landingBounceOffset = Mathf.Lerp(
                landingBounceOffset,
                0f,
                ExpLerp(landingRecoverSpeed)
            );
        }

        followHeight += landingBounceOffset;
    }

    public void PlayAttackHitStopCamera(float duration)
    {
        attackStopTimer = Mathf.Max(attackStopTimer, duration);

        lockedAttackHeading = smoothedHeading;
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
            ExpLerp(jumpSmoothSpeed)
        );

        currentJumpLookAheadOffset = Mathf.Lerp(
            currentJumpLookAheadOffset,
            targetLookAheadOffset,
            ExpLerp(jumpSmoothSpeed)
        );

        height += currentJumpOffset;
        lookAhead += currentJumpLookAheadOffset;
    }

    private void UpdateTurnBanking()
    {
        if (!useTurnBank || runner == null)
        {
            currentBank = Mathf.Lerp(
                currentBank,
                0f,
                ExpLerp(bankReturnSpeed)
            );

            return;
        }

        float steer = runner.GetSteerInput();
        float targetBank = -steer * maxBankAngle;

        float smooth = Mathf.Abs(steer) > 0.01f
            ? bankSmoothSpeed
            : bankReturnSpeed;

        currentBank = Mathf.Lerp(
            currentBank,
            targetBank,
            ExpLerp(smooth)
        );
    }

    private void ApplyCameraShake(Vector3 cameraUp)
    {
        if (!useCameraShake || shakeTimer <= 0f)
            return;

        shakeTimer -= Time.deltaTime;

        float life01 = shakeDuration <= 0.001f
            ? 0f
            : Mathf.Clamp01(shakeTimer / shakeDuration);

        float currentIntensity = shakeIntensity * life01;

        Vector3 shakeOffset = Random.insideUnitSphere * currentIntensity;

        // Keep shake relative to the banked camera frame instead of world Y.
        Vector3 verticalShake = Vector3.Project(shakeOffset, cameraUp) * 0.5f;
        Vector3 sideShake = Vector3.ProjectOnPlane(shakeOffset, cameraUp);

        transform.position += sideShake + verticalShake;

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

        float lerpSpeed = currentKickAmount < targetKickAmount
            ? kickInSpeed
            : kickOutSpeed;

        currentKickAmount = Mathf.Lerp(
            currentKickAmount,
            targetKickAmount,
            ExpLerp(lerpSpeed)
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
        Vector3 cameraUp = GetCameraUpRaw();

        if (cameraUp.sqrMagnitude < 0.0001f)
            cameraUp = Vector3.up;

        cameraUp.Normalize();

        smoothedCameraUp = cameraUp;

        Vector3 cameraForward = GetCameraForwardOnBankedPlane(cameraUp);

        if (cameraForward.sqrMagnitude < 0.0001f)
            cameraForward = target.forward;

        cameraForward.Normalize();

        smoothedCameraForward = cameraForward;

        float speed01 = GetSpeedPercent();

        float currentFollowDistance = followDistance;
        float currentFollowHeight = followHeight;

        if (useSpeedEffects)
        {
            currentFollowDistance += extraDistanceAtMaxSpeed * speed01;
            currentFollowHeight += extraHeightAtMaxSpeed * speed01;
        }

        Vector3 desiredPosition =
            target.position -
            cameraForward * currentFollowDistance +
            cameraUp * currentFollowHeight;

        transform.position = desiredPosition;

        Vector3 lookPoint =
            target.position +
            cameraUp * lookHeightOffset +
            cameraForward * lookAheadDistance;

        Vector3 lookDirection = lookPoint - transform.position;

        if (lookDirection.sqrMagnitude < 0.0001f)
            lookDirection = cameraForward;

        transform.rotation = Quaternion.LookRotation(
            lookDirection.normalized,
            cameraUp
        );

        positionVelocity = Vector3.zero;
        smoothedLookAhead = cameraForward * lookAheadDistance;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebug || target == null)
            return;

        Vector3 cameraUp = Application.isPlaying ? smoothedCameraUp : Vector3.up;
        Vector3 cameraForward = Application.isPlaying ? smoothedCameraForward : target.forward;

        Gizmos.color = Color.white;
        Gizmos.DrawLine(
            target.position,
            target.position + cameraUp * lookHeightOffset
        );

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            target.position,
            target.position + cameraForward * 3f
        );

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(
            target.position + cameraUp * lookHeightOffset + smoothedLookAhead,
            0.12f
        );
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
        GUI.Label(new Rect(20, 215, 500, 30), $"Turn Bank: {currentBank:F1}", style);
        GUI.Label(new Rect(20, 240, 500, 30), $"Shake: {shakeTimer:F2}", style);
    }
}