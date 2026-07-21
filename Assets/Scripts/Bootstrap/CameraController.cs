using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace SRPG
{
    public class CameraController : MonoBehaviour
    {
        public Camera cam;
        public GridManager grid;
        public GridPosition? startFocus;

        public event Action<Vector2> OnTap;
        public event Action<Vector2, Vector2> OnUnitDragRelease;

        // 이 위치에서 누르기를 시작하면 카메라 패닝 대신 유닛 드래그로 취급함 (PlayerController가 설정)
        public Func<Vector2, bool> IsUnitDragStart;

        private const float DragThresholdPixels = 12f;
        private const float TargetAspect = 2f / 1f;
        // 가로 칸 수 = TargetAspect(2) x ViewHeightTiles. 가로 14칸이 되도록 7로 맞춤(세로는 그 절반인 7칸)
        private const float ViewHeightTiles = 7f;
        private const float FocusLerpSpeed = 6f;
        // 전투 시작 시 살짝 당겨보는 줌인 비율(오쏘그래픽 사이즈를 줄이면 확대됨). 0.85 = 15% 확대
        private const float CombatZoomFactor = 0.85f;
        // VS 화면 전환 직전 한 번 더 확 당기는 비율/시간(짧고 빠르게 "펀치인"하는 느낌)
        private const float PunchZoomFactor = 0.6f;
        private const float PunchZoomDuration = 0.15f;
        // 마우스 휠/트랙패드로 자유 줌: 휠 델타에 곱하는 민감도와 오쏘그래픽 사이즈 허용 범위
        private const float ScrollZoomSensitivity = 0.15f;
        // 두 손가락 핀치 줌: 손가락 사이 거리(픽셀) 변화량에 곱하는 민감도
        private const float PinchZoomSensitivity = 0.01f;
        private const float ZoomLerpSpeed = 120f;
        private const float MinOrthographicSize = 2f;
        private const float MaxOrthographicSize = 6f;

        private bool pointerDown;
        private bool dragging;
        private bool unitDragActive;
        private Vector2 pressScreenPos;
        private Vector3 pressCamWorldPos;
        private Vector3? focusTarget;
        private float baseOrthographicSize;
        private float? zoomTarget;
        private float? pinchPrevDistance;

        private void Start()
        {
            ApplyLetterbox();
            baseOrthographicSize = ViewHeightTiles / 2f;
            cam.orthographicSize = baseOrthographicSize;
            if (startFocus.HasValue) FocusOnImmediate(startFocus.Value);
            EnhancedTouchSupport.Enable();
        }

        private void OnDestroy()
        {
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            // 터치 입력이 있는 프레임엔 터치로만 처리(마우스와 동시에 처리하면 패닝이 서로 충돌함)
            if (!HandleTouch())
            {
                HandlePointer();
                HandleScrollZoom();
            }
            HandleFocusLerp();
            HandleZoomLerp();
        }

        // 폰 터치 입력: 손가락 1개는 마우스와 동일하게 패닝/탭/유닛드래그로, 2개는 핀치 줌으로 처리.
        // 이번 프레임에 터치가 하나라도 있었으면 true를 돌려줘서 Update()가 마우스 처리를 건너뛰게 함
        private bool HandleTouch()
        {
            var touches = Touch.activeTouches;
            if (touches.Count == 0)
            {
                pinchPrevDistance = null;
                return false;
            }

            if (touches.Count >= 2)
            {
                // 핀치 중엔 한 손가락 패닝/드래그가 같이 발동하면 안 되므로 눌림 상태를 취소
                if (pointerDown) { pointerDown = false; dragging = false; unitDragActive = false; }
                HandlePinchZoom(touches[0], touches[1]);
                return true;
            }

            pinchPrevDistance = null;
            HandleSingleTouch(touches[0]);
            return true;
        }

        private void HandleSingleTouch(Touch t)
        {
            switch (t.phase)
            {
                case TouchPhase.Began:
                    // UI 버튼 위에서 시작된 터치는 맵 탭/드래그로 취급하지 않음
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.touchId))
                        return;

                    pointerDown = true;
                    dragging = false;
                    pressScreenPos = t.position;
                    pressCamWorldPos = cam.transform.position;
                    focusTarget = null;
                    unitDragActive = IsUnitDragStart != null && IsUnitDragStart(pressScreenPos);
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (!pointerDown) return;
                    // 유닛을 드래그하는 중에는 카메라가 같이 움직이면 안 되므로 패닝을 막음
                    if (unitDragActive) return;

                    var cur = t.position;
                    var delta = cur - pressScreenPos;
                    if (!dragging && delta.magnitude > DragThresholdPixels)
                        dragging = true;

                    if (dragging)
                    {
                        float worldPerPixel = cam.orthographicSize * 2f / cam.pixelHeight;
                        var worldDelta = new Vector3(-delta.x, -delta.y, 0f) * worldPerPixel;
                        cam.transform.position = ClampToMap(pressCamWorldPos + worldDelta);
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (!pointerDown) return;
                    if (unitDragActive) OnUnitDragRelease?.Invoke(pressScreenPos, t.position);
                    else if (!dragging) OnTap?.Invoke(pressScreenPos);
                    pointerDown = false;
                    dragging = false;
                    unitDragActive = false;
                    break;
            }
        }

        // 손가락 두 개 사이 거리 변화를 오쏘그래픽 사이즈 변화로 바꿔줌(벌리면 확대, 좁히면 축소). 마우스 휠처럼 Lerp 없이 즉시 반영
        private void HandlePinchZoom(Touch a, Touch b)
        {
            float dist = Vector2.Distance(a.position, b.position);
            if (pinchPrevDistance.HasValue)
            {
                float delta = dist - pinchPrevDistance.Value;
                baseOrthographicSize = Mathf.Clamp(
                    baseOrthographicSize - delta * PinchZoomSensitivity,
                    MinOrthographicSize, MaxOrthographicSize);
                zoomTarget = null;
                cam.orthographicSize = baseOrthographicSize;
                cam.transform.position = ClampToMap(cam.transform.position);
            }
            pinchPrevDistance = dist;
        }

        // 마우스 휠/트랙패드로 확대·축소. baseOrthographicSize 자체를 바꿔서 전투 줌/리셋 기준점도 함께 갱신됨
        private void HandleScrollZoom()
        {
            if (Mouse.current == null) return;
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            baseOrthographicSize = Mathf.Clamp(
                baseOrthographicSize - scroll * ScrollZoomSensitivity,
                MinOrthographicSize, MaxOrthographicSize);
            // 휠 줌은 Lerp로 서서히 따라가지 않고 즉시 반영(전투 줌/펀치 줌은 기존처럼 zoomTarget Lerp를 그대로 씀)
            zoomTarget = null;
            cam.orthographicSize = baseOrthographicSize;
            cam.transform.position = ClampToMap(cam.transform.position);
        }

        private void HandlePointer()
        {
            if (Mouse.current == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // UI 버튼 위에서 시작된 클릭은 맵 탭/드래그로 취급하지 않음
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                pointerDown = true;
                dragging = false;
                pressScreenPos = Mouse.current.position.ReadValue();
                pressCamWorldPos = cam.transform.position;
                focusTarget = null;
                unitDragActive = IsUnitDragStart != null && IsUnitDragStart(pressScreenPos);
            }
            else if (pointerDown && Mouse.current.leftButton.isPressed)
            {
                // 유닛을 드래그하는 중에는 카메라가 같이 움직이면 안 되므로 패닝을 막음
                if (unitDragActive) return;

                var cur = Mouse.current.position.ReadValue();
                var delta = cur - pressScreenPos;
                if (!dragging && delta.magnitude > DragThresholdPixels)
                    dragging = true;

                if (dragging)
                {
                    float worldPerPixel = cam.orthographicSize * 2f / cam.pixelHeight;
                    var worldDelta = new Vector3(-delta.x, -delta.y, 0f) * worldPerPixel;
                    cam.transform.position = ClampToMap(pressCamWorldPos + worldDelta);
                }
            }
            else if (pointerDown && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (unitDragActive) OnUnitDragRelease?.Invoke(pressScreenPos, Mouse.current.position.ReadValue());
                else if (!dragging) OnTap?.Invoke(pressScreenPos);
                pointerDown = false;
                dragging = false;
                unitDragActive = false;
            }
        }

        public void FocusOn(GridPosition gp)
        {
            var world = grid.GridToWorld(gp);
            focusTarget = new Vector3(world.x, world.y, cam.transform.position.z);
        }

        // 그리드 칸 단위가 아니라 임의의 월드 좌표(예: 전투 중인 두 유닛의 중간 지점)로 부드럽게 이동할 때 씀
        public void FocusOnWorldPoint(Vector3 worldPos)
        {
            focusTarget = new Vector3(worldPos.x, worldPos.y, cam.transform.position.z);
        }

        // 포커스 이동(Lerp)이 아직 끝나지 않았는지(목표에 도달해 focusTarget이 null로 비워졌는지) 확인용
        public bool IsFocusing => focusTarget != null;

        public void FocusOnImmediate(GridPosition gp)
        {
            var world = grid.GridToWorld(gp);
            cam.transform.position = ClampToMap(new Vector3(world.x, world.y, cam.transform.position.z));
            focusTarget = null;
        }

        private void HandleFocusLerp()
        {
            if (focusTarget == null) return;
            var target = ClampToMap(focusTarget.Value);
            cam.transform.position = Vector3.Lerp(cam.transform.position, target, Time.deltaTime * FocusLerpSpeed);
            if (Vector3.Distance(cam.transform.position, target) < 0.02f)
            {
                cam.transform.position = target;
                focusTarget = null;
            }
        }

        // 전투 시작 시 살짝 확대했다가(ZoomInForCombat), 끝나면 원래 배율로 되돌림(ResetZoom)
        public void ZoomInForCombat() => zoomTarget = baseOrthographicSize * CombatZoomFactor;
        public void ResetZoom() => zoomTarget = baseOrthographicSize;

        // VS 화면으로 전환하기 직전, 짧고 빠르게 한 번 더 확 당겨서 화면 전환의 임팩트를 줌(Lerp 목표가 아니라 즉시 진행하는 별도 애니메이션)
        public IEnumerator PunchZoomIn()
        {
            float start = cam.orthographicSize;
            float target = start * PunchZoomFactor;
            float t = 0f;
            while (t < PunchZoomDuration)
            {
                t += Time.deltaTime;
                cam.orthographicSize = Mathf.Lerp(start, target, Mathf.Clamp01(t / PunchZoomDuration));
                yield return null;
            }
            cam.orthographicSize = target;
        }

        // 줌 이동(Lerp)이 아직 끝나지 않았는지(목표 배율에 도달해 zoomTarget이 null로 비워졌는지) 확인용
        public bool IsZooming => zoomTarget != null;

        private void HandleZoomLerp()
        {
            if (zoomTarget == null) return;
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, zoomTarget.Value, Time.deltaTime * ZoomLerpSpeed);
            if (Mathf.Abs(cam.orthographicSize - zoomTarget.Value) < 0.01f)
            {
                cam.orthographicSize = zoomTarget.Value;
                zoomTarget = null;
            }
            // 줌으로 오쏘그래픽 사이즈가 바뀌면 맵 가장자리 밖이 보일 수 있으므로 위치를 다시 클램프
            cam.transform.position = ClampToMap(cam.transform.position);
        }

        private Vector3 ClampToMap(Vector3 pos)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            float x = grid.width > halfW * 2f ? Mathf.Clamp(pos.x, halfW, grid.width - halfW) : grid.width / 2f;
            float y = grid.height > halfH * 2f ? Mathf.Clamp(pos.y, halfH, grid.height - halfH) : grid.height / 2f;
            return new Vector3(x, y, pos.z);
        }

        private void ApplyLetterbox()
        {
            float windowAspect = (float)Screen.width / Screen.height;
            float scaleHeight = windowAspect / TargetAspect;
            cam.rect = scaleHeight < 1f
                ? new Rect(0f, (1f - scaleHeight) / 2f, 1f, scaleHeight)
                : new Rect((1f - 1f / scaleHeight) / 2f, 0f, 1f / scaleHeight, 1f);
        }
    }
}
