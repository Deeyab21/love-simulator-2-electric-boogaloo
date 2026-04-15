using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerGameplayInput : MonoBehaviour
{
    [Header("References")]
    public Transform aimOrigin;

    [Header("Dash Aim")]
    [Tooltip("Maximum left/right dash angle from player forward.")]
    [Range(0f, 90f)] public float maxAimAngleFromForward = 70f;

    [Tooltip("Minimum right stick magnitude before it counts as aim input.")]
    public float gamepadAimDeadzone = 0.2f;

    [Tooltip("If true, mouse aiming is enabled. If false, mouse does not affect dash direction.")]
    public bool useMouseAim = false;

    [Tooltip("Optional camera only used for mouse aiming projection.")]
    public Camera gameplayCamera;

    private PlayerControls controls;

    private Vector2 moveInput;
    private Vector2 aimInput;

    private bool jumpPressed;
    private bool dashPressed;
    private bool attackPressed;

    private Vector3 lastAimDirection = Vector3.forward;

    public Vector2 MoveInput => moveInput;
    public Vector2 AimInput => aimInput;

    public bool JumpPressed => jumpPressed;
    public bool DashPressed => dashPressed;
    public bool AttackPressed => attackPressed;

    public Vector3 LastAimDirection => lastAimDirection;

    private void Awake()
    {
        controls = new PlayerControls();

        if (aimOrigin == null)
            aimOrigin = transform;

        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        lastAimDirection = forward.normalized;
    }

    private void OnEnable()
    {
        controls.Enable();
    }

    private void OnDisable()
    {
        controls.Disable();
    }

    private void Update()
    {
        jumpPressed = false;
        dashPressed = false;
        attackPressed = false;

        moveInput = controls.Gameplay.Move.ReadValue<Vector2>();
        aimInput = controls.Gameplay.Aim.ReadValue<Vector2>();

        if (controls.Gameplay.Jump.WasPressedThisFrame())
            jumpPressed = true;

        if (controls.Gameplay.Dash.WasPressedThisFrame())
            dashPressed = true;

        if (controls.Gameplay.Attack.WasPressedThisFrame())
            attackPressed = true;
    }

    public Vector3 GetClampedAimDirection(Vector3 playerForward)
    {
        Vector3 forward = playerForward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;

        forward.Normalize();

        // 1) Prefer right stick, relative to PLAYER forward, not camera.
        if (TryGetStickAimDirection(forward, out Vector3 stickAim))
        {
            lastAimDirection = stickAim;
            return stickAim;
        }

        // 2) Optional mouse fallback.
        if (useMouseAim && TryGetMouseAimDirection(forward, out Vector3 mouseAim))
        {
            lastAimDirection = mouseAim;
            return mouseAim;
        }

        // 3) Neutral = forward.
        lastAimDirection = forward;
        return forward;
    }

    private bool TryGetStickAimDirection(Vector3 playerForward, out Vector3 result)
    {
        result = playerForward;

        if (Gamepad.current == null)
            return false;

        Vector2 stick = aimInput;

        if (stick.magnitude < gamepadAimDeadzone)
            return false;

        // Only use horizontal stick for dash angle.
        // Up/down does not rotate by camera or world anymore.
        float horizontal = stick.x;

        if (Mathf.Abs(horizontal) < 0.01f)
        {
            result = playerForward;
            return true;
        }

        float signedAngle = horizontal * maxAimAngleFromForward;
        result = Quaternion.AngleAxis(signedAngle, Vector3.up) * playerForward;
        result.y = 0f;

        if (result.sqrMagnitude < 0.001f)
            result = playerForward;

        result.Normalize();
        return true;
    }

    private bool TryGetMouseAimDirection(Vector3 playerForward, out Vector3 result)
    {
        result = playerForward;

        if (!useMouseAim || Mouse.current == null || gameplayCamera == null || aimOrigin == null)
            return false;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = gameplayCamera.ScreenPointToRay(mousePos);
        Plane plane = new Plane(Vector3.up, aimOrigin.position);

        if (!plane.Raycast(ray, out float enter))
            return false;

        Vector3 hitPoint = ray.GetPoint(enter);
        Vector3 toPoint = hitPoint - aimOrigin.position;
        toPoint.y = 0f;

        if (toPoint.sqrMagnitude < 0.001f)
            return false;

        toPoint.Normalize();

        float signedAngle = Vector3.SignedAngle(playerForward, toPoint, Vector3.up);
        signedAngle = Mathf.Clamp(signedAngle, -maxAimAngleFromForward, maxAimAngleFromForward);

        result = Quaternion.AngleAxis(signedAngle, Vector3.up) * playerForward;
        result.y = 0f;

        if (result.sqrMagnitude < 0.001f)
            result = playerForward;

        result.Normalize();
        return true;
    }
}