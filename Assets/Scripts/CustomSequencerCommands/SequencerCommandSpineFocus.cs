using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using System.Linq; // For .Where()

// Syntax:
//   SpineFocus(Fafnir)          -> Fafnir focus (color, sortingOrder), others unfocus
//   SpineFocus(Fafnir, 150)     -> Fafnir focus (color, sortingOrder=150), others unfocus (sortingOrder=0)
//   SpineFocus(all)             -> All focus (color, sortingOrder=100)
//   SpineFocus(none)            -> All unfocus (color, sortingOrder=0)
public class SequencerCommandSpineFocus : SequencerCommand
{
    void Start()
    {
        string param0 = GetParameter(0);
        int focusedSortingOrder = GetParameterAsInt(1, 100); // Default focused order
        int unfocusedSortingOrder = 0; // Default unfocused order

        Transform actorTransform = GetSubject(0);
        if (actorTransform == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineFocus: Subject '{param0}' not found by GetSubject(0).");
            Stop();
            return;
        }

        var allVisualControllers = FindObjectsByType<SpineVisualContainerController>(FindObjectsSortMode.None);

        if (param0.Equals("all", System.StringComparison.OrdinalIgnoreCase))
        {
            foreach (var visualController in allVisualControllers)
            {
                if (visualController.modelController != null) visualController.modelController.Focus();
                visualController.SetDepth(visualController.CurrentDepthScale, visualController.CurrentTargetOffsetY, focusedSortingOrder, 0f, null); // Preserve current scale and Y-offset
            }
            Stop();
            return;
        }

        if (param0.Equals("none", System.StringComparison.OrdinalIgnoreCase))
        {
            foreach (var visualController in allVisualControllers)
            {
                if (visualController.modelController != null) visualController.modelController.Unfocus();
                visualController.SetDepth(visualController.CurrentDepthScale, visualController.CurrentTargetOffsetY, unfocusedSortingOrder, 0f, null); // Preserve current scale and Y-offset
            }
            Stop();
            return;
        }

        // Specific character focus
        var targetVisualController = actorTransform != null ? actorTransform.GetComponentInChildren<SpineVisualContainerController>() : null;
        if (targetVisualController == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineFocus: '{actorTransform}'에서 SpineVisualContainerController를 찾을 수 없습니다.");
            Stop();
            return;
        }

        // Focus the target
        if (targetVisualController.modelController != null) targetVisualController.modelController.Focus();
        targetVisualController.SetDepth(targetVisualController.CurrentDepthScale, targetVisualController.CurrentTargetOffsetY, focusedSortingOrder, 0f, null); // Preserve current scale and Y-offset

        // Unfocus others
        foreach (var otherVisualController in allVisualControllers.Where(c => c != targetVisualController))
        {
            if (otherVisualController.modelController != null) otherVisualController.modelController.Unfocus();
            otherVisualController.SetDepth(otherVisualController.CurrentDepthScale, otherVisualController.CurrentTargetOffsetY, unfocusedSortingOrder, 0f, null); // Preserve current scale and Y-offset
        }

        Stop();
    }
}
