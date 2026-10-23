#if UNITY_2021_1_OR_NEWER
// CustomDialogueUI.cs
// Dialogue Entry의 커스텀 필드 "SubtitlePanel"(Number)을 읽어
// 해당 인덱스의 패널을 사용합니다.
// 필드가 없거나 0이면 기본 NPC/PC 패널로 fallback합니다.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.UIToolkit;
using BeepSync;

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
    // Typewriter & Beep Settings (UI Toolkit엔 타이핑 효과가 내장되어 있지 않아 직접 구현)
    // ─────────────────────────────────────────────

    [System.Serializable]
    public class TypewriterLabelConfig
    {
        [Tooltip("타이핑 효과 + 사운드를 적용할 Label 이름. 여러 패널에 적용하려면 각 패널의 텍스트 Label을 모두 등록하세요. " +
                 "(예: NPCSubtitleLabel, PCSubtitleLabel, ShoutPortraitLabel 등)")]
        public string labelName;
    }

    // Dialogue Entry에 이 이름의 커스텀 필드(Number)를 추가하면 해당 대사에서만
    // 타이핑 속도를 다르게 쓸 수 있음. 비어있거나 0 이하면 기본값 또는 BeepData의 charDelay 사용.
    private const string EntryCharsPerSecondFieldName = "CharsPerSecond";

    [Header("Typewriter Settings")]
    [Tooltip("타이핑 효과를 켤지 여부. 끄면 기존처럼 텍스트가 한 번에 표시됩니다.")]
    [SerializeField] private bool enableTypewriter = true;

    [Tooltip("초당 몇 글자씩 찍을지 (기본값). BeepData에 charDelay가 있거나 Dialogue Entry에 'CharsPerSecond' 커스텀 필드가 있으면 오버라이드됩니다.")]
    [SerializeField] private float charactersPerSecond = 30f;

    [Tooltip("타이핑 효과 + 사운드를 적용할 Label 목록. 각 패널의 서브타이틀 텍스트 Label 이름을 등록하세요.")]
    [SerializeField] private List<TypewriterLabelConfig> typewriterLabelConfigs = new List<TypewriterLabelConfig>()
    {
        new TypewriterLabelConfig { labelName = "NPCSubtitleLabel" },
        new TypewriterLabelConfig { labelName = "PCSubtitleLabel" }
    };

    [Header("Beep Data Settings (Character Voice Presets)")]
    [Tooltip("캐릭터별 DialogueBeepData 프리셋 목록. 화자(Speaker) 이름이나 DB Actor 이름과 일치하는 데이터가 자동으로 적용됩니다.")]
    [SerializeField] private List<DialogueBeepData> characterBeepDataList = new List<DialogueBeepData>();

    [Tooltip("일치하는 캐릭터 데이터가 없을 때 사용할 기본 BeepData (선택 사항)")]
    [SerializeField] private DialogueBeepData defaultBeepData;

    [Tooltip("타이핑 사운드 재생용 AudioSource. 비워두면 이 오브젝트에서 자동으로 찾거나 추가합니다.")]
    [SerializeField] private AudioSource typewriterAudioSource;

    [Header("Japanese Localization Settings")]
    [Tooltip("Dialogue System의 언어 설정이 일본어('ja', 'jp')일 때 일본어 최적화(요음/촉음 무음, 한자 다중 비프음)를 자동 적용합니다.")]
    [SerializeField] private bool autoDetectJapanese = true;

    [Tooltip("테스트용: Dialogue System 언어 설정과 무관하게 일본어 최적화 강제 활성화")]
    [SerializeField] private bool forceJapaneseOptimization = false;

    private static readonly HashSet<char> PunctuationChars = new HashSet<char>
    {
        '.', ',', '!', '?', ';', ':', '…', '~',
        '。', '、', '！', '？', '〜', '～'
    };

    private static readonly HashSet<char> JapaneseMutedChars = new HashSet<char>
    {
        'ぁ', 'ぃ', 'ぅ', 'ぇ', 'ぉ', 'っ', 'ゃ', 'ゅ', 'ょ', 'ゎ', 'ヵ', 'ヶ',
        'ァ', 'ィ', 'ゥ', 'ェ', 'ォ', 'ッ', 'ャ', 'ュ', 'ョ', 'ヮ', 'ヵ', 'ヶ',
        'ー'
    };

    private static bool IsKanji(char c)
    {
        return (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF);
    }

    private bool IsJapaneseLocalization()
    {
        if (forceJapaneseOptimization) return true;
        if (!autoDetectJapanese) return false;

        string currentLanguage = null;

        if (DialogueManager.displaySettings != null && DialogueManager.displaySettings.localizationSettings != null)
        {
            currentLanguage = DialogueManager.displaySettings.localizationSettings.language;
        }

        if (string.IsNullOrEmpty(currentLanguage))
        {
            currentLanguage = Localization.language;
        }

        if (string.IsNullOrEmpty(currentLanguage) && PixelCrushers.UILocalizationManager.instance != null)
        {
            currentLanguage = PixelCrushers.UILocalizationManager.instance.currentLanguage;
        }

        if (string.IsNullOrEmpty(currentLanguage)) return false;

        return currentLanguage.StartsWith("ja", System.StringComparison.OrdinalIgnoreCase)
            || currentLanguage.Equals("jp", System.StringComparison.OrdinalIgnoreCase)
            || currentLanguage.IndexOf("japanese", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool IsJapaneseTextOrLocalization(string text)
    {
        if (forceJapaneseOptimization) return true;
        if (!autoDetectJapanese) return false;

        // 1. Dialogue System / Localization 언어 설정 확인
        if (IsJapaneseLocalization()) return true;

        // 2. 텍스트 자체에 일본어 고유 문자(히라가나/가타카나)가 포함되어 있는지 검사 (자동 감지 fallback)
        if (!string.IsNullOrEmpty(text))
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                // Hiragana: 0x3040 ~ 0x309F, Katakana: 0x30A0 ~ 0x30FF
                if ((c >= 0x3040 && c <= 0x309F) || (c >= 0x30A0 && c <= 0x30FF))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private int _lastClipIndex = -1;

    private class TypewriterState
    {
        public Label label;
        public string fullText;
        public bool isTyping;
        public Coroutine coroutine;
        public string currentActorName;
        public float charactersPerSecond;
        public DialogueBeepData beepData;
    }
    private List<TypewriterState> _typewriterStates = new List<TypewriterState>();
    private bool _typewriterStatesInitialized = false;

    // ─────────────────────────────────────────────
    // Block Continue Settings
    // ─────────────────────────────────────────────

    [Header("Block Continue Settings")]
    [Tooltip("이 태그를 가진 오브젝트를 터치했을 때는 대화가 넘어가지 않도록 차단합니다.")]
    [SerializeField] private List<string> blockingTags = new List<string> { "NPC", "Player" };

    private float _lastBlockedTime = -10f;
    private UIDocument _uiDocument;

    // ─────────────────────────────────────────────
    // Dialogue Top Buttons (Backlog / Auto / Sound)
    // ─────────────────────────────────────────────

    [Header("Backlog Rewind")]
    [Tooltip("대화 중 화면 우측 상단에 '이전 대사' 버튼을 표시합니다.")]
    [SerializeField] private bool enableBacklogRewind = true;

    [Tooltip("한국어 버튼 문구")]
    [SerializeField] private string backlogButtonTextKo = "이전 대사";

    [Tooltip("일본어 버튼 문구")]
    [SerializeField] private string backlogButtonTextJa = "前のセリフ";

    [Tooltip("되감기 기록을 유지할 최대 대사 수")]
    [SerializeField] private int backlogMaxEntries = 200;

    [Header("Auto Advance Button")]
    [Tooltip("'자동진행' 버튼을 표시합니다.")]
    [SerializeField] private bool enableAutoAdvanceButton = true;

    [Tooltip("타이핑과 시퀀스가 끝난 뒤 자동으로 다음 대사로 넘어가기까지의 대기 시간(초)")]
    [SerializeField] private float autoAdvanceDelay = 1.0f;

    [SerializeField] private string autoAdvanceButtonTextKo = "AUTO";
    [SerializeField] private string autoAdvanceButtonTextJa = "AUTO";

    [Header("Sound Button")]
    [Tooltip("'사운드 On/Off' 버튼을 표시합니다.")]
    [SerializeField] private bool enableSoundButton = true;

    public enum SoundToggleScope
    {
        // 게임 전체 오디오(AudioListener)와 대사 비프음을 모두 끕니다.
        AllAudio,
        // 대사 타이핑 비프음만 끕니다.
        TypewriterOnly
    }

    [Tooltip("사운드 버튼이 끄는 대상")]
    [SerializeField] private SoundToggleScope soundToggleScope = SoundToggleScope.AllAudio;

    [Tooltip("사운드 On/Off 상태를 PlayerPrefs에 저장해 다음 실행에도 유지합니다.")]
    [SerializeField] private bool rememberSoundSetting = true;

    [SerializeField] private string soundOnButtonTextKo = "사운드 On";
    [SerializeField] private string soundOnButtonTextJa = "サウンド On";
    [SerializeField] private string soundOffButtonTextKo = "사운드 Off";
    [SerializeField] private string soundOffButtonTextJa = "サウンド Off";

    private const string SoundEnabledPrefsKey = "DialogueUI.SoundEnabled";

    // 실제로 표시된 대사(DialogueEntry) 기록. 마지막 항목이 현재 대사.
    private readonly List<DialogueEntry> _backlogEntries = new List<DialogueEntry>();
    // 각 대사가 표시되기 직전(시퀀스 재생 전)의 캐릭터 월드 상태.
    private readonly List<DialogueWorldState> _backlogWorldStates = new List<DialogueWorldState>();
    private bool _isBacklogNavigating;
    private bool _topButtonsMissingLogged;
    private Button _backlogButton;
    private Button _autoAdvanceButton;
    private Button _soundButton;
    private VisualElement _backlogSlot;
    private VisualElement _autoAdvanceSlot;
    private VisualElement _soundSlot;

    // 자동진행
    private bool _autoAdvanceEnabled;
    private float _autoAdvanceTimer;
    private bool _continueButtonVisible;
    private bool _responseMenuVisible;

    // 사운드
    private bool _soundEnabled = true;
    private float _volumeBeforeMute = 1f;
    private bool _mutedByThisUI;

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

    private void OnEnable()
    {
        PixelCrushers.UILocalizationManager.languageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        PixelCrushers.UILocalizationManager.languageChanged -= OnLanguageChanged;
    }

    public virtual void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void Start()
    {
        base.Start();
        InitializeTextSync();
        InitializeTypewriterStates();
        LoadSoundSetting();
        ApplySoundState();
        EnsureTopRightButtons();
    }

    private Vector2 GetCurrentPointerPosition()
    {
        if (UnityEngine.InputSystem.Pointer.current != null)
        {
            return UnityEngine.InputSystem.Pointer.current.position.ReadValue();
        }
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        }
        if (UnityEngine.InputSystem.Touchscreen.current != null)
        {
            return UnityEngine.InputSystem.Touchscreen.current.primaryTouch.position.ReadValue();
        }
        return Vector2.zero;
    }

    public bool IsPointerOverDialogueArea()
    {
        if (!DialogueManager.isConversationActive) return false;

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

        Vector2 pointerPos = GetCurrentPointerPosition();

        Vector2 panelPos = targetAreaElement.panel != null
            ? RuntimePanelUtils.ScreenToPanel(targetAreaElement.panel, pointerPos)
            : new Vector2(pointerPos.x, Screen.height - pointerPos.y);

        return targetAreaElement.worldBound.Contains(panelPos);
    }

    public static bool IsPointerOverDialogueAreaStatic()
    {
        if (Instance == null || !DialogueManager.isConversationActive) return false;
        return Instance.IsPointerOverDialogueArea();
    }

    public override void Open()
    {
        // 대화 시작 전에 동적 스테이지를 보장한다. 첫 대사/시퀀스보다 먼저 실행되어야 한다.
        // (파괴된 빌더를 ?.로 호출하지 않도록 Unity의 null 비교를 사용한다.)
        var stageBuilder = DialogueStageBuilder.Current;
        if (stageBuilder != null) stageBuilder.EnsureBuilt();

        base.Open();
        // NPC 상호작용 등으로 대화가 시작될 때, 버튼을 떼는(Mouse UP) 순간 대화가 넘어가버리지 않도록 차단
        _lastBlockedTime = Time.time;

        ClearBacklogHistory();
        EnsureTopRightButtons();
        UpdateTopButtonStates();
    }

    public override void Close()
    {
        ClearBacklogHistory();
        SetTopButtonsDisplay(false);
        _autoAdvanceTimer = 0f;
        _responseMenuVisible = false;
        _continueButtonVisible = false;
        base.Close();

        // 대화 경계에서만 스테이지를 정리한다. clearOnConversationEnd가 꺼져 있으면 유지된다.
        var stageBuilder = DialogueStageBuilder.Current;
        if (stageBuilder != null) stageBuilder.NotifyConversationEnd();
    }

    public override void Update()
    {
        base.Update();

        EnsureTopRightButtons();
        UpdateTopButtonStates();
        UpdateAutoAdvance();

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
    // ShowSubtitle: 배경 색상 적용 + 타이핑 효과 시작
    // ─────────────────────────────────────────────

    public override void ShowSubtitle(Subtitle subtitle)
    {
        base.ShowSubtitle(subtitle);

        if (subtitle == null) return;

        // 새 대사가 표시되면 자동진행 대기 시간을 초기화한다.
        _autoAdvanceTimer = 0f;
        _continueButtonVisible = false;

        // 되감기용 기록: 화면에 실제로 표시된 대사만 저장한다.
        // (_isBacklogNavigating 중에는 GotoState가 다시 호출한 것이므로 중복 기록하지 않음)
        if (!_isBacklogNavigating && !string.IsNullOrEmpty(subtitle.formattedText.text))
        {
            RecordBacklogEntry(subtitle.dialogueEntry);
        }

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

        // 이 패널 안에 있는 타이핑 대상 Label에서 타이핑 효과 시작
        // (base.ShowSubtitle이 이미 Label.text에 전체 텍스트를 채워놓은 상태이므로,
        //  그 텍스트를 가져다가 다시 한 글자씩 재생함)
        StartTypewriterForPanel(subtitle, panelElement);

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
    // Typewriter (UI Toolkit엔 내장 타이핑 효과가 없어서 직접 구현)
    // ─────────────────────────────────────────────

    private void InitializeTypewriterStates()
    {
        _typewriterStates.Clear();

        var doc = GetUIDocument();
        if (doc == null || doc.rootVisualElement == null) return;

        var root = doc.rootVisualElement;

        foreach (var config in typewriterLabelConfigs)
        {
            if (string.IsNullOrEmpty(config.labelName)) continue;

            var label = root.Q<Label>(config.labelName);
            if (label == null)
            {
                Debug.LogWarning($"[CustomDialogueUI] Typewriter target Label '{config.labelName}' not found.");
                continue;
            }

            _typewriterStates.Add(new TypewriterState
            {
                label = label,
                fullText = label.text,
                isTyping = false,
                coroutine = null,
                currentActorName = null
            });
        }

        if (typewriterAudioSource == null)
        {
            typewriterAudioSource = GetComponent<AudioSource>();
            if (typewriterAudioSource == null)
            {
                typewriterAudioSource = gameObject.AddComponent<AudioSource>();
                typewriterAudioSource.playOnAwake = false;
                typewriterAudioSource.loop = false;
            }
        }

        _typewriterStatesInitialized = true;
    }

    [Tooltip("타이프라이터 및 비프음 매칭/재생 디버그 로그 출력 여부")]
    [SerializeField] private bool debugBeepLogs = false;

    private DialogueBeepData FindBeepData(Subtitle subtitle)
    {
        if (subtitle == null || subtitle.speakerInfo == null) return defaultBeepData;

        string nameInDb = subtitle.speakerInfo.nameInDatabase;
        string displayName = subtitle.speakerInfo.Name;
        string transformName = subtitle.speakerInfo.transform != null ? subtitle.speakerInfo.transform.name : null;

        if (characterBeepDataList != null && characterBeepDataList.Count > 0)
        {
            foreach (var data in characterBeepDataList)
            {
                if (data == null) continue;

                // 1. DB 원본 영문 이름 매칭 (예: "Chona", "Fafnir", "Janghwa", "Vargr", "Player")
                if (!string.IsNullOrEmpty(nameInDb))
                {
                    if (string.Equals(data.characterName, nameInDb, System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(data.name, nameInDb, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (debugBeepLogs) Debug.Log($"[CustomDialogueUI] BeepData '{data.name}' matched with DB Name '{nameInDb}'");
                        return data;
                    }
                }

                // 2. 표시 이름 / 로컬라이즈된 이름 매칭 (예: "천아", "파프니르", "장화", "나")
                if (!string.IsNullOrEmpty(displayName))
                {
                    if (string.Equals(data.characterName, displayName, System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(data.name, displayName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (debugBeepLogs) Debug.Log($"[CustomDialogueUI] BeepData '{data.name}' matched with Display Name '{displayName}'");
                        return data;
                    }
                }

                // 3. GameObject Transform 이름 매칭
                if (!string.IsNullOrEmpty(transformName))
                {
                    if (string.Equals(data.characterName, transformName, System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(data.name, transformName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (debugBeepLogs) Debug.Log($"[CustomDialogueUI] BeepData '{data.name}' matched with Transform Name '{transformName}'");
                        return data;
                    }
                }
            }
        }

        if (defaultBeepData != null)
        {
            if (debugBeepLogs) Debug.Log($"[CustomDialogueUI] Using Default BeepData '{defaultBeepData.name}' for Speaker '{nameInDb ?? displayName}'");
            return defaultBeepData;
        }

        if (debugBeepLogs)
        {
            Debug.LogWarning($"[CustomDialogueUI] No BeepData found for Speaker '{nameInDb}' (Display: '{displayName}'). Checked {characterBeepDataList?.Count ?? 0} items.");
        }
        return null;
    }

    // Dialogue Entry의 "CharsPerSecond" 커스텀 필드를 확인해서, 있으면 그 값을,
    // 없거나 파싱 실패/0 이하이면 -1f를 반환한다 (BeepData의 charDelay 또는 기본값 사용 유도).
    private float GetEffectiveCharsPerSecond(Subtitle subtitle)
    {
        var entry = subtitle?.dialogueEntry;
        if (entry != null)
        {
            var fieldValue = Field.LookupValue(entry.fields, EntryCharsPerSecondFieldName);
            if (!string.IsNullOrEmpty(fieldValue) &&
                float.TryParse(fieldValue, out float overrideValue) &&
                overrideValue > 0f)
            {
                return overrideValue;
            }
        }

        return -1f;
    }

    // ShowSubtitle에서 패널이 정해질 때 호출.
    // 그 패널 안에 있는 타이핑 대상 Label들의 타이핑 코루틴을 (다시) 시작한다.
    private void StartTypewriterForPanel(Subtitle subtitle, VisualElement panelElement)
    {
        if (!_typewriterStatesInitialized)
        {
            InitializeTypewriterStates();
        }

        string actorName = (subtitle != null && subtitle.speakerInfo != null)
            ? (subtitle.speakerInfo.nameInDatabase ?? subtitle.speakerInfo.Name)
            : null;

        DialogueBeepData beepData = FindBeepData(subtitle);
        float effectiveCharsPerSecond = GetEffectiveCharsPerSecond(subtitle);

        foreach (var state in _typewriterStates)
        {
            if (state.label == null) continue;
            if (!panelElement.Contains(state.label)) continue;

            // 이전에 재생 중이던 타이핑이 있으면 중단
            if (state.coroutine != null)
            {
                StopCoroutine(state.coroutine);
                state.coroutine = null;
            }

            state.currentActorName = actorName;
            state.charactersPerSecond = effectiveCharsPerSecond;
            state.beepData = beepData;
            // base.ShowSubtitle()이 이미 Label.text에 전체 텍스트를 넣어놓은 상태
            state.fullText = state.label.text;

            if (!enableTypewriter || string.IsNullOrEmpty(state.fullText))
            {
                state.isTyping = false;
                continue;
            }

            state.label.text = $"<color=#00000000>{state.fullText}</color>";
            state.isTyping = true;
            state.coroutine = StartCoroutine(TypewriterCoroutine(state));
        }
    }

    private IEnumerator TypewriterCoroutine(TypewriterState state)
    {
        var beep = state.beepData;
        float basePitch = beep != null ? beep.basePitch : 1.0f;
        float pitchRand = beep != null ? beep.pitchRandomness : 0.08f;

        // 지연 시간 계산:
        // 1. Dialogue Entry의 CharsPerSecond 커스텀 필드가 설정되어 있다면 최우선 적용
        // 2. 아니면 BeepData의 charDelay 적용
        // 3. 둘 다 없으면 기본 charactersPerSecond 기반 delay 적용
        float charDelay;
        if (state.charactersPerSecond > 0f)
        {
            charDelay = 1f / state.charactersPerSecond;
        }
        else if (beep != null && beep.charDelay > 0f)
        {
            charDelay = beep.charDelay;
        }
        else
        {
            charDelay = charactersPerSecond > 0f ? (1f / charactersPerSecond) : 0.045f;
        }

        float puncPause = beep != null ? beep.punctuationPause : 0f;
        int soundFreq = (beep != null && beep.soundFrequency > 0) ? beep.soundFrequency : 1;
        bool playOnSpace = beep != null && beep.playOnWhitespace;

        bool isJapanese = IsJapaneseTextOrLocalization(state.fullText);
        bool muteSmallKana = isJapanese && (beep == null || beep.muteSmallKanaInJapanese);
        bool handleKanji = isJapanese && (beep == null || beep.multiBeepForKanjiInJapanese);

        // 일본어는 한 글자가 담는 정보량이 많아 같은 글자 수라도 한국어/영어보다
        // 금방 끝나 보인다. 글자당 출력 시간을 배율만큼 늘려 비프음이 충분히 이어지게 한다.
        if (isJapanese)
        {
            float japaneseMultiplier = beep != null ? Mathf.Max(1f, beep.japaneseDelayMultiplier) : 1.5f;
            charDelay *= japaneseMultiplier;
        }

        if (debugBeepLogs)
        {
            Debug.Log($"[CustomDialogueUI] Typewriter: label='{state.label?.name}', isJapanese={isJapanese} | text='{state.fullText}'");
        }

        int length = state.fullText.Length;
        int visibleCharCount = 0;

        for (int i = 1; i <= length; i++)
        {
            char c = state.fullText[i - 1];

            // 중앙/우측 정렬 시 글자 위치가 왼쪽으로 밀리는 현상(레이아웃 시프트)을 방지하기 위해
            // 아직 출력되지 않은 뒷부분 텍스트를 투명(<color=#00000000>)으로 채워 전체 문장 폭을 고정
            if (i < length)
            {
                state.label.text = $"{state.fullText.Substring(0, i)}<color=#00000000>{state.fullText.Substring(i)}</color>";
            }
            else
            {
                state.label.text = state.fullText;
            }

            // 문자 유형 판별
            bool isWhitespace = char.IsWhiteSpace(c);
            bool isMutedKana = muteSmallKana && JapaneseMutedChars.Contains(c);
            bool isKanji = handleKanji && IsKanji(c);

            // 소리가 나지 않는 공백이나 일본어 요음/촉음/장음은 비프 카운터에서 제외
            if (!isWhitespace && !isMutedKana)
            {
                visibleCharCount++;
            }

            // 비프음 재생 조건 검사
            bool shouldPlaySound = (!isWhitespace || playOnSpace) && !isMutedKana && (visibleCharCount % soundFreq == 0);

            // 한자 딜레이 및 비프음 처리
            if (isKanji)
            {
                float multiplier = beep != null ? beep.kanjiDelayMultiplier : 1.6f;
                int kanjiBeeps = beep != null ? Mathf.Max(1, beep.kanjiBeepCount) : 2;
                // 비프음 사이에 최소 간격을 확보해 여러 번의 소리로 뚜렷하게 구분되게 한다.
                float minBeepInterval = beep != null ? Mathf.Max(0.01f, beep.minBeepInterval) : 0.045f;
                float kanjiTotalDelay = Mathf.Max(charDelay * multiplier, minBeepInterval * kanjiBeeps);
                float stepDelay = kanjiTotalDelay / kanjiBeeps;

                if (shouldPlaySound)
                {
                    if (debugBeepLogs) Debug.Log($"[CustomDialogueUI] Kanji '{c}' -> playing {kanjiBeeps} beeps (delay: {kanjiTotalDelay:F3}s)");

                    for (int b = 0; b < kanjiBeeps; b++)
                    {
                        PlayTypewriterBeep(state.beepData, state.currentActorName, basePitch, pitchRand);
                        if (b < kanjiBeeps - 1)
                        {
                            if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
                            else yield return null;
                        }
                    }

                    if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
                    else yield return null;
                }
                else
                {
                    yield return new WaitForSeconds(kanjiTotalDelay);
                }
            }
            else
            {
                float currentDelay = charDelay;
                // 요음(ゃ, ゅ, ょ 등)은 앞 글자에 붙는 소리이므로 딜레이를 약간 단축하여 시각적으로 밀착
                if (isMutedKana && c != 'っ' && c != 'ッ' && c != 'ー')
                {
                    currentDelay = charDelay * 0.5f;
                }

                bool isEllipsis = isJapanese && c == '…';

                // 문장 부호 대기 또는 일반 글자 대기
                if (PunctuationChars.Contains(c) && puncPause > 0f)
                {
                    float pauseDuration = currentDelay + puncPause;

                    if (isEllipsis)
                    {
                        // 일본어는 '...' 대신 '…' 한 글자를 쓰기 때문에 비프음이 적게 들린다.
                        // 멈춤 시간 동안 여러 번 나눠 재생해 한국어 '...'와 비슷한 느낌을 준다.
                        int beepCount = beep != null ? Mathf.Max(1, beep.ellipsisBeepCount) : 3;
                        float stepDelay = pauseDuration / beepCount;

                        for (int b = 0; b < beepCount; b++)
                        {
                            PlayTypewriterBeep(state.beepData, state.currentActorName, basePitch, pitchRand);
                            if (b < beepCount - 1)
                            {
                                if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
                                else yield return null;
                            }
                        }

                        if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
                        else yield return null;
                    }
                    else
                    {
                        if (shouldPlaySound)
                        {
                            PlayTypewriterBeep(state.beepData, state.currentActorName, basePitch, pitchRand);
                        }
                        else if (isMutedKana && debugBeepLogs)
                        {
                            Debug.Log($"[CustomDialogueUI] Muted small kana / prolonged mark: '{c}' (No sound)");
                        }

                        yield return new WaitForSeconds(pauseDuration);
                    }
                }
                else
                {
                    if (shouldPlaySound)
                    {
                        PlayTypewriterBeep(state.beepData, state.currentActorName, basePitch, pitchRand);
                    }
                    else if (isMutedKana && debugBeepLogs)
                    {
                        Debug.Log($"[CustomDialogueUI] Muted small kana / prolonged mark: '{c}' (No sound)");
                    }

                    if (currentDelay > 0f)
                    {
                        yield return new WaitForSeconds(currentDelay);
                    }
                    else
                    {
                        yield return null;
                    }
                }
            }
        }

        state.isTyping = false;
        state.coroutine = null;
    }

    // 현재 타이핑 중인 Label이 있으면 즉시 전체 텍스트로 완료시킨다.
    // 하나라도 완료시켰으면 true를 반환 (호출부에서 이번 클릭은 "스킵"으로만 처리하고
    // 대화를 넘기지 않도록 하기 위함).
    private bool SkipTypewritersIfTyping()
    {
        bool skippedAny = false;

        foreach (var state in _typewriterStates)
        {
            if (!state.isTyping) continue;

            if (state.coroutine != null)
            {
                StopCoroutine(state.coroutine);
                state.coroutine = null;
            }

            if (state.label != null)
            {
                state.label.text = state.fullText;
            }

            state.isTyping = false;
            skippedAny = true;
        }

        return skippedAny;
    }

    private void PlayTypewriterBeep(DialogueBeepData beepData, string actorName, float basePitch, float pitchRandomness)
    {
        if (typewriterAudioSource == null)
        {
            typewriterAudioSource = GetComponent<AudioSource>();
            if (typewriterAudioSource == null)
            {
                typewriterAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (typewriterAudioSource == null) return;

        // 사운드 Off 상태면 비프음을 재생하지 않는다.
        if (!_soundEnabled)
        {
            typewriterAudioSource.mute = true;
            return;
        }

        // 2D 오디오 및 볼륨 안전장치
        typewriterAudioSource.spatialBlend = 0f;
        typewriterAudioSource.mute = false;

        if (beepData != null && beepData.beepClips != null && beepData.beepClips.Count > 0)
        {
            AudioClip clipToPlay;
            int count = beepData.beepClips.Count;
            if (count == 1)
            {
                clipToPlay = beepData.beepClips[0];
            }
            else
            {
                int index = UnityEngine.Random.Range(0, count);
                if (index == _lastClipIndex)
                {
                    index = (index + 1) % count;
                }
                _lastClipIndex = index;
                clipToPlay = beepData.beepClips[index];
            }

            if (clipToPlay != null)
            {
                float randomOffset = UnityEngine.Random.Range(-pitchRandomness, pitchRandomness);
                typewriterAudioSource.pitch = Mathf.Clamp(basePitch + randomOffset, 0.1f, 3.0f);
                typewriterAudioSource.volume = Mathf.Clamp01(beepData.volume);
                typewriterAudioSource.PlayOneShot(clipToPlay);
            }
        }
    }

    // ─────────────────────────────────────────────
    // Continue Block
    // ─────────────────────────────────────────────

    private bool WasPointerPressedThisFrame()
    {
        if (UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame)
        {
            return true;
        }
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }
        if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }
        return false;
    }

    private bool WasPointerReleasedThisFrame()
    {
        if (UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.wasReleasedThisFrame)
        {
            return true;
        }
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame)
        {
            return true;
        }
        if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            return true;
        }
        return false;
    }

    public override void OnContinueConversation()
    {
        bool isOverPanelBg = IsPointerOverPanelBackground();
        bool isWorldInteractable = IsWorldPointerOverInteractable();

        // PanelBackground 영역 밖(= ContinueButton 영역)에서만 오브젝트 상호작용 우선을 위해 대화 넘김 차단
        // PanelBackground 위라면 오브젝트가 뒤에 있더라도 대사 넘김/타이핑 스킵 우선 진행
        if (!isOverPanelBg)
        {
            if (Time.time - _lastBlockedTime < 0.3f || isWorldInteractable)
            {
                return;
            }
        }

        // 타이핑 중이었다면 이번 클릭은 "즉시 전체 텍스트 표시"로만 처리하고
        // 대화 자체는 넘기지 않는다. 이미 다 찍힌 상태에서 클릭하면 정상적으로 다음 대사로 진행.
        if (SkipTypewritersIfTyping())
        {
            return;
        }

        base.OnContinueConversation();
    }

    public VisualElement GetActivePanelBackgroundElement()
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
                    // 패널 안에서 class="panelBackground" 또는 특정 패널 배경 이름 검색
                    var bg = panelElement.Q<VisualElement>(className: "panelBackground")
                          ?? panelElement.Q<VisualElement>("NPCPanelBackground")
                          ?? panelElement.Q<VisualElement>("ShoutPanelBackground")
                          ?? panelElement.Q<VisualElement>("ThoughtPanelBackground");
                    return bg;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 현재 마우스/터치 포인터가 활성화된 대화창 PanelBackground 영역 내부에 있는지 확인합니다.
    /// </summary>
    public bool IsPointerOverPanelBackground()
    {
        if (!DialogueManager.isConversationActive) return false;

        var panelBg = GetActivePanelBackgroundElement();
        if (panelBg == null) return false;

        Vector2 pointerPos = GetCurrentPointerPosition();

        Vector2 panelPos = panelBg.panel != null
            ? RuntimePanelUtils.ScreenToPanel(panelBg.panel, pointerPos)
            : new Vector2(pointerPos.x, Screen.height - pointerPos.y);

        return panelBg.worldBound.Contains(panelPos);
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

        Vector2 pointerPos = GetCurrentPointerPosition();

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
    // Dialogue Top Buttons (Backlog / Auto / Sound)
    // ─────────────────────────────────────────────

    private void OnLanguageChanged(string language)
    {
        UpdateTopButtonTexts();

        // 폰트 로컬라이저가 먼저/나중에 실행될 수 있으므로 다음 프레임에 다시 복사한다.
        ScheduleFontCopy();
    }

    private void EnsureTopRightButtons()
    {
        if (IsBound(_backlogButton) && IsBound(_autoAdvanceButton) && IsBound(_soundButton))
        {
            return;
        }

        var doc = GetUIDocument();
        if (doc == null || doc.rootVisualElement == null) return;

        // 버튼은 UIToolkitDialogueUIDialogueTemplate.uxml에 정적으로 배치되어 있다.
        var root = doc.rootVisualElement;

        // 그림자까지 같이 숨기기 위해 버튼을 감싸는 슬롯도 함께 찾는다.
        _backlogSlot = root.Q<VisualElement>("BacklogButtonSlot");
        _autoAdvanceSlot = root.Q<VisualElement>("AutoAdvanceButtonSlot");
        _soundSlot = root.Q<VisualElement>("SoundButtonSlot");

        if (!IsBound(_backlogButton))
        {
            _backlogButton = root.Q<Button>("BacklogButton");
            if (_backlogButton != null)
            {
                _backlogButton.focusable = false;
                _backlogButton.clicked -= OnBacklogButtonClicked;
                _backlogButton.clicked += OnBacklogButtonClicked;
            }
        }

        if (!IsBound(_autoAdvanceButton))
        {
            _autoAdvanceButton = root.Q<Button>("AutoAdvanceButton");
            if (_autoAdvanceButton != null)
            {
                _autoAdvanceButton.focusable = false;
                _autoAdvanceButton.clicked -= OnAutoAdvanceButtonClicked;
                _autoAdvanceButton.clicked += OnAutoAdvanceButtonClicked;
            }
        }

        if (!IsBound(_soundButton))
        {
            _soundButton = root.Q<Button>("SoundButton");
            if (_soundButton != null)
            {
                _soundButton.focusable = false;
                _soundButton.clicked -= OnSoundButtonClicked;
                _soundButton.clicked += OnSoundButtonClicked;
            }
        }

        if (!IsBound(_backlogButton) || !IsBound(_autoAdvanceButton) || !IsBound(_soundButton))
        {
            if (!_topButtonsMissingLogged)
            {
                Debug.LogWarning("[CustomDialogueUI] Dialogue UXML에서 우측 상단 버튼(BacklogButton / AutoAdvanceButton / SoundButton)을 모두 찾지 못했습니다.");
                _topButtonsMissingLogged = true;
            }
            return;
        }

        // 슬롯 이름이 바뀌어도 버튼의 부모(슬롯)를 사용하도록 보정한다.
        if (_backlogSlot == null) _backlogSlot = GetButtonSlot(_backlogButton);
        if (_autoAdvanceSlot == null) _autoAdvanceSlot = GetButtonSlot(_autoAdvanceButton);
        if (_soundSlot == null) _soundSlot = GetButtonSlot(_soundButton);

        _topButtonsMissingLogged = false;

        UpdateTopButtonTexts();
        ApplySubtitleFontToTopButtons();

        // 로컬라이저가 폰트를 적용한 뒤 한 번 더 복사
        ScheduleFontCopy();

        UpdateTopButtonStates();
    }

    private static bool IsBound(Button button)
    {
        return button != null && button.panel != null;
    }

    private static VisualElement GetButtonSlot(Button button)
    {
        var parent = button != null ? button.parent : null;
        return (parent != null && parent.ClassListContains("topButtonSlot")) ? parent : null;
    }

    private bool IsJapaneseUI()
    {
        string language = (PixelCrushers.UILocalizationManager.instance != null)
            ? PixelCrushers.UILocalizationManager.instance.currentLanguage
            : null;

        return !string.IsNullOrEmpty(language)
            && (language.Equals("ja", System.StringComparison.OrdinalIgnoreCase)
                || language.Equals("jp", System.StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateTopButtonTexts()
    {
        bool japanese = IsJapaneseUI();

        // 이전 대사/사운드 버튼은 이미지로만 표시하고 라벨은 사용하지 않는다.
        if (_backlogButton != null)
        {
            _backlogButton.text = string.Empty;
            _backlogButton.tooltip = japanese ? backlogButtonTextJa : backlogButtonTextKo;
        }

        if (_autoAdvanceButton != null)
        {
            _autoAdvanceButton.text = japanese ? autoAdvanceButtonTextJa : autoAdvanceButtonTextKo;
        }

        if (_soundButton != null)
        {
            _soundButton.text = string.Empty;
            _soundButton.tooltip = _soundEnabled
                ? (japanese ? soundOnButtonTextJa : soundOnButtonTextKo)
                : (japanese ? soundOffButtonTextJa : soundOffButtonTextKo);
        }
    }

    private void ScheduleFontCopy()
    {
        if (_backlogButton != null && _backlogButton.panel != null)
        {
            _backlogButton.schedule.Execute(ApplySubtitleFontToTopButtons).ExecuteLater(0);
        }
        else if (_autoAdvanceButton != null && _autoAdvanceButton.panel != null)
        {
            _autoAdvanceButton.schedule.Execute(ApplySubtitleFontToTopButtons).ExecuteLater(0);
        }
        else if (_soundButton != null && _soundButton.panel != null)
        {
            _soundButton.schedule.Execute(ApplySubtitleFontToTopButtons).ExecuteLater(0);
        }
    }

    private void ApplySubtitleFontToTopButtons()
    {
        var doc = GetUIDocument();
        if (doc == null || doc.rootVisualElement == null) return;

        var root = doc.rootVisualElement;
        var sample = root.Q<Label>("NPCSubtitleLabel")
                     ?? root.Q<Label>("PCSubtitleLabel")
                     ?? root.Q<Label>("ShoutSubtitleLabel")
                     ?? root.Q<Label>("ThoughtSubtitleLabel");

        if (sample == null) return;

        var fontDefinition = sample.style.unityFontDefinition.value;
        if (fontDefinition.font == null && fontDefinition.fontAsset == null) return;

        ApplyFont(_backlogButton, fontDefinition);
        ApplyFont(_autoAdvanceButton, fontDefinition);
        ApplyFont(_soundButton, fontDefinition);
    }

    private static void ApplyFont(Button button, FontDefinition fontDefinition)
    {
        if (button == null) return;
        button.style.unityFontDefinition = new StyleFontDefinition(fontDefinition);
    }

    private static void SetSlotDisplay(Button button, VisualElement slot, DisplayStyle display)
    {
        if (slot != null)
        {
            slot.style.display = display;
        }
        else if (button != null)
        {
            button.style.display = display;
        }
    }

    private static void SetSlotOpacity(Button button, VisualElement slot, float opacity)
    {
        if (slot != null)
        {
            slot.style.opacity = opacity;
        }
        else if (button != null)
        {
            button.style.opacity = opacity;
        }
    }

    private void SetTopButtonsDisplay(bool visible)
    {
        var display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        SetSlotDisplay(_backlogButton, _backlogSlot, display);
        SetSlotDisplay(_autoAdvanceButton, _autoAdvanceSlot, display);
        SetSlotDisplay(_soundButton, _soundSlot, display);
    }

    private void UpdateTopButtonStates()
    {
        bool conversationActive = DialogueManager.isConversationActive;
        bool canRewind = conversationActive && _backlogEntries.Count >= 2;

        if (_backlogButton != null)
        {
            bool showBacklog = conversationActive && enableBacklogRewind;
            SetSlotDisplay(_backlogButton, _backlogSlot, showBacklog ? DisplayStyle.Flex : DisplayStyle.None);
            _backlogButton.SetEnabled(canRewind);
            SetSlotOpacity(_backlogButton, _backlogSlot, canRewind ? 1f : 0.45f);
            _backlogButton.EnableInClassList("active", canRewind);
        }

        if (_autoAdvanceButton != null)
        {
            SetSlotDisplay(
                _autoAdvanceButton,
                _autoAdvanceSlot,
                (conversationActive && enableAutoAdvanceButton) ? DisplayStyle.Flex : DisplayStyle.None);
            _autoAdvanceButton.SetEnabled(conversationActive);
            _autoAdvanceButton.EnableInClassList("active", _autoAdvanceEnabled);
        }

        if (_soundButton != null)
        {
            SetSlotDisplay(
                _soundButton,
                _soundSlot,
                (conversationActive && enableSoundButton) ? DisplayStyle.Flex : DisplayStyle.None);
            _soundButton.SetEnabled(conversationActive);
            _soundButton.EnableInClassList("active", _soundEnabled);
        }
    }

    // ─────────────────────────────────────────────
    // Auto Advance
    // ─────────────────────────────────────────────

    private void OnAutoAdvanceButtonClicked()
    {
        _autoAdvanceEnabled = !_autoAdvanceEnabled;
        _autoAdvanceTimer = 0f;
        UpdateTopButtonStates();
    }

    private void UpdateAutoAdvance()
    {
        if (!_autoAdvanceEnabled || !DialogueManager.isConversationActive)
        {
            _autoAdvanceTimer = 0f;
            return;
        }

        // 응답 메뉴/되감기 중에는 자동 진행하지 않는다.
        if (_responseMenuVisible || _isBacklogNavigating)
        {
            _autoAdvanceTimer = 0f;
            return;
        }

        // 타이핑이 끝나고, 시퀀스 연출과 컨티뉴 버튼 표시가 끝난 뒤에만 진행한다.
        if (IsAnyTypewriterTyping() || IsConversationSequencerPlaying() || !_continueButtonVisible)
        {
            _autoAdvanceTimer = 0f;
            return;
        }

        _autoAdvanceTimer += Time.deltaTime;
        if (_autoAdvanceTimer < Mathf.Max(0f, autoAdvanceDelay)) return;

        _autoAdvanceTimer = 0f;

        // 월드 클릭 판정(오브젝트 상호작용)을 건너뛰고 대화만 넘긴다.
        base.OnContinueConversation();
    }

    private bool IsAnyTypewriterTyping()
    {
        for (int i = 0; i < _typewriterStates.Count; i++)
        {
            if (_typewriterStates[i] != null && _typewriterStates[i].isTyping) return true;
        }
        return false;
    }

    private static bool IsConversationSequencerPlaying()
    {
        var controller = DialogueManager.conversationController;
        if (controller == null) return false;

        var view = controller.conversationView;
        if (view == null || view.sequencer == null) return false;

        return view.sequencer.isPlaying;
    }

    // ─────────────────────────────────────────────
    // Sound On/Off
    // ─────────────────────────────────────────────

    private void LoadSoundSetting()
    {
        _soundEnabled = !rememberSoundSetting || PlayerPrefs.GetInt(SoundEnabledPrefsKey, 1) != 0;
    }

    private void OnSoundButtonClicked()
    {
        _soundEnabled = !_soundEnabled;
        ApplySoundState();

        if (rememberSoundSetting)
        {
            PlayerPrefs.SetInt(SoundEnabledPrefsKey, _soundEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        UpdateTopButtonTexts();
        UpdateTopButtonStates();
    }

    private void ApplySoundState()
    {
        if (soundToggleScope == SoundToggleScope.AllAudio)
        {
            if (_soundEnabled)
            {
                // 이 UI 때문에 음소거한 경우에만 원래 볼륨으로 되돌린다.
                if (_mutedByThisUI)
                {
                    AudioListener.volume = _volumeBeforeMute;
                    _mutedByThisUI = false;
                }
            }
            else
            {
                if (!_mutedByThisUI)
                {
                    _volumeBeforeMute = AudioListener.volume > 0f ? AudioListener.volume : 1f;
                    _mutedByThisUI = true;
                }
                AudioListener.volume = 0f;
            }
        }

        if (typewriterAudioSource != null)
        {
            typewriterAudioSource.mute = !_soundEnabled;
        }
    }

    private void RecordBacklogEntry(DialogueEntry entry)
    {
        if (entry == null) return;

        // 같은 대사가 연속으로 다시 표시되는 경우(리마인더 등) 중복 기록 방지
        if (_backlogEntries.Count > 0 && _backlogEntries[_backlogEntries.Count - 1] == entry) return;

        // ShowSubtitle은 시퀀스 재생 전에 호출되므로 이 시점의 상태가
        // 해당 대사 시퀀스의 시작 상태가 된다.
        // 단, 이전 대사를 중간에 넘겼다면 그 액션들은 한 프레임 뒤에야 최종값으로 스냅되므로,
        // 중간값이 저장되지 않도록 먼저 정착시킨 뒤 캡처한다.
        DialogueWorldState.SettlePendingActions();
        _backlogWorldStates.Add(DialogueWorldState.Capture());
        _backlogEntries.Add(entry);

        int maxEntries = Mathf.Max(2, backlogMaxEntries);
        while (_backlogEntries.Count > maxEntries)
        {
            _backlogEntries.RemoveAt(0);
            if (_backlogWorldStates.Count > 0) _backlogWorldStates.RemoveAt(0);
        }

        // 예외 상황에서 두 리스트의 길이가 어긋나지 않도록 보정
        while (_backlogWorldStates.Count > _backlogEntries.Count)
        {
            _backlogWorldStates.RemoveAt(_backlogWorldStates.Count - 1);
        }
    }

    private void ClearBacklogHistory()
    {
        _backlogEntries.Clear();
        _backlogWorldStates.Clear();
        _isBacklogNavigating = false;
    }

    private void OnBacklogButtonClicked()
    {
        if (!enableBacklogRewind) return;
        if (_isBacklogNavigating) return;
        if (!DialogueManager.isConversationActive) return;
        if (_backlogEntries.Count < 2) return;

        var controller = DialogueManager.conversationController;
        if (controller == null) return;

        var model = controller.conversationModel;
        var view = controller.conversationView;
        if (model == null || view == null) return;

        int previousIndex = _backlogEntries.Count - 2;
        var previousEntry = _backlogEntries[previousIndex];
        DialogueWorldState previousWorldState =
            (previousIndex < _backlogWorldStates.Count) ? _backlogWorldStates[previousIndex] : null;

        // skipExecution: 이전 대사의 Lua Script/OnExecute를 다시 실행하지 않는다.
        // (시퀀스는 GotoState -> StartSubtitle에서 정상적으로 다시 재생됨)
        var previousState = model.GetState(previousEntry, true, false, true);
        if (previousState == null || previousState.subtitle == null) return;

        // 이 클릭이 대화 넘김/월드 상호작용으로 중복 처리되지 않도록 차단 시간 갱신
        _lastBlockedTime = Time.time;

        // 응답 메뉴가 열려 있었다면 닫고, 응답 메뉴 시퀀스의 콜백 상태도 복구한다.
        StopResponseMenuIfOpen(view);

        // 현재 대사의 카메라 커맨드가 OnDestroy에서 복원된 카메라 값을
        // 덮어쓰지 않도록 세대 번호를 갱신하고 진행 중인 줌 큐를 취소한다.
        DialogueCameraState.BeginRewindCleanup();

        // 현재 대사의 시퀀스/타이핑이 되감기 연출과 겹치지 않도록 중단
        StopAllTypewriters();
        if (view.sequencer != null)
        {
            view.sequencer.Stop();
        }

        // Stop()은 액션 커맨드를 다음 프레임에 Destroy하므로, 그 전에 캐릭터 큐를 비워
        // OnDestroy의 CancelOrSnapAction이 복원값을 덮어쓰지 않게 한다.
        DialogueWorldState.CancelAllActions();

        // 이전 대사의 시퀀스가 시작되기 직전 상태로 되돌린다.
        // GotoState가 해당 시퀀스를 다시 재생하므로 원래 시작 위치에서 연출이 재현된다.
        if (previousWorldState != null)
        {
            previousWorldState.Restore();
        }

        _isBacklogNavigating = true;
        try
        {
            controller.GotoState(previousState);
        }
        finally
        {
            _isBacklogNavigating = false;
        }

        // 현재 대사(미래)를 기록에서 제거해야 버튼을 다시 누를 때 한 단계 더 과거로 간다.
        if (_backlogEntries.Count >= 2)
        {
            int lastIndex = _backlogEntries.Count - 1;
            _backlogEntries.RemoveAt(lastIndex);
            if (lastIndex < _backlogWorldStates.Count)
            {
                _backlogWorldStates.RemoveAt(lastIndex);
            }
        }

        UpdateTopButtonStates();
    }

    private void StopResponseMenuIfOpen(ConversationView view)
    {
        // 응답 메뉴가 없어도 안전하게 호출 가능
        if (DialogueManager.dialogueUI != null)
        {
            DialogueManager.dialogueUI.HideResponses();
        }

        // PlayResponseMenuSequence()는 ConversationView의 FinishedSequenceHandler를 해제한다.
        // StopResponseMenuSequence()가 private이라 리플렉션으로 호출해 핸들러를 복구한다.
        // (현재 DB에는 응답이 없지만, 추후 응답 메뉴를 추가해도 되감기가 막히지 않도록 처리)
        var method = typeof(ConversationView).GetMethod(
            "StopResponseMenuSequence",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (method == null) return;

        try
        {
            method.Invoke(view, null);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CustomDialogueUI] Could not stop response menu sequence: {e.Message}");
        }
    }

    private void StopAllTypewriters()
    {
        foreach (var state in _typewriterStates)
        {
            if (state == null) continue;

            if (state.coroutine != null)
            {
                StopCoroutine(state.coroutine);
                state.coroutine = null;
            }

            // 숨겨지는 패널에 반투명 꼬리가 남지 않도록 전체 텍스트로 복원
            if (state.label != null && !string.IsNullOrEmpty(state.fullText))
            {
                state.label.text = state.fullText;
            }

            state.isTyping = false;
        }
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
        // 자동진행은 "컨티뉴 버튼이 떠 있는 상태"에서만 동작한다.
        // (시퀀스가 아직 재생 중이면 IsConversationSequencerPlaying()에서 한 번 더 막는다.)
        _continueButtonVisible = true;

        var panel = GetSubtitlePanel(subtitle);
        if (panel != null)
            panel.ShowContinueButton();
        else
            base.ShowContinueButton(subtitle);
    }

    public override void HideContinueButton(Subtitle subtitle)
    {
        _continueButtonVisible = false;

        var panel = GetSubtitlePanel(subtitle);
        if (panel != null)
            panel.HideContinueButton();
        else
            base.HideContinueButton(subtitle);
    }

    public override void ShowResponses(Subtitle subtitle, Response[] responses, float timeout)
    {
        _responseMenuVisible = true;
        base.ShowResponses(subtitle, responses, timeout);
    }

    public override void HideResponses()
    {
        _responseMenuVisible = false;
        base.HideResponses();
    }

    public override void HideSubtitle(Subtitle subtitle)
    {
        var panel = GetSubtitlePanel(subtitle);
        if (panel != null && !panel.ShouldStayVisible)
            panel.Hide();
    }
}
#endif
