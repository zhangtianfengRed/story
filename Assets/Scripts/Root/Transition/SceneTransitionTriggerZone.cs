using UnityEngine;

/// <summary>
/// 玩家进入触发区域后，通过全局 SceneTransitionController 切换场景。
/// 淡出、加载和淡入流程全部由现有控制器负责。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
[AddComponentMenu("Story/Scene Transition Trigger Zone")]
public sealed class SceneTransitionTriggerZone : MonoBehaviour
{
    [Header("Target Scene")]
    [SerializeField] private string targetSceneName;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Trigger")]
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool disableColliderAfterTransitionStarts = true;
    [SerializeField] private BoxCollider triggerCollider;

    private bool hasTriggered;

    private void Reset()
    {
        EnsureTriggerCollider();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        TransitionToTargetScene();
    }

    public void TransitionToTargetScene()
    {
        if (triggerOnce && hasTriggered)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning($"[{nameof(SceneTransitionTriggerZone)}] Target scene name is empty.", this);
            return;
        }

        SceneTransitionController transitionController = SceneTransitionController.Instance;
        if (transitionController.IsTransitioning)
        {
            return;
        }

        hasTriggered = true;

        if (disableColliderAfterTransitionStarts && triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        transitionController.LoadScene(targetSceneName);
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
        EnsureTriggerCollider();

        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (HasPlayerTagInParents(other.transform))
        {
            return true;
        }

        return other.GetComponentInParent<CityWalkCharacterMovement>() != null ||
               other.GetComponentInParent<RoomPlayerInteractor>() != null;
    }

    private bool HasPlayerTagInParents(Transform target)
    {
        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return false;
        }

        Transform current = target;
        while (current != null)
        {
            if (current.CompareTag(playerTag))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void EnsureTriggerCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<BoxCollider>();
        }
    }
}
