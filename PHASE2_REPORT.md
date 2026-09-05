# ProjectileCollisionDemo — Phase 2: OnTrigger Baseline

## 1. Final Verdict

**PASS** — 실제 Unity 6000.3.10f1 PlayMode에서 OnTrigger 피격 누락을 재현하고 결과 집계, Pool, Phase 1 회귀 및 실제 Game View 버튼 조작을 검증했다.

## 2. Files Changed

기준 경로: `Assets/ProjectileCollisionDemo/`.

- 수정: `Scripts/Core/ProjectileTestRunner.cs`, `Scripts/Core/ProjectileTestResult.cs`.
- 수정: `Scripts/Projectile/TestProjectile.cs`, `Scripts/Target/TestTarget.cs`, `Scripts/UI/ProjectileCollisionDebugPanel.cs`.
- 생성: `Editor/PhaseTwoBuilder.cs`, `Editor/PhaseTwoValidation.cs`.
- 생성: `Scenes/01_OnTrigger.unity`, `Prefabs/OnTriggerProjectile.prefab`와 Unity가 생성한 대응 `.meta`.
- 생성: 이 보고서.

공통 Config, Launcher, Pool, EndBoundary 및 Phase 1 Scene/Prefab을 재사용한다. 새 Projectile Prefab은 기존 Projectile의 Variant다. Target의 Runner 참조와 UI 크기/폰트는 Phase 2 Scene의 Prefab instance override다. Scene/Prefab은 Unity Editor API로 생성했으며 YAML 또는 GUID를 직접 작성하지 않았다.

## 3. Baseline Architecture

`TestProjectile.FixedUpdate` → `transform.position` 직접 갱신 → `TestTarget.OnTriggerEnter` → Projectile Shot ID 및 예상 Target ID 확인 → `Runner.ReportHit` → Hit 확정 → `TestProjectile.Resolve` → Pool 반환.

Target Hit가 없는 Projectile은 기존 `EndBoundary.TryReach`의 +X 종료 평면 검사 → `Runner.ReportMiss` → Miss 확정 → Pool 반환 경로를 따른다. 이 좌표 검사는 종료에만 사용하며 Target 충돌 보정에 사용하지 않는다.

Runner가 pending Shot ID, 결과, 집계를 소유한다. Hit/Miss는 pending ID를 한 번 제거할 때만 확정한다. 같은 Hit ID가 재보고되면 HitCount와 Duplicate만 증가한다. 이미 Hit인 ID의 Miss 보고와 예상하지 않은 Target ID는 무시한다. Stop으로 취소한 Shot은 IsResolved=false이며 Miss가 아니다. Reset은 결과와 ID를 초기화한다.

Phase 1 Scene은 UsesOnTrigger=false를 유지한다. UI는 Runner의 결과를 표시하고 조작 요청만 전달한다.

## 4. Physics Configuration

- Projectile: SphereCollider radius=0.5 local, IsTrigger=true. 직경 scale=0.1이므로 실제 반지름=0.05.
- Projectile Rigidbody: isKinematic=true, useGravity=false, CollisionDetectionMode.Discrete, Interpolation.None.
- 이동: FixedUpdate + Transform.position. Rigidbody 속도/MovePosition 사용 없음.
- Target: BoxCollider, IsTrigger=false, Rigidbody 없음. Config로 X scale(두께) 적용.
- Projectile/Target Layer: 기존 Default(0). 새 Layer나 ProjectSettings 변경을 요구하지 않는다.
- Fixed Delta Time=0.02 s, Shot Interval=0.1 s, Shot Count=100, Pool=32.
- Spawn=(0, 1, 0), Target=(10, 1, 0), EndBoundary=(12, 1, 0).

## 5. Validation

실제 배치 PlayMode 측정 결과:

- A: Speed=15, Travel/Tick=0.30, Thickness=1.00, Shots=100, Detected=100, Missed=0, Hit Rate=100.00%, Duplicate=0.
- B: Speed=40, Travel/Tick=0.80, Thickness=0.20, Shots=100, Detected=0, Missed=100, Hit Rate=0.00%, Duplicate=0.
- C: Speed=100, Travel/Tick=2.00, Thickness=0.20, Shots=100, Detected=100, Missed=0, Hit Rate=100.00%, Duplicate=0.
- D: Speed=200, Travel/Tick=4.00, Thickness=0.05, Shots=100, Detected=0, Missed=100, Hit Rate=0.00%, Duplicate=0.

각 조건에서 Detected + Missed = 100, Completed=100, Shot ID 중복 없음, 각 결과의 Hit/Miss 배타성, Pool active=0 및 created=32를 확인했다. Stop/Reset 후 100발 재실행, Shot ID 1부터 재시작과 Pool 재사용도 통과했다. 완료 후 UI 결과 문자열 유지와 버튼의 실제 persistent listener 호출을 확인했다.

별도 합성 API 검사에서 같은 Hit ID를 2회 재보고하여 Duplicate만 +2 증가함을 확인했다. 이 수치는 위 물리 측정 결과에 포함하지 않았다. Hit 후 Miss 보고 무시와 잘못된 Target ID 거부도 확인했다.

