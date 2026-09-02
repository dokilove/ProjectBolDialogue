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
    // Typewriter Settings (UI Toolkit엔 타이핑 효과가 내장되어 있지 않아 직접 구현)
    // ─────────────────────────────────────────────

    [System.Serializable]
    public class TypewriterLabelConfig
    {
        [Tooltip("타이핑 효과 + 사운드를 적용할 Label 이름. 여러 패널에 적용하려면 각 패널의 텍스트 Label을 모두 등록하세요. " +
                 "(예: NPCSubtitleLabel, PCSubtitleLabel, ShoutPortraitLabel 등)")]
        public string labelName;
    }

    private const string ActorSoundFieldName = "TypewriterSound";

    // Dialogue Entry에 이 이름의 커스텀 필드(Number)를 추가하면 해당 대사에서만
    // 타이핑 속도를 다르게 쓸 수 있음. 비어있거나 0 이하면 기본값(charactersPerSecond) 사용.
    private const string EntryCharsPerSecondFieldName = "CharsPerSecond";

    [Header("Typewriter Settings")]
    [Tooltip("타이핑 효과를 켤지 여부. 끄면 기존처럼 텍스트가 한 번에 표시됩니다.")]
    [SerializeField] private bool enableTypewriter = true;

    [Tooltip("초당 몇 글자씩 찍을지 (기본값). Dialogue Entry에 'CharsPerSecond' 커스텀 필드를 추가하면 " +
             "해당 대사에서만 이 값을 오버라이드할 수 있습니다.")]
    [SerializeField] private float charactersPerSecond = 30f;

    [Tooltip("타이핑 효과 + 사운드를 적용할 Label 목록. 각 패널의 서브타이틀 텍스트 Label 이름을 등록하세요.")]
    [SerializeField] private List<TypewriterLabelConfig> typewriterLabelConfigs = new List<TypewriterLabelConfig>()
    {
        new TypewriterLabelConfig { labelName = "NPCSubtitleLabel" },
        new TypewriterLabelConfig { labelName = "PCSubtitleLabel" }
    };

    [Tooltip("Actor에 TypewriterSound 필드가 없거나 클립을 못 찾았을 때 사용할 기본 사운드 (선택 사항)")]
    [SerializeField] private AudioClip defaultTypewriterSound;

    [Tooltip("타이핑 사운드 재생용 AudioSource. 비워두면 이 오브젝트에서 자동으로 찾거나 추가합니다.")]
    [SerializeField] private AudioSource typewriterAudioSource;

    private class TypewriterState
    {
        public Label label;
        public string fullText;
        public bool isTyping;
        public Coroutine coroutine;
        public string currentActorName;
        public float charactersPerSecond;
    }
    private List<TypewriterState> _typewriterStates = new List<TypewriterState>();
    private bool _typewriterStatesInitialized = false;
    private Dictionary<string, AudioClip> _actorSoundCache = new Dictionary<string, AudioClip>();

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
        InitializeTypewriterStates();
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
    // ShowSubtitle: 배경 색상 적용 + 타이핑 효과 시작
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

    // Dialogue Entry의 "CharsPerSecond" 커스텀 필드를 확인해서, 있으면 그 값을,
    // 없거나 파싱 실패/0 이하이면 기본값(charactersPerSecond)을 반환한다.
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

        return charactersPerSecond;
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
            ? subtitle.speakerInfo.Name
            : null;

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
            // base.ShowSubtitle()이 이미 Label.text에 전체 텍스트를 넣어놓은 상태
            state.fullText = state.label.text;

            if (!enableTypewriter || string.IsNullOrEmpty(state.fullText))
            {
                state.isTyping = false;
                continue;
            }

            state.label.text = "";
            state.isTyping = true;
            state.coroutine = StartCoroutine(TypewriterCoroutine(state));
        }
    }

    private IEnumerator TypewriterCoroutine(TypewriterState state)
    {
        float cps = state.charactersPerSecond > 0f ? state.charactersPerSecond : charactersPerSecond;
        float delay = cps > 0f ? 1f / cps : 0f;
        int length = state.fullText.Length;

        for (int i = 1; i <= length; i++)
        {
            state.label.text = state.fullText.Substring(0, i);
            PlayTypewriterSound(state.currentActorName);

            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            else
            {
                yield return null;
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

    private void PlayTypewriterSound(string actorName)
    {
        if (typewriterAudioSource == null) return;

        AudioClip clip = GetActorTypewriterSound(actorName);
        if (clip == null) return;

        typewriterAudioSource.PlayOneShot(clip);
    }

    private AudioClip GetActorTypewriterSound(string actorName)
    {
        if (string.IsNullOrEmpty(actorName)) return defaultTypewriterSound;

        if (_actorSoundCache.TryGetValue(actorName, out AudioClip cached))
        {
            return cached != null ? cached : defaultTypewriterSound;
        }

        string clipName = DialogueLua.GetActorField(actorName, ActorSoundFieldName).asString;
        AudioClip clip = null;

        if (!string.IsNullOrEmpty(clipName))
        {
            clip = Resources.Load<AudioClip>(clipName);
            if (clip == null)
            {
                Debug.LogWarning($"[CustomDialogueUI] Actor '{actorName}'의 '{ActorSoundFieldName}' 필드값 " +
                                  $"'{clipName}'에 해당하는 클립을 Resources 폴더에서 찾지 못했습니다.");
            }
        }

        _actorSoundCache[actorName] = clip; // null이어도 캐싱해서 매번 재검색하지 않도록 함
        return clip != null ? clip : defaultTypewriterSound;
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

        // 타이핑 중이었다면 이번 클릭은 "즉시 전체 텍스트 표시"로만 처리하고
        // 대화 자체는 넘기지 않는다. 이미 다 찍힌 상태에서 클릭하면 정상적으로 다음 대사로 진행.
        if (SkipTypewritersIfTyping())
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
