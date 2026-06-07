using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[AddComponentMenu("Command/Medicine Bottle Interaction")]
public class CommandMedicineBottleInteraction : MonoBehaviour
{
    [Header("References")]
    [Tooltip("这个玩法唯一的 CommandMouseInteractable。通常挂在 Root 上。")]
    public CommandMouseInteractable mouseInteractable;
    [Tooltip("启用后，本脚本会自动监听 mouseInteractable 的点击事件。只有 mouseInteractable 和药瓶属于同一玩法对象时才需要。")]
    public bool autoBindMouseInteractableClick = true;
    [Tooltip("这个玩法使用的独立摄像机。配置后会自动写到 CommandMouseInteractable.targetCamera。")]
    public Camera interactionCamera;
    [Tooltip("启用后，自动让 CommandMouseInteractable 使用 interactionCamera，而不是 Main Camera。")]
    public bool applyInteractionCameraToMouseInteractable = true;
    [Tooltip("药瓶玩法常在 UI/独立相机中操作。开启后，即使鼠标在 UI 上也允许 CommandMouseInteractable 射线检测。")]
    public bool allowInteractionWhenPointerOverUI = true;
    [Tooltip("药盖模型。点击命中的 Collider 如果属于这个 Transform 或其子物体，会被当作点击药盖。")]
    public Transform capTransform;
    [Tooltip("药瓶身子模型。点击命中的 Collider 如果属于这个 Transform 或其子物体，会被当作点击瓶身。")]
    public Transform bottleTransform;
    [Tooltip("拖动瓶身时实际旋转的对象。留空时使用当前 Root。")]
    public Transform rotationTarget;
    [Tooltip("倒药时实际倾斜的对象。留空时优先使用 bottleTransform。")]
    public Transform pourTarget;

    [Header("Direct Mouse Input")]
    [Tooltip("启用后，药瓶脚本自己使用 interactionCamera 做射线检测。适合入口 CommandMouseInteractable 只负责打开玩法，药瓶本身没有 CommandMouseInteractable 的情况。")]
    public bool enableDirectMouseInput = true;
    public LayerMask directRaycastLayers = ~0;
    [Min(0f)]
    public float directRaycastDistance = 1000f;
    public QueryTriggerInteraction directTriggerInteraction = QueryTriggerInteraction.Collide;
    [Tooltip("开启后，鼠标在 UI 上时不处理药瓶玩法点击。")]
    public bool directIgnoreWhenPointerOverUI;

    [Header("Cap Motion")]
    [Tooltip("初始未旋转时点击药盖，药盖小幅上移打开瓶子。")]
    public Vector3 capOpenLocalOffset = new Vector3(0f, 0.08f, 0f);
    [Tooltip("瓶子已经旋转后点击药盖，药盖飞离到这个本地偏移位置。")]
    public Vector3 capSeparatedLocalOffset = new Vector3(0f, 0.35f, 0f);
    [Min(0.01f)]
    public float capOpenDuration = 0.25f;
    [Min(0.01f)]
    public float capSeparateDuration = 0.45f;
    [Tooltip("药盖飞离时额外旋转多少度。")]
    public Vector3 capSeparateSpinEuler = new Vector3(0f, 180f, 35f);

    [Header("Bottle Drag Rotation")]
    [Tooltip("鼠标横向拖动时，瓶子绕本地 Y 轴旋转的速度。")]
    public float horizontalRotationSensitivity = 0.35f;
    [Tooltip("鼠标纵向拖动时，瓶子绕本地 Z 轴旋转的速度。")]
    public float verticalZRotationSensitivity = 0.35f;
    public bool clampDragRotation = true;
    [Min(0f)]
    public float maxHorizontalRotation = 70f;
    [Min(0f)]
    public float maxZRotation = 45f;
    [Min(0f)]
    [Tooltip("大于这个角度后，再点击药盖会触发复位并飞离。")]
    public float rotatedAngleThreshold = 2f;
    [Min(0.01f)]
    public float resetRotationDuration = 0.3f;

