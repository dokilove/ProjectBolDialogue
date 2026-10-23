using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BeepSync;

/// <summary>
/// 동적 스테이지 시스템의 에디터 도구.
/// - 현재 열린 씬의 캐릭터/카메라/배경을 Stage Definition 에셋으로 추출
/// - 스테이지 검증
/// - 기존 씬을 복제해 정적 오브젝트를 제거한 동적 테스트 씬 생성
/// </summary>
public static class DialogueStageEditorTools
{
    private const string DefaultStageFolder = "Assets/StageData";
    private const string SampleScenePath = "Assets/Scenes/SampleDialogue.unity";
    private const string DynamicScenePath = "Assets/Scenes/SampleDialogue_Dynamic.unity";

    // ─────────────────────────────────────────────
    // Extract
    // ─────────────────────────────────────────────

    [MenuItem("Tools/BolDialogue/Stage/Extract Stage From Open Scene")]
    public static void ExtractFromOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Stage Extract", "플레이 모드를 종료한 뒤 실행해주세요.", "OK");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("Stage Extract", "유효한 씬이 열려 있지 않습니다.", "OK");
            return;
        }

        var definition = ExtractStageDefinition(scene);

        EnsureFolder(DefaultStageFolder);

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Stage Definition",
            $"Stage_{scene.name}",
            "asset",
            "스테이지 정의를 저장할 위치를 선택하세요.",
            DefaultStageFolder);

        if (string.IsNullOrEmpty(path))
        {
            Object.DestroyImmediate(definition);
            return;
        }

        AssetDatabase.CreateAsset(definition, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = definition;
        EditorGUIUtility.PingObject(definition);

        LogValidationErrors(definition);

        Debug.Log($"[DialogueStage] 스테이지 추출 완료: {path} " +
                  $"(캐릭터 {definition.characters.Count}명, 카메라 {definition.cameras.Count}대)");
    }

    private static DialogueStageDefinition ExtractStageDefinition(Scene scene)
    {
        var definition = ScriptableObject.CreateInstance<DialogueStageDefinition>();
        definition.stageId = scene.name;

        ExtractBackground(definition);
        ExtractCharacters(definition);
        ExtractCameras(definition);

        if (definition.cameras.Count > 0)
        {
            definition.defaultCameraName = FindDefaultCameraName(definition);
        }

        return definition;
    }

    private static void ExtractBackground(DialogueStageDefinition definition)
    {
        var background = GameObject.Find("Background");
        if (background == null) return;

        var renderer = background.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            definition.backgroundSprite = renderer.sprite;
            definition.backgroundPosition = background.transform.position;
            definition.backgroundScale = background.transform.localScale;
            // 원본 씬이 배경과 캐릭터를 같은 sortingOrder(0)로 두고 있어도
            // 런타임 스테이지에서는 배경이 캐릭터 뒤에 깔리도록 보정한다.
            definition.backgroundSortingOrder = Mathf.Min(
                renderer.sortingOrder,
                DialogueStageDefinition.MinBackgroundSortingOrder);
            definition.backgroundSortingLayer = renderer.sortingLayerName;
        }

        var prefab = PrefabUtility.GetCorrespondingObjectFromSource(background) as GameObject;
        if (prefab != null)
        {
            definition.backgroundPrefab = prefab;
        }

        if (definition.backgroundSprite == null && definition.backgroundPrefab == null)
        {
            Debug.LogWarning("[DialogueStage] Background 오브젝트에서 SpriteRenderer 또는 프리팹을 찾지 못했습니다.");
        }
    }

    private static void ExtractCharacters(DialogueStageDefinition definition)
    {
        var controllers = Object.FindObjectsByType<CharacterRootController>(FindObjectsSortMode.None);
        System.Array.Sort(controllers, (a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));

        for (int i = 0; i < controllers.Length; i++)
        {
            var controller = controllers[i];
            if (controller == null) continue;

            var root = controller.gameObject;
            var visual = root.GetComponentInChildren<SpineVisualContainerController>();
            var character = new StageCharacterDefinition
            {
                actorName = root.name,
                prefab = PrefabUtility.GetCorrespondingObjectFromSource(root) as GameObject,
                position = root.transform.position,
                rotation = root.transform.rotation,
                scale = root.transform.localScale,
                facingRight = controller.IsFacingRight,
                beepData = AssetDatabase.LoadAssetAtPath<DialogueBeepData>($"Assets/BeepData/{root.name}.asset")
            };

            if (visual != null)
            {
                character.sortingOrder = visual.CurrentSortingOrder;
                character.depthScale = visual.CurrentDepthScale;
                character.depthOffsetY = visual.CurrentTargetOffsetY;
            }

            // Edit 모드에서는 Awake가 실행되지 않으므로 modelController가 비어 있을 수 있다.
            var model = visual != null ? visual.modelController : null;
            if (model == null)
            {
                model = root.GetComponentInChildren<SpineDualLayerController>();
            }

            if (model != null)
            {
                character.bodyAnimation = model.CurrentBodyAnimation;
                character.faceAnimation = model.CurrentFaceAnimation;
                character.extraAnimation = model.CurrentExtraAnimation;
            }

            if (character.prefab == null)
            {
                Debug.LogWarning($"[DialogueStage] '{root.name}'은 프리팹 인스턴스가 아니어서 prefab 참조를 추출하지 못했습니다.");
            }

            definition.characters.Add(character);
        }
    }

    private static void ExtractCameras(DialogueStageDefinition definition)
    {
        var vcams = Object.FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        System.Array.Sort(vcams, (a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));

        for (int i = 0; i < vcams.Length; i++)
        {
            var vcam = vcams[i];
            if (vcam == null) continue;

            var camera = new StageCameraDefinition
            {
                cameraName = vcam.gameObject.name,
                position = vcam.transform.position,
                rotation = vcam.transform.rotation,
                orthographicSize = vcam.Lens.OrthographicSize,
                fieldOfView = vcam.Lens.FieldOfView,
                priority = vcam.Priority.Value
            };

            var follow = vcam.GetComponent<CinemachineFollow>();
            if (follow != null)
            {
                camera.trackerSettings = follow.TrackerSettings;
                camera.followOffset = follow.FollowOffset;

                var target = vcam.Target.TrackingTarget;
                if (target != null)
                {
                    // 타깃이 캐릭터의 자식 트랜스폼(예: Chona_Focus)이면
                    // 배우 이름 + 루트 기준 상대 경로를 함께 저장한다.
                    var targetController = target.GetComponentInParent<CharacterRootController>();
                    if (targetController != null)
                    {
                        var actorRoot = targetController.transform;
                        camera.followActorName = targetController.gameObject.name;
                        camera.followTargetPath = GetRelativePath(actorRoot, target);
                        camera.followTargetLocalOffset = actorRoot.InverseTransformPoint(target.position);
                    }
                    else
                    {
                        camera.followActorName = target.gameObject.name;
                    }
                }
            }

            definition.cameras.Add(camera);
        }
    }

    private static string FindDefaultCameraName(DialogueStageDefinition definition)
    {
        for (int i = 0; i < definition.cameras.Count; i++)
        {
            if (definition.cameras[i].cameraName == "Wide_Cam")
            {
                return "Wide_Cam";
            }
        }

        return definition.cameras[0].cameraName;
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (root == null || target == null || target == root) return string.Empty;

        var parts = new List<string>();
        var current = target;

        while (current != null && current != root)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        // target이 root의 자손이 아니면 경로를 만들지 않는다.
        if (current != root) return string.Empty;

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void LogValidationErrors(DialogueStageDefinition definition)
    {
        var errors = definition.Validate();
        for (int i = 0; i < errors.Count; i++)
        {
            Debug.LogWarning($"[DialogueStage] {errors[i]}");
        }
    }

    // ─────────────────────────────────────────────
    // Validate
    // ─────────────────────────────────────────────

    [MenuItem("Tools/BolDialogue/Stage/Create Stage Database")]
    public static void CreateStageDatabase()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Stage Database", "플레이 모드를 종료한 뒤 실행해주세요.", "OK");
            return;
        }

        const string resourcesFolder = "Assets/Resources";
        const string databasePath = resourcesFolder + "/DialogueStageDatabase.asset";

        EnsureFolder(resourcesFolder);

        var existing = AssetDatabase.LoadAssetAtPath<DialogueStageDatabase>(databasePath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        var database = ScriptableObject.CreateInstance<DialogueStageDatabase>();
        AssetDatabase.CreateAsset(database, databasePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = database;
        EditorGUIUtility.PingObject(database);
        Debug.Log($"[DialogueStage] Stage Database 생성: {databasePath}");
    }

    [MenuItem("Tools/BolDialogue/Stage/Validate Selected Stage")]
    public static void ValidateSelectedStage()
    {
        var definition = Selection.activeObject as DialogueStageDefinition;
        if (definition == null)
        {
            EditorUtility.DisplayDialog("Stage Validate", "Project 창에서 Stage Definition 에셋을 선택해주세요.", "OK");
            return;
        }

        var errors = definition.Validate();

        if (errors.Count == 0)
        {
            Debug.Log($"[DialogueStage] '{definition.name}' 검증 통과 " +
                      $"(캐릭터 {definition.characters.Count}명, 카메라 {definition.cameras.Count}대)");
        }
        else
        {
            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogWarning($"[DialogueStage] {errors[i]}");
            }
        }
    }

    // ─────────────────────────────────────────────
    // Dynamic Test Scene
    // ─────────────────────────────────────────────

    [MenuItem("Tools/BolDialogue/Stage/Create Dynamic Test Scene")]
    public static void CreateDynamicTestScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Dynamic Test Scene", "플레이 모드를 종료한 뒤 실행해주세요.", "OK");
            return;
        }

        var stage = Selection.activeObject as DialogueStageDefinition;
        if (stage == null)
        {
            stage = FindFirstStageDefinition();
        }

        if (stage == null)
        {
            EditorUtility.DisplayDialog(
                "Dynamic Test Scene",
                "Stage Definition 에셋이 없습니다. 먼저 'Extract Stage From Open Scene'을 실행해주세요.",
                "OK");
            return;
        }

        if (!System.IO.File.Exists(SampleScenePath))
        {
            EditorUtility.DisplayDialog("Dynamic Test Scene", $"원본 씬을 찾을 수 없습니다: {SampleScenePath}", "OK");
            return;
        }

        CreateDynamicTestSceneInternal(stage, true, true);
    }

    private static bool CreateDynamicTestSceneInternal(
        DialogueStageDefinition stage,
        bool overwrite,
        bool interactive)
    {
        if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DynamicScenePath) != null)
        {
            if (!overwrite)
            {
                return false;
            }

            if (interactive && !EditorUtility.DisplayDialog(
                    "Dynamic Test Scene",
                    $"{DynamicScenePath} 이(가) 이미 있습니다. 덮어쓸까요?",
                    "덮어쓰기",
                    "취소"))
            {
                return false;
            }

            AssetDatabase.DeleteAsset(DynamicScenePath);
        }

        if (!AssetDatabase.CopyAsset(SampleScenePath, DynamicScenePath))
        {
            Debug.LogError("[DialogueStage] 씬 복제에 실패했습니다.");
            return false;
        }

        AssetDatabase.Refresh();

        var scene = EditorSceneManager.OpenScene(DynamicScenePath);

        RemoveStaticStageObjects();

        var builderObject = new GameObject("[StageBuilder]");
        var builder = builderObject.AddComponent<DialogueStageBuilder>();

        var serializedBuilder = new SerializedObject(builder);
        serializedBuilder.FindProperty("stage").objectReferenceValue = stage;
        serializedBuilder.FindProperty("buildOnStart").boolValue = true;
        serializedBuilder.FindProperty("ensureOnConversationStart").boolValue = true;
        serializedBuilder.FindProperty("clearOnConversationEnd").boolValue = true;
        serializedBuilder.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = builderObject;
        EditorGUIUtility.PingObject(builderObject);

        Debug.Log($"[DialogueStage] 동적 테스트 씬 생성: {DynamicScenePath}\n" +
                  $"스테이지: {stage.name}\n" +
                  "플레이 모드로 실행하면 정적 오브젝트 대신 Stage Definition으로 스테이지를 다시 조립합니다.");
        return true;
    }

    private static void RemoveStaticStageObjects()
    {
        var controllers = Object.FindObjectsByType<CharacterRootController>(FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
            {
                Object.DestroyImmediate(controllers[i].gameObject);
            }
        }

        var vcams = Object.FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < vcams.Length; i++)
        {
            if (vcams[i] != null)
            {
                Object.DestroyImmediate(vcams[i].gameObject);
            }
        }

        var background = GameObject.Find("Background");
        if (background != null)
        {
            Object.DestroyImmediate(background);
        }
    }

    private static DialogueStageDefinition FindFirstStageDefinition()
    {
        string[] guids = AssetDatabase.FindAssets("t:DialogueStageDefinition");
        if (guids == null || guids.Length == 0) return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<DialogueStageDefinition>(path);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(folder);

        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    // ─────────────────────────────────────────────
    // Batch / CI
    // ─────────────────────────────────────────────

    /// <summary>
    /// 배치 모드/CI용 진입점.
    /// SampleDialogue 씬에서 스테이지를 추출하고 동적 테스트 씬을 생성합니다.
    /// </summary>
    public static void ExtractAndCreateDynamicTestSceneBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[DialogueStage] 플레이 모드에서는 배치 추출을 실행할 수 없습니다.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(SampleScenePath);
        var stage = ExtractStageDefinition(scene);

        EnsureFolder(DefaultStageFolder);

        string assetPath = $"{DefaultStageFolder}/Stage_{scene.name}.asset";
        if (AssetDatabase.LoadAssetAtPath<DialogueStageDefinition>(assetPath) != null)
        {
            AssetDatabase.DeleteAsset(assetPath);
        }

        AssetDatabase.CreateAsset(stage, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        LogValidationErrors(stage);

        bool created = CreateDynamicTestSceneInternal(stage, true, false);

        Debug.Log($"[DialogueStage] 배치 완료: {assetPath}, 동적 씬 생성={created}");
    }
}
