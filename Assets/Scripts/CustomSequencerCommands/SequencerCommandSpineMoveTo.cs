using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

public class SequencerCommandSpineMoveTo : SequencerCommand
{
    bool isDone = false;
    SpineDualLayerController controller;

    void Start()
    {
        Transform actorTransform = GetSubject(0);
        float targetX = GetParameterAsFloat(1);
        float targetY = GetParameterAsFloat(2);
        float duration = GetParameterAsFloat(3, 1f);
        float bounceHeight = GetParameterAsFloat(4, 0.2f);
        bool autoFlip = string.IsNullOrEmpty(GetParameter(5)) || GetParameterAsBool(5, true);
        float squash = GetParameterAsFloat(6, 0.06f);

        controller = actorTransform != null ? actorTransform.GetComponentInChildren<SpineDualLayerController>() : null;
        if (controller == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning("SpineMoveTo: 컨트롤러를 찾을 수 없습니다.");
            Stop();
            return;
        }

        Vector3 target = new Vector3(targetX, targetY, actorTransform.position.z);
        controller.EnqueueMove(target, duration, bounceHeight, squash, autoFlip, onComplete: () => { isDone = true; });
    }

    void Update()
    {
        if (isDone) Stop();
    }

    // 대사 스킵 등으로 이 커맨드가 완료 전에 강제 파괴될 때 호출됨
    void OnDestroy()
    {
        if (!isDone && controller != null)
        {
            controller.SkipAllMoves();
        }
    }
}