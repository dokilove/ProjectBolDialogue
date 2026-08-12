using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using System.Linq; // For .Where()

// Syntax:
//   SpineFocus(Fafnir)          -> Fafnir focus (color, sortingOrder=100), others unfocus (sortingOrder=0)
//   SpineFocus(Fafnir, 150)     -> Fafnir focus (color, sortingOrder=150), others unfocus (sortingOrder=0)
//   SpineFocus(Fafnir, keep)    -> Fafnir focus (color), others unfocus, sortingOrder unchanged
//   SpineFocus(Fafnir, -1)      -> Fafnir focus (color), others unfocus, sortingOrder unchanged
//   SpineFocus(all)             -> All focus (color, sortingOrder=100)
//   SpineFocus(all, keep)       -> All focus (color), sortingOrder unchanged
//   SpineFocus(none)            -> All unfocus (color, sortingOrder=0)
//   SpineFocus(none, keep)      -> All unfocus (color), sortingOrder unchanged
public class SequencerCommandSpineFocus : SequencerCommand
{
    void Start()
    {
        string param0 = GetParameter(0);
        string param1 = GetParameter(1);

        bool keepOrder = false;
        int focusedSortingOrder = 100; // Default focused order
        int unfocusedSortingOrder = 0; // Default unfocused order

        if (!string.IsNullOrEmpty(param1))
        {
            if (param1.Equals("keep", System.StringComparison.OrdinalIgnoreCase) ||
                param1.Equals("current", System.StringComparison.OrdinalIgnoreCase) ||
                param1.Equals("same", System.StringComparison.OrdinalIgnoreCase) ||
                param1 == "-1")
            {
                keepOrder = true;
            }
            else
            {
                focusedSortingOrder = GetParameterAsInt(1, 100);
            }
        }

        var allVisualControllers = FindObjectsByType<SpineVisualContainerController>(FindObjectsSortMode.None);

        if (param0.Equals("all", System.StringComparison.OrdinalIgnoreCase))
        {
            foreach (var visualController in allVisualControllers)
            {
                if (visualController.modelController != null) visualController.modelController.Focus();
                if (!keepOrder)
                {
                    visualController.SetDepth(visualController.CurrentDepthScale, visualController.CurrentTargetOffsetY, focusedSortingOrder, 0f, null); // Preserve current scale and Y-offset
                }
            }
            Stop();
            return;
        }

        if (param0.Equals("none", System.StringComparison.OrdinalIgnoreCase))
        {
            foreach (var visualController in allVisualControllers)
            {
                if (visualController.modelController != null) visualController.modelController.Unfocus();
                if (!keepOrder)
                {
                    visualController.SetDepth(visualController.CurrentDepthScale, visualController.CurrentTargetOffsetY, unfocusedSortingOrder, 0f, null); // Preserve current scale and Y-offset
                }
            }
            Stop();
            return;
        }

        Transform actorTransform = GetSubject(0);
        if (actorTransform == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineFocus: Subject '{param0}' not found by GetSubject(0).");
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
        if (!keepOrder)
        {
            targetVisualController.SetDepth(targetVisualController.CurrentDepthScale, targetVisualController.CurrentTargetOffsetY, focusedSortingOrder, 0f, null); // Preserve current scale and Y-offset
        }

        // Unfocus others
        foreach (var otherVisualController in allVisualControllers.Where(c => c != targetVisualController))
        {
            if (otherVisualController.modelController != null) otherVisualController.modelController.Unfocus();
            if (!keepOrder)
            {
                otherVisualController.SetDepth(otherVisualController.CurrentDepthScale, otherVisualController.CurrentTargetOffsetY, unfocusedSortingOrder, 0f, null); // Preserve current scale and Y-offset
            }
        }

        Stop();
    }
}
