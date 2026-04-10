using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class HamsterBallController : MonoBehaviour
{
    [Header("Respawn")]
    [Tooltip("Where the player respawns after falling.")]
    public Transform respawnPoint;

    [Tooltip("If player falls below this Y value, they respawn.")]
    public float respawnYThreshold = -20f;

    [Header("References")]
    [Tooltip("Visual model that follows the physics body.")]
    public Transform visualRoot;

    [Tooltip("Love meter used for dash cost.")]
    public LoveMeter loveMeter;

    [Tooltip("Camera controller that receives speed-event FOV kick events.")]
    public RunnerFollowCamera followCamera;

    [Header("Movement")]
    [Tooltip("How quickly the player accelerates forward.")]
    public float forwardAcceleration = 30f;

    [Tooltip("Maximum speed while grounded.")]
    public float maxGroundSpeed = 35f;

    [Tooltip("Maximum speed while airborne.")]
    public float maxAirSpeed = 28f;

    [Header("Speed Limiting")]
    [Tooltip("If true, speed above the cap is removed gradually instead of instantly snapping down.")]
    public bool useSmoothOverSpeedDecay = true;

    [Tooltip("How quickly extra speed above the current max is removed. Higher = snappier, lower = smoother.")]
    public float overSpeedDeceleration = 45f;

    [Tooltip("If true, normal forward drive stops pushing while already above the current max speed. This prevents permanent speed creep.")]
    public bool suppressDriveWhileOverspeed = true;

    [Header("Turning")]
    [Tooltip("How fast the player rotates left/right on the ground.")]
    public float groundYawTurnSpeed = 240f;

    [Tooltip("How fast the player rotates left/right in the air.")]
    public float airYawTurnSpeed = 120f;

    [Header("Carving")]
    [Tooltip("How strongly movement direction bends toward facing direction on ground.")]
    public float groundCarveStrength = 9f;

    [Tooltip("How strongly movement direction bends toward facing direction in air.")]
    public float airCarveStrength = 2.5f;

    [Tooltip("Minimum speed required before carving starts affecting movement.")]
    public float carveMinSpeed = 2f;

    [Header("Jump Spec")]
    [Tooltip("Desired jump height in world units.")]
    public float jumpHeight = 1.8f;

    [Tooltip("How long it takes to reach the top of the jump.")]
    public float timeToApex = 0.28f;

    [Tooltip("How long it takes to fall from the apex back down.")]
    public float timeToDescend = 0.22f;

    [Tooltip("Minimum time between jumps.")]
    public float jumpCooldown = 0.10f;

    [Tooltip("How much the jump direction follows the ground slope (0 = straight up, 1 = full slope normal).")]
    [Range(0f, 1f)] public float jumpFromGroundNormalPercent = 0.55f;

    [Tooltip("Time after jumping where ground stick is disabled.")]
    public float jumpDetachTime = 0.10f;

    [Header("Dash")]
    [Tooltip("Press Left Shift to dash.")]
    public KeyCode dashKey = KeyCode.LeftShift;

    [Tooltip("If true, dash only works when the love meter is completely full.")]
    public bool requireFullLoveToDash = true;

    [Tooltip("If requireFullLoveToDash is false, this much love is required and spent.")]
    public float dashLoveCost = 100f;

    [Tooltip("Horizontal launch speed applied when dash starts.")]
    public float dashStartSpeed = 70f;

    [Tooltip("Extra acceleration applied while dash is active.")]
    public float dashAcceleration = 120f;

    [Tooltip("Maximum horizontal speed allowed during dash.")]
    public float dashMaxSpeed = 90f;

    [Tooltip("How long the dash lasts.")]
    public float dashDuration = 0.18f;

    [Tooltip("Minimum time before another dash can happen.")]
    public float dashCooldown = 0.35f;

    [Tooltip("If true, player steering is ignored during dash.")]
    public bool lockSteeringDuringDash = true;

    [Tooltip("If true, carving is ignored during dash.")]
    public bool lockCarvingDuringDash = true;

    [Tooltip("If true, dash uses the current move direction when possible. Otherwise it uses facing direction.")]
    public bool dashUsesMoveDirection = true;

    [Header("Chain Dash Targets")]
    [Tooltip("Layer(s) that contain valid chain targets.")]
    public LayerMask chainTargetLayers = ~0;

    [Tooltip("How far away a target can be acquired.")]
    public float chainTargetSearchRadius = 14f;

    [Tooltip("How wide the targeting cone is in front of the player.")]
    [Range(1f, 180f)] public float chainTargetMaxAngle = 65f;

    [Tooltip("How quickly the player is pulled to the target aim point.")]
    public float chainPullSpeed = 120f;

    [Tooltip("How quickly the player rotates to face the pull direction during chain dash.")]
    public float chainPullFacingSpeed = 18f;

    [Tooltip("If true, Shift will prefer a chain target over the normal dash when one is found.")]
    public bool prioritizeChainTargets = true;

    [Tooltip("Refill the love meter to full when a chain target is hit.")]
    public bool refillLoveOnChainHit = true;

    [Tooltip("If true, chain dash ignores the normal love requirement.")]
    public bool chainDashIgnoresLoveRequirement = true;

    [Tooltip("Extra FOV kick fired after the chain launch.")]
    public float chainLaunchCameraKickAmount = 11f;

    [Tooltip("Extra distance added beyond the player's sphere radius when ejecting from a chain target.")]
    public float chainLaunchExitPadding = 0.35f;

    [Tooltip("Short lockout after chain launch where no new chain target can be acquired.")]
    public float chainRetargetLockoutDuration = 0.18f;

    [Header("Shared Speed Camera Kick")]
    [Tooltip("If true, dash and speed boosts both trigger the same FOV kick settings.")]
    public bool triggerSpeedCameraKick = true;

    [Tooltip("Extra FOV added when a dash or speed boost starts.")]
    public float speedCameraKickAmount = 9f;

    [Tooltip("How long the FOV kick stays near full before settling.")]
    public float speedCameraKickHoldTime = 0.22f;

    [Tooltip("How quickly the FOV kick expands.")]
    public float speedCameraKickInSpeed = 14f;

    [Tooltip("How quickly the FOV kick settles back down.")]
    public float speedCameraKickOutSpeed = 5f;

    [Header("Temporary Speed Boost")]
    [Tooltip("Extra acceleration applied during boost.")]
    public float boostAccelerationBonus = 0f;

    [Tooltip("Extra max speed allowed during boost.")]
    public float boostMaxSpeedBonus = 0f;

    [Tooltip("Remaining boost time.")]
    public float boostTimer = 0f;

    [Header("Grounding")]
    [Tooltip("Maximum slope angle that still counts as ground.")]
    public float maxGroundAngle = 60f;

    [Tooltip("Time after leaving ground that still counts as grounded (coyote time).")]
    public float groundedMemory = 0.10f;

    [Header("Ground Stick")]
    [Tooltip("Force pushing the player toward the ground.")]
    public float groundStickForce = 35f;

    [Tooltip("Time after leaving ground where stick force still applies.")]
    public float groundStickGraceTime = 0.12f;

    [Tooltip("Max upward speed allowed before ground stick cancels it.")]
    public float maxStickAwaySpeed = 6f;

    [Header("Visuals")]
    [Tooltip("Height offset of visual model above physics body.")]
    public float rideHeight = 0.9f;

    [Tooltip("How smoothly the visual follows position.")]
    public float visualPositionSmooth = 18f;

    [Tooltip("How quickly the visual rotates to match movement direction.")]
    public float visualFacingSmooth = 12f;

    [Tooltip("How much the character leans when turning.")]
    public float visualTurnLean = 15f;

    [Tooltip("How quickly the lean updates.")]
    public float visualLeanSmooth = 10f;

    [Tooltip("Minimum speed required before visual starts facing movement direction.")]
    public float minVisualSpeedForFacing = 0.75f;

    private Rigidbody rb;
    private SphereCollider sphereCol;

    private float steerInput;
    private bool jumpPressed;
    private bool dashPressed;

    private float facingYaw;
    private float jumpTimer;
    private float jumpDetachTimer;
    private float groundedTimer;
    private float stickGraceTimer;

    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection = Vector3.forward;

    private bool isGrounded;
    private Vector3 lastGroundNormal = Vector3.up;

    private Quaternion visualLean = Quaternion.identity;
    private Vector3 smoothedVisualForward = Vector3.forward;
    private Vector3 visualVelocityRef;
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

        facingYaw = transform.eulerAngles.y;

        Vector3 startForward = transform.forward;
        startForward.y = 0f;
        if (startForward.sqrMagnitude < 0.001f)
            startForward = Vector3.forward;

        startForward.Normalize();
        smoothedVisualForward = startForward;
        lastStableMoveDirection = startForward;
        dashDirection = startForward;
    }

    private void Update()
    {
        steerInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump"))
            jumpPressed = true;

        if (Input.GetKeyDown(dashKey))
            dashPressed = true;

        UpdateChainTargetPreview();
    }

    private void UpdateChainTargetPreview()
    {
        ChainDashTarget newPreview = null;

        if (!isChainDashing && !isInChainHitStop && chainRetargetLockoutTimer <= 0f)
            newPreview = FindBestChainTarget();

        if (lastPreviewedChainTarget != null && lastPreviewedChainTarget != newPreview)
            lastPreviewedChainTarget.SetPreviewed(false);

        lockedChainTarget = newPreview;

        if (lockedChainTarget != null)
            lockedChainTarget.SetPreviewed(true);

        lastPreviewedChainTarget = lockedChainTarget;
    }

    private void FixedUpdate()
    {
        jumpTimer -= Time.fixedDeltaTime;
        jumpDetachTimer -= Time.fixedDeltaTime;
        groundedTimer -= Time.fixedDeltaTime;
        stickGraceTimer -= Time.fixedDeltaTime;
        dashTimer -= Time.fixedDeltaTime;
        dashCooldownTimer -= Time.fixedDeltaTime;

        chainRetargetLockoutTimer -= Time.fixedDeltaTime;
        if (chainRetargetLockoutTimer < 0f)
            chainRetargetLockoutTimer = 0f;

        CheckRespawn();

        if (isInChainHitStop)
        {
            rb.linearVelocity = Vector3.zero;
            jumpPressed = false;
            dashPressed = false;
            return;
        }

        isGrounded = groundedTimer > 0f;

        UpdateFacing();
        HandleDash();

        ApplyDrive();
        ApplyCarving();
        ApplyGroundStick();
        ApplyJumpGravity();
        HandleJump();

        jumpPressed = false;
        dashPressed = false;

        if (groundedTimer <= 0f)
            isGrounded = false;

        boostTimer -= Time.fixedDeltaTime;

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
        dashDirection = forward;

        isGrounded = false;
        groundedTimer = 0f;
        stickGraceTimer = 0f;
        jumpDetachTimer = 0f;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
    }

    private void LateUpdate()
    {
        UpdateVisuals();
    }

    private void ClearChainPreview()
    {
        if (lastPreviewedChainTarget != null)
        {
            lastPreviewedChainTarget.SetPreviewed(false);
            lastPreviewedChainTarget = null;
        }
    }

    private void UpdateFacing()
    {
        if ((IsDashing && lockSteeringDuringDash) || isChainDashing || isInChainHitStop)
            return;

        float turnSpeed = isGrounded ? groundYawTurnSpeed : airYawTurnSpeed;
        facingYaw += steerInput * turnSpeed * Time.deltaTime;
    }

    private void HandleDash()
    {
        if (!dashPressed || IsDashing || dashCooldownTimer > 0f)
            return;

        if (prioritizeChainTargets && lockedChainTarget != null)
        {
            StartChainDash(lockedChainTarget);
            return;
        }

        if (!CanDash())
            return;

        SpendLoveForDash();

        dashDirection = GetDashDirection();

        Vector3 horizontal = GetHorizontalVelocity();
        float currentSpeedAlongDash = Vector3.Dot(horizontal, dashDirection);
        float targetSpeed = Mathf.Max(dashStartSpeed, currentSpeedAlongDash);

        Vector3 newHorizontal = dashDirection * targetSpeed;
        rb.linearVelocity = new Vector3(newHorizontal.x, rb.linearVelocity.y, newHorizontal.z);

        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        TriggerSharedSpeedCameraKick();
    }

    private ChainDashTarget FindBestChainTarget()
    {
        Vector3 origin = transform.position;
        Vector3 forward = GetTargetingForward();

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
            float angle = Vector3.Angle(forward, dir);

            if (angle > chainTargetMaxAngle)
                continue;

            float angleScore = 1f - (angle / chainTargetMaxAngle);
            float distanceScore = 1f - Mathf.Clamp01(distance / chainTargetSearchRadius);
            float totalScore = angleScore * 2f + distanceScore;

            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private Vector3 GetTargetingForward()
    {
        Vector3 horizontal = GetHorizontalVelocity();
        if (horizontal.sqrMagnitude > 0.01f)
            return horizontal.normalized;

        Vector3 facing = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
        facing.y = 0f;
        return facing.normalized;
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
            pullDir = GetTargetingForward();

        rb.linearVelocity = pullDir * chainPullSpeed;
        dashDirection = new Vector3(pullDir.x, 0f, pullDir.z).normalized;

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

        isInChainHitStop = false;

        Vector3 launchDir = hitTarget.GetLaunchDirection();
        if (launchDir.sqrMagnitude < 0.001f)
            launchDir = Vector3.forward;

        launchDir.Normalize();

        Vector3 flatLaunch = new Vector3(launchDir.x, 0f, launchDir.z);
        if (flatLaunch.sqrMagnitude < 0.001f)
            flatLaunch = GetTargetingForward();

        flatLaunch.Normalize();

        facingYaw = Mathf.Atan2(flatLaunch.x, flatLaunch.z) * Mathf.Rad2Deg;
        dashDirection = flatLaunch;
        smoothedVisualForward = flatLaunch;
        lastStableMoveDirection = flatLaunch;

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

    private Vector3 GetDashDirection()
    {
        if (dashUsesMoveDirection)
        {
            Vector3 moveDir = GetHorizontalVelocity();
            if (moveDir.sqrMagnitude > 0.001f)
            {
                moveDir.Normalize();
                return moveDir;
            }
        }

        Vector3 facingDir = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
        facingDir.y = 0f;

        if (facingDir.sqrMagnitude < 0.001f)
            facingDir = transform.forward;

        facingDir.y = 0f;
        facingDir.Normalize();
        return facingDir;
    }

    private void ApplyDrive()
    {
        Vector3 desiredForward = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
        desiredForward.y = 0f;
        desiredForward.Normalize();

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
                float targetYaw = Mathf.Atan2(flatPull.x, flatPull.z) * Mathf.Rad2Deg;
                facingYaw = Mathf.LerpAngle(facingYaw, targetYaw, chainPullFacingSpeed * Time.fixedDeltaTime);
                dashDirection = flatPull;
            }

            isUsingDashDrive = true;
        }
        else if (dashTimer > 0f)
        {
            desiredForward = dashDirection;
            accelToApply += dashAcceleration;
            maxSpeed = Mathf.Max(maxSpeed, dashMaxSpeed);
            isUsingDashDrive = true;
        }

        if (isUsingDashDrive)
            return;

        Vector3 horizontalVelocity = GetHorizontalVelocity();
        float speed = horizontalVelocity.magnitude;
        bool overspeed = speed > maxSpeed;

        bool shouldApplyDrive = true;

        if (suppressDriveWhileOverspeed && overspeed)
            shouldApplyDrive = false;

        if (shouldApplyDrive)
            rb.AddForce(desiredForward * accelToApply, ForceMode.Acceleration);

        horizontalVelocity = GetHorizontalVelocity();
        speed = horizontalVelocity.magnitude;
        overspeed = speed > maxSpeed;

        if (overspeed)
        {
            if (useSmoothOverSpeedDecay)
            {
                float newSpeed = Mathf.MoveTowards(
                    speed,
                    maxSpeed,
                    overSpeedDeceleration * Time.fixedDeltaTime
                );

                Vector3 adjusted = horizontalVelocity.normalized * newSpeed;
                rb.linearVelocity = new Vector3(adjusted.x, rb.linearVelocity.y, adjusted.z);
            }
            else
            {
                Vector3 clamped = horizontalVelocity.normalized * maxSpeed;
                rb.linearVelocity = new Vector3(clamped.x, rb.linearVelocity.y, clamped.z);
            }
        }
    }

    private void ApplyCarving()
    {
        if ((IsDashing && lockCarvingDuringDash) || isChainDashing || isInChainHitStop)
            return;

        Vector3 horizontalVelocity = GetHorizontalVelocity();
        float speed = horizontalVelocity.magnitude;

        if (speed < carveMinSpeed)
            return;

        Vector3 desiredForward = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
        desiredForward.y = 0f;
        desiredForward.Normalize();

        float carveStrength = isGrounded ? groundCarveStrength : airCarveStrength;

        Vector3 bentVelocity = Vector3.Slerp(
            horizontalVelocity.normalized,
            desiredForward,
            carveStrength * Time.fixedDeltaTime
        ) * speed;

        rb.linearVelocity = new Vector3(bentVelocity.x, rb.linearVelocity.y, bentVelocity.z);
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

        Vector3 visualTargetPos = rb.position + Vector3.up * rideHeight;
        visualRoot.position = Vector3.SmoothDamp(
            visualRoot.position,
            visualTargetPos,
            ref visualVelocityRef,
            1f / Mathf.Max(0.01f, visualPositionSmooth)
        );

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
            targetForward = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;

        targetForward.Normalize();

        smoothedVisualForward = Vector3.Slerp(
            smoothedVisualForward,
            targetForward,
            visualFacingSmooth * Time.deltaTime
        ).normalized;

        float leanAmount = 0f;
        if (!(IsDashing && lockSteeringDuringDash) && !isChainDashing && !isInChainHitStop)
            leanAmount = -steerInput * visualTurnLean;

        Quaternion targetLean = Quaternion.AngleAxis(leanAmount, Vector3.forward);

        visualLean = Quaternion.Slerp(
            visualLean,
            targetLean,
            visualLeanSmooth * Time.deltaTime
        );

        visualRoot.rotation = Quaternion.LookRotation(smoothedVisualForward, Vector3.up) * visualLean;
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

    private void OnGUI()
    {
        float speed = GetHorizontalVelocity().magnitude;

        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(20, 20, 400, 40), $"Speed: {speed:F1}", style);

        if (loveMeter != null)
        {
            string dashState = CanDash() ? "READY" : "NOT READY";
            GUI.Label(new Rect(20, 60, 400, 40), $"Dash: {dashState}", style);
        }

        GUI.Label(new Rect(20, 100, 600, 40), $"Locked Target: {(lockedChainTarget != null ? lockedChainTarget.name : "None")}", style);
        GUI.Label(new Rect(20, 140, 500, 40), $"Chain Dashing: {isChainDashing}", style);
        GUI.Label(new Rect(20, 180, 500, 40), $"Hit Stop: {isInChainHitStop}", style);
        GUI.Label(new Rect(20, 220, 500, 40), $"Retarget Lockout: {chainRetargetLockoutTimer:F2}", style);
    }

    public void ApplySpeedBoost(float accelerationBonus, float maxSpeedBonus, float duration, float instantSpeedBonus = 0f)
    {
        boostAccelerationBonus = Mathf.Max(boostAccelerationBonus, accelerationBonus);
        boostMaxSpeedBonus = Mathf.Max(boostMaxSpeedBonus, maxSpeedBonus);
        boostTimer = Mathf.Max(boostTimer, duration);

        if (instantSpeedBonus > 0f)
        {
            Vector3 horizontal = GetHorizontalVelocity();
            Vector3 boostDir;

            if (horizontal.sqrMagnitude > 0.001f)
            {
                boostDir = horizontal.normalized;
            }
            else
            {
                boostDir = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
                boostDir.y = 0f;
                boostDir.Normalize();
            }

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
}