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
    [SerializeField] private bool ensureGameFlowManager = true;
    [SerializeField] private bool ensureSceneTransitionController = true;
    [SerializeField] private GameFlowDefinition gameFlowDefinition;

    [Header("场景切换黑屏")]
    [SerializeField] private Color transitionColor = Color.black;
    [SerializeField] private float transitionFadeOutDuration = 0.35f;
    [SerializeField] private float transitionHoldDuration = 0.05f;
    [SerializeField] private float transitionFadeInDuration = 0.35f;
    [SerializeField] private int transitionSortingOrder = 30000;
    [SerializeField] private bool transitionUseUnscaledTime = true;
    [SerializeField] private bool transitionBlockInputWhileVisible = true;

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

        if (ensureGameFlowManager)
        {
            GameFlowManager flowManager = GameFlowManager.Instance;
            if (gameFlowDefinition != null)
            {
                flowManager.SetDefinition(gameFlowDefinition);
            }

            AttachToRoot(flowManager);
        }

        if (ensureSceneTransitionController)
        {
            SceneTransitionController transitionController = SceneTransitionController.Instance;
            transitionController.Configure(
                transitionColor,
                transitionFadeOutDuration,
                transitionHoldDuration,
                transitionFadeInDuration,
                transitionSortingOrder,
                transitionUseUnscaledTime,
                transitionBlockInputWhileVisible);
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
