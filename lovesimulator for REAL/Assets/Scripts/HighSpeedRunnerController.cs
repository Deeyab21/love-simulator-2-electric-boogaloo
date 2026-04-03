using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class HighSpeedRunnerController : MonoBehaviour
{
    [Header("References")]
    public Transform visualRoot;

    [Header("Ground Check")]
    public LayerMask groundMask = ~0;
    public float groundCheckRadiusScale = 0.9f;
    public float groundCheckExtraDistance = 0.12f;
    public float maxGroundAngle = 65f;
    public float coyoteTime = 0.08f;

    [Header("Speed")]
    public float baseSpeed = 25f;
    public float maxSpeed = 45f;
    public float speedChangeRate = 8f;

    [Header("Steering")]
    public float yawTurnSpeed = 120f;
    public float airYawTurnSpeed = 70f;

    [Header("Carving")]
    public float lowSpeedCarveFollow = 7f;
    public float highSpeedCarveFollow = 2.25f;

    [Header("Air Control")]
    [Range(0f, 1f)] public float airControlPercent = 0.35f;
    public float airDirectionFollow = 1.8f;

    [Header("Jump")]
    public float jumpForce = 11f;
    public float jumpCooldown = 0.1f;
    public float extraGravity = 30f;
    public float fallGravity = 40f;

    [Header("Ground Stick")]
    public float groundedDownforce = 2f;
    public float groundedMoveSharpness = 14f;

    [Header("Landing Recovery")]
    public float landingRecoveryTime = 0.22f;
    public float landingControlReturnCurve = 1.6f;

    [Header("Visuals")]
    public float visualTurnLean = 18f;
    public float visualFacingSmooth = 10f;
    public float visualLeanSmooth = 10f;

    private Rigidbody rb;
    private CapsuleCollider col;

    private float steerInput;
    private bool jumpPressed;

    private bool isGrounded;
    private bool wasGroundedLastFrame;
    private float coyoteCounter;
    private float jumpTimer;

    private Vector3 groundNormal = Vector3.up;

    private float facingYaw;
    private Vector3 travelDirection;
    private float currentSpeed;

    private float landingRecoveryCounter;
    private Vector3 landingCarryDirection = Vector3.forward;

    private Vector3 smoothedVisualForward = Vector3.forward;
    private Quaternion visualLeanRotation = Quaternion.identity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        facingYaw = transform.eulerAngles.y;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        travelDirection = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        smoothedVisualForward = travelDirection;

        currentSpeed = baseSpeed;
    }

    private void Update()
    {
        steerInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump"))
            jumpPressed = true;
    }

    private void FixedUpdate()
    {
        wasGroundedLastFrame = isGrounded;

        jumpTimer -= Time.fixedDeltaTime;
        landingRecoveryCounter -= Time.fixedDeltaTime;

        CheckGround();
        UpdateFacingIntent();
        UpdateSpeed();
        UpdateTravelDirection();
        HandleJump();
        ApplyMovement();
        ApplyExtraGravity();
        HandleLandingEvents();
        UpdateVisuals();

        jumpPressed = false;
    }

    private void CheckGround()
    {
        bool foundGround = false;
        groundNormal = Vector3.up;

        Vector3 worldCenter = rb.position + transform.TransformDirection(col.center);

        float radius = Mathf.Max(0.05f, col.radius * groundCheckRadiusScale);
        float castDistance = (col.height * 0.5f) - col.radius + groundCheckExtraDistance;

        Vector3 castOrigin = worldCenter + Vector3.up * 0.02f;

        if (Physics.SphereCast(castOrigin, radius, Vector3.down, out RaycastHit hit, castDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle <= maxGroundAngle)
            {
                foundGround = true;
                groundNormal = hit.normal;
                coyoteCounter = coyoteTime;
            }
        }

        if (!foundGround)
            coyoteCounter -= Time.fixedDeltaTime;

        isGrounded = coyoteCounter > 0f;
    }

    private void UpdateFacingIntent()
    {
        float turnSpeed = isGrounded ? yawTurnSpeed : airYawTurnSpeed;
        facingYaw += steerInput * turnSpeed * Time.fixedDeltaTime;
    }

    private void UpdateSpeed()
    {
        float targetSpeed = baseSpeed;
        targetSpeed = Mathf.Clamp(targetSpeed, 0f, maxSpeed);

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, speedChangeRate * Time.fixedDeltaTime);
    }

    private void UpdateTravelDirection()
    {
        Vector3 desired = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
        desired.y = 0f;

        if (desired.sqrMagnitude < 0.0001f)
            return;

        desired.Normalize();

        Vector3 current = GetPlanarTravelDirection();

        float speed01 = Mathf.InverseLerp(baseSpeed, maxSpeed, currentSpeed);

        float groundedFollow = Mathf.Lerp(lowSpeedCarveFollow, highSpeedCarveFollow, speed01);
        float follow = isGrounded ? groundedFollow : airDirectionFollow;

        if (landingRecoveryCounter > 0f)
        {
            float recovery01 = 1f - Mathf.Clamp01(landingRecoveryCounter / landingRecoveryTime);
            recovery01 = Mathf.Pow(recovery01, landingControlReturnCurve);

            Vector3 preserved = landingCarryDirection;
            preserved.y = 0f;
            if (preserved.sqrMagnitude < 0.0001f)
                preserved = current;
            preserved.Normalize();

            Vector3 recovering = Vector3.Slerp(preserved, desired, recovery01);
            recovering.y = 0f;

            travelDirection = Vector3.Slerp(current, recovering.normalized, follow * Time.fixedDeltaTime).normalized;
        }
        else
        {
            travelDirection = Vector3.Slerp(current, desired, follow * Time.fixedDeltaTime).normalized;
        }
    }

    private void HandleJump()
    {
        if (!jumpPressed || !isGrounded || jumpTimer > 0f)
            return;

        landingCarryDirection = GetPlanarTravelDirection();

        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

        isGrounded = false;
        coyoteCounter = 0f;
        jumpTimer = jumpCooldown;
    }

    private void ApplyMovement()
    {
        Vector3 vel = rb.linearVelocity;

        if (isGrounded)
        {
            Vector3 moveDir = Vector3.ProjectOnPlane(GetPlanarTravelDirection(), groundNormal).normalized;
            if (moveDir.sqrMagnitude < 0.0001f)
                moveDir = GetPlanarTravelDirection();

            Vector3 currentHorizontal = Vector3.ProjectOnPlane(vel, Vector3.up);
            Vector3 targetHorizontal = moveDir * currentSpeed;

            currentHorizontal = Vector3.MoveTowards(
                currentHorizontal,
                targetHorizontal,
                groundedMoveSharpness * Time.fixedDeltaTime * currentSpeed
            );

            vel.x = currentHorizontal.x;
            vel.z = currentHorizontal.z;

            // Very light downward hold instead of aggressively slamming into the ground.
            if (vel.y <= 0f)
                vel.y = -groundedDownforce;
        }
        else
        {
            Vector3 currentHorizontal = new Vector3(vel.x, 0f, vel.z);
            Vector3 targetHorizontal = GetPlanarTravelDirection() * currentSpeed;

            currentHorizontal = Vector3.Lerp(
                currentHorizontal,
                targetHorizontal,
                airControlPercent * Time.fixedDeltaTime
            );

            vel.x = currentHorizontal.x;
            vel.z = currentHorizontal.z;
        }

        rb.linearVelocity = vel;

        Vector3 look = GetPlanarTravelDirection();
        if (look.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(look, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, 10f * Time.fixedDeltaTime));
        }
    }

    private void ApplyExtraGravity()
    {
        if (isGrounded)
            return;

        float gravityAmount = rb.linearVelocity.y > 0f ? extraGravity : fallGravity;
        rb.AddForce(Vector3.down * gravityAmount, ForceMode.Acceleration);
    }

    private void HandleLandingEvents()
    {
        if (!wasGroundedLastFrame && isGrounded)
        {
            landingRecoveryCounter = landingRecoveryTime;
        }
    }

    private void UpdateVisuals()
    {
        if (visualRoot == null)
            return;

        Vector3 forward = GetPlanarTravelDirection();
        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;

        forward.y = 0f;
        forward.Normalize();

        smoothedVisualForward = Vector3.Slerp(
            smoothedVisualForward,
            forward,
            visualFacingSmooth * Time.fixedDeltaTime
        ).normalized;

        Quaternion facingRot = Quaternion.LookRotation(smoothedVisualForward, Vector3.up);

        float lean = -steerInput * visualTurnLean;
        Quaternion targetLean = Quaternion.AngleAxis(lean, Vector3.forward);
        visualLeanRotation = Quaternion.Slerp(
            visualLeanRotation,
            targetLean,
            visualLeanSmooth * Time.fixedDeltaTime
        );

        Quaternion localVisual = Quaternion.Inverse(transform.rotation) * facingRot;
        visualRoot.localRotation = localVisual * visualLeanRotation;
    }

    private Vector3 GetPlanarTravelDirection()
    {
        Vector3 flat = new Vector3(travelDirection.x, 0f, travelDirection.z);
        if (flat.sqrMagnitude < 0.0001f)
        {
            flat = transform.forward;
            flat.y = 0f;
        }

        return flat.normalized;
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;

        Vector3 pos = transform.position + Vector3.up * 0.5f;

        Vector3 facing = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
        facing.y = 0f;
        facing.Normalize();

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(pos, pos + facing * 3f);

        Vector3 travel = GetPlanarTravelDirection();
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pos, pos + travel * 3f);

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(pos, pos + groundNormal * 2f);
    }

    public Vector3 GetTravelDirection()
    {
        return GetPlanarTravelDirection();
    }

    public bool IsGrounded()
    {
        return isGrounded;
    }
}