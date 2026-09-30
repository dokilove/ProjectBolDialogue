// =================================================================================================
// [SpineTRS / SpineTransform / SpineMoveRotate]
// Move(이동), Rotate(회전), Scale(크기)을 TRS 순서로 한 번에 동시에 보간 변환하는 시퀀서 커맨드
//
// ■ 문법:
//   SpineTRS(actor, x, y, angle, [duration], [scale], [pivot], [easeType], [bounceHeight], [autoFlip], [squash], [flipDuration])
//   SpineTransform(...) 및 SpineMoveRotate(...)로도 동일하게 사용 가능합니다.
//
// ■ 파라미터 설명:
//   - actor       : 대상 캐릭터 (예: Chona, speaker, listener) [필수]
//   - x, y        : 목표 월드 좌표 (Z축은 기존 유지) [필수]
//   - angle       : 목표 회전 각도 (Z축 도 단위, 예: 15, -10, 0) [필수]
//   - duration    : 이동 및 회전 소요 시간(초, 기본값 1.0, 0 = 즉시 스냅)
//   - scale       : 목표 크기 배율 (생략 시 현재 크기 유지, 예: 1.2, 0.8)
//   - pivot       : 정규화 회전 피벗 (예: "0.5, 0.0" = 발바닥 중심, "0.5, 0.5" = 몸 중심)
//   - easeType    : Linear, EaseIn, EaseOut, EaseInOut, EaseOutBack, EaseInBack (기본값 EaseInOut)
//   - bounceHeight: 이동 중 통통 튀는 높이 (기본값 0)
//   - autoFlip    : 이동 방향에 따라 좌우 자동 반전 (기본값 true)
//
// ■ 상황별 실전 예시 (Sequence 필드에 복사해서 사용 가능):
//   1. 가장 단순한 이동 + 기울이기 (1초 기본):
//      SpineTRS(Chona, 2.5, 0, 15)
//
//   2. 빠르게 휙 이동하며 회전 (0.4초 + 감속 EaseOut):
//      SpineTRS(Chona, 2.5, 0, -10, 0.4, "EaseOut")
//
//   3. 발바닥을 축으로 자연스럽게 기울어지며 이동 (피벗 "0.5, 0", 탄력 EaseOutBack) ★추천:
//      SpineTRS(Chona, 3, 0, 20, 0.8, "0.5, 0", "EaseOutBack")
//
//   4. 이동 + 회전 + 크기 확대 (완전한 TRS 연출, 1.2배 확대):
//      SpineTRS(Chona, 1.5, 0, 10, 0.6, 1.2, "0.5, 0", "EaseOut")
//
//   5. 통통 튀면서 회전 이동 (바운스 높이 0.25):
//      SpineTRS(Chona, 4, 0, -15, 1.2, "0.5, 0", 0.25)
//
//   6. 즉시 원래 위치/각도(0도)로 복귀 (duration 0):
//      SpineTRS(Chona, 0, 0, 0, 0)
//
//   7. 다른 커맨드와 조합 (카메라 줌인 + 캐릭터 이동/회전 후 원위치):
//      CinemachineZoom(4.0, 0.8, "EaseOut");
//      SpineTRS(Chona, 1.5, 0, 10, 0.8, "0.5, 0", "EaseOut");
//      SpineTRS(Chona, 1.5, 0, 0, 0.5, "EaseOut")@1.0;
// =================================================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

/// <summary>
/// Move(이동), Rotate(회전), Scale(크기)을 TRS 순서로 동시에 보간 변환하는 Dialogue System 시퀀서 커맨드.
/// 예: SpineTRS(Chona, 3, 0, 20, 0.8, "0.5, 0", "EaseOutBack")
/// </summary>
public class SequencerCommandSpineTRS : SequencerCommand
{
    private bool isDone = false;
    private CharacterRootController controller;
    private int actionId = -1;