Unity 컴파일 및 Phase 2 검증 로그: `Logs/PhaseTwoUnity.log`, `Logs/PhaseTwoValidation.txt` (`PHASE2_PLAYMODE_PASS`). 기존 Phase 1 검증기도 재실행하여 `Logs/PhaseOneRegression.log`의 `PHASE1_PLAYMODE_PASS`를 확인했다. Phase 1 검증에는 실제 FixedUpdate 이동량 검사, 100발 2회, Stop/Reset 및 Pool 검사가 포함된다.

Runtime 소스에 Raycast/SphereCast/Overlap/Sweep/CCD/MovePosition/velocity 보정 없음. 검증 중 PlayMode Error/Exception 수신 시 실패하도록 구성했으며 해당 검사 통과.

## 6. Tunneling Reproduction

대표 누락 조건은 B: Speed=40, dt=0.02, Travel/Tick=0.8, Thickness=0.2, Target=(10,1,0). 실제 100발 중 100발이 Target Trigger 감지 없이 EndBoundary에 도달했다.

C의 더 높은 속도 100에서는 100발 모두 감지됐다. 고정 Spawn/Target에 대한 이산 이동 위치 관계와 일치하는 결과이며 속도에 따른 단조로운 실패율을 주장할 수 없다. 동일한 궤적과 발사 조건을 반복하므로 모든 Shot의 결과가 같을 수 있다.

특정 고정 조건에서 Transform 기반 discrete 이동과 OnTrigger Baseline의 피격 누락을 재현했다. 이것만으로 모든 고속 Projectile의 실패, Unity Trigger 전반의 신뢰성, 다른 방식의 성능 우위 또는 원인의 100% 확정을 주장하지 않는다.

## 7. Visual Validation

실제 Unity Game View에서 Strategy=OnTrigger, 설정/결과 텍스트가 겹치거나 잘리지 않고 보이며 Travel Per Tick / Target Thickness / Hit Rate가 한 화면에 표시됨을 확인했다.

포인터로 Start를 눌러 Running과 Miss 실시간 증가를 확인했다. Stop 클릭 시 Stopped(발사 80, 완료 77) 상태와 Projectile 회수를 확인했다. Reset 클릭 후 Idle 및 모든 카운터 0 복귀를 확인했다. B 조건을 다시 Start하여 100발 완료, Detected=0/Missed=100/Hit Rate=0.00%/Duplicate=0과 완료 결과 유지를 육안 확인했다.

Speed - 버튼으로 40→15 및 Travel/Tick 0.8→0.3을 확인했다. Thickness + 버튼으로 0.2→1.0 및 실제 Target의 두께 증가를 확인했다. A 조건을 Start하여 Detected 실시간 증가와 Hit Rate=100.00%를 확인했다. 현재 관찰한 Game View에서 Console의 Error/Warning 카운터는 모두 0이다. 고속 Projectile이 Target을 가로지르는 연속 프레임 장면의 육안 판독은 별도로 확정하지 않았으며 누락 판단은 Shot 결과에 근거한다.

A 조건도 최종 Detected=100/Missed=0/Hit Rate=100.00%/Duplicate=0을 육안 확인했다. Speed +와 Thickness -의 역방향 조작도 확인하여 7개 버튼 모두 실제 클릭했다. 기준값 40/0.2 및 Idle로 Reset한 뒤 PlayMode를 종료하고 `01_OnTrigger` Scene을 열어 두었다.

## 8. Issues / Deviations

- EndBoundary는 Phase 1의 좌표 기반 종료 평면을 유지했다. Target Hit만 OnTrigger 이벤트에 의존한다.
- Hit 즉시 반환하므로 자연 발생 Duplicate=0은 모든 다중 Collider 상황에서 중복이 없다는 보장이 아니다. 계측은 Runner에 전달된 보고 횟수를 센다.
- 대표 A–D 조건을 재현 가능한 검증 도구로 실행했다. 전체 Speed×Thickness 자동 Matrix나 성능 Benchmark는 구현하지 않았다.
- Inspector를 통한 실행 중 Config 직접 변경은 잠그지 않는다. 실행 중 설정은 변경하지 않아야 한다.
- 완료 후 설정 버튼을 변경하면 기존 결과는 다음 Start/Reset까지 유지되지만 설정 표시는 새 값으로 바뀐다. 이전 결과를 기록한 뒤 다음 조건으로 변경해야 한다.
- 초기 샌드박스 실행은 Unity 사용자 캐시 접근 실패로 시작하지 못했다. 권한 확장 실행에서 컴파일 및 PlayMode 검증을 완료했다.
- 기존 패키지의 `System.Runtime.CompilerServices.Unsafe.dll` 중복 메시지와 시작 시 라이선스 access-token 메시지가 로그에 있다. Package 설정을 변경하지 않았다.
- 기존 작업의 폴더 이동과 `.meta` 삭제/추가 상태는 보존했다. Commit/Push는 수행하지 않았다.
- Unity가 이번 실행 중 자동 생성한 `ProjectSettings/SceneTemplateSettings.json`은 작업 결과에서 제거하여 기존 ProjectSettings 상태를 보존했다.

## 9. Phase 3 Readiness

대표 Hit/Miss 조건, Shot ID 기반 집계, Pool 재사용 및 Phase 1 회귀 검증을 확보했다. 동일한 Spawn/Target 좌표와 Config로 Phase 3 비교에 사용할 수 있다. SphereCastAll/SphereCastNonAlloc 구현은 추가하지 않았다.
