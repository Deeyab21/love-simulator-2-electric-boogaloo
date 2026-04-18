using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SplineRailBumper : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Only objects on these layers will be bumped.")]
    public LayerMask targetLayers = ~0;

    [Tooltip("If true, searches parent objects for the HamsterBallController.")]
    public bool searchParentsForRunner = true;

    [Header("Bounce")]
    [Tooltip("Minimum speed pushing the player away from the rail.")]
    public float lateralBounceSpeed = 18f;

    [Tooltip("Extra bounce added based on how hard the player was moving into the wall.")]
    public float incomingSpeedToBounceMultiplier = 0.75f;

    [Tooltip("How far the player is nudged away from the rail after impact so they do not keep scraping along it.")]
    public float separationDistance = 1.25f;

    [Tooltip("Optional extra upward velocity added during the bounce.")]
    public float upwardBounceSpeed = 0f;

    [Header("Momentum Preservation")]
    [Tooltip("How much of the player's horizontal speed is kept after the bounce. 1 = keep all current speed.")]
    public float speedPreservation = 1f;

    [Tooltip("If true, preserves the player's current travel direction projected away from the wall.")]
    public bool preserveCurrentTravelDirection = true;

    [Header("Cooldown")]
    [Tooltip("Prevents repeated heavy bounce application every single physics step.")]
    public float repeatBounceCooldown = 0.08f;

    [Header("Debug")]
    public bool drawDebug = true;
    public float debugNormalLength = 1.5f;

    private float lastBounceTime = -999f;
    private Vector3 lastContactPoint;
    private Vector3 lastContactNormal;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = false;
    }

    private void OnValidate()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryBounce(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (Time.time < lastBounceTime + repeatBounceCooldown)
            return;

        TryBounce(collision);
    }

    private void TryBounce(Collision collision)
    {
        if (!IsInLayerMask(collision.gameObject.layer, targetLayers))
            return;

        HamsterBallController runner = FindRunner(collision.collider);
        if (runner == null)
            return;

        Rigidbody rb = runner.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        if (collision.contactCount == 0)
            return;

        Vector3 averageNormal = Vector3.zero;
        Vector3 averagePoint = Vector3.zero;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            averageNormal += contact.normal;
            averagePoint += contact.point;
        }

        averageNormal /= collision.contactCount;
        averagePoint /= collision.contactCount;

        if (averageNormal.sqrMagnitude < 0.0001f)
            return;

        averageNormal.Normalize();

        lastContactNormal = averageNormal;
        lastContactPoint = averagePoint;

        Vector3 horizontalVelocity = runner.GetHorizontalVelocity();
        float currentSpeed = horizontalVelocity.magnitude;

        Vector3 awayFromRail = averageNormal;
        awayFromRail.y = 0f;

        if (awayFromRail.sqrMagnitude < 0.0001f)
            awayFromRail = Vector3.ProjectOnPlane(averageNormal, Vector3.up);

        if (awayFromRail.sqrMagnitude < 0.0001f)
            awayFromRail = transform.right;

        awayFromRail.Normalize();

        float intoWallSpeed = 0f;
        if (currentSpeed > 0.001f)
            intoWallSpeed = Mathf.Max(0f, Vector3.Dot(horizontalVelocity, -awayFromRail));

        float bounceSpeed = lateralBounceSpeed + intoWallSpeed * incomingSpeedToBounceMultiplier;

        Vector3 preservedDirection;
        if (preserveCurrentTravelDirection && currentSpeed > 0.001f)
        {
            preservedDirection = Vector3.ProjectOnPlane(horizontalVelocity.normalized, -awayFromRail);
            preservedDirection.y = 0f;

            if (preservedDirection.sqrMagnitude < 0.0001f)
                preservedDirection = Vector3.Cross(Vector3.up, awayFromRail);
        }
        else
        {
            preservedDirection = Vector3.Cross(Vector3.up, awayFromRail);
        }

        preservedDirection.y = 0f;
        preservedDirection.Normalize();

        float preservedSpeed = currentSpeed * speedPreservation;

        Vector3 newHorizontal =
            preservedDirection * preservedSpeed +
            awayFromRail * bounceSpeed;

        float minimumFinalSpeed = Mathf.Max(currentSpeed * speedPreservation, preservedSpeed);
        if (newHorizontal.sqrMagnitude > 0.001f && newHorizontal.magnitude < minimumFinalSpeed)
            newHorizontal = newHorizontal.normalized * minimumFinalSpeed;

        rb.linearVelocity = new Vector3(
            newHorizontal.x,
            rb.linearVelocity.y + upwardBounceSpeed,
            newHorizontal.z
        );

        rb.position += awayFromRail * separationDistance;

        lastBounceTime = Time.time;
    }

    private HamsterBallController FindRunner(Collider other)
    {
        if (searchParentsForRunner)
            return other.GetComponentInParent<HamsterBallController>();

        return other.GetComponent<HamsterBallController>();
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebug || lastContactNormal.sqrMagnitude < 0.0001f)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(lastContactPoint, lastContactPoint + lastContactNormal * debugNormalLength);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(lastContactPoint, 0.08f);
    }
}