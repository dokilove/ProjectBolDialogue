// =================================================================================================
// [SpineFlip]
// 캐릭터를 이동시키지 않고 제자리에서 좌우 뒤집기(Flip)만 수행하는 시퀀서 커맨드.
// CharacterRootController의 통합 액션 큐(actionQueue)에 등록되어 SpineMoveTo, SpineTRS, SpineRotate와
// 완벽하게 동기화되어 순차적으로 실행됩니다.
//
// 문법:
//   SpineFlip(actor)                              -> 반대 방향으로 즉시 토글 반전 (Toggle)
//   SpineFlip(actor, [duration])                  -> duration 동안 부드럽게 반대 방향으로 토글 반전
//   SpineFlip(actor, left / right, [duration])    -> 특정 방향(왼쪽/오른쪽) 바라보기 (기본값 duration = 0 즉시)
//   SpineFlip(actor, targetActor, [duration])     -> 상대 캐릭터(speaker/listener 등)가 있는 방향 바라보기
//
// 파라미터 설명:
//   - actor       : 대상 캐릭터 [필수] (예: Janghwa, Chona, speaker, listener)
//   - direction   : 목표 방향 또는 대상 [선택, 기본값 toggle]
//                   * "left" / "l" : 왼쪽을 바라봄
//                   * "right" / "r": 오른쪽을 바라봄
//                   * "toggle"     : 현재(또는 큐의 직전 액션 기준) 방향의 반대로 전환
//                   * 상대 액터 이름(예: speaker, listener, Fafnir): 상대방의 X 위치를 향해 돌아봄
//                   * 숫자(예: 0.4): duration으로 인식되어 해당 시간 동안 토글 반전 수행
//   - duration    : 반전 소요 시간(초) [선택, 기본값 0 = 즉시 반전, 0보다 크면 부드러운 Y축 회전]
//
// 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. 제자리에서 즉시 반대 방향으로 뒤돌아보기:
//      SpineFlip(Chona);
//
//   2. 제자리에서 0.3초 동안 스무스하게 회전하며 뒤돌아보기:
//      SpineFlip(Chona, 0.3);
//
//   3. 확실하게 오른쪽 / 왼쪽을 바라보도록 지정:
//      SpineFlip(Chona, right);
//      SpineFlip(Chona, left, 0.25);
//
//   4. 상대 캐릭터(화자/청자/특정인물)가 있는 쪽을 쳐다보기:
//      SpineFlip(Chona, speaker);
//      SpineFlip(Chona, Janghwa, 0.3);
//
//   5. 큐 연계 연출 (1.5초 걸어간 뒤 자동으로 0.3초 동안 뒤돌아보고 기울임):
//      SpineMoveTo(Janghwa, -5, 0, 1.5);
//      SpineFlip(Janghwa, left, 0.3);
//      SpineTRS(Janghwa, -5, 0, 15, 0.5);
//
// 스킵(Skip) 동작 특성:
//   - 대화 스킵 시 CancelOrSnapAction(actionId)에 의해 목표 방향으로 즉시 칼같이 스냅(Snap)되어
//     뒤이어 재생되는 액션들의 시작 방향이 흐트러지지 않습니다.
// =================================================================================================

using System.Globalization;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

/// <summary>
/// 캐릭터를 이동시키지 않고 제자리에서 좌우 반전(Flip)을 수행하는 시퀀서 커맨드.
/// 예: SpineFlip(Chona, left, 0.3)
/// </summary>
public class SequencerCommandSpineFlip : SequencerCommand
{
    private bool isDone = false;
    private CharacterRootController controller;
    private int actionId = -1;

    void Start()
    {
        Transform actorTransform = GetSubject(0);
        if (actorTransform == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineFlip: Subject '{GetParameter(0)}' not found.");
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
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineFlip: CharacterRootController not found on subject '{actorTransform.name}'.");
            Stop();
            return;
        }

        string param1 = GetParameter(1);

        bool isToggle = false;
        bool targetFacing = false;
        float duration = 0f;

        if (string.IsNullOrEmpty(param1))
        {
            isToggle = true;
            duration = 0f;
        }
        else if (float.TryParse(param1, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedDuration))
        {
            // SpineFlip(actor, 0.4)
            isToggle = true;
            duration = parsedDuration;
        }
        else
        {
            string clean1 = param1.Trim().ToLowerInvariant();
            if (clean1 == "right" || clean1 == "r" || clean1 == "true")
            {
                targetFacing = true;
                duration = GetParameterAsFloat(2, 0f);
            }
            else if (clean1 == "left" || clean1 == "l" || clean1 == "false")
            {
                targetFacing = false;
                duration = GetParameterAsFloat(2, 0f);
            }
            else if (clean1 == "toggle" || clean1 == "flip")
            {
                isToggle = true;
                duration = GetParameterAsFloat(2, 0f);
            }
            else
            {
                // Check if param1 names a target subject (e.g. other character, speaker, listener)
                Transform targetActor = GetSubject(1);
                if (targetActor != null && targetActor != actorTransform)
                {
                    targetFacing = targetActor.position.x > actorTransform.position.x;
                    duration = GetParameterAsFloat(2, 0f);
                }
                else
                {
                    isToggle = true;
                    duration = GetParameterAsFloat(2, 0f);
                }
            }
        }

        if (isToggle)
        {
            targetFacing = !controller.LastQueuedFacing;
        }

        actionId = controller.EnqueueFlip(targetFacing, duration, onComplete: () => { isDone = true; });
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
        if (!isDone && controller != null && actionId != -1)
        {
            controller.CancelOrSnapAction(actionId);
        }
    }
}
