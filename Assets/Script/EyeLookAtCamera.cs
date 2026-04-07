using UnityEngine;

/// <summary>
/// 自动眼神追踪脚本（依赖头部姿态版本）。
/// 自动根据头部朝向和目标位置计算眼神，
/// 当头部离目标越远，会自动形成“偏头偷瞄”的效果。
/// </summary>
public class EyeLookAtCamera : MonoBehaviour
{
    [Header("核心引用")]
    [Tooltip("必须要指定头部骨骼，它是眼神转动的基准中心")]
    public Transform head;
    public Transform leftEye;
    public Transform rightEye;
    public Transform target;       // 追踪目标 (留空则默认为主相机)

    [Header("追踪设置")]
    public bool useHeadConstraint = true;           // 是否启用头部角度限制 (关掉它，眼球会死死跟着目标)
    [Range(0, 1)] public float weight = 1.0f;        // 脚本整体开关/权重
    public float maxAngle = 35f;                    // 眼珠相对于头部的最大偏转角
    public float trackingSpeed = 10f;               // 追踪平滑速度

    [Header("偏差校正")]
    [Tooltip("核心：调整这个，直到 Scene 窗口里的绿线指向你的相机")]
    public Vector3 rotationOffset = Vector3.zero;
    [Tooltip("头部校正。如果关掉约束后眼球正常，开启后却不正常，调这个")]
    public Vector3 headRotationOffset = Vector3.zero;

    [Header("调试工具")]
    public bool showDebugLines = true;
    private string lastLoggedTarget = "";

    void LateUpdate()
    {
        if (leftEye == null || rightEye == null) return;
        if (weight <= 0) return;

        // 获取目标位置
        Transform lookTarget = target != null ? target : (Camera.main ? Camera.main.transform : null);

        if (lookTarget == null) return;

        Vector3 targetPos = lookTarget.position;
        UpdateEye(leftEye, targetPos);
        UpdateEye(rightEye, targetPos);
    }

    void UpdateEye(Transform eye, Vector3 targetWorldPos)
    {
        if (!eye) return;

        // 1. 计算看向目标的全局旋转
        Vector3 targetDir = (targetWorldPos - eye.position).normalized;

        // 核心：基于目标方向计算旋转，并加上偏移修正
        Quaternion lookRot = Quaternion.LookRotation(targetDir) * Quaternion.Euler(rotationOffset);

        // 2. 角度限制逻辑
        Quaternion finalTargetRot;
        if (useHeadConstraint && head != null)
        {
            Quaternion headBaseRot = head.rotation * Quaternion.Euler(headRotationOffset);
            finalTargetRot = Quaternion.RotateTowards(headBaseRot, lookRot, maxAngle);
        }
        else
        {
            finalTargetRot = lookRot;
        }

        // 3. 调试射线
        if (showDebugLines)
        {
            Debug.DrawRay(eye.position, targetDir * 2f, Color.yellow);           // 黄色：理想目标线
            Debug.DrawRay(eye.position, (finalTargetRot * Vector3.forward) * 2f, Color.green); // 绿色：脚本计算出的指向
            Debug.DrawRay(eye.position, eye.forward * 1.5f, Color.red);          // 红色：骨骼当前真正的 Z 轴
        }

        // 4. 应用平滑插值 (如果想瞬间测试，可以关掉 Slerp 设为直接赋值)
        eye.rotation = Quaternion.Slerp(eye.rotation, finalTargetRot, Time.deltaTime * trackingSpeed * weight);
    }
}
