# ProjectileCollisionDemo — Phase 1

## 1. Final Verdict

**CONDITIONAL PASS** — Unity 6000.3.10f1 실제 배치 PlayMode 자동 검증 통과.
화면을 직접 보며 확인하는 UI 잘림·가독성·마우스 클릭 검증은 미검증이다.

새 Scene: `Assets/ProjectileCollisionDemo/Scenes/ProjectileCollisionTestScene.unity`
기존 `Assets/Scenes/ProjectileCollisionTestScene.unity`는 변경하지 않았다.
새 Scene을 열고 Play 후 Start Test로 실행한다.

## 2. Files Created

`Assets/ProjectileCollisionDemo/` 아래에 다음을 생성했다.

- `Scripts/Core/ProjectileTestConfig.cs`
- `Scripts/Core/ProjectileTestRunner.cs`
- `Scripts/Core/ProjectileTestResult.cs`
- `Scripts/Projectile/TestProjectile.cs`
- `Scripts/Projectile/ProjectilePool.cs`
- `Scripts/Projectile/ProjectileLauncher.cs`
- `Scripts/Target/TestTarget.cs`
- `Scripts/Target/EndBoundary.cs`
- `Scripts/UI/ProjectileCollisionDebugPanel.cs`
- `Editor/ProjectileTestbedBuilder.cs`
- `Editor/PhaseOneValidation.cs`
- `Scenes/ProjectileCollisionTestScene.unity`
- `Prefabs/Projectile.prefab`
- `Prefabs/Target.prefab`
- `Prefabs/ProjectileCollisionDebugPanel.prefab`
- `Materials/Projectile.mat`, `Target.mat`, `Ground.mat`, `Boundary.mat`
- Unity가 생성한 대응 `.meta`와 폴더 `.meta`

이 문서 외에 검증 기록은 Git 제외 디렉터리인 `Logs/PhaseOneValidation.txt`, `Logs/PhaseOneUnity.log`에 있다.

## 3. Architecture

- Config: 속도·반지름·두께·발사 수·간격의 공통 설정 컴포넌트.
- Runner: 실행 상태, 발사 일정, Shot ID, 미완료 Shot 집합 및 결과 소유.
- Launcher: Runner 요청을 받아 Pool에서 대여하고 발사 조건 적용.
- Pool: 비활성 Queue와 활성 집합을 관리. 32개 예열 후 부족할 때만 확장.
- Projectile: FixedUpdate의 Transform 직선 이동. 다음 위치를 EndBoundary에 전달하고 도달 시 반환.
- EndBoundary: +X 방향 종료 평면의 좌표 판정과 Shot 완료 이벤트. 충돌 성공/실패 판정이 아니다.
- Target: X 두께 적용. BoxCollider는 있으나 Hit 처리는 없다.
- Result: ShotId와 ReachedEndBoundary만 저장.
- Panel: Runner 상태를 읽고 Runner API로 명령 전달. Projectile 참조 없음.

참조 흐름: Panel → Runner → Launcher → Pool/Projectile, Projectile → EndBoundary → Runner 이벤트.
이동 적용은 `MoveTo`로 분리했으며 실제 Collision Strategy 또는 인터페이스는 아직 없다.

## 4. Scene Structure

```text
ProjectileCollisionTestScene
├─ Systems
│  ├─ ProjectilePool
│  ├─ ProjectileLauncher
│  └─ ProjectileTestRunner (Config 포함)
├─ Environment
│  ├─ Target
│  ├─ EndBoundary
│  ├─ Ground
│  └─ LauncherVisual
├─ SpawnPoint
├─ Main Camera
├─ Directional Light
├─ Canvas
│  └─ ProjectileCollisionDebugPanel
└─ EventSystem
```

발사점 (0, 1, 0), Target (10, 1, 0), 경계 (12, 1, 0).
카메라 오른쪽 영역에 세 지점이 들어오는 것을 PlayMode 좌표 검사로 확인했다.

## 5. Test Configuration

- Speed: 40 unit/s
- Radius: 0.05 unit
- Thickness: 0.2 unit
- Shot Count: 100
- Shot Interval: 0.1 s
- Fixed Delta Time: 기존 `Time.fixedDeltaTime` 0.02 s를 읽음
- Travel Per Tick: 0.8 unit
- Pool Prewarm: 32

