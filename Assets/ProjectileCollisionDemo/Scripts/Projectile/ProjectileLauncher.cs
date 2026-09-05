using UnityEngine;

namespace ProjectileCollisionDemo
{
    // Runner의 발사 요청을 수행하며 결과를 판단하지 않는다.
    public sealed class ProjectileLauncher : MonoBehaviour
    {
        [SerializeField] private ProjectilePool pool;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private EndBoundary boundary;
        public bool IsValid => pool != null && spawnPoint != null && boundary != null && spawnPoint.position.x < boundary.transform.position.x;
        public void Configure(ProjectilePool owner, Transform spawn, EndBoundary end)
        { pool = owner; spawnPoint = spawn; boundary = end; }
        public void Fire(int shotId, float speed, float radius) =>
            pool.Get().Launch(pool, boundary, shotId, spawnPoint.position, speed, radius);
    }
}
