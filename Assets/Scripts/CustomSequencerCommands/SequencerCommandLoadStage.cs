// =================================================================================================
// [LoadStage]
// 다음 대화에서 사용할 동적 스테이지를 예약하는 시퀀서 커맨드.
//
// 문법:
//   LoadStage(stageId)
//
// 동작:
//   - 대화 중에 실행되면 현재 대화가 끝난 뒤, 다음 대화 시작 시 스테이지를 교체합니다.
//     (DialogueWorldState / DialogueCameraState의 백로그 스냅샷이 오브젝트 참조를 들고 있으므로
//      대화 중 교체는 안전하지 않습니다.)
//   - 대화 밖에서 실행되면 즉시 빌드합니다.
//
// 예:
//   LoadStage(Stage_SampleDialogue);
//   LoadStage(TownSquare);
// =================================================================================================

using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

/// <summary>
/// 다음 대화의 동적 스테이지를 예약하는 시퀀서 커맨드.
/// 예: LoadStage(Stage_SampleDialogue)
/// </summary>
public class SequencerCommandLoadStage : SequencerCommand
{
    private void Start()
    {
        string stageId = GetParameter(0);

        if (string.IsNullOrWhiteSpace(stageId))
        {
            if (DialogueDebug.logWarnings)
            {
                Debug.LogWarning("LoadStage: stageId 파라미터가 비어 있습니다.");
            }

            Stop();
            return;
        }

        var builder = DialogueStageBuilder.Current;
        if (builder == null)
        {
            if (DialogueDebug.logWarnings)
            {
                Debug.LogWarning($"LoadStage: 씬에 DialogueStageBuilder가 없어 '{stageId}'를 예약하지 못했습니다.");
            }

            Stop();
            return;
        }

        builder.RequestStage(stageId);
        Stop();
    }
}
