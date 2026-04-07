using System.Collections;
using UnityEngine;

public class TalkController : MonoBehaviour
{
    public FaceBlendDriver face; // 你的脚本
    Coroutine co;

    [Range(0, 1)] public float talkLevel = 0.5f;
    public float fadeIn = 0.08f;
    public float fadeOut = 0.10f;

    public void SpeakFor(float seconds)
    {
        if (!face) return;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(SpeakRoutine(seconds));
    }

    public void StartSpeaking()
    {
        if (!face)
        {
            Debug.LogWarning("TalkController: 未指定 FaceBlendDriver 引用！");
            return;
        }
        Debug.Log($"TalkController: 开始说话信号 (TalkLevel: {talkLevel})");
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(StartSpeakRoutine());
    }

    public void StopSpeaking()
    {
        Debug.Log("TalkController: 停止说话信号");
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(StopSpeakRoutine());
    }

    IEnumerator StartSpeakRoutine()
    {
        // fade in
        float t = 0f;
        float start = face.talk;
        while (t < fadeIn)
        {
            t += Time.deltaTime;
            face.talk = Mathf.Lerp(start, talkLevel, t / Mathf.Max(0.0001f, fadeIn));
            yield return null;
        }
        face.talk = talkLevel;
        co = null;
    }

    IEnumerator StopSpeakRoutine()
    {
        // fade out
        float t = 0f;
        float start = face.talk;
        while (t < fadeOut)
        {
            t += Time.deltaTime;
            face.talk = Mathf.Lerp(start, 0f, t / Mathf.Max(0.0001f, fadeOut));
            yield return null;
        }
        face.talk = 0f;
        co = null;
    }

    IEnumerator SpeakRoutine(float seconds)
    {
        yield return StartCoroutine(StartSpeakRoutine());
        yield return new WaitForSeconds(Mathf.Max(0, seconds));
        yield return StartCoroutine(StopSpeakRoutine());
    }
}