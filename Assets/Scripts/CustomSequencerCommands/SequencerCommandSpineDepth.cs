using System.Collections;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

// Syntax: SpineDepth(actor, scale, offsetY, duration)
// This command only changes depth (scale and Y-offset), not sorting order.
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

        // Get the MeshRenderer to find the current sorting order, which will be preserved.
        var meshRenderer = controller.modelController != null ? controller.modelController.GetComponent<MeshRenderer>() : null;
        if (meshRenderer == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineDepth: No MeshRenderer found on '{controller.modelController.name}'. Cannot proceed.");
            Stop();
            return;
        }

        float targetScale = GetParameterAsFloat(1, 1f);
        float targetOffsetY = GetParameterAsFloat(2, 0f);
        float duration = GetParameterAsFloat(3, 0f);
        int currentSortingOrder = meshRenderer.sortingOrder; // Preserve current sorting order.

        if (duration <= 0)
        {
            controller.SetDepth(targetScale, targetOffsetY, currentSortingOrder, 0, null);
            Stop();
        }
        else
        {
            controller.SetDepth(targetScale, targetOffsetY, currentSortingOrder, duration, () => isDone = true);
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
