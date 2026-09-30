// =================================================================================================
// [CinemachineFocus]
// 특정 Cinemachine 가상 카메라의 우선순위(Priority)를 높이고, 다른 모든 가상 카메라는 0으로 낮추어
// 원하는 카메라 시점으로 앵글을 즉시 전환(Focus/Blend)시키는 시퀀서 커맨드.
//
// 문법:
//   CinemachineFocus(vcamName, [priority])
//
// 파라미터 설명:
//   - vcamName : 활성화할 CinemachineCamera 게임 오브젝트 이름 [필수] (대소문자 무관)
//   - priority : 대상 카메라에 부여할 우선순위 값 [선택, 기본값 15] (대상 외 카메라는 자동으로 0 설정)
//
// 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. 고정 와이드 카메라(Wide_Cam)로 시점 전환:
//      CinemachineFocus(Wide_Cam);
//
//   2. 장화 클로즈업 카메라(Janghwa_Cam)로 시점 전환:
//      CinemachineFocus(Janghwa_Cam);
//
//   3. 우선순위를 20으로 높게 지정하여 강제 전환:
//      CinemachineFocus(Wide_Cam, 20);
//
//   4. 고정 앵글 전환 후 캐릭터가 화면 밖에서 걸어 들어오는 연계:
//      CinemachineFocus(Wide_Cam);
//      SpineTRS(Janghwa, -10, 0, 0, 0);
//      SpineMoveTo(Janghwa, -5, 0, 1.5);
//
//   5. 걸어 들어온 후 1.5초 뒤에 캐릭터 카메라로 전환:
//      CinemachineFocus(Wide_Cam);
//      SpineTRS(Janghwa, -10, 0, 0, 0);
//      SpineMoveTo(Janghwa, -5, 0, 1.5);
//      CinemachineFocus(Janghwa_Cam)@1.5;
//
// 스킵(Skip) 동작 특성:
//   - 우선순위 값만 즉시 변경하는 즉각 실행(0초) 커맨드이므로 대화 흐름을 지연시키지 않습니다.
//   - 대사를 스킵하더라도 지정된 카메라가 즉시 활성화된 상태로 유지됩니다.
// =================================================================================================

using UnityEngine;
using Unity.Cinemachine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

/// <summary>
/// 특정 Cinemachine 가상 카메라의 우선순위를 높여 화면 전환을 수행하는 시퀀서 커맨드.
/// 예: CinemachineFocus(Wide_Cam)
/// </summary>
public class SequencerCommandCinemachineFocus : SequencerCommand
{
    void Start()
    {
        string targetName = GetParameter(0);
        int activePriority = GetParameterAsInt(1, 15);

        var allVcams = GameObject.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (var vcam in allVcams)
        {
            bool isTarget = vcam.gameObject.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase);
            vcam.Priority = isTarget ? activePriority : 0;
        }

        Stop();
    }
}