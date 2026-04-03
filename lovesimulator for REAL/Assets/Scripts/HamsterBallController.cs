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

    [Header("Movement")]
    public float forwardAcceleration = 30f;
    public float maxGroundSpeed = 35f;
    public float maxAirSpeed = 28f;

    [Header("Turning")]
    public float groundYawTurnSpeed = 240f;
    public float airYawTurnSpeed = 120f;

    [Header("Carving")]
    public float groundCarveStrength = 9f;
    public float airCarveStrength = 2.5f;
    public float carveMinSpeed = 2f;

    [Header("Jump")]
    public float jumpVelocity = 10f;
    public float jumpCooldown = 0.15f;
    [Range(0f, 1f)] public float jumpFromGroundNormalPercent = 0.85f;
    public float jumpDetachTime = 0.12f;

    [Header("Gravity")]
    public float extraRiseGravity = 10f;
    public float extraFallGravity = 20f;

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

    private float steerInput;
    private bool jumpPressed;

    private float facingYaw;
    private float jumpTimer;
    private float jumpDetachTimer;
    private float groundedTimer;
    private float stickGraceTimer;

    private bool isGrounded;
    private Vector3 lastGroundNormal = Vector3.up;

    private Quaternion visualLean = Quaternion.identity;
    private Vector3 smoothedVisualForward = Vector3.forward;
    private Vector3 visualVelocityRef;
    private Vector3 lastStableMoveDirection = Vector3.forward;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

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
        steerInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump"))
            jumpPressed = true;
    }

    private void FixedUpdate()
    {
        jumpTimer -= Time.fixedDeltaTime;
        jumpDetachTimer -= Time.fixedDeltaTime;
        groundedTimer -= Time.fixedDeltaTime;
        stickGraceTimer -= Time.fixedDeltaTime;

        CheckRespawn();

        isGrounded = groundedTimer > 0f;

        UpdateFacing();
        ApplyDrive();
        ApplyCarving();
        ApplyGroundStick();
        ApplyExtraGravity();
        HandleJump();

        jumpPressed = false;

        if (groundedTimer <= 0f)
        {
            isGrounded = false;
        }
    }

    private void CheckRespawn()
    {
        if (transform.position.y > respawnYThreshold)
            return;

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
    }

    private void LateUpdate()
    {
        UpdateVisuals();
    }

    private void UpdateFacing()
    {
        float turnSpeed = isGrounded ? groundYawTurnSpeed : airYawTurnSpeed;
        facingYaw += steerInput * turnSpeed * Time.deltaTime;
    }

    private void ApplyDrive()
    {
        Vector3 desiredForward = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
        desiredForward.y = 0f;
        desiredForward.Normalize();

        rb.AddForce(desiredForward * forwardAcceleration, ForceMode.Acceleration);

        Vector3 horizontalVelocity = GetHorizontalVelocity();
        float maxSpeed = isGrounded ? maxGroundSpeed : maxAirSpeed;

        if (horizontalVelocity.magnitude > maxSpeed)
        {
            Vector3 clamped = horizontalVelocity.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(clamped.x, rb.linearVelocity.y, clamped.z);
        }
    }

    private void ApplyCarving()
    {
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
        if (jumpDetachTimer > 0f)
            return;

        bool shouldStick = isGrounded || stickGraceTimer > 0f;
        if (!shouldStick)
            return;

        Vector3 groundUp = lastGroundNormal.normalized;

        rb.AddForce(-groundUp * groundStickForce, ForceMode.Acceleration);

        float awaySpeed = Vector3.Dot(rb.linearVelocity, groundUp);

        if (awaySpeed > 0f && awaySpeed < maxStickAwaySpeed)
        {
            rb.linearVelocity -= groundUp * awaySpeed;
        }
    }

    private void ApplyExtraGravity()
    {
        if (isGrounded)
            return;

        float gravityToApply = rb.linearVelocity.y > 0f ? extraRiseGravity : extraFallGravity;
        rb.AddForce(Vector3.down * gravityToApply, ForceMode.Acceleration);
    }

    private void HandleJump()
    {
        if (!jumpPressed || !isGrounded || jumpTimer > 0f)
            return;

        Vector3 jumpDir = Vector3.Slerp(Vector3.up, lastGroundNormal.normalized, jumpFromGroundNormalPercent).normalized;

        float existingAlongJump = Vector3.Dot(rb.linearVelocity, jumpDir);
        if (existingAlongJump < 0f)
        {
            rb.linearVelocity -= jumpDir * existingAlongJump;
        }

        rb.linearVelocity += jumpDir * jumpVelocity;

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

        float leanAmount = -steerInput * visualTurnLean;
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
}