// =================================================================================================
// [CinemachineMove] (CinemachinePosition의 별칭 커맨드)
// Cinemachine 가상 카메라의 월드 위치(X, Y)를 직접 지정하거나 부드럽게 패닝(이동)시키는 시퀀서 커맨드.
// 모든 파라미터 및 동작은 CinemachinePosition과 100% 동일합니다.
//
// 문법:
//   CinemachineMove(vcamName, x, y, [duration], [easeType])
//   CinemachineMove(x, y, [duration], [vcamName], [easeType])
//
// 파라미터 설명:
//   - vcamName : 대상 CinemachineCamera 오브젝트 이름 [선택] (생략 시 현재 활성화된 최고 우선순위 카메라 자동 선택)
//   - x, y     : 목표 월드 좌표 [필수] (카메라의 기존 Z축 깊이는 자동 유지)
//   - duration : 이동에 걸리는 시간(초) [선택, 기본값 0 = 즉시 스냅 이동]
//   - easeType : Linear, EaseIn, EaseOut, EaseInOut, EaseOutBack, EaseInBack [선택, 기본값 EaseInOut]
//
// 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. Wide_Cam의 위치를 (0, 3.14)로 즉시 설정:
//      CinemachineMove(Wide_Cam, 0, 3.14);
//
//   2. Wide_Cam을 1.5초 동안 감속(EaseOut)하며 부드럽게 패닝(이동):
//      CinemachineMove(Wide_Cam, 3.0, 3.14, 1.5, EaseOut);
//
//   3. 현재 활성 카메라의 위치를 (2.5, 3.0)으로 1초 동안 이동:
//      CinemachineMove(2.5, 3.0, 1.0);
// =================================================================================================

/// <summary>
/// Cinemachine 카메라의 위치(X, Y)를 직접 지정하거나 부드럽게 패닝(이동)시키는 시퀀서 커맨드 (CinemachinePosition의 별칭).
/// 예: CinemachineMove(Wide_Cam, 0, 3.14, 1.5, EaseOut)
/// </summary>
public class SequencerCommandCinemachineMove : SequencerCommandCinemachinePosition
{
    // Inherits all logic from SequencerCommandCinemachinePosition
}
