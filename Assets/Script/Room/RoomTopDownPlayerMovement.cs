using System;
using System.Runtime.InteropServices;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class RoomTopDownPlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("关闭后不响应 WASD/方向键移动输入，可由房间互动行为切换。")]
    [SerializeField] private bool movementControlEnabled = true;

    [Min(0f)]
    public float moveSpeed = 4f;
    [Tooltip("使用 CharacterController.Move 时用于贴地的向下速度。")]
    public float groundedVerticalSpeed = -2f;

    [Tooltip("不指定时会自动使用 Main Camera。WASD 会按照这个相机画面的上下左右方向移动。")]
    public Camera movementCamera;

    [Header("Rotation")]
    public bool rotateToMoveDirection = true;
    [Min(0f)]
    public float rotationSpeed = 12f;

    [Header("Animation")]
    [Tooltip("Keep movement controlled by this script instead of animation root motion.")]
    public bool disableAnimatorRootMotion = true;
    [Tooltip("不指定时会自动查找当前对象或子对象上的 Animator。")]
    public Animator targetAnimator;

    [Header("Resume")]
    [Tooltip("进入场景时，如果当前步骤保存过继续坐标，则自动把玩家摆到那个位置。")]
    public bool restoreSavedResumeStateOnStart = true;

    [Header("Footsteps")]
    [Tooltip("播放移动脚步声的 AudioSource。不指定时会自动使用或创建当前对象上的 AudioSource。")]
    public AudioSource footstepAudioSource;
    public AudioClip leftFootstepClip;
    public AudioClip rightFootstepClip;
    [Min(0.05f)]
    public float footstepInterval = 0.42f;
    [HideInInspector]
    public float footstepVolume = 0.75f;
    public Vector2 footstepVolumeRange = new Vector2(0.82f, 1f);
    public Vector2 footstepPitchRange = new Vector2(0.92f, 1.06f);
    [Tooltip("移动开始时是否立刻播放第一下脚步声。")]
    public bool playFootstepImmediately = true;

    private CharacterController characterController;
    private Animator characterAnimator;
    private static readonly int WalkParameter = Animator.StringToHash("Walk");
    private const float MoveThreshold = 0.0001f;
    private float verticalSpeed;
    private float footstepTimer;
    private bool playLeftFootstepNext = true;
    private bool wasPlayingFootsteps;
    private bool moveForwardPressed;
    private bool moveBackwardPressed;
    private bool moveRightPressed;
    private bool moveLeftPressed;
    private bool hasAttemptedResumeRestore;
    private const int VirtualKeyW = 0x57;
    private const int VirtualKeyA = 0x41;
    private const int VirtualKeyS = 0x53;
    private const int VirtualKeyD = 0x44;
    private const int VirtualKeyUpArrow = 0x26;
    private const int VirtualKeyDownArrow = 0x28;
    private const int VirtualKeyLeftArrow = 0x25;
    private const int VirtualKeyRightArrow = 0x27;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