Speed 버튼 단계는 15/40/100/200, Thickness는 0.05/0.2/1.0이다.
실행 중 UI의 설정 변경 요청은 무시한다. Inspector 직접 편집을 잠그는 기능은 없다.
공정한 실행을 위해 실행 중 Inspector에서 Config를 변경하지 않아야 한다.

## 6. Validation

Unity Editor 컴파일 후 실제 PlayMode에서 다음을 확인했다.

1. PASS 1: 속도 40에서 관찰한 FixedUpdate 경과 시간에 맞는 +X 이동량 및 Y/Z 유지.
2. PASS 2: 공통 기본 설정, Speed 변경, Thickness 변경 시 실제 Target X 크기 반영.
3. PASS 3: 100 Shot 결과를 두 차례 수집하고 각 실행의 Shot ID 중복 없음 확인.
4. PASS 4: 모든 정상 Shot의 ReachedEndBoundary=true 확인.
5. PASS 5: 완료 후 활성 Pool 0개, 생성 수 32개 유지, Reset 후 재사용.
6. PASS 6: UI의 100/100 완료 수, 이동량 0.800, 반지름 0.050, 두께 0.200 문자열 및 7개 버튼의 Prefab 참조 확인.
7. PASS 7: 중간 Stop 시 활성 Projectile 회수·결과 수 일치, Reset 후 Shot ID 1부터 100발 재실행 완료.
8. PASS 8: 런타임 소스 검사에서 OnTrigger/SphereCast/Raycast/Rigidbody/UnityEditor 사용 없음.

검증 로그에 `PHASE1_PLAYMODE_PASS` 기록. 해당 실행에서 C# 컴파일 오류 및 Exception 검색 결과 없음.
자동화는 버튼의 공개 동작 메서드를 호출하고 persistent listener 대상을 검사했다. 실제 포인터 입력과 화면 렌더링에 대한 육안 검사는 수행하지 않았다.

재실행 명령 (Unity가 해당 프로젝트를 열고 있지 않을 때):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe' -batchmode -projectPath 'D:\CS_Project\ProjectileCollisionDemo' -executeMethod ProjectileCollisionDemo.Editor.PhaseOneValidation.BuildAndValidate -logFile 'D:\CS_Project\ProjectileCollisionDemo\Logs\PhaseOneUnity.log'
```

검증 도구는 완료 시 Editor를 종료한다. 일반 편집 세션용 메뉴가 아니라 배치 실행용이다.

## 7. Issues / Deviations

- Config는 ScriptableObject 대신 컴포넌트를 사용했다.
- 기존 동명 Scene 보호를 위해 전용 하위 폴더에 새 Scene을 생성했다.
- Start는 이전 실행 결과를 비우고 새 실행을 시작한다. Shot ID는 Start 간에는 이어지고 Reset에서만 1로 초기화한다.
- Stop은 일시정지가 아니다. 활성 Shot을 회수하고 ReachedEndBoundary=false 결과를 남긴다. Completed는 경계 도달 수다.
- Stop/Reset으로 모든 발사체를 회수하므로 취소된 Shot의 지연 결과가 다음 실행에 섞이지 않는다.
- Detected/Missed는 N/A. 충돌 판정, 성능 측정, Strategy별 Scene 복제는 수행하지 않았다.
- Scene/Prefab/Material은 Unity API로 생성했다. serialized YAML 및 GUID를 직접 작성하지 않았다.
- 기존 패키지 간 `System.Runtime.CompilerServices.Unsafe.dll` 버전 중복 메시지가 Unity 로그에 있다. Package 설정은 변경하지 않았다.
- 육안 UI 확인이 남아 있으므로 최종 판정은 CONDITIONAL PASS다.

## 8. Phase 2 Readiness

공통 이동·설정·발사·결과 수집 기반은 자동 검증을 통과했으며 OnTrigger Baseline을 추가할 준비가 되어 있다.
Phase 1 최종 PASS 전에는 새 Scene을 Play하여 UI 가독성과 실제 버튼 클릭을 확인해야 한다.
이후 이동 공식을 유지하면서 충돌 처리 책임을 별도로 추가하면 된다.
