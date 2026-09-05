# ProjectileCollisionDemo — Phase 3: SphereCastAll Alternative

## 1. Final Verdict

**PASS** — Unity 6000.3.10f1 실제 배치 PlayMode에서 대표 조건과 A–D, 다중 Collider, Miss, Trigger 분리, 결과/Pool/버튼 연결 검증을 통과했다. Phase 1·2 회귀와 실제 Game View 육안 확인, 7개 버튼의 포인터 조작도 완료했다. 대표 동일 조건에서 OnTrigger는 0/100 Hit, SphereCastAll은 100/100 Hit였다.

## 2. Files Changed

기준 경로: `Assets/ProjectileCollisionDemo/`.

- 수정: `Scripts/Projectile/TestProjectile.cs` — 이동 전 선택적 SphereCastAll 호출과 Launch 당시 Sweep Radius 보관.
- 수정: `Scripts/Core/ProjectileTestRunner.cs` — 상호 배타적 전략 설정, 공통 Hit/Miss 집계 허용.
- 수정: `Scripts/UI/ProjectileCollisionDebugPanel.cs` — Phase 3 전략명 및 기존 통계 표시.
- 신규: `Scripts/Projectile/SphereCastAllDetector.cs` — Query, 유효 결과 필터, 최근접 선택, Runner 보고.
- 신규: `Editor/PhaseThreeBuilder.cs`, `Editor/PhaseThreeValidation.cs`, `Editor/PhaseThreeMovementProbe.cs` — Scene 생성 및 재현 가능한 검증.
- 신규: `Scenes/02_SphereCastAll.unity`와 위 신규 Asset에 대해 Unity가 생성한 `.meta`.
- 신규: 이 보고서.

Prefab 신규 생성/수정 없음. Phase 2의 `OnTriggerProjectile.prefab`을 그대로 사용한다. Collider/Rigidbody 조건을 유지하면서 Target의 기존 OnTrigger callback은 `UsesOnTrigger=false`로 Hit 보고를 하지 않는다.

Scene은 `PhaseThreeBuilder.Build`가 Unity Editor API로 생성했다. Phase 2 Scene과의 읽기 전용 비교에서 차이는 Pool 오브젝트의 판정 컴포넌트 추가 및 Runner 전략 플래그뿐이다. Hierarchy, Config, 위치, Layer, 기존 Prefab 참조와 UI 배치는 동일하다. Scene/Prefab YAML과 GUID를 직접 작성하지 않았다.

## 3. SphereCastAll Architecture

`TestProjectile.FixedUpdate` → `movementDistance = Speed * Time.fixedDeltaTime` → Pool에 부착된 `SphereCastAllDetector.TryHit` → `Physics.SphereCastAll` → 필터 → 최소 `hit.distance` → `Runner.ReportHit` → Shot Resolve → Pool Return.

Hit가 없으면 기존 `nextPosition = transform.position + Direction * movementDistance` 및 `transform.position = nextPosition`을 실행하고 기존 `EndBoundary.TryReach`로 종료 여부를 확인한다. EndBoundary Script와 Miss 종료 평면은 변경하지 않았다.

Hit일 때 위치를 `hit.point`로 이동하지 않는다. 현재 이동 시작 위치에서 Resolve/Pool Return하며 해당 Tick의 이동은 수행하지 않는다. Phase 4도 이 정책을 유지해야 한다.

판정 컴포넌트는 Query와 후보 선택만 담당하고, Runner는 pending Shot ID, 결과 및 통계, Projectile은 이동과 반환, Pool은 대여/회수를 계속 소유한다. 추가 Interface/Base Class/Manager는 없다.

## 4. Query Configuration

