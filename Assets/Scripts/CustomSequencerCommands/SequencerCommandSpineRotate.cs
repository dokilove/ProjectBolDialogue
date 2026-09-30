// =================================================================================================
// [SpineRotate]
// 캐릭터의 비주얼을 지정한 각도(angle)로 회전(기울임)시키는 시퀀서 커맨드.
// 피벗(회전 중심축)과 이징(EaseType)을 자유롭게 지정할 수 있으며,
// CharacterRootController의 통합 액션 큐(actionQueue)에 등록되어 이동/반전과 순차적으로 실행됩니다.
//
// 문법:
//   SpineRotate(actor, angle, [duration], [pivot], [easeType])
//   - pivot 파라미터는 "bottom", "center" 같은 키워드 또는 "0.5, 0.0" 형태의 좌표 문자열을 지원합니다.
//   - duration, pivot, easeType은 순서에 구애받지 않고 스마트하게 자동 인식됩니다.
//
// 파라미터 설명:
//   - actor       : 대상 캐릭터 [필수] (예: Janghwa, Chona, speaker, listener)
//   - angle       : 목표 회전 각도(Z축 도 단위) [필수] (예: 15 = 반시계 회전, -15 = 시계 회전, 0 = 직립)
//   - duration    : 회전 소요 시간(초) [선택, 기본값 0 = 즉시 회전]
//   - pivot       : 회전 중심 피벗 [선택, 기본값 "center" / (0.5, 0.5)]
//                   * 키워드 지원: "bottom" / "feet" (발바닥 중심), "center" / "middle" (중심),
//                                  "top" / "head" (머리), "bottomleft", "bottomright" 등
//                   * 직접 좌표 지정: "0.5, 0" (발바닥), "0.5, 1.0" (정수리) 등
//   - easeType    : Linear, EaseIn, EaseOut, EaseInOut, EaseOutBack, EaseInBack [선택, 기본값 EaseInOut]
//
// 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. 발바닥을 축으로 0.5초 동안 오른쪽으로 15도 기울이기 (인사, 갸우뚱, 놀람) [추천]:
//      SpineRotate(Chona, -15, 0.5, "bottom");
//
//   2. 탄력 있는 EaseOutBack 이징으로 0.6초 동안 발바닥 축 회전:
//      SpineRotate(Chona, 20, 0.6, "feet", "EaseOutBack");
//
//   3. 즉시 각도를 0도로 리셋하여 똑바로 서기 (duration 0):
//      SpineRotate(Chona, 0);
//
//   4. 중심(center) 축으로 0.3초 동안 회전:
//      SpineRotate(Chona, 10, 0.3, "center", "EaseOut");
//
//   5. 머리를 축(top)으로 시계추처럼 흔들리기:
//      SpineRotate(Chona, 15, 0.4, "top", "EaseInOut");
//
//   6. 좌표로 직접 피벗 지정 ("0.5, 0.0" = 발바닥 중심):
//      SpineRotate(Chona, -12, 0.8, "0.5, 0.0", "EaseInOut");
//
//   7. 이동과 연계하여 걷다가 멈추며 인사하기:
//      SpineMoveTo(Chona, 2.0, 0, 1.0);
//      SpineRotate(Chona, 15, 0.5, "bottom", "EaseOut");
//      required SpineRotate(Chona, 0, 0.3, "bottom")@1.6;
//
// 스킵(Skip) 동작 특성:
//   - 대화 스킵 시 CancelOrSnapAction(actionId)에 의해 목표 각도로 즉시 스냅(Snap)되며,
//     0도로 복귀하는 회전의 경우 Visual Container가 초기 기본 위치와 각도로 완전 무결하게 복구됩니다.
// =================================================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

/// <summary>
/// 캐릭터의 비주얼을 지정한 각도로 회전(기울임)시키는 시퀀서 커맨드.
/// 예: SpineRotate(Chona, 15, 0.5, "bottom", "EaseOutBack")
/// </summary>
public class SequencerCommandSpineRotate : SequencerCommand
{
    private bool isDone = false;
    private CharacterRootController controller;
    private int actionId = -1;