    [Header("Pour Motion")]
    [FormerlySerializedAs("preDragLocalOffset")]
    [Tooltip("触发倒药动作前，pourTarget 相对初始本地坐标先移动到的偏移。保持 0 不移动。")]
    public Vector3 prePourLocalOffset = Vector3.zero;
    [Min(0f)]
    [FormerlySerializedAs("preDragMoveDuration")]
    [Tooltip("倒药动作前位移到 prePourLocalOffset 的时长。0 表示立即到位。")]
    public float prePourMoveDuration = 0.2f;
    [Tooltip("药盖飞离后点击瓶身，瓶子斜倒到这个本地旋转角度。")]
    public Vector3 pourLocalEulerOffset = new Vector3(0f, 0f, -65f);
    [Min(0.01f)]
    public float pourDuration = 0.45f;

    [Header("Capsules")]
    [Tooltip("需要被倒出来的胶囊药。全部倒出后，这个玩法才算完成。")]
    public Transform[] capsuleTransforms;
    [Tooltip("可选：每颗胶囊倒出后的目标位置。数量不足时会使用自动偏移。")]
    public Transform[] capsulePourTargets;
    [Tooltip("倒出每颗胶囊时是否先激活它。适合胶囊初始隐藏在瓶子里，倒药时再显示。")]
    public bool activateCapsulesOnPour = true;
    [Tooltip("未配置目标点时，胶囊倒出后的基础本地偏移。")]
    public Vector3 capsulePourLocalOffset = new Vector3(0.18f, -0.08f, 0f);
    [Tooltip("未配置目标点时，每颗胶囊额外错开的本地偏移。")]
    public Vector3 capsuleSpreadStep = new Vector3(0.04f, 0f, 0.03f);
    [Tooltip("胶囊倒出时额外旋转多少度。")]
    public Vector3 capsulePourSpinEuler = new Vector3(0f, 0f, 220f);
    [Min(0.01f)]
    public float capsulePourDuration = 0.28f;
    [Min(0f)]
    public float capsulePourInterval = 0.05f;

    [Header("Completion")]
    [Tooltip("胶囊全部倒出后，是否调用唯一的 CommandMouseInteractable.CompleteInteraction()。")]
    public bool completeMouseInteractableOnFinished = true;

    [Header("Debug")]
    public bool logDebugMessages;

    [Header("Events")]
    public UnityEvent onCapOpened = new UnityEvent();
    public UnityEvent onBottleDragStarted = new UnityEvent();
    public UnityEvent onCapSeparated = new UnityEvent();
    public UnityEvent onBottlePoured = new UnityEvent();
    public UnityEvent onCapsulesPoured = new UnityEvent();
    public UnityEvent onCompleted = new UnityEvent();

    private Vector3 capInitialLocalPosition;
    private Quaternion capInitialLocalRotation;
    private Quaternion rotationInitialLocalRotation;
    private Vector3 pourInitialLocalPosition;
    private Quaternion pourInitialLocalRotation;
    private Vector3[] capsuleInitialLocalPositions;
    private Quaternion[] capsuleInitialLocalRotations;
    private bool[] capsuleInitialActiveStates;
    private Vector3 lastMousePosition;
    private Coroutine activeMotion;
    private float horizontalAngle;
    private float zAngle;
    private bool hasCapturedInitialState;
    private bool capOpened;
    private bool capSeparated;
    private bool bottlePoured;
    private bool capsulesPoured;
    private bool interactionCompleted;
    private bool isDraggingBottle;
    private bool prePourPositionApplied;

    private void Reset()
    {
        mouseInteractable = GetComponent<CommandMouseInteractable>();
        rotationTarget = transform;
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureInitialStateIfNeeded();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureInitialStateIfNeeded();
        SubscribeMouseInteractable();
    }

    private void OnDisable()
    {
        UnsubscribeMouseInteractable();
        isDraggingBottle = false;
    }

