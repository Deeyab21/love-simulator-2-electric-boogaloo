using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ChainDashImpactable : MonoBehaviour
{
    [Header("Bodies")]
    [Tooltip("If true, automatically finds all child rigidbodies.")]
    public bool autoFindRigidbodies = true;

    [Tooltip("Include inactive child rigidbodies when auto-finding.")]
    public bool includeInactiveChildren = true;

    [Tooltip("Rigidbodies controlled by this impactable.")]
    public Rigidbody[] rigidbodies;

    [Header("Pre-Impact State")]
    [Tooltip("If true, bodies stay kinematic until the exact launch moment.")]
    public bool keepBodiesKinematicUntilImpact = true;

    [Tooltip("If true, gravity stays off until the exact launch moment.")]
    public bool disableGravityUntilImpact = true;

    [Tooltip("If true, rigidbody collisions stay off until the exact launch moment.")]
    public bool disableCollisionsUntilImpact = false;

    [Header("Detach")]
    [Tooltip("Optional root to detach before physics is enabled.")]
    public Transform rootToDetach;

    [Tooltip("If true, detaches the chosen root on impact.")]
    public bool detachRootOnImpact = false;

    [Header("Physics State On Launch")]
    [Tooltip("If true, rigidbodies are forced out of kinematic mode when launched.")]
    public bool makeBodiesNonKinematic = true;

    [Tooltip("If true, rigidbody constraints are cleared when launched.")]
    public bool clearConstraintsOnImpact = false;

    [Header("Explosion After Fly Time")]
    [Tooltip("If true, this impactable spawns an explosion VFX after a short fly time.")]
    public bool explodeAfterFlyTime = false;

    [Tooltip("How long the launched debris flies before the explosion VFX triggers.")]
    public float flyTimeBeforeExplode = 0.6f;

    [Tooltip("Custom VFX spawned after the fly time ends.")]
    public GameObject explodeVfxPrefab;

    [Tooltip("Optional spawn point for the explosion VFX. If null, uses this object or detached root.")]
    public Transform explodeVfxSpawnPoint;

    [Tooltip("How long the spawned explosion VFX should live before being destroyed.")]
    public float explodeVfxLifetime = 5f;

    [Tooltip("If true, disables the debris root after the explosion VFX triggers.")]
    public bool disableRootAfterExplosion = false;

    [Tooltip("If true, destroys the debris root after the explosion VFX triggers.")]
    public bool destroyRootAfterExplosion = false;

    [Header("One Shot")]
    [Tooltip("If true, this impactable only reacts once until reset.")]
    public bool oneShot = false;

    private bool hasBeenImpacted;

    private bool[] originalKinematicStates;
    private bool[] originalGravityStates;
    private bool[] originalDetectCollisionStates;
    private RigidbodyConstraints[] originalConstraints;

    private Transform originalParent;
    private Coroutine explodeRoutine;

    private void Awake()
    {
        originalParent = transform.parent;
        RefreshRigidbodies();
        CacheOriginalStates();
        ApplyPreImpactState();
    }

    private void OnEnable()
    {
        if (rigidbodies == null || rigidbodies.Length == 0)
            RefreshRigidbodies();

        if (originalKinematicStates == null || originalKinematicStates.Length != rigidbodies.Length)
            CacheOriginalStates();

        if (originalParent == null)
            originalParent = transform.parent;

        ApplyPreImpactState();
    }

    [ContextMenu("Refresh Rigidbodies")]
    public void RefreshRigidbodies()
    {
        if (!autoFindRigidbodies)
            return;

        rigidbodies = GetComponentsInChildren<Rigidbody>(includeInactiveChildren);
    }

    [ContextMenu("Cache Original States")]
    public void CacheOriginalStates()
    {
        if (rigidbodies == null)
        {
            originalKinematicStates = null;
            originalGravityStates = null;
            originalDetectCollisionStates = null;
            originalConstraints = null;
            return;
        }

        originalKinematicStates = new bool[rigidbodies.Length];
        originalGravityStates = new bool[rigidbodies.Length];
        originalDetectCollisionStates = new bool[rigidbodies.Length];
        originalConstraints = new RigidbodyConstraints[rigidbodies.Length];

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];
            if (rb == null)
                continue;

            originalKinematicStates[i] = rb.isKinematic;
            originalGravityStates[i] = rb.useGravity;
            originalDetectCollisionStates[i] = rb.detectCollisions;
            originalConstraints[i] = rb.constraints;
        }
    }

    [ContextMenu("Apply Pre-Impact State")]
    public void ApplyPreImpactState()
    {
        if (rigidbodies == null)
            return;

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];
            if (rb == null)
                continue;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (keepBodiesKinematicUntilImpact)
                rb.isKinematic = true;

            if (disableGravityUntilImpact)
                rb.useGravity = false;

            if (disableCollisionsUntilImpact)
                rb.detectCollisions = false;
        }
    }

    public Rigidbody[] PrepareForImpact()
    {
        if (oneShot && hasBeenImpacted)
            return System.Array.Empty<Rigidbody>();

        hasBeenImpacted = true;

        if (autoFindRigidbodies && (rigidbodies == null || rigidbodies.Length == 0))
        {
            RefreshRigidbodies();
            CacheOriginalStates();
            ApplyPreImpactState();
        }

        if (detachRootOnImpact)
        {
            Transform detachTarget = rootToDetach != null ? rootToDetach : transform;
            detachTarget.SetParent(null, true);
        }

        if (rigidbodies == null)
            return System.Array.Empty<Rigidbody>();

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];
            if (rb == null)
                continue;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (disableCollisionsUntilImpact)
                rb.detectCollisions = true;

            if (disableGravityUntilImpact)
                rb.useGravity = true;

            if (makeBodiesNonKinematic)
                rb.isKinematic = false;

            if (clearConstraintsOnImpact)
                rb.constraints = RigidbodyConstraints.None;

            rb.WakeUp();
        }

        if (explodeRoutine != null)
            StopCoroutine(explodeRoutine);

        if (explodeAfterFlyTime)
            explodeRoutine = StartCoroutine(ExplodeAfterDelayRoutine());

        return rigidbodies;
    }

    private IEnumerator ExplodeAfterDelayRoutine()
    {
        float delay = Mathf.Max(0f, flyTimeBeforeExplode);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Transform spawnAnchor = explodeVfxSpawnPoint != null
            ? explodeVfxSpawnPoint
            : (rootToDetach != null ? rootToDetach : transform);

        if (explodeVfxPrefab != null)
        {
            GameObject vfx = Instantiate(
                explodeVfxPrefab,
                spawnAnchor.position,
                spawnAnchor.rotation
            );
            Destroy(vfx, explodeVfxLifetime);
        }

        if (destroyRootAfterExplosion)
        {
            Transform root = rootToDetach != null ? rootToDetach : transform;
            Destroy(root.gameObject);
        }
        else if (disableRootAfterExplosion)
        {
            Transform root = rootToDetach != null ? rootToDetach : transform;
            root.gameObject.SetActive(false);
        }

        explodeRoutine = null;
    }

    public void ResetImpactState()
    {
        hasBeenImpacted = false;

        if (explodeRoutine != null)
        {
            StopCoroutine(explodeRoutine);
            explodeRoutine = null;
        }

        Transform resetRoot = rootToDetach != null ? rootToDetach : transform;
        if (resetRoot != null && !resetRoot.gameObject.activeSelf)
            resetRoot.gameObject.SetActive(true);

        if (detachRootOnImpact && resetRoot != null && originalParent != null)
            resetRoot.SetParent(originalParent, true);

        if (rigidbodies == null)
            return;

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];
            if (rb == null)
                continue;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (originalConstraints != null && i < originalConstraints.Length)
                rb.constraints = originalConstraints[i];

            if (keepBodiesKinematicUntilImpact)
                rb.isKinematic = true;
            else if (originalKinematicStates != null && i < originalKinematicStates.Length)
                rb.isKinematic = originalKinematicStates[i];

            if (disableGravityUntilImpact)
                rb.useGravity = false;
            else if (originalGravityStates != null && i < originalGravityStates.Length)
                rb.useGravity = originalGravityStates[i];

            if (disableCollisionsUntilImpact)
                rb.detectCollisions = false;
            else if (originalDetectCollisionStates != null && i < originalDetectCollisionStates.Length)
                rb.detectCollisions = originalDetectCollisionStates[i];
        }
    }
}