using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Fades Post Processing Vignette settings from Timeline Signal Receiver events.
/// </summary>
public class TimelineVignetteFade : MonoBehaviour
{
    [System.Serializable]
    public struct VignettePreset
    {
        public string name;
        [UnityEngine.Min(0f)] public float fadeDuration;
        public bool enabled;
        public VignetteMode mode;
        public Color color;
        [Range(0f, 1f)] public float intensity;
        [Range(0.01f, 1f)] public float smoothness;
        [Range(0f, 1f)] public float roundness;
        public Vector2 center;
        public bool rounded;
    }

    [Header("Target")]
    public PostProcessVolume targetVolume;
    public bool autoFindVolumeIfEmpty = true;

    [Header("Default Fade")]
    [UnityEngine.Min(0f)] public float fadeDuration = 1f;
    public bool targetEnabled = true;
    public VignetteMode targetMode = VignetteMode.Classic;
    public Color targetColor = Color.black;
    [Range(0f, 1f)] public float targetIntensity = 0.45f;
    [Range(0.01f, 1f)] public float targetSmoothness = 0.45f;
    [Range(0f, 1f)] public float targetRoundness = 1f;
    public Vector2 targetCenter = new Vector2(0.5f, 0.5f);
    public bool targetRounded;

    [Header("Preset List")]
    public VignettePreset[] presets;
    [UnityEngine.Min(0)] public int defaultPresetIndex;

    [Header("Playback")]
    public bool useUnscaledTime;
    public bool captureOriginalOnAwake = true;

    private Vignette vignette;
    private Coroutine fadeCoroutine;
    private VignettePreset originalPreset;
    private bool hasOriginalPreset;

    private void Reset()
    {
        ResolveVignette();
    }

    private void Awake()
    {
        ResolveVignette();

        if (captureOriginalOnAwake)
        {
            CaptureOriginal();
        }
    }

    public void FadeToConfigured()
    {
        FadeToPreset(new VignettePreset
        {
            name = "Configured",
            fadeDuration = fadeDuration,
            enabled = targetEnabled,
            mode = targetMode,
            color = targetColor,
            intensity = targetIntensity,
            smoothness = targetSmoothness,
            roundness = targetRoundness,
            center = targetCenter,
            rounded = targetRounded
        });
    }

    public void ApplyConfiguredImmediate()
    {
        ApplyPresetImmediate(new VignettePreset
        {
            name = "Configured",
            fadeDuration = 0f,
            enabled = targetEnabled,
            mode = targetMode,
            color = targetColor,
            intensity = targetIntensity,
            smoothness = targetSmoothness,
            roundness = targetRoundness,
            center = targetCenter,
            rounded = targetRounded
        });
    }

    public void FadeToDefaultPreset()
    {
        FadeToPresetByIndex(defaultPresetIndex);
    }

    public void ApplyDefaultPresetImmediate()
    {
        ApplyPresetByIndexImmediate(defaultPresetIndex);
    }

    public void FadeToPresetByIndex(int presetIndex)
    {
        if (presets == null || presetIndex < 0 || presetIndex >= presets.Length)
        {
            Debug.LogWarning($"[{nameof(TimelineVignetteFade)}] Preset index {presetIndex} is not valid on {name}.", this);
            return;
        }

        FadeToPreset(presets[presetIndex]);
    }

    public void ApplyPresetByIndexImmediate(int presetIndex)
    {
        if (presets == null || presetIndex < 0 || presetIndex >= presets.Length)
        {
            Debug.LogWarning($"[{nameof(TimelineVignetteFade)}] Preset index {presetIndex} is not valid on {name}.", this);
            return;
        }

        ApplyPresetImmediate(presets[presetIndex]);
    }

    public void FadeToPresetByName(string presetName)
    {
        int presetIndex = FindPresetIndex(presetName);
        if (presetIndex < 0)
        {
            Debug.LogWarning($"[{nameof(TimelineVignetteFade)}] Preset named '{presetName}' was not found on {name}.", this);
            return;
        }

        FadeToPreset(presets[presetIndex]);
    }

    public void ApplyPresetByNameImmediate(string presetName)
    {
        int presetIndex = FindPresetIndex(presetName);
        if (presetIndex < 0)
        {
            Debug.LogWarning($"[{nameof(TimelineVignetteFade)}] Preset named '{presetName}' was not found on {name}.", this);
            return;
        }

        ApplyPresetImmediate(presets[presetIndex]);
    }

    public void FadeToOriginal()
    {
        if (!hasOriginalPreset)
        {
            Debug.LogWarning($"[{nameof(TimelineVignetteFade)}] Original Vignette settings have not been captured on {name}.", this);
            return;
        }

        FadeToPreset(originalPreset);
    }

    public void ApplyOriginalImmediate()
    {
        if (!hasOriginalPreset)
        {
            Debug.LogWarning($"[{nameof(TimelineVignetteFade)}] Original Vignette settings have not been captured on {name}.", this);
            return;
        }

        ApplyPresetImmediate(originalPreset);
    }

    [ContextMenu("Capture Current Vignette As Original")]
    public void CaptureOriginal()
    {
        if (!ResolveVignette())
        {
            return;
        }

        originalPreset = CaptureCurrentPreset("Original", 0f);
        hasOriginalPreset = true;
    }

    [ContextMenu("Capture Current Vignette To Default Preset")]
    public void CaptureCurrentToDefaultPreset()
    {
        if (!ResolveVignette())
        {
            return;
        }

        if (presets == null || defaultPresetIndex < 0 || defaultPresetIndex >= presets.Length)
        {
            Debug.LogWarning($"[{nameof(TimelineVignetteFade)}] Default preset index {defaultPresetIndex} is not valid on {name}.", this);
            return;
        }

        VignettePreset preset = CaptureCurrentPreset(presets[defaultPresetIndex].name, presets[defaultPresetIndex].fadeDuration);
        presets[defaultPresetIndex] = preset;
    }

