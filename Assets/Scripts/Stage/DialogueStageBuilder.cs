using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using PixelCrushers.DialogueSystem;

/// <summary>
/// DialogueStageDefinition을 런타임에 실제 씬 오브젝트로 조립합니다.
///
/// - 캐릭터: 프리팹을 생성하고 GameObject 이름을 actorName으로 맞춥니다.
///   (시퀀스의 GetSubject → GameObject.Find(이름) 계약 유지)
/// - 카메라: cameraName으로 CinemachineCamera를 생성합니다.
///   (CinemachineFocus/Position의 이름 매칭 계약 유지)
/// - 배경: SpriteRenderer 또는 프리팹으로 생성합니다.
///
/// 대화 중에는 파괴/재생성하지 마세요. DialogueWorldState가 오브젝트 참조를
/// 스냅샷으로 보관하므로, 스테이지 교체는 대화 시작 전/종료 후에만 해야 합니다.
/// </summary>
[DefaultExecutionOrder(-100)]
public class DialogueStageBuilder : MonoBehaviour
{
    [Header("Stage")]
    [SerializeField] private DialogueStageDefinition stage;
    [SerializeField] private bool buildOnStart = true;
    [SerializeField] private bool clearOnDestroy = true;

    [Header("Conversation Lifecycle")]
    [Tooltip("대화 시작 시 스테이지가 없으면 기본 stage를 자동으로 빌드합니다.")]
    [SerializeField] private bool ensureOnConversationStart = true;

    [Tooltip("대화 종료 시 스테이지를 정리합니다. 다음 대화 시작 시 다시 빌드됩니다.")]
    [SerializeField] private bool clearOnConversationEnd = true;

    [Tooltip("LoadStage(stageId)에서 사용할 스테이지 목록. 비워두면 기본 stage만 ID로 찾을 수 있습니다.")]
    [SerializeField] private DialogueStageDatabase database;

    [Header("Options")]
    [Tooltip("생성 후 기본 카메라로 즉시 전환하기 위해 Brain 상태를 리셋합니다.")]
    [SerializeField] private bool resetBrainOnBuild = true;

    [Tooltip("빌드 결과/누락된 참조를 콘솔에 출력합니다.")]
    [SerializeField] private bool logBuild = true;

    private GameObject stageRoot;
    private DialogueStageDefinition activeDefinition;
    private DialogueStageDefinition pendingStage;
    private bool clearScheduled;
    private readonly Dictionary<string, GameObject> characters = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, CinemachineCamera> cameras = new Dictionary<string, CinemachineCamera>();
    private readonly List<CinemachineCamera> spawnedCameras = new List<CinemachineCamera>();

    /// <summary>현재 활성 스테이지 빌더 (없으면 null).</summary>
    public static DialogueStageBuilder Current { get; private set; }

