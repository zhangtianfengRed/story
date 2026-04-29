using System.Collections;
using UnityEngine;

/// <summary>
/// Applies a camera transform pose from Timeline Signal Receiver events.
/// </summary>
public class TimelineCameraPoseSetter : MonoBehaviour
{
    [System.Serializable]
    public struct CameraPose
    {
        public string name;
        public bool useLocalSpace;
        public Vector3 position;
        public Vector3 eulerAngles;
        public bool disableAnimatorAfterApply;
    }

    [Header("Target")]
    public Transform targetCamera;
    public bool useMainCameraIfEmpty = true;

    [Header("Animation Driver")]
    public Transform animationDriverTransform;
    public bool useTargetCameraAsAnimationDriver = true;

    [Header("Single Pose")]
    public bool useLocalSpace;
    public Vector3 position;
    public Vector3 eulerAngles;

    [Header("Pose List")]
    public CameraPose[] poses;
    [Min(0)] public int defaultPoseIndex;

    [Header("Timeline Override")]
    public bool disableAnimatorAfterApply;
    public Animator animatorToDisable;
    public bool clearAnimatorControllerWhenEnabling = true;
    public bool cleanupAnimatorStateBeforeEnable = true;

    [Header("Isolated Camera Shots")]
    public TimelineShotPlayer isolatedShotPlayer;
    public bool autoPlayAssignedShotOnDisableAnimatorApply;
    public bool suppressAnimatorEnableWhileShotDirectorPlays = true;
    public bool stopIsolatedShotWhenHoldingPose = true;
    public int[] poseShotIndices;

    private Coroutine enableAnimatorRoutine;
    private RuntimeAnimatorController cachedAnimatorController;
    private bool hasCachedAnimatorController;

    private void Reset()
    {
        ResolveTargetCamera();
        CaptureCurrentPose();
    }

    public void ApplyPose()
    {
        ResolveTargetCamera();

        if (targetCamera == null)
        {
            Debug.LogWarning($"[{nameof(TimelineCameraPoseSetter)}] Missing target camera transform on {name}.", this);
            return;
        }

        ApplyPoseValues(position, eulerAngles, useLocalSpace, disableAnimatorAfterApply);
    }

    public void ApplyPoseAndDisableAnimator()
    {
        bool previousDisableAnimator = disableAnimatorAfterApply;
        disableAnimatorAfterApply = true;
        ApplyPose();
        disableAnimatorAfterApply = previousDisableAnimator;
    }

    public void ApplyPoseAndEnableAnimator()
    {
        ApplyPose();
        EnableAnimator();
    }

    public void ApplyPoseAndEnableAnimatorNextFrame()
    {
        ApplyPose();
        EnableAnimatorNextFrame();
    }

    public void ApplyDefaultListPose()
    {
        ApplyPoseByIndex(defaultPoseIndex);
    }

    public void ApplyDefaultListPoseAndEnableAnimator()
    {
        ApplyPoseByIndex(defaultPoseIndex);
        EnableAnimator();
    }

    public void ApplyDefaultListPoseAndEnableAnimatorNextFrame()
    {
        ApplyPoseByIndex(defaultPoseIndex);
        EnableAnimatorNextFrame();
    }

    public void ApplyPoseByIndex(int poseIndex)
    {
        ResolveTargetCamera();

        if (targetCamera == null)
        {
            Debug.LogWarning($"[{nameof(TimelineCameraPoseSetter)}] Missing target camera transform on {name}.", this);
            return;
        }

        if (poses == null || poseIndex < 0 || poseIndex >= poses.Length)
        {
            Debug.LogWarning($"[{nameof(TimelineCameraPoseSetter)}] Pose index {poseIndex} is not valid on {name}.", this);
            return;
        }

        CameraPose pose = poses[poseIndex];
        ApplyPoseValues(
            pose.position,
            pose.eulerAngles,
            pose.useLocalSpace,
            disableAnimatorAfterApply || pose.disableAnimatorAfterApply);
    }

