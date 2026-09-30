// =================================================================================================
// [SpineMoveRotate] (SpineTRS의 별칭 커맨드)
// Move(이동), Rotate(회전), Scale(크기)의 TRS 변형을 한 번에 동시에 보간 변형하는 만능 시퀀서 커맨드.
// CharacterRootController의 통합 액션 큐(actionQueue)에 등록되어 순차적으로 실행됩니다.
//
// 문법:
//   SpineMoveRotate(actor, x, y, angle, [duration], [scale], [pivot], [easeType], [bounceHeight], [autoFlip], [squash], [flipDuration])
//
// 파라미터 설명:
//   - actor       : 대상 캐릭터 [필수] (예: Janghwa, Chona, speaker, listener)
//   - x, y        : 목표 월드 좌표 [필수] (Z축은 기존 유지)
//   - angle       : 목표 회전 각도 [필수] (Z축 각도 단위, 예: 15, -10, 0)
//   - duration    : 이동 및 회전 소요 시간(초) [선택, 기본값 1.0, 0 = 즉시 스냅]
//   - scale       : 목표 크기 배율 [선택, 생략 시 현재 크기 유지]
//                   (단일 배율: 1.2, 0.8 또는 2D 비균등 크기: "1, 1.2", "scale:1, 1.2")
//   - pivot       : 정규화 회전 피벗 [선택, 기본값 "0.5, 0.5" = 중심]
//                   (키워드: "bottom" / "feet" = 발바닥, "center" = 중심, "top" = 머리, "0.5, 0" 등)
//   - easeType    : Linear, EaseIn, EaseOut, EaseInOut, EaseOutBack, EaseInBack [선택, 기본값 EaseInOut]
//   - bounceHeight: 이동 중 통통 튀는 높이 [선택, 기본값 0]
//   - autoFlip    : 이동 방향에 따라 좌우 자동 반전 [선택, 기본값 true]
//                   (false, "noflip", "keep" 입력 시 이동 방향과 상관없이 현재 바라보는 방향 유지)
//   - squash      : 바운스 시 찌그러짐/늘어남 강도 [선택, 기본값 0]
//   - flipDuration: 반전 시 부드러운 회전 시간 [선택, 기본값 0]
//
// 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. 가장 단순한 이동 + 기울이기 (1초 기본):
//      SpineMoveRotate(Chona, 2.5, 0, 15);
//
//   2. 몸을 뒤집지 않고 뒷걸음질하듯 이동 (autoFlip = false 또는 "noflip"):
//      SpineMoveRotate(Chona, -3.0, 0, 10, 0.8, false);
//      SpineMoveRotate(Chona, -3.0, 0, 10, 0.8, "noflip");
//
//   3. 빠르게 이동하며 회전 (0.4초 + 감속 EaseOut):
//      SpineMoveRotate(Chona, 2.5, 0, -10, 0.4, "EaseOut");
//
//   4. 발바닥을 축으로 자연스럽게 기울이며 이동 (피벗 "0.5, 0", 탄력 EaseOutBack) [추천]:
//      SpineMoveRotate(Chona, 3, 0, 20, 0.8, "0.5, 0", "EaseOutBack");
//
//   5. 이동 + 회전 + 크기 확대 (완전한 TRS 연출, 1.2배 확대):
//      SpineMoveRotate(Chona, 1.5, 0, 10, 0.6, 1.2, "0.5, 0", "EaseOut");
//
//   6. 통통 튀면서 회전 이동 (바운스 높이 0.25):
//      SpineMoveRotate(Chona, 4, 0, -15, 1.2, "0.5, 0", 0.25);
//
//   7. 즉시 원래 위치/각도(0도)로 복귀 (duration 0):
//      SpineMoveRotate(Chona, 0, 0, 0, 0);
//
//   7. 다른 커맨드와 조합 (카메라 줌인 + 캐릭터 이동/회전 후 원위치):
//      CinemachineZoom(4.0, 0.8, "EaseOut");
//      SpineMoveRotate(Chona, 1.5, 0, 10, 0.8, "0.5, 0", "EaseOut");
//      required SpineMoveRotate(Chona, 1.5, 0, 0, 0.5, "EaseOut")@1.0;
//
// 스킵(Skip) 동작 특성:
//   - 스킵 시 CancelOrSnapAction(actionId)을 통해 목표 위치, 각도(0도 복귀 시 완벽한 원점 복귀),
//     스케일로 즉시 칼같이 스냅(Snap)되어 오차가 누적되지 않습니다.
// =================================================================================================

using System.Collections;
using UnityEngine;

/// <summary>
/// Move(이동), Rotate(회전), Scale(크기)을 TRS 순서로 동시에 보간 변환하는 시퀀서 커맨드 (SpineTRS의 별칭).
/// </summary>
public class SequencerCommandSpineMoveRotate : SequencerCommandSpineTRS
{
    // Inherits all logic from SequencerCommandSpineTRS
}
