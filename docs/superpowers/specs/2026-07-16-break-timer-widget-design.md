# Break Timer 원형 위젯 — Design Spec

## 배경 및 목표

현재 Break Timer(⌃3)는 화면 전체를 덮는 풀스크린 오버레이다. 화면을 완전히 가리기 때문에 타이머를 켜둔 채로 다른 작업을 계속하기 어렵다. 화면 한쪽에 떠서 자유롭게 드래그해 옮길 수 있는 작은 원형 위젯으로 교체해, 다른 앱 작업을 방해하지 않으면서도 남은 시간을 계속 확인할 수 있게 한다.

HTML 목업(인터랙티브 프로토타입)으로 인터랙션·비주얼을 먼저 검증했고, 사용자 승인 완료.

## 요구사항 요약

- 화면 전체를 덮는 대신 **작은 원형 위젯**으로 완전히 대체 (풀스크린 모드는 제거, 별도 토글 없음)
- 원 안에 **남은 시간 숫자 + 진행률 링**(남은 시간 비율만큼 색이 채워진 원형 링)을 표시
- **드래그로 자유롭게 위치 이동** 가능, 옮긴 위치는 다음 실행 시 기억
- 마우스를 올리면(hover) **닫기(×) / 시간 조절(−, +) 버튼**이 나타남 (평소엔 숨김)
- 기존 키보드 단축키(방향키 시간조절, Escape 닫기, 색상키)는 **제거** — 버튼 방식으로 완전 대체
- 만료(0초 도달) 시 **링이 빨간색으로 바뀌며 깜빡이고**, 숫자는 경과 시간 카운트업으로 전환 (`showElapsed` 설정 존중)
- 위젯 표시 중에도 **다른 앱의 포커스를 뺏지 않음** (기존엔 `NSApplication.activate`로 앱을 활성화시켰음)

## 현재 아키텍처 관련 사실

- `Overlay/BreakTimerWindow.swift`: 화면 크기 그대로의 `.borderless` 풀스크린 창. `level = .screenSaver`, `collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]`, `ignoresMouseEvents = true`(마우스 통과) — Draw 오버레이 위에 표시되도록 설계되어 있고 이 레벨/collectionBehavior는 원형 위젯에도 그대로 유효하다.
- `Overlay/BreakTimerWindowController.swift`: `showTimer()`에서 배경 모드가 `.fadedDesktop`이면 `ScreenCaptureKit`(`SCShareableContent`, `SCScreenshotManager`)으로 데스크톱을 캡처해 어둡게 깔아 배경으로 쓴다. 원형 위젯은 화면을 가릴 필요가 없으므로 이 캡처 경로 전체가 불필요해진다. `NSApplication.shared.activate(ignoringOtherApps: true)`를 호출해 앱을 활성화시키는데, 이는 다른 작업을 방해하지 않는다는 새 목표와 배치된다.
- `Overlay/BreakTimerView.swift`: `draw(_:)`에서 배경 → 메인 타이머 텍스트 → (만료 시) 경과 텍스트 순으로 그리고, 위치는 `BreakTimerPosition`(3×3 그리드) 기반. `keyDown`으로 방향키(시간조절)/Escape(닫기)/색상키를 처리하며, 이를 위해 창이 `canBecomeKey`/`canBecomeMain`을 `true`로 오버라이드하고 `makeFirstResponder`로 포커스를 받는다.
- `Models/BreakTimerState.swift`: `BreakTimerPosition`(3×3 그리드 enum), `BreakTimerBackground`(`.black`/`.fadedDesktop` enum) 두 enum과 `tick()`/`adjustTime(byMinutes:)`/`formattedTime`/`formattedElapsed` 등 타이머 로직 보유. 타이머 로직(`tick`, `adjustTime`, 포맷팅)은 위젯 형태와 무관하므로 그대로 재사용 가능.
- `Settings/BreakTimerTab.swift`: Duration/Color/Opacity/Background/Fade Darkness/Show Elapsed/Play Sound 설정 UI. Background·Fade Darkness는 이번에 제거 대상.
- `Settings/Settings.swift`: `breakTimerBackground`, `breakTimerBackgroundFadeDarkness` 등 `UserDefaults` 키 보유 — 제거 대상 포함.
- `Draw/DrawingCanvasView.swift`의 펜 커서 배율 로직(2026-07-15 작업)처럼, 이 코드베이스는 뷰 크기에 비례해 렌더링 요소 크기를 계산하는 패턴을 이미 쓰고 있다 — 위젯 크기 대비 폰트/링 두께 계산에 동일한 접근을 따른다.

## 채택 접근: 작은 `NSPanel` + AppKit 기본 드래그(`isMovableByWindowBackground`)

