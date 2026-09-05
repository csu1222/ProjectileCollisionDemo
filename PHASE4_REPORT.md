# ProjectileCollisionDemo — Phase 4: SphereCastNonAlloc Selected

## 1. Final Verdict

**PASS** — Unity 6000.3.10f1에서 Phase 4 실제 GUI/배치 PlayMode 검증과 Phase 1–3 배치 회귀가 모두 통과했다. 네 배치 실행의 종료 코드는 모두 0이다. 대표 조건과 A–D의 NonAlloc 결과는 각각 100/100 Hit, Miss=0, Duplicate=0이며 Phase 3 기록과 일치한다. 별도 일반 Game View 대표 실행에서도 같은 결과를 확인했다.

## 2. Files Changed

기준 경로: `Assets/ProjectileCollisionDemo/`.

- 승인 후 수정: `Scripts/Projectile/TestProjectile.cs` — 인스턴스별 `RaycastHit[16]`, NonAlloc 판정 참조와 이동 전 호출.
- 승인 후 수정: `Scripts/Core/ProjectileTestRunner.cs` — 상호 배타적인 NonAlloc 전략과 기존 집계 연결.
- 승인 후 수정: `Scripts/UI/ProjectileCollisionDebugPanel.cs` — Phase 4 / SphereCastNonAlloc 표시.
- 신규: `Scripts/Projectile/SphereCastNonAllocDetector.cs` — Query, 기존 필터, 최근접 선택, 기본 포화 의심 계측.
- 신규: `Editor/PhaseFourBuilder.cs`, `Editor/PhaseFourValidation.cs`, `Editor/PhaseFourMovementProbe.cs` — Scene 생성 및 재현 가능한 검증.
- 신규: `Scenes/03_SphereCastNonAlloc.unity`, 신규 Asset에 대해 Unity가 생성한 대응 `.meta`.
- 신규: 이 보고서.

기존 `OnTriggerProjectile.prefab`을 그대로 사용한다. Scene은 Unity Editor API로 Phase 3 Scene을 복제했다. 읽기 전용 Scene diff에서 판정 컴포넌트 교체와 Runner 전략 플래그 외의 차이는 없다. Hierarchy, 위치, Config, UI, Prefab 참조는 동일하다. 기존 Directional Light와 EventSystem도 유지했다. Scene/Prefab YAML 또는 GUID를 직접 작성하지 않았다.

## 3. SphereCastNonAlloc Architecture

`TestProjectile.FixedUpdate` → `movementDistance = Speed * Time.fixedDeltaTime` → `SphereCastNonAllocDetector.TryHit` → `Physics.SphereCastNonAlloc` → `0 <= i < hitCount` → 기존 필터 → 최소 distance 한 개 → `Runner.ReportHit` → Shot Resolve → Pool Return.

Hit 시 현재 위치에서 Resolve하고 해당 Tick에는 이동하지 않는다. Hit Point 이동을 추가하지 않았다. No-Hit이면 `nextPosition = transform.position + Direction * movementDistance`, `transform.position = nextPosition`을 실행하고 기존 `EndBoundary.TryReach`로 Miss를 확정한다.

NonAlloc Scene의 `UsesOnTrigger=false`, `UsesSphereCastAll=false`, `UsesSphereCastNonAlloc=true`다. 기존 Trigger callback은 Hit 보고를 하지 않는다. Query를 비활성화한 별도 A 조건 실행에서도 Hit가 발생하지 않았다.

Query와 후보 선택은 판정 컴포넌트, 버퍼는 각 Projectile, 결과와 pending Shot ID는 Runner, 대여/회수는 기존 Pool이 소유한다. 추가 Interface/Base Class/Manager는 없다.

## 4. Buffer Configuration

