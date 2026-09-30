// =================================================================================================
// [SpineMoveRotate] (SpineTRS의 별칭 커맨드)
// Move(이동), Rotate(회전), Scale(크기)을 TRS 순서로 한 번에 동시에 보간 변환하는 시퀀서 커맨드
//
// ■ 문법:
//   SpineMoveRotate(actor, x, y, angle, [duration], [scale], [pivot], [easeType], [bounceHeight], [autoFlip], [squash], [flipDuration])
//
// ■ 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. 가장 단순한 이동 + 기울이기 (1초 기본):
//      SpineMoveRotate(Chona, 2.5, 0, 15)
//
//   2. 빠르게 휙 이동하며 회전 (0.4초 + 감속 EaseOut):
//      SpineMoveRotate(Chona, 2.5, 0, -10, 0.4, "EaseOut")
//
//   3. 발바닥을 축으로 자연스럽게 기울어지며 이동 (피벗 "0.5, 0", 탄력 EaseOutBack) ★추천:
//      SpineMoveRotate(Chona, 3, 0, 20, 0.8, "0.5, 0", "EaseOutBack")
//
//   4. 이동 + 회전 + 크기 확대 (완전한 TRS 연출, 1.2배 확대):
//      SpineMoveRotate(Chona, 1.5, 0, 10, 0.6, 1.2, "0.5, 0", "EaseOut")
//
//   5. 통통 튀면서 회전 이동 (바운스 높이 0.25):
//      SpineMoveRotate(Chona, 4, 0, -15, 1.2, "0.5, 0", 0.25)
//
//   6. 즉시 원래 위치/각도(0도)로 복귀 (duration 0):
//      SpineMoveRotate(Chona, 0, 0, 0, 0)
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