검토한 대안:
- **(B) 기존 풀스크린 창을 유지하고 원만 그 안에서 커스텀 드래그 처리**: 화면 전체 크기 투명 창 위에서 마우스 이벤트를 받아 원 영역만 히트테스트하는 방식. 화면 전체 크기 창이 계속 마우스 이벤트를 가로챌 여지가 있고(구석 데드존 등), `ignoresMouseEvents`를 전체 창 단위로 켜고 끄는 로직이 필요해 복잡함. 기각.
- **(C) SwiftUI `WindowGroup`/`Panel`로 전면 재작성**: 이 프로젝트의 오버레이 계열은 전부 AppKit(`NSWindow`/`NSView` + `draw(_:)`)로 통일되어 있어(Draw, Zoom 등), 이 기능만 SwiftUI 창으로 바꾸면 스타일이 어긋나고 기존 `NSWindow` 레벨/collectionBehavior 제어 방식과도 안 맞음. 기각.
- **(A, 채택) 창 자체를 위젯 크기로 축소 + AppKit 기본 드래그**: 창을 위젯 지름만큼의 정사각형으로 줄이고 `.nonactivatingPanel` + `isMovableByWindowBackground = true`를 쓰면 커스텀 드래그 코드 없이 AppKit이 드래그·"컨트롤 위에서는 드래그 안 걸림" 처리를 대신해준다. 화면 캡처·전체화면 렌더링 로직이 통째로 사라져 코드가 단순해진다.

## 상세 설계

### 1. 창(Window) 구조 — `BreakTimerWindow`

- `contentRect`를 화면 크기 대신 **고정 정사각형**(지름 기본값 110pt + 여백)으로 변경. 정확한 기본 지름은 구현 단계에서 확정하되, 설정 UI에 크기 조절 슬라이더는 두지 않는다(목업에서 크기 시연은 방향성 확인용이었고, 요구사항엔 크기 설정이 없었으므로 YAGNI — 필요해지면 후속 요청으로 추가).
- `styleMask`에 `.nonactivatingPanel` 추가(`NSPanel` 서브클래스로 전환). **(구현 시 확정: `isFloatingPanel = true`는 의도적으로 생략 — `level = .screenSaver` 설정 이후에 켜면 AppKit이 레벨을 `.floating`으로 되돌려버리므로 설정하면 안 됨.)**
- `level = .screenSaver`, `collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]` 그대로 유지.
- `isMovableByWindowBackground = true` — 원의 빈 배경(버튼이 없는 영역)을 누르고 드래그하면 창이 따라 움직인다. 버튼(`NSButton` 서브뷰) 위를 누르면 AppKit이 자동으로 드래그를 억제하므로 별도 hit-test 코드가 필요 없다.
- `ignoresMouseEvents`는 더 이상 쓰지 않는다(전체 화면을 덮지 않으므로 마우스 통과가 필요 없음) — 창 바깥은 애초에 창 영역이 아니라 다른 앱이 정상적으로 마우스를 받는다.
- `canBecomeKey`/`canBecomeMain`을 `true`로 오버라이드할 필요가 없어진다 — 키보드 단축키를 없애므로 첫 응답자 관리가 불필요.

### 2. 컨트롤러 — `BreakTimerWindowController`

- `showTimer()`에서 `ScreenCaptureKit` 캡처 분기(`captureScreenImage`, `Task { @MainActor in ... }`)를 전부 제거. 배경 모드 분기 없이 곧바로 `presentTimer(screen:)` 호출.
- `NSApplication.shared.activate(ignoringOtherApps: true)` 호출 제거 — 위젯을 띄워도 현재 활성 앱(포커스)이 바뀌지 않게 한다. `window.orderFront(nil)`만으로 화면에 표시.
- `presentTimer`: 저장된 위젯 위치(`Settings.breakTimerWidgetPosition`)가 있으면 그 좌표로, 없으면 화면 중앙 기본값으로 창을 배치.
- `dismiss()`: 기존 로직(타이머 무효화, 사운드 정지, 창 닫기, `appDelegate.breakTimerDidEnd()`)에 더해 **현재 창 위치를 `Settings`에 저장**하는 단계 추가.
- `startCountdown()`(1초 간격 `Timer`)은 그대로 재사용.

### 3. 뷰 — `BreakTimerView`

- 배경: 반투명 검정 원(`opacity` 설정 반영), 링 색상은 `timerColor`. **(구현 시 확정: 중앙 숫자는 사용자 승인 HTML 목업 그대로 흰색 고정 — `timerColor`는 링에만 적용. 가독성을 위해 숫자는 `opacity` 설정과도 무관하게 항상 불투명.)**
- 진행률 링: `remainingSeconds / defaultDuration` 비율만큼 12시 방향에서 시계방향으로 그림. `draw(_:)` 내에서 `CGContext`의 `addArc`로 그리거나, 별도 `CAShapeLayer`(`wantsLayer = true`로 전환) 중 구현 단계에서 성능/코드 단순성을 비교해 선택.
- 중앙 숫자: 기존 `formattedTime`/`formattedElapsed`를 재사용하되, 3×3 그리드 포지셔닝(`BreakTimerPosition.origin(forTextSize:in:)`) 대신 항상 원 중앙에 그린다.
- 만료 시: 링 색을 고정 빨강으로 바꾸고 0.5~1초 주기로 알파를 오가는 펄스 애니메이션(타이머 틱에서 `needsDisplay` 트리거 또는 `CABasicAnimation`). `showElapsed`가 켜져 있으면 숫자를 경과 카운트업으로, 꺼져 있으면 "0:00" 고정 표시.
- 호버 버튼(×, −, +): `NSTrackingArea`(`.mouseEnteredAndExited`, `.activeAlways`)로 hover를 감지해 3개의 `NSButton` 서브뷰(원 하단에 배치)를 페이드인/아웃. `−`/`+`는 기존 `state.adjustTime(byMinutes:)`(±1분) 그대로 호출, `×`는 `onDismiss?()` 호출.
- **제거**: `keyDown` 오버라이드 전체(방향키/Escape/색상키 처리), `acceptsFirstResponder` 오버라이드.

