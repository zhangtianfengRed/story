using UnityEngine;

/// <summary>
/// 控制 3D 角色使用 A/D 键左右移动，直接操作 Transform.position，
/// 并通过 Animator 的 Walk bool 驱动行走动画。
/// </summary>
[RequireComponent(typeof(Animator))]
public class ADCharacterController : MonoBehaviour
{
    [Header("移动设置")]
    [Tooltip("角色水平移动速度")]
    public float moveSpeed = 5f;

    [Header("动画设置")]
    [Tooltip("Animator 中控制行走状态的 Bool 参数名称")]
    public string walkBoolName = "Walk";

    [Header("翻转设置")]
    [Tooltip("根据移动方向自动翻转角色朝向（沿 Y 轴旋转 180°）")]
    public bool flipOnMove = true;
    [Tooltip("翻转时朝右的 Y 轴角度")]
    public float faceRightY = 90f;
    [Tooltip("翻转时朝左的 Y 轴角度")]
    public float faceLeftY = -90f;

    private Animator _animator;

    // 外部可关闭移动控制（触发器等使用）
    [HideInInspector] public bool canMove = true;

    // 记录上一帧的有效方向（避免切换方向时单帧 0 导致动画停顿）
    private float _lastDirection = 1f;
    // 是否正在按键（A 或 D 至少有一个被按下）
    private bool _isMoving;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Update()
    {
        // 被外部禁用时：只阻止移动，不干涉 Animator（避免覆盖 Fail 等外部动画状态）
        if (!canMove)
        {
            _animator.SetBool(walkBoolName, false); // 确保 Walk 关闭，但不 return，让外部状态机自行处理
            return;
        }

        // 分别检测 A/D 键是否按住，避免切换方向时单帧输入为 0 的问题
        bool pressingRight = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        bool pressingLeft  = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);

        _isMoving = pressingRight || pressingLeft;

        // 计算实际方向（同时按下视为右）
        float direction = 0f;
        if (pressingRight) direction += 1f;
        if (pressingLeft)  direction -= 1f;

        if (direction != 0f)
        {
            _lastDirection = direction;

            // 移动 position（沿 X 轴）
            transform.position += new Vector3(direction, 0f, 0f) * moveSpeed * Time.deltaTime;

            // 翻转朝向（只在方向真正改变时更新，避免每帧写 eulerAngles）
            if (flipOnMove)
            {
                float targetY = direction > 0f ? faceRightY : faceLeftY;
                Vector3 euler = transform.eulerAngles;
                // 规范化后比较，避免浮点误差导致每帧赋值
                if (!Mathf.Approximately(Mathf.DeltaAngle(euler.y, targetY), 0f))
                {
                    euler.y = targetY;
                    transform.eulerAngles = euler;
                }
            }
        }

        // Walk bool 由是否按键决定，与方向计算分离，不受单帧 0 影响
        _animator.SetBool(walkBoolName, _isMoving);
    }
}
