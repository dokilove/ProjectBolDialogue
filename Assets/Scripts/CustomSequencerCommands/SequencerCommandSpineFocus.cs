using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

// 사용법:
//   SpineFocus(Fafnir)         -> Fafnir만 focus, 나머지 전원 unfocus
//   SpineFocus(all)            -> 전원 focus
//   SpineFocus(none)           -> 전원 unfocus
public class SequencerCommandSpineFocus : SequencerCommand
{
    void Start()
    {
        string param0 = GetParameter(0);

        if (SpineCharacterGroupManager.Instance == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning("SpineFocus: SpineCharacterGroupManager를 찾을 수 없습니다.");
            Stop();
            return;
        }

        if (param0.Equals("all", System.StringComparison.OrdinalIgnoreCase))
        {
            SpineCharacterGroupManager.Instance.FocusAll();
            Stop();
            return;
        }

        if (param0.Equals("none", System.StringComparison.OrdinalIgnoreCase))
        {
            SpineCharacterGroupManager.Instance.UnfocusAll();
            Stop();
            return;
        }

        // 특정 캐릭터 지정 -> 그 캐릭터만 focus, 나머지 unfocus
        Transform actorTransform = GetSubject(0);
        var controller = actorTransform != null ? actorTransform.GetComponentInChildren<SpineDualLayerController>() : null;

        if (controller == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineFocus: '{actorTransform}'에서 SpineDualLayerController를 찾을 수 없습니다.");
            Stop();
            return;
        }

        SpineCharacterGroupManager.Instance.FocusOnly(controller);
        Stop();
    }
}