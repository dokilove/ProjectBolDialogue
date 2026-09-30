// =================================================================================================
// [SpineOrder] (SpineSortingOrder의 짧은 별칭 커맨드)
// 캐릭터의 음영/밝기(Focus)나 원근 스케일(Scale)을 건드리지 않고,
// 오직 렌더링 앞뒤 정렬 순서(MeshRenderer.sortingOrder)만 단독으로 변경하는 시퀀서 커맨드.
// 특정 캐릭터 또는 전체 캐릭터("all")를 대상으로 즉시 변경하거나 시간(duration)에 걸쳐 부드럽게 전환할 수 있습니다.
//
// 문법:
//   SpineOrder(actor, order, [duration])
//   SpineSortingOrder(actor, order, [duration])
//
// 파라미터 설명:
//   - actor   : 대상 캐릭터 [필수] (예: Janghwa, Chona, speaker, listener, all)
//               * "all" : 씬에 존재하는 모든 캐릭터의 sortingOrder를 일괄 변경
//   - order   : 목표 sortingOrder 정수값 [필수] (예: 100, 150, 0, -10 등)
//               * 키워드 지원: "front" = 100, "back" = 0
//   - duration: 순서 변경 보간 시간(초) [선택, 기본값 0 = 즉시 변경]
//
// 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. Chona를 앞쪽 레이어로 즉시 올리기:
//      SpineOrder(Chona, 150);
//
//   2. 현재 대화 화자(speaker)를 앞으로 올리고, 청자(listener)를 뒤로 보내기:
//      SpineOrder(speaker, 100);
//      SpineOrder(listener, 50);
//
//   3. 0.5초 동안 점진적으로 레이어 순서를 변경:
//      SpineOrder(Chona, 120, 0.5);
//
//   4. 씬의 모든 캐릭터의 sortingOrder를 0으로 일괄 초기화:
//      SpineOrder(all, 0);
//
// 스킵(Skip) 동작 특성:
//   - duration이 0이면 즉시 적용되며, 보간 도중 대화가 스킵되더라도
//     SnapSortingOrder에 의해 최종 목표 sortingOrder로 즉시 스냅(Snap)되어 레이어가 꼬이지 않습니다.
// =================================================================================================

using System.Collections;
using UnityEngine;

/// <summary>
/// 캐릭터의 밝기나 스케일 변화 없이 오직 렌더링 앞뒤 순서(sortingOrder)만 단독으로 변경하는 시퀀서 커맨드 (SpineSortingOrder의 별칭).
/// 예: SpineOrder(Chona, 150), SpineOrder(speaker, 100)
/// </summary>
public class SequencerCommandSpineOrder : SequencerCommandSpineSortingOrder
{
    // Inherits all logic from SequencerCommandSpineSortingOrder
}
