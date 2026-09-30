// =================================================================================================
// [CinemachineZoom / OrthographicZoom]
// 2D/Orthographic 환경에서 Cinemachine 가상 카메라의 Orthographic Size(줌 인/아웃)를
// 4:3 기준 종횡비에 맞추어 해상도 독립적으로 부드럽게 보간(Lerp)하거나 즉시 변경하는 시퀀서 커맨드.
//
// 문법:
//   CinemachineZoom(targetOrthoSizeAt4x3, [duration], [vcamName], [easeType])
//   CinemachineZoom(targetOrthoSizeAt4x3, [duration], [easeType])
//   OrthographicZoom(...) 로도 동일하게 사용 가능 (별칭)
//
// 파라미터 설명:
//   - targetOrthoSizeAt4x3 : 4:3 종횡비 기준 목표 Orthographic Size [필수/기본값 5.0]
//                            (숫자가 작을수록 확대/Zoom In, 숫자가 클수록 축소/Zoom Out)
//   - duration             : 줌 전환에 소요되는 시간(초) [선택, 기본값 0 = 즉시 변경]
//   - vcamName             : 적용할 CinemachineCamera 이름 [선택]
//                            (생략 시 씬 내의 모든 활성 가상 카메라 또는 MainCamera에 적용)
//   - easeType             : Linear, EaseIn, EaseOut, EaseInOut, EaseOutBack, EaseInBack [선택, 기본값 EaseInOut]
//
// 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. 4:3 기준 OrthoSize 3.5로 즉시 줌 인:
//      CinemachineZoom(3.5);
//
//   2. 1.5초 동안 부드럽게 4.0 배율로 줌 아웃 (감속 EaseOut):
//      CinemachineZoom(4.0, 1.5, EaseOut);
//
//   3. 특정 카메라(Wide_Cam)만 0.8초 동안 탄성 있는 EaseOutBack 곡선으로 줌 인:
//      CinemachineZoom(3.0, 0.8, Wide_Cam, EaseOutBack);
//
//   4. 카메라 줌 + 캐릭터 이동 연계:
//      CinemachineZoom(3.5, 1.0, EaseOut);
//      SpineTRS(Janghwa, 2.0, 0, 10, 1.0, "EaseOut");
//
// 스킵(Skip) 동작 특성:
//   - 대사를 스킵하거나 노드가 조기 파괴되더라도 OnDestroy에서 CinemachineZoomController.Instance.SkipAllZooms()를 호출하여
//     최종 목표 줌 값으로 즉시 이동되므로 화면 배율이 어긋나지 않습니다.
// =================================================================================================

using System;
using System.Collections;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

/// <summary>
/// 2D Orthographic 환경에서 해상도 독립적으로 카메라 줌을 보간 제어하는 시퀀서 커맨드.
/// 예: CinemachineZoom(4.0, 1.5, EaseOut)
/// </summary>
public class SequencerCommandCinemachineZoom : SequencerCommand
{
    private bool isDone = false;

    void Start()
    {
        float targetOrthoSizeAt4x3 = GetParameterAsFloat(0, 5f);
        float duration = GetParameterAsFloat(1, 0f);

        string vcamName = string.Empty;
        EaseType easeType = EaseType.EaseInOut;

        string param2 = GetParameter(2);
        string param3 = GetParameter(3);

        if (!string.IsNullOrEmpty(param2) && IsEaseTypeString(param2))
        {
            easeType = ParseEaseType(param2);
        }
        else if (!string.IsNullOrEmpty(param2))
        {
            vcamName = param2;
            if (!string.IsNullOrEmpty(param3))
            {
                easeType = ParseEaseType(param3);
            }
        }

        CinemachineZoomController.Instance.EnqueueZoom(targetOrthoSizeAt4x3, duration, vcamName, easeType, () => isDone = true);
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
        if (!isDone && CinemachineZoomController.Instance != null)
        {
            CinemachineZoomController.Instance.SkipAllZooms();
        }
    }

    private static bool IsEaseTypeString(string str)
    {
        if (string.IsNullOrEmpty(str)) return false;
        string clean = str.Trim().ToLowerInvariant().Replace("_", "").Replace(" ", "");
        return clean == "linear" || clean == "easein" || clean == "in" ||
               clean == "easeout" || clean == "out" || clean == "easeinout" ||
               clean == "inout" || clean == "smooth" || clean == "easeoutback" ||
               clean == "outback" || clean == "backout" || clean == "easeinback" ||
               clean == "inback" || clean == "backin";
    }

    private static EaseType ParseEaseType(string str, EaseType defaultType = EaseType.EaseInOut)
    {
        if (string.IsNullOrEmpty(str)) return defaultType;
        string clean = str.Trim().ToLowerInvariant().Replace("_", "").Replace(" ", "");
        switch (clean)
        {
            case "linear":
                return EaseType.Linear;
            case "easein":
            case "in":
                return EaseType.EaseIn;
            case "easeout":
            case "out":
                return EaseType.EaseOut;
            case "easeinout":
            case "inout":
            case "smooth":
                return EaseType.EaseInOut;
            case "easeoutback":
            case "outback":
            case "backout":
                return EaseType.EaseOutBack;
            case "easeinback":
            case "inback":
            case "backin":
                return EaseType.EaseInBack;
            default:
                return defaultType;
        }
    }
}
