// Syntax: SpineFlip(actor, right, [duration]) or SpineFlip(actor, left, [duration])
// duration: 0 or omitted for instant flip. Otherwise, smoothly flips over the specified duration.
using System.Collections;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

public class SequencerCommandSpineFlip : SequencerCommand
{
    IEnumerator Start()
    {
        Transform actorTransform = GetSubject(0);
        if (actorTransform == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineFlip: Subject '{GetParameter(0)}' not found.");
            Stop();
            yield break;
        }

        var controller = actorTransform.GetComponent<CharacterRootController>();
        if (controller == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineFlip: CharacterRootController not found on subject '{actorTransform.name}'.");
            Stop();
            yield break;
        }

        bool faceRight = GetParameter(1).Equals("right", System.StringComparison.OrdinalIgnoreCase);
        float duration = GetParameterAsFloat(2, 0f);

        if (duration <= 0f)
        {
            controller.SetFacing(faceRight);
            Stop();
        }
        else
        {
            controller.SetFacingOverTime(faceRight, duration);
            yield return new WaitForSeconds(duration);
            Stop();
        }
    }
}
