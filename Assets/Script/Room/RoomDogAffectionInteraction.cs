using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(RoomInteractable))]
[RequireComponent(typeof(Animator))]
public sealed class RoomDogAffectionInteraction : RoomInteractionBehaviour
{
    [Header("Interaction")]
    [SerializeField] private RoomInteractable roomInteractable;

    [Header("Cameras")]
    [SerializeField] private CinemachineVirtualCameraBase dogCamera;
    [SerializeField] private CinemachineVirtualCameraBase followCamera;
    [SerializeField] private int dogCameraPriority = 10;

    [Header("Dog Animation")]
    [SerializeField] private Animator dogAnimator;
    [SerializeField] private int animatorLayer;
    [SerializeField] private string sitParameterName = "Sit_b";
    [SerializeField] private string actionParameterName = "ActionType_int";
    [SerializeField] private int affectionActionValue = 2;
    [SerializeField] private string affectionStateName = "2_Sitting_Beg";

    [Header("Animation Safety")]
    [Min(0.1f)]
    [SerializeField] private float animationEnterTimeout = 3f;

    [Min(0.1f)]
    [SerializeField] private float animationFinishTimeout = 10f;

    [Header("Dialogue Synchronization")]
    [Tooltip("开启后，狗动画结束时会等待当前语音自然播放完毕，再恢复相机并完成互动。")]
    [SerializeField] private bool waitForDialoguePlaybackBeforeCompleting = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onInteractionStarted = new UnityEvent();
    [SerializeField] private UnityEvent onInteractionCompleted = new UnityEvent();

    private Coroutine interactionRoutine;
    private int originalDogCameraPriority;
    private int originalFollowCameraPriority;
    private bool cameraPrioritiesCaptured;
    private bool interactionStarted;

    private void Reset()
    {
        ResolveLocalReferences();
    }

    private void Awake()
    {
        ResolveLocalReferences();
    }

    public override void Execute(RoomInteractionContext context)
    {
        if (interactionStarted)
        {
            return;
        }

        if (context != null && context.Interactable != null)
        {
            roomInteractable = context.Interactable;
        }

        ResolveLocalReferences();

        if (!ValidateConfiguration())
        {
            return;
        }

        interactionStarted = true;
        roomInteractable.SetInteractable(false);
        FocusDogCamera();

        dogAnimator.SetBool(Animator.StringToHash(sitParameterName), true);
        dogAnimator.SetInteger(Animator.StringToHash(actionParameterName), affectionActionValue);

        onInteractionStarted.Invoke();
        interactionRoutine = StartCoroutine(WaitForAffectionAnimation());
    }

    private IEnumerator WaitForAffectionAnimation()
    {
        int affectionStateHash = Animator.StringToHash(affectionStateName);
        float enterDeadline = Time.time + Mathf.Max(0.1f, animationEnterTimeout);

        while (!IsAnimatorInState(affectionStateHash) && Time.time < enterDeadline)
        {
            yield return null;
        }

        if (!IsAnimatorInState(affectionStateHash))
        {
            AbortInteraction($"Animator did not enter state '{affectionStateName}'.");
            yield break;
        }

        // The action state has an exit-time transition. Resetting the selector here
        // prevents it from immediately starting the same action again after it exits.
        dogAnimator.SetInteger(Animator.StringToHash(actionParameterName), 0);

        float finishDeadline = Time.time + Mathf.Max(0.1f, animationFinishTimeout);
        while (IsAnimatorInState(affectionStateHash) && Time.time < finishDeadline)
        {
            yield return null;
        }

        if (IsAnimatorInState(affectionStateHash))
        {
            AbortInteraction($"Animator state '{affectionStateName}' did not finish before the timeout.");
            yield break;
        }

        if (waitForDialoguePlaybackBeforeCompleting)
        {
            yield return WaitForDialoguePlaybackToFinish();
        }

        CompleteInteraction();
    }

    private static IEnumerator WaitForDialoguePlaybackToFinish()
    {
        DialogueSubtitleOverlay overlay = DialogueSubtitleOverlay.Instance;
        if (overlay == null)
        {
            yield break;
        }

        while (overlay != null && overlay.IsPlaying)
        {
            yield return null;
        }
    }

