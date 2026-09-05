# Phase 6-A — SphereCast GC Allocation

## Final Verdict

**CONDITIONAL PASS** — 실제 Unity Profiler 캡처 6개에서 동일 Query 횟수와 반복 GC 차이를 확인했다. CPU Usage / Hierarchy 화면의 수치 교차 확인은 UI 자동화의 파일 대화상자 포커스 문제로 미완료다. 아래 값은 추정이나 GC.GetAllocatedBytes 기반 값이 아니라 Unity Profiler의 RawFrameDataView에서 읽은 GC.Alloc Sample의 바이트 메타데이터다.

## Test Condition

- Unity 6000.3.10f1, Windows Editor PlayMode, Deep Profile OFF, Editor profiling OFF.
- Scene: `02_SphereCastAll`, `03_SphereCastNonAlloc`만 측정.
- Speed: 40, Radius: 0.05, Target Thickness: 0.2, Fixed DT: 0.02 s.
- 매 Run 1,000발 동시 발사(기존 Runner, ShotInterval = 0), 실제 13,000 Query.
- 각 Scene에서 Pool 1,000개 Prewarm + 1,000발 워밍업 1회 후 측정 3회.
- UI 문자열 갱신 컴포넌트 비활성화. 정상 실행 중 Debug.Log, 커스텀 Debug Draw/Gizmo, VFX/SFX 코드는 기존 Runtime Script에 없음.
- 측정 중 Pool CreatedCount = 1,000 유지, 종료 시 ActiveCount = 0. 매 Run 1,000 Hit, 0 Miss, 0 Duplicate.
- 조건 변경은 PlayMode 인스턴스에만 적용. Scene/Prefab 저장 및 ProjectSettings/Package 변경 없음.

## SphereCastAll / SphereCastNonAlloc

GC Alloc은 해당 Run의 **Query Marker 하위 GC.Alloc 합계**이며 전체 Frame GC 합계가 아니다.

| Strategy | Run | Shot/Query 조건 | Query GC Alloc | Notes |
|---|---:|---|---:|---|
| SphereCastAll | 1 | 1,000 shots / 13,000 queries | 76,000 B | 76 B × 1,000 queries; 나머지 12,000은 0 B |
| SphereCastAll | 2 | 1,000 shots / 13,000 queries | 76,000 B | 동일 |
| SphereCastAll | 3 | 1,000 shots / 13,000 queries | 76,000 B | 동일 |
| SphereCastNonAlloc | 1 | 1,000 shots / 13,000 queries | 0 B | 13,000 queries에서 하위 GC.Alloc Sample 없음 |
| SphereCastNonAlloc | 2 | 1,000 shots / 13,000 queries | 0 B | 동일 |
| SphereCastNonAlloc | 3 | 1,000 shots / 13,000 queries | 0 B | 동일 |

## Result

실제 Profiler 캡처에서 SphereCastAll Query 구간의 반복 Allocation을 확인했고, 동일한 13,000 Query 조건의 SphereCastNonAlloc Query 구간에서는 Allocation이 관찰되지 않았다. SphereCastNonAlloc의 기존 Projectile별 RaycastHit[16] 재사용 코드는 유지했다.

SphereCastAll의 모든 호출이 할당했다고 표현하지 않는다. 이 환경에서는 13,000개 중 1,000개 Query에만 각각 76 B가 기록됐다. Hit가 있는 Query의 결과 배열 할당과 일치하는 패턴이지만, 개별 배열 객체 타입/반환 길이를 따로 계측하지는 않았다.

## Noise / Limits

