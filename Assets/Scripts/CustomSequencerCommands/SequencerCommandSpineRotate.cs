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
        float duration = GetParameterAsFloat(2, 0f);

        float pivotX = 0.5f;
        float pivotY = 0.5f;
        EaseType easeType = EaseType.EaseInOut;

        string param3 = GetParameter(3);
        string param4 = GetParameter(4);
        string param5 = GetParameter(5);

        if (!string.IsNullOrEmpty(param3) && IsEaseTypeString(param3))
        {
            easeType = ParseEaseType(param3);
        }
        else if (!string.IsNullOrEmpty(param3))
        {
            if (param3.Contains(","))
            {
                string[] parts = param3.Split(',');
                if (parts.Length >= 2)
                {
                    float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out pivotX);
                    float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out pivotY);
                }
                if (!string.IsNullOrEmpty(param4))
                {
                    easeType = ParseEaseType(param4);
                }
            }
            else
            {
                pivotX = GetParameterAsFloat(3, 0.5f);
                if (!string.IsNullOrEmpty(param4))
                {
                    if (IsEaseTypeString(param4))
                    {
                        easeType = ParseEaseType(param4);
                    }
                    else
                    {
                        pivotY = GetParameterAsFloat(4, 0.5f);
                        if (!string.IsNullOrEmpty(param5))
                        {
                            easeType = ParseEaseType(param5);
                        }
                    }
                }
            }
        }

        Vector2 normalizedPivot = new Vector2(pivotX, pivotY);

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
