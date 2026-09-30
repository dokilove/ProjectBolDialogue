// =================================================================================================
// [CinemachinePosition / CinemachineMove]
// Cinemachine 가상 카메라의 월드 위치(X, Y)를 직접 지정하거나 부드럽게 패닝(이동)시키는 시퀀서 커맨드.
//
// 문법:
//   CinemachinePosition(vcamName, x, y, [duration], [easeType])
//   CinemachinePosition(x, y, [duration], [vcamName], [easeType])
//   CinemachineMove(...) 로도 동일하게 사용 가능 (별칭)
//
// 파라미터 설명:
//   - vcamName : 대상 CinemachineCamera 오브젝트 이름 [선택] (생략 시 현재 활성화된 최고 우선순위 카메라 자동 선택)
//   - x, y     : 목표 월드 좌표 [필수] (카메라의 기존 Z축 깊이는 자동 유지)
//   - duration : 이동에 걸리는 시간(초) [선택, 기본값 0 = 즉시 스냅 이동]
//   - easeType : Linear, EaseIn, EaseOut, EaseInOut, EaseOutBack, EaseInBack [선택, 기본값 EaseInOut]
//
// 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. 고정 카메라(Wide_Cam)의 위치를 (0, 3.14)로 즉시 설정:
//      CinemachinePosition(Wide_Cam, 0, 3.14);
//
//   2. 현재 활성화된 카메라를 즉시 특정 위치로 이동:
//      CinemachinePosition(2.5, 3.0);
//
//   3. Wide_Cam을 1.5초 동안 감속(EaseOut)하며 부드럽게 패닝(이동):
//      CinemachinePosition(Wide_Cam, 3.0, 3.14, 1.5, EaseOut);
//
//   4. 별칭인 CinemachineMove로 작성:
//      CinemachineMove(Wide_Cam, -2.0, 3.14, 1.0, EaseInOut);
//
//   5. 카메라 위치 세팅 + 고정 + 화면 밖 캐릭터 입장 종합 연계:
//      CinemachinePosition(Wide_Cam, 0, 3.14);
//      CinemachineFocus(Wide_Cam);
//      SpineTRS(Janghwa, -10, 0, 0, 0);
//      SpineMoveTo(Janghwa, -5, 0, 1.5);
//
// 스킵(Skip) 동작 특성:
//   - 카메라 이동 도중 대사를 스킵하더라도 OnDestroy에서 최종 목표 좌표(targetX, targetY)로 즉시 칼같이 스냅(Snap)됩니다.
//   - 대상 카메라가 다른 오브젝트를 추적(Follow) 중이었다면 수동 위치 제어를 위해 추적을 자동으로 해제합니다.
// =================================================================================================

using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using Unity.Cinemachine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

/// <summary>
/// Cinemachine 카메라의 위치(X, Y)를 직접 지정하거나 부드럽게 패닝(이동)시키는 시퀀서 커맨드.
/// 예: CinemachinePosition(Wide_Cam, 0, 3.14, 1.5, EaseOut)
/// </summary>
public class SequencerCommandCinemachinePosition : SequencerCommand
{
    private bool isDone = false;
    private CinemachineCamera targetCam;
    private Vector3 finalTargetPos;