- Type: `RaycastHit[]`.
- Capacity: 16.
- Allocation: `TestProjectile`의 readonly 필드 초기화 시 인스턴스별 한 번.
- Reuse: Query에 같은 배열을 전달하며 Launch, Resolve, Pool Return, Reset에서 교체하지 않는다.
- Query/FixedUpdate에서 새 Hit 배열 생성, LINQ 복사·정렬, 자동 확장, fallback은 없다.
- 실제 검증: Pool의 32개 Projectile이 서로 다른 길이 16 배열을 소유하며 각 완료 시 최초 배열 참조와 동일함을 검사했다.

`BufferCapacity`, `LastHitCount`, `SaturationSuspectedCount`는 판정 컴포넌트에서 읽을 수 있다. LastHitCount는 가장 최근 Query 결과 수이며 미실행 Tick의 수치를 뜻하지 않는다. 포화 의심 횟수는 컴포넌트 lifetime 누계이고 각 조건은 실행 전후 차이로 기록한다. Stop/Reset이 이 진단 누계를 초기화하지는 않는다. UI에는 기존 비교 항목만 유지했다.

## 5. Query Configuration

- Origin: 현재 FixedUpdate 이동 시작 위치 `projectile.transform.position`.
- Direction: Launch에서 설정된 정규화된 `Vector3.right` (+X).
- Distance: `Speed * Time.fixedDeltaTime`.
- Radius: Launch에 전달한 Config 값. 대표값 0.05.
- LayerMask: `1 << target.gameObject.layer` = 1 (Default Layer 0).
- QueryTriggerInteraction: `Ignore`.
- Spawn=(0,1,0), Target=(10,1,0), EndBoundary=(12,1,0).
- Fixed Delta Time=0.02 s, Shot Interval=0.1 s, Shot Count=100, Pool prewarm=32.

자기 Transform/자식 Collider, Mask 외 Collider, TestTarget 없는 Collider, Runner 예상 Target과 다른 TestTarget을 제외한다. 반환 순서를 정렬 순서로 가정하지 않으며 반환된 유효 결과 중 최소 `hit.distance`만 선택한다. 같은 최소 거리의 동률은 Phase 3처럼 먼저 만난 유효 후보를 유지한다.

## 6. Representative Comparison

공통 Radius=0.05, Thickness=0.20, Fixed DT=0.02, Speed=40, Shots=100.

| Strategy | Speed | Travel/Tick | Radius | Thickness | Shots | Detected | Missed | Hit Rate |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| OnTrigger | 40 | 0.80 | 0.05 | 0.20 | 100 | 0 | 100 | 0.00% |
| SphereCastAll | 40 | 0.80 | 0.05 | 0.20 | 100 | 100 | 0 | 100.00% |
| SphereCastNonAlloc | 40 | 0.80 | 0.05 | 0.20 | 100 | 100 | 0 | 100.00% |

OnTrigger/SphereCastAll 기준은 `PHASE2_REPORT.md`, `PHASE3_REPORT.md`의 실제 기록이다. 이번 배치 회귀에서도 OnTrigger/SphereCastAll의 기록을 재확인했다. NonAlloc 첫 실행은 `Logs/PhaseFourInitialValidation.txt`의 `B representative`다. 대표값을 조정하지 않았다. NonAlloc Completed=100, Duplicate=0, LastHitCount=1, Buffer Saturation Suspected=0.

관찰 도구는 대표 실행 중 No-Hit 이동 1,200회와 Cast-first Hit 100회를 실제 FixedUpdate 경계에서 검사했다. 관찰 도구 없는 일반 Game View에서도 100/100 Hit를 별도로 확인했다.

## 7. A–D Comparison

Phase 2/3 보고서에서 읽은 조건을 재사용했다. 모든 조건은 Radius=0.05, dt=0.02, Interval=0.1, Shots=100이다. 아래 비교 항목은 Hit Rate다.

| Case | Speed | Thickness | OnTrigger | SphereCastAll | SphereCastNonAlloc |
|---|---:|---:|---:|---:|---:|
| A | 15 | 1.00 | 100.00% | 100.00% | 100.00% |
| B | 40 | 0.20 | 0.00% | 100.00% | 100.00% |
| C | 100 | 0.20 | 100.00% | 100.00% | 100.00% |
| D | 200 | 0.05 | 0.00% | 100.00% | 100.00% |

