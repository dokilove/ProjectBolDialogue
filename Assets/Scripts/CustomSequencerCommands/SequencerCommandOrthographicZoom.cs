// =================================================================================================
// [OrthographicZoom] (CinemachineZoom의 별칭 커맨드)
// 2D/Orthographic 환경에서 Cinemachine 가상 카메라의 Orthographic Size(줌 인/아웃)를
// 4:3 기준 종횡비에 맞추어 해상도 독립적으로 부드럽게 보간(Lerp)하거나 즉시 변경하는 시퀀서 커맨드.
// 모든 파라미터 및 동작은 CinemachineZoom과 100% 동일합니다.
//
// 문법:
//   OrthographicZoom(targetOrthoSizeAt4x3, [duration], [vcamName], [easeType])
//   OrthographicZoom(targetOrthoSizeAt4x3, [duration], [easeType])
//
// 파라미터 설명:
//   - targetOrthoSizeAt4x3 : 4:3 종횡비 기준 목표 Orthographic Size [필수/기본값 5.0]
//                            (숫자가 작을수록 확대/Zoom In, 숫자가 클수록 축소/Zoom Out)
//   - duration             : 줌 전환에 소요되는 시간(초) [선택, 기본값 0 = 즉시 변경]
//   - vcamName             : 적용할 CinemachineCamera 이름 [선택]
//   - easeType             : Linear, EaseIn, EaseOut, EaseInOut, EaseOutBack, EaseInBack [선택, 기본값 EaseInOut]
//
// 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. 4:3 기준 OrthoSize 3.5로 즉시 줌 인:
//      OrthographicZoom(3.5);
//
//   2. 1.5초 동안 부드럽게 4.0 배율로 줌 아웃 (감속 EaseOut):
//      OrthographicZoom(4.0, 1.5, EaseOut);
// =================================================================================================

/// <summary>
/// 2D Orthographic 환경에서 카메라 줌을 보간 제어하는 시퀀서 커맨드 (CinemachineZoom의 별칭).
/// 예: OrthographicZoom(4.0, 1.5, EaseOut)
/// </summary>
public class SequencerCommandOrthographicZoom : SequencerCommandCinemachineZoom
{
    // Inherits all logic from SequencerCommandCinemachineZoom
}