#endif

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        characterAnimator = targetAnimator != null
            ? targetAnimator
            : GetComponent<Animator>();

        if (characterAnimator == null)
        {
            characterAnimator = GetComponentInChildren<Animator>();
        }

        if (disableAnimatorRootMotion && characterAnimator != null)
        {
            characterAnimator.applyRootMotion = false;
        }

        if (movementCamera == null)
        {
            movementCamera = Camera.main;
        }

        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
        }

        if (footstepAudioSource == null && (leftFootstepClip != null || rightFootstepClip != null))
        {
            footstepAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (footstepAudioSource != null)
        {
            footstepAudioSource.playOnAwake = false;

            if (footstepAudioSource.GetComponent<SettingsAudioSource>() == null)
            {
                footstepAudioSource.gameObject.AddComponent<SettingsAudioSource>();
            }
        }
    }

    private void Start()
    {
        TryRestoreSavedResumeState();
    }

    private void Update()
    {
        if (!movementControlEnabled)
        {
            return;
        }

        UpdateMovementButtons();

        Vector2 moveInput = GetMoveInput();
        Vector3 moveDirection = GetWorldMoveDirection(moveInput);
        bool isMoving = moveInput.sqrMagnitude > MoveThreshold;

        UpdateVerticalSpeed();

        Vector3 frameMotion =
            (moveDirection * moveSpeed * Time.deltaTime) +
            (Vector3.up * (verticalSpeed * Time.deltaTime));

        CollisionFlags collisionFlags = characterController.Move(frameMotion);

        if ((collisionFlags & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
        {
            verticalSpeed = groundedVerticalSpeed;
        }

        if (characterAnimator != null)
        {
            characterAnimator.SetBool(WalkParameter, isMoving);
        }

        UpdateFootsteps(isMoving);

        if (rotateToMoveDirection && isMoving)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
        }
    }

    private void UpdateVerticalSpeed()
    {
        if (characterController.isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = groundedVerticalSpeed;
            return;
        }

        verticalSpeed += Physics.gravity.y * Time.deltaTime;
    }

    private void UpdateFootsteps(bool isMoving)
    {
        if (!isMoving || footstepAudioSource == null)
        {
            if (wasPlayingFootsteps && footstepAudioSource != null)
            {
                footstepAudioSource.Stop();
            }

            footstepTimer = playFootstepImmediately ? 0f : footstepInterval;
            playLeftFootstepNext = true;
            wasPlayingFootsteps = false;
            return;
        }

        wasPlayingFootsteps = true;
        footstepTimer -= Time.deltaTime;

        if (footstepTimer > 0f)
        {
            return;
        }

        PlayNextFootstep();
        footstepTimer = footstepInterval;
    }

    private void PlayNextFootstep()
    {
        AudioClip clip = playLeftFootstepNext ? leftFootstepClip : rightFootstepClip;

        if (clip == null)
        {
            clip = playLeftFootstepNext ? rightFootstepClip : leftFootstepClip;
        }

        if (clip != null)
        {
            float randomVolume = UnityEngine.Random.Range(footstepVolumeRange.x, footstepVolumeRange.y);
            footstepAudioSource.pitch = UnityEngine.Random.Range(footstepPitchRange.x, footstepPitchRange.y);
            footstepAudioSource.PlayOneShot(clip, footstepVolume * randomVolume);
        }

        playLeftFootstepNext = !playLeftFootstepNext;
    }

    private void OnDisable()
    {
        ResetMovementState();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            ResetMovementState();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            ResetMovementState();
        }
    }

    private void ResetMovementState()
    {
        verticalSpeed = groundedVerticalSpeed;
        moveForwardPressed = false;
        moveBackwardPressed = false;
        moveRightPressed = false;
        moveLeftPressed = false;
        footstepTimer = playFootstepImmediately ? 0f : footstepInterval;
        playLeftFootstepNext = true;
        wasPlayingFootsteps = false;

        if (footstepAudioSource != null)
        {
            footstepAudioSource.Stop();
        }

        if (characterAnimator != null)
        {
            characterAnimator.SetBool(WalkParameter, false);
        }
    }

    public bool TryRestoreSavedResumeState()
    {
        if (!restoreSavedResumeStateOnStart || hasAttemptedResumeRestore)
        {
            return false;
        }

        hasAttemptedResumeRestore = true;

        if (!RoomInteractionProgressManager.Instance.TryGetResumeState(out RoomInteractionResumeState resumeState) ||
            !resumeState.HasAnyData())
        {
            return false;
        }

        ApplyResumeState(resumeState);
        return true;
    }

    private void ApplyResumeState(RoomInteractionResumeState resumeState)
    {
        Vector3 targetPosition = resumeState.hasPosition ? resumeState.position : transform.position;
        Quaternion targetRotation = resumeState.hasRotation
            ? Quaternion.Euler(resumeState.eulerAngles)
            : transform.rotation;

        bool wasControllerEnabled = characterController != null && characterController.enabled;
        if (wasControllerEnabled)
        {
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(targetPosition, targetRotation);
        Physics.SyncTransforms();

        if (wasControllerEnabled)
        {
            characterController.enabled = true;
        }

        ResetMovementState();
    }

    public bool MovementControlEnabled
    {
        get { return movementControlEnabled; }
    }

    public void SetMovementControlEnabled(bool enabled)
    {
        if (movementControlEnabled == enabled)
        {
            return;
        }

        movementControlEnabled = enabled;

        if (!movementControlEnabled)
        {
            ResetMovementState();
        }
    }

    private void UpdateMovementButtons()
    {
        UpdateButtonState(IsMoveKeyPressed(KeyCode.W, KeyCode.UpArrow, VirtualKeyW, VirtualKeyUpArrow), ref moveForwardPressed);
        UpdateButtonState(IsMoveKeyPressed(KeyCode.S, KeyCode.DownArrow, VirtualKeyS, VirtualKeyDownArrow), ref moveBackwardPressed);
        UpdateButtonState(IsMoveKeyPressed(KeyCode.D, KeyCode.RightArrow, VirtualKeyD, VirtualKeyRightArrow), ref moveRightPressed);
        UpdateButtonState(IsMoveKeyPressed(KeyCode.A, KeyCode.LeftArrow, VirtualKeyA, VirtualKeyLeftArrow), ref moveLeftPressed);
    }

    private bool IsMoveKeyPressed(KeyCode primary, KeyCode alternate, int primaryVirtualKey, int alternateVirtualKey)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (Application.isFocused)
        {
            return IsVirtualKeyPressed(primaryVirtualKey) || IsVirtualKeyPressed(alternateVirtualKey);
        }
#endif
        return Input.GetKey(primary) || Input.GetKey(alternate);
    }

    private void UpdateButtonState(bool isPressedNow, ref bool pressedState)
    {
        if (pressedState == isPressedNow)
        {
            return;
        }

        pressedState = isPressedNow;
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private static bool IsVirtualKeyPressed(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }
#endif

    private Vector2 GetMoveInput()
    {
        float vertical = 0f;
        float horizontal = 0f;

        if (moveForwardPressed)
        {
            vertical += 1f;
        }

        if (moveBackwardPressed)
        {
            vertical -= 1f;
        }

        if (moveRightPressed)
        {
            horizontal += 1f;
        }

        if (moveLeftPressed)
        {
            horizontal -= 1f;
        }

        Vector2 moveInput = new Vector2(horizontal, vertical);
        return moveInput.sqrMagnitude > 1f ? moveInput.normalized : moveInput;
    }

    private Vector3 GetWorldMoveDirection(Vector2 moveInput)
    {
        Vector3 screenUp = Vector3.forward;
        Vector3 screenRight = Vector3.right;

        if (movementCamera != null)
        {
            screenUp = Vector3.ProjectOnPlane(movementCamera.transform.up, Vector3.up);
            screenRight = Vector3.ProjectOnPlane(movementCamera.transform.right, Vector3.up);

            if (screenUp.sqrMagnitude <= 0.0001f)
            {
                screenUp = Vector3.forward;
            }

            if (screenRight.sqrMagnitude <= 0.0001f)
            {
                screenRight = Vector3.right;
            }
        }

        screenUp.Normalize();
        screenRight.Normalize();

        Vector3 direction = Vector3.zero;
        direction += screenUp * moveInput.y;
        direction += screenRight * moveInput.x;

        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }
}
