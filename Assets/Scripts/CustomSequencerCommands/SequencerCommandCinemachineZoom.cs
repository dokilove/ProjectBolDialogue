// Syntax: CinemachineZoom(targetSize, [duration], [vcamName], [easeType])
// Syntax: CinemachineZoom(targetSize, [duration], [easeType])
// Syntax: OrthographicZoom(targetSize, [duration], [vcamName], [easeType])
// targetSize: Target Orthographic Size (Smaller = Zoom In, Larger = Zoom Out)
// duration: Time in seconds to interpolate zoom (default 0 = instant)
// vcamName: Name of target CinemachineCamera (optional; if omitted, applies to all active Cinemachine cameras)
// easeType: Linear, EaseIn, EaseOut, EaseInOut, EaseOutBack, EaseInBack (default EaseInOut)

using System;
using System.Collections;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

public class SequencerCommandCinemachineZoom : SequencerCommand
{
    private bool isDone = false;

    void Start()
    {
        float targetSize = GetParameterAsFloat(0, 5f);
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

        CinemachineZoomController.Instance.EnqueueZoom(targetSize, duration, vcamName, easeType, () => isDone = true);
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
