using UnityEngine;

/// <summary>
/// 电影感环绕摄像机脚本
/// 功能：看向目标，在左右两侧之间循环摆动，并随机产生放大/缩小效果。
/// </summary>
public class LoopingCinematicCamera : MonoBehaviour
{
    [Header("追踪目标")]
    public Transform target;
    public Vector3 targetOffset = new Vector3(0, 1.5f, 0); // 看向目标的高度偏移（通常是头部高度）

    [Header("摆动设置")]
    public float distance = 4.0f;          // 距离目标的半径
    [Tooltip("调整这个角度来让相机面向正面 (通常 0 或 180)")]
    public float baseRotation = 180f;      // 基础角度偏移
    public float swingAngle = 30f;         // 左右摆动的最大角度
    public float swingSpeed = 0.5f;        // 摆动速度
    public float heightOffset = 1.4f;      // 相机相对于基础坐标的高度

    [Header("呼吸位移")]
    public float bobAmount = 0.08f;        // 上下位移幅度 (建议值较小，如 0.05-0.1)
    public float bobSpeed = 1.2f;          // 上下位移速度

    [Header("缩放设置")]
    public float baseFOV = 60f;            // 默认视野角度
    public float zoomFOV = 40f;            // 放大后的视野角度
    public float zoomChance = 0.3f;        // 每隔一段时间尝试放大的概率
    public float zoomCheckInterval = 3f;   // 检查是否放大的时间间隔
    public float zoomDuration = 2f;        // 放大持续时间
    public float lerpSpeed = 2f;           // 移动和缩放的平滑系数

    private float currentAngle;
    private float targetFOVValue;
    private float nextZoomCheckTime;
    private float stopZoomTime;
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        targetFOVValue = baseFOV;

        // 如果没有指定目标，尝试寻找 Tag 为 Player 的物体
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player) target = player.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 计算摆动位置
        // 将基础角度与 Sine 波结合实现循环
        currentAngle = baseRotation + Mathf.Sin(Time.time * swingSpeed) * swingAngle;

        // 加算呼吸感位移 (上下微调)
        float verticalBob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;

        // 计算圆周运动坐标
        Quaternion rotation = Quaternion.Euler(0, currentAngle, 0);
        Vector3 positionOffset = rotation * Vector3.forward * distance;

        // 目标位置
        Vector3 targetPosition = target.position + positionOffset + Vector3.up * (heightOffset + verticalBob);

        // 平滑移动到目标位置
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);

        // 2. 摄像机始终看向目标
        Vector3 lookAtTarget = target.position + targetOffset;
        Quaternion targetRotation = Quaternion.LookRotation(lookAtTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lerpSpeed);

        // 3. 随机缩放逻辑
        UpdateZoom();
    }

    void UpdateZoom()
    {
        if (cam == null) return;

        // 时间间隔检查是否需要放大
        if (Time.time > nextZoomCheckTime)
        {
            nextZoomCheckTime = Time.time + zoomCheckInterval;

            if (Random.value < zoomChance)
            {
                targetFOVValue = zoomFOV;
                stopZoomTime = Time.time + zoomDuration;
            }
        }

        // 放大时间结束后恢复
        if (targetFOVValue == zoomFOV && Time.time > stopZoomTime)
        {
            targetFOVValue = baseFOV;
        }

        // 平滑应用 FOV
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOVValue, Time.deltaTime * lerpSpeed);
    }
}