    public void ApplyPoseByIndexAndEnableAnimator(int poseIndex)
    {
        bool previousDisableAnimator = disableAnimatorAfterApply;
        disableAnimatorAfterApply = false;
        ApplyPoseByIndex(poseIndex);
        disableAnimatorAfterApply = previousDisableAnimator;
        EnableAnimator();
    }

    public void ApplyPoseByIndexAndDisableAnimator(int poseIndex)
    {
        bool previousDisableAnimator = disableAnimatorAfterApply;
        disableAnimatorAfterApply = true;
        ApplyPoseByIndex(poseIndex);
        disableAnimatorAfterApply = previousDisableAnimator;
        TryPlayAssignedShot(poseIndex);
    }

    public void ApplyPoseByIndexAndEnableAnimatorNextFrame(int poseIndex)
    {
        bool previousDisableAnimator = disableAnimatorAfterApply;
        disableAnimatorAfterApply = false;
        ApplyPoseByIndex(poseIndex);
        disableAnimatorAfterApply = previousDisableAnimator;
        EnableAnimatorNextFrame();
    }

    public void ApplyPoseByName(string poseName)
    {
        ResolveTargetCamera();

        if (targetCamera == null)
        {
            Debug.LogWarning($"[{nameof(TimelineCameraPoseSetter)}] Missing target camera transform on {name}.", this);
            return;
        }

        if (poses == null)
        {
            Debug.LogWarning($"[{nameof(TimelineCameraPoseSetter)}] No poses are configured on {name}.", this);
            return;
        }

        for (int i = 0; i < poses.Length; i++)
        {
            if (poses[i].name == poseName)
            {
                ApplyPoseByIndex(i);
                return;
            }
        }

        Debug.LogWarning($"[{nameof(TimelineCameraPoseSetter)}] Pose named '{poseName}' was not found on {name}.", this);
    }

    public void ApplyPoseByNameAndEnableAnimator(string poseName)
    {
        bool previousDisableAnimator = disableAnimatorAfterApply;
        disableAnimatorAfterApply = false;
        ApplyPoseByName(poseName);
        disableAnimatorAfterApply = previousDisableAnimator;
        EnableAnimator();
    }

    public void ApplyPoseByNameAndDisableAnimator(string poseName)
    {
        bool previousDisableAnimator = disableAnimatorAfterApply;
        disableAnimatorAfterApply = true;
        ApplyPoseByName(poseName);
        disableAnimatorAfterApply = previousDisableAnimator;
    }

    public void ApplyPoseByNameAndEnableAnimatorNextFrame(string poseName)
    {
        bool previousDisableAnimator = disableAnimatorAfterApply;
        disableAnimatorAfterApply = false;
        ApplyPoseByName(poseName);
        disableAnimatorAfterApply = previousDisableAnimator;
        EnableAnimatorNextFrame();
    }

    public void EnableAnimator()
    {
        if (ShouldSuppressAnimatorEnable())
        {
            return;
        }

        if (enableAnimatorRoutine != null)
        {
            StopCoroutine(enableAnimatorRoutine);
            enableAnimatorRoutine = null;
        }

        ResolveAnimator();
        if (animatorToDisable != null)
        {
            if (cleanupAnimatorStateBeforeEnable)
            {
                CleanupAnimationStateInternal();
            }

            ClearAnimatorControllerForTimeline();
            animatorToDisable.enabled = true;
        }
    }

    public void EnableAnimatorNextFrame()
    {
        if (!Application.isPlaying)
        {
            EnableAnimator();
            return;
        }

        if (enableAnimatorRoutine != null)
        {
            StopCoroutine(enableAnimatorRoutine);
        }

        enableAnimatorRoutine = StartCoroutine(EnableAnimatorNextFrameRoutine());
    }

    public void ClearAnimatorControllerForTimeline()
    {
        ResolveAnimator();
        if (animatorToDisable == null || !clearAnimatorControllerWhenEnabling)
        {
            return;
        }

        if (!hasCachedAnimatorController)
        {
            cachedAnimatorController = animatorToDisable.runtimeAnimatorController;
            hasCachedAnimatorController = true;
        }

        animatorToDisable.runtimeAnimatorController = null;
    }