### 4. 데이터 모델 — `BreakTimerState` / `Settings`

- **제거**: `BreakTimerPosition` enum(3×3 그리드), `BreakTimerBackground` enum(`.black`/`.fadedDesktop`), `Settings.breakTimerBackground`, `Settings.breakTimerBackgroundFadeDarkness` 및 관련 `UserDefaults` 키.
- **추가**: 위젯 위치 저장용 `Settings.breakTimerWidgetPositionX`/`Y`(`Double`, 옵셔널 — 저장된 값 없으면 위치 기본값은 화면 중앙에서 계산). `resetToDefaults()`의 `allKeys` 목록도 함께 갱신.
- **유지**: `defaultDuration`, `timerColor`, `opacity`, `showElapsed`, `playSoundOnExpiration`, `soundFileURL`, `tick()`, `adjustTime(byMinutes:)`, `formattedTime`, `formattedElapsed`, `reloadFromSettings()`.

### 5. 설정 UI — `BreakTimerTab.swift`

- **제거**: "Background" `Picker`, "Fade Darkness" `Slider`와 관련 `@AppStorage` 바인딩(`backgroundRaw`, `fadeDarkness`).
- **유지**: Duration Stepper, Timer Color Picker, Opacity Slider, Show Elapsed / Play Sound 토글, 사운드 파일 선택 및 테스트 재생 버튼 — 전부 그대로 둔다.

## 기존 기능과의 상호작용

- **사운드 알림**: 만료 시 사운드 재생 로직(`playExpirationSound`, `playTestSound`)은 위젯 형태와 무관하므로 변경 없음.
- **`bringToFront()`**: 메뉴바 클릭 시 위젯을 앞으로 가져오는 동작은 유지하되, 여기서도 앱을 강제 활성화(`activate`)하지 않는 방향으로 맞춘다 — 위젯만 화면 맨 위로.
- **`AppDelegate.breakTimerDidEnd()`**: 변경 없음 — 콜백 시그니처 그대로 재사용.
- **다른 오버레이(Draw, Zoom)와의 레벨 관계**: `level = .screenSaver` 유지로 기존과 동일하게 Draw 오버레이보다 위에 뜬다.

## 테스트 관점

- `BreakTimerState`의 기존 로직(`tick`, `adjustTime`, 포맷팅)은 변경이 없으므로 기존 테스트가 그대로 통과해야 한다.
- 새로 추가되는 위치 저장(`Settings.breakTimerWidgetPositionX/Y`) get/set과 `resetToDefaults()` 반영 여부를 `SettingsTests.swift` 스타일로 검증.
- `BreakTimerPosition`/`BreakTimerBackground` 제거로 인해 깨지는 기존 테스트가 있는지 확인하고 함께 정리.
- 진행률 링 비율 계산(`remaining/duration` → 각도/알파)은 뷰 로직과 분리 가능하면 순수 함수로 빼서 단위 테스트 가능하게 한다 — 실제 구현 계획(writing-plans) 단계에서 구체화.

## 범위 밖 (Out of Scope)

- 위젯 지름을 설정 UI에서 조절하는 기능 (목업으로 방향만 확인, 고정값 하나만 구현).
- 풀스크린/원형 두 스타일을 오가는 토글 (원형으로 완전 대체, 풀스크린 모드 삭제).
- 데스크톱 캡처 기반 배경(`fadedDesktop`) — 원형 위젯 배경은 항상 단순 반투명 검정.
- 키보드 단축키 유지 — 버튼 방식으로 완전 대체.
- 여러 화면(멀티 모니터) 간 위젯 위치를 화면별로 따로 기억하는 기능 (단일 저장 위치만 지원).

## 작업 위치

- Fork: https://github.com/workcalmkite-hue/ZoomacIt (origin)
- Upstream: https://github.com/07JP27/ZoomacIt (추후 원본 업데이트 병합용)
- 로컬 클론: `~/Desktop/ZoomacIt`
- 참고 목업: HTML 인터랙티브 프로토타입(원형 위젯 드래그/호버/만료 애니메이션) — 사용자 승인 완료