    [ContextMenu("Add Current Vignette As Preset")]
    public void AddCurrentVignetteAsPreset()
    {
        if (!ResolveVignette())
        {
            return;
        }

        int nextIndex = presets == null ? 0 : presets.Length;
        System.Array.Resize(ref presets, nextIndex + 1);
        presets[nextIndex] = CaptureCurrentPreset($"Preset {nextIndex}", fadeDuration);
        defaultPresetIndex = nextIndex;
    }

    public void StopFade()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
    }

    private void FadeToPreset(VignettePreset targetPreset)
    {
        if (!ResolveVignette())
        {
            return;
        }

        StopFade();

        if (!Application.isPlaying || targetPreset.fadeDuration <= 0f)
        {
            ApplyPresetImmediate(targetPreset);
            return;
        }

        fadeCoroutine = StartCoroutine(FadeRoutine(targetPreset));
    }

    private IEnumerator FadeRoutine(VignettePreset targetPreset)
    {
        VignettePreset startPreset = CaptureCurrentPreset("Start", 0f);
        VignettePreset visibleTargetPreset = targetPreset;
        float duration = Mathf.Max(0f, targetPreset.fadeDuration);
        float elapsed = 0f;

        if (!startPreset.enabled && targetPreset.enabled)
        {
            startPreset.intensity = 0f;
        }

        if (!targetPreset.enabled)
        {
            visibleTargetPreset.intensity = 0f;
        }

        EnsureVignetteOverrides();
        vignette.active = true;
        vignette.enabled.value = targetPreset.enabled || startPreset.enabled;

        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ApplyInterpolatedPreset(startPreset, visibleTargetPreset, t);
            yield return null;
        }

        ApplyPresetValues(targetPreset);
        fadeCoroutine = null;
    }

    private void ApplyPresetImmediate(VignettePreset preset)
    {
        if (!ResolveVignette())
        {
            return;
        }

        StopFade();
        ApplyPresetValues(preset);
    }

    private void ApplyPresetValues(VignettePreset preset)
    {
        EnsureVignetteOverrides();

        vignette.active = true;
        vignette.enabled.value = preset.enabled;
        vignette.mode.value = preset.mode;
        vignette.color.value = preset.color;
        vignette.intensity.value = preset.intensity;
        vignette.smoothness.value = preset.smoothness;
        vignette.roundness.value = preset.roundness;
        vignette.center.value = preset.center;
        vignette.rounded.value = preset.rounded;
    }

    private void ApplyInterpolatedPreset(VignettePreset startPreset, VignettePreset targetPreset, float t)
    {
        EnsureVignetteOverrides();

        vignette.enabled.value = targetPreset.enabled || startPreset.enabled;
        vignette.mode.value = targetPreset.mode;
        vignette.color.value = Color.Lerp(startPreset.color, targetPreset.color, t);
        vignette.intensity.value = Mathf.Lerp(startPreset.intensity, targetPreset.intensity, t);
        vignette.smoothness.value = Mathf.Lerp(startPreset.smoothness, targetPreset.smoothness, t);
        vignette.roundness.value = Mathf.Lerp(startPreset.roundness, targetPreset.roundness, t);
        vignette.center.value = Vector2.Lerp(startPreset.center, targetPreset.center, t);
        vignette.rounded.value = t < 1f ? startPreset.rounded : targetPreset.rounded;

        if (!targetPreset.enabled && t >= 1f)
        {
            vignette.enabled.value = false;
        }
    }

    private VignettePreset CaptureCurrentPreset(string presetName, float presetFadeDuration)
    {
        return new VignettePreset
        {
            name = presetName,
            fadeDuration = presetFadeDuration,
            enabled = vignette.enabled.value,
            mode = vignette.mode.value,
            color = vignette.color.value,
            intensity = vignette.intensity.value,
            smoothness = vignette.smoothness.value,
            roundness = vignette.roundness.value,
            center = vignette.center.value,
            rounded = vignette.rounded.value
        };
    }

    private int FindPresetIndex(string presetName)
    {
        if (presets == null)
        {
            return -1;
        }

        for (int i = 0; i < presets.Length; i++)
        {
            if (presets[i].name == presetName)
            {
                return i;
            }
        }

        return -1;
    }

    private bool ResolveVignette()
    {
        if (targetVolume == null && autoFindVolumeIfEmpty)
        {
            targetVolume = FindObjectOfType<PostProcessVolume>();
        }

        if (targetVolume == null)
        {
            Debug.LogWarning($"[{nameof(TimelineVignetteFade)}] Missing PostProcessVolume on {name}.", this);
            return false;
        }

        PostProcessProfile profile = targetVolume.profile;
        if (profile == null)
        {
            Debug.LogWarning($"[{nameof(TimelineVignetteFade)}] Missing PostProcessProfile on {targetVolume.name}.", targetVolume);
            return false;
        }

        if (vignette == null && !profile.TryGetSettings(out vignette))
        {
            vignette = profile.AddSettings<Vignette>();
        }

        return vignette != null;
    }

    private void EnsureVignetteOverrides()
    {
        vignette.enabled.overrideState = true;
        vignette.mode.overrideState = true;
        vignette.color.overrideState = true;
        vignette.intensity.overrideState = true;
        vignette.smoothness.overrideState = true;
        vignette.roundness.overrideState = true;
        vignette.center.overrideState = true;
        vignette.rounded.overrideState = true;
    }
}
