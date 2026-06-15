using System.Collections.Generic;
using System.Text;

public static class DialogueSubtitleTextUtility
{
    private static readonly char[] HardBreakCharacters =
    {
        '\n', '\r', '。', '！', '？', '!', '?', ';', '；'
    };

    private static readonly char[] SoftBreakCharacters =
    {
        '，', ',', '、', ':', '：'
    };

    public static List<DialogueSubtitleCue> BuildCues(
        DialogueSubtitleInputMode inputMode,
        string fullSubtitleText,
        string fullTimingText,
        IList<DialogueSubtitleCue> manualCues,
        int preferredMaxCharactersPerCue)
    {
        if (inputMode == DialogueSubtitleInputMode.ManualCues)
        {
            return CopyManualCues(manualCues);
        }

        return BuildAutoSplitCues(fullSubtitleText, fullTimingText, preferredMaxCharactersPerCue);
    }

    private static List<DialogueSubtitleCue> CopyManualCues(IList<DialogueSubtitleCue> manualCues)
    {
        List<DialogueSubtitleCue> result = new List<DialogueSubtitleCue>();
        if (manualCues == null)
        {
            return result;
        }

        for (int i = 0; i < manualCues.Count; i++)
        {
            DialogueSubtitleCue cue = manualCues[i];
            if (cue == null || !cue.HasText)
            {
                continue;
            }

            result.Add(cue);
        }

        return result;
    }

    private static List<DialogueSubtitleCue> BuildAutoSplitCues(
        string fullSubtitleText,
        string fullTimingText,
        int preferredMaxCharactersPerCue)
    {
        List<string> displaySegments = SplitText(fullSubtitleText, preferredMaxCharactersPerCue);
        List<string> timingSegments = SplitText(fullTimingText, preferredMaxCharactersPerCue);
        bool canUseTimingSegments = timingSegments.Count == displaySegments.Count;

        List<DialogueSubtitleCue> result = new List<DialogueSubtitleCue>();
        for (int i = 0; i < displaySegments.Count; i++)
        {
            result.Add(new DialogueSubtitleCue
            {
                startTime = 0f,
                endTime = -1f,
                text = displaySegments[i],
                timingText = canUseTimingSegments ? timingSegments[i] : string.Empty
            });
        }

        return result;
    }

    private static List<string> SplitText(string text, int preferredMaxCharactersPerCue)
    {
        List<string> result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        int maxCharacters = preferredMaxCharactersPerCue <= 0
            ? 42
            : preferredMaxCharactersPerCue;

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];
            if (character == '\r')
            {
                continue;
            }

            builder.Append(character);

            if (IsHardBreak(character))
            {
                FlushSegment(builder, result);
                continue;
            }

            if (GetVisibleLength(builder) >= maxCharacters && TryFlushAtSoftBreak(builder, result))
            {
                continue;
            }
        }

        FlushSegment(builder, result);
        return result;
    }

    private static bool TryFlushAtSoftBreak(StringBuilder builder, List<string> result)
    {
        for (int i = builder.Length - 1; i >= 0; i--)
        {
            if (!IsSoftBreak(builder[i]))
            {
                continue;
            }

            string segment = builder.ToString(0, i + 1).Trim();
            if (segment.Length == 0)
            {
                return false;
            }

            result.Add(segment);

            string remainder = builder.ToString(i + 1, builder.Length - i - 1).TrimStart();
            builder.Length = 0;
            builder.Append(remainder);
            return true;
        }

        FlushSegment(builder, result);
        return true;
    }

    private static void FlushSegment(StringBuilder builder, List<string> result)
    {
        string segment = builder.ToString().Trim();
        builder.Length = 0;

        if (!string.IsNullOrWhiteSpace(segment))
        {
            result.Add(segment);
        }
    }

    private static int GetVisibleLength(StringBuilder builder)
    {
        int length = 0;
        bool insideTag = false;

        for (int i = 0; i < builder.Length; i++)
        {
            char character = builder[i];
            if (character == '<')
            {
                insideTag = true;
                continue;
            }

            if (character == '>')
            {
                insideTag = false;
                continue;
            }

            if (!insideTag && !char.IsWhiteSpace(character))
            {
                length++;
            }
        }

        return length;
    }

    private static bool IsHardBreak(char character)
    {
        for (int i = 0; i < HardBreakCharacters.Length; i++)
        {
            if (character == HardBreakCharacters[i])
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSoftBreak(char character)
    {
        for (int i = 0; i < SoftBreakCharacters.Length; i++)
        {
            if (character == SoftBreakCharacters[i])
            {
                return true;
            }
        }

        return false;
    }
}
