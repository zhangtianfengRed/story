using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves a background pedestrian along fixed scene waypoints. By default the
/// pedestrian walks to the final point and retraces the route back to the first
/// point forever. Civil Characters Pack animators are driven through "walk".
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Story/City Walk/Pedestrian Waypoint Patrol")]
public sealed class CityWalkPedestrianPatrol : MonoBehaviour
{
    /// <summary>
    /// Raised once when the pedestrian reaches the first or final route point.
    /// </summary>
    public event Action RouteBoundaryReached;

    public enum RouteTraversalMode
    {
        PingPong,
        Loop
    }

    [Header("Waypoint Route")]
    [SerializeField]
    [Tooltip("Optional route container. Its direct children are used as ordered points, making it easy to add and reorder points.")]
    private Transform routeRoot;

    [SerializeField]
    [Tooltip("Manual fixed markers used when Route Root is empty. Use at least two points.")]
    private Transform[] routePoints = Array.Empty<Transform>();

    [SerializeField]
    [Tooltip("Ping Pong retraces the route at both ends. Loop connects the last point back to the first.")]
    private RouteTraversalMode traversalMode = RouteTraversalMode.PingPong;

    [SerializeField]
    [Tooltip("Move the pedestrian to the first route point when enabled.")]
    private bool snapToFirstPointOnEnable;

    [Header("Fallback Route")]
    [SerializeField]
    [Tooltip("Used only when fewer than two valid waypoint transforms are assigned.")]
    private Vector3 fallbackEndOffset = new Vector3(6f, 0f, 0f);

    [SerializeField]
    [Tooltip("Rotate the fallback offset by the NPC's initial rotation.")]
    private bool fallbackOffsetIsLocal;

    [Header("Movement")]
    [SerializeField]
    [Tooltip("A new movement speed is selected from this range at startup and whenever the pedestrian reaches either end of the route.")]
    private Vector2 moveSpeedRange = new Vector2(0.5f, 1.8f);

    [SerializeField, Min(0f)]
    private float turnSpeed = 8f;

    [SerializeField, Min(0.001f)]
    private float arrivalDistance = 0.04f;

    [SerializeField, Min(0f)]
    [Tooltip("Pause only at the first and final points of a Ping Pong route.")]
    private float endpointPause = 0.25f;

    [SerializeField]
    [Tooltip("Keeps the pedestrian at its current height while walking.")]
    private bool lockVerticalPosition = true;

    [SerializeField]
    [Tooltip("Extra yaw for models whose visual forward direction is not local +Z.")]
    private float modelForwardYawOffset;

    [Header("Civil Characters Pack Animation")]
    [SerializeField]
    private string walkParameterName = "walk";

    [SerializeField, Min(0.01f)]
    [Tooltip("Movement speed at which the original walk animation plays at 1x speed.")]
    private float animationReferenceMoveSpeed = 1.2f;

    [SerializeField]
    [Tooltip("Find every Animator below the NPC, including all LOD levels.")]
    private bool autoFindAnimators = true;

    [SerializeField]
    private Animator[] animators = Array.Empty<Animator>();

    private readonly List<Transform> validRoutePoints = new List<Transform>();
    private readonly List<AnimatorBinding> animatorBindings =
        new List<AnimatorBinding>();

    private CharacterController characterController;
    private Vector3 fallbackStartPosition;
    private Vector3 fallbackEndPosition;
    private Quaternion initialRotation;
    private float currentMoveSpeed = 1f;
    private float waitRemaining;
    private int targetPointIndex;
    private int routeDirection = 1;
    private bool waitingAtEndpoint;
    private bool animationWalking;
    private bool initialized;
    private Transform animatorSearchRoot;

    private sealed class AnimatorBinding
    {
        public Animator animator;
        public float originalSpeed;
        public bool hasWalkParameter;
    }

    private int RoutePointCount => validRoutePoints.Count >= 2
        ? validRoutePoints.Count
        : 2;

