using UnityEngine;

public class LoveMeter : MonoBehaviour
{
    [Header("Meter")]
    public float currentLove = 0f;
    public float maxLove = 100f;

    public float CurrentNormalized
    {
        get
        {
            if (maxLove <= 0.001f) return 0f;
            return Mathf.Clamp01(currentLove / maxLove);
        }
    }

    public void AddLove(float amount)
    {
        currentLove = Mathf.Clamp(currentLove + amount, 0f, maxLove);
    }

    public bool HasEnough(float amount)
    {
        return currentLove >= amount;
    }

    public bool TrySpendLove(float amount)
    {
        if (!HasEnough(amount))
            return false;

        currentLove -= amount;
        return true;
    }

    public void SetLove(float amount)
    {
        currentLove = Mathf.Clamp(amount, 0f, maxLove);
    }

    private void OnGUI()
    {
        float width = 260f;
        float height = 24f;
        float x = 20f;
        float y = 60f;

        GUI.Box(new Rect(x, y, width, height), "");
        GUI.Box(new Rect(x, y, width * CurrentNormalized, height), "");

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 18;
        style.normal.textColor = Color.white;

        GUI.Label(
            new Rect(x, y - 24f, 300f, 24f),
            $"Love: {currentLove:F0} / {maxLove:F0}",
            style
        );
    }
}