- Origin: FixedUpdate의 현재 Projectile `transform.position`.
- Direction: Launch에서 설정하는 정규화된 `Vector3.right` (+X).
- Distance: 해당 Tick의 `Speed * Time.fixedDeltaTime`.
- Radius: Config에서 Launcher/Launch로 전달된 값을 Shot에 보관. 대표값 0.05. Collider에서 자동 추출하는 값이 아닌 명시적 Sweep Radius다.
- LayerMask: `1 << target.gameObject.layer`, 현재 `1` (Default Layer 0).
- QueryTriggerInteraction: `Ignore`. 실제 Target BoxCollider는 `isTrigger=false`다.
- Projectile: Phase 2와 같은 Layer 0, SphereCollider `isTrigger=true`, kinematic Rigidbody, Discrete, Interpolation.None, 중력 비활성.

Ground/Projectile/Target이 기존 Default Layer를 공유하므로 Mask만으로 모든 불필요 Collider를 제외할 수 없다. Layer 설정을 바꾸지 않고 Query 결과를 추가로 필터링한다. 자기 Transform 및 자식 Collider, Mask 외 Collider, TestTarget 컴포넌트가 없는 Collider, Runner의 예상 Target과 다른 TestTarget을 제외한다. 유효 후보 중 최소 distance 하나만 선택한다. 같은 최소 거리의 동률은 먼저 만난 유효 후보를 유지하며 배열 순서를 거리 순서로 가정하지 않는다.

현재 Runner는 단일 예상 Target ID를 집계하므로 다중 Collider도 같은 예상 TestTarget에 속한 후보끼리 비교한다. 다른 TestTarget을 맞히는 다중 타깃 게임플레이로 확장하지 않았다.

## 5. Representative Comparison

공통 조건: Fixed Delta Time=0.02 s, Shot Interval=0.1 s, Spawn=(0,1,0), Target=(10,1,0), EndBoundary=(12,1,0), Direction=+X, Pool prewarm=32.

| Strategy | Speed | Travel/Tick | Radius | Thickness | Shots | Detected | Missed | Hit Rate |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| OnTrigger | 40 | 0.80 | 0.05 | 0.20 | 100 | 0 | 100 | 0.00% |
| SphereCastAll | 40 | 0.80 | 0.05 | 0.20 | 100 | 100 | 0 | 100.00% |

OnTrigger 기준은 `PHASE2_REPORT.md`의 실제 기록이며 이번 Phase 2 회귀에서도 동일한 수치를 재확인했다. SphereCastAll은 이번 검증의 **첫 실행 조건**이며 `Logs/PhaseThreeValidation.txt`의 `B representative`에 실제 수치를 기록했다. 대표값을 SphereCast에 유리하게 조정하지 않았다.

관찰 도구는 대표 실행 중 No-Hit 이동 1,200회와 Cast-first Hit 100회를 확인했다. 이전 Tick의 이동 시작 위치/거리로 예상한 다음 위치와 실제 위치를 비교하고, Hit Tick에는 이동하지 않은 채 Resolve되어 비활성 상태로 반환되는지 검사했다. Config 반지름, +X 단위 방향, speed×dt 구간을 사용한다.

## 6. Additional Test Cases

Phase 2 보고서에서 읽은 정확한 A–D 조건을 재사용했다. 모든 조건의 Radius=0.05, dt=0.02, Interval=0.1, Shots=100이다.

| Case | Speed | Travel/Tick | Thickness | OnTrigger Detected/Missed | OnTrigger Hit Rate | SphereCastAll Detected/Missed | SphereCastAll Hit Rate |
|---|---:|---:|---:|---|---:|---|---:|
| A | 15 | 0.30 | 1.00 | 100/0 | 100.00% | 100/0 | 100.00% |
| B | 40 | 0.80 | 0.20 | 0/100 | 0.00% | 100/0 | 100.00% |
| C | 100 | 2.00 | 0.20 | 100/0 | 100.00% | 100/0 | 100.00% |
| D | 200 | 4.00 | 0.05 | 0/100 | 0.00% | 100/0 | 100.00% |

별도 진단은 대표 비교와 구분하고 PlayMode에서만 설정했다. Scene에 저장하지 않았다.

