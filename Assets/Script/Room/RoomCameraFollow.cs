using UnityEngine;

[RequireComponent(typeof(Camera))]
public class RoomCameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Min(0f)]
    public float followSpeed = 5f;

    [Header("Dead Zone")]
    public bool enableDeadZone = true;
    public Vector2 deadZoneSize = new Vector2(2f, 2f);
    [Min(0f)]
    public float centerStopDistance = 0.05f;

    [Header("World Boundary")]
    public bool enableBoundary = true;
    public Rect worldBoundary = new Rect(-10f, -10f, 20f, 20f);
    public float groundY = 0f;

    [Header("Stabilization")]
    [Tooltip("Stops tiny final follow movements that can look like camera jitter.")]
    [Min(0f)]
    public float settleDistance = 0.01f;

    [Header("Debug")]
    public bool drawGizmos = true;

    private Camera cachedCamera;
    private bool isCenteringAfterDeadZoneExit;

    private void Awake()
    {
        cachedCamera = GetComponent<Camera>();

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 desiredPosition = currentPosition;
        Vector2 viewCenter = GetViewCenterOnGround(currentPosition);
        Vector2 targetGroundPosition = new Vector2(target.position.x, target.position.z);

        if (!enableDeadZone)
        {
            isCenteringAfterDeadZoneExit = false;
            MoveDesiredPositionToTargetCenter(ref desiredPosition, viewCenter, targetGroundPosition);
        }
        else
        {
            if (!isCenteringAfterDeadZoneExit && !IsTargetInsideDeadZone(viewCenter))
            {
                isCenteringAfterDeadZoneExit = true;
            }

            if (isCenteringAfterDeadZoneExit)
            {
                float centerDistance = Vector2.Distance(targetGroundPosition, viewCenter);

                if (centerDistance > centerStopDistance)
                {
                    MoveDesiredPositionToTargetCenter(ref desiredPosition, viewCenter, targetGroundPosition);
                }
                else
                {
                    isCenteringAfterDeadZoneExit = false;
                }
            }
        }

        Vector3 nextPosition = SmoothFollow(currentPosition, desiredPosition);

        if (enableBoundary)
        {
            nextPosition = ClampCameraViewToBoundary(nextPosition);
        }

        transform.position = nextPosition;
    }

    private void MoveDesiredPositionToTargetCenter(
        ref Vector3 desiredPosition,
        Vector2 viewCenter,
        Vector2 targetGroundPosition)
    {
        desiredPosition.x += targetGroundPosition.x - viewCenter.x;
        desiredPosition.z += targetGroundPosition.y - viewCenter.y;
    }

    private bool IsTargetInsideDeadZone(Vector2 viewCenter)
    {
        Vector2 halfSize = deadZoneSize * 0.5f;
        float deltaX = Mathf.Abs(target.position.x - viewCenter.x);
        float deltaZ = Mathf.Abs(target.position.z - viewCenter.y);

        return deltaX <= halfSize.x && deltaZ <= halfSize.y;
    }

    private Vector3 SmoothFollow(Vector3 currentPosition, Vector3 desiredPosition)
    {
        if (followSpeed <= 0f)
        {
            return currentPosition;
        }

        Vector2 groundDelta = new Vector2(
            desiredPosition.x - currentPosition.x,
            desiredPosition.z - currentPosition.z);

        if (groundDelta.sqrMagnitude <= settleDistance * settleDistance)
        {
            desiredPosition.y = currentPosition.y;
            return desiredPosition;
        }

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        Vector3 nextPosition = Vector3.Lerp(currentPosition, desiredPosition, t);

        nextPosition.y = currentPosition.y;
        return nextPosition;
    }

    private Vector3 ClampCameraViewToBoundary(Vector3 candidatePosition)
    {
        if (cachedCamera == null || worldBoundary.width <= 0f || worldBoundary.height <= 0f)
        {
            return candidatePosition;
        }

        Vector3 originalPosition = transform.position;
        transform.position = candidatePosition;

        bool hasGroundFootprint = TryGetGroundFootprintBounds(out Vector2 footprintMin, out Vector2 footprintMax);
        transform.position = originalPosition;

        if (!hasGroundFootprint)
        {
            return candidatePosition;
        }

        Vector3 clampedPosition = candidatePosition;

        float boundaryMinX = worldBoundary.xMin;
        float boundaryMaxX = worldBoundary.xMax;
        float boundaryMinZ = worldBoundary.yMin;
        float boundaryMaxZ = worldBoundary.yMax;

        float footprintWidth = footprintMax.x - footprintMin.x;
        float footprintDepth = footprintMax.y - footprintMin.y;

        if (footprintWidth > worldBoundary.width)
        {
            clampedPosition.x += worldBoundary.center.x - (footprintMin.x + footprintMax.x) * 0.5f;
        }
        else if (footprintMin.x < boundaryMinX)
        {
            clampedPosition.x += boundaryMinX - footprintMin.x;
        }
        else if (footprintMax.x > boundaryMaxX)
        {
            clampedPosition.x -= footprintMax.x - boundaryMaxX;
        }

        if (footprintDepth > worldBoundary.height)
        {
            clampedPosition.z += worldBoundary.center.y - (footprintMin.y + footprintMax.y) * 0.5f;
        }
        else if (footprintMin.y < boundaryMinZ)
        {
            clampedPosition.z += boundaryMinZ - footprintMin.y;
        }
        else if (footprintMax.y > boundaryMaxZ)
        {
            clampedPosition.z -= footprintMax.y - boundaryMaxZ;
        }

        clampedPosition.y = candidatePosition.y;
        return clampedPosition;
    }

    private bool TryGetGroundFootprintBounds(out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;

        bool foundPoint = false;

        TryAddViewportGroundPoint(new Vector3(0f, 0f, 0f), ref foundPoint, ref min, ref max);
        TryAddViewportGroundPoint(new Vector3(1f, 0f, 0f), ref foundPoint, ref min, ref max);
        TryAddViewportGroundPoint(new Vector3(0f, 1f, 0f), ref foundPoint, ref min, ref max);
        TryAddViewportGroundPoint(new Vector3(1f, 1f, 0f), ref foundPoint, ref min, ref max);

        return foundPoint;
    }

    private Vector2 GetViewCenterOnGround(Vector3 cameraPosition)
    {
        if (cachedCamera == null)
        {
            return new Vector2(cameraPosition.x, cameraPosition.z);
        }

        Vector3 originalPosition = transform.position;
        transform.position = cameraPosition;

        bool hasCenter = TryProjectViewportPointToGround(new Vector3(0.5f, 0.5f, 0f), out Vector3 center);
        transform.position = originalPosition;

        if (!hasCenter)
        {
            return new Vector2(cameraPosition.x, cameraPosition.z);
        }

        return new Vector2(center.x, center.z);
    }

    private void TryAddViewportGroundPoint(
        Vector3 viewportPoint,
        ref bool foundPoint,
        ref Vector2 min,
        ref Vector2 max)
    {
        if (!TryProjectViewportPointToGround(viewportPoint, out Vector3 groundPoint))
        {
            return;
        }

        Vector2 point = new Vector2(groundPoint.x, groundPoint.z);

        if (!foundPoint)
        {
            min = point;
            max = point;
            foundPoint = true;
            return;
        }

        min = Vector2.Min(min, point);
        max = Vector2.Max(max, point);
    }

    private bool TryProjectViewportPointToGround(Vector3 viewportPoint, out Vector3 groundPoint)
    {
        Ray ray = cachedCamera.ViewportPointToRay(viewportPoint);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));

        if (groundPlane.Raycast(ray, out float distance))
        {
            groundPoint = ray.GetPoint(distance);
            return true;
        }

        groundPoint = Vector3.zero;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        DrawBoundaryGizmo();
        DrawDeadZoneGizmo();
        DrawCameraFootprintGizmo();
    }

    private void DrawBoundaryGizmo()
    {
        Gizmos.color = Color.yellow;

        Vector3 center = new Vector3(worldBoundary.center.x, groundY, worldBoundary.center.y);
        Vector3 size = new Vector3(worldBoundary.width, 0f, worldBoundary.height);
        Gizmos.DrawWireCube(center, size);
    }

    private void DrawDeadZoneGizmo()
    {
        Gizmos.color = Color.cyan;

        Vector2 viewCenter = GetViewCenterOnGround(transform.position);
        Vector3 center = new Vector3(viewCenter.x, groundY, viewCenter.y);
        Vector3 size = new Vector3(deadZoneSize.x, 0f, deadZoneSize.y);
        Gizmos.DrawWireCube(center, size);
    }

    private void DrawCameraFootprintGizmo()
    {
        Camera cameraComponent = cachedCamera != null ? cachedCamera : GetComponent<Camera>();
        if (cameraComponent == null)
        {
            return;
        }

        cachedCamera = cameraComponent;

        if (!TryProjectViewportPointToGround(new Vector3(0f, 0f, 0f), out Vector3 bottomLeft))
        {
            return;
        }

        if (!TryProjectViewportPointToGround(new Vector3(1f, 0f, 0f), out Vector3 bottomRight))
        {
            return;
        }

        if (!TryProjectViewportPointToGround(new Vector3(1f, 1f, 0f), out Vector3 topRight))
        {
            return;
        }

        if (!TryProjectViewportPointToGround(new Vector3(0f, 1f, 0f), out Vector3 topLeft))
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);
    }
}
