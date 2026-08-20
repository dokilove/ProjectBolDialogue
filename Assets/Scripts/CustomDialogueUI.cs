#if UNITY_2021_1_OR_NEWER
// CustomDialogueUI.cs
// Dialogue Entry의 커스텀 필드 "SubtitlePanel"(Number)을 읽어
// 해당 인덱스의 패널을 사용합니다.
// 필드가 없거나 0이면 기본 NPC/PC 패널로 fallback합니다.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.UIToolkit;

public class CustomDialogueUI : UIToolkitDialogueUI
{
    // ─────────────────────────────────────────────
    // Panel Color Settings
    // ─────────────────────────────────────────────

    [System.Serializable]
    public class PanelColorConfig
    {
        [Tooltip("서브타이틀 패널 이름 (예: NPCSubtitlePanel)")]
        public string panelName;

        [Tooltip("색상을 적용할 VisualElement 이름 (예: NPCBackgroundColor)")]
        public string colorElementName = "NPCBackgroundColor";

        [Tooltip("대화창 클릭 판정 영역으로 사용할 VisualElement 이름 (비워두면 패널 자체를 사용)")]
        public string dialoguePanelAreaName = "";
    }

    private const string PanelFieldName = "SubtitlePanel";

    [Header("Color Settings")]
    [SerializeField] private List<PanelColorConfig> panelColorConfigs = new List<PanelColorConfig>()
    {
        new PanelColorConfig { panelName = "NPCSubtitlePanel", colorElementName = "NPCBackgroundColor" }
    };

    private Color _defaultNPCBackgroundColor = Color.white;
    private bool _hasCachedDefaultColor = false;

    // ─────────────────────────────────────────────
    // Element Align from Entry Field
    // ─────────────────────────────────────────────

    [System.Serializable]
    public class ElementAlignConfig
    {
        [Tooltip("정렬을 제어할 VisualElement 이름 (예: ShoutPortraitContainer)")]
        public string elementName;

        [Tooltip("Dialogue Entry에서 읽을 커스텀 필드 이름 (예: AlignItems)")]
        public string entryFieldName = "AlignItems";

        [Tooltip("필드 값이 없거나 비어있을 때 적용할 기본 정렬")]
        public Align defaultAlign = Align.FlexStart;
    }

    [Header("Element Align from Entry Field")]
    [Tooltip("Dialogue Entry 커스텀 필드 값으로 VisualElement의 align-items를 제어합니다.")]
    [SerializeField] private List<ElementAlignConfig> elementAlignConfigs = new List<ElementAlignConfig>();

    // ─────────────────────────────────────────────
    // Label Text Sync (아웃라인 그림자 Label 동기화)
    // ─────────────────────────────────────────────

    [System.Serializable]
    public class LabelTextSync
    {
        [Tooltip("텍스트를 가져올 원본 Label 이름 (예: ShoutPortraitLabel)")]
        public string sourceLabelName;

        [Tooltip("텍스트를 복사할 대상 Label 이름 목록 (예: ShoutPortraitLabel_Shadow)")]
        public List<string> targetLabelNames = new List<string>();
    }

    [Header("Label Text Sync")]
    [Tooltip("대화 중 텍스트를 동기화할 Label 쌍. 타이프라이터 포함. (원본 → 그림자들)")]
    [SerializeField] private List<LabelTextSync> labelTextSyncs = new List<LabelTextSync>();

    private class LabelTextSyncGroup
    {
        public Label sourceLabel;
        public List<Label> targetLabels = new List<Label>();
    }
    private List<LabelTextSyncGroup> _syncGroups = new List<LabelTextSyncGroup>();

    // ─────────────────────────────────────────────
    // Block Continue Settings
    // ─────────────────────────────────────────────

    [Header("Block Continue Settings")]
    [Tooltip("이 태그를 가진 오브젝트를 터치했을 때는 대화가 넘어가지 않도록 차단합니다.")]
    [SerializeField] private List<string> blockingTags = new List<string> { "NPC", "Player" };

