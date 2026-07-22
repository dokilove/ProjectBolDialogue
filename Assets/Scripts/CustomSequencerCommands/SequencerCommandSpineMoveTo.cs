// Syntax: SpineMoveTo(actor, x, y, duration, [bounceHeight], [autoFlip], [squash], [flipDuration])
using System.Collections;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

public class SequencerCommandSpineMoveTo : SequencerCommand
{
    private bool isDone = false;
    private CharacterRootController controller;

    void Start()
    {
        Transform actorTransform = GetSubject(0);
        if (actorTransform == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineMoveTo: Subject '{GetParameter(0)}' not found.");
            Stop();
            return;
        }

        controller = actorTransform.GetComponent<CharacterRootController>();
        if (controller == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineMoveTo: CharacterRootController not found on subject '{actorTransform.name}'.");
            Stop();
            return;
        }

        float targetX = GetParameterAsFloat(1);
        float targetY = GetParameterAsFloat(2);
        float duration = GetParameterAsFloat(3, 1f);
        float bounceHeight = GetParameterAsFloat(4, 0.2f);
        bool autoFlip = string.IsNullOrEmpty(GetParameter(5)) || GetParameterAsBool(5, true);
        float squash = GetParameterAsFloat(6, 0.06f);
        float flipDuration = GetParameterAsFloat(7, 0f);

        // The target's Z position should be the root's current Z position.
        Vector3 target = new Vector3(targetX, targetY, actorTransform.position.z);
        
        controller.EnqueueMove(target, duration, bounceHeight, squash, autoFlip, flipDuration, onComplete: () => { isDone = true; });
    }

    void Update()
    {
        if (isDone)
        {
            Stop();
        }
    }

    void OnDestroy()
    {
        // If the sequence is stopped early, skip the move.
        if (!isDone && controller != null)
        {
            controller.SkipAllMoves();
        }
    }
}