    public void RestoreAnimatorController()
    {
        ResolveAnimator();
        if (animatorToDisable != null && hasCachedAnimatorController)
        {
            animatorToDisable.runtimeAnimatorController = cachedAnimatorController;
        }
    }

    [ContextMenu("Capture Current Target Pose")]
    public void CaptureCurrentPose()
    {
        ResolveTargetCamera();
        if (targetCamera == null)
        {
            return;
        }

        if (useLocalSpace)
        {
            position = targetCamera.localPosition;
            eulerAngles = targetCamera.localEulerAngles;
        }
        else
        {
            position = targetCamera.position;
            eulerAngles = targetCamera.eulerAngles;
        }
    }

    [ContextMenu("Capture Current Target Pose To Default List Index")]
    public void CaptureCurrentPoseToDefaultListIndex()
    {
        ResolveTargetCamera();
        if (targetCamera == null)
        {
            return;
        }

        if (poses == null || defaultPoseIndex < 0 || defaultPoseIndex >= poses.Length)
        {
            Debug.LogWarning($"[{nameof(TimelineCameraPoseSetter)}] Default pose index {defaultPoseIndex} is not valid on {name}.", this);
            return;
        }

        CameraPose pose = poses[defaultPoseIndex];
        if (pose.useLocalSpace)
        {
            pose.position = targetCamera.localPosition;
            pose.eulerAngles = targetCamera.localEulerAngles;
        }
        else
        {
            pose.position = targetCamera.position;
            pose.eulerAngles = targetCamera.eulerAngles;
        }

        poses[defaultPoseIndex] = pose;
    }

    [ContextMenu("Add Current Target Pose To List")]
    public void AddCurrentTargetPoseToList()
    {
        ResolveTargetCamera();
        if (targetCamera == null)
        {
            return;
        }

        int nextIndex = poses == null ? 0 : poses.Length;
        System.Array.Resize(ref poses, nextIndex + 1);
        poses[nextIndex] = new CameraPose
        {
            name = $"Pose {nextIndex}",
            useLocalSpace = useLocalSpace,
            position = useLocalSpace ? targetCamera.localPosition : targetCamera.position,
            eulerAngles = useLocalSpace ? targetCamera.localEulerAngles : targetCamera.eulerAngles
        };
        defaultPoseIndex = nextIndex;
    }

    private void ApplyPoseValues(
        Vector3 targetPosition,
        Vector3 targetEulerAngles,
        bool targetUsesLocalSpace,
        bool shouldDisableAnimator)
    {
        if (shouldDisableAnimator)
        {
            ResolveAnimator();
            if (animatorToDisable != null)
            {
                animatorToDisable.enabled = false;
            }
        }

        ResetAnimationDriverLocalOffset();

        Quaternion targetRotation = Quaternion.Euler(targetEulerAngles);
        if (targetUsesLocalSpace)
        {
            targetCamera.localPosition = targetPosition;
            targetCamera.localRotation = targetRotation;
        }
        else
        {
            targetCamera.SetPositionAndRotation(targetPosition, targetRotation);
        }
    }

    private IEnumerator EnableAnimatorNextFrameRoutine()
    {
        yield return null;
        enableAnimatorRoutine = null;
        EnableAnimator();
    }

    public void DisableAnimator()
    {
        if (enableAnimatorRoutine != null)
        {
            StopCoroutine(enableAnimatorRoutine);
            enableAnimatorRoutine = null;
        }

        ResolveAnimator();
        if (animatorToDisable != null)
        {
            animatorToDisable.enabled = false;
        }
    }

    public void HoldCurrentPoseAndDisableAnimator()
    {
        ResolveTargetCamera();
        ResolveAnimationDriverTransform();

        Transform poseSource = animationDriverTransform != null ? animationDriverTransform : targetCamera;
        if (targetCamera == null || poseSource == null)
        {
            Debug.LogWarning($"[{nameof(TimelineCameraPoseSetter)}] Missing target camera transform on {name}.", this);
            return;
        }

        Vector3 currentPosition = poseSource.position;
        Quaternion currentRotation = poseSource.rotation;

        if (stopIsolatedShotWhenHoldingPose && isolatedShotPlayer != null)
        {
            isolatedShotPlayer.StopActiveShot();
        }

        DisableAnimator();
        targetCamera.SetPositionAndRotation(currentPosition, currentRotation);
        ResetAnimationDriverLocalOffset();
    }