    public DialogueStageDefinition Stage => stage;
    public DialogueStageDefinition ActiveDefinition => activeDefinition;
    public DialogueStageDefinition PendingStage => pendingStage;
    public bool IsBuilt => stageRoot != null;
    public Transform Root => stageRoot != null ? stageRoot.transform : null;
    public IReadOnlyDictionary<string, GameObject> Characters => characters;
    public IReadOnlyDictionary<string, CinemachineCamera> Cameras => cameras;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Enter Play Mode Options에서 도메인 리로드를 끈 경우에도 이전 참조가 남지 않게 한다.
        Current = null;
    }

    private void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogWarning("[DialogueStageBuilder] 씬에 빌더가 여러 개 있습니다. 마지막 빌더를 Current로 사용합니다.");
        }

        Current = this;
    }

    private void Start()
    {
        if (buildOnStart)
        {
            Build(stage);
        }
    }

    /// <summary>
    /// 대화 시작 시 호출됩니다. 예약된 스테이지가 있으면 그것을,
    /// 없으면 아직 빌드되지 않은 기본 stage를 빌드합니다.
    /// </summary>
    public void EnsureBuilt()
    {
        // 새 대화가 시작되면, 직전 대화 종료 시 예약한 정리를 취소한다.
        // (대화가 끝난 직후 같은 프레임에 다음 대화가 시작되는 경우 보호)
        clearScheduled = false;

        // 예약된 전환은 ensureOnConversationStart와 무관하게 대화 경계에서 적용한다.
        if (pendingStage != null)
        {
            var next = pendingStage;
            pendingStage = null;
            Build(next);
            return;
        }

        if (!ensureOnConversationStart && !IsBuilt) return;

        if (!IsBuilt)
        {
            if (stage == null)
            {
                if (logBuild)
                {
                    Debug.LogWarning("[DialogueStageBuilder] 대화 시작 시 빌드할 기본 stage가 없습니다.");
                }
                return;
            }

            Build(stage);
        }
    }

    /// <summary>
    /// 다음 대화에서 사용할 스테이지를 예약합니다.
    /// 대화 중이면 즉시 빌드하지 않고, 다음 대화 시작 시 적용합니다.
    /// </summary>
    public void RequestStage(DialogueStageDefinition next)
    {
        if (next == null) return;

        if (next == activeDefinition && IsBuilt)
        {
            // 현재 스테이지를 다시 요청하면 예약된 전환/정리를 취소한다.
            pendingStage = null;
            clearScheduled = false;
            return;
        }

        if (DialogueManager.isConversationActive)
        {
            pendingStage = next;

            if (logBuild)
            {
                Debug.Log($"[DialogueStageBuilder] 다음 대화 시작 시 스테이지 전환 예약: '{next.name}'");
            }

            return;
        }

        Build(next);
    }

    /// <summary>
    /// 스테이지 ID로 다음 대화의 스테이지를 예약합니다.
    /// </summary>
    public bool RequestStage(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            Debug.LogWarning("[DialogueStageBuilder] LoadStage에 stageId가 비어 있습니다.");
            return false;
        }

        DialogueStageDefinition next = null;

        if (database == null)
        {
            // 편의용: Assets/Resources/DialogueStageDatabase.asset이 있으면 자동으로 사용한다.
            database = Resources.Load<DialogueStageDatabase>("DialogueStageDatabase");
        }

        if (database != null)
        {
            next = database.Find(stageId);
        }

        // 데이터베이스가 없거나 목록에 없으면 현재 기본 stage의 ID와 비교한다.
        if (next == null && stage != null && string.Equals(stage.stageId, stageId, System.StringComparison.OrdinalIgnoreCase))
        {
            next = stage;
        }

        // 마지막 fallback: 이미 로드되어 있는 스테이지 에셋에서 ID/이름으로 찾는다.
        if (next == null)
        {
            var loadedStages = Resources.FindObjectsOfTypeAll<DialogueStageDefinition>();
            for (int i = 0; i < loadedStages.Length; i++)
            {
                var loaded = loadedStages[i];
                if (loaded == null) continue;

                if (string.Equals(loaded.stageId, stageId, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(loaded.name, stageId, System.StringComparison.OrdinalIgnoreCase))
                {
                    next = loaded;
                    break;
                }
            }
        }

        if (next == null)
        {
            Debug.LogWarning($"[DialogueStageBuilder] '{stageId}' 스테이지를 찾지 못했습니다. Stage Database를 확인하세요.");
            return false;
        }

        RequestStage(next);
        return true;
    }

    /// <summary>
    /// 대화 종료 시 호출됩니다. 설정에 따라 스테이지를 정리합니다.
    /// 예약된 스테이지는 다음 대화 시작까지 유지됩니다.
    /// </summary>
    public void NotifyConversationEnd()
    {
        if (!clearOnConversationEnd)
        {
            clearScheduled = false;
            return;
        }

        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            Clear();
            return;
        }

        if (clearScheduled) return;

        // ConversationView.Close()는 UI.Close() 이후 OnConversationEnd 참가자 메시지를 보낸다.
        // 같은 프레임에 스테이지를 지우면 그 핸들러들이 캐릭터를 찾지 못하므로 한 프레임 미룬다.
        clearScheduled = true;
        StartCoroutine(ClearAfterConversationEnd());
    }

    private IEnumerator ClearAfterConversationEnd()
    {
        yield return null;

        if (!clearScheduled) yield break;

        clearScheduled = false;
        Clear();
    }

    [ContextMenu("Rebuild Stage")]
    private void RebuildFromContextMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DialogueStageBuilder] Rebuild는 플레이 모드에서만 사용하세요.");
            return;
        }

        Build(stage);
    }

    private void OnDestroy()
    {
        if (clearOnDestroy)
        {
            Clear();
        }

        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>
    /// 스테이지를 새로 조립합니다. 기존 스테이지는 먼저 정리됩니다.
    /// </summary>
    public void Build(DialogueStageDefinition definition)
    {
        // UI.Open()은 ConversationController 생성 직후(첫 상태 설정 전)에도 호출된다.
        // currentConversationState가 실제로 설정된 뒤의 Build만 대화 중 재구성으로 본다.
        if (Application.isPlaying &&
            DialogueManager.isConversationActive &&
            DialogueManager.currentConversationState != null)
        {
            Debug.LogWarning("[DialogueStageBuilder] 대화 중 Build가 호출되었습니다. " +
                             "백로그 스냅샷의 오브젝트 참조가 깨질 수 있습니다. " +
                             "스테이지 전환은 RequestStage를 사용하세요.");
        }

        Clear();
        pendingStage = null;

        if (definition == null)
        {
            Debug.LogWarning("[DialogueStageBuilder] Stage Definition이 지정되지 않았습니다.");
            return;
        }

        stage = definition;
        activeDefinition = definition;

        var errors = definition.Validate();
        if (errors.Count > 0 && logBuild)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogWarning($"[DialogueStageBuilder] {errors[i]}");
            }
        }

        string stageName = string.IsNullOrWhiteSpace(definition.stageId)
            ? definition.name
            : definition.stageId;

        stageRoot = new GameObject($"[Stage] {stageName}");
        Current = this;

        BuildBackground(definition);
        BuildCharacters(definition);
        BuildCameras(definition);
        RegisterCharactersToGroupManager();
        BindCameraEnumManagers();
        ActivateDefaultCamera(definition);

        if (logBuild)
        {
            Debug.Log($"[DialogueStageBuilder] '{stageName}' 빌드 완료 " +
                      $"(캐릭터 {characters.Count}명, 카메라 {cameras.Count}대)");
        }
    }

    /// <summary>
    /// 현재 스테이지를 제거합니다. 카메라는 줌 컨트롤러에서 등록 해제합니다.
    /// </summary>
    public void Clear()
    {
        clearScheduled = false;

        var zoom = CinemachineZoomController.InstanceOrNull;
        if (zoom != null)
        {
            for (int i = 0; i < spawnedCameras.Count; i++)
            {
                if (spawnedCameras[i] != null)
                {
                    zoom.UnregisterCamera(spawnedCameras[i]);
                }
            }
        }

        // Destroy는 프레임 끝에 실제로 제거된다. 같은 프레임에 새 스테이지를 빌드해도
        // GameObject.Find가 이전 캐릭터/카메라를 찾지 않도록 이름을 먼저 바꾼다.
        if (stageRoot != null)
        {
            // Destroy는 프레임 끝에 실행되므로, 전환 중 이전 스테이지가 한 프레임 보이거나
            // DialogueWorldState/CameraState에 섞이지 않도록 즉시 비활성화한다.
            stageRoot.SetActive(false);
            stageRoot.name = $"[Destroying] {stageRoot.name}";

            foreach (var pair in characters)
            {
                if (pair.Value != null)
                {
                    pair.Value.name = $"[Destroying] {pair.Key}";
                }
            }

            foreach (var pair in cameras)
            {
                if (pair.Value != null)
                {
                    pair.Value.gameObject.name = $"[Destroying] {pair.Key}";
                }
            }
        }

        spawnedCameras.Clear();
        characters.Clear();
        cameras.Clear();
        activeDefinition = null;

        if (stageRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(stageRoot);
            }
            else
            {
                DestroyImmediate(stageRoot);
            }
        }

        stageRoot = null;

        // Current는 유지한다. Clear 후에도 같은 빌더가 다음 대화에서 다시 빌드할 수 있어야 한다.
    }

    public GameObject GetCharacter(string actorName)
    {
        if (string.IsNullOrEmpty(actorName)) return null;

        if (characters.TryGetValue(actorName, out var character) && character != null)
        {
            return character;
        }

        // 이름 계약을 유지한 채로 외부에서 찾을 수 있도록 fallback 제공.
        return GameObject.Find(actorName);
    }

    public CinemachineCamera GetCamera(string cameraName)
    {
        if (string.IsNullOrEmpty(cameraName)) return null;

        if (cameras.TryGetValue(cameraName, out var camera) && camera != null)
        {
            return camera;
        }

        return null;
    }

    // ─────────────────────────────────────────────
    // Build Steps
    // ─────────────────────────────────────────────

    private void BuildBackground(DialogueStageDefinition definition)
    {
        if (definition.backgroundPrefab != null)
        {
            var instance = Instantiate(definition.backgroundPrefab, stageRoot.transform);
            instance.name = "Background";
            instance.transform.SetPositionAndRotation(definition.backgroundPosition, Quaternion.identity);
            instance.transform.localScale = definition.backgroundScale;
            return;
        }

        if (definition.backgroundSprite == null)
        {
            return;
        }

        var background = new GameObject("Background");
        background.transform.SetParent(stageRoot.transform, false);
        background.transform.position = definition.backgroundPosition;
        background.transform.localScale = definition.backgroundScale;

        var renderer = background.AddComponent<SpriteRenderer>();
        renderer.sprite = definition.backgroundSprite;
        // 배경은 항상 캐릭터(기본 sortingOrder 0 이상) 뒤에 깔리도록 보정한다.
        // 일부 플랫폼(WebGL/URP)에서는 같은 sortingOrder의 투명 오브젝트 정렬이
        // 에디터와 다르게 동작해 캐릭터가 배경에 가려질 수 있다.
        renderer.sortingOrder = Mathf.Min(
            definition.backgroundSortingOrder,
            DialogueStageDefinition.MinBackgroundSortingOrder);

        if (!string.IsNullOrEmpty(definition.backgroundSortingLayer))
        {
            renderer.sortingLayerName = definition.backgroundSortingLayer;
        }

        if (logBuild)
        {
            Debug.Log($"[DialogueStageBuilder] 배경 배치 완료 " +
                      $"(sortingOrder={renderer.sortingOrder}, layer={renderer.sortingLayerName})");
        }
    }

    private void BuildCharacters(DialogueStageDefinition definition)
    {
        for (int i = 0; i < definition.characters.Count; i++)
        {
            var character = definition.characters[i];
            if (character == null || string.IsNullOrWhiteSpace(character.actorName)) continue;

            if (characters.ContainsKey(character.actorName))
            {
                Debug.LogWarning($"[DialogueStageBuilder] 캐릭터 이름 중복으로 건너뜁니다: '{character.actorName}'");
                continue;
            }

            if (character.prefab == null)
            {
                Debug.LogWarning($"[DialogueStageBuilder] '{character.actorName}' 프리팹이 없어 건너뜁니다.");
                continue;
            }

            var instance = Instantiate(character.prefab, stageRoot.transform);

            // 시퀀스는 GameObject 이름으로 배우를 찾으므로 이름을 반드시 맞춘다.
            instance.name = character.actorName;
            instance.transform.position = character.position;
            instance.transform.rotation = character.rotation;
            instance.transform.localScale = character.scale;

            var controller = instance.GetComponent<CharacterRootController>();
            if (controller == null)
            {
                controller = instance.GetComponentInChildren<CharacterRootController>();
            }

            if (controller == null)
            {
                Debug.LogWarning($"[DialogueStageBuilder] '{character.actorName}'에 CharacterRootController가 없습니다.");
                DestroyInstance(instance);
                continue;
            }

            controller.SetFacing(character.facingRight);

            var visual = instance.GetComponentInChildren<SpineVisualContainerController>();
            if (visual != null)
            {
                visual.SetDepth(
                    character.depthScale,
                    character.depthOffsetY,
                    character.sortingOrder,
                    0f);

                var model = visual.modelController;
                if (model != null)
                {
                    if (!string.IsNullOrEmpty(character.bodyAnimation))
                    {
                        model.SetBodyAnimation(character.bodyAnimation);
                    }

                    if (!string.IsNullOrEmpty(character.faceAnimation))
                    {
                        model.SetFaceAnimation(character.faceAnimation);
                    }

                    if (!string.IsNullOrEmpty(character.extraAnimation))
                    {
                        model.SetExtraAnimation(character.extraAnimation);
                    }
                }
            }

            if (logBuild)
            {
                var meshRenderer = visual != null && visual.modelController != null
                    ? visual.modelController.GetComponent<MeshRenderer>()
                    : null;

                Debug.Log($"[DialogueStageBuilder] '{character.actorName}' 배치 완료 " +
                          $"(pos={instance.transform.position}, active={instance.activeInHierarchy}, " +
                          $"renderer={(meshRenderer != null ? meshRenderer.enabled.ToString() : "none")}, " +
                          $"sortingOrder={(meshRenderer != null ? meshRenderer.sortingOrder.ToString() : "-")})");
            }

            characters[character.actorName] = instance;
        }
    }

    private void BuildCameras(DialogueStageDefinition definition)
    {
        for (int i = 0; i < definition.cameras.Count; i++)
        {
            var cameraDefinition = definition.cameras[i];
            if (cameraDefinition == null || string.IsNullOrWhiteSpace(cameraDefinition.cameraName)) continue;

            if (cameras.ContainsKey(cameraDefinition.cameraName))
            {
                Debug.LogWarning($"[DialogueStageBuilder] 카메라 이름 중복으로 건너뜁니다: '{cameraDefinition.cameraName}'");
                continue;
            }

            var cameraObject = new GameObject(cameraDefinition.cameraName);
            cameraObject.transform.SetParent(stageRoot.transform, false);
            cameraObject.transform.position = cameraDefinition.position;
            cameraObject.transform.rotation = cameraDefinition.rotation;

            var vcam = cameraObject.AddComponent<CinemachineCamera>();

            var lens = vcam.Lens;
            lens.OrthographicSize = cameraDefinition.orthographicSize;
            lens.FieldOfView = cameraDefinition.fieldOfView;
            vcam.Lens = lens;
            vcam.Priority = cameraDefinition.priority;

            if (!string.IsNullOrWhiteSpace(cameraDefinition.followActorName))
            {
                var target = GetCharacter(cameraDefinition.followActorName);

                if (target != null)
                {
                    var followTransform = ResolveFollowTarget(target.transform, cameraDefinition);

                    var cameraTarget = vcam.Target;
                    cameraTarget.TrackingTarget = followTransform;
                    vcam.Target = cameraTarget;

                    var follow = cameraObject.AddComponent<CinemachineFollow>();
                    follow.FollowOffset = cameraDefinition.followOffset;

                    var trackerSettings = cameraDefinition.trackerSettings;
                    trackerSettings.Validate();
                    follow.TrackerSettings = trackerSettings;
                }
                else
                {
                    Debug.LogWarning($"[DialogueStageBuilder] 카메라 '{cameraDefinition.cameraName}'의 추적 대상 " +
                                     $"'{cameraDefinition.followActorName}'을 찾지 못했습니다.");
                }
            }

            cameras[cameraDefinition.cameraName] = vcam;
            spawnedCameras.Add(vcam);

            var zoom = CinemachineZoomController.InstanceOrNull;
            if (zoom != null)
            {
                zoom.RegisterCamera(vcam);
            }
        }
    }

    /// <summary>
    /// 카메라가 바라볼 실제 트랜스폼을 결정합니다.
    /// 원본 씬의 캐릭터 카메라는 루트가 아니라 머리 높이의 `_Focus` 자식을 추적합니다.
    /// </summary>
    private Transform ResolveFollowTarget(Transform actorRoot, StageCameraDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.followTargetPath))
        {
            var found = actorRoot.Find(definition.followTargetPath);
            if (found != null)
            {
                return found;
            }

            // 프리팹 구조가 바뀌어 경로를 찾지 못하면 저장된 로컬 오프셋에 마커를 만든다.
            return CreateFollowMarker(actorRoot, definition.followTargetLocalOffset);
        }

        // 구버전 스테이지 에셋 호환: {Character}_Focus 자식을 자동 탐색.
        var focus = FindFocusChild(actorRoot);
        if (focus != null)
        {
            return focus;
        }

        return actorRoot;
    }

    private static Transform FindFocusChild(Transform actorRoot)
    {
        // 예: Chona_Visual/Chona_Focus
        var direct = actorRoot.Find(actorRoot.name + "_Focus");
        if (direct != null) return direct;

        var transforms = actorRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            var transform = transforms[i];
            if (transform != null &&
                transform != actorRoot &&
                transform.name.EndsWith("_Focus", System.StringComparison.Ordinal))
            {
                return transform;
            }
        }

        return null;
    }

    private Transform CreateFollowMarker(Transform actorRoot, Vector3 localOffset)
    {
        var marker = new GameObject($"{actorRoot.name}_CameraTarget").transform;
        marker.SetParent(actorRoot, false);
        marker.localPosition = localOffset;
        return marker;
    }

    private void RegisterCharactersToGroupManager()
    {
        var groupManager = SpineCharacterGroupManager.Instance;
        if (groupManager == null) return;

        groupManager.characters.Clear();

        foreach (var pair in characters)
        {
            if (pair.Value == null) continue;

            var visual = pair.Value.GetComponentInChildren<SpineVisualContainerController>();
            if (visual != null)
            {
                groupManager.characters.Add(visual);
            }
        }
    }

    /// <summary>
    /// 씬 이벤트용 CameraEnumManager가 정적 카메라를 참조하고 있으면
    /// 동적 카메라로 다시 연결합니다. (이 프로젝트의 카메라 이름 규칙 기준)
    /// </summary>
    private void BindCameraEnumManagers()
    {
        var managers = FindObjectsByType<CameraEnumManager>(FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            var manager = managers[i];
            if (manager == null) continue;

            manager.vCams = new[]
            {
                GetCamera("Fafnir_Cam"),
                GetCamera("Chona_Cam"),
                GetCamera("Janghwa_Cam"),
                GetCamera("Vargr_Cam"),
                GetCamera("Wide_Cam")
            };
        }
    }

    private void ActivateDefaultCamera(DialogueStageDefinition definition)
    {
        if (cameras.Count == 0) return;

        CinemachineCamera defaultCamera = null;

        if (!string.IsNullOrWhiteSpace(definition.defaultCameraName))
        {
            cameras.TryGetValue(definition.defaultCameraName, out defaultCamera);
        }

        if (defaultCamera == null)
        {
            // 지정이 없으면 생성된 첫 카메라를 사용한다.
            foreach (var pair in cameras)
            {
                defaultCamera = pair.Value;
                break;
            }
        }

        if (defaultCamera == null) return;

        int highestPriority = int.MinValue;
        foreach (var pair in cameras)
        {
            if (pair.Value == null) continue;
            highestPriority = Mathf.Max(highestPriority, pair.Value.Priority.Value);
        }

        // 기본 카메라가 반드시 선택되도록 최고 우선순위보다 하나 높인다.
        defaultCamera.Priority = highestPriority + 1;
        defaultCamera.Prioritize();

        if (!resetBrainOnBuild) return;

        var brain = Camera.main != null
            ? Camera.main.GetComponent<CinemachineBrain>()
            : null;

        if (brain == null)
        {
            brain = FindAnyObjectByType<CinemachineBrain>();
        }

        if (brain != null)
        {
            brain.ActiveBlend = null;
            brain.ResetState();
        }
    }

    private void DestroyInstance(GameObject instance)
    {
        if (instance == null) return;

        if (Application.isPlaying)
        {
            Destroy(instance);
        }
        else
        {
            DestroyImmediate(instance);
        }
    }
}
