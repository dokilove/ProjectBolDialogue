// SequencerCommandSpineFlip.cs
// 사용법: SpineFlip(actor, right) 또는 SpineFlip(actor, left)
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

public class SequencerCommandSpineFlip : SequencerCommand
{
    void Start()
    {
        Transform actorTransform = GetSubject(0);
        bool faceRight = GetParameter(1).Equals("right", System.StringComparison.OrdinalIgnoreCase);

        var controller = actorTransform != null ? actorTransform.GetComponentInChildren<SpineDualLayerController>() : null;
        if (controller == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineFlip: '{actorTransform}'에서 컨트롤러를 찾을 수 없습니다.");
            Stop();
            return;
        }

        controller.SetFacing(faceRight);
        Stop();
    }
}