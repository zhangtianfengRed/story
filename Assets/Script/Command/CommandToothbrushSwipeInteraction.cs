using System;
using UnityEngine;
using UnityEngine.Events;

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
    [Tooltip("实际左右滑动的物体。留空时滑动当前物体。")]
    public Transform slideTarget;

    [Header("Slide")]
    [Tooltip("本地空间里的滑动方向。默认 Local X。")]
    public Vector3 localSlideAxis = Vector3.right;
    [Min(0f)]
    [Tooltip("从初始位置到单侧端点的最大距离。")]
    public float slideRange = 0.08f;
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

        slideRange = Mathf.Max(0f, slideRange);
        mouseSensitivity = Mathf.Max(0f, mouseSensitivity);
        requiredSwipeCount = Mathf.Max(1, requiredSwipeCount);
    }

    private void Update()
    {
        if (isCompleted)
        {
            return;
        }

        if (isDragging)
        {
            UpdateDrag();
        }
    }

    public void BeginSwipe()
    {
        if (isCompleted || isDragging)
        {
            return;
        }

        if (mouseInteractable != null && !mouseInteractable.IsInteractable)
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
            -slideRange,
            slideRange);

        ApplySlideOffset();
        EvaluateSwipeEndpoint();
    }

    private void EvaluateSwipeEndpoint()
    {
        if (slideRange <= 0.0001f)
        {
            return;
        }

        float normalizedOffset = currentOffset / slideRange;
        int endpointSide = 0;
        if (normalizedOffset >= endpointThreshold)
        {
            endpointSide = 1;
        }
        else if (normalizedOffset <= -endpointThreshold)
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
