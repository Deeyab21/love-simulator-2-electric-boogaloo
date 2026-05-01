using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ImpactFlashUI : MonoBehaviour
{
    public static ImpactFlashUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("A full-screen UI Image used for the flash.")]
    public Image flashImage;

    [Header("Default Flash")]
    public Color defaultFlashColor = Color.white;
    [Range(0f, 1f)] public float defaultMaxAlpha = 0.85f;
    public float defaultFadeOutTime = 0.08f;

    private Coroutine flashRoutine;

    private void Awake()
    {
        Instance = this;
        ForceHidden();
    }

    private void OnEnable()
    {
        Instance = this;
        ForceHidden();
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ForceHidden()
    {
        if (flashImage == null)
            return;

        flashImage.raycastTarget = false;

        Color c = flashImage.color;
        c.a = 0f;
        flashImage.color = c;

        // Important: keep the script object active, but hide only the Image object.
        flashImage.gameObject.SetActive(false);
    }

    public static void Play(Color color, float maxAlpha, float fadeOutTime)
    {
        if (Instance == null)
            return;

        Instance.PlayFlash(color, maxAlpha, fadeOutTime);
    }

    public static void PlayDefault()
    {
        if (Instance == null)
            return;

        Instance.PlayFlash(
            Instance.defaultFlashColor,
            Instance.defaultMaxAlpha,
            Instance.defaultFadeOutTime
        );
    }

    public void PlayFlash(Color color, float maxAlpha, float fadeOutTime)
    {
        if (flashImage == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine(color, maxAlpha, fadeOutTime));
    }

    private IEnumerator FlashRoutine(Color color, float maxAlpha, float fadeOutTime)
    {
        maxAlpha = Mathf.Clamp01(maxAlpha);
        fadeOutTime = Mathf.Max(0.001f, fadeOutTime);

        flashImage.gameObject.SetActive(true);

        color.a = maxAlpha;
        flashImage.color = color;

        float timer = 0f;

        while (timer < fadeOutTime)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / fadeOutTime);

            Color c = color;
            c.a = Mathf.Lerp(maxAlpha, 0f, t);
            flashImage.color = c;

            yield return null;
        }

        color.a = 0f;
        flashImage.color = color;

        flashImage.gameObject.SetActive(false);
        flashRoutine = null;
    }
}