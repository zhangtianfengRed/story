using System.Collections;
using UnityEngine;

/// <summary>
/// 触发区域脚本：
///   1. 角色进入后立即 → Animator.Fail = true，禁止移动
///   2. 延迟 0.3 秒后   → Quad 上 MemoryBarrierDistortion 材质的 _GlobalAlpha 设为 1
/// 
/// 使用方法：
///   - 挂载到一个带 Collider（Is Trigger = true）的 GameObject 上
///   - 将场景中的 Quad（含 MemoryBarrierDistortion 材质）拖入 barrierQuad 字段
///   - Tag 过滤：默认检测 "Player" Tag，可在 Inspector 修改
/// </summary>
public class FailTrigger : MonoBehaviour
{
    [Header("目标引用")]
    [Tooltip("场景中挂有 MemoryBarrierDistortion 材质的 Quad")]
    public Renderer barrierQuad;

    [Header("过滤设置")]
    [Tooltip("只对该 Tag 的角色触发")]
    public string playerTag = "Player";

    [Header("时间设置")]
    [Tooltip("进入触发器后多少秒激活 Barrier（_GlobalAlpha → 1）")]
    public float barrierDelay = 0.3f;
    [Tooltip("_GlobalAlpha 从当前值渐变到 1 的时长（0 = 瞬间到位）")]
    public float barrierFadeDuration = 0f;

    [Header("记忆闪回")]
    [Tooltip("挂有 MemoryFlashback 脚本的 GameObject（Barrier 激活后触发）")]
    public MemoryFlashback memoryFlashback;

    // 防止重复触发
    private bool _triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag(playerTag)) return;

        _triggered = true;
        Debug.Log("[FailTrigger] 触发！进入对象: " + other.name);

        // 搜索组件（自身 → 父级 → 子级）
        Animator anim = other.GetComponent<Animator>()
                     ?? other.GetComponentInParent<Animator>()
                     ?? other.GetComponentInChildren<Animator>();

        ADCharacterController controller = other.GetComponent<ADCharacterController>()
                                        ?? other.GetComponentInParent<ADCharacterController>()
                                        ?? other.GetComponentInChildren<ADCharacterController>();

        StartCoroutine(FailSequence(anim, controller));

        // 延迟激活 Barrier Shader
        StartCoroutine(ActivateBarrierDelayed());
    }

    /// <summary>
    /// 先设 Fail=true，等一帧让状态机切换生效，再禁止移动。
    /// 避免 canMove=false → Walk=false → 进入 Idle 把 Fail 覆盖掉。
    /// </summary>
    private System.Collections.IEnumerator FailSequence(Animator anim, ADCharacterController controller)
    {
        // ① 第一步：立即设置 Fail，让状态机优先开始转换
        if (anim != null)
        {
            anim.SetBool("Fail", true);
            Debug.Log("[FailTrigger] Fail 已设为 true，等待一帧再停止移动...");
        }
        else
        {
            Debug.LogWarning("[FailTrigger] 未找到 Animator！请检查角色层级结构。");
        }

        // ② 等一帧，让 Animator 先完成状态转换判断
        yield return null;

        // ③ 再禁止移动（此时 Fail 状态已经开始切换，Walk=false 不会打断它）
        if (controller != null)
        {
            controller.canMove = false;
            Debug.Log("[FailTrigger] 已禁止移动。");
        }
        else
        {
            Debug.LogWarning("[FailTrigger] 未找到 ADCharacterController！");
        }
    }

    private IEnumerator ActivateBarrierDelayed()
    {
        yield return new WaitForSeconds(barrierDelay);

        if (barrierQuad == null)
        {
            Debug.LogWarning("[FailTrigger] barrierQuad 未赋值，无法激活 Barrier Shader。");
            yield break;
        }

        Material mat = barrierQuad.material;

        if (barrierFadeDuration <= 0f)
        {
            // 瞬间设为 1
            mat.SetFloat("_GlobalAlpha", 1f);
        }
        else
        {
            // 平滑渐变到 1
            float startAlpha = mat.GetFloat("_GlobalAlpha");
            float elapsed = 0f;

            while (elapsed < barrierFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / barrierFadeDuration);
                mat.SetFloat("_GlobalAlpha", Mathf.Lerp(startAlpha, 1f, t));
                yield return null;
            }

            mat.SetFloat("_GlobalAlpha", 1f);
        }

        // Barrier 激活完毕 → 启动记忆闪回走马灯
        if (memoryFlashback != null)
        {
            memoryFlashback.StartFlashback();
        }
    }
}
