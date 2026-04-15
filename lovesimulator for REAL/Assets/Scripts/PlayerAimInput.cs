using UnityEngine;

public class PlayerAimInput : MonoBehaviour
{
    [Header("Movement Input")]
    [Tooltip("Horizontal axis used for steering / left stick X.")]
    public string horizontalAxis = "Horizontal";

    [Tooltip("Vertical axis if you want it later. Not required for your current runner movement.")]
    public string verticalAxis = "Vertical";

    [Header("Buttons")]
    [Tooltip("Jump input button name.")]
    public string jumpButton = "Jump";

    [Tooltip("Keyboard key for free dash.")]
    public KeyCode keyboardDashKey = KeyCode.LeftShift;

    [Tooltip("Mouse button for target attack. 0 = left click.")]
    public int mouseAttackButton = 0;

    [Header("Gamepad Axes")]
    [Tooltip("Right stick X axis name.")]
    public string aimHorizontalAxis = "AimHorizontal";

    [Tooltip("Right stick Y axis name.")]
    public string aimVerticalAxis = "AimVertical";

    [Tooltip("Gamepad left trigger axis for dash.")]
    public string dashTriggerAxis = "DashTrigger";

    [Tooltip("Gamepad right trigger axis for attack.")]
    public string attackTriggerAxis = "AttackTrigger";

    [Header("Trigger Thresholds")]
    [Tooltip("How far a trigger axis must be pressed before it counts as pressed.")]
    public float triggerPressedThreshold = 0.5f;

    [Header("Aim Settings")]
    [Tooltip("Minimum right stick magnitude before it counts as valid aim.")]
    public float gamepadAimDeadzone = 0.2f;

    [Tooltip("Camera used for mouse aiming. If null, uses Camera.main.")]
    public Camera aimCamera;

    [Tooltip("Optional transform to use as the world-space aim origin. Usually the player transform.")]
    public Transform aimOrigin;

    [Tooltip("If true, mouse aiming projects onto a flat plane at aimOrigin height.")]
    public bool useFlatAimPlane = true;

    private bool dashHeldLastFrame;
    private bool attackHeldLastFrame;

    private Vector2 moveInput;
    private bool jumpPressed;
    private bool dashPressed;
    private bool attackPressed;

    private Vector3 lastAimDirection = Vector3.forward;
    private bool hasAim;

    public float MoveX => moveInput.x;
    public float MoveY => moveInput.y;
    public bool JumpPressed => jumpPressed;
    public bool DashPressed => dashPressed;
    public bool AttackPressed => attackPressed;
    public bool HasAim => hasAim;

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = Camera.main;

        if (aimOrigin == null)
            aimOrigin = transform;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        lastAimDirection = forward.normalized;
    }

    private void Update()
    {
        ReadMovement();
        ReadButtons();
        ReadAim();
    }

    private void ReadMovement()
    {
        float x = Input.GetAxisRaw(horizontalAxis);
        float y = Input.GetAxisRaw(verticalAxis);
        moveInput = new Vector2(x, y);
    }

    private void ReadButtons()
    {
        jumpPressed = Input.GetButtonDown(jumpButton);

        bool keyboardDash = Input.GetKey(keyboardDashKey);
        bool gamepadDash = Input.GetAxisRaw(dashTriggerAxis) >= triggerPressedThreshold;
        bool dashHeld = keyboardDash || gamepadDash;
        dashPressed = dashHeld && !dashHeldLastFrame;
        dashHeldLastFrame = dashHeld;

        bool mouseAttack = Input.GetMouseButton(mouseAttackButton);
        bool gamepadAttack = Input.GetAxisRaw(attackTriggerAxis) >= triggerPressedThreshold;
        bool attackHeld = mouseAttack || gamepadAttack;
        attackPressed = attackHeld && !attackHeldLastFrame;
        attackHeldLastFrame = attackHeld;
    }

    private void ReadAim()
    {
        Vector3 aimDir = Vector3.zero;
        bool foundAim = false;

        // Gamepad right stick aim gets first priority if actively being moved.
        float aimX = Input.GetAxisRaw(aimHorizontalAxis);
        float aimY = Input.GetAxisRaw(aimVerticalAxis);
        Vector2 stick = new Vector2(aimX, aimY);

        if (stick.magnitude >= gamepadAimDeadzone)
        {
            aimDir = new Vector3(stick.x, 0f, stick.y).normalized;
            foundAim = true;
        }
        else
        {
            // Mouse aim fallback.
            if (aimCamera != null && aimOrigin != null)
            {
                Ray ray = aimCamera.ScreenPointToRay(Input.mousePosition);

                if (useFlatAimPlane)
                {
                    Plane plane = new Plane(Vector3.up, aimOrigin.position);

                    if (plane.Raycast(ray, out float enter))
                    {
                        Vector3 hitPoint = ray.GetPoint(enter);
                        Vector3 toPoint = hitPoint - aimOrigin.position;
                        toPoint.y = 0f;

                        if (toPoint.sqrMagnitude > 0.001f)
                        {
                            aimDir = toPoint.normalized;
                            foundAim = true;
                        }
                    }
                }
            }
        }

        hasAim = foundAim;

        if (foundAim)
            lastAimDirection = aimDir;
    }

    public Vector3 GetAimDirectionClampedToPlayerForward(Vector3 playerForward, float maxAngleFromForward = 90f)
    {
        Vector3 forward = playerForward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;

        forward.y = 0f;
        forward.Normalize();

        Vector3 rawAim = hasAim ? lastAimDirection : forward;
        rawAim.y = 0f;

        if (rawAim.sqrMagnitude < 0.001f)
            rawAim = forward;

        rawAim.Normalize();

        float signedAngle = Vector3.SignedAngle(forward, rawAim, Vector3.up);
        signedAngle = Mathf.Clamp(signedAngle, -maxAngleFromForward, maxAngleFromForward);

        Vector3 clamped = Quaternion.AngleAxis(signedAngle, Vector3.up) * forward;
        clamped.y = 0f;

        if (clamped.sqrMagnitude < 0.001f)
            clamped = forward;

        return clamped.normalized;
    }
}