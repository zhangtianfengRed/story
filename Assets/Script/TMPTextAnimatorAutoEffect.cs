using UnityEngine;
using Febucci.UI;
using Febucci.UI.Core;
using TMPro;
using System.Collections.Generic;

[RequireComponent(typeof(TMP_Text))]
[RequireComponent(typeof(TextAnimator_TMP))]
[AddComponentMenu("Febucci/TextAnimator/Custom/TMP Auto Text Effects (Balanced)")]
public class TMPTextAnimatorAutoEffect : MonoBehaviour
{
    [Header("0. 基础物理布局")]
    [Tooltip("字符物理间距。如果动画幅度大，增加此值可防止字符重叠。")]
    [Range(-50, 100)] public float characterSpacing = 15f;

    [Header("1. Wave (上下波动 - 纵向间隔)")]
    public bool useWave = true;
    public float waveAmplitude = 8f;
    public float waveFrequency = 3.5f;
    [Tooltip("核心参数：波形间隔。值越大，相邻字母越‘不同步’，流动感更强。建议 0.3-0.8")]
    public float waveSize = 0.5f;

    [Header("2. Pulse (缩放呼吸 - 大小间隔)")]
    public bool usePulse = true;
    public float pulseAmplitude = 0.45f;
    public float pulseFrequency = 2.2f;
    [Tooltip("缩放的流动感。建议与 WaveSize 略微不同以产生交错感。")]
    public float pulseWaveSize = 0.3f;

    [Header("3. Sway (左右摆动 - 角度间隔)")]
    public bool useSway = true;
    public float swayAmplitude = 12f;
    public float swayFrequency = 2.8f;
    public float swayWaveSize = 0.4f;

    [Header("4. Noise (随机微颤 - 弥补规律性)")]
    public bool useNoise = true;
    public float noiseAmplitude = 2f;
    public float noiseFrequency = 4f;

    private TextAnimator_TMP textAnimator;
    private TMP_Text tmpro;

    private void Awake()
    {
        textAnimator = GetComponent<TextAnimator_TMP>();
        tmpro = GetComponent<TMP_Text>();
        ApplyGlobalEffects();
    }

    private void OnEnable() => ApplyGlobalEffects();

    public void ApplyGlobalEffects()
    {
        if (textAnimator == null || tmpro == null) return;

        // 1. 调整物理间距
        tmpro.characterSpacing = characterSpacing;

        // 2. 动态构建标签列表
        List<string> activeTags = new List<string>();

        if (useWave) activeTags.Add($"wave a={waveAmplitude} f={waveFrequency} w={waveSize}");
        if (usePulse) activeTags.Add($"incr a={pulseAmplitude} f={pulseFrequency} w={pulseWaveSize}");
        if (useSway) activeTags.Add($"swing a={swayAmplitude} f={swayFrequency} w={swayWaveSize}");
        if (useNoise) activeTags.Add($"wiggle a={noiseAmplitude} f={noiseFrequency}");

        // 应用
        textAnimator.DefaultBehaviorsTags = activeTags.ToArray();
        textAnimator.defaultTagsMode = TAnimCore.DefaultTagsMode.Constant;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (textAnimator == null) textAnimator = GetComponent<TextAnimator_TMP>();
        if (tmpro == null) tmpro = GetComponent<TMP_Text>();
        
        // 实时应用到面板，编辑器里也能调间距
        if(tmpro != null) tmpro.characterSpacing = characterSpacing;

        if (Application.isPlaying) ApplyGlobalEffects();
    }
#endif
}
