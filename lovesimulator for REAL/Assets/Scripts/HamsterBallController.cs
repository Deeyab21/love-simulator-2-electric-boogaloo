using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class HamsterBallController : MonoBehaviour
{
    [Header("Respawn")]
    public Transform respawnPoint;
    public float respawnYThreshold = -20f;

    [Header("References")]
    public Transform visualRoot;
    public LoveMeter loveMeter;
    public RunnerFollowCamera followCamera;
    public PlayerGameplayInput gameplayInput;

    [Header("Movement")]
    public float forwardAcceleration = 30f;
    public float maxGroundSpeed = 35f;
    public float maxAirSpeed = 28f;

    [Header("Speed Limiting")]
    public bool useSmoothOverSpeedDecay = true;
    public float overSpeedDeceleration = 45f;
    public bool suppressDriveWhileOverspeed = true;

    [Header("Turning")]
    public float groundYawTurnSpeed = 240f;
    public float airYawTurnSpeed = 120f;

    [Header("Carving")]
    public float groundCarveStrength = 9f;
    public float airCarveStrength = 2.5f;
    public float carveMinSpeed = 2f;

    [Header("Jump Spec")]
    public float jumpHeight = 1.8f;
    public float timeToApex = 0.28f;
    public float timeToDescend = 0.22f;
    public float jumpCooldown = 0.10f;
    [Range(0f, 1f)] public float jumpFromGroundNormalPercent = 0.55f;
    public float jumpDetachTime = 0.10f;

    [Header("Free Dash")]
    public bool requireFullLoveToDash = true;
    public float dashLoveCost = 100f;
    public float dashStartSpeed = 70f;
    public float dashAcceleration = 120f;
    public float dashMaxSpeed = 90f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 0.35f;
    public bool lockSteeringDuringDash = true;
    public bool lockCarvingDuringDash = true;

    [Header("Target Attack")]
    public LayerMask chainTargetLayers = ~0;
    public float chainTargetSearchRadius = 14f;
    [Range(1f, 180f)] public float chainTargetMaxAngle = 50f;
    public float chainPullSpeed = 120f;
    public float chainPullFacingSpeed = 18f;
    public bool refillLoveOnChainHit = true;
    public bool chainDashIgnoresLoveRequirement = true;
    public float chainLaunchCameraKickAmount = 11f;
    public float chainLaunchExitPadding = 0.35f;
    public float chainRetargetLockoutDuration = 0.18f;

    [Header("Shared Speed Camera Kick")]
    public bool triggerSpeedCameraKick = true;
    public float speedCameraKickAmount = 9f;
    public float speedCameraKickHoldTime = 0.22f;
    public float speedCameraKickInSpeed = 14f;
    public float speedCameraKickOutSpeed = 5f;

    [Header("Temporary Speed Boost")]
    public float boostAccelerationBonus = 0f;
    public float boostMaxSpeedBonus = 0f;
    public float boostTimer = 0f;

    [Header("Grounding")]
    public float maxGroundAngle = 60f;
    public float groundedMemory = 0.10f;

    [Header("Ground Stick")]
    public float groundStickForce = 35f;
    public float groundStickGraceTime = 0.12f;
    public float maxStickAwaySpeed = 6f;

    [Header("Visuals")]
    public float rideHeight = 0.9f;
    public float visualPositionSmooth = 18f;
    public float visualFacingSmooth = 12f;
    public float visualTurnLean = 15f;
    public float visualLeanSmooth = 10f;
    public float minVisualSpeedForFacing = 0.75f;

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
        dashDirection = startForward;
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
            attackPressed = false;
            return;
        }

        isGrounded = groundedTimer > 0f;

        UpdateFacing();
        HandleFreeDash();
        HandleTargetAttack();

        ApplyDrive();
        ApplyCarving();
        ApplyGroundStick();
        ApplyJumpGravity();
        HandleJump();

        jumpPressed = false;
        dashPressed = false;
        attackPressed = false;

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
        dashDirection = forward;

        isGrounded = false;
        groundedTimer = 0f;
        stickGraceTimer = 0f;
        jumpDetachTimer = 0f;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
    }

    private void UpdateFacing()
    {
        if ((IsDashing && lockSteeringDuringDash) || isChainDashing || isInChainHitStop)
            return;

        float turnSpeed = isGrounded ? groundYawTurnSpeed : airYawTurnSpeed;
        facingYaw += steerInput * turnSpeed * Time.deltaTime;
    }

    private void HandleFreeDash()
    {
        if (!dashPressed || IsDashing || dashCooldownTimer > 0f)
            return;

        if (!CanDash())
            return;

        SpendLoveForDash();

        // Dash always straight ahead based on current facing.
        dashDirection = GetAimDirection();

        Vector3 horizontal = GetHorizontalVelocity();
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

    private Vector3 GetAimDirection()
    {
        Vector3 forward = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;

        forward.y = 0f;
        forward.Normalize();
        return forward;
    }

    private ChainDashTarget FindBestChainTarget()
    {
        Vector3 origin = transform.position;
        Vector3 aimForward = GetAimDirection();

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
            pullDir = GetAimDirection();

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

        hitTarget.TriggerZoneImpact();

        Vector3 launchDir = hitTarget.GetLaunchDirection();
        if (launchDir.sqrMagnitude < 0.001f)
            launchDir = Vector3.forward;

        launchDir.Normalize();

        Vector3 flatLaunch = new Vector3(launchDir.x, 0f, launchDir.z);
        if (flatLaunch.sqrMagnitude < 0.001f)
            flatLaunch = GetAimDirection();

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
        dashDirection = flatLaunch;

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