    private bool IsAnimatorInState(int stateHash)
    {
        AnimatorStateInfo currentState = dogAnimator.GetCurrentAnimatorStateInfo(animatorLayer);
        if (currentState.shortNameHash == stateHash)
        {
            return true;
        }

        return dogAnimator.IsInTransition(animatorLayer) &&
               dogAnimator.GetNextAnimatorStateInfo(animatorLayer).shortNameHash == stateHash;
    }

    private void CompleteInteraction()
    {
        interactionRoutine = null;
        interactionStarted = false;

        RestoreCameraPriorities();

        // The local gate is only used while the animation is running. Once the
        // sequence ends, RoomInteractable's completion configuration exclusively
        // decides whether this interaction can be detected again.
        roomInteractable.SetInteractable(true);
        roomInteractable.CompleteFromScript();
        onInteractionCompleted.Invoke();
    }

    private void AbortInteraction(string reason)
    {
        Debug.LogWarning($"[RoomDogAffectionInteraction] {reason}", this);

        interactionRoutine = null;
        interactionStarted = false;
        dogAnimator.SetInteger(Animator.StringToHash(actionParameterName), 0);
        RestoreCameraPriorities();

        if (roomInteractable != null)
        {
            roomInteractable.SetInteractable(true);
        }
    }

    private void FocusDogCamera()
    {
        originalDogCameraPriority = dogCamera.Priority;
        originalFollowCameraPriority = followCamera.Priority;
        cameraPrioritiesCaptured = true;

        dogCamera.Priority = Mathf.Max(dogCameraPriority, followCamera.Priority + 1);
    }

    private void RestoreCameraPriorities()
    {
        if (!cameraPrioritiesCaptured)
        {
            return;
        }

        dogCamera.Priority = originalDogCameraPriority;
        followCamera.Priority = originalFollowCameraPriority;
        cameraPrioritiesCaptured = false;
    }

    private bool ValidateConfiguration()
    {
        if (roomInteractable == null || dogAnimator == null || dogCamera == null || followCamera == null)
        {
            Debug.LogWarning(
                "[RoomDogAffectionInteraction] RoomInteractable, Animator, DogCamera and FllowCamera must all be assigned.",
                this);
            return false;
        }

        if (animatorLayer < 0 || animatorLayer >= dogAnimator.layerCount)
        {
            Debug.LogWarning($"[RoomDogAffectionInteraction] Animator layer {animatorLayer} does not exist.", this);
            return false;
        }

        if (!HasAnimatorParameter(sitParameterName, AnimatorControllerParameterType.Bool) ||
            !HasAnimatorParameter(actionParameterName, AnimatorControllerParameterType.Int))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(affectionStateName))
        {
            Debug.LogWarning("[RoomDogAffectionInteraction] Affection state name is empty.", this);
            return false;
        }

        return true;
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType expectedType)
    {
        int parameterHash = Animator.StringToHash(parameterName);
        AnimatorControllerParameter[] parameters = dogAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == parameterHash && parameters[i].type == expectedType)
            {
                return true;
            }
        }

        Debug.LogWarning(
            $"[RoomDogAffectionInteraction] Animator parameter '{parameterName}' ({expectedType}) was not found.",
            this);
        return false;
    }

    private void ResolveLocalReferences()
    {
        if (roomInteractable == null)
        {
            roomInteractable = GetComponent<RoomInteractable>();
        }

        if (dogAnimator == null)
        {
            dogAnimator = GetComponent<Animator>();
        }
    }

    private void OnDisable()
    {
        bool wasRunning = interactionStarted;

        if (interactionRoutine != null)
        {
            StopCoroutine(interactionRoutine);
            interactionRoutine = null;
        }

        if (dogAnimator != null && !string.IsNullOrWhiteSpace(actionParameterName))
        {
            dogAnimator.SetInteger(Animator.StringToHash(actionParameterName), 0);
        }

        RestoreCameraPriorities();

        if (wasRunning && roomInteractable != null)
        {
            roomInteractable.SetInteractable(true);
        }

        interactionStarted = false;
    }
}
