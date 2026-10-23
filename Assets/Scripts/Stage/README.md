# 동적 스테이지 시스템 (Phase 0 + 1 + 2)

## 구성

| 파일 | 역할 |
| --- | --- |
| `DialogueStageDefinition.cs` | 스테이지 1개의 데이터(배경/캐릭터/카메라). ScriptableObject. |
| `DialogueStageBuilder.cs` | 정의를 런타임 씬 오브젝트로 조립/정리하고 대화 시작·종료를 처리. |
| `DialogueStageDatabase.cs` | `LoadStage(stageId)`에서 사용할 스테이지 목록. |
| `CustomSequencerCommands/SequencerCommandLoadStage.cs` | 다음 대화의 스테이지를 예약하는 시퀀서 커맨드. |
| `Editor/DialogueStageEditorTools.cs` | 현재 씬에서 추출, 검증, 동적 테스트 씬 생성. |

## 사용 순서

1. `Assets/Scenes/SampleDialogue.unity`를 엽니다.
2. `Tools > BolDialogue > Stage > Extract Stage From Open Scene`
   - 저장 위치: `Assets/StageData/Stage_SampleDialogue.asset` 권장.
3. 스테이지 에셋이 선택된 상태에서
   `Tools > BolDialogue > Stage > Create Dynamic Test Scene`
   - `Assets/Scenes/SampleDialogue_Dynamic.unity`가 생성됩니다.
4. `SampleDialogue_Dynamic.unity`를 열고 Play.
   - 정적 캐릭터/카메라/배경 대신 `[StageBuilder]`가 스테이지를 다시 조립합니다.

## 대화 수명주기 (Phase 2)

- `CustomDialogueUI.Open()` → `DialogueStageBuilder.EnsureBuilt()`
  - 예약된 스테이지가 있으면 그것을 빌드하고, 없으면 기본 `stage`를 빌드합니다.
  - 첫 대사/시퀀스보다 먼저 실행됩니다.
- `CustomDialogueUI.Close()` → `DialogueStageBuilder.NotifyConversationEnd()`
  - `clearOnConversationEnd`가 켜져 있으면 스테이지를 정리합니다. (기본값: 켜짐)
  - 정리는 `OnConversationEnd` 참가자 핸들러가 끝난 뒤(1프레임 뒤) 실행됩니다.
  - 끄면 대화 사이에도 스테이지를 유지합니다.
- 대화 중 `RequestStage(...)`가 호출되면 즉시 빌드하지 않고
  **다음 대화 시작 시** 적용합니다.
- `LoadStage(stageId)` 시퀀서 커맨드로 다음 대화의 스테이지를 예약할 수 있습니다.
  - `Tools > BolDialogue > Stage > Create Stage Database`로
    `Assets/Resources/DialogueStageDatabase.asset`을 만들고 스테이지를 등록합니다.
  - 빌더의 `database`에 연결하거나, Resources 경로에 두면 자동으로 찾습니다.
  - 대화 밖에서 호출하면 즉시 빌드합니다.

```text
LoadStage(Stage_SampleDialogue);
```

## 이름 계약 (중요)

- `StageCharacterDefinition.actorName` = 시퀀스에서 쓰는 배우 이름 = 런타임 GameObject 이름
- `StageCameraDefinition.cameraName` = `CinemachineFocus` / `CinemachinePosition`에서 쓰는 이름
- 이름이 다르면 `GetSubject`의 `GameObject.Find`와 카메라 이름 매칭이 실패합니다.
- 캐릭터별 카메라는 루트가 아니라 머리 높이의 `_Focus` 자식(예: `Chona_Visual/Chona_Focus`)을 추적합니다.
  `StageCameraDefinition.followTargetPath`에 이 경로가 저장되며, 비어 있으면 빌더가 `_Focus` 자식을 자동 탐색합니다.

## Scene Event

- 표정/몸/추가 애니메이션은 시퀀스의 `SpineTrack(...)`으로 제어합니다.
- Scene Event는 동적 캐릭터를 자동으로 추적하지 않으므로, 새로 추가할 때는
  정적 캐릭터 대신 시퀀스 커맨드를 사용하세요.

## 제약

- 대화 중에는 스테이지를 파괴/재생성하지 마세요.
  `DialogueWorldState` / `DialogueCameraState`가 오브젝트 참조를 백로그 스냅샷으로 보관합니다.
  스테이지 교체는 대화 시작 전 또는 종료 후에만 안전합니다.
- 스테이지 전환은 `RequestStage`/`LoadStage`를 사용하세요. 직접 `Build`를 호출하면
  대화 중 재구성될 수 있습니다.
