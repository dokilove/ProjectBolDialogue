using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class Drag2DObject : MonoBehaviour
{
    // 올바른 Input Actions 참조
    public InputActionReference pointAction;
    public InputActionReference clickAction;

    [Header("Reference Transform")]
    public Transform referenceTransform; // 참조할 GameObject의 Transform (null이면 자신의 위치 기준)

    [Header("Directional Bounds (World Space)")]
    // 초기 위치(startPosition)를 기준으로 허용되는 최대 이동 거리
    public float maxDistanceLeft = 3f;  // 왼쪽으로 최대 이동 거리 (X-)
    public float maxDistanceRight = 3f; // 오른쪽으로 최대 이동 거리 (X+)
    public float maxDistanceUp = 3f;    // 위쪽으로 최대 이동 거리 (Y+)
    public float maxDistanceDown = 3f;  // 아래쪽으로 최대 이동 거리 (Y-)

    [Header("Return Settings")]
    public float returnSpeed = 5f;

    [Header("Event Hooks")]
    public UnityEvent OnDragStartSuccess;
    public UnityEvent OnDragEndSuccess;

    private bool dragging = false;
    private bool isReturning = false;
    private Vector3 startPosition;    // 오브젝트의 초기 시작 위치

    private Camera cam;
    private Vector3 offset;

    void Awake()
    {
        cam = Camera.main;
        // referenceTransform이 할당되어 있으면 해당 Transform의 위치를 기준으로,
        // 그렇지 않으면 자신의 현재 위치를 기준으로 startPosition을 설정합니다.
        startPosition = (referenceTransform != null) ? referenceTransform.position : transform.position;
    }

    void OnEnable()
    {
        pointAction.action.Enable();
        clickAction.action.Enable();
        clickAction.action.performed += HandleClick;
    }

    void OnDisable()
    {
        pointAction.action.Disable();
        clickAction.action.Disable();
        clickAction.action.performed -= HandleClick;
    }

    private void HandleClick(InputAction.CallbackContext context)
    {
        // PassThrough 액션은 값이 변경될 때 'performed'를 호출합니다.
        // 마우스 버튼의 경우, 눌렀을 때 1, 뗐을 때 0을 전달합니다.
        if (context.ReadValue<float>() > 0.5f) // 버튼을 눌렀을 때
        {
            TryStartDrag();
        }
        else // 버튼을 뗐을 때
        {
            StartReturn();
        }
    }

    void TryStartDrag()
    {
        Vector2 mousePos = pointAction.action.ReadValue<Vector2>();
        float z = cam.WorldToScreenPoint(transform.position).z;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, z));

        // OverlapPoint를 사용해 현재 마우스 위치에 오브젝트가 있는지 확인
        Collider2D hitCollider = Physics2D.OverlapPoint(world);

        if (hitCollider != null && hitCollider.gameObject == this.gameObject)
        {
            dragging = true;
            isReturning = false;
            offset = transform.position - world;

            OnDragStartSuccess?.Invoke();
        }
    }

    void StartReturn()
    {
        dragging = false;

        OnDragEndSuccess?.Invoke();

        Vector3 returnTargetPosition = (referenceTransform != null) ? referenceTransform.position : startPosition;
        if (Vector3.Distance(transform.position, returnTargetPosition) > 0.01f)
        {
            isReturning = true;
        }
    }

    void Update()
    {
        if (dragging)
        {
            // 1. 마우스의 현재 좌표 값과 오프셋을 사용
            Vector2 mousePos = pointAction.action.ReadValue<Vector2>();
            float z = cam.WorldToScreenPoint(transform.position).z;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, z));

            // 드래그 중인 임시 목표 위치
            Vector3 targetWorldPosition = world + offset;

            // 2. 클램핑 경계 계산 (referenceTransform 또는 startPosition 기준)
            Vector3 basePosition = (referenceTransform != null) ? referenceTransform.position : startPosition;
            float minX = basePosition.x - maxDistanceLeft;  // 왼쪽 경계 (X축 최소)
            float maxX = basePosition.x + maxDistanceRight; // 오른쪽 경계 (X축 최대)
            float minY = basePosition.y - maxDistanceDown;  // 아래쪽 경계 (Y축 최소)
            float maxY = basePosition.y + maxDistanceUp;    // 위쪽 경계 (Y축 최대)

            // 3. X, Y좌표를 경계 내에서 클램핑
            targetWorldPosition.x = Mathf.Clamp(targetWorldPosition.x, minX, maxX);
            targetWorldPosition.y = Mathf.Clamp(targetWorldPosition.y, minY, maxY);

            // 4. 오브젝트 위치 업데이트
            transform.position = targetWorldPosition;
        }
        else if (isReturning)
        {
            // 원래 위치로 복귀
            Vector3 returnTargetPosition = (referenceTransform != null) ? referenceTransform.position : startPosition;
            transform.position = Vector3.Lerp(
                transform.position,
                returnTargetPosition,
                Time.deltaTime * returnSpeed
            );

            if (Vector3.Distance(transform.position, returnTargetPosition) < 0.01f)
            {
                transform.position = returnTargetPosition;
                isReturning = false;
            }
        }
    }
}