- 최근접 선택: 별도 Origin=(8,1,0), Radius=0.05, Distance=3 Query가 실제로 4개 Hit를 반환했다. 자기 Collider를 일시적으로 non-trigger로 설정하고 더 가까운 잘못된 TestTarget과 예상 Target의 여러 Collider를 배치했다. 선택된 것은 예상 Target의 최근접 자식 Collider, distance=1.4000이었다. 유효 후보들의 최소 거리와 일치했다.
- 동일 Target에 BoxCollider 추가 후 대표 조건 100발: Detected=100, Missed=0, Duplicate=0.
- Target Collider 비활성 후 대표 조건 100발: Detected=0, Missed=100, Duplicate=0. 기존 EndBoundary 경로를 검증했다.
- Target 복구, 판정 컴포넌트 비활성, A 조건 100발: Detected=0, Missed=100, Duplicate=0. OnTrigger가 SphereCastAll Scene의 Hit Resolver로 사용되지 않음을 확인했다.
- 원래 조건 복원 후 대표 조건 재실행: Detected=100, Missed=0, Duplicate=0.

## 7. Result Integrity

완료된 각 실행에서 Fired=Completed=Results.Count=100, `Detected + Missed = 100`, Duplicate=0을 확인했다. Shot ID는 1–100의 고유값이며 각 결과는 IsResolved=true, Hit/Miss 배타성, HitCount=Hit이면 1/Miss이면 0을 만족한다. 결과별 합계와 Runner 통계도 일치한다.

각 완료 시 Pool CreatedCount=32, ActiveCount=0이다. Stop은 발사 중 Shot을 회수하고, Reset은 결과/통계를 0으로 만든다. Reset 후 100발 재실행에서도 Shot ID 1부터 시작하고 Pool을 재사용했다. Stop으로 취소된 Shot을 Miss로 바꾸지 않았다.

Duplicate는 Query 반환 배열 길이가 아니라 Runner에 중복 보고된 Hit 횟수다. 다중 Query 결과 중 하나만 보고하므로 다중 Collider 실행에서도 0이다. 기존 중복 계측 구조는 유지한다.

## 8. Visual / Runtime Validation

- Unity 6000.3.10f1에서 실제 컴파일 및 배치 PlayMode 검증 완료: `Logs/PhaseThreeUnity.log`, `Logs/PhaseThreeValidation.txt` (`PHASE3_PLAYMODE_PASS`).
- 성공한 Phase 3 PlayMode 실행 중 Error/Exception/Assert 수신 시 실패하도록 검사했고 통과했다.
- 7개 버튼의 실제 persistent listener 호출, 설정 변경, Start/Stop/Reset, 완료 후 UI 결과 문자열 유지 검증 완료.
- 실제 Game View에서 Strategy=SphereCastAll, Speed=40, dt=0.020, Travel/Tick=0.800, Radius=0.050, Thickness=0.200과 모든 결과 항목이 잘림·겹침 없이 한 화면에 표시됨을 확인했다.
- 실제 Start 클릭 후 Running과 Detected 증가를 확인했다. Stop 클릭 시 Stopped, Shots=86, Completed=84, Detected=84, Missed=0 상태에서 발사가 멈추고 화면의 Projectile이 회수됐다. Reset 클릭 후 Idle과 모든 카운터 0을 확인했다.
- Start로 대표 조건을 다시 실행해 Complete, Shots=100/100, Completed=100/100, Detected=100, Missed=0, Hit Rate=100.00%, Duplicate=0 및 완료 결과 유지를 육안 확인했다. 이 실행에는 배치 관찰 도구가 없다.
- Speed -/+를 실제 클릭해 40→15→40, Travel/Tick 0.800→0.300→0.800을 확인했다. Thickness +/-도 실제 클릭해 0.200→1.000→0.200과 Target의 실제 두께 변화를 확인했다. 7개 버튼 모두 포인터 입력으로 검증했다.
- GUI 검증 중 보이는 Console Warning/Error 카운터는 모두 0이었다. GUI 실행 로그는 `Logs/PhaseThreeVisual.log`다. 기준값 40/0.2와 Idle로 Reset한 뒤 PlayMode를 종료하고 `02_SphereCastAll` Scene을 열어 두었다.
- Phase 2 회귀: `Logs/PhaseThreePhaseTwoRegression.log`의 `PHASE2_PLAYMODE_PASS`, 프로세스 종료 코드 0. A–D 수치가 기존 보고서와 모두 일치했다. 합성 중복 보고 +2, 잘못된 Target 거부, Hit 후 Miss 무시, Stop/Reset 재실행도 통과했다.
- Phase 1 회귀: `Logs/PhaseThreePhaseOneRegression.log`의 `PHASE1_PLAYMODE_PASS`, 프로세스 종료 코드 0. 기존 이동량, 100발 2회, EndBoundary, Pool, Stop/Reset을 확인했다.

