using System.Collections.Generic;
using UnityEngine;

namespace ProjectileCollisionDemo
{
    // 비활성 Queue와 대여 집합을 소유하여 중복 반환을 막는다.
    public sealed class ProjectilePool : MonoBehaviour
    {
        [SerializeField] private TestProjectile prefab;
        private readonly Queue<TestProjectile> available = new Queue<TestProjectile>();
        private readonly HashSet<TestProjectile> active = new HashSet<TestProjectile>();
        public int ActiveCount => active.Count;
        public int CreatedCount { get; private set; }
        public void Configure(TestProjectile template) => prefab = template;
        public void Prewarm(int count)
        {
            while (CreatedCount < count) available.Enqueue(Create());
        }
        private TestProjectile Create()
        {
            TestProjectile projectile = Instantiate(prefab, transform);
            projectile.gameObject.SetActive(false);
            CreatedCount++;
            return projectile;
        }
        public TestProjectile Get()
        {
            TestProjectile projectile = available.Count > 0 ? available.Dequeue() : Create();
            active.Add(projectile);
            return projectile;
        }
        public void Return(TestProjectile projectile)
        {
            if (!active.Remove(projectile)) return;
            projectile.gameObject.SetActive(false);
            available.Enqueue(projectile);
        }
        public void ReturnAll()
        {
            foreach (TestProjectile projectile in active)
            {
                projectile.gameObject.SetActive(false);
                available.Enqueue(projectile);
            }
            active.Clear();
        }
    }
}
