using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SRPG
{
    public class GameManager : MonoBehaviour
    {
        public GridManager grid;
        public UIManager ui;
        public CameraController cameraController;
        public List<Unit> allUnits = new List<Unit>();
        public TurnPhase phase { get; private set; } = TurnPhase.Player;
        public int turnNumber { get; private set; } = 1;

        public event Action<TurnPhase, int> OnPhaseChanged;
        public event Action<TurnPhase> OnGameOver;

        public IEnumerable<Unit> PlayerUnits => allUnits.Where(u => u.team == Team.Player);
        public IEnumerable<Unit> EnemyUnits => allUnits.Where(u => u.team == Team.Enemy);

        public void RegisterUnit(Unit u) => allUnits.Add(u);

        public void StartPlayerPhase()
        {
            phase = TurnPhase.Player;
            foreach (var u in PlayerUnits.Where(u => u.IsAlive))
            {
                u.hasActed = false;
                u.isBroken = false;
                u.RefreshActedVisual();
            }

            // 방금 끝난 적 턴에 행동해서 어두워진 적 유닛들을 내 턴 동안은 원래 색으로 되돌림
            foreach (var u in EnemyUnits.Where(u => u.IsAlive))
            {
                u.hasActed = false;
                u.RefreshActedVisual();
            }
            OnPhaseChanged?.Invoke(phase, turnNumber);
        }

        public void RequestEndPlayerPhase()
        {
            if (phase != TurnPhase.Player) return;
            StartCoroutine(EnemyPhaseRoutine());
        }

        private IEnumerator EnemyPhaseRoutine()
        {
            phase = TurnPhase.Enemy;
            OnPhaseChanged?.Invoke(phase, turnNumber);

            // 방금 끝난 내 턴에 행동해서 어두워진 아군 유닛들을 적 턴 동안은 원래 색으로 되돌림
            foreach (var u in PlayerUnits.Where(u => u.IsAlive))
            {
                u.hasActed = false;
                u.RefreshActedVisual();
            }

            foreach (var u in EnemyUnits.Where(u => u.IsAlive))
            {
                u.hasActed = false;
                u.isBroken = false;
            }

            // 적 턴 시작 임팩트 연출이 끝날 때까지 기다렸다가 행동을 시작함
            yield return new WaitUntil(() => !ui.IsPlayingPhaseImpact);

            foreach (var enemy in EnemyUnits.Where(u => u.IsAlive).ToList())
            {
                if (!enemy.IsAlive || enemy.hasActed) continue;
                yield return StartCoroutine(EnemyAI.TakeTurn(enemy, this, grid));
                yield return new WaitForSeconds(0.25f);

                if (CheckGameOver()) yield break;
            }

            turnNumber++;
            StartPlayerPhase();
        }

        public bool CheckGameOver()
        {
            if (!PlayerUnits.Any(u => u.IsAlive))
            {
                phase = TurnPhase.Defeat;
                OnGameOver?.Invoke(phase);
                return true;
            }
            if (!EnemyUnits.Any(u => u.IsAlive))
            {
                phase = TurnPhase.Victory;
                OnGameOver?.Invoke(phase);
                return true;
            }
            return false;
        }

        public void RemoveDeadFromGrid()
        {
            foreach (var u in allUnits.Where(u => !u.IsAlive))
                grid.RemoveOccupant(u);
        }
    }
}
