using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대사 엔트리가 시작되기 직전의 캐릭터/카메라 월드 상태 스냅샷.
/// 백로그로 되감을 때 이 상태로 복원한 뒤 Dialogue System이 해당 대사 시퀀스를
/// 다시 재생하므로, 이동/카메라 연출이 원래 시작 상태에서 재현됩니다.
/// </summary>
public sealed class DialogueWorldState
{
    private struct CharacterEntry
    {
        public CharacterRootController Controller;
        public CharacterRootController.CharacterState State;
    }

    private readonly List<CharacterEntry> characters = new List<CharacterEntry>();
    private DialogueCameraState cameraState;

    public static DialogueWorldState Capture()
    {
        var result = new DialogueWorldState();
        var controllers = Object.FindObjectsByType<CharacterRootController>(FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            var controller = controllers[i];
            if (controller == null) continue;

            result.characters.Add(new CharacterEntry
            {
                Controller = controller,
                State = controller.CaptureState()
            });
        }

        result.cameraState = DialogueCameraState.Capture();
        return result;
    }

    /// <summary>
    /// 대사가 넘어갈 때 이전 대사의 시퀀스가 중간에 끊기면, 남아 있던 이동/회전/줌 액션은
    /// Sequencer가 커맨드를 파괴하는 "다음 프레임"에야 최종 목표값으로 스냅됩니다.
    /// 그 전에 스냅샷을 찍으면 보간 도중의 중간값이 저장되므로,
    /// 스냅샷 직전에 동일한 스냅을 미리 적용해 "대사 시작 시점의 정착된 상태"를 저장합니다.
    /// (정상적으로 시퀀스가 끝난 경우에는 남은 액션이 없어 아무 일도 하지 않습니다.)
    /// </summary>
    public static void SettlePendingActions()
    {
        var zoom = CinemachineZoomController.InstanceOrNull;
        if (zoom != null)
        {
            zoom.SkipAllZooms();
        }

        var controllers = Object.FindObjectsByType<CharacterRootController>(FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
            {
                controllers[i].SkipAllActions();
            }
        }

        // 깊이/정렬 보간은 CharacterRootController 액션 큐가 아니라
        // SpineVisualContainerController 자체 코루틴으로 돌기 때문에 따로 정착시킨다.
        var visuals = Object.FindObjectsByType<SpineVisualContainerController>(FindObjectsSortMode.None);

        for (int i = 0; i < visuals.Length; i++)
        {
            if (visuals[i] != null)
            {
                visuals[i].FinishDepthAnimation();
            }
        }
    }

    public static void CancelAllActions()
    {
        var controllers = Object.FindObjectsByType<CharacterRootController>(FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
            {
                controllers[i].CancelAllActions();
            }
        }
    }

    public void Restore()
    {
        for (int i = 0; i < characters.Count; i++)
        {
            var entry = characters[i];
            if (entry.Controller != null)
            {
                entry.Controller.RestoreState(entry.State);
            }
        }

        if (cameraState != null)
        {
            cameraState.Restore();
        }
    }
}
