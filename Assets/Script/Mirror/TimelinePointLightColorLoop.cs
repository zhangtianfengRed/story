using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

/// <summary>
/// Drives a point light through a looping list of colors.
/// Expose the public methods to Timeline Signal Receiver events.
/// </summary>
[ExecuteAlways]
public class TimelinePointLightColorLoop : MonoBehaviour
{
    [Header("Target")]
    public Light targetLight;
    public bool autoFindPointLight = true;

    [Header("Playback")]
    public bool playOnEnable;
    [Min(0.01f)] public float secondsPerColor = 0.5f;
    [Min(0f)] public float startBlendDuration = 0.25f;
    public int startColorIndex;
    public bool useUnscaledTime;
    public bool restoreInitialColorOnStop = true;

    [Header("Colors")]
    public Color[] colors =
    {
        new Color(1f, 0.45f, 0.1f),
        new Color(1f, 0.9f, 0.2f),
        new Color(0.4f, 0.85f, 1f),
        new Color(0.8f, 0.3f, 1f)
    };

    private bool isLooping;
    private bool hasInitialColor;
    private bool hasWarnedAboutLightType;
    private Color initialColor;
    private int currentColorIndex;
    private float colorTimer;
    private bool isStartingBlend;
    private Color startBlendFromColor;
    private float startBlendTimer;
#if UNITY_EDITOR
    private double editorLastUpdateTime;
#endif

    private void Reset()
    {
        ResolveTargetLight();
    }

    private void Awake()
    {
        ResolveTargetLight();
        CacheInitialColor();
        ResetLoopState();
    }

    private void OnEnable()
    {
        ResolveTargetLight();
        CacheInitialColor();
        RegisterEditorUpdate();

        if (playOnEnable)
        {
            RestartLoop();
        }
    }

    private void OnDisable()
    {
        UnregisterEditorUpdate();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        TickLoop(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
    }

    private void TickLoop(float deltaTime)
    {
        if (!isLooping || !HasUsableSetup())
        {
            return;
        }

        if (TickStartBlend(deltaTime))
        {
            return;
        }

        if (colors.Length == 1)
        {
            targetLight.color = colors[0];
            return;
        }

        float duration = Mathf.Max(0.01f, secondsPerColor);
        colorTimer += Mathf.Max(0f, deltaTime);

        while (colorTimer >= duration)
        {
            colorTimer -= duration;
            currentColorIndex = GetNextColorIndex(currentColorIndex);
        }

        int nextColorIndex = GetNextColorIndex(currentColorIndex);
        float t = colorTimer / duration;
        targetLight.color = Color.Lerp(colors[currentColorIndex], colors[nextColorIndex], t);
    }

    public void StartLoop()
    {
        if (!HasUsableSetup())
        {
            return;
        }

        isLooping = true;
        BeginStartBlend();
    }

    public void StopLoop()
    {
        isLooping = false;
        isStartingBlend = false;
        colorTimer = 0f;
        startBlendTimer = 0f;

        if (restoreInitialColorOnStop && targetLight != null && hasInitialColor)
        {
            targetLight.color = initialColor;
        }
    }

    public void PauseLoop()
    {
        isLooping = false;
    }

    public void ResumeLoop()
    {
        if (!HasUsableSetup())
        {
            return;
        }

        isLooping = true;
    }

    public void RestartLoop()
    {
        if (!HasUsableSetup())
        {
            return;
        }

        ResetLoopState();
        isLooping = true;
        BeginStartBlend();
    }

    public void ApplyStartColor()
    {
        if (!HasUsableSetup())
        {
            return;
        }

        ResetLoopState();
        ApplyCurrentColorImmediate();
    }

    private void ResolveTargetLight()
    {
        if (targetLight != null || !autoFindPointLight)
        {
            return;
        }

        Light ownLight = GetComponent<Light>();
        if (ownLight != null && ownLight.type == LightType.Point)
        {
            targetLight = ownLight;
            return;
        }

        Light[] lights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Point)
            {
                targetLight = lights[i];
                return;
            }
        }

