using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[AddComponentMenu("Command/Mouse Click Object Activator")]
public class CommandMouseClickObjectActivator : MonoBehaviour
{
    [Header("Activation")]
    [Tooltip("被 CommandMouseInteractable.onClick 调用后要切换激活状态的对象。")]
    public GameObject targetObject;
    [Tooltip("调用 ActivateTarget 时设置目标对象的激活状态。默认开启。")]
    public bool targetActiveState = true;
    [Tooltip("触发一次后禁用本组件，避免重复响应点击。")]
    public bool triggerOnce;

    [Header("Events")]
    public UnityEvent onActivated = new UnityEvent();

    private bool hasTriggered;

    // Bind this method manually in CommandMouseInteractable.onClick.
    public void ActivateTarget()
    {
        if (triggerOnce && hasTriggered)
        {
            return;
        }

        hasTriggered = true;

        if (targetObject != null)
        {
            targetObject.SetActive(targetActiveState);
        }
        else
        {
            Debug.LogWarning("[CommandMouseClickObjectActivator] Target object is not assigned.", this);
        }

        onActivated.Invoke();

        if (triggerOnce)
        {
            enabled = false;
        }
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
        enabled = true;
    }
}
