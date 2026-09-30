// =================================================================================================
// [SpineMoveTo]
// 캐릭터를 지정한 월드 좌표 (x, y)로 바운스 및 자동 좌우 반전을 적용하며 이동시키는 시퀀서 커맨드.
// CharacterRootController의 통합 액션 큐(actionQueue)에 등록되어 순차적으로 실행됩니다.
//
// 문법:
//   SpineMoveTo(actor, x, y, [duration], [bounceHeight], [autoFlip], [squash], [flipDuration])
//
// 파라미터 설명:
//   - actor       : 대상 캐릭터 [필수] (예: Janghwa, Chona, speaker, listener)
//   - x, y        : 목표 월드 좌표 [필수] (Z축은 기존 캐릭터의 Z 좌표 유지)
//   - duration    : 이동 소요 시간(초) [선택, 기본값 1.0, 0 = 즉시 순간이동]
//   - bounceHeight: 이동 중 통통 튀는 포물선 높이 [선택, 기본값 0.2, 0 = 평지 직선 이동]
//   - autoFlip    : 이동 방향에 따라 캐릭터 좌우 자동 반전 여부 [선택, 기본값 true]
//   - squash      : 바운스 착지 시 찌그러짐/늘어남(Squash & Stretch) 강도 [선택, 기본값 0.06, 0 = 변형 없음]
//   - flipDuration: 반전 시 부드러운 Y축 회전 시간 [선택, 기본값 0 = 즉시 반전]
//
// 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. 가장 기본적이고 자연스러운 통통 튀는 이동 (1초 동안 (2.5, 0) 위치로 이동):
//      SpineMoveTo(Chona, 2.5, 0);
//
//   2. 빠르고 부드러운 이동 (0.4초):
//      SpineMoveTo(Chona, 3.0, 0, 0.4);
//
//   3. 바운스 없이 바닥에 붙어서 미끄러지듯 이동 (bounceHeight = 0):
//      SpineMoveTo(Chona, 1.5, 0, 0.8, 0);
//
//   4. 방향 전환을 하지 않고 뒷걸음질치듯 이동 (autoFlip = false):
//      SpineMoveTo(Chona, -2.0, 0, 1.0, 0.2, false);
//
//   5. 큰 보폭으로 높게 통통 튀면서 과장된 탄력 이동 (높이 0.5, 스쿼시 0.12):
//      SpineMoveTo(Chona, 4.0, 0, 1.2, 0.5, true, 0.12);
//
//   6. 뒤돌아볼 때 0.3초 동안 스무스하게 회전하며 반전:
//      SpineMoveTo(Chona, -3.0, 0, 1.0, 0.2, true, 0.06, 0.3);
//
//   7. 즉시 지정 좌표로 순간이동 (duration 0):
//      SpineMoveTo(Chona, 0, 0, 0);
//
//   8. 연속 이동 (통합 큐에 의해 1번 이동 후 2번 이동이 순차적으로 실행):
//      SpineMoveTo(Chona, 2.0, 0, 0.8);
//      SpineMoveTo(Chona, -1.0, 0, 1.0);
//
// 스킵(Skip) 동작 특성:
//   - 대화 스킵 시 CancelOrSnapAction(actionId)에 의해 현재 진행 중인 이동이
//     목표 좌표 (x, y)로 즉시 칼같이 스냅(Snap)되어 오차가 전혀 누적되지 않습니다.
// =================================================================================================

using System.Collections;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

/// <summary>
/// 캐릭터를 지정한 월드 좌표로 바운스 및 자동 반전 효과와 함께 이동시키는 시퀀서 커맨드.
/// 예: SpineMoveTo(Chona, 2.5, 0, 1.0, 0.2)
/// </summary>
public class SequencerCommandSpineMoveTo : SequencerCommand
{
    private bool isDone = false;
    private CharacterRootController controller;
    private int actionId = -1;

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
            controller = actorTransform.GetComponentInChildren<CharacterRootController>();
        }

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
        string p5 = GetParameter(5);
        bool autoFlip = true;
        if (!string.IsNullOrEmpty(p5))
        {
            string c5 = p5.Trim().ToLowerInvariant();
            if (c5 == "false" || c5 == "noflip" || c5 == "keep" || c5 == "keepflip")
                autoFlip = false;
            else
                autoFlip = GetParameterAsBool(5, true);
        }
        float squash = GetParameterAsFloat(6, 0.06f);
        float flipDuration = GetParameterAsFloat(7, 0f);

        // The target's Z position should be the root's current Z position.
        Vector3 target = new Vector3(targetX, targetY, actorTransform.position.z);
        
        actionId = controller.EnqueueMove(target, duration, bounceHeight, squash, autoFlip, flipDuration, onComplete: () => { isDone = true; });
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
        // If the sequence is stopped early, skip/snap this specific move.
        if (!isDone && controller != null && actionId != -1)
        {
            controller.CancelOrSnapAction(actionId);
        }
    }
}
