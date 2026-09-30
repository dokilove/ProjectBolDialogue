// =================================================================================================
// [SpineFlip]
// 캐릭터를 이동시키지 않고 좌우 뒤집기(Flip)만 수행하는 시퀀서 커맨드.
// CharacterRootController의 통합 액션 큐(actionQueue)에 등록되어 Move, TRS, Rotate와 순차적으로 실행됩니다.
//
// 문법:
//   SpineFlip(actor)                              -> 현재(또는 큐의 직전) 방향의 반대로 반전 (Toggle)
//   SpineFlip(actor, [duration])                  -> duration 동안 부드럽게 반전 (Toggle)
//   SpineFlip(actor, left / right, [duration])    -> 특정 방향(왼쪽/오른쪽) 바라보기 (duration 기본값 0 = 즉시)
//   SpineFlip(actor, targetActor, [duration])     -> 상대 캐릭터(또는 speaker/listener)가 있는 방향 바라보기
//
// 예시:
//   SpineMoveTo(Janghwa, -5, 0, 1.5);
//   SpineFlip(Janghwa, left, 0.3);                -> 1.5초 걸어간 뒤 자동으로 0.3초 동안 뒤돌아봄!
//   SpineTRS(Janghwa, -5, 0, 15, 0.5);            -> 뒤돌아본 뒤 0.5초 동안 몸 기울이기
//
// 스킵 지원:
//   스킵 시 CancelOrSnapAction(actionId)을 통해 목표 방향으로 즉시 칼같이 스냅(Snap)됩니다.
// =================================================================================================

using System.Globalization;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

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
