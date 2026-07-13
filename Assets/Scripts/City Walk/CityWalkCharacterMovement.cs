using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// City Walk 场景中的角色平面移动。
/// A/D 控制世界 X 轴，W/S 控制世界 Z 轴。
/// </summary>
public class CityWalkCharacterMovement : MonoBehaviour
{
    private static readonly int WalkParameter = Animator.StringToHash("Walk");

    [SerializeField, Min(0f)]
    private float moveSpeed = 4f;

    [Header("Animation")]
    [SerializeField]
    [Tooltip("If left empty, an Animator is found automatically on this object or its children at runtime.")]
    private Animator animator;

    [Header("Movement Facing")]
    [SerializeField]
    [Tooltip("Additional world Y rotation used when the model's visual forward axis is not local +Z.")]
    private float modelForwardYawOffset;

    [Header("Movement Bounds (World X / Z)")]
    [SerializeField]
    [Tooltip("X is the minimum world X coordinate; Y is the minimum world Z coordinate.")]
    private Vector2 movementBoundsMin = new Vector2(-5f, -5f);

    [SerializeField]
    [Tooltip("X is the maximum world X coordinate; Y is the maximum world Z coordinate.")]
    private Vector2 movementBoundsMax = new Vector2(5f, 5f);

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private void Update()
    {
        Vector2 input = ReadMovementInput();
        Vector3 movement = new Vector3(input.x, 0f, input.y);

        // 避免斜向移动比单轴移动更快。
        if (movement.sqrMagnitude > 1f)
        {
            movement.Normalize();
        }

        Vector3 currentPosition = transform.position;
        Vector3 nextPosition = currentPosition + movement * (moveSpeed * Time.deltaTime);
        GetOrderedMovementBounds(out Vector2 boundsMin, out Vector2 boundsMax);

        // Clamp after movement so the character can move back inward while it can
        // never continue past any of the four X/Z boundaries.
        nextPosition.x = Mathf.Clamp(nextPosition.x, boundsMin.x, boundsMax.x);
        nextPosition.z = Mathf.Clamp(nextPosition.z, boundsMin.y, boundsMax.y);
        transform.position = nextPosition;

        Vector3 actualMovement = nextPosition - currentPosition;
        UpdateMovementFacing(actualMovement);

        bool isWalking = actualMovement.sqrMagnitude > 0.000001f;
        if (animator != null)
        {
            animator.SetBool(WalkParameter, isWalking);
        }
    }

    private void UpdateMovementFacing(Vector3 movementDirection)
    {
        movementDirection.y = 0f;
        if (movementDirection.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(
            movementDirection.normalized,
            Vector3.up);
        Quaternion modelOffset = Quaternion.Euler(0f, modelForwardYawOffset, 0f);
        transform.rotation = targetRotation * modelOffset;
    }

    private void OnDisable()
    {
        if (animator != null)
        {
            animator.SetBool(WalkParameter, false);
        }
    }

    private void GetOrderedMovementBounds(out Vector2 boundsMin, out Vector2 boundsMax)
    {
        boundsMin = Vector2.Min(movementBoundsMin, movementBoundsMax);
        boundsMax = Vector2.Max(movementBoundsMin, movementBoundsMax);
    }

    private void OnValidate()
    {
        GetOrderedMovementBounds(out Vector2 boundsMin, out Vector2 boundsMax);
        movementBoundsMin = boundsMin;
        movementBoundsMax = boundsMax;
    }

    private void OnDrawGizmosSelected()
    {
        GetOrderedMovementBounds(out Vector2 boundsMin, out Vector2 boundsMax);

        float width = boundsMax.x - boundsMin.x;
        float depth = boundsMax.y - boundsMin.y;
        Vector3 center = new Vector3(
            (boundsMin.x + boundsMax.x) * 0.5f,
            transform.position.y,
            (boundsMin.y + boundsMax.y) * 0.5f);

        Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.9f);
        Gizmos.DrawWireCube(center, new Vector3(width, 0.02f, depth));
    }

    private static Vector2 ReadMovementInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed)
        {
            horizontal += 1f;
        }

        if (keyboard.dKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (keyboard.sKey.isPressed)
        {
            vertical += 1f;
        }

        if (keyboard.wKey.isPressed)
        {
            vertical -= 1f;
        }

        return new Vector2(horizontal, vertical);
#else
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.A))
        {
            horizontal += 1f;
        }

        if (Input.GetKey(KeyCode.D))
        {
            horizontal -= 1f;
        }

        if (Input.GetKey(KeyCode.S))
        {
            vertical += 1f;
        }

        if (Input.GetKey(KeyCode.W))
        {
            vertical -= 1f;
        }

        return new Vector2(horizontal, vertical);
#endif
    }
}
