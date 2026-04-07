using System;
using UnityEngine;

[DisallowMultipleComponent]
public class FaceBlendDriver : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private SkinnedMeshRenderer smr;

    [Header("Timeline-controlled (0~1) 这些给 Timeline 拉曲线")]
    [Range(0, 1)] public float mouthJoy;     // blendShape4.Mouth_Joy
    [Range(0, 1)] public float mouthFun;     // blendShape4.Mouth_Fun
    [Range(0, 1)] public float eyeJoy;       // blendShape4.Eye_Joy_L/R
    [Range(0, 1)] public float browJoy;      // blendShape4.Brow_Joy

    [Space(6)]
    [Range(0, 1)] public float mouthAngry;   // blendShape4.Mouth_Angry
    [Range(0, 1)] public float eyeAngry;     // blendShape4.Eye_Angry
    [Range(0, 1)] public float browAngry;    // blendShape4.Brow_Angry
    [Range(0, 1)] public float faceAngry;    // blendShape4.Face_Angry

    [Space(6)]
    [Range(0, 1)] public float mouthSorrow;  // blendShape4.Mouth_Sorrow
    [Range(0, 1)] public float eyeSorrow;    // blendShape4.Eye_Sorrow
    [Range(0, 1)] public float browSorrow;   // blendShape4.Brow_Sorrow
    [Range(0, 1)] public float faceSorrow;   // blendShape4.Face_Sorrow

    [Space(6)]
    [Range(0, 1)] public float mouthSurprised; // blendShape4.Mouth_Surprised
    [Range(0, 1)] public float eyeSurprised;   // blendShape4.Eye_Surprised
    [Range(0, 1)] public float browSurprised;  // blendShape4.Brow_Surprised
    [Range(0, 1)] public float faceSurprised;  // blendShape4.Face_Surprised

    [Header("Optional Neutral shaping (0~1)")]
    [Tooltip("一般不用录，中性就是全0；但有些模型 Neutral 不是0，可用它轻微校正")]
    [Range(0, 1)] public float mouthNeutral; // blendShape4.Mouth_Neutral
    [Range(0, 1)] public float eyeNatural;   // blendShape4.Eye_Natural
    [Range(0, 1)] public float faceNormal;   // blendShape4.Face_Normal

    [Header("Talk (procedural visemes)")]
    [Range(0, 1)] public float talk;         // 说话强度（驱动口型波动）
    [Range(0.5f, 12f)] public float talkSpeed = 6f;

    [Header("Auto Blink (always on)")]
    public bool autoBlink = true;
    [Range(1.0f, 8.0f)] public float blinkIntervalMin = 2.5f;
    [Range(1.0f, 10.0f)] public float blinkIntervalMax = 5.0f;
    [Range(0.04f, 0.25f)] public float blinkCloseTime = 0.08f;
    [Range(0.04f, 0.35f)] public float blinkOpenTime = 0.10f;

    [Tooltip("笑的时候眨眼不闭死，越大越“软眨眼”】【0~0.6比较自然】")]
    [Range(0f, 0.6f)] public float blinkSoftnessWhenSmile = 0.25f;

    [Tooltip("说话时略减少眨眼频率（更专注看人）")]
    [Range(0f, 0.6f)] public float blinkReduceWhenTalking = 0.25f;

    [Header("Smoothing & Life")]
    [Tooltip("表情切换的平滑速度，越大越快")]
    public float expressionSmoothTime = 2.0f;
    [Tooltip("微表情强度，模拟肌肉微颤")]
    public float microExpressionAmount = 0.02f;

    // --- 内部平滑逻辑私有变量 ---
    private float currMouthJoy, currMouthFun, currEyeJoy, currBrowJoy;
    private float currMouthAngry, currEyeAngry, currBrowAngry, currFaceAngry;
    private float currMouthSorrow, currEyeSorrow, currBrowSorrow, currFaceSorrow;
    private float currMouthSurprised, currEyeSurprised, currBrowSurprised, currFaceSurprised;

    // --- indices (Joy/Fun) ---
    int iMouthJoy, iMouthFun, iBrowJoy, iEyeJoyL, iEyeJoyR;

    // --- indices (Angry/Sorrow/Surprised/Neutral) ---
    int iMouthAngry, iEyeAngry, iBrowAngry, iFaceAngry;
    int iMouthSorrow, iEyeSorrow, iBrowSorrow, iFaceSorrow;
    int iMouthSurprised, iEyeSurprised, iBrowSurprised, iFaceSurprised;

    int iMouthNeutral, iEyeNatural, iFaceNormal;

    // --- blink / talk indices ---
    int iEyeCloseL, iEyeCloseR, iEyeCloseBoth;
    int iMouthUp, iMouthDown;
    int iA, iE, iI, iO, iU;

    // blink state
    float nextBlinkTime;
    int blinkPhase; // 0 idle, 1 closing, 2 opening
    float blinkT;
    float blinkAmpL, blinkAmpR;

    // talk phase
    float talkPhase;

    void Reset()
    {
        smr = GetComponent<SkinnedMeshRenderer>();
    }

    void Awake()
    {
        if (!smr) smr = GetComponent<SkinnedMeshRenderer>();
        CacheIndices();
        ScheduleNextBlink();
    }

    void CacheIndices()
    {
        var mesh = smr ? smr.sharedMesh : null;
        if (!mesh) return;

        // Joy/Fun
        iMouthJoy = mesh.GetBlendShapeIndex("blendShape4.Mouth_Joy");
        iMouthFun = mesh.GetBlendShapeIndex("blendShape4.Mouth_Fun");
        iBrowJoy = mesh.GetBlendShapeIndex("blendShape4.Brow_Joy");
        iEyeJoyL = mesh.GetBlendShapeIndex("blendShape4.Eye_Joy_L");
        iEyeJoyR = mesh.GetBlendShapeIndex("blendShape4.Eye_Joy_R");

        // Angry
        iMouthAngry = mesh.GetBlendShapeIndex("blendShape4.Mouth_Angry");
        iEyeAngry = mesh.GetBlendShapeIndex("blendShape4.Eye_Angry");
        iBrowAngry = mesh.GetBlendShapeIndex("blendShape4.Brow_Angry");
        iFaceAngry = mesh.GetBlendShapeIndex("blendShape4.Face_Angry");

        // Sorrow
        iMouthSorrow = mesh.GetBlendShapeIndex("blendShape4.Mouth_Sorrow");
        iEyeSorrow = mesh.GetBlendShapeIndex("blendShape4.Eye_Sorrow");
        iBrowSorrow = mesh.GetBlendShapeIndex("blendShape4.Brow_Sorrow");
        iFaceSorrow = mesh.GetBlendShapeIndex("blendShape4.Face_Sorrow");

        // Surprised
        iMouthSurprised = mesh.GetBlendShapeIndex("blendShape4.Mouth_Surprised");
        iEyeSurprised = mesh.GetBlendShapeIndex("blendShape4.Eye_Surprised");
        iBrowSurprised = mesh.GetBlendShapeIndex("blendShape4.Brow_Surprised");
        iFaceSurprised = mesh.GetBlendShapeIndex("blendShape4.Face_Surprised");

        // Optional Neutral
        iMouthNeutral = mesh.GetBlendShapeIndex("blendShape4.Mouth_Neutral");
        iEyeNatural = mesh.GetBlendShapeIndex("blendShape4.Eye_Natural");
        iFaceNormal = mesh.GetBlendShapeIndex("blendShape4.Face_Normal");

        // blink
        iEyeCloseBoth = mesh.GetBlendShapeIndex("blendShape4.Eye_Close");
        iEyeCloseL = mesh.GetBlendShapeIndex("blendShape4.Eye_Close_L");
        iEyeCloseR = mesh.GetBlendShapeIndex("blendShape4.Eye_Close_R");

        // mouth helper + visemes
        iMouthUp = mesh.GetBlendShapeIndex("blendShape4.Mouth_Up");
        iMouthDown = mesh.GetBlendShapeIndex("blendShape4.Mouth_Down");

        iA = mesh.GetBlendShapeIndex("blendShape4.Mouth_A");
        iE = mesh.GetBlendShapeIndex("blendShape4.Mouth_E");
        iI = mesh.GetBlendShapeIndex("blendShape4.Mouth_I");
        iO = mesh.GetBlendShapeIndex("blendShape4.Mouth_O");
        iU = mesh.GetBlendShapeIndex("blendShape4.Mouth_U");
    }

    void ScheduleNextBlink()
    {
        float interval = UnityEngine.Random.Range(blinkIntervalMin, blinkIntervalMax);
        interval *= Mathf.Lerp(1f, 1f + blinkReduceWhenTalking, Mathf.Clamp01(talk));
        nextBlinkTime = Time.time + interval;
    }

    void LateUpdate()
    {
        if (!smr || !smr.sharedMesh) return;

        // 1. 平滑处理所有表情
        SmoothAllExpressions();

        // 2. 基础表情应用 (使用平滑后的值)
        ApplyBaseExpressions();

        // 3. 说话联动
        ApplyTalkVisemes();

        // 4. 自动眨眼
        if (autoBlink)
        {
            float smile = Mathf.Clamp01(currMouthJoy * 0.8f + currMouthFun);
            ApplyAutoBlink(smile);
        }
    }

    void SmoothAllExpressions()
    {
        float dt = Time.deltaTime * expressionSmoothTime;

        // 微表情扰动 (让由于静止带来的僵硬感消失)
        float noise = (Mathf.PerlinNoise(Time.time * 0.5f, 0) - 0.5f) * microExpressionAmount;

        currMouthJoy = Mathf.Lerp(currMouthJoy, mouthJoy + noise, dt);
        currMouthFun = Mathf.Lerp(currMouthFun, mouthFun + noise, dt);
        currEyeJoy = Mathf.Lerp(currEyeJoy, eyeJoy + noise, dt);
        currBrowJoy = Mathf.Lerp(currBrowJoy, browJoy + noise * 0.5f, dt);

        currMouthAngry = Mathf.Lerp(currMouthAngry, mouthAngry, dt);
        currEyeAngry = Mathf.Lerp(currEyeAngry, eyeAngry, dt);
        currBrowAngry = Mathf.Lerp(currBrowAngry, browAngry, dt);
        currFaceAngry = Mathf.Lerp(currFaceAngry, faceAngry, dt);

        currMouthSorrow = Mathf.Lerp(currMouthSorrow, mouthSorrow, dt);
        currEyeSorrow = Mathf.Lerp(currEyeSorrow, eyeSorrow, dt);
        currBrowSorrow = Mathf.Lerp(currBrowSorrow, browSorrow, dt);
        currFaceSorrow = Mathf.Lerp(currFaceSorrow, faceSorrow, dt);

        currMouthSurprised = Mathf.Lerp(currMouthSurprised, mouthSurprised, dt);
        currEyeSurprised = Mathf.Lerp(currEyeSurprised, eyeSurprised, dt);
        currBrowSurprised = Mathf.Lerp(currBrowSurprised, browSurprised, dt);
        currFaceSurprised = Mathf.Lerp(currFaceSurprised, faceSurprised, dt);
    }

    void ApplyBaseExpressions()
    {
        // 0) Optional Neutral
        Set01(iFaceNormal, faceNormal);
        Set01(iEyeNatural, eyeNatural);
        Set01(iMouthNeutral, mouthNeutral);

        // 1) Joy (采用平滑值)
        Set01(iMouthJoy, currMouthJoy);
        Set01(iMouthFun, currMouthFun);
        Set01(iBrowJoy, currBrowJoy);
        Set01(iEyeJoyL, currEyeJoy);
        Set01(iEyeJoyR, currEyeJoy);

        // 2) Angry
        Set01(iFaceAngry, currFaceAngry);
        Set01(iBrowAngry, currBrowAngry);
        Set01(iEyeAngry, currEyeAngry);
        Set01(iMouthAngry, currMouthAngry);

        // 3) Sorrow
        Set01(iFaceSorrow, currFaceSorrow);
        Set01(iBrowSorrow, currBrowSorrow);
        Set01(iEyeSorrow, currEyeSorrow);
        Set01(iMouthSorrow, currMouthSorrow);

        // 4) Surprised
        Set01(iFaceSurprised, currFaceSurprised);
        Set01(iBrowSurprised, currBrowSurprised);
        Set01(iEyeSurprised, currEyeSurprised);
        Set01(iMouthSurprised, currMouthSurprised);
    }

    void ApplyTalkVisemes()
    {
        float t = Mathf.Clamp01(talk);

        // 如果完全不说话，显式归零所有口型参数（确保嘴巴闭合）
        if (t <= 0.0001f)
        {
            Set01(iA, 0); Set01(iE, 0); Set01(iI, 0); Set01(iO, 0); Set01(iU, 0);
            Set01(iMouthUp, 0); Set01(iMouthDown, 0);
            return;
        }

        talkPhase += Time.deltaTime * talkSpeed;
        float wave = (Mathf.Sin(talkPhase) * 0.5f + 0.5f); // 0..1波动

        float smile = Mathf.Clamp01(currMouthJoy * 0.8f + currMouthFun);
        float amp = t * Mathf.Lerp(1f, 0.75f, smile);

        // --- 核心优化：让眼睛和眉毛随着说话同步“跳动” ---
        // 说话幅度大时，眼睛会微微眯起，眉毛会微微抬起，显得更生动
        float dynamicReaction = amp * wave * 0.15f;

        // 如果正在笑，这种联动感会更强（即所谓的“眉飞色舞”）
        float finalEyeJoy = currEyeJoy + dynamicReaction * (0.5f + smile * 1.5f);
        float finalBrowJoy = currBrowJoy + dynamicReaction * 0.3f;

        Set01(iEyeJoyL, finalEyeJoy);
        Set01(iEyeJoyR, finalEyeJoy);
        Set01(iBrowJoy, finalBrowJoy);

        // --- 原有的口型逻辑 ---
        float a = amp * SmoothPeak(wave, 0.10f);
        float e = amp * SmoothPeak(wave, 0.30f);
        float i = amp * SmoothPeak(wave, 0.50f);
        float o = amp * SmoothPeak(wave, 0.70f);
        float u = amp * SmoothPeak(wave, 0.90f);

        Set01(iA, a); Set01(iE, e); Set01(iI, i); Set01(iO, o); Set01(iU, u);

        float open = amp * Mathf.Lerp(0.1f, 0.9f, wave);
        Set01(iMouthUp, open * 0.6f);
        Set01(iMouthDown, open);
    }

    float SmoothPeak(float x01, float center)
    {
        float d = Mathf.Abs(x01 - center);
        float v = Mathf.Clamp01(1f - d * 4f);
        return v * v * (3f - 2f * v);
    }

    void ApplyAutoBlink(float smile01)
    {
        float baseAmp = 1f - Mathf.Lerp(0f, blinkSoftnessWhenSmile, Mathf.Clamp01(smile01));

        float blinkL = 0f, blinkR = 0f;

        if (blinkPhase == 0)
        {
            if (Time.time >= nextBlinkTime)
            {
                blinkPhase = 1;
                blinkT = 0f;
                blinkAmpL = baseAmp * UnityEngine.Random.Range(0.90f, 1.00f);
                blinkAmpR = baseAmp * UnityEngine.Random.Range(0.90f, 1.00f);
            }
            else
            {
                WriteBlink(0, 0);
                return;
            }
        }

        if (blinkPhase == 1) // closing
        {
            blinkT += Time.deltaTime / Mathf.Max(0.0001f, blinkCloseTime);
            float k = Mathf.SmoothStep(0f, 1f, blinkT);
            blinkL = k * blinkAmpL;
            blinkR = k * blinkAmpR;

            if (blinkT >= 1f)
            {
                blinkPhase = 2;
                blinkT = 0f;
            }
        }
        else // opening
        {
            blinkT += Time.deltaTime / Mathf.Max(0.0001f, blinkOpenTime);
            float k = 1f - Mathf.SmoothStep(0f, 1f, blinkT);
            blinkL = k * blinkAmpL;
            blinkR = k * blinkAmpR;

            if (blinkT >= 1f)
            {
                blinkPhase = 0;
                blinkT = 0f;
                ScheduleNextBlink();
            }
        }

        WriteBlink(blinkL, blinkR);
    }

    void WriteBlink(float blinkL01, float blinkR01)
    {
        Set01(iEyeCloseL, blinkL01);
        Set01(iEyeCloseR, blinkR01);

        float both = (blinkL01 + blinkR01) * 0.5f;
        Set01(iEyeCloseBoth, both);
    }

    void Set01(int idx, float v01)
    {
        if (idx < 0) return;

        // 优化：只有当"非说话状态"且"已经在Timeline控制下(输入为0)"时才跳过。
        // 为了确保说话结束能闭上嘴，如果当前权重不为0，必须允许设置一次0。
        // 这里我们简化逻辑：如果正在说话，或者我们要设置的值不是0，就必须执行。
        if (talk <= 0.001f && v01 <= 0.001f)
        {
            // 额外检查：如果当前 smr 里的值已经是 0 了，才跳过（减少 DC）
            if (smr.GetBlendShapeWeight(idx) <= 0.001f) return;
        }

        smr.SetBlendShapeWeight(idx, Mathf.Clamp01(v01) * 100f);
    }
}