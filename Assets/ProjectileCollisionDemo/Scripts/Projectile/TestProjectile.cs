using UnityEngine;

namespace ProjectileCollisionDemo
{
    // 이동은 Transform으로 유지하며 Shot 종료 시 Pool에 한 번만 반환한다.
    public sealed class TestProjectile : MonoBehaviour
    {
        public int ShotId { get; private set; }
        public float Speed { get; private set; }
        public Vector3 Direction { get; private set; }
        public bool IsResolved { get; private set; }
        private ProjectilePool pool;
        private EndBoundary boundary;
        private SphereCastAllDetector detector;
        private SphereCastNonAllocDetector nonAllocDetector;
        internal readonly RaycastHit[] HitBuffer = new RaycastHit[16];
        private float sweepRadius;
        public void Launch(ProjectilePool owner, EndBoundary end, int id, Vector3 position, float speed, float radius)
        {
            pool = owner;
            boundary = end;
            detector = owner.GetComponent<SphereCastAllDetector>();
            nonAllocDetector = owner.GetComponent<SphereCastNonAllocDetector>();
            sweepRadius = radius;
            ShotId = id;
            IsResolved = false;
            Speed = speed;
            Direction = Vector3.right;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            transform.localScale = Vector3.one * (radius * 2f);
            gameObject.SetActive(true);
        }
        private void FixedUpdate()
        {
            if (IsResolved) return;
            float movementDistance = Speed * Time.fixedDeltaTime;
            if (detector != null && detector.TryHit(this, sweepRadius, movementDistance))
            { Resolve(); return; }
            if (nonAllocDetector != null && nonAllocDetector.TryHit(this, sweepRadius, movementDistance))
            { Resolve(); return; }
            Vector3 nextPosition = transform.position + Direction * movementDistance;
            MoveTo(nextPosition);
            if (boundary.TryReach(ShotId, nextPosition)) Resolve();
        }
        public void Resolve()
        {
            if (IsResolved) return;
            IsResolved = true;
            pool.Return(this);
        }
        private void MoveTo(Vector3 nextPosition) => transform.position = nextPosition;
    }
}