    private float _lastBlockedTime = -10f;
    private UIDocument _uiDocument;

    // ─────────────────────────────────────────────
    // UIDocument 가져오기
    // ─────────────────────────────────────────────

    private UIDocument GetUIDocument()
    {
        if (_uiDocument == null)
        {
            var uiDialogueElements = dialogueControls as UIToolkitDialogueElements;
            if (uiDialogueElements != null)
            {
                var field = typeof(UIToolkitDialogueElements).GetField("document",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    _uiDocument = field.GetValue(uiDialogueElements) as UIDocument;
                }
            }

            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
                if (_uiDocument == null)
                {
                    _uiDocument = GetComponentInChildren<UIDocument>();
                }
            }
        }
        return _uiDocument;
    }

    public static CustomDialogueUI Instance { get; private set; }

    public override void Awake()
    {
        base.Awake();
        Instance = this;
    }

    public override void Start()
    {
        base.Start();
        InitializeTextSync();
    }

    public bool IsPointerOverDialogueArea()
    {
        var activePanel = GetActivePanelElement();
        if (activePanel == null) return false;

        VisualElement targetAreaElement = activePanel;
        if (panelColorConfigs != null)
        {
            var config = panelColorConfigs.Find(c => c.panelName == activePanel.name);
            if (config != null && !string.IsNullOrEmpty(config.dialoguePanelAreaName))
            {
                var childArea = activePanel.Q<VisualElement>(config.dialoguePanelAreaName);
                if (childArea != null)
                {
                    targetAreaElement = childArea;
                }
            }
        }

        Vector2 pointerPos = Vector2.zero;
        if (UnityEngine.InputSystem.Pointer.current != null)
        {
            pointerPos = UnityEngine.InputSystem.Pointer.current.position.ReadValue();
        }
        else
        {
            pointerPos = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        }

        // UI Toolkit: (0,0)이 좌상단 / Screen: (0,0)이 좌하단
        Vector2 panelPos = new Vector2(pointerPos.x, Screen.height - pointerPos.y);
        return targetAreaElement.worldBound.Contains(panelPos);
    }

    public static bool IsPointerOverDialogueAreaStatic()
    {
        if (Instance == null || !DialogueManager.isConversationActive) return false;
        return Instance.IsPointerOverDialogueArea();
    }

    public override void Open()
    {
        base.Open();
        // NPC 상호작용 등으로 대화가 시작될 때, 버튼을 떼는(Mouse UP) 순간 대화가 넘어가버리지 않도록 차단
        _lastBlockedTime = Time.time;
    }

    public override void Update()
    {
        base.Update();

        // 타이프라이터 효과 중에도 target label 텍스트를 동기화
        if (DialogueManager.isConversationActive)
        {
            UpdateTextSync();
        }

        if (WasPointerPressedThisFrame() || WasPointerReleasedThisFrame())
        {
            if (IsWorldPointerOverInteractable())
            {
                _lastBlockedTime = Time.time;
            }
        }
    }

    // ─────────────────────────────────────────────
    // ShowSubtitle: 배경 색상 적용
    // ─────────────────────────────────────────────

    public override void ShowSubtitle(Subtitle subtitle)
    {
        base.ShowSubtitle(subtitle);

        if (subtitle == null) return;

        var doc = GetUIDocument();
        if (doc == null || doc.rootVisualElement == null) return;

        var root = doc.rootVisualElement;

        // VisualElement align-items 적용 — 패널 종류와 무관하게 항상 실행
        ApplyElementAligns(subtitle, root);

        // 패널 배경색 적용
        var panel = GetSubtitlePanel(subtitle);
        if (panel == null) return;

        var panelElement = root.Q<VisualElement>(panel.SubtitlePanelName);
        if (panelElement == null) return;

        string colorElementName = "NPCBackgroundColor";
        if (panelColorConfigs != null)
        {
            var config = panelColorConfigs.Find(c => c.panelName == panel.SubtitlePanelName);
            if (config != null && !string.IsNullOrEmpty(config.colorElementName))
            {
                colorElementName = config.colorElementName;
            }
        }

        var npcBackgroundColor = panelElement.Q<VisualElement>(colorElementName);
        if (npcBackgroundColor == null) return;

        if (!_hasCachedDefaultColor)
        {
            if (npcBackgroundColor.style.backgroundColor.keyword == StyleKeyword.Null ||
                npcBackgroundColor.style.backgroundColor.keyword == StyleKeyword.Undefined)
            {
                _defaultNPCBackgroundColor = Color.white;
            }
            else
            {
                _defaultNPCBackgroundColor = npcBackgroundColor.style.backgroundColor.value;
            }
            _hasCachedDefaultColor = true;
        }

        string hexColor = subtitle.speakerInfo.GetFieldText("NodeColor");
        if (string.IsNullOrEmpty(hexColor) && subtitle.speakerInfo.nameInDatabase != null)
        {
            hexColor = DialogueLua.GetActorField(subtitle.speakerInfo.nameInDatabase, "NodeColor").asString;
        }

        if (!string.IsNullOrEmpty(hexColor) && ColorUtility.TryParseHtmlString(hexColor, out Color color))
        {
            npcBackgroundColor.style.backgroundColor = new StyleColor(color);
        }
        else
        {
            npcBackgroundColor.style.backgroundColor = _defaultNPCBackgroundColor;
        }
    }

    private void ApplyElementAligns(Subtitle subtitle, VisualElement root)
    {
        if (elementAlignConfigs == null || elementAlignConfigs.Count == 0) return;
        if (subtitle?.dialogueEntry == null) return;

        foreach (var config in elementAlignConfigs)
        {
            if (string.IsNullOrEmpty(config.elementName)) continue;

            var element = root.Q<VisualElement>(config.elementName);
            if (element == null) continue;

            var fieldValue = Field.LookupValue(subtitle.dialogueEntry.fields, config.entryFieldName);

            Align align = config.defaultAlign;
            if (!string.IsNullOrEmpty(fieldValue))
            {
                switch (fieldValue.Trim().ToLower())
                {
                    case "left":
                    case "flexstart":
                    case "flex-start":
                    case "start":
                        align = Align.FlexStart;
                        break;
                    case "center":
                        align = Align.Center;
                        break;
                    case "right":
                    case "flexend":
                    case "flex-end":
                    case "end":
                        align = Align.FlexEnd;
                        break;
                    case "stretch":
                        align = Align.Stretch;
                        break;
                }
            }

            element.style.alignItems = align;
        }
    }

    // ─────────────────────────────────────────────
    // Label Text Sync
    // ─────────────────────────────────────────────

    private void InitializeTextSync()
    {
        _syncGroups.Clear();
        var doc = GetUIDocument();
        if (doc == null || doc.rootVisualElement == null) return;

        var root = doc.rootVisualElement;

        foreach (var config in labelTextSyncs)
        {
            if (string.IsNullOrEmpty(config.sourceLabelName)) continue;

            var sourceLabel = root.Q<Label>(config.sourceLabelName);
            if (sourceLabel == null)
            {
                Debug.LogWarning($"[CustomDialogueUI] Source Label '{config.sourceLabelName}' not found.");
                continue;
            }

            var group = new LabelTextSyncGroup { sourceLabel = sourceLabel };

            if (config.targetLabelNames != null)
            {
                foreach (var targetName in config.targetLabelNames)
                {
                    if (string.IsNullOrEmpty(targetName)) continue;

                    var targetLabel = root.Q<Label>(targetName);
                    if (targetLabel != null)
                    {
                        group.targetLabels.Add(targetLabel);
                    }
                    else
                    {
                        Debug.LogWarning($"[CustomDialogueUI] Target Label '{targetName}' not found.");
                    }
                }
            }

            _syncGroups.Add(group);
        }
    }

    private void UpdateTextSync()
    {
        if (_syncGroups.Count == 0)
        {
            InitializeTextSync();
        }

        foreach (var group in _syncGroups)
        {
            if (group.sourceLabel != null)
            {
                string text = group.sourceLabel.text;
                foreach (var target in group.targetLabels)
                {
                    if (target != null)
                    {
                        target.text = text;
                    }
                }
            }
        }
    }

    // ─────────────────────────────────────────────
    // Continue Block
    // ─────────────────────────────────────────────

    private bool WasPointerPressedThisFrame()
    {
        if (UnityEngine.InputSystem.Pointer.current != null)
        {
            return UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame;
        }
        return Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
    }

    private bool WasPointerReleasedThisFrame()
    {
        if (UnityEngine.InputSystem.Pointer.current != null)
        {
            return UnityEngine.InputSystem.Pointer.current.press.wasReleasedThisFrame;
        }
        return Input.GetMouseButtonUp(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended);
    }

    public override void OnContinueConversation()
    {
        // 상호작용 또는 대화창 오픈 직후 0.3초 동안의 릴리즈/클릭 및 인터랙터블 위에 마우스가 있을 때 대화 넘김 차단
        if (Time.time - _lastBlockedTime < 0.3f || IsWorldPointerOverInteractable())
        {
            return;
        }
        base.OnContinueConversation();
    }

    private VisualElement GetActivePanelElement()
    {
        var doc = GetUIDocument();
        if (doc == null || doc.rootVisualElement == null) return null;

        var uiDialogueElements = dialogueControls as UIToolkitDialogueElements;
        if (uiDialogueElements != null && uiDialogueElements.SubtitlePanelElements != null)
        {
            foreach (var panel in uiDialogueElements.SubtitlePanelElements)
            {
                var panelElement = doc.rootVisualElement.Q<VisualElement>(panel.SubtitlePanelName);
                if (panelElement != null && panelElement.resolvedStyle.display != DisplayStyle.None)
                {
                    return panelElement;
                }
            }
        }
        return null;
    }

    private bool IsWorldPointerOverInteractable()
    {
        if (Camera.main == null) return false;

        Vector2 pointerPos = Vector2.zero;
        if (UnityEngine.InputSystem.Pointer.current != null)
        {
            pointerPos = UnityEngine.InputSystem.Pointer.current.position.ReadValue();
        }
        else
        {
            pointerPos = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        }

        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(pointerPos.x, pointerPos.y, 0f));

        Collider2D hit2D = Physics2D.OverlapPoint(worldPos);
        if (hit2D != null)
        {
            if (hit2D.GetComponent<Drag2DObject>() != null ||
                hit2D.GetComponentInParent<Drag2DObject>() != null ||
                hit2D.GetComponent<DialogueSystemTrigger>() != null ||
                hit2D.GetComponentInParent<DialogueSystemTrigger>() != null ||
                blockingTags.Contains(hit2D.tag))
            {
                return true;
            }
        }

        Ray ray = Camera.main.ScreenPointToRay(pointerPos);
        RaycastHit hit3D;
        if (Physics.Raycast(ray, out hit3D))
        {
            if (hit3D.collider.GetComponent<Drag2DObject>() != null ||
                hit3D.collider.GetComponentInParent<Drag2DObject>() != null ||
                hit3D.collider.GetComponent<DialogueSystemTrigger>() != null ||
                hit3D.collider.GetComponentInParent<DialogueSystemTrigger>() != null ||
                blockingTags.Contains(hit3D.collider.tag))
            {
                return true;
            }
        }

        return false;
    }

    // ─────────────────────────────────────────────
    // Panel Selection Overrides
    // ─────────────────────────────────────────────

    protected override UIToolkitSubtitleElements GetSubtitlePanel(Subtitle subtitle)
    {
        if (subtitle == null) return null;

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

        return base.GetSubtitlePanel(subtitle);
    }

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