    private void OnValidate()
    {
        capOpenDuration = Mathf.Max(0.01f, capOpenDuration);
        capSeparateDuration = Mathf.Max(0.01f, capSeparateDuration);
        resetRotationDuration = Mathf.Max(0.01f, resetRotationDuration);
        prePourMoveDuration = Mathf.Max(0f, prePourMoveDuration);
        pourDuration = Mathf.Max(0.01f, pourDuration);
        capsulePourDuration = Mathf.Max(0.01f, capsulePourDuration);
        capsulePourInterval = Mathf.Max(0f, capsulePourInterval);
        maxHorizontalRotation = Mathf.Max(0f, maxHorizontalRotation);
        maxZRotation = Mathf.Max(0f, maxZRotation);
        rotatedAngleThreshold = Mathf.Max(0f, rotatedAngleThreshold);
        directRaycastDistance = Mathf.Max(0f, directRaycastDistance);
    }

    private void Update()
    {
        if (enableDirectMouseInput && Input.GetMouseButtonDown(0))
        {
            HandleDirectMouseDown();
        }

        if (isDraggingBottle)
        {
            UpdateBottleDrag();
        }
    }

    // Bind this to CommandMouseInteractable.onClick when you do not need the hit collider parameter.
    public void OnClicked()
    {
        Collider hitCollider = mouseInteractable != null ? mouseInteractable.CurrentHitCollider : null;
        OnClickedCollider(hitCollider);
    }

    // Preferred binding: CommandMouseInteractable.onClickCollider -> OnClickedCollider(Collider).
    public void OnClickedCollider(Collider hitCollider)
    {
        LogDebug(hitCollider != null
            ? $"Clicked collider: {hitCollider.name}"
            : "Clicked with no collider. Falling back to bottle click.");

        if (interactionCompleted || activeMotion != null)
        {
            LogDebug("Click ignored because interaction is completed or an animation is playing.");
            return;
        }

        if (IsColliderInTransform(hitCollider, capTransform))
        {
            LogDebug("Click resolved as cap.");
            HandleCapClicked();
            return;
        }

        if (hitCollider == null ||
            bottleTransform == null ||
            IsColliderInTransform(hitCollider, bottleTransform) ||
            hitCollider.transform == transform ||
            hitCollider.transform.IsChildOf(transform))
        {
            LogDebug("Click resolved as bottle.");
            HandleBottleClicked();
            return;
        }

        LogDebug("Click did not match cap or bottle transforms.");
    }

    public void ResetInteraction()
    {
        StopActiveMotion();
        ResolveReferences();
        CaptureInitialStateIfNeeded();

        isDraggingBottle = false;
        capOpened = false;
        capSeparated = false;
        bottlePoured = false;
        capsulesPoured = false;
        interactionCompleted = false;
        horizontalAngle = 0f;
        zAngle = 0f;
        prePourPositionApplied = false;

        if (capTransform != null)
        {
            capTransform.localPosition = capInitialLocalPosition;
            capTransform.localRotation = capInitialLocalRotation;
        }

        Transform resolvedRotationTarget = ResolveRotationTarget();
        if (resolvedRotationTarget != null)
        {
            resolvedRotationTarget.localRotation = rotationInitialLocalRotation;
        }

        Transform resolvedPourTarget = ResolvePourTarget();
        if (resolvedPourTarget != null)
        {
            resolvedPourTarget.localPosition = pourInitialLocalPosition;
            resolvedPourTarget.localRotation = pourInitialLocalRotation;
        }

        RestoreCapsules();

        if (mouseInteractable != null)
        {
            mouseInteractable.ResetCompletion();
        }
    }

    private void HandleCapClicked()
    {
        if (capTransform == null || capSeparated)
        {
            return;
        }

        CaptureInitialStateIfNeeded();

        if (HasBottleRotation())
        {
            isDraggingBottle = false;
            activeMotion = StartCoroutine(ResetRotationAndSeparateCapRoutine());
            return;
        }

        activeMotion = StartCoroutine(OpenCapRoutine());
    }