    void Start()
    {
        Transform subject = GetSubject(0);
        if (subject == null)
        {
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineTRS: Subject '{GetParameter(0)}' not found.");
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
            if (DialogueDebug.logWarnings) Debug.LogWarning($"SpineTRS: CharacterRootController not found on subject '{subject.name}'.");
            Stop();
            return;
        }

        float targetX = GetParameterAsFloat(1, subject.position.x);
        float targetY = GetParameterAsFloat(2, subject.position.y);
        float targetAngle = GetParameterAsFloat(3, 0f);
        float duration = 1.0f;
        float? targetScale = null;
        Vector2 normalizedPivot = new Vector2(0.5f, 0.5f);
        EaseType easeType = EaseType.EaseInOut;
        float bounceHeight = 0f;
        bool autoFlip = true;
        float squash = 0.06f;
        float flipDuration = 0f;

        // Parse optional parameters from index 4 onwards
        // Note: Dialogue System sequence parser splits parameters on every comma, even inside quotes!
        // E.g. "0, 0" becomes two parameters: "\"0" and "0\"". We stitch them back together and strip quotes.
        List<string> tokens = new List<string>();
        int numParams = Parameters != null ? Parameters.Length : 0;
        for (int i = 4; i < numParams; i++)
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
        bool scaleSet = false;
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

            // 2. Bool check (autoFlip)
            if (IsBoolString(tok))
            {
                autoFlip = ParseBool(tok);
                continue;
            }

            // 3. Named Pivot check (e.g. "bottom", "feet", "center", "top", "bottomleft", "bottomright")
            if (TryGetNamedPivot(tok, out Vector2 namedP))
            {
                normalizedPivot = namedP;
                pivotSet = true;
                continue;
            }

            // 4. "x,y" Pivot check (e.g. "0,0", "1,0", "0.5,0")
            if (tok.Contains(","))
            {
                string[] parts = tok.Split(',');
                if (parts.Length >= 2 &&
                    float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float px) &&
                    float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float py))
                {
                    normalizedPivot = new Vector2(px, py);
                    pivotSet = true;
                    continue;
                }
            }

            // 5. Numeric values: duration, scale, pivot pair, or bounce
            if (float.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
            {
                if (!durationSet)
                {
                    duration = val;
                    durationSet = true;
                }
                else if (!pivotSet && i + 1 < tokens.Count &&
                         float.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float nextVal) &&
                         !IsEaseTypeString(tokens[i + 1]) && !IsBoolString(tokens[i + 1]) && !tokens[i + 1].Contains(","))
                {
                    if (!scaleSet && i + 2 < tokens.Count && float.TryParse(tokens[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        targetScale = val;
                        scaleSet = true;
                    }
                    else
                    {
                        normalizedPivot = new Vector2(val, nextVal);
                        pivotSet = true;
                        i++; // Consume next token
                    }
                }
                else if (!scaleSet)
                {
                    targetScale = val;
                    scaleSet = true;
                }
                else if (bounceHeight == 0f)
                {
                    bounceHeight = val;
                }
                else
                {
                    squash = val;
                }
            }
        }

        if (DialogueDebug.logInfo)
        {
            Debug.Log($"[SpineTRS] Subject: '{subject.name}', TargetPos: ({targetX}, {targetY}), Angle: {targetAngle}, Duration: {duration}, Scale: {(targetScale.HasValue ? targetScale.Value.ToString() : "none")}, Pivot: ({normalizedPivot.x}, {normalizedPivot.y}), Ease: {easeType}");
        }

        Vector3 targetPos = new Vector3(targetX, targetY, subject.position.z);
        Vector3? scaleVec = targetScale.HasValue ? new Vector3(targetScale.Value, targetScale.Value, subject.localScale.z) : (Vector3?)null;

        if (duration <= 0f)
        {
            actionId = controller.TransformTo(targetPos, targetAngle, scaleVec, 0f, normalizedPivot, bounceHeight, squash, autoFlip, flipDuration, easeType, null);
            Stop();
        }
        else
        {
            actionId = controller.EnqueueTransform(targetPos, targetAngle, scaleVec, duration, normalizedPivot, bounceHeight, squash, autoFlip, flipDuration, easeType, () => isDone = true);
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

    private static bool IsBoolString(string str)
    {
        if (string.IsNullOrEmpty(str)) return false;
        string s = CleanParam(str).ToLowerInvariant();
        return s == "true" || s == "false";
    }

    private static bool ParseBool(string str)
    {
        if (string.IsNullOrEmpty(str)) return true;
        return CleanParam(str).ToLowerInvariant() == "true";
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
        string clean = CleanParam(str).ToLowerInvariant().Replace("_", "").Replace(" ", "");
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
