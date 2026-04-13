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

    [Header("Detach")]
    [Tooltip("Optional root to detach before physics is enabled.")]
    public Transform rootToDetach;

    [Tooltip("If true, detaches the chosen root on impact.")]
    public bool detachRootOnImpact = false;

    [Header("Physics State")]
    [Tooltip("If true, rigidbodies are forced out of kinematic mode on impact.")]
    public bool makeBodiesNonKinematic = true;

    [Tooltip("If true, rigidbody constraints are cleared on impact.")]
    public bool clearConstraintsOnImpact = false;

    [Header("One Shot")]
    [Tooltip("If true, this impactable only reacts once until reset.")]
    public bool oneShot = false;

    private bool hasBeenImpacted;

    private void Awake()
    {
        RefreshRigidbodies();
    }

    [ContextMenu("Refresh Rigidbodies")]
    public void RefreshRigidbodies()
    {
        if (!autoFindRigidbodies)
            return;

        rigidbodies = GetComponentsInChildren<Rigidbody>(includeInactiveChildren);
    }

    public Rigidbody[] PrepareForImpact()
    {
        if (oneShot && hasBeenImpacted)
            return System.Array.Empty<Rigidbody>();

        hasBeenImpacted = true;

        if (autoFindRigidbodies && (rigidbodies == null || rigidbodies.Length == 0))
            RefreshRigidbodies();

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

            if (makeBodiesNonKinematic)
                rb.isKinematic = false;

            if (clearConstraintsOnImpact)
                rb.constraints = RigidbodyConstraints.None;

            rb.WakeUp();
        }

        return rigidbodies;
    }

    public void ResetImpactState()
    {
        hasBeenImpacted = false;
    }
}