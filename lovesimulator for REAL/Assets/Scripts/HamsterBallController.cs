using System.Collections;
using UnityEngine;
using Dreamteck.Splines;
using UnityEngine.InputSystem;

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

    [Tooltip("Spline sampler used as the source of truth for road attachment.")]
    public SplineSampler roadSplineSampler;

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

 

    [Tooltip("If true, hitting a chain target refills the love meter.")]
    public bool refillLoveOnChainHit = true;

    [Tooltip("If true, chain attack ignores the normal love requirement.")]
    public bool chainDashIgnoresLoveRequirement = true;

   

    [Tooltip("Extra distance to push the player out of the target before the launch begins.")]
    public float chainLaunchExitPadding = 0.35f;

    [Tooltip("How long attack target re-locking is prevented after a chain launch.")]
    public float chainRetargetLockoutDuration = 0.18f;

    [Header("Rail Grinding")]
    [Tooltip("All rail grind splines in the scene. Leave empty to auto-find.")]
    public RailGrindSplineDreamteck[] railSplines;

    [Tooltip("If true, the player root rotates to match the rail frame while grinding.")]
    public bool rotateRootToRail = true;

    [Tooltip("How quickly the player root rotates to match the rail frame.")]
    public float railRootRotationSpeed = 20f;

    [Tooltip("Minimum speed when snapping onto a rail.")]
    public float railSnapSpeed = 40f;

    [Tooltip("Target grind speed.")]
    public float railGrindSpeed = 52f;

    [Tooltip("How quickly grind speed approaches target speed.")]
    public float railGrindAcceleration = 70f;

    [Tooltip("Extra force pulling the player onto the rail center while grinding.")]
    public float railStickStrength = 35f;

    [Tooltip("How quickly the player visually aligns to the rail.")]
    public float railAlignSpeed = 18f;

    [Tooltip("Extra forward speed added when jumping off a rail.")]
    public float railJumpForwardBoost = 10f;

    [Tooltip("Extra upward speed added when jumping off a rail.")]
    public float railJumpUpBoost = 9f;

    [Tooltip("Cooldown before the player can switch rails again.")]
    public float railSwitchCooldown = 0.20f;

    [Tooltip("How close another rail must be to allow switching.")]
    public float railSwitchDistance = 3.0f;

    [Tooltip("How far ahead along the current rail we check for switch targets.")]
    public float railSwitchLookAheadDistance = 4.0f;

    [Tooltip("Do not allow switching onto a rail if the landing point is this close to the end. Example: 0.15 means the last 15% of the target rail cannot be switched onto.")]
    [Range(0f, 0.5f)]
    public float railSwitchTargetEndBufferPercent = 0.15f;

    [Tooltip("How many points inside the forward/back switch window are tested.")]
    public int railSwitchForgivenessSamples = 6;

    [Tooltip("Minimum left/right input needed to try switching rails.")]
    public float railSwitchInputThreshold = 0.45f;

    [Header("Rail Switch Debug")]
    [Tooltip("If true, draws a sphere where the player would land when switching rails.")]
    public bool drawRailSwitchDebug = true;

    [Tooltip("Radius of the switch target debug sphere.")]
    public float railSwitchDebugSphereRadius = 0.35f;

    [Tooltip("Vertical lift for the switch target debug sphere.")]
    public float railSwitchDebugLift = 0.25f;

    [Tooltip("Invert left/right rail switching if controls feel backwards.")]
    public bool invertRailSwitchInput = true;

    [Header("Rail Switch Hop")]
    public float railSwitchHopDuration = 0.18f;
    public float railSwitchHopHeight = 0.65f;
    public AnimationCurve railSwitchHopCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Rail Switch Preview Object")]
    public GameObject railSwitchPreviewPrefab;
    public bool showRailSwitchPreviewObject = true;
    public float railSwitchPreviewObjectLift = 0.25f;

    [Tooltip("Small push away from the rail when exiting so the player doesn't instantly sit back inside the catch zone.")]
    public float railExitSeparation = 0.4f;

    [Tooltip("How long rail locking is disabled after jumping off or exiting a rail.")]
    public float railRelockCooldown = 0.35f;

    [Tooltip("If true, jumping off a rail starts the relock cooldown.")]
    public bool disableRailRelockAfterJump = true;

    [Tooltip("If true, reaching the end of a rail starts the relock cooldown.")]
    public bool disableRailRelockAfterRailEnd = true;

    [Header("Shared Speed Camera Kick")]
    [Tooltip("If true, dashes and boosts trigger the shared speed camera kick.")]
    public bool triggerSpeedCameraKick = true;

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

    [Header("Road Attachment")]
    [Tooltip("If true, the spline road is used to keep the player attached during normal running.")]
    public bool useRoadAttachment = true;

    [Tooltip("How many spline segments are checked when finding the nearest road sample.")]
    public int roadAttachmentSearchResolution = 28;

    [Tooltip("How close the player must be to the road before road attachment can activate.")]
    public float roadAttachmentCatchDistance = 2.25f;

    [Tooltip("Small hover offset above the road while attached.")]
    public float roadAttachmentHoverOffset = 0.03f;

    [Tooltip("Keeps the player slightly inside the road edge instead of exactly on the outermost width.")]
    public float roadAttachmentWidthPadding = 0.15f;

    [Tooltip("How quickly the player is moved back onto the road along the road normal.")]
    public float roadAttachmentSnapSpeed = 45f;

    [Tooltip("If the player is moving away from the road faster than this, attachment waits.")]
    public float roadAttachmentMaxCatchAwaySpeed = 2.5f;

    [Tooltip("Maximum speed allowed into the road while attached.")]
    public float roadAttachmentMaxIntoRoadSpeed = 8f;

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

    private ChainDashTarget activeChainTarget;
    private bool isChainDashing;
    private bool isInChainHitStop;
    private Coroutine chainHitRoutine;
    private float chainRetargetLockoutTimer;

    private bool hasRoadAttachmentThisFrame;
    private Vector3 roadAttachmentTargetPosition;
    private Vector3 roadAttachmentUp = Vector3.up;
    private Vector3 roadAttachmentForward = Vector3.forward;

    private RailGrindSplineDreamteck activeRailSpline;
    private RailGrindSplineDreamteck.RailSample activeRailSample;
    private bool isRailGrinding;
    private float railTravelDirection = 1f;
    private float currentRailSpeed = 0f;
    private float railSwitchCooldownTimer = 0f;
    private float railRelockTimer = 0f;
    private GameObject railSwitchPreviewInstance;
    private bool isRailSwitchHopping;
    private float railSwitchHopTimer;

    private Vector3 railSwitchHopStartPos;
    private Vector3 railSwitchHopMidPos;
    private Vector3 railSwitchHopEndPos;

    private Quaternion railSwitchHopStartRot;
    private Quaternion railSwitchHopEndRot;

    private RailGrindSplineDreamteck pendingRailSpline;
    private RailGrindSplineDreamteck.RailSample pendingRailSample;
    private float JumpLaunchSpeed => (2f * jumpHeight) / Mathf.Max(0.01f, timeToApex);
    private float RiseGravity => (2f * jumpHeight) / Mathf.Max(0.01f, timeToApex * timeToApex);
    private float FallGravity => (2f * jumpHeight) / Mathf.Max(0.01f, timeToDescend * timeToDescend);

    public bool IsDashing => dashTimer > 0f || isChainDashing || isInChainHitStop || isRailGrinding;

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

        if (roadSplineSampler == null)
            roadSplineSampler = FindAnyObjectByType<SplineSampler>();

      

        if (railSplines == null || railSplines.Length == 0)
            railSplines = FindObjectsByType<RailGrindSplineDreamteck>();
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
        railSwitchCooldownTimer -= dt;
        railRelockTimer -= dt;

        if (chainRetargetLockoutTimer < 0f)
            chainRetargetLockoutTimer = 0f;

        if (railSwitchCooldownTimer < 0f)
            railSwitchCooldownTimer = 0f;

        if (railRelockTimer < 0f)
            railRelockTimer = 0f;

        CheckRespawn();

        if (isInChainHitStop)
        {
            rb.linearVelocity = Vector3.zero;
            jumpPressed = false;
            dashPressed = false;
            attackPressed = false;
            return;
        }

        hasRoadAttachmentThisFrame = false;
        isGrounded = groundedTimer > 0f;

        RefreshRoadAttachment();


        if (isRailGrinding)
        {
            UpdateRailGrinding(dt);
            jumpPressed = false;
            dashPressed = false;
            attackPressed = false;
            return;
        }

        HandleFreeDash();
        HandleTargetAttack();
        TryAutoCatchRail();

        ApplyDrive(dt);
        ApplyDirectionalGrip(dt);
        ApplyRoadAttachment(dt);
        UpdateFacingFromMovement();
        ApplyGroundStick();
        ApplyJumpGravity();
        HandleJump();

        jumpPressed = false;
        dashPressed = false;
        attackPressed = false;

        if (groundedTimer <= 0f && !hasRoadAttachmentThisFrame)
            isGrounded = false;

        if (dashTimer <= 0f)
            dashTimer = 0f;

        if (dashCooldownTimer <= 0f)
            dashCooldownTimer = 0f;
    }

  

    private void LateUpdate()
    {
        UpdateVisuals();
        UpdateRailSwitchPreviewObject();
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


        activeChainTarget = null;
        isChainDashing = false;
        isInChainHitStop = false;
        chainRetargetLockoutTimer = 0f;
        hasRoadAttachmentThisFrame = false;

        isRailGrinding = false;
        activeRailSpline = null;
        currentRailSpeed = 0f;
        railSwitchCooldownTimer = 0f;
        isRailSwitchHopping = false;
        pendingRailSpline = null;
        rb.isKinematic = false;

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
        if (isChainDashing || isInChainHitStop || isRailGrinding)
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
        if (!attackPressed || IsDashing || railRelockTimer > 0f)
            return;

        Vector3 aimForward = GetAttackAimForward();

        ChainDashTarget chainTarget = FindBestChainTargetFromDirection(aimForward);

        RailGrindSplineDreamteck railSpline = null;
        RailGrindSplineDreamteck.RailSample railSample = default;
        float bestRailScore = float.NegativeInfinity;

        TryFindBestRailTarget(
            transform.position,
            aimForward,
            out railSpline,
            out railSample,
            out bestRailScore
        );

        float chainScore = chainTarget != null ? ScoreChainTarget(chainTarget, aimForward) : float.NegativeInfinity;

        if (railSpline != null && bestRailScore > chainScore)
        {
            StartRailGrind(railSpline, railSample);
            return;
        }

        if (chainTarget != null)
            StartChainDash(chainTarget);
    }

    private Vector3 GetAttackAimForward()
    {
        Vector3 facing = GetForwardFromFacing();

        if (gameplayInput != null)
            return gameplayInput.GetClampedAimDirection(facing);

        return facing;
    }

    
    private ChainDashTarget FindBestChainTargetFromDirection(Vector3 aimForward)
    {
        Vector3 origin = transform.position;

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

    private float ScoreChainTarget(ChainDashTarget target, Vector3 aimForward)
    {
        if (target == null)
            return float.NegativeInfinity;

        Vector3 origin = transform.position;
        Vector3 toTarget = target.GetAimPosition() - origin;
        float distance = toTarget.magnitude;

        if (distance <= 0.001f)
            return float.NegativeInfinity;

        Vector3 dir = toTarget / distance;
        float angle = Vector3.Angle(aimForward, dir);

        if (angle > chainTargetMaxAngle)
            return float.NegativeInfinity;

        float angleScore = 1f - (angle / chainTargetMaxAngle);
        float distanceScore = 1f - Mathf.Clamp01(distance / chainTargetSearchRadius);
        return angleScore * 3f + distanceScore;
    }

    private bool TryFindBestRailTarget(
    Vector3 origin,
    Vector3 aimForward,
    out RailGrindSplineDreamteck bestSpline,
    out RailGrindSplineDreamteck.RailSample bestSample,
    out float bestScore)
    {
        bestSpline = null;
        bestSample = default;
        bestScore = float.NegativeInfinity;

        if (railSplines == null || railSplines.Length == 0)
            return false;

        bool found = false;

        for (int i = 0; i < railSplines.Length; i++)
        {
            RailGrindSplineDreamteck rail = railSplines[i];
            if (rail == null)
                continue;

            if (rail.TryFindStartAttackTarget(
                    origin,
                    aimForward,
                    out RailGrindSplineDreamteck.RailSample sample,
                    out float score))
            {
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSpline = rail;
                    bestSample = sample;
                    found = true;
                }
            }
        }

        return found;
    }

    private void TryAutoCatchRail()
    {
        if (isRailGrinding || IsDashing || isChainDashing || isInChainHitStop || railRelockTimer > 0f)
            return;

        if (attackPressed)
            return;

        if (railSplines == null || railSplines.Length == 0)
            return;

        float bestDistance = float.PositiveInfinity;
        RailGrindSplineDreamteck bestRail = null;
        RailGrindSplineDreamteck.RailSample bestSample = default;

        for (int i = 0; i < railSplines.Length; i++)
        {
            RailGrindSplineDreamteck rail = railSplines[i];
            if (rail == null)
                continue;

            if (!rail.TryGetProximityCatch(transform.position, out RailGrindSplineDreamteck.RailSample sample))
                continue;

            if (sample.distance < bestDistance)
            {
                bestDistance = sample.distance;
                bestRail = rail;
                bestSample = sample;
            }
        }

        if (bestRail != null)
            StartRailGrind(bestRail, bestSample);
    }

    private void StartRailGrind(RailGrindSplineDreamteck railSpline, RailGrindSplineDreamteck.RailSample sample)
    {
        if (railSpline == null)
            return;

        activeRailSpline = railSpline;
        activeRailSample = sample;
        isRailGrinding = true;
        isChainDashing = false;
        isInChainHitStop = false;
        dashTimer = 0f;
        dashCooldownTimer = dashCooldown;

        Vector3 currentHorizontal = GetHorizontalVelocity();
        float entrySpeed = currentHorizontal.magnitude;

        // Rails are one-way only: head to tail / percent 0 -> 1.
        railTravelDirection = 1f;

        currentRailSpeed = Mathf.Max(railSnapSpeed, entrySpeed);

        rb.angularVelocity = Vector3.zero;

        float hoverOffset = GetRailHoverOffset(railSpline);
        Vector3 snapPos = sample.point + sample.up * hoverOffset; rb.position = snapPos;
        rb.linearVelocity = sample.forward * railTravelDirection * currentRailSpeed;

        Vector3 railForward = sample.forward * railTravelDirection;
        if (railForward.sqrMagnitude < 0.001f)
            railForward = GetForwardFromFacing();

        railForward.Normalize();

        Quaternion railRotation = Quaternion.LookRotation(railForward, sample.up);

        if (rotateRootToRail)
        {
            rb.rotation = railRotation;
        }
        else
        {
            Vector3 flatForward = railForward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude < 0.001f)
                flatForward = GetForwardFromFacing();

            flatForward.Normalize();
            facingYaw = Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg;
        }

        groundedTimer = 0f;
        stickGraceTimer = 0f;
        jumpDetachTimer = 0f;
        isGrounded = false;
        hasRoadAttachmentThisFrame = false;

        TriggerSharedSpeedCameraKick();
    }

    private void UpdateRailGrinding(float dt)
    {
        if (activeRailSpline == null)
        {
            ExitRailGrind(false);
            return;
        }

        if (isRailSwitchHopping)
        {
            UpdateRailSwitchHop(dt);
            return;
        }

        if (jumpPressed)
        {
            ExitRailGrind(true);
            return;
        }

    TryStartRailDashBoost();

        float targetRailSpeed = railGrindSpeed;
        float railAcceleration = railGrindAcceleration;

        if (dashTimer > 0f)
        {
            targetRailSpeed = Mathf.Max(targetRailSpeed, dashMaxSpeed);
            railAcceleration += dashAcceleration;
        }

        currentRailSpeed = Mathf.MoveTowards(
            currentRailSpeed,
            targetRailSpeed,
            railAcceleration * dt
        );

        Spline.Direction direction = railTravelDirection >= 0f
            ? Spline.Direction.Forward
            : Spline.Direction.Backward;

        if (!activeRailSpline.Travel(
                activeRailSample.percent,
                currentRailSpeed * dt,
                direction,
                out activeRailSample))
        {
            ExitRailGrind(false);
            return;
        }

        if (!activeRailSpline.IsClosed())
        {
            if (activeRailSample.percent >= 0.9999)
            {
                ForceRailEndSampleForward();
                ExitRailGrind(false);
                return;
            }
        }

        TrySwitchRail();

        Vector3 railForward = activeRailSample.forward;
        if (railForward.sqrMagnitude < 0.0001f)
            railForward = GetForwardFromFacing();

        railForward.Normalize();

        float hoverOffset = GetRailHoverOffset(activeRailSpline);
        Vector3 desiredPos = activeRailSample.point + activeRailSample.up * hoverOffset;
        Vector3 correction = desiredPos - rb.position;

        rb.linearVelocity = railForward * currentRailSpeed + correction * railStickStrength;
        rb.MovePosition(Vector3.Lerp(rb.position, desiredPos, Mathf.Clamp01(railAlignSpeed * dt)));

        rb.angularVelocity = Vector3.zero;

        if (rotateRootToRail)
        {
            Quaternion targetRotation = Quaternion.LookRotation(railForward, activeRailSample.up);
            Quaternion smoothedRotation = Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                Mathf.Clamp01(railRootRotationSpeed * dt)
            );

            rb.MoveRotation(smoothedRotation);
        }
        else
        {
            Vector3 flatForward = railForward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = GetForwardFromFacing();

            flatForward.Normalize();
            facingYaw = Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg;
        }

        lastGroundNormal = activeRailSample.up;
        groundedTimer = 0f;
        stickGraceTimer = 0f;
        isGrounded = false;
        hasRoadAttachmentThisFrame = false;
    }

    private void ForceRailEndSampleForward()
    {
        if (activeRailSpline == null)
            return;

        // Sample slightly before the very end.
        // This avoids bad tangent spikes at percent 1.0.
        double safeEndPercent = 0.995;

        if (!activeRailSpline.SampleAtPercent(
                safeEndPercent,
                out RailGrindSplineDreamteck.RailSample safeSample))
        {
            return;
        }

        activeRailSample = safeSample;
        railTravelDirection = 1f;
    }

    private void UpdateRailSwitchHop(float dt)
    {
        railSwitchHopTimer += dt;

        float duration = Mathf.Max(0.01f, railSwitchHopDuration);
        float rawT = Mathf.Clamp01(railSwitchHopTimer / duration);

        float t = railSwitchHopCurve != null
            ? railSwitchHopCurve.Evaluate(rawT)
            : rawT;

        Vector3 pos = QuadraticBezier(
            railSwitchHopStartPos,
            railSwitchHopMidPos,
            railSwitchHopEndPos,
            t
        );

        rb.position = pos;

        if (rotateRootToRail)
            rb.rotation = Quaternion.Slerp(railSwitchHopStartRot, railSwitchHopEndRot, t);

        if (rawT >= 1f)
            FinishRailSwitchHop();
    }

    private Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return (u * u * a) + (2f * u * t * b) + (t * t * c);
    }

    private void FinishRailSwitchHop()
    {
        isRailSwitchHopping = false;

        activeRailSpline = pendingRailSpline;
        activeRailSample = pendingRailSample;
        pendingRailSpline = null;

        railTravelDirection = 1f;

        rb.isKinematic = false;
        rb.position = railSwitchHopEndPos;
        rb.rotation = railSwitchHopEndRot;
        rb.linearVelocity = activeRailSample.forward.normalized * Mathf.Max(currentRailSpeed, railGrindSpeed);
        rb.angularVelocity = Vector3.zero;
    }

    private void TryStartRailDashBoost()
    {
        if (!dashPressed)
            return;

        if (!isRailGrinding)
            return;

        if (dashCooldownTimer > 0f)
            return;

        if (!CanDash())
            return;

        SpendLoveForDash();

        currentRailSpeed = Mathf.Max(currentRailSpeed, dashStartSpeed);

        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        TriggerSharedSpeedCameraKick();
    }

    private void TrySwitchRail()
    {
        if (railSwitchCooldownTimer > 0f || activeRailSpline == null)
            return;

        float sideInput = steerInput;
        if (Mathf.Abs(sideInput) < railSwitchInputThreshold)
            return;

        float desiredSide = Mathf.Sign(sideInput);

        if (invertRailSwitchInput)
            desiredSide *= -1f;

        if (!TryFindBufferedRailSwitchTarget(
                desiredSide,
                true,
                out RailGrindSplineDreamteck bestRail,
                out RailGrindSplineDreamteck.RailSample bestSample))
        {
            return;
        }

        StartRailSwitchHop(bestRail, bestSample);
    }

    private void StartRailSwitchHop(RailGrindSplineDreamteck targetRail, RailGrindSplineDreamteck.RailSample targetSample)
    {
        if (targetRail == null)
            return;

        isRailSwitchHopping = true;
        railSwitchHopTimer = 0f;

        pendingRailSpline = targetRail;

        float duration = Mathf.Max(0.01f, railSwitchHopDuration);
        float predictedTravelDistance = Mathf.Max(currentRailSpeed, railGrindSpeed) * duration;

        if (!targetRail.Travel(
                targetSample.percent,
                predictedTravelDistance,
                Spline.Direction.Forward,
                out pendingRailSample))
        {
            pendingRailSample = targetSample;
        }

        float hoverOffset = GetRailHoverOffset(targetRail);

        railSwitchHopStartPos = rb.position;
        railSwitchHopEndPos =
            pendingRailSample.point +
            pendingRailSample.up.normalized * hoverOffset;

        Vector3 arcUp = Vector3.Slerp(
            activeRailSample.up.normalized,
            pendingRailSample.up.normalized,
            0.5f
        ).normalized;

        railSwitchHopMidPos =
            (railSwitchHopStartPos + railSwitchHopEndPos) * 0.5f +
            arcUp * railSwitchHopHeight;

        Vector3 endForward = pendingRailSample.forward;
        if (endForward.sqrMagnitude < 0.001f)
            endForward = GetForwardFromFacing();

        endForward.Normalize();

        railSwitchHopStartRot = rb.rotation;
        railSwitchHopEndRot = Quaternion.LookRotation(
            endForward,
            pendingRailSample.up.normalized
        );

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        railSwitchCooldownTimer = railSwitchCooldown;
    }

    private bool IsValidRailSwitchLanding(RailGrindSplineDreamteck targetRail, RailGrindSplineDreamteck.RailSample targetSample)
    {
        if (targetRail == null)
            return false;

        // Closed rails loop forever, so they do not have a dangerous "end".
        if (targetRail.IsClosed())
            return true;

        float endBuffer = Mathf.Clamp01(railSwitchTargetEndBufferPercent);

        // Rails are forward-only in your current setup: 0 -> 1.
        // So prevent switching onto the final chunk of the target rail.
        return targetSample.percent <= 1.0 - endBuffer;
    }

    private bool TryFindBufferedRailSwitchTarget(
    float desiredSide,
    bool requireCorrectSide,
    out RailGrindSplineDreamteck bestRail,
    out RailGrindSplineDreamteck.RailSample bestSample)
    {
        bestRail = null;
        bestSample = default;

        if (activeRailSpline == null || railSplines == null || railSplines.Length == 0)
            return false;

        int samples = Mathf.Max(1, railSwitchForgivenessSamples);
        float bestScore = float.PositiveInfinity;

        for (int s = 0; s <= samples; s++)
        {
            float t = samples <= 0 ? 0f : s / (float)samples;
            float offsetDistance = Mathf.Lerp(0f, railSwitchLookAheadDistance, t);
            RailGrindSplineDreamteck.RailSample probeSample;

            if (Mathf.Abs(offsetDistance) < 0.001f)
            {
                probeSample = activeRailSample;
            }
            else
            {
                Spline.Direction probeDirection = offsetDistance >= 0f
                    ? Spline.Direction.Forward
                    : Spline.Direction.Backward;

                if (!activeRailSpline.Travel(
                        activeRailSample.percent,
                        Mathf.Abs(offsetDistance),
                        probeDirection,
                        out probeSample))
                {
                    continue;
                }
            }

            for (int i = 0; i < railSplines.Length; i++)
            {
                RailGrindSplineDreamteck rail = railSplines[i];

                if (rail == null || rail == activeRailSpline)
                    continue;

                bool found;

                if (requireCorrectSide)
                {
                    found = rail.TryFindSwitchTarget(
     probeSample,
     probeSample.point,
     desiredSide,
     railSwitchDistance,
     out RailGrindSplineDreamteck.RailSample candidate
 );

                    if (!found)
                        continue;

                    if (!IsValidRailSwitchLanding(rail, candidate))
                        continue;

                    float lateralScore = candidate.distance;
                    float timingScore = Mathf.Abs(offsetDistance) * 0.25f;
                    float totalScore = lateralScore + timingScore;

                    if (totalScore < bestScore)
                    {
                        bestScore = totalScore;
                        bestRail = rail;
                        bestSample = candidate;
                    }
                }
                else
                {
                    found = rail.TryProject(
                        probeSample.point,
                        out RailGrindSplineDreamteck.RailSample candidate
                    );

                    if (!found)
                        continue;

                    if (candidate.distance > railSwitchDistance)
                        continue;

                    if (!IsValidRailSwitchLanding(rail, candidate))
                        continue;

                    float lateralScore = candidate.distance;
                    float timingScore = Mathf.Abs(offsetDistance) * 0.25f;
                    float totalScore = lateralScore + timingScore;

                    if (totalScore < bestScore)
                    {
                        bestScore = totalScore;
                        bestRail = rail;
                        bestSample = candidate;
                    }
                }
            }
        }

        return bestRail != null;
    }

    private void ExitRailGrind(bool jumpedOff)
    {
        if (!isRailGrinding)
            return;

        Vector3 launchForward = activeRailSample.forward;
        if (launchForward.sqrMagnitude < 0.0001f)
            launchForward = GetForwardFromFacing();

        launchForward.Normalize();

        facingYaw = Mathf.Atan2(launchForward.x, launchForward.z) * Mathf.Rad2Deg;
        lastStableMoveDirection = launchForward;
        smoothedVisualForward = launchForward;

        isRailGrinding = false;
        if (railSwitchPreviewInstance != null)
            railSwitchPreviewInstance.SetActive(false);

        Vector3 exitVelocity = launchForward * currentRailSpeed;

        bool shouldStartRelock = false;

        if (jumpedOff)
        {
            Vector3 railUp = activeRailSample.up.normalized;

            // Remove any velocity pushing into/down away from the rail's jump-up direction.
            float velocityAlongRailUp = Vector3.Dot(exitVelocity, railUp);
            if (velocityAlongRailUp < 0f)
                exitVelocity -= railUp * velocityAlongRailUp;

            exitVelocity += launchForward * railJumpForwardBoost;
            exitVelocity += railUp * railJumpUpBoost;

            // Guarantee a minimum launch away from the rail.
            float finalRailUpSpeed = Vector3.Dot(exitVelocity, railUp);
            if (finalRailUpSpeed < railJumpUpBoost)
                exitVelocity += railUp * (railJumpUpBoost - finalRailUpSpeed);

            jumpTimer = jumpCooldown;
            jumpDetachTimer = jumpDetachTime;

            if (disableRailRelockAfterJump)
                shouldStartRelock = true;
        }
        else
        {
            if (disableRailRelockAfterRailEnd)
                shouldStartRelock = true;
        }

        Vector3 exitPosition = rb.position + activeRailSample.up * railExitSeparation;
        rb.position = exitPosition;

        rb.linearVelocity = exitVelocity;
        rb.angularVelocity = Vector3.zero;

        groundedTimer = 0f;
        stickGraceTimer = 0f;
        isGrounded = false;
        hasRoadAttachmentThisFrame = false;

        activeRailSpline = null;
        currentRailSpeed = 0f;

        if (shouldStartRelock)
            railRelockTimer = Mathf.Max(railRelockTimer, railRelockCooldown);
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


        Vector3 toAim = target.GetAimPosition() - transform.position;
        Vector3 pullDir = toAim.normalized;

        if (pullDir.sqrMagnitude < 0.001f)
            pullDir = GetForwardFromFacing();

        rb.linearVelocity = pullDir * chainPullSpeed;

        if (followCamera != null)
            followCamera.PlayAttackAttachCameraJuice();

        if (followCamera != null)
        {
            followCamera.TriggerFovKick(14f, 0.08f, 22f, 9f);
            followCamera.TriggerShake(0.08f, 0.08f);
        }
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

        if (followCamera != null && hitStop > 0f)
            followCamera.PlayAttackHitStopCamera(hitStop + 0.08f);

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
        hasRoadAttachmentThisFrame = false;

        activeChainTarget = null;
        chainRetargetLockoutTimer = chainRetargetLockoutDuration;

        if (followCamera != null)
            followCamera.PlayChainLaunchCameraJuice();

        chainHitRoutine = null;

        if (followCamera != null)
        {
            followCamera.TriggerShake(0.28f, 0.18f);
        }

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

    private void RefreshRoadAttachment()
    {
        hasRoadAttachmentThisFrame = false;

        if (!useRoadAttachment || roadSplineSampler == null)
            return;

        if (jumpDetachTimer > 0f || isChainDashing || isInChainHitStop || isRailGrinding)
            return;

        if (!roadSplineSampler.TryFindClosestRoadSample(
                rb.position,
                roadAttachmentSearchResolution,
                out SplineSampler.ClosestRoadSample sample))
        {
            return;
        }

        float usableHalfWidth = Mathf.Max(0.05f, roadSplineSampler.Width - roadAttachmentWidthPadding);

        Vector3 toPlayer = rb.position - sample.center;
        float lateral = Vector3.Dot(toPlayer, sample.right);
        float clampedLateral = Mathf.Clamp(lateral, -usableHalfWidth, usableHalfWidth);

        Vector3 desiredRoadPoint =
            sample.center +
            sample.right * clampedLateral +
            sample.up * roadAttachmentHoverOffset;

        float distanceToDesired = Vector3.Distance(rb.position, desiredRoadPoint);
        float awaySpeed = Vector3.Dot(rb.linearVelocity, sample.up);
        float heightAboveRoad = Vector3.Dot(rb.position - desiredRoadPoint, sample.up);

        if (distanceToDesired > roadAttachmentCatchDistance)
            return;

        if (heightAboveRoad > 0f && awaySpeed > roadAttachmentMaxCatchAwaySpeed)
            return;

        hasRoadAttachmentThisFrame = true;
        roadAttachmentTargetPosition = desiredRoadPoint;
        roadAttachmentUp = sample.up.normalized;
        roadAttachmentForward = sample.forward.normalized;

        lastGroundNormal = roadAttachmentUp;
        groundedTimer = Mathf.Max(groundedTimer, groundedMemory);
        stickGraceTimer = Mathf.Max(stickGraceTimer, groundStickGraceTime);
        isGrounded = true;
    }

    private void ApplyRoadAttachment(float dt)
    {
        if (!hasRoadAttachmentThisFrame)
            return;

        Vector3 correction = Vector3.Project(roadAttachmentTargetPosition - rb.position, roadAttachmentUp);

        float maxStep = roadAttachmentSnapSpeed * dt;
        if (correction.magnitude > maxStep)
            correction = correction.normalized * maxStep;

        rb.MovePosition(rb.position + correction);

        float awaySpeed = Vector3.Dot(rb.linearVelocity, roadAttachmentUp);
        if (awaySpeed > 0f)
            rb.linearVelocity -= roadAttachmentUp * awaySpeed;

        float intoRoadSpeed = Vector3.Dot(rb.linearVelocity, -roadAttachmentUp);
        if (intoRoadSpeed > roadAttachmentMaxIntoRoadSpeed)
            rb.linearVelocity += roadAttachmentUp * (intoRoadSpeed - roadAttachmentMaxIntoRoadSpeed);

        PreventBackwardsRoadTravel();
    }

    private void PreventBackwardsRoadTravel()
    {
        if (!hasRoadAttachmentThisFrame)
            return;

        Vector3 roadForward = roadAttachmentForward;
        roadForward = Vector3.ProjectOnPlane(roadForward, roadAttachmentUp);

        if (roadForward.sqrMagnitude < 0.0001f)
            return;

        roadForward.Normalize();

        Vector3 velocity = rb.linearVelocity;
        Vector3 verticalVelocity = Vector3.Project(velocity, roadAttachmentUp);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, roadAttachmentUp);

        float planarSpeed = planarVelocity.magnitude;

        if (planarSpeed < 0.1f)
            planarSpeed = maxGroundSpeed * 0.5f;

        float forwardDot = Vector3.Dot(planarVelocity.normalized, roadForward);

        // If moving even slightly backwards, HARD SNAP velocity to the spline's correct direction.
        if (forwardDot < 0f)
        {
            rb.linearVelocity = roadForward * planarSpeed + verticalVelocity;

            facingYaw = Mathf.Atan2(roadForward.x, roadForward.z) * Mathf.Rad2Deg;
            lastStableMoveDirection = roadForward;
            smoothedVisualForward = roadForward;
        }
    }

    private void ApplyDrive(float dt)
    {
        Vector3 desiredForward = GetBiasedMoveDirection();

        float accelToApply = forwardAcceleration;
        float baseMaxSpeed = isGrounded ? maxGroundSpeed : maxAirSpeed;
        float maxSpeed = baseMaxSpeed;
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
        if (isChainDashing || isInChainHitStop || isRailGrinding)
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
        if (hasRoadAttachmentThisFrame)
            return;

        if (jumpDetachTimer > 0f || isInChainHitStop || isRailGrinding)
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
        if (isGrounded || isInChainHitStop || isRailGrinding)
            return;

        float gravityToApply = rb.linearVelocity.y > 0f ? RiseGravity : FallGravity;
        rb.AddForce(Vector3.down * gravityToApply, ForceMode.Acceleration);
    }

    private void HandleJump()
    {
        if (isRailGrinding)
            return;

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
        hasRoadAttachmentThisFrame = false;
    }

    private void UpdateVisuals()
    {
        if (visualRoot == null)
            return;

        Vector3 visualUp = isRailGrinding ? activeRailSample.up.normalized : Vector3.up;
        visualRoot.position = transform.position + visualUp * rideHeight;
        Vector3 horizontalVelocity = GetHorizontalVelocity();
        float horizontalSpeed = horizontalVelocity.magnitude;

        Vector3 targetForward;

        if (isRailGrinding)
        {
            Vector3 grindForward = activeRailSample.forward * railTravelDirection;
            grindForward.y = 0f;

            if (grindForward.sqrMagnitude < 0.001f)
                grindForward = GetForwardFromFacing();

            targetForward = grindForward.normalized;
            lastStableMoveDirection = targetForward;
        }
        else if (horizontalSpeed > minVisualSpeedForFacing)
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
        if ((!IsDashing || !lockSteeringDuringDash) && !isChainDashing && !isInChainHitStop && !isRailGrinding)
            leanAmount = -steerInput * visualTurnLean;

        Quaternion facingRotation;

        if (isRailGrinding)
        {
            Vector3 railForward = activeRailSample.forward * railTravelDirection;
            if (railForward.sqrMagnitude < 0.001f)
                railForward = smoothedVisualForward;

            railForward.Normalize();
            facingRotation = Quaternion.LookRotation(railForward, activeRailSample.up);
        }
        else
        {
            facingRotation = Quaternion.LookRotation(smoothedVisualForward, Vector3.up);
        }

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
        if (isRailGrinding)
            return;

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

        followCamera.PlayDashCameraJuice();
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

    public float GetSteerInput()
    {
        return steerInput;
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

        if (isRailGrinding)
            GUI.Label(new Rect(20, 45, 320, 30), $"Grinding: YES", style);
    }

    private float GetRailHoverOffset(RailGrindSplineDreamteck rail)
    {
        if (rail == null)
            return 0f;

        float hover = rail.grindHoverHeight;

        if (rail.includePlayerRadiusInHover && sphereCol != null)
        {
            float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            hover += sphereCol.radius * maxScale;
        }

        hover += rail.extraHoverClearance;
        return hover;
    }

    private void OnDrawGizmos()
    {
        DrawRailSwitchDebug();
    }

    private void DrawRailSwitchDebug()
    {
        if (!drawRailSwitchDebug)
            return;

        if (!Application.isPlaying)
            return;

        if (!isRailGrinding || activeRailSpline == null)
            return;

        if (railSplines == null || railSplines.Length == 0)
            return;

        bool hasSwitchInput = Mathf.Abs(steerInput) >= railSwitchInputThreshold;
        float desiredSide = 0f;

        if (hasSwitchInput)
        {
            desiredSide = Mathf.Sign(steerInput);

            if (invertRailSwitchInput)
                desiredSide *= -1f;
        }

        RailGrindSplineDreamteck bestRail = null;
        RailGrindSplineDreamteck.RailSample bestSample = default;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < railSplines.Length; i++)
        {
            RailGrindSplineDreamteck rail = railSplines[i];

            if (rail == null || rail == activeRailSpline)
                continue;

            bool found;

            if (hasSwitchInput)
            {
                found = rail.TryFindSwitchTarget(
                    activeRailSample,
                    transform.position,
                    desiredSide,
                    railSwitchDistance,
                    out RailGrindSplineDreamteck.RailSample switchedSample
                );

                if (!found)
                    continue;

                if (switchedSample.distance < bestDistance)
                {
                    bestDistance = switchedSample.distance;
                    bestRail = rail;
                    bestSample = switchedSample;
                }
            }
            else
            {
                found = rail.TryProject(transform.position, out RailGrindSplineDreamteck.RailSample switchedSample);

                if (!found)
                    continue;

                if (switchedSample.distance > railSwitchDistance)
                    continue;

                if (switchedSample.distance < bestDistance)
                {
                    bestDistance = switchedSample.distance;
                    bestRail = rail;
                    bestSample = switchedSample;
                }
            }
        }

        if (bestRail == null)
            return;

        float hoverOffset = GetRailHoverOffset(bestRail);

        Vector3 landingPoint =
            bestSample.point +
            bestSample.up * (hoverOffset + railSwitchDebugLift);

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(landingPoint, railSwitchDebugSphereRadius);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, landingPoint);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(landingPoint, bestSample.forward.normalized * 1.5f);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(landingPoint, bestSample.up.normalized * 1.0f);
    }

    private void UpdateRailSwitchPreviewObject()
    {
        if (!showRailSwitchPreviewObject || railSwitchPreviewPrefab == null)
            return;

        if (railSwitchPreviewInstance == null)
        {
            railSwitchPreviewInstance = Instantiate(railSwitchPreviewPrefab);
            railSwitchPreviewInstance.name = railSwitchPreviewPrefab.name + "_RailSwitchPreview";
            railSwitchPreviewInstance.SetActive(false);
        }

        if (!isRailGrinding || activeRailSpline == null)
        {
            railSwitchPreviewInstance.SetActive(false);
            return;
        }

        if (!TryGetRailSwitchPreview(out RailGrindSplineDreamteck bestRail, out RailGrindSplineDreamteck.RailSample bestSample))
        {
            railSwitchPreviewInstance.SetActive(false);
            return;
        }

        float hoverOffset = GetRailHoverOffset(bestRail);

        Vector3 previewPosition =
            bestSample.point +
            bestSample.up * (hoverOffset + railSwitchPreviewObjectLift);

        railSwitchPreviewInstance.transform.position = previewPosition;

        if (bestSample.forward.sqrMagnitude > 0.001f && bestSample.up.sqrMagnitude > 0.001f)
        {
            railSwitchPreviewInstance.transform.rotation =
                Quaternion.LookRotation(bestSample.forward.normalized, bestSample.up.normalized);
        }

        if (!railSwitchPreviewInstance.activeSelf)
            railSwitchPreviewInstance.SetActive(true);
    }

    private bool TryGetRailSwitchPreview(
    out RailGrindSplineDreamteck bestRail,
    out RailGrindSplineDreamteck.RailSample bestSample)
    {
        float desiredSide = 0f;
        bool hasSwitchInput = Mathf.Abs(steerInput) >= railSwitchInputThreshold;

        if (hasSwitchInput)
        {
            desiredSide = Mathf.Sign(steerInput);

            if (invertRailSwitchInput)
                desiredSide *= -1f;
        }

        return TryFindBufferedRailSwitchTarget(
            desiredSide,
            hasSwitchInput,
            out bestRail,
            out bestSample
        );
    }
}