    private void HandleBottleClicked()
    {
        if (bottlePoured)
        {
            return;
        }

        CaptureInitialStateIfNeeded();

        if (capSeparated)
        {
            isDraggingBottle = false;
            activeMotion = StartCoroutine(PourBottleAndCapsulesRoutine());
            return;
        }

        if (capOpened)
        {
            isDraggingBottle = false;
            LogDebug("Bottle drag ignored because cap is already opened.");
            return;
        }

        BeginBottleDrag();
    }

    private void HandleDirectMouseDown()
    {
        if (directIgnoreWhenPointerOverUI &&
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            LogDebug("Direct click ignored because pointer is over UI.");
            return;
        }

        if (!TryGetDirectHitCollider(out Collider hitCollider))
        {
            LogDebug("Direct click did not hit cap or bottle collider.");
            return;
        }

        OnClickedCollider(hitCollider);
    }

    private bool TryGetDirectHitCollider(out Collider hitCollider)
    {
        hitCollider = null;

        Camera rayCamera = ResolveInteractionCamera();
        if (rayCamera == null)
        {
            LogDebug("Direct raycast has no camera. Assign interactionCamera or set a Main Camera.");
            return false;
        }

        Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, directRaycastDistance, directRaycastLayers, directTriggerInteraction))
        {
            return false;
        }

        if (!IsColliderInMedicineBottle(hit.collider))
        {
            LogDebug($"Direct raycast hit {hit.collider.name}, but it is not under cap/bottle/root.");
            return false;
        }

        hitCollider = hit.collider;
        return true;
    }

    private void BeginBottleDrag()
    {
        if (capOpened && !capSeparated)
        {
            isDraggingBottle = false;
            LogDebug("Bottle drag blocked because cap is already opened.");
            return;
        }

        isDraggingBottle = true;
        lastMousePosition = Input.mousePosition;
        LogDebug("Bottle drag started.");
        onBottleDragStarted.Invoke();
    }

    private void UpdateBottleDrag()
    {
        if (capOpened && !capSeparated)
        {
            LogDebug("Bottle drag stopped because cap is already opened.");
            isDraggingBottle = false;
            return;
        }

        if (!Input.GetMouseButton(0))
        {
            LogDebug("Bottle drag stopped because mouse button is up.");
            isDraggingBottle = false;
            return;
        }

        Transform target = ResolveRotationTarget();
        if (target == null)
        {
            LogDebug("Bottle drag stopped because rotation target is missing.");
            isDraggingBottle = false;
            return;
        }

        Vector3 mousePosition = Input.mousePosition;
        Vector3 delta = mousePosition - lastMousePosition;
        lastMousePosition = mousePosition;

        horizontalAngle += delta.x * horizontalRotationSensitivity;
        zAngle += delta.y * verticalZRotationSensitivity;

        if (clampDragRotation)
        {
            horizontalAngle = Mathf.Clamp(horizontalAngle, -maxHorizontalRotation, maxHorizontalRotation);
            zAngle = Mathf.Clamp(zAngle, -maxZRotation, maxZRotation);
        }

        ApplyBottleDragRotation();
    }

    private void ApplyBottleDragRotation()
    {
        Transform target = ResolveRotationTarget();
        if (target == null)
        {
            return;
        }

        Quaternion dragRotation =
            Quaternion.AngleAxis(horizontalAngle, Vector3.up) *
            Quaternion.AngleAxis(zAngle, Vector3.forward);

        target.localRotation = rotationInitialLocalRotation * dragRotation;
    }

    private IEnumerator OpenCapRoutine()
    {
        if (capOpened)
        {
            activeMotion = null;
            yield break;
        }

        Vector3 startPosition = capTransform.localPosition;
        Vector3 targetPosition = capInitialLocalPosition + capOpenLocalOffset;

        yield return AnimateTransform(
            capTransform,
            startPosition,
            capTransform.localRotation,
            targetPosition,
            capTransform.localRotation,
            capOpenDuration);

        capOpened = true;
        isDraggingBottle = false;
        onCapOpened.Invoke();
        activeMotion = null;
    }

    private IEnumerator ResetRotationAndSeparateCapRoutine()
    {
        Transform target = ResolveRotationTarget();
        if (target != null)
        {
            Quaternion startRotation = target.localRotation;
            yield return AnimateLocalRotation(target, startRotation, rotationInitialLocalRotation, resetRotationDuration);
        }

        horizontalAngle = 0f;
        zAngle = 0f;

        Vector3 startPosition = capTransform.localPosition;
        Quaternion startCapRotation = capTransform.localRotation;
        Vector3 targetPosition = capInitialLocalPosition + capSeparatedLocalOffset;
        Quaternion targetCapRotation = capInitialLocalRotation * Quaternion.Euler(capSeparateSpinEuler);

        yield return AnimateTransform(
            capTransform,
            startPosition,
            startCapRotation,
            targetPosition,
            targetCapRotation,
            capSeparateDuration);

        capOpened = true;
        capSeparated = true;
        onCapSeparated.Invoke();
        activeMotion = null;
    }

    private bool ShouldApplyPrePourMove(Transform target)
    {
        if (target == null ||
            prePourPositionApplied ||
            prePourLocalOffset.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        return true;
    }

    private IEnumerator PourBottleAndCapsulesRoutine()
    {
        Transform target = ResolvePourTarget();
        if (ShouldApplyPrePourMove(target))
        {
            Vector3 startPosition = target.localPosition;
            Vector3 targetPosition = pourInitialLocalPosition + prePourLocalOffset;

            yield return AnimateTransform(
                target,
                startPosition,
                target.localRotation,
                targetPosition,
                target.localRotation,
                prePourMoveDuration);

            prePourPositionApplied = true;
        }

        if (target != null)
        {
            Quaternion startRotation = target.localRotation;
            Quaternion targetRotation = pourInitialLocalRotation * Quaternion.Euler(pourLocalEulerOffset);
            yield return AnimateLocalRotation(target, startRotation, targetRotation, pourDuration);
        }

        bottlePoured = true;
        onBottlePoured.Invoke();

        if (CountCapsules() == 0)
        {
            Debug.LogWarning("[CommandMedicineBottleInteraction] No capsules assigned. Completion is blocked until capsuleTransforms is configured.", this);
            activeMotion = null;
            yield break;
        }

        yield return PourCapsulesRoutine();

        capsulesPoured = true;
        onCapsulesPoured.Invoke();
        CompleteWholeInteraction();
        activeMotion = null;
    }

    private IEnumerator PourCapsulesRoutine()
    {
        for (int i = 0; i < capsuleTransforms.Length; i++)
        {
            Transform capsule = capsuleTransforms[i];
            if (capsule == null)
            {
                continue;
            }

            if (activateCapsulesOnPour)
            {
                capsule.gameObject.SetActive(true);
            }

            Vector3 startPosition = capsule.localPosition;
            Quaternion startRotation = capsule.localRotation;
            Vector3 targetPosition = ResolveCapsuleTargetPosition(i, capsule);
            Quaternion targetRotation = ResolveCapsuleTargetRotation(i, capsule);

            yield return AnimateTransform(
                capsule,
                startPosition,
                startRotation,
                targetPosition,
                targetRotation,
                capsulePourDuration);

            if (capsulePourInterval > 0f)
            {
                yield return new WaitForSeconds(capsulePourInterval);
            }
        }
    }

    private void CompleteWholeInteraction()
    {
        if (interactionCompleted)
        {
            return;
        }

        interactionCompleted = true;
        onCompleted.Invoke();

        if (completeMouseInteractableOnFinished && mouseInteractable != null)
        {
            mouseInteractable.CompleteInteraction();
        }
    }

    private IEnumerator AnimateTransform(
        Transform target,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 targetPosition,
        Quaternion targetRotation,
        float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Smooth01(elapsed / duration);
            target.localPosition = Vector3.LerpUnclamped(startPosition, targetPosition, t);
            target.localRotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, t);
            yield return null;
        }

        target.localPosition = targetPosition;
        target.localRotation = targetRotation;
    }

    private IEnumerator AnimateLocalRotation(
        Transform target,
        Quaternion startRotation,
        Quaternion targetRotation,
        float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Smooth01(elapsed / duration);
            target.localRotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, t);
            yield return null;
        }

        target.localRotation = targetRotation;
    }

    private bool HasBottleRotation()
    {
        Transform target = ResolveRotationTarget();
        if (target == null)
        {
            return Mathf.Abs(horizontalAngle) + Mathf.Abs(zAngle) > rotatedAngleThreshold;
        }

        float currentAngle = Quaternion.Angle(rotationInitialLocalRotation, target.localRotation);
        return currentAngle > rotatedAngleThreshold ||
               Mathf.Abs(horizontalAngle) + Mathf.Abs(zAngle) > rotatedAngleThreshold;
    }

    private Vector3 ResolveCapsuleTargetPosition(int index, Transform capsule)
    {
        if (capsulePourTargets != null &&
            index < capsulePourTargets.Length &&
            capsulePourTargets[index] != null)
        {
            if (capsule.parent == null)
            {
                return capsulePourTargets[index].position;
            }

            return capsule.parent == capsulePourTargets[index].parent
                ? capsulePourTargets[index].localPosition
                : capsule.parent.InverseTransformPoint(capsulePourTargets[index].position);
        }

        Vector3 basePosition = GetCapsuleInitialLocalPosition(index, capsule);
        return basePosition + capsulePourLocalOffset + capsuleSpreadStep * index;
    }

    private Quaternion ResolveCapsuleTargetRotation(int index, Transform capsule)
    {
        if (capsulePourTargets != null &&
            index < capsulePourTargets.Length &&
            capsulePourTargets[index] != null)
        {
            if (capsule.parent == null)
            {
                return capsulePourTargets[index].rotation;
            }

            return capsule.parent == capsulePourTargets[index].parent
                ? capsulePourTargets[index].localRotation
                : Quaternion.Inverse(capsule.parent.rotation) * capsulePourTargets[index].rotation;
        }

        Quaternion baseRotation = GetCapsuleInitialLocalRotation(index, capsule);
        return baseRotation * Quaternion.Euler(capsulePourSpinEuler);
    }

    private Vector3 GetCapsuleInitialLocalPosition(int index, Transform capsule)
    {
        if (capsuleInitialLocalPositions != null && index < capsuleInitialLocalPositions.Length)
        {
            return capsuleInitialLocalPositions[index];
        }

        return capsule.localPosition;
    }

    private Quaternion GetCapsuleInitialLocalRotation(int index, Transform capsule)
    {
        if (capsuleInitialLocalRotations != null && index < capsuleInitialLocalRotations.Length)
        {
            return capsuleInitialLocalRotations[index];
        }

        return capsule.localRotation;
    }

    private int CountCapsules()
    {
        if (capsuleTransforms == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < capsuleTransforms.Length; i++)
        {
            if (capsuleTransforms[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private void RestoreCapsules()
    {
        if (capsuleTransforms == null)
        {
            return;
        }

        for (int i = 0; i < capsuleTransforms.Length; i++)
        {
            Transform capsule = capsuleTransforms[i];
            if (capsule == null)
            {
                continue;
            }

            if (capsuleInitialLocalPositions != null && i < capsuleInitialLocalPositions.Length)
            {
                capsule.localPosition = capsuleInitialLocalPositions[i];
            }

            if (capsuleInitialLocalRotations != null && i < capsuleInitialLocalRotations.Length)
            {
                capsule.localRotation = capsuleInitialLocalRotations[i];
            }

            if (capsuleInitialActiveStates != null && i < capsuleInitialActiveStates.Length)
            {
                capsule.gameObject.SetActive(capsuleInitialActiveStates[i]);
            }
        }
    }

    private void ResolveReferences()
    {
        if (mouseInteractable == null)
        {
            mouseInteractable = GetComponent<CommandMouseInteractable>();
        }

        ApplyMouseInteractableSettings();

        if (rotationTarget == null)
        {
            rotationTarget = transform;
        }
    }

    private void SubscribeMouseInteractable()
    {
        if (!autoBindMouseInteractableClick || mouseInteractable == null)
        {
            return;
        }

        mouseInteractable.onClickCollider.RemoveListener(OnClickedCollider);
        mouseInteractable.onClickCollider.AddListener(OnClickedCollider);
    }

    private void UnsubscribeMouseInteractable()
    {
        if (mouseInteractable != null)
        {
            mouseInteractable.onClickCollider.RemoveListener(OnClickedCollider);
        }
    }

    private void ApplyMouseInteractableSettings()
    {
        if (mouseInteractable == null)
        {
            return;
        }

        mouseInteractable.ignoreWhenPointerOverUI = !allowInteractionWhenPointerOverUI;

        if (applyInteractionCameraToMouseInteractable && interactionCamera != null)
        {
            mouseInteractable.targetCamera = interactionCamera;
            mouseInteractable.autoUseMainCamera = false;
        }
    }

    private void LogDebug(string message)
    {
        if (logDebugMessages)
        {
            Debug.Log($"[CommandMedicineBottleInteraction] {message}", this);
        }
    }

    private Camera ResolveInteractionCamera()
    {
        if (interactionCamera != null)
        {
            return interactionCamera;
        }

        if (mouseInteractable != null && mouseInteractable.targetCamera != null)
        {
            return mouseInteractable.targetCamera;
        }

        return Camera.main;
    }

    private Transform ResolveRotationTarget()
    {
        return rotationTarget != null ? rotationTarget : transform;
    }

    private Transform ResolvePourTarget()
    {
        if (pourTarget != null)
        {
            return pourTarget;
        }

        if (bottleTransform != null)
        {
            return bottleTransform;
        }

        return ResolveRotationTarget();
    }

    private void CaptureInitialStateIfNeeded()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        ResolveReferences();

        if (capTransform != null)
        {
            capInitialLocalPosition = capTransform.localPosition;
            capInitialLocalRotation = capTransform.localRotation;
        }

        Transform resolvedRotationTarget = ResolveRotationTarget();
        if (resolvedRotationTarget != null)
        {
            rotationInitialLocalRotation = resolvedRotationTarget.localRotation;
        }

        Transform resolvedPourTarget = ResolvePourTarget();
        if (resolvedPourTarget != null)
        {
            pourInitialLocalPosition = resolvedPourTarget.localPosition;
            pourInitialLocalRotation = resolvedPourTarget.localRotation;
        }

        CaptureCapsuleInitialState();
        hasCapturedInitialState = true;
    }

    private void CaptureCapsuleInitialState()
    {
        int length = capsuleTransforms != null ? capsuleTransforms.Length : 0;
        capsuleInitialLocalPositions = new Vector3[length];
        capsuleInitialLocalRotations = new Quaternion[length];
        capsuleInitialActiveStates = new bool[length];

        for (int i = 0; i < length; i++)
        {
            Transform capsule = capsuleTransforms[i];
            if (capsule == null)
            {
                capsuleInitialLocalRotations[i] = Quaternion.identity;
                continue;
            }

            capsuleInitialLocalPositions[i] = capsule.localPosition;
            capsuleInitialLocalRotations[i] = capsule.localRotation;
            capsuleInitialActiveStates[i] = capsule.gameObject.activeSelf;
        }
    }

    private void StopActiveMotion()
    {
        if (activeMotion == null)
        {
            return;
        }

        StopCoroutine(activeMotion);
        activeMotion = null;
    }

    private static bool IsColliderInTransform(Collider hitCollider, Transform target)
    {
        if (hitCollider == null || target == null)
        {
            return false;
        }

        Transform hitTransform = hitCollider.transform;
        return hitTransform == target || hitTransform.IsChildOf(target);
    }

    private bool IsColliderInMedicineBottle(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return false;
        }

        Transform hitTransform = hitCollider.transform;
        return IsColliderInTransform(hitCollider, capTransform) ||
               IsColliderInTransform(hitCollider, bottleTransform) ||
               hitTransform == transform ||
               hitTransform.IsChildOf(transform);
    }

    private static float Smooth01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }
}