    public float CurrentMoveSpeed => currentMoveSpeed;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        InitializePatrol();
    }

    private void OnEnable()
    {
        if (initialized)
        {
            BeginPatrol();
        }
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        Vector3 targetPosition = GetRoutePointPosition(targetPointIndex);
        Vector3 toTarget = targetPosition - transform.position;
        if (lockVerticalPosition)
        {
            toTarget.y = 0f;
        }

        if (toTarget.sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            HandlePointArrival();
            return;
        }

        waitingAtEndpoint = false;
        SetWalkingAnimation(true);
        FaceMovementDirection(toTarget);
        MoveTowards(targetPosition);
    }

    private void OnDisable()
    {
        SetWalkingAnimation(false);
        RestoreAnimatorSpeeds();
    }

    /// <summary>
    /// Assigns a fixed route. Route points should not be children of this NPC.
    /// </summary>
    public void SetRoute(Transform[] points)
    {
        routeRoot = null;
        routePoints = points ?? Array.Empty<Transform>();

        if (Application.isPlaying && initialized)
        {
            RefreshValidRoutePoints();
            BeginPatrol();
        }
    }

    /// <summary>
    /// Uses the direct children of a fixed scene object as ordered route points.
    /// </summary>
    public void SetRouteRoot(Transform root)
    {
        routeRoot = root;

        if (Application.isPlaying && initialized)
        {
            RefreshValidRoutePoints();
            BeginPatrol();
        }
    }

    /// <summary>
    /// Rebinds the walk animation after a runtime character model is replaced.
    /// </summary>
    public void RefreshAnimators(Transform searchRoot = null)
    {
        RestoreAnimatorSpeeds();
        animatorSearchRoot = searchRoot;
        CacheAnimators();
        ApplyAnimatorSpeeds();

        bool shouldWalk = !waitingAtEndpoint;
        animationWalking = !shouldWalk;
        SetWalkingAnimation(shouldWalk);
    }

    private void InitializePatrol()
    {
        initialRotation = transform.rotation;
        fallbackStartPosition = transform.position;
        fallbackEndPosition = fallbackStartPosition +
            (fallbackOffsetIsLocal
                ? initialRotation * fallbackEndOffset
                : fallbackEndOffset);

        SelectNewMovementSpeed();

        RefreshValidRoutePoints();
        CacheAnimators();
        initialized = true;
        BeginPatrol();
    }

    private void BeginPatrol()
    {
        RefreshValidRoutePoints();
        waitingAtEndpoint = false;
        waitRemaining = 0f;
        routeDirection = 1;

        if (snapToFirstPointOnEnable)
        {
            SetWorldPosition(GetRoutePointPosition(0));
            targetPointIndex = 1;
        }
        else
        {
            ChooseInitialTargetPoint();
        }

        ApplyAnimatorSpeeds();
        SetWalkingAnimation(true);
    }

    private void ChooseInitialTargetPoint()
    {
        int closestPointIndex = 0;
        float closestSqrDistance = float.PositiveInfinity;

        for (int i = 0; i < RoutePointCount; i++)
        {
            Vector3 difference = GetRoutePointPosition(i) - transform.position;
            if (lockVerticalPosition)
            {
                difference.y = 0f;
            }

            float sqrDistance = difference.sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestPointIndex = i;
            }
        }

        if (closestSqrDistance > arrivalDistance * arrivalDistance)
        {
            targetPointIndex = closestPointIndex;
            routeDirection = closestPointIndex >= RoutePointCount - 1 ? -1 : 1;
            return;
        }

        if (traversalMode == RouteTraversalMode.Loop)
        {
            targetPointIndex = (closestPointIndex + 1) % RoutePointCount;
            routeDirection = 1;
            return;
        }

        routeDirection = closestPointIndex >= RoutePointCount - 1 ? -1 : 1;
        targetPointIndex = closestPointIndex + routeDirection;
    }

    private void HandlePointArrival()
    {
        bool isRouteBoundary =
            targetPointIndex == 0 || targetPointIndex == RoutePointCount - 1;
        bool shouldPause =
            traversalMode == RouteTraversalMode.PingPong &&
            isRouteBoundary &&
            endpointPause > 0f;

        if (!isRouteBoundary)
        {
            waitingAtEndpoint = false;
            AdvanceTargetPoint();
            return;
        }

        if (!waitingAtEndpoint)
        {
            SelectNewMovementSpeed();
            ApplyAnimatorSpeeds();

            if (shouldPause)
            {
                waitingAtEndpoint = true;
                waitRemaining = endpointPause;
                SetWalkingAnimation(false);
            }

            RouteBoundaryReached?.Invoke();

            if (!shouldPause)
            {
                AdvanceTargetPoint();
                return;
            }
        }

        if (waitRemaining > 0f)
        {
            waitRemaining -= Time.deltaTime;
            return;
        }

        waitingAtEndpoint = false;
        AdvanceTargetPoint();
        SetWalkingAnimation(true);
    }

    private void AdvanceTargetPoint()
    {
        if (traversalMode == RouteTraversalMode.Loop)
        {
            targetPointIndex = (targetPointIndex + 1) % RoutePointCount;
            routeDirection = 1;
            return;
        }

        if (targetPointIndex >= RoutePointCount - 1)
        {
            routeDirection = -1;
        }
        else if (targetPointIndex <= 0)
        {
            routeDirection = 1;
        }

        targetPointIndex += routeDirection;
    }

    private void MoveTowards(Vector3 targetPosition)
    {
        Vector3 currentPosition = transform.position;
        if (lockVerticalPosition)
        {
            targetPosition.y = currentPosition.y;
        }

        float frameDistance = currentMoveSpeed * Time.deltaTime;
        Vector3 nextPosition = Vector3.MoveTowards(
            currentPosition,
            targetPosition,
            frameDistance);
        Vector3 frameMotion = nextPosition - currentPosition;

        if (characterController != null && characterController.enabled)
        {
            characterController.Move(frameMotion);
        }
        else
        {
            transform.position = nextPosition;
        }
    }

    private void FaceMovementDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Quaternion facing = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Quaternion modelOffset = Quaternion.Euler(0f, modelForwardYawOffset, 0f);
        Quaternion targetRotation = facing * modelOffset;

        if (turnSpeed <= 0f)
        {
            transform.rotation = targetRotation;
            return;
        }

        float blend = 1f - Mathf.Exp(-turnSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            blend);
    }

    private void RefreshValidRoutePoints()
    {
        validRoutePoints.Clear();

        if (routeRoot != null &&
            routeRoot != transform &&
            !routeRoot.IsChildOf(transform))
        {
            for (int i = 0; i < routeRoot.childCount; i++)
            {
                Transform point = routeRoot.GetChild(i);
                if (point != null)
                {
                    validRoutePoints.Add(point);
                }
            }

            return;
        }

        if (routePoints == null)
        {
            return;
        }

        for (int i = 0; i < routePoints.Length; i++)
        {
            Transform point = routePoints[i];
            if (point != null && point != transform && !point.IsChildOf(transform))
            {
                validRoutePoints.Add(point);
            }
        }
    }

    private Vector3 GetRoutePointPosition(int pointIndex)
    {
        if (validRoutePoints.Count >= 2)
        {
            int clampedIndex = Mathf.Clamp(
                pointIndex,
                0,
                validRoutePoints.Count - 1);
            return validRoutePoints[clampedIndex].position;
        }

        return pointIndex <= 0
            ? fallbackStartPosition
            : fallbackEndPosition;
    }

    private void CacheAnimators()
    {
        animatorBindings.Clear();

        if (autoFindAnimators || animators == null || animators.Length == 0)
        {
            Transform searchRoot = animatorSearchRoot != null
                ? animatorSearchRoot
                : transform;
            animators = searchRoot.GetComponentsInChildren<Animator>(true);
        }

        int walkParameterHash = Animator.StringToHash(walkParameterName);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
            {
                continue;
            }

            animatorBindings.Add(new AnimatorBinding
            {
                animator = animator,
                originalSpeed = animator.speed,
                hasWalkParameter = HasBoolParameter(animator, walkParameterHash)
            });
        }
    }

    private void ApplyAnimatorSpeeds()
    {
        for (int i = 0; i < animatorBindings.Count; i++)
        {
            AnimatorBinding binding = animatorBindings[i];
            if (binding.animator != null)
            {
                binding.animator.speed =
                    binding.originalSpeed *
                    (currentMoveSpeed / animationReferenceMoveSpeed);
            }
        }
    }

    private void SelectNewMovementSpeed()
    {
        float minimumSpeed = Mathf.Min(moveSpeedRange.x, moveSpeedRange.y);
        float maximumSpeed = Mathf.Max(moveSpeedRange.x, moveSpeedRange.y);
        currentMoveSpeed = UnityEngine.Random.Range(minimumSpeed, maximumSpeed);
    }

    private void RestoreAnimatorSpeeds()
    {
        for (int i = 0; i < animatorBindings.Count; i++)
        {
            AnimatorBinding binding = animatorBindings[i];
            if (binding.animator != null)
            {
                binding.animator.speed = binding.originalSpeed;
            }
        }
    }

    private void SetWalkingAnimation(bool isWalking)
    {
        if (animationWalking == isWalking && animatorBindings.Count > 0)
        {
            return;
        }

        animationWalking = isWalking;
        int walkParameterHash = Animator.StringToHash(walkParameterName);

        for (int i = 0; i < animatorBindings.Count; i++)
        {
            AnimatorBinding binding = animatorBindings[i];
            if (binding.animator != null && binding.hasWalkParameter)
            {
                binding.animator.SetBool(walkParameterHash, isWalking);
            }
        }
    }

    private static bool HasBoolParameter(Animator animator, int parameterHash)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == parameterHash &&
                parameter.type == AnimatorControllerParameterType.Bool)
            {
                return true;
            }
        }

        return false;
    }

    private void SetWorldPosition(Vector3 position)
    {
        if (lockVerticalPosition)
        {
            position.y = transform.position.y;
        }

        if (characterController != null && characterController.enabled)
        {
            characterController.enabled = false;
            transform.position = position;
            characterController.enabled = true;
        }
        else
        {
            transform.position = position;
        }
    }

    private void OnValidate()
    {
        moveSpeedRange.x = Mathf.Max(0f, moveSpeedRange.x);
        moveSpeedRange.y = Mathf.Max(0f, moveSpeedRange.y);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        arrivalDistance = Mathf.Max(0.001f, arrivalDistance);
        endpointPause = Mathf.Max(0f, endpointPause);
        animationReferenceMoveSpeed = Mathf.Max(
            0.01f,
            animationReferenceMoveSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        List<Transform> editorPoints = new List<Transform>();
        if (routeRoot != null &&
            routeRoot != transform &&
            !routeRoot.IsChildOf(transform))
        {
            for (int i = 0; i < routeRoot.childCount; i++)
            {
                Transform point = routeRoot.GetChild(i);
                if (point != null)
                {
                    editorPoints.Add(point);
                }
            }
        }
        else if (routePoints != null)
        {
            for (int i = 0; i < routePoints.Length; i++)
            {
                Transform point = routePoints[i];
                if (point != null && point != transform && !point.IsChildOf(transform))
                {
                    editorPoints.Add(point);
                }
            }
        }

        Gizmos.color = new Color(0.15f, 0.9f, 0.95f, 0.9f);
        if (editorPoints.Count >= 2)
        {
            for (int i = 0; i < editorPoints.Count; i++)
            {
                Gizmos.DrawWireSphere(editorPoints[i].position, 0.12f);
                if (i < editorPoints.Count - 1)
                {
                    Gizmos.DrawLine(
                        editorPoints[i].position,
                        editorPoints[i + 1].position);
                }
            }

            if (traversalMode == RouteTraversalMode.Loop)
            {
                Gizmos.DrawLine(
                    editorPoints[editorPoints.Count - 1].position,
                    editorPoints[0].position);
            }

            return;
        }

        Quaternion routeRotation = Application.isPlaying
            ? initialRotation
            : transform.rotation;
        Vector3 routeStart = Application.isPlaying
            ? fallbackStartPosition
            : transform.position;
        Vector3 routeEnd = routeStart + (fallbackOffsetIsLocal
            ? routeRotation * fallbackEndOffset
            : fallbackEndOffset);

        Gizmos.DrawLine(routeStart, routeEnd);
        Gizmos.DrawWireSphere(routeStart, 0.12f);
        Gizmos.DrawWireSphere(routeEnd, 0.12f);
    }
}
