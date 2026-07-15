using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class CityWalkAmbientZone : MonoBehaviour
{
    [Header("Player Detection")]
    [SerializeField]
    private string playerTag = "Player";

    [Header("Audio Sources")]
    [SerializeField]
    private AudioSource loopSource;

    [SerializeField]
    private AudioSource oneShotSource;

    [Header("Loop Ambience")]
    [SerializeField]
    private AudioClip loopClip;

    [SerializeField, Range(0f, 1f)]
    private float loopBaseVolume = 0.35f;

    [SerializeField, Min(0f)]
    private float fadeDuration = 1.5f;

    [Header("Random One Shots")]
    [SerializeField]
    private AudioClip[] randomClips;

    [SerializeField]
    private Vector2 randomInterval = new Vector2(6f, 15f);

    [SerializeField]
    private Vector2 randomVolume = new Vector2(0.55f, 0.85f);

    [SerializeField]
    private Vector2 randomPitch = new Vector2(0.95f, 1.05f);

    [SerializeField]
    private bool playRandomImmediately;

    [SerializeField]
    private bool allowOneShotTailOnExit = true;

    private int playerOverlapCount;
    private float currentFade;
    private float targetFade;
    private float nextRandomPlayTime = float.PositiveInfinity;

    private bool IsPlayerInside => playerOverlapCount > 0;

    private void Reset()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;

        AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
        if (sources.Length > 0)
        {
            loopSource = sources[0];
        }

        if (sources.Length > 1)
        {
            oneShotSource = sources[1];
        }
    }

    private void Awake()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;

        ConfigureSources();
        ApplyVolumes();
    }

    private void Update()
    {
        float fadeStep = fadeDuration <= 0f
            ? 1f
            : Time.deltaTime / fadeDuration;

        currentFade = Mathf.MoveTowards(currentFade, targetFade, fadeStep);
        ApplyVolumes();

        if (!IsPlayerInside)
        {
            StopLoopWhenFadedOut();
            return;
        }

        EnsureLoopIsPlaying();
        TryPlayRandomOneShot();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        playerOverlapCount++;
        if (playerOverlapCount == 1)
        {
            ActivateZone();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        playerOverlapCount = Mathf.Max(0, playerOverlapCount - 1);
        if (playerOverlapCount == 0)
        {
            DeactivateZone();
        }
    }

    private void OnDisable()
    {
        playerOverlapCount = 0;
        currentFade = 0f;
        targetFade = 0f;
        nextRandomPlayTime = float.PositiveInfinity;

        if (loopSource != null)
        {
            loopSource.Stop();
            loopSource.volume = 0f;
        }

        if (oneShotSource != null)
        {
            oneShotSource.Stop();
        }
    }

    private void ActivateZone()
    {
        targetFade = 1f;
        EnsureLoopIsPlaying();

        nextRandomPlayTime = playRandomImmediately
            ? Time.time
            : Time.time + GetRandomValue(randomInterval);
    }

    private void DeactivateZone()
    {
        targetFade = 0f;
        nextRandomPlayTime = float.PositiveInfinity;

        if (!allowOneShotTailOnExit && oneShotSource != null)
        {
            oneShotSource.Stop();
        }
    }

    private void ConfigureSources()
    {
        if (loopSource != null)
        {
            if (loopClip == null)
            {
                loopClip = loopSource.clip;
            }

            loopSource.clip = loopClip;
            loopSource.playOnAwake = false;
            loopSource.loop = true;
            loopSource.volume = 0f;
        }

        if (oneShotSource != null)
        {
            oneShotSource.playOnAwake = false;
            oneShotSource.loop = false;
        }
    }

    private void EnsureLoopIsPlaying()
    {
        if (loopSource == null || loopClip == null || loopSource.isPlaying)
        {
            return;
        }

        loopSource.clip = loopClip;
        loopSource.Play();
    }

    private void StopLoopWhenFadedOut()
    {
        if (currentFade > 0f || loopSource == null || !loopSource.isPlaying)
        {
            return;
        }

        loopSource.Stop();
    }

    private void TryPlayRandomOneShot()
    {
        if (oneShotSource == null || Time.time < nextRandomPlayTime)
        {
            return;
        }

        AudioClip clip = GetRandomClip();
        if (clip != null)
        {
            oneShotSource.pitch = GetRandomValue(randomPitch);
            oneShotSource.PlayOneShot(clip, GetRandomValue(randomVolume));
        }

        nextRandomPlayTime = Time.time + GetRandomValue(randomInterval);
    }

    private AudioClip GetRandomClip()
    {
        if (randomClips == null || randomClips.Length == 0)
        {
            return null;
        }

        int startIndex = Random.Range(0, randomClips.Length);
        for (int i = 0; i < randomClips.Length; i++)
        {
            AudioClip clip = randomClips[(startIndex + i) % randomClips.Length];
            if (clip != null)
            {
                return clip;
            }
        }

        return null;
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(playerTag) && other.CompareTag(playerTag))
        {
            return true;
        }

        return other.GetComponentInParent<CityWalkCharacterMovement>() != null;
    }

    private void ApplyVolumes()
    {
        float sfxVolume = GameSettingsManager.GetChannelVolume(SettingsAudioChannel.Sfx);

        if (loopSource != null)
        {
            loopSource.volume = Mathf.Clamp01(loopBaseVolume * currentFade * sfxVolume);
        }

        if (oneShotSource != null)
        {
            oneShotSource.volume = Mathf.Clamp01(sfxVolume);
        }
    }

    private static float GetRandomValue(Vector2 range)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        return Random.Range(min, max);
    }

    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);
        loopBaseVolume = Mathf.Clamp01(loopBaseVolume);
        randomInterval = OrderRange(randomInterval, 0.1f, float.MaxValue);
        randomVolume = OrderRange(randomVolume, 0f, 1f);
        randomPitch = OrderRange(randomPitch, 0.01f, 3f);

        BoxCollider trigger = GetComponent<BoxCollider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    private static Vector2 OrderRange(Vector2 range, float minimum, float maximum)
    {
        float min = Mathf.Clamp(Mathf.Min(range.x, range.y), minimum, maximum);
        float max = Mathf.Clamp(Mathf.Max(range.x, range.y), min, maximum);
        return new Vector2(min, max);
    }
}