        if (ownLight != null)
        {
            targetLight = ownLight;
        }
    }

    private void CacheInitialColor()
    {
        if (targetLight == null || hasInitialColor)
        {
            return;
        }

        initialColor = targetLight.color;
        hasInitialColor = true;
    }

    private bool HasUsableSetup()
    {
        ResolveTargetLight();
        CacheInitialColor();

        if (targetLight == null)
        {
            Debug.LogWarning($"[{nameof(TimelinePointLightColorLoop)}] Missing target Light on {name}.", this);
            return false;
        }

        if (targetLight.type != LightType.Point && !hasWarnedAboutLightType)
        {
            Debug.LogWarning(
                $"[{nameof(TimelinePointLightColorLoop)}] {targetLight.name} is {targetLight.type}, not Point. The script will still run.",
                this
            );
            hasWarnedAboutLightType = true;
        }

        if (colors == null || colors.Length == 0)
        {
            Debug.LogWarning($"[{nameof(TimelinePointLightColorLoop)}] No colors configured on {name}.", this);
            return false;
        }

        return true;
    }

    private void ResetLoopState()
    {
        currentColorIndex = GetClampedStartIndex();
        colorTimer = 0f;
        isStartingBlend = false;
        startBlendTimer = 0f;
    }

    private void ApplyCurrentColorImmediate()
    {
        if (targetLight == null || colors == null || colors.Length == 0)
        {
            return;
        }

        currentColorIndex = Mathf.Clamp(currentColorIndex, 0, colors.Length - 1);
        targetLight.color = colors[currentColorIndex];
    }

    private void BeginStartBlend()
    {
        if (targetLight == null || colors == null || colors.Length == 0)
        {
            return;
        }

        currentColorIndex = Mathf.Clamp(currentColorIndex, 0, colors.Length - 1);
        startBlendFromColor = targetLight.color;
        startBlendTimer = 0f;
        colorTimer = 0f;

        if (GetStartBlendDuration() <= 0f || startBlendFromColor == colors[currentColorIndex])
        {
            isStartingBlend = false;
            ApplyCurrentColorImmediate();
            return;
        }

        isStartingBlend = true;
    }

    private bool TickStartBlend(float deltaTime)
    {
        if (!isStartingBlend || targetLight == null || colors == null || colors.Length == 0)
        {
            return false;
        }

        float duration = GetStartBlendDuration();
        if (duration <= 0f)
        {
            isStartingBlend = false;
            ApplyCurrentColorImmediate();
            return false;
        }

        startBlendTimer += Mathf.Max(0f, deltaTime);
        float t = Mathf.Clamp01(startBlendTimer / duration);
        targetLight.color = Color.Lerp(startBlendFromColor, colors[currentColorIndex], t);

        if (t < 1f)
        {
            return true;
        }

        isStartingBlend = false;
        startBlendTimer = 0f;
        colorTimer = 0f;
        return colors.Length > 1;
    }

    private float GetStartBlendDuration()
    {
        if (startBlendDuration > 0f)
        {
            return startBlendDuration;
        }

        return Mathf.Max(0.01f, secondsPerColor);
    }

    private int GetClampedStartIndex()
    {
        if (colors == null || colors.Length == 0)
        {
            return 0;
        }

        return Mathf.Clamp(startColorIndex, 0, colors.Length - 1);
    }

    private int GetNextColorIndex(int index)
    {
        if (colors == null || colors.Length == 0)
        {
            return 0;
        }

        return (index + 1) % colors.Length;
    }

#if UNITY_EDITOR
    private void RegisterEditorUpdate()
    {
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
        editorLastUpdateTime = EditorApplication.timeSinceStartup;
    }

    private void UnregisterEditorUpdate()
    {
        EditorApplication.update -= EditorTick;
    }

    private void EditorTick()
    {
        if (Application.isPlaying || this == null || !isActiveAndEnabled)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Max(0f, (float)(now - editorLastUpdateTime));
        editorLastUpdateTime = now;

        TickLoop(deltaTime);

        if (isLooping)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            InternalEditorUtility.RepaintAllViews();
        }
    }
#endif
}
