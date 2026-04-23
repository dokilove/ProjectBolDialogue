using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;

public class CameraAutoMatcher : MonoBehaviour
{
    [System.Serializable]
    public struct CameraMapping
    {
        public string characterName;
        public CinemachineCamera vcam;
    }

    public List<CameraMapping> cameraMappings;
    public int activePriority = 20;
    public int inactivePriority = 10;
    public string defaultCameraName = "Default";

    private string currentSpeaker = "";

    // 대화가 진행 중일 때 매 프레임 체크 (첫 줄 씹힘 방지용)
    void Update()
    {
        if (DialogueManager.isConversationActive)
        {
            var state = DialogueManager.currentConversationState;
            if (state != null && state.subtitle != null)
            {
                string speaker = state.subtitle.speakerInfo.nameInDatabase;

                // 화자가 바뀌었을 때만 카메라 업데이트 실행
                if (currentSpeaker != speaker)
                {
                    currentSpeaker = speaker;
                    Debug.Log($"<color=cyan>[CameraAutoMatcher]</color> 실시간 감지: {speaker}");
                    FocusCamera(speaker);
                }
            }
        }
        else if (currentSpeaker != "")
        {
            currentSpeaker = ""; // 대화 종료 시 초기화
        }
    }

    // 기존 OnConversationLine도 안전장치로 유지
    public void OnConversationLine(Subtitle subtitle)
    {
        if (subtitle != null) FocusCamera(subtitle.speakerInfo.nameInDatabase);
    }

    public void FocusCamera(string characterName)
    {
        if (string.IsNullOrEmpty(characterName)) return;

        bool found = false;
        foreach (var mapping in cameraMappings)
        {
            if (mapping.vcam == null) continue;

            if (mapping.characterName == characterName)
            {
                mapping.vcam.Priority = activePriority;
                found = true;
            }
            else
            {
                mapping.vcam.Priority = inactivePriority;
            }
        }
        if (!found) FocusDefault();
    }

    private void FocusDefault()
    {
        foreach (var mapping in cameraMappings)
        {
            if (mapping.vcam == null) continue;
            mapping.vcam.Priority = (mapping.characterName == defaultCameraName) ? activePriority : inactivePriority;
        }
    }
}