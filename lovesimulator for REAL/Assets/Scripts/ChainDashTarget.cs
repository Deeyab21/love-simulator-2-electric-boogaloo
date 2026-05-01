using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ChainDashTarget : MonoBehaviour
{
    [Header("Target Points")]
    [Tooltip("Where the player is pulled to during the chain dash.")]
    public Transform aimPoint;

    [Tooltip("Empty transform that defines the player launch direction after impact.")]
    public Transform launchDirectionPoint;

    [Header("Homing")]
    [Tooltip("How close the player has to get before the target is considered reached.")]
    public float arriveDistance = 0.15f;

    [Header("Impact / Launch")]
    [Tooltip("Tiny freeze/pause on hit.")]
    public float hitStopDuration = 0.05f;

    [Tooltip("Speed applied after hit-stop in the dictated launch direction.")]
    public float launchSpeed = 95f;

    [Tooltip("Optional extra vertical velocity added after launch.")]
    public float bonusUpwardLaunch = 0f;

    [Tooltip("How long ground stick is disabled after launch.")]
    public float detachFromGroundTime = 0.10f;

    [Header("Timing")]
    [Tooltip("Delay before debris is launched after impact is triggered.")]
    public float debrisLaunchDelay = 0f;

    [Header("Debris Launch")]
    [Tooltip("If true, uses the aim point as the center of the launch calculation.")]
    public bool useAimPointAsImpactCenter = true;

    [Tooltip("Base outward force from the center so debris still scatters.")]
    public float radialForce = 20f;

    [Tooltip("Extra force applied in the same direction the player launches.")]
    public float launchDirectionForce = 24f;

    [Tooltip("Extra upward lift added to the final launch vector.")]
    public float upwardForce = 4f;

    [Tooltip("Only rigidbodies within this radius are affected.")]
    public float impactRadius = 6f;

    [Tooltip("If true, objects farther from the center receive less force.")]
    public bool useDistanceFalloff = true;

    [Tooltip("How strongly force falls off with distance. 1 = linear style.")]
    public float distanceFalloffPower = 1f;

    [Header("Spin")]
    [Tooltip("If true, adds random spin to launched rigidbodies.")]
    public bool addRandomTorque = true;

    [Tooltip("How much random spin is added.")]
    public float randomTorqueAmount = 18f;

    [Header("Impact Targets")]
    [Tooltip("Manually assign the impactable objects that this target should launch.")]
    public List<ChainDashImpactable> impactables = new List<ChainDashImpactable>();

    [Tooltip("Fallback: also affect loose rigidbodies found in the radius.")]
    public bool affectLooseRigidbodiesInRadius = false;

    [Tooltip("Layers used when searching for loose rigidbodies.")]
    public LayerMask fallbackImpactLayers = ~0;

    [Header("Impact VFX")]
    public GameObject impactVfxPrefab;
    public Transform impactVfxSpawnPoint;
    public float impactVfxLifetime = 5f;

    [Header("Lock-On Glow")]
    [Tooltip("Optional glow used when this target is the current lock-on preview target.")]
    public ChainDashTargetGlow targetGlow;

    [Tooltip("If true, the target tries to find a ChainDashTargetGlow on itself or its children.")]
    public bool autoFindTargetGlow = true;


 

    [Header("Availability")]
    [Tooltip("If true, the target disables briefly after being hit.")]
    public bool disableTemporarilyAfterHit = true;

    [Tooltip("How long the target stays disabled after being hit.")]
    public float reactivateDelay = 0.35f;

    [Header("Destroy On Hit")]
    [Tooltip("If true, the target will be destroyed after being hit.")]
    public bool destroyOnHit = false;

    [Tooltip("Delay before the object is destroyed.")]
    public float destroyDelay = 0.15f;

    [Tooltip("VFX prefab spawned when destroyed.")]
    public GameObject destroyVfxPrefab;

    [Tooltip("Optional custom spawn point. If null, uses this transform.")]
    public Transform destroyVfxSpawnPoint;

    [Tooltip("How long the VFX lives before being cleaned up.")]
    public float destroyVfxLifetime = 5f;

    [Header("Debug")]
    public bool drawGizmos = true;
    public float directionGizmoLength = 2.5f;
    public float directionGizmoArrowSize = 0.35f;

    private Collider cachedCollider;
    private bool isAvailable = true;
    private bool isPreviewed;
    private bool impactTriggeredThisDisableCycle;

    private Coroutine disableRoutine;
    private Coroutine delayedImpactRoutine;

   

    private void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        cachedCollider.isTrigger = true;


        if (targetGlow == null && autoFindTargetGlow)
            targetGlow = GetComponentInChildren<ChainDashTargetGlow>(true);

        
    }

    private void OnEnable()
    {
        UpdateTargetGlow();
    }

    private void OnDisable()
    {
        isPreviewed = false;
        UpdateTargetGlow();
    }

    private void OnDestroy()
    {
        
    }

    private void OnValidate()
    {
        if (cachedCollider == null)
            cachedCollider = GetComponent<Collider>();

        if (cachedCollider != null)
            cachedCollider.isTrigger = true;
    }

 

    public bool CanBeTargeted()
    {
        return isAvailable && enabled && gameObject.activeInHierarchy;
    }

    public Vector3 GetAimPosition()
    {
        return aimPoint != null ? aimPoint.position : transform.position;
    }

    public Vector3 GetLaunchDirection()
    {
        Vector3 origin = GetAimPosition();

        if (launchDirectionPoint != null)
        {
            Vector3 dir = launchDirectionPoint.position - origin;
            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;
        }

        return transform.forward.normalized;
    }

    public float GetArriveDistance()
    {
        return Mathf.Max(0.01f, arriveDistance);
    }

    public float GetHitStopDuration()
    {
        return Mathf.Max(0f, hitStopDuration);
    }

    public float GetLaunchSpeed()
    {
        return Mathf.Max(0f, launchSpeed);
    }

    public float GetBonusUpwardLaunch()
    {
        return bonusUpwardLaunch;
    }

    public float GetDetachFromGroundTime()
    {
        return Mathf.Max(0f, detachFromGroundTime);
    }

    public void SetPreviewed(bool previewed)
    {
        if (isPreviewed == previewed)
            return;

        isPreviewed = previewed;

        UpdateTargetGlow();
    }

    public void NotifyHit()
    {
        if (!isAvailable)
            return;

        if (destroyOnHit)
        {
            StartCoroutine(DestroyRoutine());
            return;
        }

        if (disableTemporarilyAfterHit)
        {
            if (disableRoutine != null)
                StopCoroutine(disableRoutine);

            disableRoutine = StartCoroutine(DisableRoutine());
        }
    }

    private IEnumerator DestroyRoutine()
    {
        isAvailable = false;
        isPreviewed = false;

        if (cachedCollider != null)
            cachedCollider.enabled = false;

        float delay = Mathf.Max(0f, destroyDelay);

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // Spawn VFX
        if (destroyVfxPrefab != null)
        {
            Transform anchor = destroyVfxSpawnPoint != null ? destroyVfxSpawnPoint : transform;

            GameObject vfx = Instantiate(
                destroyVfxPrefab,
                anchor.position,
                anchor.rotation
            );

            Destroy(vfx, destroyVfxLifetime);
        }

        Destroy(gameObject);
    }

    public void PlayImpactVfxOnly()
    {
        Vector3 impactCenter = GetImpactCenter();

        if (impactVfxPrefab != null)
        {
            Vector3 vfxPos = impactVfxSpawnPoint != null ? impactVfxSpawnPoint.position : impactCenter;
            Quaternion vfxRot = impactVfxSpawnPoint != null ? impactVfxSpawnPoint.rotation : Quaternion.identity;

            GameObject vfx = Instantiate(impactVfxPrefab, vfxPos, vfxRot);
            Destroy(vfx, impactVfxLifetime);
        }
    }

    public void TriggerZoneImpact()
    {
        if (impactTriggeredThisDisableCycle)
            return;

        impactTriggeredThisDisableCycle = true;

        if (delayedImpactRoutine != null)
            StopCoroutine(delayedImpactRoutine);

        delayedImpactRoutine = StartCoroutine(DelayedImpactRoutine());
    }

    private IEnumerator DelayedImpactRoutine()
    {
        float delay = Mathf.Max(0f, debrisLaunchDelay);

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Vector3 impactCenter = GetImpactCenter();
        HashSet<Rigidbody> affectedBodies = GatherAffectedRigidbodies(impactCenter);
        ApplyUnifiedLaunch(affectedBodies, impactCenter);

        delayedImpactRoutine = null;
    }

    private Vector3 GetImpactCenter()
    {
        return useAimPointAsImpactCenter ? GetAimPosition() : transform.position;
    }

    private HashSet<Rigidbody> GatherAffectedRigidbodies(Vector3 impactCenter)
    {
        HashSet<Rigidbody> affectedBodies = new HashSet<Rigidbody>();

        for (int i = 0; i < impactables.Count; i++)
        {
            ChainDashImpactable impactable = impactables[i];
            if (impactable == null)
                continue;

            Rigidbody[] bodies = impactable.PrepareForImpact();

            for (int j = 0; j < bodies.Length; j++)
            {
                Rigidbody rb = bodies[j];
                if (rb == null)
                    continue;

                if (Vector3.Distance(rb.worldCenterOfMass, impactCenter) <= impactRadius)
                    affectedBodies.Add(rb);
            }
        }

        if (affectLooseRigidbodiesInRadius)
        {
            Collider[] hits = Physics.OverlapSphere(
                impactCenter,
                impactRadius,
                fallbackImpactLayers,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hits.Length; i++)
            {
                Rigidbody rb = hits[i].attachedRigidbody;
                if (rb == null || affectedBodies.Contains(rb))
                    continue;

                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.useGravity = true;
                rb.WakeUp();
                affectedBodies.Add(rb);
            }
        }

        return affectedBodies;
    }

    private void ApplyUnifiedLaunch(HashSet<Rigidbody> affectedBodies, Vector3 impactCenter)
    {
        if (affectedBodies == null || affectedBodies.Count == 0)
            return;

        Vector3 playerLaunchDir = GetLaunchDirection();
        if (playerLaunchDir.sqrMagnitude < 0.0001f)
            playerLaunchDir = transform.forward;

        playerLaunchDir.Normalize();

        foreach (Rigidbody rb in affectedBodies)
        {
            if (rb == null)
                continue;

            Vector3 fromCenter = rb.worldCenterOfMass - impactCenter;
            float distance = fromCenter.magnitude;

            Vector3 radialDir = distance > 0.0001f ? (fromCenter / distance) : Vector3.up;

            float falloff = 1f;
            if (useDistanceFalloff && impactRadius > 0.001f)
            {
                float t = Mathf.Clamp01(distance / impactRadius);
                falloff = 1f - Mathf.Pow(t, Mathf.Max(0.01f, distanceFalloffPower));
            }

            Vector3 sideScatter = radialDir * radialForce;
            Vector3 launchVector =
                (playerLaunchDir * launchDirectionForce) +
                sideScatter +
                (Vector3.up * upwardForce);

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(launchVector * falloff, ForceMode.Impulse);

            if (addRandomTorque && randomTorqueAmount > 0f)
                rb.AddTorque(Random.onUnitSphere * randomTorqueAmount * falloff, ForceMode.Impulse);
        }
    }

    private IEnumerator DisableRoutine()
    {
        isAvailable = false;
        isPreviewed = false;

        if (cachedCollider != null)
            cachedCollider.enabled = false;

        yield return new WaitForSeconds(reactivateDelay);

        if (cachedCollider != null)
            cachedCollider.enabled = true;

        for (int i = 0; i < impactables.Count; i++)
        {
            if (impactables[i] != null)
                impactables[i].ResetImpactState();
        }

        impactTriggeredThisDisableCycle = false;
        isAvailable = true;
        disableRoutine = null;
    }

   

    

    private void UpdateTargetGlow()
    {
        if (targetGlow == null && autoFindTargetGlow)
            targetGlow = GetComponentInChildren<ChainDashTargetGlow>(true);

        if (targetGlow == null)
            return;

        bool shouldShow = isPreviewed && isAvailable;
        targetGlow.SetHighlighted(shouldShow);
    }

  

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        Vector3 aim = aimPoint != null ? aimPoint.position : transform.position;
        Vector3 playerLaunchDir = GetLaunchDirection();
        Vector3 impactCenter = GetImpactCenter();
        Vector3 dirEnd = impactCenter + playerLaunchDir.normalized * directionGizmoLength;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(aim, Mathf.Max(0.01f, arriveDistance));
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(aim, aim + playerLaunchDir * 2.5f);
        Gizmos.DrawSphere(aim + playerLaunchDir * 2.5f, 0.15f);

        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(impactCenter, impactRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(impactCenter, dirEnd);
        Gizmos.DrawSphere(dirEnd, 0.12f);

        Vector3 side = Vector3.Cross(playerLaunchDir, Vector3.up);
        if (side.sqrMagnitude < 0.001f)
            side = Vector3.Cross(playerLaunchDir, Vector3.right);

        side.Normalize();

        Vector3 arrowBack = -playerLaunchDir.normalized * directionGizmoArrowSize;
        Vector3 arrowSide = side * directionGizmoArrowSize * 0.6f;

        Gizmos.DrawLine(dirEnd, dirEnd + arrowBack + arrowSide);
        Gizmos.DrawLine(dirEnd, dirEnd + arrowBack - arrowSide);
    }

}