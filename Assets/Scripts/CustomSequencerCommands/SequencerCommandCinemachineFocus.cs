// =================================================================================================
// [CinemachineFocus]
// 특정 Cinemachine 가상 카메라의 우선순위(Priority)를 높이고 전환 시간(블렌딩 시간)을 제어하여,
// 순식간에 즉시 컷(Cut) 전환하거나 지정한 시간 동안 부드럽게(Ease) 시점을 전환하는 시퀀서 커맨드.
//
// 문법:
//   CinemachineFocus(vcamName, [blendDurationOrCut], [priority])
//   CinemachineFocus(vcamName, [blendDurationOrCut], [easeStyle], [priority])
//
// 파라미터 설명:
//   - vcamName           : 활성화할 CinemachineCamera 게임 오브젝트 이름 [필수] (대소문자 무관)
//   - blendDurationOrCut : 카메라 전환(블렌드) 소요 시간 [선택, 기본값: CinemachineBrain 기본 설정 유지]
//                          * 0, "cut", "instant", "snap" : 순식간에 즉시 컷 전환 (블렌딩 시간 0초)
//                          * 숫자(예: 0.5, 1.5, 2.0)     : 지정한 초 동안 부드럽게 이동하며 전환
//                          * 생략 시                     : 씬의 CinemachineBrain에 설정된 Default Blend 유지
//   - easeStyle          : 블렌딩 방식 [선택, 기본값 EaseInOut]
//                          * "EaseInOut", "Linear", "EaseIn", "EaseOut", "Cut" 등
//   - priority           : 대상 카메라에 부여할 우선순위 값 [선택, 기본값 15] (대상 외 카메라는 자동으로 0 설정)
//
// 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. 순식간에 즉시 컷 전환 (0초 즉시 전환) ★질문하신 기능:
//      CinemachineFocus(Wide_Cam, 0);
//      // 또는 키워드 사용:
//      CinemachineFocus(Wide_Cam, "cut");
//
//   2. 0.5초 동안 빠르게 앵글 전환:
//      CinemachineFocus(Janghwa_Cam, 0.5);
//
//   3. 1.5초 동안 영화처럼 천천히 부드럽게 시점 전환:
//      CinemachineFocus(Chona_Cam, 1.5);
//
//   4. Linear(등속) 블렌딩으로 1.0초 동안 전환:
//      CinemachineFocus(Wide_Cam, 1.0, "Linear");
//
//   5. 전환 시간 생략 시 (인스펙터의 CinemachineBrain 기본값 사용):
//      CinemachineFocus(Wide_Cam);
//
//   6. 즉시 컷 전환 후 캐릭터가 화면 밖에서 걸어 들어오는 연출:
//      CinemachineFocus(Wide_Cam, "cut");
//      SpineTRS(Janghwa, -10, 0, 0, 0);
//      SpineMoveTo(Janghwa, -5, 0, 1.5);
//
// 스킵(Skip) 동작 특성:
//   - 시점 우선순위와 블렌드 모드를 즉각 변경하는 0초 즉발 커맨드이므로 대화 흐름을 지연시키지 않습니다.
//   - 대사를 스킵하더라도 지정된 카메라 앵글이 오차 없이 즉시 활성화됩니다.
// =================================================================================================

using System;
using System.Globalization;
using UnityEngine;
using Unity.Cinemachine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

/// <summary>
/// 특정 Cinemachine 가상 카메라로 순식간에 컷 전환하거나 시간 지정 블렌딩 전환을 수행하는 시퀀서 커맨드.
/// 예: CinemachineFocus(Wide_Cam, 0), CinemachineFocus(Wide_Cam, "cut"), CinemachineFocus(Wide_Cam, 1.2)
/// </summary>
public class SequencerCommandCinemachineFocus : SequencerCommand
{
    private static CinemachineBlendDefinition? cachedOriginalBlend = null;

    void Start()
    {
        string targetName = GetParameter(0);
        string param1 = GetParameter(1);
        string param2 = GetParameter(2);
        string param3 = GetParameter(3);

        if (string.IsNullOrEmpty(targetName))
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning("CinemachineFocus: 대상 카메라(vcamName) 파라미터가 비어 있습니다.");
            Stop();
            return;
        }

        float? blendDuration = null;
        CinemachineBlendDefinition.Styles blendStyle = CinemachineBlendDefinition.Styles.EaseInOut;
        bool isCut = false;
        int activePriority = 15;

