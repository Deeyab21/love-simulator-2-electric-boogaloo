using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class LoveCollector : MonoBehaviour
{
    [Header("References")]
    public LoveMeter loveMeter;
    public Transform attractTarget;

    [Header("Magnet")]
    public float magnetRange = 3.5f;
    public float collectRadius = 0.5f;
    public float pullSpeed = 18f;
    public bool accelerateAsItGetsCloser = true;
    public float closeRangeSpeedMultiplier = 1.5f;

    private SphereCollider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<SphereCollider>();
        triggerCollider.isTrigger = true;

        if (loveMeter == null)
            loveMeter = GetComponentInParent<LoveMeter>();

        if (attractTarget == null)
            attractTarget = transform;
    }

    private void OnValidate()
    {
        SphereCollider col = GetComponent<SphereCollider>();
        if (col != null)
        {
            col.isTrigger = true;
            col.radius = Mathf.Max(0.01f, magnetRange);
        }
    }

    private void LateUpdate()
    {
        if (triggerCollider != null)
            triggerCollider.radius = Mathf.Max(0.01f, magnetRange);
    }

    public Vector3 GetAttractPosition()
    {
        return attractTarget != null ? attractTarget.position : transform.position;
    }

    public float GetPullSpeed(float distanceToTarget)
    {
        float speed = pullSpeed;

        if (accelerateAsItGetsCloser && magnetRange > 0.001f)
        {
            float closeness = 1f - Mathf.Clamp01(distanceToTarget / magnetRange);
            speed *= Mathf.Lerp(1f, closeRangeSpeedMultiplier, closeness);
        }

        return speed;
    }

    public bool CanCollect()
    {
        return loveMeter != null;
    }

    public void AddLove(float amount)
    {
        if (loveMeter == null)
            return;

        loveMeter.AddLove(amount);
    }

    private void OnTriggerEnter(Collider other)
    {
        LovePellet pellet = other.GetComponent<LovePellet>();
        if (pellet == null)
            pellet = other.GetComponentInParent<LovePellet>();

        if (pellet != null)
            pellet.BeginMagnet(this);
    }

    private void OnTriggerExit(Collider other)
    {
        LovePellet pellet = other.GetComponent<LovePellet>();
        if (pellet == null)
            pellet = other.GetComponentInParent<LovePellet>();

        if (pellet != null)
            pellet.EndMagnet(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, magnetRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(GetAttractPosition(), collectRadius);
    }
}