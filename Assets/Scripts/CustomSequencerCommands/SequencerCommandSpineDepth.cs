using System.Collections;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

// Syntax: SpineDepth(actor, scale, offsetY, duration, [sortingOrder])
public class SequencerCommandSpineDepth : SequencerCommand
{
    private bool isDone = false;

    void Start()
    {
        Transform subject = GetSubject(0);
        if (subject == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineDepth: Subject '{GetParameter(0)}' not found.");
            Stop();
            return;
        }

        var controller = subject.GetComponentInChildren<SpineVisualContainerController>();
        if (controller == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineDepth: No SpineVisualContainerController found on '{subject.name}' or its children.");
            Stop();
            return;
        }

        float targetScale = GetParameterAsFloat(1, 1f);
        float targetOffsetY = GetParameterAsFloat(2, 0f);
        float duration = GetParameterAsFloat(3, 0f);
        int targetSortingOrder = GetParameterAsInt(4, 0); // Default to 0 if not specified

        if (duration <= 0)
        {
            controller.SetDepth(targetScale, targetOffsetY, targetSortingOrder, 0, null);
            Stop();
        }
        else
        {
            controller.SetDepth(targetScale, targetOffsetY, targetSortingOrder, duration, () => isDone = true);
        }
    }

    void Update()
    {
        if (isDone)
        {
            Stop();
        }
    }
}