NonAlloc A–D 각각 Detected=100, Missed=0, Duplicate=0, Buffer Saturation Suspected=0. Travel/Tick은 순서대로 0.30, 0.80, 2.00, 4.00이다. 상세 실제 수치는 검증 로그에 기록한다.

## 8. Multi-Hit / Duplicate Validation

Phase 3과 같은 별도 진단 배치를 PlayMode에서만 사용했다. Origin=(8,1,0), Radius=0.05, Distance=3, 자기 Collider를 일시적으로 non-trigger로 설정하고 더 가까운 잘못된 TestTarget과 예상 Target의 가까운 자식 Collider를 배치했다.

- SphereCastAll raw=4, NonAlloc returned=4.
- 두 구현의 실제 선택 Collider가 동일: `Phase4 nearest valid child`.
- 선택 distance=1.4000. 반환 후보의 유효 최소 거리와 일치.
- 자기 Collider와 잘못된 Target은 선택하지 않았다.
- 이어 같은 Projectile을 (20,1,0)으로 옮겨 빈 Query를 실행: hitCount=0, 선택 없음. 이전 버퍼 데이터가 Hit로 처리되지 않았다.
- 포화 의심 누계=0.
- 같은 Target에 Collider를 추가한 별도 대표 100발: Detected=100, Missed=0, Duplicate=0, LastHitCount=2.

Runner는 단일 예상 Target을 집계한다. 여기서 Near/Far는 같은 예상 TestTarget의 Collider 후보이며 여러 독립 Target을 허용하는 정책으로 확장하지 않았다. 진단 오브젝트/설정은 저장된 Scene에 포함하지 않았다.

## 9. Result Integrity

각 완료 실행은 Fired=Completed=Results.Count=100, `Detected + Missed = 100`, Duplicate=0이다. Shot ID는 1–100 고유값, 결과는 IsResolved=true, Hit/Miss 배타성, HitCount=Hit이면 1/Miss이면 0을 검사했다. 결과 합계와 Runner 통계가 일치한다.

Target Collider 비활성 시 0 Hit / 100 Miss로 EndBoundary 경로를 확인했다. Query 비활성 A 조건도 0 Hit / 100 Miss로 Trigger 분리를 확인했다. 복원 후 대표 실행은 100 Hit / 0 Miss다.

Hit/Miss 모두 Pool 반환 후 CreatedCount=32, ActiveCount=0이다. 런타임 Pool 코드는 추가 Instantiate/Destroy 변경이 없고 배열 참조도 유지된다. Stop은 진행 중 Shot을 회수하며 취소 Shot을 Miss로 바꾸지 않는다. Reset은 결과를 지우고 Shot ID를 1부터 재시작한다.

## 10. Runtime / UI Validation

- Unity 6000.3.10f1 실제 컴파일 및 첫 PlayMode 검증 통과. 로그: `Logs/PhaseFourInitialValidation.txt`.
- 검증 중 Error/Exception/Assert 수신 시 실패하도록 검사했으며 첫 실행에서 통과했다.
- 실제 Game View에서 Phase 4 / SphereCastNonAlloc, 설정과 결과 항목이 겹치거나 잘리지 않고 표시된다.
- 실제 포인터 Start → Running과 Hit 증가. Stop → Stopped, Shots=77, Completed=74, Detected=74, Missed=0 및 Projectile 회수 확인.
- Reset → Idle, 결과 0 확인.
- Speed -/+ → 40→15→40, Travel/Tick 0.800→0.300→0.800 확인.
- Thickness +/- → 0.200→1.000→0.200, 실제 Target 두께 변경 확인.
- 7개 버튼 모두 포인터로 조작했다. 관찰 도구 없는 대표 재실행은 Complete, Shots=Completed=100, Detected=100, Missed=0, Hit Rate=100.00%, Duplicate=0이었다.
- 화면의 Console Warning/Error는 모두 0이었다. 완료 후 Reset하고 PlayMode를 종료했다.
- GUI 실행은 이전부터 열려 있던 Unity를 사용했으므로 해당 세션의 원본 로그 이름은 `Logs/PhaseThreeVisual.log`다.