    void Start()
    {
        string param0 = GetParameter(0);
        string param1 = GetParameter(1);
        string param2 = GetParameter(2);
        string param3 = GetParameter(3);
        string param4 = GetParameter(4);

        string vcamName = string.Empty;
        float targetX = 0f;
        float targetY = 0f;
        float duration = 0f;
        EaseType easeType = EaseType.EaseInOut;

        // 패턴 1: 첫 번째 인자가 숫자인 경우 -> (x, y, [duration], [vcamName/easeType], [easeType])
        if (float.TryParse(param0, NumberStyles.Float, CultureInfo.InvariantCulture, out float px0))
        {
            targetX = px0;
            targetY = GetParameterAsFloat(1, 0f);

            // param2: duration 또는 vcamName
            if (float.TryParse(param2, NumberStyles.Float, CultureInfo.InvariantCulture, out float d))
            {
                duration = d;

                // param3: vcamName 또는 easeType
                if (!string.IsNullOrEmpty(param3))
                {
                    if (IsEaseTypeString(param3))
                    {
                        easeType = ParseEaseType(param3);
                    }
                    else
                    {
                        vcamName = param3;
                        if (!string.IsNullOrEmpty(param4)) easeType = ParseEaseType(param4);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(param2))
            {
                vcamName = param2;
                if (!string.IsNullOrEmpty(param3)) easeType = ParseEaseType(param3);
            }
        }
        // 패턴 2: 첫 번째 인자가 카메라 이름인 경우 -> (vcamName, x, y, [duration], [easeType])
        else
        {
            vcamName = param0;
            targetX = GetParameterAsFloat(1, 0f);
            targetY = GetParameterAsFloat(2, 0f);

            // param3: duration
            if (float.TryParse(param3, NumberStyles.Float, CultureInfo.InvariantCulture, out float d))
            {
                duration = d;
                if (!string.IsNullOrEmpty(param4)) easeType = ParseEaseType(param4);
            }
            else if (!string.IsNullOrEmpty(param3) && IsEaseTypeString(param3))
            {
                easeType = ParseEaseType(param3);
            }
        }

        targetCam = FindTargetCamera(vcamName);
        if (targetCam == null)
        {
            if (DialogueDebug.logWarnings)
            {
                Debug.LogWarning($"[CinemachinePosition] 대상 카메라 '{vcamName}'를 찾을 수 없습니다.");
            }
            Stop();
            return;
        }

        // 대상 카메라가 다른 오브젝트를 추적 중이면 위치 직접 제어를 위해 추적을 해제
        if (targetCam.Follow != null)
        {
            targetCam.Follow = null;
        }

        Vector3 currentPos = targetCam.transform.position;
        finalTargetPos = new Vector3(targetX, targetY, currentPos.z);

        if (duration <= 0f)
        {
            targetCam.transform.position = finalTargetPos;
            isDone = true;
            Stop();
        }
        else
        {
            StartCoroutine(MoveRoutine(targetCam, currentPos, finalTargetPos, duration, easeType));
        }
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
        if (!isDone && targetCam != null)
        {
            targetCam.transform.position = finalTargetPos;
        }
    }

    private IEnumerator MoveRoutine(CinemachineCamera vcam, Vector3 startPos, Vector3 targetPos, float duration, EaseType easeType)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            float easedRatio = CharacterRootController.EvaluateEase(easeType, ratio);

            if (vcam != null)
            {
                vcam.transform.position = Vector3.Lerp(startPos, targetPos, easedRatio);
            }
            yield return null;
        }

        if (vcam != null)
        {
            vcam.transform.position = targetPos;
        }

        isDone = true;
    }

    private static CinemachineCamera FindTargetCamera(string vcamName)
    {
        var allVcams = GameObject.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        if (allVcams == null || allVcams.Length == 0) return null;

        if (!string.IsNullOrEmpty(vcamName))
        {
            foreach (var vcam in allVcams)
            {
                if (vcam.gameObject.name.Equals(vcamName, StringComparison.OrdinalIgnoreCase))
                {
                    return vcam;
                }
            }
        }

        // 이름이 없거나 일치하는 이름이 없으면 가장 Priority가 높은 활성 카메라 선택
        CinemachineCamera bestVcam = null;
        int maxPriority = int.MinValue;
        foreach (var vcam in allVcams)
        {
            if (vcam.isActiveAndEnabled && vcam.Priority.Value > maxPriority)
            {
                maxPriority = vcam.Priority.Value;
                bestVcam = vcam;
            }
        }

        return bestVcam ?? allVcams[0];
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
            case "linear": return EaseType.Linear;
            case "easein":
            case "in": return EaseType.EaseIn;
            case "easeout":
            case "out": return EaseType.EaseOut;
            case "easeinout":
            case "inout":
            case "smooth": return EaseType.EaseInOut;
            case "easeoutback":
            case "outback":
            case "backout": return EaseType.EaseOutBack;
            case "easeinback":
            case "inback":
            case "backin": return EaseType.EaseInBack;
            default: return defaultType;
        }
    }
}
