using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

[Serializable]
public class CommandIntEvent : UnityEvent<int>
{
}

[DisallowMultipleComponent]
[AddComponentMenu("Command/Toothbrush Swipe Interaction")]
public class CommandToothbrushSwipeInteraction : MonoBehaviour
{
    [Header("References")]
    [Tooltip("负责鼠标射线检测和高亮的组件。留空时会从当前物体自动获取。")]
    public CommandMouseInteractable mouseInteractable;
    [Tooltip("这个牙刷玩法使用的独立摄像机。只用于牙刷脚本自己的点击检测，不会覆盖 CommandMouseInteractable.targetCamera。")]
    public Camera interactionCamera;
    [Tooltip("实际左右滑动的物体。留空时滑动当前物体。")]
    public Transform slideTarget;

    [Header("Direct Input")]
    [Tooltip("牙刷玩法常在独立相机或 UI 上层中操作。开启后，即使鼠标在 UI 上也允许点击和滑动。")]
    public bool allowInteractionWhenPointerOverUI = true;
    public LayerMask directRaycastLayers = ~0;
    [Min(0f)]
    public float directRaycastDistance = 1000f;
    public QueryTriggerInteraction directTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Slide")]
    [Tooltip("本地空间里的滑动方向。默认 Local X。")]
    public Vector3 localSlideAxis = Vector3.right;
    [Min(0f)]
    [FormerlySerializedAs("slideRange")]
    [Tooltip("鼠标向左拖动时，从初始位置到左侧端点的最大偏移。")]
    public float leftSlideRange = 0.08f;
    [Min(0f)]
    [Tooltip("鼠标向右拖动时，从初始位置到右侧端点的最大偏移。")]
    public float rightSlideRange = 0.08f;
    [Min(0f)]
    [Tooltip("鼠标横向拖动转换到模型位移的倍率。")]
    public float mouseSensitivity = 0.0025f;
    [Range(0.1f, 1f)]
    [Tooltip("滑到单侧范围的多少比例后，算碰到这一侧端点。")]
    public float endpointThreshold = 0.82f;
    [Min(1)]
    [Tooltip("一次计数等于从一侧端点滑到另一侧端点。")]
    public int requiredSwipeCount = 4;

    [Header("Completion")]
    public bool completeCommandInteractable = true;
    [Tooltip("牙刷玩法通过后隐藏的对象。")]
    public GameObject[] objectsToHideOnCompleted;
    [Tooltip("牙刷玩法通过后显示的对象。")]
    public GameObject[] objectsToShowOnCompleted;

    [Header("Events")]
    public UnityEvent onSwipeStarted = new UnityEvent();
    public CommandIntEvent onSwipeCountChanged = new CommandIntEvent();
    public UnityEvent onCompleted = new UnityEvent();

    private Vector3 initialLocalPosition;
    private Vector3 lastMousePosition;
    private float currentOffset;
    private int lastEndpointSide;
    private int completedSwipeCount;
    private bool hasInitialPosition;
    private bool isDragging;
    private bool isCompleted;

    public int CompletedSwipeCount
    {
        get { return completedSwipeCount; }
    }

    public bool IsCompleted
    {
        get { return isCompleted; }
    }

    private void Reset()
    {
        mouseInteractable = GetComponent<CommandMouseInteractable>();
        slideTarget = transform;
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureInitialPositionIfNeeded();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureInitialPositionIfNeeded();
        SubscribeMouseInteractable();
    }

    private void OnDisable()
    {
        UnsubscribeMouseInteractable();
        isDragging = false;
    }

    private void OnValidate()
    {
        if (localSlideAxis.sqrMagnitude < 0.0001f)
        {
            localSlideAxis = Vector3.right;
        }

        leftSlideRange = Mathf.Max(0f, leftSlideRange);
        rightSlideRange = Mathf.Max(0f, rightSlideRange);
        mouseSensitivity = Mathf.Max(0f, mouseSensitivity);
        requiredSwipeCount = Mathf.Max(1, requiredSwipeCount);
        directRaycastDistance = Mathf.Max(0f, directRaycastDistance);
    }

    private void Update()
    {
        if (isCompleted)
        {
            return;
        }

        Collider hitCollider;
        if (interactionCamera != null &&
            !isDragging &&
            Input.GetMouseButtonDown(0) &&
            TryGetInteractionCameraHitCollider(out hitCollider))
        {
            BeginSwipeFromDirectInput();
        }

        if (isDragging)
        {
            UpdateDrag();
        }
    }

    public void BeginSwipe()
    {
        BeginSwipe(false);
    }

    private void BeginSwipeFromDirectInput()
    {
        BeginSwipe(true);
    }

    private void BeginSwipe(bool ignoreMouseInteractableGate)
    {
        if (isCompleted || isDragging)
        {
            return;
        }

        if (!ignoreMouseInteractableGate &&
            mouseInteractable != null &&
            !mouseInteractable.IsInteractable)
        {
            return;
        }

        CaptureInitialPositionIfNeeded();
        isDragging = true;
        lastMousePosition = Input.mousePosition;
        onSwipeStarted.Invoke();
    }

