using UnityEngine;

/// <summary>
/// 全局常驻根物体。
/// 挂到任意场景中的一个空物体后，会把常驻系统统一保活并收拢到该根节点下。
/// </summary>
public class PersistentGameRoot : MonoBehaviour
{
    private static PersistentGameRoot _instance;

    [Header("启动系统")]
    [SerializeField] private bool ensureSettingsManager = true;
    [SerializeField] private bool ensureProgressManager = true;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        BootstrapSystems();
    }

    [ContextMenu("Bootstrap Persistent Systems")]
    public void BootstrapSystems()
    {
        if (ensureSettingsManager)
        {
            AttachToRoot(GameSettingsManager.Instance);
        }

        if (ensureProgressManager)
        {
            AttachToRoot(GameProgressManager.Instance);
        }
    }

    private void AttachToRoot(Component target)
    {
        if (target == null)
        {
            return;
        }

        Transform targetTransform = target.transform;
        if (targetTransform == transform)
        {
            return;
        }

        targetTransform.SetParent(transform, true);
    }
}
