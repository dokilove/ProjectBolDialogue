using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine.TargetTracking;
using BeepSync;

/// <summary>
/// 스테이지에 배치할 캐릭터 1명의 정의.
/// actorName은 시퀀스에서 사용하는 이름(GameObject 이름)과 반드시 일치해야 합니다.
/// </summary>
[System.Serializable]
public class StageCharacterDefinition
{
    [Tooltip("시퀀스에서 사용하는 배우 이름. 런타임 GameObject 이름으로 그대로 사용됩니다.")]
    public string actorName;

    [Tooltip("캐릭터 프리팹 (CharacterRootController 포함)")]
    public GameObject prefab;

    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
    public Vector3 scale = Vector3.one;

    [Tooltip("오른쪽을 바라보는 상태로 시작할지 여부")]
    public bool facingRight;

    [Tooltip("Spine MeshRenderer의 sortingOrder")]
    public int sortingOrder;

    [Tooltip("SpineDepth 기준 스케일")]
    public float depthScale = 1f;

    [Tooltip("SpineDepth 기준 Y 오프셋")]
    public float depthOffsetY;

    [Header("Initial Spine Animation (선택)")]
    [Tooltip("비워두면 프리팹의 기본 상태를 유지합니다.")]
    public string bodyAnimation;

    [Tooltip("비워두면 프리팹의 기본 상태를 유지합니다.")]
    public string faceAnimation;

    [Tooltip("비워두면 프리팹의 기본 상태를 유지합니다.")]
    public string extraAnimation;

    [Tooltip("연출용 비프 데이터 (선택). CustomDialogueUI의 이름 매칭과 별개로 보관용.")]
    public DialogueBeepData beepData;
}

/// <summary>
/// 스테이지에 배치할 Cinemachine 카메라 1대의 정의.
/// cameraName은 시퀀스의 CinemachineFocus/Position에서 사용하는 이름과 일치해야 합니다.
/// </summary>
[System.Serializable]
public class StageCameraDefinition
{
    [Tooltip("시퀀스에서 사용하는 카메라 이름. 런타임 GameObject 이름으로 그대로 사용됩니다.")]
    public string cameraName;

    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;

    [Tooltip("출력 카메라가 Orthographic일 때 사용되는 크기")]
    public float orthographicSize = 3f;

    [Tooltip("출력 카메라가 Perspective일 때 사용되는 FOV")]
    public float fieldOfView = 60f;

    [Tooltip("Cinemachine 우선순위")]
    public int priority;

    [Tooltip("비워두면 고정 카메라. 값이 있으면 해당 배우를 추적합니다.")]
    public string followActorName;

    [Tooltip("배우 루트 기준 추적 대상의 상대 경로 (예: Chona_Visual/Chona_Focus). 비어 있으면 루트를 추적합니다.")]
    public string followTargetPath;

    [Tooltip("경로를 찾지 못했을 때 사용할, 배우 루트 기준 로컬 오프셋")]
    public Vector3 followTargetLocalOffset;

    [Tooltip("추적 대상으로부터의 오프셋")]
    public Vector3 followOffset = new Vector3(0f, 0f, -10f);

    [Tooltip("Cinemachine 추적 설정 (BindingMode, Damping)")]
    public TrackerSettings trackerSettings = TrackerSettings.Default;
}

/// <summary>
/// 캐릭터·배경·카메라를 런타임에 조립하기 위한 스테이지 정의.
/// 대화 시작 전에 이 데이터로 스테이지를 구성하면, 기존 씬에 정적으로 배치한 것과
/// 동일한 이름/상태를 만들어 시퀀스 커맨드가 그대로 동작합니다.
/// </summary>
[CreateAssetMenu(menuName = "BolDialogue/Stage Definition", fileName = "Stage", order = 0)]
public class DialogueStageDefinition : ScriptableObject
{
    /// <summary>배경이 캐릭터보다 뒤에 깔리도록 보장하는 최대 sortingOrder.</summary>
    public const int MinBackgroundSortingOrder = -10;

    [Header("Identity")]
    [Tooltip("스테이지 식별자 (비워두면 에셋 이름 사용)")]
    public string stageId;

    [Header("Background")]
    [Tooltip("SpriteRenderer로 생성할 배경 스프라이트")]
    public Sprite backgroundSprite;

    [Tooltip("Sprite 대신 프리팹으로 생성할 경우 사용")]
    public GameObject backgroundPrefab;

    public Vector3 backgroundPosition = new Vector3(0f, 5f, 0f);
    public Vector3 backgroundScale = Vector3.one;

    [Tooltip("배경 SpriteRenderer의 sortingOrder. 캐릭터(0 이상)보다 뒤에 깔리도록 -10 이하를 권장합니다.")]
    public int backgroundSortingOrder = MinBackgroundSortingOrder;

    public string backgroundSortingLayer = "Default";

    [Header("Cameras")]
    [Tooltip("스테이지 시작 시 활성화할 카메라 이름")]
    public string defaultCameraName = "Wide_Cam";

    public List<StageCameraDefinition> cameras = new List<StageCameraDefinition>();

    [Header("Characters")]
    public List<StageCharacterDefinition> characters = new List<StageCharacterDefinition>();

    /// <summary>
    /// 스테이지 정의의 문제를 검사합니다. 빈 목록이면 정상입니다.
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        var characterNames = new HashSet<string>();

        for (int i = 0; i < characters.Count; i++)
        {
            var character = characters[i];
            if (character == null)
            {
                errors.Add($"characters[{i}]가 null입니다.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(character.actorName))
            {
                errors.Add($"characters[{i}]의 actorName이 비어 있습니다.");
            }
            else if (!characterNames.Add(character.actorName))
            {
                errors.Add($"캐릭터 이름이 중복됩니다: '{character.actorName}'");
            }

            if (character.prefab == null)
            {
                errors.Add($"'{character.actorName}' 캐릭터의 prefab이 없습니다.");
            }
        }

        var cameraNames = new HashSet<string>();

        for (int i = 0; i < cameras.Count; i++)
        {
            var camera = cameras[i];
            if (camera == null)
            {
                errors.Add($"cameras[{i}]가 null입니다.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(camera.cameraName))
            {
                errors.Add($"cameras[{i}]의 cameraName이 비어 있습니다.");
            }
            else if (!cameraNames.Add(camera.cameraName))
            {
                errors.Add($"카메라 이름이 중복됩니다: '{camera.cameraName}'");
            }

            if (!string.IsNullOrWhiteSpace(camera.followActorName) &&
                !characterNames.Contains(camera.followActorName))
            {
                errors.Add($"카메라 '{camera.cameraName}'의 추적 대상 '{camera.followActorName}'이 캐릭터 목록에 없습니다.");
            }
        }

        if (!string.IsNullOrWhiteSpace(defaultCameraName) &&
            !cameraNames.Contains(defaultCameraName))
        {
            errors.Add($"defaultCameraName '{defaultCameraName}'이 카메라 목록에 없습니다.");
        }

        return errors;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            stageId = name;
        }
    }
}
