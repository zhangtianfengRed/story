using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class RoomTopDownPlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [Min(0f)]
    public float moveSpeed = 4f;

    [Tooltip("不指定时会自动使用 Main Camera。WASD 会按照这个相机画面的上下左右方向移动。")]
    public Camera movementCamera;

    [Header("Rotation")]
    public bool rotateToMoveDirection = true;
    [Min(0f)]
    public float rotationSpeed = 12f;

    [Header("Animation")]
    [Tooltip("Keep movement controlled by this script instead of animation root motion.")]
    public bool disableAnimatorRootMotion = true;

    private CharacterController characterController;
    private Animator characterAnimator;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        characterAnimator = GetComponent<Animator>();

        if (disableAnimatorRootMotion && characterAnimator != null)
        {
            characterAnimator.applyRootMotion = false;
        }

        if (movementCamera == null)
        {
            movementCamera = Camera.main;
        }
    }

    private void Update()
    {
        Vector3 moveDirection = GetWorldMoveDirection();
        characterController.SimpleMove(moveDirection * moveSpeed);

        if (rotateToMoveDirection && moveDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
        }
    }

    private Vector3 GetWorldMoveDirection()
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

        if (Input.GetKey(KeyCode.W))
        {
            direction += screenUp;
        }

        if (Input.GetKey(KeyCode.S))
        {
            direction -= screenUp;
        }

        if (Input.GetKey(KeyCode.D))
        {
            direction += screenRight;
        }

        if (Input.GetKey(KeyCode.A))
        {
            direction -= screenRight;
        }

        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }
}
