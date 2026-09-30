// =================================================================================================
// [SpineTRS / SpineTransform / SpineMoveRotate]
// Move(이동), Rotate(회전), Scale(크기)의 TRS 변형을 한 번에 동시에 보간 변형하는 만능 시퀀서 커맨드.
// CharacterRootController의 통합 액션 큐(actionQueue)에 등록되어 순차적으로 실행됩니다.
//
// 문법:
//   SpineTRS(actor, x, y, angle, [duration], [scale], [pivot], [easeType], [bounceHeight], [autoFlip], [squash], [flipDuration])
//   SpineTransform(...) 및 SpineMoveRotate(...)로도 동일하게 사용 가능합니다.
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
//      SpineTRS(Chona, 2.5, 0, 15);
//
//   2. 몸을 뒤집지 않고 뒷걸음질하듯 이동 (autoFlip = false 또는 "noflip"):
//      SpineTRS(Chona, -3.0, 0, 10, 0.8, false);
//      SpineTRS(Chona, -3.0, 0, 10, 0.8, "noflip");
//
//   3. 빠르게 이동하며 회전 (0.4초 + 감속 EaseOut):
//      SpineTRS(Chona, 2.5, 0, -10, 0.4, "EaseOut");
//
//   4. 발바닥을 축으로 자연스럽게 기울이며 이동 (피벗 "0.5, 0", 탄력 EaseOutBack) [추천]:
//      SpineTRS(Chona, 3, 0, 20, 0.8, "0.5, 0", "EaseOutBack");
//
//   5. 이동 + 회전 + 크기 확대 (완전한 TRS 연출, 1.2배 확대):
//      SpineTRS(Chona, 1.5, 0, 10, 0.6, 1.2, "0.5, 0", "EaseOut");
//
//   6. 통통 튀면서 회전 이동 (바운스 높이 0.25):
//      SpineTRS(Chona, 4, 0, -15, 1.2, "0.5, 0", 0.25);
//
//   7. 즉시 원래 위치/각도(0도)로 복귀 (duration 0):
//      SpineTRS(Chona, 0, 0, 0, 0);
//
//   7. 다른 커맨드와 조합 (카메라 줌인 + 캐릭터 이동/회전 후 원위치):
//      CinemachineZoom(4.0, 0.8, "EaseOut");
//      SpineTRS(Chona, 1.5, 0, 10, 0.8, "0.5, 0", "EaseOut");
//      required SpineTRS(Chona, 1.5, 0, 0, 0.5, "EaseOut")@1.0;
//
// 스킵(Skip) 동작 특성:
//   - 스킵 시 CancelOrSnapAction(actionId)을 통해 목표 위치, 각도(0도 복귀 시 완벽한 원점 복귀),
//     스케일로 즉시 칼같이 스냅(Snap)되어 오차가 누적되지 않습니다.
// =================================================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

/// <summary>
/// Move(이동), Rotate(회전), Scale(크기)을 동시에 보간 변형하는 만능 시퀀서 커맨드.
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

        // Defaults for optional parameters
        float duration = 1.0f;
        Vector2? targetScale2D = null;
        Vector2 normalizedPivot = new Vector2(0.5f, 0.5f);
        EaseType easeType = EaseType.EaseInOut;
        float bounceHeight = 0f;
        bool autoFlip = true;
        float squash = 0f;
        float flipDuration = 0f;

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

            // 4. "x,y" Vector check (Scale vs Pivot)
            if (TryParseVector2(tok, out Vector2 v2))
            {
                // Explicit prefix
                if (tok.StartsWith("scale:", StringComparison.OrdinalIgnoreCase) || tok.StartsWith("s:", StringComparison.OrdinalIgnoreCase))
                {
                    targetScale2D = v2;
                    scaleSet = true;
                    continue;
                }
                if (tok.StartsWith("pivot:", StringComparison.OrdinalIgnoreCase) || tok.StartsWith("p:", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedPivot = v2;
                    pivotSet = true;
                    continue;
                }

                // If scale is not yet set, and there is another pivot token later or pivot was already set, this is scale
                if (!scaleSet && (pivotSet || HasAnotherPivotToken(tokens, i)))
                {
                    targetScale2D = v2;
                    scaleSet = true;
                    continue;
                }

                // Otherwise, treat as pivot
                normalizedPivot = v2;
                pivotSet = true;
                continue;
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
                        targetScale2D = new Vector2(val, val);
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
                    targetScale2D = new Vector2(val, val);
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
            Debug.Log($"[SpineTRS] Subject: '{subject.name}', TargetPos: ({targetX}, {targetY}), Angle: {targetAngle}, Duration: {duration}, Scale: {(targetScale2D.HasValue ? targetScale2D.Value.ToString() : "none")}, Pivot: ({normalizedPivot.x}, {normalizedPivot.y}), Ease: {easeType}");
        }

        Vector3 targetPos = new Vector3(targetX, targetY, subject.position.z);
        Vector3? scaleVec = targetScale2D.HasValue ? new Vector3(targetScale2D.Value.x, targetScale2D.Value.y, subject.localScale.z) : (Vector3?)null;

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
        return s == "true" || s == "false" || s == "noflip" || s == "keepflip" || s == "keep" || s == "autoflip";
    }

    private static bool ParseBool(string str)
    {
        if (string.IsNullOrEmpty(str)) return true;
        string s = CleanParam(str).ToLowerInvariant();
        return s == "true" || s == "autoflip";
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
            case "linear": return EaseType.Linear;
            case "easein": return EaseType.EaseIn;
            case "easeout": return EaseType.EaseOut;
            case "easeinout": return EaseType.EaseInOut;
            case "easeoutback": return EaseType.EaseOutBack;
            case "easeinback": return EaseType.EaseInBack;
            default: return defaultType;
        }
    }

    private static bool HasAnotherPivotToken(List<string> tokens, int currentIndex)
    {
        for (int j = currentIndex + 1; j < tokens.Count; j++)
        {
            string t = tokens[j];
            if (string.IsNullOrEmpty(t)) continue;
            if (t.StartsWith("pivot:", StringComparison.OrdinalIgnoreCase) || t.StartsWith("p:", StringComparison.OrdinalIgnoreCase))
                return true;
            if (TryGetNamedPivot(t, out _))
                return true;
            if (t.Contains(","))
                return true;
        }
        return false;
    }

    private static bool TryParseVector2(string str, out Vector2 result)
    {
        result = Vector2.zero;
        if (string.IsNullOrEmpty(str) || !str.Contains(",")) return false;
        string clean = CleanParam(str);
        if (clean.StartsWith("scale:", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring("scale:".Length).Trim();
        else if (clean.StartsWith("s:", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring("s:".Length).Trim();
        else if (clean.StartsWith("pivot:", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring("pivot:".Length).Trim();
        else if (clean.StartsWith("p:", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring("p:".Length).Trim();

        string[] parts = clean.Split(',');
        if (parts.Length >= 2 &&
            float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
            float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
        {
            result = new Vector2(x, y);
            return true;
        }
        return false;
    }
}
