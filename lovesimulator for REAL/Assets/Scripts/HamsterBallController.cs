using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class HamsterBallController : MonoBehaviour
{
    [Header("Respawn")]
    [Tooltip("Where the player is placed after falling out of bounds.")]
    public Transform respawnPoint;

    [Tooltip("If the player falls below this Y value, they respawn.")]
    public float respawnYThreshold = -20f;

    [Header("References")]
    [Tooltip("Visual child object that tilts and faces movement. Leave null if not used.")]
    public Transform visualRoot;

    [Tooltip("Love meter used for dash cost and refill.")]
    public LoveMeter loveMeter;

    [Tooltip("Camera script used for FOV kick effects.")]
    public RunnerFollowCamera followCamera;

    [Tooltip("Input reader for move / jump / dash / attack.")]
    public PlayerGameplayInput gameplayInput;

    [Header("Forward Movement")]
    [Tooltip("How strongly the player is pushed forward every physics step.")]
    public float forwardAcceleration = 30f;

    [Tooltip("Maximum normal grounded speed.")]
    public float maxGroundSpeed = 35f;

    [Tooltip("Maximum normal airborne speed.")]
    public float maxAirSpeed = 28f;

    [Header("Speed Limiting")]
    [Tooltip("If true, overspeed is reduced gradually. If false, overspeed is clamped more directly.")]
    public bool useSmoothOverSpeedDecay = false;

    [Tooltip("How fast overspeed is reduced when smooth decay is enabled.")]
    public float overSpeedDeceleration = 55f;

    [Tooltip("If true, normal forward drive is reduced once the player is already overspeed.")]
    public bool suppressDriveWhileOverspeed = true;

    [Header("Runner Steering")]
    [Tooltip("Maximum steering angle in degrees. Smaller = straighter. Bigger = wider curves.")]
    public float maxSteerAngleDegrees = 12f;

    [Tooltip("If true, steering becomes tighter at high speed and looser at lower speed.")]
    public bool useSpeedScaledSteering = false;

    [Tooltip("Steering angle used at lower speed when speed scaling is enabled.")]
    public float lowSpeedSteerAngleDegrees = 16f;

    [Tooltip("Steering angle used at high speed when speed scaling is enabled.")]
    public float highSpeedSteerAngleDegrees = 8f;

    [Tooltip("Speed used as the reference for reaching the high-speed steering angle.")]
    public float speedForMinSteer = 35f;

    [Tooltip("How much current horizontal speed is preserved while steering.")]
    public float turnSpeedPreservation = 0.96f;

    [Header("Directional Grip")]
    [Tooltip("How strongly grounded sideways drift is removed.")]
    public float groundLateralGrip = 40f;

    [Tooltip("How strongly airborne sideways drift is removed.")]
    public float airLateralGrip = 14f;

    [Header("Facing")]
    [Tooltip("Minimum horizontal speed needed before the player's facing updates from movement direction.")]
    public float minSpeedToUpdateFacing = 0.25f;

    [Header("Jump")]
    [Tooltip("Desired jump height.")]
    public float jumpHeight = 1.8f;

    [Tooltip("Time from jump start to apex.")]
    public float timeToApex = 0.28f;

    [Tooltip("Time from apex back down.")]
    public float timeToDescend = 0.22f;

    [Tooltip("Minimum time between jumps.")]
    public float jumpCooldown = 0.10f;

    [Tooltip("How much the jump follows the ground normal instead of straight up.")]
    [Range(0f, 1f)] public float jumpFromGroundNormalPercent = 0.55f;

    [Tooltip("How long ground stick is disabled after jumping.")]
    public float jumpDetachTime = 0.10f;

    [Header("Free Dash")]
    [Tooltip("If true, the love meter must be full to dash.")]
    public bool requireFullLoveToDash = true;

    [Tooltip("Love cost when full-love requirement is turned off.")]
    public float dashLoveCost = 100f;

    [Tooltip("Minimum speed applied when a dash begins.")]
    public float dashStartSpeed = 70f;

    [Tooltip("Extra acceleration during dash.")]
    public float dashAcceleration = 120f;

    [Tooltip("Maximum speed allowed during dash.")]
    public float dashMaxSpeed = 90f;

    [Tooltip("How long the dash lasts.")]
    public float dashDuration = 0.18f;

    [Tooltip("Minimum time before another dash can begin.")]
    public float dashCooldown = 0.35f;

    [Tooltip("If true, steering input is ignored during free dash.")]
    public bool lockSteeringDuringDash = true;

    [Header("Target Attack / Chain Dash")]
    [Tooltip("Layers searched when looking for chain dash targets.")]
    public LayerMask chainTargetLayers = ~0;

    [Tooltip("How far away a target can be and still be considered for attack.")]
    public float chainTargetSearchRadius = 14f;

    [Tooltip("Maximum front-facing angle allowed when choosing a chain target.")]
    [Range(1f, 180f)] public float chainTargetMaxAngle = 50f;

    [Tooltip("Speed used while being pulled toward a chain target.")]
    public float chainPullSpeed = 120f;

    [Tooltip("How quickly facing updates while being pulled toward a chain target.")]
    public float chainPullFacingSpeed = 18f;

    [Tooltip("If true, hitting a chain target refills the love meter.")]
    public bool refillLoveOnChainHit = true;

    [Tooltip("If true, chain attack ignores the normal love requirement.")]
    public bool chainDashIgnoresLoveRequirement = true;

    [Tooltip("Extra camera FOV kick when launched out of a chain target.")]
    public float chainLaunchCameraKickAmount = 11f;

    [Tooltip("Extra distance to push the player out of the target before the launch begins.")]
    public float chainLaunchExitPadding = 0.35f;

    [Tooltip("How long attack target re-locking is prevented after a chain launch.")]
    public float chainRetargetLockoutDuration = 0.18f;

    [Header("Shared Speed Camera Kick")]
    [Tooltip("If true, dashes and boosts trigger the shared speed camera kick.")]
    public bool triggerSpeedCameraKick = true;

    [Tooltip("Extra FOV added by the shared speed kick.")]
    public float speedCameraKickAmount = 9f;

    [Tooltip("How long the shared speed kick is held before relaxing.")]
    public float speedCameraKickHoldTime = 0.22f;

    [Tooltip("How quickly the shared speed kick ramps in.")]
    public float speedCameraKickInSpeed = 14f;

    [Tooltip("How quickly the shared speed kick ramps out.")]
    public float speedCameraKickOutSpeed = 5f;

    [Header("Temporary Speed Boost")]
    [Tooltip("Temporary acceleration bonus applied by boost zones or other effects.")]
    public float boostAccelerationBonus = 0f;

    [Tooltip("Temporary max-speed bonus applied by boost zones or other effects.")]
    public float boostMaxSpeedBonus = 0f;

    [Tooltip("How long the current temporary boost remains active.")]
    public float boostTimer = 0f;

    [Header("Grounding")]
    [Tooltip("Maximum walkable ground angle.")]
    public float maxGroundAngle = 60f;

    [Tooltip("How long grounded state lingers after losing contact.")]
    public float groundedMemory = 0.10f;

    [Header("Ground Stick")]
    [Tooltip("Downward force used to keep the player attached to the ground.")]
    public float groundStickForce = 35f;

    [Tooltip("Extra grace period where ground stick still applies after losing contact.")]
    public float groundStickGraceTime = 0.12f;

    [Tooltip("Maximum upward-away speed that ground stick is allowed to cancel.")]
    public float maxStickAwaySpeed = 6f;

    [Header("Visuals")]
    [Tooltip("Vertical offset for the visual model.")]
    public float rideHeight = 0.9f;

    [Tooltip("How quickly the visual model rotates toward travel direction.")]
    public float visualFacingSmooth = 12f;

    [Tooltip("How much the visual model tilts left/right from steering input.")]
    public float visualTurnLean = 22f;

    [Tooltip("Minimum movement speed before the visual model starts using live travel direction.")]
    public float minVisualSpeedForFacing = 0.75f;

    [Header("Debug")]
    [Tooltip("If true, draws on-screen speed debug.")]
    public bool showSpeedDebug = true;

    private Rigidbody rb;
    private SphereCollider sphereCol;

    private float steerInput;
    private bool jumpPressed;
    private bool dashPressed;
    private bool attackPressed;

    private float facingYaw;
    private float jumpTimer;
    private float jumpDetachTimer;
    private float groundedTimer;
    private float stickGraceTimer;

    private float dashTimer;
    private float dashCooldownTimer;

    private bool isGrounded;
    private Vector3 lastGroundNormal = Vector3.up;

    private Vector3 smoothedVisualForward = Vector3.forward;
    private Vector3 lastStableMoveDirection = Vector3.forward;

    private ChainDashTarget lockedChainTarget;
    private ChainDashTarget lastPreviewedChainTarget;
    private ChainDashTarget activeChainTarget;
    private bool isChainDashing;
    private bool isInChainHitStop;
    private Coroutine chainHitRoutine;
    private float chainRetargetLockoutTimer;

    private float JumpLaunchSpeed => (2f * jumpHeight) / Mathf.Max(0.01f, timeToApex);
    private float RiseGravity => (2f * jumpHeight) / Mathf.Max(0.01f, timeToApex * timeToApex);
    private float FallGravity => (2f * jumpHeight) / Mathf.Max(0.01f, timeToDescend * timeToDescend);

    public bool IsDashing => dashTimer > 0f || isChainDashing || isInChainHitStop;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sphereCol = GetComponent<SphereCollider>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (loveMeter == null)
            loveMeter = GetComponentInParent<LoveMeter>();

        if (followCamera == null)
            followCamera = FindAnyObjectByType<RunnerFollowCamera>();

        if (gameplayInput == null)
            gameplayInput = GetComponent<PlayerGameplayInput>();

        facingYaw = transform.eulerAngles.y;

        Vector3 startForward = transform.forward;
        startForward.y = 0f;
        if (startForward.sqrMagnitude < 0.001f)
            startForward = Vector3.forward;

        startForward.Normalize();
        smoothedVisualForward = startForward;
        lastStableMoveDirection = startForward;
    }

    private void Update()
    {
        if (gameplayInput != null)
        {
            steerInput = gameplayInput.MoveInput.x;

            if (gameplayInput.JumpPressed)
                jumpPressed = true;

            if (gameplayInput.DashPressed)
                dashPressed = true;

            if (gameplayInput.AttackPressed)
                attackPressed = true;
        }
        else
        {
            steerInput = 0f;
        }

        UpdateChainTargetPreview();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        jumpTimer -= dt;
        jumpDetachTimer -= dt;
        groundedTimer -= dt;
        stickGraceTimer -= dt;
        dashTimer -= dt;
        dashCooldownTimer -= dt;
        chainRetargetLockoutTimer -= dt;

        if (chainRetargetLockoutTimer < 0f)
            chainRetargetLockoutTimer = 0f;

        CheckRespawn();

        if (isInChainHitStop)
        {
            rb.linearVelocity = Vector3.zero;
            jumpPressed = false;
            dashPressed = false;
            attackPressed = false;
            return;
        }

        isGrounded = groundedTimer > 0f;

        HandleFreeDash();
        HandleTargetAttack();

        ApplyDrive(dt);
        ApplyDirectionalGrip(dt);
        UpdateFacingFromMovement();
        ApplyGroundStick();
        ApplyJumpGravity();
        HandleJump();

        jumpPressed = false;
        dashPressed = false;
        attackPressed = false;

        if (groundedTimer <= 0f)
            isGrounded = false;

        boostTimer -= dt;
        if (boostTimer <= 0f)
        {
            boostTimer = 0f;
            boostAccelerationBonus = 0f;
            boostMaxSpeedBonus = 0f;
        }

        if (dashTimer <= 0f)
            dashTimer = 0f;

        if (dashCooldownTimer <= 0f)
            dashCooldownTimer = 0f;
    }

    private void LateUpdate()
    {
        UpdateVisuals();
    }

    private void CheckRespawn()
    {
        if (transform.position.y > respawnYThreshold)
            return;

        if (chainHitRoutine != null)
        {
            StopCoroutine(chainHitRoutine);
            chainHitRoutine = null;
        }

        ClearChainPreview();

        lockedChainTarget = null;
        activeChainTarget = null;
        isChainDashing = false;
        isInChainHitStop = false;
        chainRetargetLockoutTimer = 0f;

        Vector3 targetPos = respawnPoint != null ? respawnPoint.position : Vector3.zero;
        Quaternion targetRot = respawnPoint != null ? respawnPoint.rotation : Quaternion.identity;

        rb.position = targetPos;
        rb.rotation = targetRot;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        facingYaw = targetRot.eulerAngles.y;

        Vector3 forward = targetRot * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        forward.Normalize();
        smoothedVisualForward = forward;
        lastStableMoveDirection = forward;

        isGrounded = false;
        groundedTimer = 0f;
        stickGraceTimer = 0f;
        jumpDetachTimer = 0f;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
    }

    private float GetCurrentSteerAngleDegrees()
    {
        if (!useSpeedScaledSteering)
            return maxSteerAngleDegrees;

        float refSpeed = Mathf.Max(0.01f, speedForMinSteer);
        float speed01 = Mathf.Clamp01(GetHorizontalVelocity().magnitude / refSpeed);
        return Mathf.Lerp(lowSpeedSteerAngleDegrees, highSpeedSteerAngleDegrees, speed01);
    }

    private Vector3 GetForwardFromFacing()
    {
        Vector3 forward = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        forward.Normalize();
        return forward;
    }

    private Vector3 GetBiasedMoveDirection()
    {
        Vector3 forward = GetForwardFromFacing();
        float steerAngle = steerInput * GetCurrentSteerAngleDegrees();

        Vector3 moveDir = Quaternion.AngleAxis(steerAngle, Vector3.up) * forward;
        moveDir.y = 0f;

        if (moveDir.sqrMagnitude < 0.001f)
            return forward;

        return moveDir.normalized;
    }

    private void UpdateFacingFromMovement()
    {
        if (isChainDashing || isInChainHitStop)
            return;

        Vector3 horizontal = GetHorizontalVelocity();
        if (horizontal.sqrMagnitude < (minSpeedToUpdateFacing * minSpeedToUpdateFacing))
            return;

        horizontal.y = 0f;
        horizontal.Normalize();
        facingYaw = Mathf.Atan2(horizontal.x, horizontal.z) * Mathf.Rad2Deg;
    }

    private void HandleFreeDash()
    {
        if (!dashPressed || IsDashing || dashCooldownTimer > 0f)
            return;

        if (!CanDash())
            return;

        SpendLoveForDash();

        Vector3 horizontal = GetHorizontalVelocity();
        Vector3 dashDirection = horizontal.sqrMagnitude > 0.001f
            ? horizontal.normalized
            : GetForwardFromFacing();

        float currentSpeedAlongDash = Vector3.Dot(horizontal, dashDirection);
        float targetSpeed = Mathf.Max(dashStartSpeed, currentSpeedAlongDash);

        Vector3 newHorizontal = dashDirection * targetSpeed;
        rb.linearVelocity = new Vector3(newHorizontal.x, rb.linearVelocity.y, newHorizontal.z);

        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        TriggerSharedSpeedCameraKick();
    }

    private void HandleTargetAttack()
    {
        if (!attackPressed || IsDashing)
            return;

        ChainDashTarget target = FindBestChainTarget();
        if (target == null)
            return;

        StartChainDash(target);
    }

    private ChainDashTarget FindBestChainTarget()
    {
        Vector3 origin = transform.position;
        Vector3 aimForward = GetForwardFromFacing();

        Collider[] hits = Physics.OverlapSphere(
            origin,
            chainTargetSearchRadius,
            chainTargetLayers,
            QueryTriggerInteraction.Collide
        );

        ChainDashTarget bestTarget = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            ChainDashTarget candidate = hits[i].GetComponentInParent<ChainDashTarget>();
            if (candidate == null || !candidate.CanBeTargeted())
                continue;

            Vector3 toTarget = candidate.GetAimPosition() - origin;
            float distance = toTarget.magnitude;

            if (distance <= 0.001f)
                continue;

            Vector3 dir = toTarget / distance;
            float angle = Vector3.Angle(aimForward, dir);

            if (angle > chainTargetMaxAngle)
                continue;

            float angleScore = 1f - (angle / chainTargetMaxAngle);
            float distanceScore = 1f - Mathf.Clamp01(distance / chainTargetSearchRadius);
            float totalScore = angleScore * 3f + distanceScore;

            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private void UpdateChainTargetPreview()
    {
        if (lastPreviewedChainTarget != null)
        {
            lastPreviewedChainTarget.SetPreviewed(false);
            lastPreviewedChainTarget = null;
        }

        lockedChainTarget = null;
    }

    private void ClearChainPreview()
    {
        if (lastPreviewedChainTarget != null)
        {
            lastPreviewedChainTarget.SetPreviewed(false);
            lastPreviewedChainTarget = null;
        }
    }

    private void StartChainDash(ChainDashTarget target)
    {
        if (target == null)
            return;

        if (!chainDashIgnoresLoveRequirement && !CanDash())
            return;

        if (!chainDashIgnoresLoveRequirement)
            SpendLoveForDash();

        activeChainTarget = target;
        isChainDashing = true;
        isInChainHitStop = false;
        dashTimer = 0f;
        dashCooldownTimer = dashCooldown;

        ClearChainPreview();
        lockedChainTarget = null;

        Vector3 toAim = target.GetAimPosition() - transform.position;
        Vector3 pullDir = toAim.normalized;

        if (pullDir.sqrMagnitude < 0.001f)
            pullDir = GetForwardFromFacing();

        rb.linearVelocity = pullDir * chainPullSpeed;
        TriggerSharedSpeedCameraKick();
    }

    private void ResolveChainDashHit()
    {
        if (activeChainTarget == null)
        {
            isChainDashing = false;
            return;
        }

        ChainDashTarget hitTarget = activeChainTarget;

        isChainDashing = false;
        activeChainTarget = null;

        if (refillLoveOnChainHit && loveMeter != null)
            loveMeter.SetLove(loveMeter.maxLove);

        if (chainHitRoutine != null)
            StopCoroutine(chainHitRoutine);

        chainHitRoutine = StartCoroutine(ChainHitSequence(hitTarget));
    }

    private IEnumerator ChainHitSequence(ChainDashTarget hitTarget)
    {
        isInChainHitStop = true;
        isChainDashing = false;

        hitTarget.NotifyHit();

        Vector3 aimPos = hitTarget.GetAimPosition();
        rb.position = aimPos;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        float hitStop = hitTarget.GetHitStopDuration();
        if (hitStop > 0f)
            yield return new WaitForSeconds(hitStop);

        hitTarget.TriggerZoneImpact();

        Vector3 launchDir = hitTarget.GetLaunchDirection();
        if (launchDir.sqrMagnitude < 0.001f)
            launchDir = Vector3.forward;

        launchDir.Normalize();

        Vector3 flatLaunch = new Vector3(launchDir.x, 0f, launchDir.z);
        if (flatLaunch.sqrMagnitude < 0.001f)
            flatLaunch = GetForwardFromFacing();

        flatLaunch.Normalize();

        float playerRadius = 0.5f;
        if (sphereCol != null)
        {
            float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            playerRadius = sphereCol.radius * maxScale;
        }

        float exitOffset = playerRadius + chainLaunchExitPadding;

        rb.position = aimPos + launchDir * exitOffset;

        Vector3 launchVelocity = launchDir * hitTarget.GetLaunchSpeed();
        launchVelocity.y += hitTarget.GetBonusUpwardLaunch();

        rb.linearVelocity = launchVelocity;
        rb.angularVelocity = Vector3.zero;

        facingYaw = Mathf.Atan2(flatLaunch.x, flatLaunch.z) * Mathf.Rad2Deg;

        isInChainHitStop = false;

        jumpDetachTimer = Mathf.Max(jumpDetachTimer, hitTarget.GetDetachFromGroundTime());
        groundedTimer = 0f;
        stickGraceTimer = 0f;
        isGrounded = false;

        activeChainTarget = null;
        lockedChainTarget = null;
        chainRetargetLockoutTimer = chainRetargetLockoutDuration;

        TriggerFovKick(
            chainLaunchCameraKickAmount,
            speedCameraKickHoldTime,
            speedCameraKickInSpeed,
            speedCameraKickOutSpeed
        );

        chainHitRoutine = null;
    }

    private bool CanDash()
    {
        if (loveMeter == null)
            return false;

        if (requireFullLoveToDash)
            return loveMeter.currentLove >= loveMeter.maxLove;

        return loveMeter.HasEnough(dashLoveCost);
    }

    private void SpendLoveForDash()
    {
        if (loveMeter == null)
            return;

        if (requireFullLoveToDash)
            loveMeter.SetLove(0f);
        else
            loveMeter.TrySpendLove(dashLoveCost);
    }

    private void ApplyDrive(float dt)
    {
        Vector3 desiredForward = GetBiasedMoveDirection();

        float accelToApply = forwardAcceleration + boostAccelerationBonus;

        float baseMaxSpeed = isGrounded ? maxGroundSpeed : maxAirSpeed;
        float maxSpeed = baseMaxSpeed + boostMaxSpeedBonus;

        bool isUsingDashDrive = false;

        if (isChainDashing && activeChainTarget != null)
        {
            Vector3 aimPos = activeChainTarget.GetAimPosition();
            Vector3 toAim = aimPos - rb.position;
            float distance = toAim.magnitude;

            if (distance <= activeChainTarget.GetArriveDistance())
            {
                ResolveChainDashHit();
                return;
            }

            Vector3 pullDir = toAim / Mathf.Max(0.0001f, distance);

            rb.linearVelocity = pullDir * chainPullSpeed;

            Vector3 flatPull = new Vector3(pullDir.x, 0f, pullDir.z);
            if (flatPull.sqrMagnitude > 0.001f)
            {
                flatPull.Normalize();
                facingYaw = Mathf.Atan2(flatPull.x, flatPull.z) * Mathf.Rad2Deg;
            }

            isUsingDashDrive = true;
        }
        else if (dashTimer > 0f)
        {
            if (lockSteeringDuringDash)
            {
                desiredForward = GetHorizontalVelocity().sqrMagnitude > 0.001f
                    ? GetHorizontalVelocity().normalized
                    : GetForwardFromFacing();
            }
            else
            {
                desiredForward = GetBiasedMoveDirection();
            }

            accelToApply += dashAcceleration;
            maxSpeed = Mathf.Max(maxSpeed, dashMaxSpeed);
            isUsingDashDrive = true;

            Vector3 currentHorizontal = GetHorizontalVelocity();
            float dashSpeed = Mathf.Max(currentHorizontal.magnitude, dashStartSpeed);
            Vector3 redirected = desiredForward * dashSpeed;
            rb.linearVelocity = new Vector3(redirected.x, rb.linearVelocity.y, redirected.z);
        }

        if (!isUsingDashDrive)
        {
            Vector3 horizontalVelocity = GetHorizontalVelocity();
            float currentSpeed = horizontalVelocity.magnitude;
            bool overspeed = currentSpeed > maxSpeed;

            bool shouldApplyDrive = true;
            if (suppressDriveWhileOverspeed && overspeed)
                shouldApplyDrive = false;

            if (shouldApplyDrive)
                rb.AddForce(desiredForward * accelToApply, ForceMode.Acceleration);

            if (currentSpeed > 0.001f && Mathf.Abs(steerInput) > 0.001f)
            {
                float preservedSpeed = Mathf.Lerp(currentSpeed, Mathf.Max(currentSpeed, baseMaxSpeed), turnSpeedPreservation);
                Vector3 redirected = desiredForward * preservedSpeed;
                rb.linearVelocity = new Vector3(redirected.x, rb.linearVelocity.y, redirected.z);
            }
        }

        Vector3 newHorizontal = GetHorizontalVelocity();
        float newSpeed = newHorizontal.magnitude;
        bool stillOverspeed = newSpeed > maxSpeed;

        if (stillOverspeed)
        {
            if (useSmoothOverSpeedDecay)
            {
                float clampedSpeed = Mathf.MoveTowards(newSpeed, maxSpeed, overSpeedDeceleration * dt);
                Vector3 adjusted = newHorizontal.normalized * clampedSpeed;
                rb.linearVelocity = new Vector3(adjusted.x, rb.linearVelocity.y, adjusted.z);
            }
            else
            {
                Vector3 clamped = newHorizontal.normalized * maxSpeed;
                rb.linearVelocity = new Vector3(clamped.x, rb.linearVelocity.y, clamped.z);
            }
        }
    }

    private void ApplyDirectionalGrip(float dt)
    {
        if (isChainDashing || isInChainHitStop)
            return;

        Vector3 horizontal = GetHorizontalVelocity();
        if (horizontal.sqrMagnitude < 0.0001f)
            return;

        Vector3 forward = GetForwardFromFacing();
        float forwardSpeed = Vector3.Dot(horizontal, forward);

        Vector3 forwardVelocity = forward * forwardSpeed;
        Vector3 lateralVelocity = horizontal - forwardVelocity;

        float grip = isGrounded ? groundLateralGrip : airLateralGrip;
        Vector3 lateralReduction = Vector3.MoveTowards(lateralVelocity, Vector3.zero, grip * dt);

        Vector3 correctedHorizontal = forwardVelocity + lateralReduction;
        rb.linearVelocity = new Vector3(correctedHorizontal.x, rb.linearVelocity.y, correctedHorizontal.z);
    }

    private void ApplyGroundStick()
    {
        if (jumpDetachTimer > 0f || isInChainHitStop)
            return;

        bool shouldStick = isGrounded || stickGraceTimer > 0f;
        if (!shouldStick)
            return;

        Vector3 groundUp = lastGroundNormal.normalized;

        rb.AddForce(-groundUp * groundStickForce, ForceMode.Acceleration);

        float awaySpeed = Vector3.Dot(rb.linearVelocity, groundUp);
        if (awaySpeed > 0f && awaySpeed < maxStickAwaySpeed)
            rb.linearVelocity -= groundUp * awaySpeed;
    }

    private void ApplyJumpGravity()
    {
        if (isGrounded || isInChainHitStop)
            return;

        float gravityToApply = rb.linearVelocity.y > 0f ? RiseGravity : FallGravity;
        rb.AddForce(Vector3.down * gravityToApply, ForceMode.Acceleration);
    }

    private void HandleJump()
    {
        if (!jumpPressed || !isGrounded || jumpTimer > 0f || isInChainHitStop)
            return;

        Vector3 jumpDir = Vector3.Slerp(
            Vector3.up,
            lastGroundNormal.normalized,
            jumpFromGroundNormalPercent
        ).normalized;

        float existingAlongJump = Vector3.Dot(rb.linearVelocity, jumpDir);
        if (existingAlongJump < 0f)
            rb.linearVelocity -= jumpDir * existingAlongJump;

        rb.linearVelocity += jumpDir * JumpLaunchSpeed;

        isGrounded = false;
        groundedTimer = 0f;
        stickGraceTimer = 0f;
        jumpTimer = jumpCooldown;
        jumpDetachTimer = jumpDetachTime;
    }

    private void UpdateVisuals()
    {
        if (visualRoot == null)
            return;

        // World position above the ball.
        visualRoot.position = transform.position + Vector3.up * rideHeight;

        Vector3 horizontalVelocity = GetHorizontalVelocity();
        float horizontalSpeed = horizontalVelocity.magnitude;

        Vector3 targetForward;

        if (horizontalSpeed > minVisualSpeedForFacing)
        {
            targetForward = horizontalVelocity.normalized;
            lastStableMoveDirection = targetForward;
        }
        else
        {
            targetForward = lastStableMoveDirection;
        }

        targetForward.y = 0f;
        if (targetForward.sqrMagnitude < 0.001f)
            targetForward = GetForwardFromFacing();

        targetForward.Normalize();

        smoothedVisualForward = Vector3.Slerp(
            smoothedVisualForward,
            targetForward,
            visualFacingSmooth * Time.deltaTime
        ).normalized;

        float leanAmount = 0f;
        if ((!IsDashing || !lockSteeringDuringDash) && !isChainDashing && !isInChainHitStop)
            leanAmount = -steerInput * visualTurnLean;

        Quaternion facingRotation = Quaternion.LookRotation(smoothedVisualForward, Vector3.up);
        Quaternion leanRotation = Quaternion.AngleAxis(leanAmount, Vector3.forward);

        visualRoot.rotation = facingRotation * leanRotation;
    }

    private void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void EvaluateCollision(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            float angle = Vector3.Angle(normal, Vector3.up);

            if (angle <= maxGroundAngle)
            {
                lastGroundNormal = normal.normalized;
                groundedTimer = groundedMemory;
                stickGraceTimer = groundStickGraceTime;
                isGrounded = true;
                return;
            }
        }
    }

    private void TriggerSharedSpeedCameraKick()
    {
        if (!triggerSpeedCameraKick || followCamera == null)
            return;

        followCamera.TriggerFovKick(
            speedCameraKickAmount,
            speedCameraKickHoldTime,
            speedCameraKickInSpeed,
            speedCameraKickOutSpeed
        );
    }

    private void TriggerFovKick(float amount, float hold, float inSpeed, float outSpeed)
    {
        if (followCamera == null)
            return;

        followCamera.TriggerFovKick(amount, hold, inSpeed, outSpeed);
    }

    public bool IsGrounded()
    {
        return isGrounded;
    }

    public Vector3 GetVelocity()
    {
        return rb.linearVelocity;
    }

    public Vector3 GetHorizontalVelocity()
    {
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        return v;
    }

    public Vector3 GetGroundNormal()
    {
        return lastGroundNormal;
    }

    public ChainDashTarget GetLockedChainTarget()
    {
        return lockedChainTarget;
    }

    public bool HasLockedChainTarget()
    {
        return lockedChainTarget != null && !isChainDashing && !isInChainHitStop && chainRetargetLockoutTimer <= 0f;
    }

    public void ApplySpeedBoost(float accelerationBonus, float maxSpeedBonus, float duration, float instantSpeedBonus = 0f)
    {
        boostAccelerationBonus = Mathf.Max(boostAccelerationBonus, accelerationBonus);
        boostMaxSpeedBonus = Mathf.Max(boostMaxSpeedBonus, maxSpeedBonus);
        boostTimer = Mathf.Max(boostTimer, duration);

        if (instantSpeedBonus > 0f)
        {
            Vector3 horizontal = GetHorizontalVelocity();
            Vector3 boostDir = horizontal.sqrMagnitude > 0.001f
                ? horizontal.normalized
                : GetForwardFromFacing();

            float currentSpeed = horizontal.magnitude;
            float boostedSpeed = currentSpeed + instantSpeedBonus;

            rb.linearVelocity = new Vector3(
                boostDir.x * boostedSpeed,
                rb.linearVelocity.y,
                boostDir.z * boostedSpeed
            );
        }

        TriggerSharedSpeedCameraKick();
    }

    public bool HasActiveSpeedBoost()
    {
        return boostTimer > 0f;
    }

    private void OnGUI()
    {
        if (!showSpeedDebug)
            return;

        float speed = GetHorizontalVelocity().magnitude;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 22;
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(20, 20, 320, 30), $"Speed: {speed:F1}", style);
    }
}