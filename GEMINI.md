# ProjectBolDialogue (대화 연출 및 시네머신 연계 프로젝트)

## 1. 프로젝트 개요
- **환경**: Unity 6, Cinemachine 3.x (`Unity.Cinemachine.CinemachineCamera`, `CinemachineBrain`), Pixel Crushers Dialogue System, Spine 2D (Spine-Unity).
- **작업 공간**: `S:\unityproject\projectboldialogue`
- **핵심 목표**: Pixel Crushers Dialogue System의 시퀀서(Sequencer) 커맨드를 통해 Spine 2D 캐릭터 연출, 카메라 시점 전환(Cinemachine 3), 자막 및 폰트 현지화를 유기적으로 제어하는 비주얼 노벨/대화 연출 시스템 구축.

---

## 2. 주요 카메라 시스템 및 Cinemachine 3 연동 규칙

### 씬 내 주요 가상 카메라 (`SampleDialogue.unity`)
- `Wide_Cam`: 고정 와이드 풀샷 카메라 (OrthographicSize: 1.8)
- `Janghwa_Cam`, `Fafnir_Cam`, `Chona_Cam`, `Vargr_Cam`: 캐릭터별 클로즈업/포커스 카메라 (OrthographicSize: 3, `CinemachineFollow` 적용)
- `Main Camera`: `CinemachineBrain` 탑재, 기본 블렌드(Default Blend)는 `EaseInOut 0.5초`.

### `CinemachineFocus` 시퀀서 커맨드 (`SequencerCommandCinemachineFocus.cs`)
- **문법**:
  - `CinemachineFocus(vcamName, [blendDurationOrCut], [easeStyle], [priority])`
- **전환 시간 제어 및 즉시 컷(Cut)**:
  - `0`, `"cut"`, `"instant"`, `"snap"` 입력 시 순식간에 즉시 컷 전환.
  - 숫자(예: `0.5`, `1.2` 등) 입력 시 해당 시간 동안 부드러운 블렌딩 전환.
  - 파라미터 생략 시 씬 원래의 `DefaultBlend` 사용.
- **★ 하드 컷(0초 전환) 시 깜빡임/1프레임 렉 해결 원리 (필수 숙지)**:
  1. **우선순위 큐 강제 재정렬 (`targetVcam.Prioritize()`)**:
     - Cinemachine 3에서는 `vcam.Priority` 값만 변경하면 내부 정렬 플래그(`m_ActiveCamerasAreSorted`)가 갱신되지 않아, 해당 프레임 동안 이전 카메라가 유지되다가 다음 프레임 `Update()`에서야 반영되는 1프레임 지연이 발생함.
     - 반드시 `targetVcam.Prioritize()`를 호출하여 당일 프레임에 우선순위 큐를 강제 재정렬해야 함.
  2. **가상 카메라 상태 선행 계산 (`targetVcam.UpdateCameraState(Vector3.up, -1f)`)**:
     - 대상 가상 카메라의 위치, 회전, 렌즈(`OrthographicSize`/FOV)를 호출 즉시 선행 계산.
  3. **브레인 상태 초기화 (`brain.ActiveBlend = null`, `brain.ResetState()`)**:
     - 진행 중이던 블렌드를 즉시 중단하고 루트 프레임을 리셋하여 이전 카메라 이력 없이 새 카메라를 즉시 채택.
  4. **메인 카메라 직접 스냅 동기화**:
     - `brain.OutputCamera.transform.SetPositionAndRotation(...)` 및 `orthographicSize`/`fieldOfView`를 계산된 `targetVcam.State`로 즉시 스냅 동기화하여 LateUpdate 이전/중간 렌더링에 의한 1프레임 공백 및 깜빡임을 100% 원천 차단.
  5. **주의사항**:
     - `brain.ManualUpdate()`는 `UpdateMethod != ManualUpdate`일 때 에러 로그를 발생시키므로 호출 금지.
     - `PreviousStateIsValid = false` 임의 호출 금지 (트랜스폼 트래킹 리셋으로 인한 튀는 현상 유발).
     - 0초 컷 구현 시 미세 보간(0.015초 등)을 사용하지 말고 순수 `Styles.Cut(0초)`과 상태 스냅 사용.

---

## 3. 커스텀 시퀀서 커맨드 체계 (`Assets/Scripts/CustomSequencerCommands/`)

### Spine 관련 커맨드
1. **`SpineTRS`**:
   - `SpineTRS(actor, x, y, rot, duration, [scale], [pivot], [ease], [flipMode])`
   - 이동(Translate), 회전(Rotate), 스케일(Scale)을 한 번에 제어.
   - `flipMode` 지원:
     - `"noflip"`: 이동 방향에 따른 자동 Flip을 방지하고 현재 바라보는 방향 유지.
     - `"flip"`: 강제 방향 반전.
   - 단일 축 스케일 문자열 지원: `"1, 1.2"` 등 가로/세로 개별 스케일 지원.
2. **`SpineMoveTo` / `SpineMoveRotate` / `SpineTransform`**:
   - 캐릭터 위치 이동 및 회전 보간 연출.
3. **`SpineFocus`**:
   - `SpineFocus(actor, [sortingOrder/keep])`, `SpineFocus(all)`, `SpineFocus(none)`
   - 대상 캐릭터의 밝기/색상 강조 및 정렬 순서(`sortingOrder`) 상승, 다른 캐릭터는 음영/어둡게 처리.
4. **`SpineDepth` / `SpineOrder` / `SpineSortingOrder`**:
   - 2D 캐릭터 간 앞뒤 레이어 깊이, 원근 스케일 및 렌더링 소팅 오더 제어.
5. **`SpineTrack`**:
   - Spine 애니메이션 트랙 재생 및 표정/모션 변경.

### Cinemachine 관련 커맨드
1. **`CinemachineFocus`**: 가상 카메라 우선순위 전환 및 블렌드/컷 시간 제어.
2. **`CinemachineMove` / `CinemachinePosition` / `CinemachineZoom` / `OrthographicZoom`**: 카메라 트랜스폼 및 줌 보간 제어.

---

## 4. UI 및 폰트 현지화 (UI Toolkit)
- **`UIToolkitFontLocalizer.cs`**:
  - 언어 설정(ko, ja 등)에 따라 UI Toolkit 라벨들의 폰트 에셋을 동적으로 치환(`ONE Mobile POP SDF` 등).
  - Pixel Crushers `UILocalizationManager`와 연동되어 언어 전환 시 폰트 자동 갱신.
