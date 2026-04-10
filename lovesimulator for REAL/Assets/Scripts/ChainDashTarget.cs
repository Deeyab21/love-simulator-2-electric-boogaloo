using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ChainDashTarget : MonoBehaviour
{
    [Header("Target Points")]
    [Tooltip("Where the player is pulled to during the chain dash.")]
    public Transform aimPoint;

    [Tooltip("Empty transform that defines the launch direction after impact.")]
    public Transform launchDirectionPoint;

    [Header("Homing")]
    [Tooltip("How close the player has to get before the target is considered reached.")]
    public float arriveDistance = 0.15f;

    [Header("Impact")]
    [Tooltip("Tiny freeze/pause on hit.")]
    public float hitStopDuration = 0.05f;

    [Tooltip("Speed applied after hit-stop in the dictated launch direction.")]
    public float launchSpeed = 95f;

    [Tooltip("Optional extra vertical velocity added after launch.")]
    public float bonusUpwardLaunch = 0f;

    [Tooltip("How long ground stick is disabled after launch.")]
    public float detachFromGroundTime = 0.10f;

    [Header("Availability")]
    [Tooltip("If true, the target disables briefly after being hit.")]
    public bool disableTemporarilyAfterHit = true;

    [Tooltip("How long the target stays disabled after being hit.")]
    public float reactivateDelay = 0.35f;

    [Header("Visual Swap")]
    [Tooltip("Prefab shown when this target is not the currently locked one.")]
    public GameObject defaultVisualPrefab;

    [Tooltip("Prefab shown when this target is the currently locked one.")]
    public GameObject lockedVisualPrefab;

    [Tooltip("Optional parent for spawned visuals. If null, this transform is used.")]
    public Transform visualParent;

    [Tooltip("If true, the spawned visuals are aligned to the aim point instead of this object.")]
    public bool spawnVisualsAtAimPoint = false;

    [Header("Debug")]
    public bool drawGizmos = true;

    private Collider cachedCollider;
    private bool isAvailable = true;
    private bool isPreviewed;

    private GameObject defaultVisualInstance;
    private GameObject lockedVisualInstance;

    private void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        cachedCollider.isTrigger = true;

        if (visualParent == null)
            visualParent = transform;

        CreateVisualsIfNeeded();
        RefreshVisualState();
    }

    private void OnEnable()
    {
        RefreshVisualState();
    }

    private void OnDisable()
    {
        isPreviewed = false;
        RefreshVisualState();
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
        RefreshVisualState();
    }

    public void NotifyHit()
    {
        if (disableTemporarilyAfterHit)
            StartCoroutine(DisableRoutine());
    }

    private IEnumerator DisableRoutine()
    {
        isAvailable = false;
        isPreviewed = false;
        RefreshVisualState();

        if (cachedCollider != null)
            cachedCollider.enabled = false;

        yield return new WaitForSeconds(reactivateDelay);

        if (cachedCollider != null)
            cachedCollider.enabled = true;

        isAvailable = true;
        RefreshVisualState();
    }

    private void CreateVisualsIfNeeded()
    {
        if (defaultVisualPrefab != null && defaultVisualInstance == null)
        {
            defaultVisualInstance = Instantiate(defaultVisualPrefab, GetVisualSpawnPosition(), GetVisualSpawnRotation(), visualParent);
            defaultVisualInstance.name = defaultVisualPrefab.name + "_DefaultPreview";
        }

        if (lockedVisualPrefab != null && lockedVisualInstance == null)
        {
            lockedVisualInstance = Instantiate(lockedVisualPrefab, GetVisualSpawnPosition(), GetVisualSpawnRotation(), visualParent);
            lockedVisualInstance.name = lockedVisualPrefab.name + "_LockedPreview";
        }
    }

    private Vector3 GetVisualSpawnPosition()
    {
        if (spawnVisualsAtAimPoint && aimPoint != null)
            return aimPoint.position;

        return visualParent != null ? visualParent.position : transform.position;
    }

    private Quaternion GetVisualSpawnRotation()
    {
        if (spawnVisualsAtAimPoint && aimPoint != null)
            return aimPoint.rotation;

        return visualParent != null ? visualParent.rotation : transform.rotation;
    }

    private void RefreshVisualState()
    {
        if (defaultVisualInstance == null && lockedVisualInstance == null)
            CreateVisualsIfNeeded();

        bool showLocked = isPreviewed && isAvailable;
        bool showDefault = !showLocked;

        if (defaultVisualInstance != null)
            defaultVisualInstance.SetActive(showDefault);

        if (lockedVisualInstance != null)
            lockedVisualInstance.SetActive(showLocked);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        Vector3 aim = aimPoint != null ? aimPoint.position : transform.position;
        Vector3 launchDir = GetLaunchDirection();

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(aim, Mathf.Max(0.01f, arriveDistance));

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(aim, aim + launchDir * 2.5f);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(aim + launchDir * 2.5f, 0.15f);
    }
}