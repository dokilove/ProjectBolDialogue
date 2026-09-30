// =================================================================================================
// [SpineDepth]
// 캐릭터의 렌더링 정렬 순서(sortingOrder)는 그대로 유지하면서,
// 원근감(깊이감) 연출을 위한 크기 배율(Depth Scale)과 Y축 보정치(Target Offset Y)만 보간 조절하는 시퀀서 커맨드.
//
// 문법:
//   SpineDepth(actor, [scale], [offsetY], [duration])
//
// 파라미터 설명:
//   - actor       : 대상 캐릭터 [필수] (예: Janghwa, Chona, speaker, listener)
//   - scale       : 원근감 크기 배율 [선택, 기본값 1.0] (예: 1.2 = 전경/가까움, 0.8 = 원경/멀어짐)
//   - offsetY     : 원근감 적용 시 Y축 위치 보정값 [선택, 기본값 0.0]
//   - duration    : 깊이 보간 소요 시간(초) [선택, 기본값 0 = 즉시 변경]
//
// 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. 캐릭터를 0.8초 동안 1.3배 크기로 확대하여 카메라 앞으로 성큼 다가선 느낌 연출:
//      SpineDepth(Chona, 1.3, 0, 0.8);
//
//   2. 원경으로 물러나며 발 위치(offsetY)를 약간 위로 보정 (0.8배 축소 + Y오프셋 0.2):
//      SpineDepth(Chona, 0.8, 0.2, 1.0);
//
//   3. 즉시 기본 깊이(1.0 배율, 오프셋 0)로 원상 복구:
//      SpineDepth(Chona, 1.0, 0, 0);
//
//   4. 캐릭터 이동과 함께 점진적으로 원근 스케일 변화:
//      SpineMoveTo(Chona, 3.0, -0.5, 1.2);
//      SpineDepth(Chona, 1.25, -0.2, 1.2);
//
// 스킵(Skip) 동작 특성:
//   - duration이 0이면 즉시 적용되며, 보간 도중 대화가 넘어가더라도
//     SetDepth에 지정된 목표 스케일과 오프셋 상태가 캐릭터에 안정적으로 보존됩니다.
// =================================================================================================

using System.Collections;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

/// <summary>
/// 캐릭터의 sortingOrder를 유지하며 원근감 크기 배율(scale)과 Y 오프셋(offsetY)을 조절하는 시퀀서 커맨드.
/// 예: SpineDepth(Chona, 1.2, 0.1, 0.5)
/// </summary>
public class SequencerCommandSpineDepth : SequencerCommand
{
    private bool isDone = false;

    void Start()
    {
        Transform subject = GetSubject(0);
        if (subject == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineDepth: Subject '{GetParameter(0)}' not found.");
            Stop();
            return;
        }

        var controller = subject.GetComponentInChildren<SpineVisualContainerController>();
        if (controller == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineDepth: No SpineVisualContainerController found on '{subject.name}' or its children.");
            Stop();
            return;
        }

        // Get the MeshRenderer to find the current sorting order, which will be preserved.
        var meshRenderer = controller.modelController != null ? controller.modelController.GetComponent<MeshRenderer>() : null;
        if (meshRenderer == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineDepth: No MeshRenderer found on '{controller.modelController.name}'. Cannot proceed.");
            Stop();
            return;
        }

        float targetScale = GetParameterAsFloat(1, 1f);
        float targetOffsetY = GetParameterAsFloat(2, 0f);
        float duration = GetParameterAsFloat(3, 0f);
        int currentSortingOrder = meshRenderer.sortingOrder; // Preserve current sorting order.

        if (duration <= 0)
        {
            controller.SetDepth(targetScale, targetOffsetY, currentSortingOrder, 0, null);
            Stop();
        }
        else
        {
            controller.SetDepth(targetScale, targetOffsetY, currentSortingOrder, duration, () => isDone = true);
        }
    }

    void Update()
    {
        if (isDone)
        {
            Stop();
        }
    }
}
