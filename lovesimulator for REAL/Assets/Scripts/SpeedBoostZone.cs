using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpeedBoostZone : MonoBehaviour
{
    [Header("Boost Settings")]
    public float accelerationBonus = 80f;
    public float maxSpeedBonus = 60f;
    public float duration = 2.0f;
    public float instantSpeedBonus = 20f;

    [Header("Behavior")]
    public bool refreshDuration = true;
    public bool oneShotPerEntry = true;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        HamsterBallController player = other.GetComponentInParent<HamsterBallController>();
        if (player == null)
            return;

        player.ApplySpeedBoost(
            accelerationBonus,
            maxSpeedBonus,
            duration,
            instantSpeedBonus
        );
    }

    private void OnTriggerStay(Collider other)
    {
        if (!refreshDuration || oneShotPerEntry)
            return;

        HamsterBallController player = other.GetComponentInParent<HamsterBallController>();
        if (player == null)
            return;

        player.ApplySpeedBoost(
            accelerationBonus,
            maxSpeedBonus,
            duration,
            0f
        );
    }
}