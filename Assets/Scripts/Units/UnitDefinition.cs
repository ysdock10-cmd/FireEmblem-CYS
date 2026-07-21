using UnityEngine;

namespace SRPG
{
    [System.Serializable]
    public class UnitDefinition
    {
        public string unitName;
        public Team team;
        public Stats baseStats;
        public WeaponData weapon;
        public GridPosition startPosition;
        public Color teamColorOverride;
        public string portraitFile; // 맵에 들어가는 캐릭터 SD(미니 캐릭터)
        public string illustrationFile; // 정보창에 보이는 캐릭터 일러스트(캐릭터 선택 시 정보칸 왼쪽에 크게 보여줌)
        // 아래 둘은 비워두면(null) UIManager의 공통 위치/배율을 그대로 씀. 특정 캐릭터의 그림만 위치/크기를 다르게 하고 싶을 때만 채움
        public Vector2? illustrationOffset;
        public float? illustrationZoom;
    }
}
