using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visualRoot;

    [Header("Level Direction")]
    [Tooltip("The intended world-space downhill direction for the level. Example: (1,0,0) or (-1,0,0).")]
    [SerializeField] private Vector3 levelDownhillDirection = Vector3.right;

    [Header("Forward Run")]
    [SerializeField] private float baseDownhillSpeed = 14f;
    [SerializeField] private float maxDownhillSpeed = 24f;
    [SerializeField] private float downhillAcceleration = 10f;

    [Header("Steering")]
    [SerializeField] private float steerSpeed = 8f;
    [SerializeField] private float steerAcceleration = 22f;
    [SerializeField] private float rotationSpeed = 14f;

    [Header("Jump / Gravity")]
    [SerializeField] private float gravity = 38f;
    [SerializeField] private float groundedGravity = 6f;
    [SerializeField] private float jumpHeight = 2.3f;
    [SerializeField] private float maxFallSpeed = 45f;

    [Header("Grounding")]
    [SerializeField] private float groundCheckDistance = 0.4f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float slopeStickForce = 8f;

    [Header("Slide Stub")]
    [SerializeField] private bool allowSlide = true;
    [SerializeField] private float slideSpeedBonus = 6f;

    private CharacterController controller;

    private Vector3 groundNormal = Vector3.up;
    private Vector3 downhillDirection = Vector3.right;
    private Vector3 crossSlopeDirection = Vector3.forward;

    private Vector3 moveVelocity;
    private float verticalVelocity;
    private float currentForwardSpeed;
    private float currentSteerSpeed;

    private bool isGrounded;
    private bool isSliding;

    public bool IsGrounded => isGrounded;
    public bool IsSliding => isSliding;
    public Vector3 GroundNormal => groundNormal;
    public Vector3 DownhillDirection => downhillDirection;
    public Vector3 MoveVelocity => moveVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (levelDownhillDirection.sqrMagnitude < 0.001f)
            levelDownhillDirection = Vector3.right;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        GroundCheck();
        UpdateSurfaceDirections();
        HandleMovement(dt);
        HandleGravity(dt);
        MoveCharacter(dt);
        RotateVisual(dt);
    }

    private void GroundCheck()
    {
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        float castDistance = (controller.height * 0.5f) + groundCheckDistance;

        if (Physics.SphereCast(origin, controller.radius * 0.9f, Vector3.down, out RaycastHit hit, castDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            groundNormal = hit.normal;
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            isGrounded = slopeAngle <= controller.slopeLimit + 2f && hit.distance <= (controller.height * 0.5f) + 0.3f;
        }
        else
        {
            groundNormal = Vector3.up;
            isGrounded = false;
        }
    }

    private void UpdateSurfaceDirections()
    {
        Vector3 desiredForward = levelDownhillDirection.normalized;

        // Project intended level-forward onto the slope
        Vector3 projectedForward = Vector3.ProjectOnPlane(desiredForward, groundNormal);

        if (projectedForward.sqrMagnitude > 0.0001f)
        {
            downhillDirection = projectedForward.normalized;
            crossSlopeDirection = Vector3.Cross(groundNormal, downhillDirection).normalized;
        }
        else
        {
            downhillDirection = desiredForward;
            crossSlopeDirection = Vector3.Cross(Vector3.up, downhillDirection).normalized;
        }
    }

    private void HandleMovement(float dt)
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        bool jumpPressed = Input.GetButtonDown("Jump");
        bool slideHeld = allowSlide && Input.GetKey(KeyCode.LeftControl);

        isSliding = isGrounded && slideHeld;

        float targetForwardSpeed = baseDownhillSpeed;
        if (isSliding)
            targetForwardSpeed += slideSpeedBonus;

        if (isGrounded)
        {
            currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, targetForwardSpeed, downhillAcceleration * dt);

            float targetSteer = horizontal * steerSpeed;
            currentSteerSpeed = Mathf.MoveTowards(currentSteerSpeed, targetSteer, steerAcceleration * dt);

            if (jumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                isGrounded = false;
            }
        }
        else
        {
            currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, targetForwardSpeed, downhillAcceleration * 0.35f * dt);

            float targetSteer = horizontal * steerSpeed;
            currentSteerSpeed = Mathf.MoveTowards(currentSteerSpeed, targetSteer, steerAcceleration * 0.35f * dt);

            isSliding = false;
        }

        currentForwardSpeed = Mathf.Clamp(currentForwardSpeed, 0f, maxDownhillSpeed);

        Vector3 planarMove = downhillDirection * currentForwardSpeed;
        planarMove += crossSlopeDirection * currentSteerSpeed;
        planarMove = Vector3.ProjectOnPlane(planarMove, groundNormal);

        moveVelocity = planarMove;
    }

    private void HandleGravity(float dt)
    {
        if (isGrounded && verticalVelocity <= 0f)
        {
            verticalVelocity = -groundedGravity - slopeStickForce;
        }
        else
        {
            verticalVelocity -= gravity * dt;
            verticalVelocity = Mathf.Max(verticalVelocity, -maxFallSpeed);
        }
    }

    private void MoveCharacter(float dt)
    {
        Vector3 finalVelocity = moveVelocity;
        finalVelocity.y = verticalVelocity;

        controller.Move(finalVelocity * dt);
    }

    private void RotateVisual(float dt)
    {
        Vector3 lookDirection = moveVelocity;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

        if (visualRoot != null)
            visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, targetRotation, rotationSpeed * dt);
        else
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * dt);
    }

    private void OnDrawGizmosSelected()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null) return;

        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        float castDistance = (cc.height * 0.5f) + groundCheckDistance;
        Gizmos.DrawWireSphere(origin + Vector3.down * castDistance, cc.radius * 0.9f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + levelDownhillDirection.normalized * 3f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + downhillDirection * 3f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + crossSlopeDirection * 2f);
    }
}