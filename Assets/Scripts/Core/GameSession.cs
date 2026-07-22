using System.Collections.Generic;

namespace SRPG
{
    // 씬을 다시 로드해서 스테이지를 새로 시작할 때, 다음에 어느 스테이지로 들어갈지 씬 전환 너머로 전달하는 정적 상태.
    // null이면 홈 화면부터 시작(기존 동작), 값이 있으면 그 스테이지로 바로 진입(재도전/다음 스테이지/스테이지 선택)
    public static class GameSession
    {
        public const int StageCount = 6;
        public static int? PendingStageIndex;

        // 한 번이라도 클리어(승리)한 스테이지 번호 모음. 씬 리로드에도 유지되도록 정적 필드로 둠(앱을 완전히 재시작하면 초기화됨)
        public static readonly HashSet<int> ClearedStages = new HashSet<int>();
    }
}