    public void HoldCurrentPoseAndDisableAnimatorNextFrame()
    {
        if (!Application.isPlaying)
        {
            HoldCurrentPoseAndDisableAnimator();
            return;
        }

        StartCoroutine(HoldCurrentPoseAndDisableAnimatorNextFrameRoutine());
    }

    public void CleanupAnimationState()
    {
        CleanupAnimationStateInternal();
    }

    public void CleanupAnimationStateNextFrame()
    {
        if (!Application.isPlaying)
        {
            CleanupAnimationStateInternal();
            return;
        }

        StartCoroutine(CleanupAnimationStateNextFrameRoutine());
    }

    private IEnumerator HoldCurrentPoseAndDisableAnimatorNextFrameRoutine()
    {
        yield return null;
        HoldCurrentPoseAndDisableAnimator();
    }

    private IEnumerator CleanupAnimationStateNextFrameRoutine()
    {
        yield return null;
        CleanupAnimationStateInternal();
    }

    private void TryPlayAssignedShot(int poseIndex)
    {
        if (!autoPlayAssignedShotOnDisableAnimatorApply || isolatedShotPlayer == null || poseShotIndices == null)
        {
            return;
        }

        if (poseIndex < 0 || poseIndex >= poseShotIndices.Length)
        {
            return;
        }

        int shotIndex = poseShotIndices[poseIndex];
        if (shotIndex < 0)
        {
            return;
        }

        isolatedShotPlayer.PlayShotByIndex(shotIndex);
    }

    private bool ShouldSuppressAnimatorEnable()
    {
        return suppressAnimatorEnableWhileShotDirectorPlays
            && isolatedShotPlayer != null
            && isolatedShotPlayer.IsPlaying;
    }

    private void ResolveTargetCamera()
    {
        if (targetCamera == null && useMainCameraIfEmpty && Camera.main != null)
        {
            targetCamera = Camera.main.transform;
        }
    }

    private void ResolveAnimationDriverTransform()
    {
        ResolveTargetCamera();
        if (animationDriverTransform == null && useTargetCameraAsAnimationDriver)
        {
            animationDriverTransform = targetCamera;
        }
    }

    private void ResolveAnimator()
    {
        ResolveAnimationDriverTransform();

        Transform animatorTransform = animationDriverTransform != null ? animationDriverTransform : targetCamera;
        if (animatorToDisable == null && animatorTransform != null)
        {
            animatorToDisable = animatorTransform.GetComponent<Animator>();
        }

        if (animatorToDisable != null && !hasCachedAnimatorController)
        {
            cachedAnimatorController = animatorToDisable.runtimeAnimatorController;
            hasCachedAnimatorController = true;
        }
    }

    private void ResetAnimationDriverLocalOffset()
    {
        ResolveTargetCamera();
        ResolveAnimationDriverTransform();

        if (targetCamera == null || animationDriverTransform == null || animationDriverTransform == targetCamera)
        {
            return;
        }

        if (animationDriverTransform.parent != targetCamera)
        {
            return;
        }

        animationDriverTransform.localPosition = Vector3.zero;
        animationDriverTransform.localRotation = Quaternion.identity;
    }

    private void CleanupAnimationStateInternal()
    {
        ResolveTargetCamera();
        ResolveAnimationDriverTransform();
        ResolveAnimator();

        Transform poseSource = animationDriverTransform != null ? animationDriverTransform : targetCamera;
        if (targetCamera == null || poseSource == null)
        {
            return;
        }

        Vector3 currentPosition = poseSource.position;
        Quaternion currentRotation = poseSource.rotation;

        if (animatorToDisable != null)
        {
            animatorToDisable.enabled = false;
            animatorToDisable.Rebind();
            animatorToDisable.Update(0f);
        }

        targetCamera.SetPositionAndRotation(currentPosition, currentRotation);
        ResetAnimationDriverLocalOffset();
    }
}
