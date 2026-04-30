using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 场景级转场适配器。
/// 用于让某些场景在加载完成后接管全局黑幕，例如场景自己有开场黑屏/Timeline 淡出逻辑时。
/// </summary>
[DisallowMultipleComponent]
public class SceneTransitionSceneAdapter : MonoBehaviour
{
    [Header("Takeover")]
    [SerializeField] private bool takeOverGlobalFadeIn = true;
    [SerializeField] private bool hideGlobalOverlayImmediately = true;

    [Header("Callbacks")]
    [Tooltip("场景已加载完成且全局黑幕仍然覆盖时触发。可在这里把场景本地黑幕设为纯黑，准备接管。")]
    [SerializeField] private UnityEvent onSceneLoadedBehindBlack;

    public bool TakeOverGlobalFadeIn => takeOverGlobalFadeIn;

    public void HandleSceneLoadedBehindBlack(SceneTransitionController controller)
    {
        onSceneLoadedBehindBlack?.Invoke();

        if (hideGlobalOverlayImmediately && controller != null)
        {
            controller.HideImmediate();
        }
    }
}
