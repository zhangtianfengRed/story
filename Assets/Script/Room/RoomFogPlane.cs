using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class RoomFogPlane : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
    private static readonly int AccentStrengthId = Shader.PropertyToID("_AccentStrength");
    private static readonly int BaseAlphaId = Shader.PropertyToID("_BaseAlpha");
    private static readonly int FogDensityId = Shader.PropertyToID("_FogDensity");
    private static readonly int AlphaMultiplierId = Shader.PropertyToID("_AlphaMultiplier");
    private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
    private static readonly int NoiseTilingId = Shader.PropertyToID("_NoiseTiling");
    private static readonly int NoiseScrollId = Shader.PropertyToID("_NoiseScroll");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int FloatAmplitudeId = Shader.PropertyToID("_FloatAmplitude");
    private static readonly int FloatFrequencyId = Shader.PropertyToID("_FloatFrequency");
    private static readonly int FloatSpeedId = Shader.PropertyToID("_FloatSpeed");
    private static readonly int FlowStrengthId = Shader.PropertyToID("_FlowStrength");
    private static readonly int FlowEnabledId = Shader.PropertyToID("_FlowEnabled");

    [Header("References")]
    public Renderer fogRenderer;
    public MeshFilter fogMeshFilter;
    public Transform player;
    public string playerTag = "Player";

    [Header("Visual")]
    public Color fogColor = new Color(0.86f, 0.86f, 0.9f, 1f);
    public Color accentColor = new Color(0.76f, 0.71f, 0.88f, 1f);
    [Range(0f, 1f)]
    public float accentStrength = 0.32f;
    [Range(0f, 1f)]
    public float baseAlpha = 0.35f;
    [Range(0f, 4f)]
    public float fogDensity = 1f;
    [Range(0f, 1f)]
    public float noiseStrength = 0.25f;
    [Min(0.01f)]
    public float noiseTiling = 1.6f;
    public Vector2 noiseScroll = new Vector2(0.02f, 0.01f);
    [Range(0.001f, 0.49f)]
    public float edgeSoftness = 0.18f;

    [Header("Planar Flow")]
    public bool enablePlanarFlow = true;
    [FormerlySerializedAs("floatAmplitude")]
    [Range(0f, 0.15f)]
    public float planarWarpAmount = 0.04f;
    [FormerlySerializedAs("floatFrequency")]
    [Min(0f)]
    public float planarFlowScale = 1f;
    [FormerlySerializedAs("floatSpeed")]
    [Min(0f)]
    public float planarFlowSpeed = 0.55f;
    [Range(0f, 1f)]
    public float flowStrength = 0.35f;

    [Header("Fade")]
    public bool autoFadeOnPlayerEnter = true;
    public bool restoreOnPlayerExit = true;
    public bool syncInitialStateToPlayerPosition = true;
    [Range(0f, 0.45f)]
    public float entryInset = 0.08f;
    public Vector2 fadeOutDurationRange = new Vector2(0.35f, 0.5f);
    public Vector2 fadeInDurationRange = new Vector2(0.35f, 0.5f);
    public bool hideRendererAfterFade = true;
    public bool disableGameObjectAfterFade = false;

    private MaterialPropertyBlock propertyBlock;
    private Bounds localFogBounds;
    private int planeAxisA;
    private int planeAxisB;
    private bool hasFogBounds;
    private bool hasPresenceState;
    private bool isPlayerInside;
    private float currentAlphaMultiplier = 1f;
    private float targetAlphaMultiplier = 1f;
    private Coroutine fadeCoroutine;

    private void Reset()
    {
        CacheReferences();
        CacheFogBounds();
        currentAlphaMultiplier = 1f;
        targetAlphaMultiplier = 1f;
    }

    private void Awake()
    {
        CacheReferences();
        CacheFogBounds();
        ApplyVisualProperties();
    }

    private void Start()
    {
        ResolvePlayerIfNeeded();
        SyncFogStateWithPlayer(syncImmediately: syncInitialStateToPlayerPosition);
    }

    private void OnEnable()
    {
        CacheReferences();
        CacheFogBounds();

        if (fogRenderer != null)
        {
            fogRenderer.enabled = currentAlphaMultiplier > 0f || targetAlphaMultiplier > 0f;
        }

        ApplyVisualProperties();
    }

    private void OnValidate()
    {
        CacheReferences();
        CacheFogBounds();

        accentStrength = Mathf.Clamp01(accentStrength);
        baseAlpha = Mathf.Clamp01(baseAlpha);
        fogDensity = Mathf.Clamp(fogDensity, 0f, 4f);
        noiseStrength = Mathf.Clamp01(noiseStrength);
        noiseTiling = Mathf.Max(0.01f, noiseTiling);
        edgeSoftness = Mathf.Clamp(edgeSoftness, 0.001f, 0.49f);
        planarWarpAmount = Mathf.Clamp(planarWarpAmount, 0f, 0.15f);
        planarFlowScale = Mathf.Max(0.01f, planarFlowScale);
        planarFlowSpeed = Mathf.Max(0f, planarFlowSpeed);
        flowStrength = Mathf.Clamp01(flowStrength);
        entryInset = Mathf.Clamp(entryInset, 0f, 0.45f);
        fadeOutDurationRange.x = Mathf.Max(0f, fadeOutDurationRange.x);
        fadeOutDurationRange.y = Mathf.Max(fadeOutDurationRange.x, fadeOutDurationRange.y);
        fadeInDurationRange.x = Mathf.Max(0f, fadeInDurationRange.x);
        fadeInDurationRange.y = Mathf.Max(fadeInDurationRange.x, fadeInDurationRange.y);

        if (!Application.isPlaying)
        {
            currentAlphaMultiplier = 1f;
            targetAlphaMultiplier = 1f;
        }

        ApplyVisualProperties();
    }

    private void Update()
    {
        if (!Application.isPlaying || !autoFadeOnPlayerEnter)
        {
            return;
        }

        SyncFogStateWithPlayer(syncImmediately: false);
    }

    public void TriggerFadeOut()
    {
        FadeToVisibility(false, fadeOutDurationRange);
    }

    public void TriggerFadeIn()
    {
        FadeToVisibility(true, fadeInDurationRange);
    }

    public void ResetFog()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        hasPresenceState = false;
        ResolvePlayerIfNeeded();
        if (autoFadeOnPlayerEnter && syncInitialStateToPlayerPosition)
        {
            SyncFogStateWithPlayer(syncImmediately: true);
        }
        else
        {
            SetVisibilityImmediate(true);
        }
    }

    private void SyncFogStateWithPlayer(bool syncImmediately)
    {
        ResolvePlayerIfNeeded();
        if (player == null || !hasFogBounds)
        {
            return;
        }

        bool playerInside = IsWorldPointInsideFogArea(player.position);

        if (!hasPresenceState)
        {
            hasPresenceState = true;
            isPlayerInside = playerInside;

            if (syncImmediately)
            {
                SetVisibilityImmediate(!playerInside);
            }

            return;
        }

        if (playerInside == isPlayerInside)
        {
            return;
        }

        isPlayerInside = playerInside;

        if (playerInside)
        {
            if (syncImmediately)
            {
                SetVisibilityImmediate(false);
            }
            else
            {
                TriggerFadeOut();
            }
        }
        else if (restoreOnPlayerExit)
        {
            if (syncImmediately)
            {
                SetVisibilityImmediate(true);
            }
            else
            {
                TriggerFadeIn();
            }
        }
    }

    private void FadeToVisibility(bool visible, Vector2 durationRange)
    {
        targetAlphaMultiplier = visible ? 1f : 0f;

        if (visible && fogRenderer != null)
        {
            fogRenderer.enabled = true;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        float minDuration = Mathf.Max(0f, durationRange.x);
        float maxDuration = Mathf.Max(minDuration, durationRange.y);
        float duration = Random.Range(minDuration, maxDuration);

        if (duration <= 0f)
        {
            SetVisibilityImmediate(visible);
            return;
        }

        fadeCoroutine = StartCoroutine(FadeRoutine(duration, targetAlphaMultiplier));
    }

    private void SetVisibilityImmediate(bool visible)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        targetAlphaMultiplier = visible ? 1f : 0f;
        SetAlphaMultiplier(targetAlphaMultiplier);
        FinalizeVisibilityState();
    }

    private IEnumerator FadeRoutine(float duration, float targetAlpha)
    {
        float startAlpha = currentAlphaMultiplier;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlphaMultiplier(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetAlphaMultiplier(targetAlpha);
        FinalizeVisibilityState();
        fadeCoroutine = null;
    }

    private void FinalizeVisibilityState()
    {
        bool isVisible = targetAlphaMultiplier > 0.0001f;

        if (!isVisible && disableGameObjectAfterFade && !restoreOnPlayerExit)
        {
            gameObject.SetActive(false);
            return;
        }

        if (fogRenderer != null)
        {
            fogRenderer.enabled = isVisible || !hideRendererAfterFade;
        }
    }

    private void SetAlphaMultiplier(float alphaMultiplier)
    {
        currentAlphaMultiplier = Mathf.Clamp01(alphaMultiplier);
        ApplyVisualProperties();
    }

    private void ApplyVisualProperties()
    {
        if (fogRenderer == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        fogRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(ColorId, fogColor);
        propertyBlock.SetColor(AccentColorId, accentColor);
        propertyBlock.SetFloat(AccentStrengthId, accentStrength);
        propertyBlock.SetFloat(BaseAlphaId, baseAlpha);
        propertyBlock.SetFloat(FogDensityId, fogDensity);
        propertyBlock.SetFloat(AlphaMultiplierId, currentAlphaMultiplier);
        propertyBlock.SetFloat(NoiseStrengthId, noiseStrength);
        propertyBlock.SetFloat(NoiseTilingId, noiseTiling);
        propertyBlock.SetVector(NoiseScrollId, new Vector4(noiseScroll.x, noiseScroll.y, 0f, 0f));
        propertyBlock.SetFloat(EdgeSoftnessId, edgeSoftness);
        propertyBlock.SetFloat(FloatAmplitudeId, planarWarpAmount);
        propertyBlock.SetFloat(FloatFrequencyId, planarFlowScale);
        propertyBlock.SetFloat(FloatSpeedId, planarFlowSpeed);
        propertyBlock.SetFloat(FlowStrengthId, flowStrength);
        propertyBlock.SetFloat(FlowEnabledId, enablePlanarFlow ? 1f : 0f);
        fogRenderer.SetPropertyBlock(propertyBlock);
    }

    private void CacheReferences()
    {
        if (fogRenderer == null)
        {
            fogRenderer = GetComponent<Renderer>();
        }

        if (fogMeshFilter == null)
        {
            fogMeshFilter = GetComponent<MeshFilter>();
        }
    }

    private void CacheFogBounds()
    {
        hasFogBounds = false;

        if (fogMeshFilter == null || fogMeshFilter.sharedMesh == null)
        {
            return;
        }

        localFogBounds = fogMeshFilter.sharedMesh.bounds;
        Vector3 size = localFogBounds.size;

        int normalAxis = 2;
        if (size.x <= size.y && size.x <= size.z)
        {
            normalAxis = 0;
        }
        else if (size.y <= size.x && size.y <= size.z)
        {
            normalAxis = 1;
        }

        switch (normalAxis)
        {
            case 0:
                planeAxisA = 1;
                planeAxisB = 2;
                break;
            case 1:
                planeAxisA = 0;
                planeAxisB = 2;
                break;
            default:
                planeAxisA = 0;
                planeAxisB = 1;
                break;
        }

        hasFogBounds = GetAxis(localFogBounds.extents, planeAxisA) > 0.0001f
            && GetAxis(localFogBounds.extents, planeAxisB) > 0.0001f;
    }

    private void ResolvePlayerIfNeeded()
    {
        if (player != null || string.IsNullOrEmpty(playerTag))
        {
            return;
        }

        try
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }
        catch (UnityException)
        {
        }
    }

    private bool IsWorldPointInsideFogArea(Vector3 worldPoint)
    {
        if (!hasFogBounds || fogMeshFilter == null)
        {
            return false;
        }

        Vector3 localPoint = fogMeshFilter.transform.InverseTransformPoint(worldPoint);
        float insetFactor = 1f - entryInset;

        float halfExtentA = GetAxis(localFogBounds.extents, planeAxisA) * insetFactor;
        float halfExtentB = GetAxis(localFogBounds.extents, planeAxisB) * insetFactor;

        float pointA = Mathf.Abs(GetAxis(localPoint, planeAxisA));
        float pointB = Mathf.Abs(GetAxis(localPoint, planeAxisB));

        return pointA <= halfExtentA && pointB <= halfExtentB;
    }

    private static float GetAxis(Vector3 value, int axis)
    {
        switch (axis)
        {
            case 0:
                return value.x;
            case 1:
                return value.y;
            default:
                return value.z;
        }
    }
}
