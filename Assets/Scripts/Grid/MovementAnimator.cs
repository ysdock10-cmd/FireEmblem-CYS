using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SRPG
{
    // 아군/적군이 같은 방식으로 이동 애니메이션을 재생하도록 공용으로 씀
    public static class MovementAnimator
    {
        private const float StepDuration = 0.05f; // 칸 하나를 이동하는 데 걸리는 시간

        // path[0]은 시작 칸(현재 위치)이므로 건너뛰고, 그 다음 칸부터 순서대로 한 칸씩 가로/세로로 이동
        public static IEnumerator AnimateAlongPath(Unit unit, List<GridPosition> path, GridManager grid)
        {
            // 칸 경계에서 t를 0으로 리셋하면 이전 칸에서 넘친 시간(overshoot)이 버려져 프레임이 조금만 밀려도
            // 매 칸마다 속도가 들쭉날쭉해 보임(끊기는 원인) -> 넘친 시간을 다음 칸으로 이월해 프레임에 덜 민감하게 함
            float carryOver = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 start = unit.transform.position;
                Vector3 end = grid.GridToWorld(path[i]);
                float t = carryOver;
                while (t < StepDuration)
                {
                    unit.transform.position = Vector3.Lerp(start, end, t / StepDuration);
                    yield return null;
                    t += Time.deltaTime;
                }
                unit.transform.position = end;
                carryOver = Mathf.Min(t - StepDuration, StepDuration);
            }
        }
    }
}