- Query Marker는 API 호출과 인자 평가만 감싼다. 최근접 Hit 선택, ReportHit, Pool 반환, UI 및 Editor 측정 도구는 Marker 밖이다.
- All Run 1의 첫 Query Frame: Query GC 0 B / Main Thread GC 654,336 B. All Run 2의 Query Frame: Query GC 76,000 B / Main Thread GC 94,204 B. 외부 Allocation이 있으므로 전체 Frame GC를 결론 근거로 사용하지 않았다. 외부 할당의 정확한 호출 원인은 미분석이다.
- 동시 발사 부하로 여러 FixedUpdate가 한 렌더 Frame에 묶였다. All Run 1은 Query가 있는 Frame 2개, 다른 Run은 각각 1개였다. 따라서 GC/Frame 평균을 서로 비교하지 않고, 동일한 13,000 Query 전체와 Query별 GC를 3회 반복 비교했다.
- Editor 환경이며 다른 앱도 실행 중이었다. CPU 성능, 전체 Frame/Projectile의 Zero Allocation, Player/IL2CPP 결과는 주장하지 않는다.
- Profiler Hierarchy 화면 교차 확인과 증빙 스크린샷은 미완료. 원본 `.data` 6개는 저장 완료다.

## Verification

- Unity가 측정용 Script를 컴파일하고 실제 PlayMode에서 워밍업 2회 + 측정 6회를 실행했다.
- 측정 기간 Error/Exception/Assert 콜백 없음. 로그에서도 C# 컴파일 오류/Exception 없음.
- 후속 캡처 보기 메뉴 및 Editor 설정 복원 보완은 dotnet build 성공(0 errors). SDK/Package 참조의 System.Threading.Tasks.Extensions 버전 충돌 경고 1개. 이 후속 보완의 Unity 실행은 미검증이다.
- 기존 파일의 작업 전후 SHA-256 비교: 변경된 기존 Asset은 승인된 두 Detector Script뿐. 두 Script diff는 Marker 추가만 포함하며 이동, Speed/Radius 전달, Target/LayerMask, Hit 처리, Pool, 최근접 Hit 정책은 유지됐다.
- 기존 미추적 프로젝트 파일과 삭제된 구형 `.meta` 상태는 작업 시작 전부터 존재했으며 건드리지 않았다. Commit/Push 없음.

## Evidence / Reproduce

- 원본 및 바이트 기록: `Logs/Phase6A/02_SphereCastAll-Run1..3.data`, `03_SphereCastNonAlloc-Run1..3.data`와 같은 이름의 `.txt`.
- 전체 결과: `Logs/Phase6A/Results.txt`; Unity 실행 로그: `Logs/Phase6A/Editor.log`.
- `Logs`는 기존 gitignore에 의해 제외된다. 포트폴리오에 원본을 첨부하려면 별도 보관이 필요하다.
- 재측정 메뉴: `Tools > Projectile Collision Demo > Measure Phase 6-A GC`. 기존 Profiler 기록을 교체하므로 필요한 캡처는 먼저 저장한다.
- 화면 확인: Profiler에서 `.data`를 Load하고 CPU Usage / Hierarchy에서 `SphereCastAll Query` 또는 `SphereCastNonAlloc Query` 검색. Run 3의 Raw Frame index는 All = 0, NonAlloc = 1이며 UI의 Frame 표시는 1부터 시작할 수 있다.
- 캡처 보기 보조 메뉴: `View Phase 6-A All Run 3`, `View Phase 6-A NonAlloc Run 3` (후속 추가, Unity 실행 확인 필요).
- 추출 방식 참고: Unity 공식 [RawFrameDataView.GetSampleMetadataAsLong](https://docs.unity.cn/6000.2/Documentation/ScriptReference/Profiling.RawFrameDataView.GetSampleMetadataAsLong.html)의 GC.Alloc 바이트 읽기 API를 사용하고 Query Marker의 하위 Sample로 범위를 제한했다.

## Phase 7 Readiness

동일 Query 조건의 GC 비교 수치는 Decision/Measure 초안에 사용할 수 있다. 공개 포트폴리오의 최종 증빙으로는 저장된 캡처를 Profiler Hierarchy에서 확인하고 스크린샷을 추가한 뒤 사용하는 것을 권장한다.