    void Start()
    {
        Transform subject = GetSubject(0);
        if (subject == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineRotate: Subject '{GetParameter(0)}' not found.");
            Stop();
            return;
        }

        controller = subject.GetComponent<CharacterRootController>();
        if (controller == null)
        {
            controller = subject.GetComponentInChildren<CharacterRootController>();
        }

        if (controller == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineRotate: CharacterRootController not found on subject '{subject.name}'.");
            Stop();
            return;
        }

        float targetAngle = GetParameterAsFloat(1, 0f);
        float duration = 0f;
        Vector2 normalizedPivot = new Vector2(0.5f, 0.5f);
        EaseType easeType = EaseType.EaseInOut;

        // Parse optional parameters from index 2 onwards: duration, pivot, easeType
        // Stitch together parameters that were split by comma inside quotes
        List<string> tokens = new List<string>();
        int numParams = Parameters != null ? Parameters.Length : 0;
        for (int i = 2; i < numParams; i++)
        {
            string raw = Parameters[i];
            if (string.IsNullOrEmpty(raw)) continue;
            raw = raw.Trim();

            if ((raw.StartsWith("\"") || raw.StartsWith("'")) && !(raw.EndsWith("\"") || raw.EndsWith("'")) && i + 1 < numParams)
            {
                string nextRaw = Parameters[i + 1].Trim();
                tokens.Add(CleanParam(raw) + "," + CleanParam(nextRaw));
                i++;
            }
            else
            {
                tokens.Add(CleanParam(raw));
            }
        }

        bool durationSet = false;
        bool pivotSet = false;

        for (int i = 0; i < tokens.Count; i++)
        {
            string tok = tokens[i];
            if (string.IsNullOrEmpty(tok)) continue;

            // 1. EaseType check
            if (IsEaseTypeString(tok))
            {
                easeType = ParseEaseType(tok);
                continue;
            }

            // 2. Named Pivot check
            if (TryGetNamedPivot(tok, out Vector2 namedP))
            {
                normalizedPivot = namedP;
                pivotSet = true;
                continue;
            }

            // 3. "x,y" Pivot check
            if (tok.Contains(","))
            {
                string[] parts = tok.Split(',');
                if (parts.Length >= 2 &&
                    float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float px) &&
                    float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float py))
                {
                    normalizedPivot = new Vector2(px, py);
                    pivotSet = true;
                    continue;
                }
            }

            // 4. Numeric: duration or pivot pair
            if (float.TryParse(tok, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float num))
            {
                if (!durationSet)
                {
                    duration = num;
                    durationSet = true;
                }
                else if (!pivotSet && i + 1 < tokens.Count &&
                         float.TryParse(tokens[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float nextNum) &&
                         !IsEaseTypeString(tokens[i + 1]))
                {
                    normalizedPivot = new Vector2(num, nextNum);
                    pivotSet = true;
                    i++;
                }
                else if (!pivotSet)
                {
                    normalizedPivot = new Vector2(num, normalizedPivot.y);
                }
            }
        }

        if (DialogueDebug.logInfo)
        {
            Debug.Log($"[SpineRotate] Subject: '{subject.name}', Angle: {targetAngle}, Duration: {duration}, Pivot: ({normalizedPivot.x}, {normalizedPivot.y}), Ease: {easeType}");
        }

        if (duration <= 0f)
        {
            actionId = controller.EnqueueRotate(targetAngle, 0f, normalizedPivot, null, easeType);
            Stop();
        }
        else
        {
            actionId = controller.EnqueueRotate(targetAngle, duration, normalizedPivot, () => isDone = true, easeType);
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
        if (!isDone && controller != null && actionId != -1)
        {
            controller.CancelOrSnapAction(actionId);
        }
    }

    private static string CleanParam(string str)
    {
        if (string.IsNullOrEmpty(str)) return string.Empty;
        return str.Trim(' ', '\"', '\'', '\t', '\r', '\n');
    }

    private static bool TryGetNamedPivot(string str, out Vector2 pivot)
    {
        pivot = new Vector2(0.5f, 0.5f);
        if (string.IsNullOrEmpty(str)) return false;
        string clean = CleanParam(str).ToLowerInvariant().Replace("_", "").Replace(" ", "").Replace("-", "");

        switch (clean)
        {
            case "bottom":
            case "feet":
            case "foot":
            case "down":
                pivot = new Vector2(0.5f, 0.0f);
                return true;
            case "bottomleft":
            case "leftbottom":
            case "bl":
            case "leftfoot":
                pivot = new Vector2(0.0f, 0.0f);
                return true;
            case "bottomright":
            case "rightbottom":
            case "br":
            case "rightfoot":
                pivot = new Vector2(1.0f, 0.0f);
                return true;
            case "top":
            case "head":
            case "up":
                pivot = new Vector2(0.5f, 1.0f);
                return true;
            case "topleft":
            case "lefttop":
            case "tl":
                pivot = new Vector2(0.0f, 1.0f);
                return true;
            case "topright":
            case "righttop":
            case "tr":
                pivot = new Vector2(1.0f, 1.0f);
                return true;
            case "center":
            case "middle":
            case "mid":
                pivot = new Vector2(0.5f, 0.5f);
                return true;
            case "left":
                pivot = new Vector2(0.0f, 0.5f);
                return true;
            case "right":
                pivot = new Vector2(1.0f, 0.5f);
                return true;
            default:
                return false;
        }
    }

    private static bool IsEaseTypeString(string str)
    {
        if (string.IsNullOrEmpty(str)) return false;
        string clean = CleanParam(str).ToLowerInvariant().Replace("_", "").Replace(" ", "");
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