    public void CompleteInteraction()
    {
        if (isCompleted)
        {
            return;
        }

        isCompleted = true;
        isDragging = false;

        SetGameObjectsActive(objectsToHideOnCompleted, false);
        SetGameObjectsActive(objectsToShowOnCompleted, true);
        onCompleted.Invoke();

        if (completeCommandInteractable && mouseInteractable != null)
        {
            mouseInteractable.CompleteInteraction();
        }
    }

    public void ResetInteraction()
    {
        isCompleted = false;
        isDragging = false;
        completedSwipeCount = 0;
        lastEndpointSide = 0;
        currentOffset = 0f;
        ApplySlideOffset();
        onSwipeCountChanged.Invoke(completedSwipeCount);

        if (mouseInteractable != null)
        {
            mouseInteractable.ResetCompletion();
        }
    }

    private void UpdateDrag()
    {
        if (!Input.GetMouseButton(0))
        {
            isDragging = false;
            return;
        }

        Vector3 mousePosition = Input.mousePosition;
        float deltaX = mousePosition.x - lastMousePosition.x;
        lastMousePosition = mousePosition;

        currentOffset = Mathf.Clamp(
            currentOffset + deltaX * mouseSensitivity,
            -leftSlideRange,
            rightSlideRange);

        ApplySlideOffset();
        EvaluateSwipeEndpoint();
    }

    private bool TryGetInteractionCameraHitCollider(out Collider hitCollider)
    {
        hitCollider = null;

        if (ShouldIgnorePointerOverUI())
        {
            return false;
        }

        Ray ray = interactionCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, directRaycastDistance, directRaycastLayers, directTriggerInteraction))
        {
            return false;
        }

        if (!IsSwipeTargetCollider(hit.collider))
        {
            return false;
        }

        hitCollider = hit.collider;
        return true;
    }

    private bool ShouldIgnorePointerOverUI()
    {
        return !allowInteractionWhenPointerOverUI &&
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsSwipeTargetCollider(Collider hitCollider)
    {
        return IsColliderUnderRoot(hitCollider, mouseInteractable != null ? mouseInteractable.transform : null) ||
            IsColliderUnderRoot(hitCollider, transform) ||
            IsColliderUnderRoot(hitCollider, slideTarget);
    }

    private static bool IsColliderUnderRoot(Collider hitCollider, Transform root)
    {
        if (hitCollider == null || root == null)
        {
            return false;
        }

        Transform hitTransform = hitCollider.transform;
        return hitTransform == root || hitTransform.IsChildOf(root);
    }

    private void EvaluateSwipeEndpoint()
    {
        bool canReachLeftEndpoint = leftSlideRange > 0.0001f;
        bool canReachRightEndpoint = rightSlideRange > 0.0001f;
        if (!canReachLeftEndpoint && !canReachRightEndpoint)
        {
            return;
        }

        int endpointSide = 0;
        if (canReachRightEndpoint &&
            currentOffset / rightSlideRange >= endpointThreshold)
        {
            endpointSide = 1;
        }
        else if (canReachLeftEndpoint &&
            currentOffset / leftSlideRange <= -endpointThreshold)
        {
            endpointSide = -1;
        }

        if (endpointSide == 0 || endpointSide == lastEndpointSide)
        {
            return;
        }

        if (lastEndpointSide != 0)
        {
            completedSwipeCount++;
            onSwipeCountChanged.Invoke(completedSwipeCount);

            if (completedSwipeCount >= requiredSwipeCount)
            {
                CompleteInteraction();
                return;
            }
        }

        lastEndpointSide = endpointSide;
    }

    private void ApplySlideOffset()
    {
        Transform target = slideTarget != null ? slideTarget : transform;
        Vector3 axis = localSlideAxis.normalized;
        target.localPosition = initialLocalPosition + axis * currentOffset;
    }

    private void ResolveReferences()
    {
        if (mouseInteractable == null)
        {
            mouseInteractable = GetComponent<CommandMouseInteractable>();
        }

        if (slideTarget == null)
        {
            slideTarget = transform;
        }
    }

    private void CaptureInitialPositionIfNeeded()
    {
        if (hasInitialPosition)
        {
            return;
        }

        Transform target = slideTarget != null ? slideTarget : transform;
        initialLocalPosition = target.localPosition;
        hasInitialPosition = true;
    }

    private void SubscribeMouseInteractable()
    {
        if (mouseInteractable != null)
        {
            mouseInteractable.onClick.RemoveListener(BeginSwipe);
            mouseInteractable.onClick.AddListener(BeginSwipe);
        }
    }

    private void UnsubscribeMouseInteractable()
    {
        if (mouseInteractable != null)
        {
            mouseInteractable.onClick.RemoveListener(BeginSwipe);
        }
    }

    private static void SetGameObjectsActive(GameObject[] targets, bool active)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                targets[i].SetActive(active);
            }
        }
    }
}