최종 배치 검증 결과:

- Phase 1: `Logs/PhaseFourRegressionOne.log`, `PHASE1_PLAYMODE_PASS`, 종료 코드 0. 기존 이동, Pool, Stop/Reset 확인.
- Phase 2: `Logs/PhaseFourRegressionTwo.log`, `PHASE2_PLAYMODE_PASS`, 종료 코드 0. A–D 결과 및 합성 집계 검사 유지.
- Phase 3: `Logs/PhaseFourRegressionThree.log`, `PHASE3_PLAYMODE_PASS`, 종료 코드 0. 대표/A–D, 최근접, Miss, Trigger 분리, Stop/Reset 유지.
- Phase 4: `Logs/PhaseFourRegressionFour.log`, `Logs/PhaseFourValidation.txt`, `PHASE4_PLAYMODE_PASS`, 종료 코드 0. 첫 측정 결과 재현, 모든 Hit의 예상 Target ID 일치(Wrong Target=0), 최종 Reset 재실행 100 Hit / 0 Miss / Duplicate=0, 32개 독립 버퍼 참조 유지, lifetime Buffer Saturation Suspected=0.
- 배치 시작 시 라이선스 access-token 메시지는 있었지만 실제 PlayMode 성공과 구분한다. 컴파일 error CS 및 PlayMode Exception은 확인되지 않았다.

## 11. Limits

- `hitCount == 16`이면 실제 Collider가 정확히 16개인지, 더 많지만 잘렸는지 구분할 수 없다. 따라서 **Buffer Saturation Suspected**로만 표현한다.
- 버퍼가 포화되면 실제 전체 후보 중 최근접 Hit가 포함된다고 보장할 수 없다. 보장 범위는 **버퍼에 반환된 유효 결과 중 최근접 Hit 선택**이다.
- 이번 조건에서 포화 의심이 없었다는 사실은 버퍼 16이 모든 환경에 충분하다는 뜻이 아니다. 포화 Boundary Test는 Phase 6에 남긴다.
- 버퍼 자동 확장/재Query/fallback은 구현하지 않았다.
- GC/CPU/Profiler 측정은 하지 않았다. Zero Allocation, 더 빠름, CPU 비용 감소 등 성능 결론을 내리지 않는다.
- 공통 TestProjectile 필드이므로 Phase 1–3 인스턴스에도 버퍼가 생성된다. 동작 회귀와 별개로 Phase 6 메모리/초기화 비교에서는 이 구조를 고려해야 한다.
- 이동 관찰 도구는 추가 진단 Query와 Reflection을 사용한다. 포화 누계에는 이 진단 Query도 포함될 수 있다. 일반 Scene 실행에는 관찰 도구가 없으며 이 검증은 성능 측정이 아니다.
- 기존 실행 중 Inspector Config 변경 및 완료 후 설정 변경 시 결과 표시의 제한은 그대로다. 각 측정 실행 중 조건을 고정했다.
- 기존 폴더 이동과 `.meta` 삭제/추가 상태는 보존했다. 실행 전후 Asset 해시 검사에서 승인된 기존 Script 3개 외의 기존 파일 변경은 없었다. Unity가 자동 생성한 `ProjectSettings/SceneTemplateSettings.json`은 `Logs/PhaseFourGeneratedSceneTemplateSettings.json`으로 보관하여 ProjectSettings 변경을 남기지 않았다. Commit/Push는 수행하지 않았다.

## 12. Phase 5 Readiness

통제 조건의 Reliability와 최근접 선택 비교는 확보했다. Phase 1–4 최종 회귀까지 통과하여 자동 Reliability Matrix 단계로 진행할 수 있다. Phase 5 Matrix 자체는 이번 변경에 포함하지 않으며 GC/CPU 및 포화 Boundary Test는 Phase 6에서 별도로 판단한다.
