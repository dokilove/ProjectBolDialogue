// Syntax: SpineRotate(actor, angle, [duration], [pivotX], [pivotY], [easeType])
// Syntax: SpineRotate(actor, angle, [duration], [pivotString], [easeType]) e.g., SpineRotate(Chona, 15, 1.0, "0.5, 0.0", "EaseOut")
// Syntax: SpineRotate(actor, angle, [duration], [easeType]) e.g., SpineRotate(Chona, 15, 1.0, "EaseOut")
// actor: Target actor / transform (e.g. Chona, speaker, listener)
// angle: Target rotation angle in degrees (Z-axis)
// duration: Rotation transition time in seconds (default 0 = instant)
// pivot: Normalized pivot ratio (0.0 ~ 1.0, default 0.5, 0.5)
// easeType: Linear, EaseIn, EaseOut, EaseInOut, EaseOutBack, EaseInBack (default EaseInOut)

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

public class SequencerCommandSpineRotate : SequencerCommand
{
    private bool isDone = false;
    private CharacterRootController controller;

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
            controller.EnqueueRotate(targetAngle, 0f, normalizedPivot, null, easeType);
            Stop();
        }
        else
        {
            controller.EnqueueRotate(targetAngle, duration, normalizedPivot, () => isDone = true, easeType);
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
        if (!isDone && controller != null)
        {
            controller.SkipAllRotations();
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
