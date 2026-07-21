using UnityEngine;
using UnityEngine.SceneManagement;

namespace SRPG
{
    public static class GameBootstrap
    {
        // RuntimeInitializeOnLoadMethod는 앱 시작 시 딱 한 번만 호출되고, 이후 SceneManager.LoadScene으로
        // 씬을 다시 로드해도 재호출되지 않는다(재도전/다음 스테이지/홈으로가 씬 리로드로 처리되므로 이 부트스트랩도
        // 매번 다시 돌아야 함). 그래서 최초 1회는 바로 만들고, 이후의 모든 씬 로드에 대해서도 다시 만들도록 구독해 둔다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            CreateRoot();
            SceneManager.sceneLoaded += (_, __) => CreateRoot();
        }

        private static void CreateRoot()
        {
            var root = new GameObject("~SRPG_Root");
            root.AddComponent<GameRoot>();
        }
    }
}
