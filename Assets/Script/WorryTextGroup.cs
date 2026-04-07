using UnityEngine;
using System.Collections.Generic;
using TMPro;
using Febucci.UI;

/// <summary>
/// 管理“担忧/杂念”文本群组的脚本。
/// 模仿“小蚊子”乱飞的效果，通过控制多个小文本的随机位置和我们之前的自动动画脚本。
/// </summary>
public class WorryTextGroup : MonoBehaviour
{
    [Header("设置")]
    public GameObject worryTextPrefab; // 带有 TMP_Text 和 TMPTextAnimatorAutoEffect 的预制体
    public int poolSize = 5;
    public Vector2 spawnRange = new Vector2(250f, 180f); // 随机分布范围
    public float appearanceInterval = 0.4f; // 每个念头出现的间隔时间
    [Tooltip("两个念头中心点之间的最小距离，防止重叠")]
    public float minDistance = 80f;

    [Header("动态内容")]
    [TextArea] public List<string> worryLines = new List<string>();

    private List<GameObject> textPool = new List<GameObject>();
    private List<Vector3> usedPositions = new List<Vector3>();
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 初始隐藏
        canvasGroup.alpha = 0;
        gameObject.SetActive(false);

        // 预生成池
        if (worryTextPrefab != null)
        {
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(worryTextPrefab, transform);

                // 强制对齐修正：确保子物体是中心点对齐，方便坐标计算
                var rect = obj.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                }

                obj.SetActive(false);
                textPool.Add(obj);
            }
        }
    }

    public void Show(List<string> specificLines = null)
    {
        // 确保父物体层级处于激活状态
        gameObject.SetActive(true);

        // 停止之前的淡入淡出或显示序列
        StopAllCoroutines();

        // 开启淡入
        StartCoroutine(FadeRoutine(1f));

        // 开启逐个显示的序列
        List<string> linesToUse = (specificLines != null && specificLines.Count > 0) ? specificLines : worryLines;
        StartCoroutine(ShowSequentiallyRoutine(linesToUse));
    }

    private System.Collections.IEnumerator ShowSequentiallyRoutine(List<string> lines)
    {
        // 先隐藏所有池里的物体
        foreach (var obj in textPool) obj.SetActive(false);
        usedPositions.Clear();

        for (int i = 0; i < lines.Count && i < textPool.Count; i++)
        {
            GameObject textObj = textPool[i];

            // 1. 寻找不冲突的位置 (Y 轴检测进度)
            Vector3 targetPos = GetRandomNonOverlappingPos();
            usedPositions.Add(targetPos);

            textObj.transform.localPosition = targetPos;
            textObj.SetActive(true);

            // 2. 尝试获取打字机组件
            var typewriter = textObj.GetComponent<Febucci.UI.Core.TypewriterCore>();
            var tmp = textObj.GetComponent<TMP_Text>();

            if (tmp != null)
            {
                tmp.alignment = TextAlignmentOptions.Center;
            }

            if (typewriter != null)
            {
                typewriter.SetTypewriterSpeed(GameSettingsManager.GetDialogueSpeed());
                // 如果有打字机，使用打字机播放
                typewriter.ShowText(lines[i]);
            }
            else if (tmp != null)
            {
                // 否则直接赋值
                tmp.text = lines[i];
            }

            // 3. 应用动画效果 (使用物体上预设的参数，不再通过代码强制覆盖，解决乱飞问题)
            var animEffect = textObj.GetComponent<TMPTextAnimatorAutoEffect>();
            if (animEffect != null)
            {
                animEffect.ApplyGlobalEffects();
            }

            // 4. 等待间隔再出下一个
            yield return new WaitForSeconds(appearanceInterval);
        }
    }

    private Vector3 GetRandomNonOverlappingPos()
    {
        Vector3 newPos = Vector3.zero;
        int maxAttempts = 20; // 增加尝试次数以获得更好的垂直分布

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // 1. 确定 Y 坐标
            float randomY = UnityEngine.Random.Range(-spawnRange.y, spawnRange.y);

            // 2. 检查 Y 轴碰撞
            bool yOverlap = false;
            foreach (var pos in usedPositions)
            {
                // 只对比 Y 轴的绝对距离是否小于最小行距
                if (Mathf.Abs(randomY - pos.y) < minDistance)
                {
                    yOverlap = true;
                    break;
                }
            }

            if (!yOverlap || attempt == maxAttempts - 1)
            {
                // 3. Y 轴没问题了，X 轴随便给一个随机值
                newPos = new Vector3(
                    UnityEngine.Random.Range(-spawnRange.x, spawnRange.x),
                    randomY,
                    0
                );
                return newPos;
            }
        }

        return newPos;
    }

    public void Hide()
    {
        // 如果物体已经是不激活状态，直接返回，避免触发协程错误
        if (!gameObject.activeInHierarchy) return;

        StopAllCoroutines();
        StartCoroutine(FadeRoutine(0f, true));
    }

    private System.Collections.IEnumerator FadeRoutine(float targetAlpha, bool disableAfter = false)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        if (disableAfter && targetAlpha <= 0) gameObject.SetActive(false);
    }
}
