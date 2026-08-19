#if UNITY_2021_1_OR_NEWER
// CustomDialogueUI.cs
// Dialogue Entry의 커스텀 필드 "SubtitlePanel"(Number)을 읽어
// 해당 인덱스의 패널을 사용합니다.
// 필드가 없거나 0이면 기본 NPC/PC 패널로 fallback합니다.

using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.UIToolkit;

public class CustomDialogueUI : UIToolkitDialogueUI
{
    // 커스텀 필드 이름 (Dialogue Database Template의 필드명과 일치해야 함)
    private const string PanelFieldName = "SubtitlePanel";

    protected override UIToolkitSubtitleElements GetSubtitlePanel(Subtitle subtitle)
    {
        if (subtitle == null) return null;

        // Dialogue Entry의 커스텀 필드 "SubtitlePanel" 확인
        var entry = subtitle.dialogueEntry;
        if (entry != null)
        {
            var fieldValue = Field.LookupValue(entry.fields, PanelFieldName);
            if (!string.IsNullOrEmpty(fieldValue) && int.TryParse(fieldValue, out int panelIndex) && panelIndex >= 0)
            {
                var panel = GetSubtitlePanel(panelIndex);
                if (panel != null) return panel;
            }
        }

        // 커스텀 필드가 없거나 유효하지 않으면 기본 로직 사용 (NPC/PC 자동 분기)
        return base.GetSubtitlePanel(subtitle);
    }

    // 기본 구현은 항상 npcSubtitleControls / pcSubtitleControls 에만 ShowContinueButton을 호출하므로,
    // 커스텀 패널(index 2+)에는 버튼이 표시되지 않는 문제를 수정합니다.
    public override void ShowContinueButton(Subtitle subtitle)
    {
        var panel = GetSubtitlePanel(subtitle);
        if (panel != null)
            panel.ShowContinueButton();
        else
            base.ShowContinueButton(subtitle);
    }

    public override void HideSubtitle(Subtitle subtitle)
    {
        var panel = GetSubtitlePanel(subtitle);
        if (panel != null && !panel.ShouldStayVisible)
            panel.Hide();
    }
}
#endif
