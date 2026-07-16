using UnityEngine;
using Unity.Cinemachine; // Cinemachine 3.x 네임스페이스
using PixelCrushers.DialogueSystem;
using System.Collections.Generic;
using System.Linq;
using PixelCrushers.DialogueSystem.SequencerCommands;

// 사용법: CinemachineFocus(vcamName)
// 지정한 vcam만 Priority를 올리고, 씬의 다른 모든 CinemachineCamera는 0으로 내림
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