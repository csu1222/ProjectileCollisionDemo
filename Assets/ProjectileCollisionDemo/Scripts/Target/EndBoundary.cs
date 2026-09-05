using System;
using UnityEngine;

namespace ProjectileCollisionDemo
{
    // +X 이동의 종료 평면이다. 물리 충돌 없이 큰 이동량도 처리한다.
    public sealed class EndBoundary : MonoBehaviour
    {
        public event Action<int> Reached;
        public bool TryReach(int shotId, Vector3 nextPosition)
        {
            if (nextPosition.x < transform.position.x) return false;
            Reached?.Invoke(shotId);
            return true;
        }
    }
}
