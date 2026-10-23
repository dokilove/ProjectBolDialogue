using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 ID로 DialogueStageDefinition을 찾기 위한 목록.
/// 시퀀서 커맨드 LoadStage(stageId)에서 사용합니다.
/// </summary>
[CreateAssetMenu(menuName = "BolDialogue/Stage Database", fileName = "DialogueStageDatabase", order = 1)]
public class DialogueStageDatabase : ScriptableObject
{
    [Tooltip("스테이지 ID로 검색할 정의 목록")]
    public List<DialogueStageDefinition> stages = new List<DialogueStageDefinition>();

    /// <summary>
    /// stageId 또는 에셋 이름으로 스테이지를 찾습니다. 대소문자를 구분하지 않습니다.
    /// </summary>
    public DialogueStageDefinition Find(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId) || stages == null) return null;

        for (int i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            if (stage == null) continue;

            if (string.Equals(stage.stageId, stageId, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stage.name, stageId, System.StringComparison.OrdinalIgnoreCase))
            {
                return stage;
            }
        }

        return null;
    }
}
