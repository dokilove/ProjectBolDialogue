// =================================================================================================
// [SpineTrack]
// Spine 듀얼 레이어 컨트롤러(SpineDualLayerController)의 세부 트랙(body, face, extra)에
// 애니메이션을 재생하거나 표정 초기화/이전 모션 리플레이 등의 트랙 액션을 제어하는 시퀀서 커맨드.
//
// 문법:
//   SpineTrack(actor, track, actionOrAnimName)
//
// 파라미터 설명:
//   - actor            : 대상 캐릭터 [필수] (예: Janghwa, Chona, speaker, listener)
//   - track            : 대상 레이어/트랙 [필수]
//                        * "body" : 몸통/기본 모션 레이어
//                        * "face" : 얼굴/표정 레이어
//                        * "extra": 추가/특수 효과 레이어
//   - actionOrAnimName : 재생할 애니메이션 클립 이름 또는 제어 키워드 [필수]
//                        [body 트랙 전용]:
//                          * 애니메이션 이름 (예: "Idle", "Walk", "Attack")
//                          * "clearface:애니이름" -> 얼굴 표정을 지우고 새 몸통 애니메이션 시작
//                          * "replay"             -> 이전에 재생하던 몸통 모션 복구
//                        [face 트랙 전용]:
//                          * 표정 이름 (예: "Smile", "Angry", "Surprise")
//                          * "clear"              -> 현재 표정 제거 (기본 표정으로 복귀)
//                          * "replay"             -> 이전 표정 다시 재생
//                        [extra 트랙 전용]:
//                          * 특수 애니메이션 이름
//                          * "clear"              -> 엑스트라 모션 제거
//                          * "hold"               -> 마지막 프레임에서 유지/정지
//                          * "replay"             -> 이전 엑스트라 모션 다시 재생
//
// 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. 캐릭터의 표정을 미소("Smile")로 변경:
//      SpineTrack(Chona, face, Smile);
//
//   2. 표정을 지우고 기본 얼굴로 되돌리기:
//      SpineTrack(Chona, face, clear);
//
//   3. 몸통 애니메이션을 "Walk"로 변경:
//      SpineTrack(Chona, body, Walk);
//
//   4. 얼굴 표정을 초기화하면서 동시에 몸통 애니메이션을 "Idle"로 변경:
//      SpineTrack(Chona, body, clearface:Idle);
//
//   5. 이전 표정으로 다시 되돌리기 (리플레이):
//      SpineTrack(Chona, face, replay);
//
//   6. 엑스트라 트랙의 모션을 마지막 포즈로 홀드:
//      SpineTrack(Chona, extra, hold);
//
//   7. 대화 시작 시 말하는 사람(speaker)의 표정과 포즈 동시 변경:
//      SpineTrack(speaker, body, Talk);
//      SpineTrack(speaker, face, Smile);
//
// 스킵(Skip) 동작 특성:
//   - 즉발(Instant) 커맨드로서 실행 즉시 해당 트랙의 애니메이션 상태가 세팅되므로,
//     대화 스킵 시에도 최종 지정된 표정과 포즈가 정확하게 적용됩니다.
// =================================================================================================

using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

/// <summary>
/// Spine 캐릭터의 특정 트랙(body, face, extra)에 애니메이션이나 제어 키워드를 실행하는 시퀀서 커맨드.
/// 예: SpineTrack(speaker, face, Smile), SpineTrack(speaker, body, clearface:Idle)
/// </summary>
public class SequencerCommandSpineTrack : SequencerCommand
{
    void Start()
    {
        Transform actorTransform = GetSubject(0);
        if (actorTransform == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineTrack: Subject '{GetParameter(0)}' not found.");
            Stop();
            return;
        }

        GameObject actorGO = actorTransform.gameObject;
        string track = GetParameter(1)?.ToLower();
        string action = GetParameter(2);

        var controller = actorGO.GetComponentInChildren<SpineDualLayerController>();
        if (controller == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineTrack: '{actorGO.name}'에서 SpineDualLayerController를 찾을 수 없습니다.");
            Stop();
            return;
        }

        if (string.IsNullOrEmpty(action))
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning("SpineTrack: 애니메이션 이름 또는 제어 키워드가 비어 있습니다.");
            Stop();
            return;
        }

        switch (track)
        {
            case "body":
                if (action.StartsWith("clearface:", System.StringComparison.OrdinalIgnoreCase))
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
                if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineTrack: 지원하지 않는 트랙 '{track}'입니다. (body, face, extra 중 하나여야 합니다)");
                break;
        }

        Stop();
    }
}