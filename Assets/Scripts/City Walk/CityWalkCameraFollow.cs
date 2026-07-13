using UnityEngine;

/// <summary>
/// City Walk 场景专用的横向摄像机跟随。
/// 只跟随目标的世界 X 坐标，并始终保持摄像机的初始 Y、Z 和旋转。
/// </summary>
public class CityWalkCameraFollow : MonoBehaviour
{
    [SerializeField]
    private Transform target;

    [SerializeField]
    private bool smoothFollow = true;

    [SerializeField, Min(0f)]
    private float smoothTime = 0.15f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float initialXOffset;
    private float xVelocity;
    private bool hasXOffset;

    private void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        TryInitializeXOffset();
    }

    private void LateUpdate()
    {
        TryInitializeXOffset();

        float nextX = transform.position.x;
        if (target != null && hasXOffset)
        {
            float targetX = target.position.x + initialXOffset;
            nextX = smoothFollow && smoothTime > 0f
                ? Mathf.SmoothDamp(nextX, targetX, ref xVelocity, smoothTime)
                : targetX;
        }

        transform.SetPositionAndRotation(
            new Vector3(nextX, initialPosition.y, initialPosition.z),
            initialRotation);
    }

    private void TryInitializeXOffset()
    {
        if (hasXOffset || target == null)
        {
            return;
        }

        initialXOffset = initialPosition.x - target.position.x;
        hasXOffset = true;
    }
}
