using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LovePellet : MonoBehaviour
{
    [Header("Value")]
    public float loveValue = 5f;

    [Header("Visual Motion")]
    public bool rotate = true;
    public float rotateSpeed = 120f;
    public bool bob = true;
    public float bobHeight = 0.15f;
    public float bobSpeed = 3f;

    [Header("Pickup")]
    public bool destroyOnPickup = true;

    [Header("Magnet")]
    public bool canBeMagnetized = true;
    public float snapCollectBuffer = 0.05f;

    private Vector3 startLocalPos;
    private LoveCollector currentCollector;
    private bool collected;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        startLocalPos = transform.localPosition;
    }

    private void Update()
    {
        if (collected)
            return;

        if (currentCollector != null && canBeMagnetized && currentCollector.CanCollect())
        {
            UpdateMagnetMovement();
            return;
        }

        UpdateIdleMotion();
    }

    private void UpdateIdleMotion()
    {
        if (rotate)
        {
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }

        if (bob)
        {
            Vector3 p = startLocalPos;
            p.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.localPosition = p;
        }
    }

    private void UpdateMagnetMovement()
    {
        Vector3 targetPos = currentCollector.GetAttractPosition();
        Vector3 toTarget = targetPos - transform.position;
        float distance = toTarget.magnitude;

        if (distance <= currentCollector.collectRadius + snapCollectBuffer)
        {
            Collect(currentCollector);
            return;
        }

        if (distance > 0.0001f)
        {
            float speed = currentCollector.GetPullSpeed(distance);
            Vector3 move = toTarget.normalized * speed * Time.deltaTime;

            if (move.magnitude >= distance)
            {
                transform.position = targetPos;
                Collect(currentCollector);
                return;
            }

            transform.position += move;
        }

        if (rotate)
        {
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }
    }

    public void BeginMagnet(LoveCollector collector)
    {
        if (!canBeMagnetized || collected)
            return;

        currentCollector = collector;
    }

    public void EndMagnet(LoveCollector collector)
    {
        if (currentCollector == collector)
            currentCollector = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        LoveCollector collector = other.GetComponent<LoveCollector>();
        if (collector == null)
            collector = other.GetComponentInParent<LoveCollector>();

        if (collector != null)
        {
            BeginMagnet(collector);

            float dist = Vector3.Distance(transform.position, collector.GetAttractPosition());
            if (dist <= collector.collectRadius + snapCollectBuffer)
            {
                Collect(collector);
            }

            return;
        }

        LoveMeter meter = other.GetComponentInParent<LoveMeter>();
        if (meter != null)
        {
            meter.AddLove(loveValue);

            if (destroyOnPickup)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }
    }

    private void Collect(LoveCollector collector)
    {
        if (collected)
            return;

        collected = true;

        if (collector != null)
            collector.AddLove(loveValue);

        if (destroyOnPickup)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }
}