        // Parse optional parameters
        int numParams = Parameters != null ? Parameters.Length : 0;
        for (int i = 1; i < numParams; i++)
        {
            string raw = Parameters[i];
            if (string.IsNullOrEmpty(raw)) continue;
            string clean = CleanParam(raw).ToLowerInvariant();

            // 1. Cut keywords
            if (clean == "cut" || clean == "instant" || clean == "snap" || clean == "none")
            {
                isCut = true;
                blendDuration = 0f;
                continue;
            }

            // 2. Blend Styles
            if (TryGetBlendStyle(clean, out var parsedStyle))
            {
                blendStyle = parsedStyle;
                if (blendStyle == CinemachineBlendDefinition.Styles.Cut)
                {
                    isCut = true;
                    blendDuration = 0f;
                }
                continue;
            }

            // 3. Explicit priority prefix
            if (clean.StartsWith("priority:") || clean.StartsWith("p:"))
            {
                int colonIdx = clean.IndexOf(':');
                if (int.TryParse(clean.Substring(colonIdx + 1), out int pVal))
                {
                    activePriority = pVal;
                }
                continue;
            }

            // 4. Numeric value: could be duration (0, 0.5, 1.2) or legacy priority
            if (float.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out float num))
            {
                if (!blendDuration.HasValue && !isCut)
                {
                    if (num == 0f)
                    {
                        isCut = true;
                        blendDuration = 0f;
                    }
                    else if (num <= 5f || clean.Contains(".") || i + 1 < numParams)
                    {
                        blendDuration = num;
                    }
                    else
                    {
                        // Integer > 5 without decimal and no further params: likely priority in legacy usage
                        activePriority = (int)num;
                    }
                }
                else
                {
                    activePriority = (int)num;
                }
            }
        }

        // Apply blend setting to CinemachineBrain
        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain == null)
        {
            brain = UnityEngine.Object.FindAnyObjectByType<CinemachineBrain>();
        }

        if (brain != null && !cachedOriginalBlend.HasValue)
        {
            cachedOriginalBlend = brain.DefaultBlend;
        }

        // Find target camera first
        var allVcams = UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        CinemachineCamera targetVcam = null;
        for (int i = 0; i < allVcams.Length; i++)
        {
            if (allVcams[i] != null && allVcams[i].gameObject.name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            {
                targetVcam = allVcams[i];
                break;
            }
        }

        if (targetVcam == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"CinemachineFocus: '{targetName}' 카메라를 찾을 수 없습니다.");
            Stop();
            return;
        }

        // 1. Ensure target camera is active and enabled
        targetVcam.gameObject.SetActive(true);
        targetVcam.enabled = true;
        targetVcam.StandbyUpdate = CinemachineCamera.StandbyUpdateMode.Always;
        targetVcam.Priority = activePriority;

        // 2. Lower all other cameras' priority to 0
        for (int i = 0; i < allVcams.Length; i++)
        {
            if (allVcams[i] != null && allVcams[i] != targetVcam)
            {
                allVcams[i].StandbyUpdate = CinemachineCamera.StandbyUpdateMode.Always;
                allVcams[i].Priority = 0;
            }
        }

        // 3. Force Cinemachine to immediately re-sort the priority queue on this exact frame!
        // (Without this, Cinemachine maintains a stale queue until the next frame's Update(), causing a 1-frame visual glitch/flicker)
        targetVcam.Prioritize();

        // 4. Force calculate target camera state right now so its position, rotation, and lens are fully computed
        targetVcam.UpdateCameraState(Vector3.up, -1f);

        // 5. Apply instant cut or smooth blend to CinemachineBrain
        if (brain != null)
        {
            if (isCut)
            {
                // --- INSTANT CUT (0초 즉시 전환) ---
                // a) 진행 중이던 기존 블렌드가 있다면 즉시 중단
                brain.ActiveBlend = null;

                // b) RootFrame을 초기화하여 이전 카메라 이력 없이 새 카메라를 즉시 채택
                brain.ResetState();

                // c) DefaultBlend를 Cut (0초)으로 설정
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

                // d) 메인 카메라의 Transform 및 Lens를 대상 가상 카메라의 계산된 상태로 즉시 스냅(동기화)
                //    -> LateUpdate 이전 렌더링 또는 프레임 평가 타이밍 차이로 인한 1프레임 공백/깜빡임을 100% 원천 차단
                if (brain.OutputCamera != null)
                {
                    var finalPos = targetVcam.State.GetFinalPosition();
                    var finalRot = targetVcam.State.GetFinalOrientation();
                    brain.OutputCamera.transform.SetPositionAndRotation(finalPos, finalRot);

                    if (brain.OutputCamera.orthographic)
                    {
                        brain.OutputCamera.orthographicSize = targetVcam.State.Lens.OrthographicSize;
                    }
                    else
                    {
                        brain.OutputCamera.fieldOfView = targetVcam.State.Lens.FieldOfView;
                    }
                }
            }
            else if (blendDuration.HasValue && blendDuration.Value > 0f)
            {
                // 시간 지정 부드러운 전환 (Ease / Linear 등)
                brain.DefaultBlend = new CinemachineBlendDefinition(blendStyle, blendDuration.Value);
            }
            else
            {
                // 전환 시간 파라미터가 생략된 경우: 씬 원래의 DefaultBlend 복원
                if (cachedOriginalBlend.HasValue)
                {
                    brain.DefaultBlend = cachedOriginalBlend.Value;
                }
            }
        }

        Stop();
    }

    private static string CleanParam(string str)
    {
        if (string.IsNullOrEmpty(str)) return string.Empty;
        return str.Trim(' ', '\"', '\'', '\t', '\r', '\n');
    }

    private static bool TryGetBlendStyle(string str, out CinemachineBlendDefinition.Styles style)
    {
        style = CinemachineBlendDefinition.Styles.EaseInOut;
        string clean = str.Replace("_", "").Replace(" ", "").Replace("-", "");
        switch (clean)
        {
            case "cut":
                style = CinemachineBlendDefinition.Styles.Cut;
                return true;
            case "easeinout":
            case "inout":
            case "smooth":
                style = CinemachineBlendDefinition.Styles.EaseInOut;
                return true;
            case "easein":
            case "in":
                style = CinemachineBlendDefinition.Styles.EaseIn;
                return true;
            case "easeout":
            case "out":
                style = CinemachineBlendDefinition.Styles.EaseOut;
                return true;
            case "linear":
                style = CinemachineBlendDefinition.Styles.Linear;
                return true;
            case "hardin":
                style = CinemachineBlendDefinition.Styles.HardIn;
                return true;
            case "hardout":
                style = CinemachineBlendDefinition.Styles.HardOut;
                return true;
            default:
                return false;
        }
    }
}