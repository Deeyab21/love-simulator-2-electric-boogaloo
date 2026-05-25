using UnityEngine;

public class CameraJitterDebugger : MonoBehaviour
{
    [Header("Transforms To Check")]
    public Transform playerRoot;
    public Transform visualRoot;
    public Transform cameraTarget;
    public Transform cameraTransform;

    [Header("Debug Settings")]
    public bool drawGizmos = true;
    public bool showOnGUI = true;

    [Tooltip("Movement smaller than this is ignored in the display.")]
    public float tinyMovementDeadzone = 0.0001f;

    [Tooltip("If true, logs big jitter spikes to the Console.")]
    public bool logSpikes = false;

    [Tooltip("World-space movement per frame that counts as a spike.")]
    public float spikeThreshold = 0.03f;

    private Vector3 lastPlayerPos;
    private Vector3 lastVisualPos;
    private Vector3 lastTargetPos;
    private Vector3 lastCameraPos;

    private Quaternion lastPlayerRot;
    private Quaternion lastVisualRot;
    private Quaternion lastTargetRot;
    private Quaternion lastCameraRot;

    private float playerMove;
    private float visualMove;
    private float targetMove;
    private float cameraMove;

    private float playerRotDelta;
    private float visualRotDelta;
    private float targetRotDelta;
    private float cameraRotDelta;

    private bool hasLastFrame;

    private void LateUpdate()
    {
        if (!hasLastFrame)
        {
            CaptureLastFrame();
            hasLastFrame = true;
            return;
        }

        MeasureTransform(playerRoot, lastPlayerPos, lastPlayerRot, out playerMove, out playerRotDelta);
        MeasureTransform(visualRoot, lastVisualPos, lastVisualRot, out visualMove, out visualRotDelta);
        MeasureTransform(cameraTarget, lastTargetPos, lastTargetRot, out targetMove, out targetRotDelta);
        MeasureTransform(cameraTransform, lastCameraPos, lastCameraRot, out cameraMove, out cameraRotDelta);

        if (logSpikes)
        {
            if (playerMove > spikeThreshold)
                Debug.Log($"PLAYER ROOT jitter spike: {playerMove:F5}");

            if (visualMove > spikeThreshold)
                Debug.Log($"VISUAL ROOT jitter spike: {visualMove:F5}");

            if (targetMove > spikeThreshold)
                Debug.Log($"CAMERA TARGET jitter spike: {targetMove:F5}");

            if (cameraMove > spikeThreshold)
                Debug.Log($"CAMERA jitter spike: {cameraMove:F5}");
        }

        CaptureLastFrame();
    }

    private void MeasureTransform(
        Transform t,
        Vector3 lastPos,
        Quaternion lastRot,
        out float moveAmount,
        out float rotationAmount)
    {
        moveAmount = 0f;
        rotationAmount = 0f;

        if (t == null)
            return;

        moveAmount = Vector3.Distance(t.position, lastPos);
        rotationAmount = Quaternion.Angle(lastRot, t.rotation);

        if (moveAmount < tinyMovementDeadzone)
            moveAmount = 0f;

        if (rotationAmount < tinyMovementDeadzone)
            rotationAmount = 0f;
    }

    private void CaptureLastFrame()
    {
        if (playerRoot != null)
        {
            lastPlayerPos = playerRoot.position;
            lastPlayerRot = playerRoot.rotation;
        }

        if (visualRoot != null)
        {
            lastVisualPos = visualRoot.position;
            lastVisualRot = visualRoot.rotation;
        }

        if (cameraTarget != null)
        {
            lastTargetPos = cameraTarget.position;
            lastTargetRot = cameraTarget.rotation;
        }

        if (cameraTransform != null)
        {
            lastCameraPos = cameraTransform.position;
            lastCameraRot = cameraTransform.rotation;
        }
    }

    private void OnGUI()
    {
        if (!showOnGUI)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 18;
        style.normal.textColor = Color.white;

        float y = 20f;

        GUI.Label(new Rect(20, y, 900, 24), "CAMERA JITTER DEBUGGER", style);
        y += 30f;

        DrawLine(ref y, "Player Root", playerMove, playerRotDelta, style);
        DrawLine(ref y, "Visual Root", visualMove, visualRotDelta, style);
        DrawLine(ref y, "Camera Target", targetMove, targetRotDelta, style);
        DrawLine(ref y, "Camera", cameraMove, cameraRotDelta, style);
    }

    private void DrawLine(ref float y, string label, float move, float rot, GUIStyle style)
    {
        GUI.Label(
            new Rect(20, y, 900, 24),
            $"{label} | Move/frame: {move:F5} | Rot/frame: {rot:F3}°",
            style
        );

        y += 24f;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        DrawTransformGizmo(playerRoot, Color.green, 0.35f);
        DrawTransformGizmo(visualRoot, Color.yellow, 0.28f);
        DrawTransformGizmo(cameraTarget, Color.cyan, 0.22f);
        DrawTransformGizmo(cameraTransform, Color.magenta, 0.18f);
    }

    private void DrawTransformGizmo(Transform t, Color color, float size)
    {
        if (t == null)
            return;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(t.position, size);

        Gizmos.DrawLine(t.position, t.position + t.forward * size * 2f);
    }
}