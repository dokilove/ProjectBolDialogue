// =================================================================================================
// [SpineFocus]
// 특정 캐릭터를 강조(Focus, 밝은 색상 및 앞 레이어로 정렬)하고,
// 나머지 다른 캐릭터들을 비강조(Unfocus, 어두운 음영 처리 및 뒤 레이어로 정렬)하는 시퀀서 커맨드.
// 전체 캐릭터 강조(all) 및 전체 비강조(none)도 지원합니다.
//
// 문법:
//   SpineFocus(actor, [sortingOrder/keep])
//   SpineFocus(all, [sortingOrder/keep])
//   SpineFocus(none, [sortingOrder/keep])
//
// 파라미터 설명:
//   - actor               : 강조할 대상 캐릭터 [필수] (예: Janghwa, Chona, speaker, listener, all, none)
//                           * "all" : 씬의 모든 캐릭터를 포커스 상태로 전환
//                           * "none": 씬의 모든 캐릭터를 언포커스(어두운 상태)로 전환
//   - sortingOrder / keep : 포커스 캐릭터의 렌더링 정렬 순서 [선택, 기본값 100]
//                           * 숫자(예: 150): 포커스 대상의 sortingOrder를 150으로, 다른 캐릭터를 0으로 설정
//                           * "keep" / "current" / "same" / -1: sortingOrder를 변경하지 않고 색상(밝기)만 전환
//
// 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. 말하는 캐릭터(speaker)만 강조하고 상대방을 어둡게 처리:
//      SpineFocus(speaker);
//
//   2. 특정 캐릭터(Chona) 강조:
//      SpineFocus(Chona);
//
//   3. sortingOrder를 바꾸지 않고 음영(밝기)만 변경하고 싶을 때:
//      SpineFocus(Janghwa, keep);
//
//   4. 더 높은 레이어 순서(200)로 확실하게 앞에 오도록 강조:
//      SpineFocus(Chona, 200);
//
//   5. 모든 캐릭터를 다시 원래 밝기로 복구:
//      SpineFocus(all);
//
//   6. 모든 캐릭터를 일괄 어둡게 처리 (배경 효과 등):
//      SpineFocus(none);
//
// 스킵(Skip) 동작 특성:
//   - 즉발(Instant) 커맨드로서 실행 즉시 머티리얼 색상 및 sortingOrder가 적용되므로
//     스킵 시에도 즉시 최종 포커스 상태가 정확하게 유지됩니다.
// =================================================================================================

using System.Linq;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

/// <summary>
/// 특정 캐릭터를 강조(밝기 및 sortingOrder 상승)하고 다른 캐릭터를 어둡게 처리하는 시퀀서 커맨드.
/// 예: SpineFocus(speaker), SpineFocus(all), SpineFocus(Chona, keep)
/// </summary>
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
