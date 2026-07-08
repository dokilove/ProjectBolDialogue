using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

// 사용법: SpineTrack(actor, track, animNameOrKeyword)
// track: body / face / extra
// animNameOrKeyword: 실제 애니메이션 이름, 또는 예약어(clear, hold, replay)
//
// 예시:
//   SpineTrack(Fafnir, face, Smile)          -> SetFaceAnimation("Smile")
//   SpineTrack(Fafnir, face, clear)          -> ClearFaceAnimation()
//   SpineTrack(Fafnir, face, replay)         -> ReplayPrevFaceAnimation()
//   SpineTrack(Fafnir, extra, hold)          -> HoldExtraAnimation()
//   SpineTrack(Fafnir, body, clearface:Idle) -> ClearFaceAndSetBodyAnimation("Idle")
public class SequencerCommandSpineTrack : SequencerCommand
{
    void Start()
    {
        GameObject actorGO = GetSubject(0).gameObject;
        string track = GetParameter(1)?.ToLower();
        string action = GetParameter(2);

        var controller = actorGO != null ? actorGO.GetComponentInChildren<SpineDualLayerController>() : null;
        if (controller == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineTrack: '{actorGO}'에서 SpineDualLayerController를 찾을 수 없습니다.");
            Stop();
            return;
        }

        if (string.IsNullOrEmpty(action))
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning("SpineTrack: 애니메이션 이름/키워드가 비어있습니다.");
            Stop();
            return;
        }

        switch (track)
        {
            case "body":
                if (action.StartsWith("clearface:"))
                    controller.ClearFaceAndSetBodyAnimation(action.Substring("clearface:".Length));
                else if (action.Equals("replay", System.StringComparison.OrdinalIgnoreCase))
                    controller.ReplayPrevBodyAnimation();
                else
                    controller.SetBodyAnimation(action);
                break;

            case "face":
                if (action.Equals("clear", System.StringComparison.OrdinalIgnoreCase))
                    controller.ClearFaceAnimation();
                else if (action.Equals("replay", System.StringComparison.OrdinalIgnoreCase))
                    controller.ReplayPrevFaceAnimation();
                else
                    controller.SetFaceAnimation(action);
                break;

            case "extra":
                if (action.Equals("clear", System.StringComparison.OrdinalIgnoreCase))
                    controller.ClearExtraAnimation();
                else if (action.Equals("hold", System.StringComparison.OrdinalIgnoreCase))
                    controller.HoldExtraAnimation();
                else if (action.Equals("replay", System.StringComparison.OrdinalIgnoreCase))
                    controller.ReplayPrevExtraAnimation();
                else
                    controller.SetExtraAnimation(action);
                break;

            default:
                if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineTrack: 알 수 없는 트랙 '{track}' (body/face/extra 중 하나여야 함)");
                break;
        }

        Stop();
    }
}