using System.Collections;
using UnityEngine;

/// <summary>
/// Controls a fullscreen black overlay that can be faded away from Timeline signals.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class TimelineBlackFadeOverlay : MonoBehaviour
{
    [Header("Target")]
    public CanvasGroup targetCanvasGroup;

    [Header("Playback")]
    [Min(0f)] public float fadeDuration = 1.5f;
    public bool startBlack = true;
    public bool useUnscaledTime = true;
    public bool blockRaycastsWhileVisible = true;

    private Coroutine fadeCoroutine;

    private void Reset()
    {
        ResolveCanvasGroup();
    }

    private void Awake()
    {
        ResolveCanvasGroup();

        if (startBlack)
        {
            ApplyAlpha(1f);
        }
        else
        {
            ApplyAlpha(0f);
        }
    }

    private void OnDisable()
    {
        StopFade();
    }

    public void FadeFromBlack()
    {
        FadeTo(0f);
    }

    public void FadeToBlack()
    {
        FadeTo(1f);
    }

    public void RestartFromBlack()
    {
        SetBlackImmediate();
        FadeFromBlack();
    }

    public void SetBlackImmediate()
    {
        StopFade();
        ApplyAlpha(1f);
    }

    public void SetTransparentImmediate()
    {
        StopFade();
        ApplyAlpha(0f);
    }

    private void FadeTo(float targetAlpha)
    {
        ResolveCanvasGroup();
        if (targetCanvasGroup == null)
        {
            Debug.LogWarning($"[{nameof(TimelineBlackFadeOverlay)}] Missing CanvasGroup on {name}.", this);
            return;
        }

        StopFade();

        if (!Application.isPlaying || fadeDuration <= 0f)
        {
            ApplyAlpha(targetAlpha);
            return;
        }

        fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        float startAlpha = targetCanvasGroup.alpha;
        float duration = Mathf.Max(0f, fadeDuration);

        if (Mathf.Approximately(startAlpha, targetAlpha) || duration <= 0f)
        {
            ApplyAlpha(targetAlpha);
            fadeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ApplyAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        ApplyAlpha(targetAlpha);
        fadeCoroutine = null;
    }

    private void ResolveCanvasGroup()
    {
        if (targetCanvasGroup == null)
        {
            targetCanvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void ApplyAlpha(float alpha)
    {
        ResolveCanvasGroup();
        if (targetCanvasGroup == null)
        {
            return;
        }

        targetCanvasGroup.alpha = alpha;
        targetCanvasGroup.interactable = false;
        targetCanvasGroup.blocksRaycasts = blockRaycastsWhileVisible && alpha > 0.001f;
    }

    private void StopFade()
    {
        if (fadeCoroutine == null)
        {
            return;
        }

        StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
    }
}