## 9. Issues / Limits

- 최초 샌드박스 Unity 실행은 사용자 캐시 접근 제한으로 시작하지 못했다. 권한 확장 후 실제 검증했다.
- 첫 검증 도구는 Editor 폴더의 MonoBehaviour를 부착하려다 실패했다. 이를 검증 중에만 설치하고 종료 시 복구하는 FixedUpdate PlayerLoop 관찰자로 변경한 뒤 전체 Phase 3 검증을 재실행해 통과했다. 최초 실패 로그는 `Logs/PhaseThreeInitialValidationFailure.log`에 보관했다.
- 관찰 도구는 검증용 Query를 추가 호출한다. 저장된 Scene이나 일반 PlayMode에는 이 관찰자가 없으며, 이번 수치는 성능 측정 결과가 아니다.
- 기존 패키지의 `System.Runtime.CompilerServices.Unsafe.dll` 중복 메시지와 시작 시 플랫폼 확장/라이선스 메시지는 PlayMode 성공 여부와 구분한다. Package 설정은 변경하지 않았다.
- 이번 통제 조건에서 이동 구간 검사가 OnTrigger의 B/D 누락을 검출했다. 모든 충돌 상황의 100% 보장이나 모든 속도/배치에서의 우위는 주장하지 않는다. 시작부터 겹친 상태, 움직이는 Target, 임의 방향/다중 Target 게임플레이 등은 이번 비교 범위가 아니다.
- GC/CPU/Allocation은 측정하지 않았다. SphereCastNonAlloc, Raycast, Overlap, CCD, Buffer/Matrix/CSV 기능은 구현하지 않았다.
- 기존 Phase 2의 실행 중 Inspector Config 변경 및 완료 후 설정 변경에 따른 이전 결과 표시 제한은 그대로다. 각 실행 중 설정을 고정했다.
- 기존 폴더 이동과 `.meta` 삭제/추가 상태는 보존했다. Commit/Push는 수행하지 않았다.
- Unity가 자동 생성한 `ProjectSettings/SceneTemplateSettings.json`은 `Logs/PhaseThreeGeneratedSceneTemplateSettings.json`으로 보관해 요청 범위의 ProjectSettings 변경을 남기지 않았다. 기존 Asset 해시 비교에서 승인받은 Script 3개 외의 변경은 없었다.

## 10. Phase 4 Readiness

SphereCastNonAlloc 비교에 사용할 공통 이동/Config/Pool/집계와 최근접 선택 정책을 확보했다. Phase 4는 Origin, 명시적 Sweep Radius, Direction, Distance, Mask, Ignore, 예상 Target 필터, 자기 hierarchy 제외, 최근접 하나만 ReportHit, Hit 시 이동 없이 반환, No-Hit 이동/EndBoundary 정책을 동일하게 유지해야 한다.

코드/물리 결과와 Game View 검증 기준으로 Phase 4 비교 준비가 됐다. 이번 Phase에서는 SphereCastNonAlloc을 구현하거나 효율성을 주장하지 않았다.
