# Vanishing Pen — Design Spec

## 배경 및 목표

ZoomacIt의 기존 Draw 기능은 펜으로 그린 획이 화면에 영구히 남는다. 강의 중 화면에 계속 표시하고 싶지 않은 임시 주석(밑줄, 원, 화살표 등)을 그릴 때, 일정 시간이 지나면 자동으로 서서히 사라지는 "Vanishing Pen" 모드를 추가한다.

## 요구사항 요약

- 그린 선이 **일정 시간(레이저 포인터 잔상 느낌)이 지나면 자동으로 사라짐**
- 사라지는 시간은 **설정 UI에서 슬라이더로 조절 가능**
- 기존 일반 Draw와 **별도의 토글("Vanishing Pen" 모드)**로 켜고 끔 — 켜져 있지 않을 때는 기존 동작과 100% 동일
- 사라질 때는 **즉시 사라짐이 아니라 서서히 페이드아웃**(투명도 감소)
- 모드가 켜져 있는 동안 **화면 구석에 작은 표시**를 띄워 깜박하고 계속 켜둔 채로 강의하는 일을 방지

## 현재 아키텍처 관련 사실

- `Models/Stroke.swift`에 `Stroke` 구조체(points/startPoint/endPoint/color/lineWidth/shapeType/isHighlighter)가 이미 정의되어 있으나, 실제 프로덕션 코드(`DrawingCanvasView`)에서는 전혀 쓰이지 않고 테스트에서만 사용됨 — 이번 기능에서 실사용으로 전환한다.
- 현재 `DrawingCanvasView`는 `mouseUp`에서 확정된 획을 즉시 `finishedLayer`라는 단일 `CGImage` 비트맵에 합성(rasterize)해버린다 (`compositeStrokeOntoFinished`). 한 번 합성되면 개별 획 단위로 되돌리거나 투명도를 바꿀 방법이 없다.
- `wantsLayer = false`로 설정되어 있고 전부 `draw(_:)` 기반 렌더링을 사용한다 (CALayer 트리 없음).
- `ShapeRenderer`(line/rectangle/ellipse/arrow), `FreehandRenderer`(Catmull-Rom 스무딩), `HighlighterRenderer`(멀티플라이 블렌드) — 획 경로 생성 로직이 이미 잘 분리되어 있어 재사용 가능.
- `Timer.scheduledTimer` 패턴은 `BreakTimerWindowController`에서 1초 간격 카운트다운으로 이미 사용 중.

## 채택 접근: 벡터 기반 살아있는 획 목록 + 주기적 재렌더링

검토한 대안:
- **(B) 매 프레임 전체 비트맵 재합성**: 기존 스타일과 비슷하지만, 애니메이션 틱마다(초당 ~30회) 화면 전체 크기 비트맵을 다시 그려야 해서 CPU/배터리 부담이 큼. 기각.
- **(C) CALayer + CABasicAnimation**: 애니메이션 자체는 매끄럽지만 `wantsLayer = false`로 의도적으로 설계된 렌더링 모델을 크게 바꿔야 하고, 하이라이터의 `.multiply` 블렌드를 CALayer 컴포지팅으로 재현하기 까다로움. 기각.
- **(A, 채택) 벡터 기반**: `Stroke`를 실제로 사용해 살아있는 획을 배열로 보관하고, 짧은 반복 타이머로 경과 시간 기반 투명도를 계산해 기존 렌더러로 다시 그린다. 화면에 동시에 떠 있는 획이 소수이므로 가볍고, 기존 렌더링 모델과 렌더러들을 그대로 재사용한다.

## 상세 설계

### 1. 토글 (모드 on/off)

- `DrawingState`에 `var isVanishingPenEnabled: Bool = false` 추가.
  - 앱 재시작/세션마다 초기화 (영구 저장 안 함) — `activeTool`(스팟라이트 선택)과 동일한 휘발성 패턴.
- `DrawingCanvasView.keyDown`에 새 케이스 추가: 문자 `"V"` → `drawingState.isVanishingPenEnabled.toggle()`, `setNeedsDisplay(bounds)`.
  - 기존 사용 키(R/G/B/O/Y/P 색상, E 클리어, W 화이트보드, K 블랙보드, T 텍스트, S 스팟라이트, Tab 타원, Space, ⌘Z/⌘C/⌘S, 방향키)와 충돌 없음 확인됨.
- 적용 범위: 프리핸드·라인·사각형·타원·화살표(하이라이터 포함) 획. **텍스트 모드와 스팟라이트는 영향받지 않음** (요구사항에 없었고, 각각 별도 커밋 경로를 가짐).

### 2. 데이터 모델

`Models/Stroke.swift`에 필드 추가:

```swift
struct Stroke {
    let id: UUID
    var points: [CGPoint]
    var startPoint: CGPoint
    var endPoint: CGPoint
    var color: NSColor
    var lineWidth: CGFloat
    var shapeType: ShapeType
    var isHighlighter: Bool
    var createdAt: CFTimeInterval   // CACurrentMediaTime() 기준
}
```

- `id`는 배열에서 제거 시 식별용.
- `createdAt`은 `mouseUp` 시점의 `CACurrentMediaTime()`.

### 3. 페이드 타이밍 모델

- 설정값 하나만 사용: **"사라지는 시간" (`vanishingPenLifetime`, 초 단위, 기본 3.0초, 슬라이더 범위 1.0~8.0초, 0.5초 단위)**.
- 페이드는 **그려진 즉시 시작**해서 `lifetime` 동안 선형으로 투명도 1.0 → 0.0으로 감소한다 (별도의 "유지 후 페이드 시작" 딜레이는 두지 않음 — 현재 요구사항에는 없으므로 YAGNI. 추후 필요해지면 별도 설정으로 추가 가능).
- `age = now - stroke.createdAt`, `fadeAlpha = max(0, 1 - age / lifetime)`. `fadeAlpha <= 0`이 되면 배열에서 제거.

### 4. 렌더링 & 애니메이션 루프

`DrawingCanvasView`에 추가:

```swift
private var vanishingStrokes: [Stroke] = []
private var vanishingTimer: Timer?
```

- **획 확정 시** (`mouseUp`, `drawingState.isVanishingPenEnabled == true`, 텍스트/스팟라이트 아님): 기존처럼 `compositeStrokeOntoFinished`로 `finishedLayer`에 굽는 대신, 그려진 정보를 `Stroke`로 만들어 `vanishingStrokes.append(...)`. `finishedLayer`는 건드리지 않음. Undo 스냅샷도 푸시하지 않음 (§6 참고).
- **타이머**: `vanishingStrokes`가 비어있다가 처음 추가되는 순간 시작, 배열이 다시 비면 정지 (유휴 시 CPU/배터리 낭비 방지).
  - `Timer.scheduledTimer(withTimeInterval: 1.0/30.0, repeats: true) { ... }`, 생성 직후 `RunLoop.main.add(timer, forMode: .common)`으로 등록해 마우스 드래그 트래킹 중에도 계속 동작하도록 함.
  - 틱마다: 각 stroke의 `fadeAlpha` 계산 → `fadeAlpha <= 0`인 것 제거 → `setNeedsDisplay(bounds)`. 배열이 비면 타이머 무효화 후 `nil`.
- **`draw(_:)` 렌더링 순서** (기존 순서에 삽입):
  1. 배경
  2. 스팟라이트 마스크
  3. `finishedLayer`
  4. **(신규) `vanishingStrokes` — 경과시간 기반 알파로 순회 렌더링**
  5. `previewLayer` (드래그 중 프리뷰)
  6. `activeFreehand`
  - 각 stroke는 `shapeType`에 따라 `ShapeRenderer`/`FreehandRenderer`로 경로 생성 후, `color.withAlphaComponent(baseAlpha * fadeAlpha)`로 스트로크. 하이라이터 획은 기존과 동일하게 `HighlighterRenderer.applyHighlighterStyle` + 멀티플라이 블렌드 모드를 적용하되, 곱해지는 알파에 `fadeAlpha`를 반영.

### 5. 설정 UI

- `Settings.Keys.vanishingPenLifetime = "drawVanishingPenLifetime"` 추가.
- `registerDefaults()`에 `Keys.vanishingPenLifetime: 3.0` 추가.
- `var vanishingPenLifetime: TimeInterval { get/set }` computed property 추가 (기존 `spotlightDarkness` 패턴과 동일).
- `resetToDefaults()`의 `allKeys` 목록에 추가.
- `DrawTab.swift`에 새 `Section("Vanishing Pen")` 추가, 기존 슬라이더와 동일한 스타일:
  ```swift
  @AppStorage(Settings.Keys.vanishingPenLifetime) private var vanishingPenLifetime: Double = 3.0
  ...
  HStack {
      Text("Fade Duration")
      Slider(value: $vanishingPenLifetime, in: 1.0...8.0, step: 0.5)
      Text(String(format: "%.1fs", vanishingPenLifetime))
          .frame(width: 40, alignment: .trailing)
          .monospacedDigit()
  }
  ```

### 6. 기존 기능과의 상호작용

- **Undo (⌘Z)**: vanishing 획은 undo 스택에 올리지 않는다 — 애초에 곧 자연 소멸되는 임시 주석이므로. `performUndo()`는 기존처럼 `finishedLayer`/배경모드/스팟라이트만 되돌린다. (vanishing 모드가 켜진 동안 그린 획을 되돌리고 싶다면, 사라지길 기다리거나 아래 Clear All을 쓰면 됨.)
- **Clear All ("E" 키)**: 기존 `finishedLayer = nil`에 더해 `vanishingStrokes.removeAll()`도 수행. 타이머가 돌고 있었다면 다음 틱에서 빈 배열을 감지해 정지.
- **복사/저장 (⌘C/⌘S, `renderFinalImage()`)**: 내보내는 시점에 아직 다 사라지지 않은 vanishing 획을 **그 시점의 투명도 그대로 포함**해서 렌더링 (화면에 보이는 그대로 저장 — WYSIWYG). `finishedLayer`를 그린 직후, 기존 draw(_:) 4단계와 동일한 방식으로 순회 렌더링을 추가.
- **화이트보드/블랙보드 전환, 스팟라이트**: 서로 독립적이므로 영향 없음.
- **커서**: Vanishing Pen 모드는 그리기 방식(사라짐 여부)만 바꾸므로 기존 펜/크로스헤어 커서 로직은 그대로 유지.

### 7. 화면 표시 (HUD)

- Vanishing Pen이 켜져 있는 동안, 화면 한쪽 구석(예: 우측 하단)에 작은 텍스트/아이콘 "Vanishing Pen"을 표시.
- `draw(_:)` 마지막 단계에서 `isVanishingPenEnabled == true`일 때만 `NSAttributedString`으로 작게 그림 (다른 UI 텍스트 렌더링, 예: `BreakTimerView`의 텍스트 드로잉 방식을 참고).
- 꺼지면 즉시 사라짐 (자체 페이드 없음 — 단순 on/off 표시).

## 테스트 관점

- 기존 `StrokeTests.swift`, `DrawingStateTests.swift`, `SettingsTests.swift`와 동일한 스타일로:
  - `Stroke`에 `createdAt`/`id` 필드가 추가된 후에도 기존 초기화 테스트가 깨지지 않는지 (기본값 처리).
  - 알파 계산 로직(`age/lifetime` → `fadeAlpha`)은 뷰 로직과 분리된 순수 함수로 빼서(`VanishingPenFader` 같은 enum/struct) 단위 테스트 가능하게 한다 — 실제 구현 계획(writing-plans) 단계에서 구체화.
  - `Settings.vanishingPenLifetime` get/set, 기본값, `resetToDefaults()` 반영 여부.

## 범위 밖 (Out of Scope)

- 유지 후 페이드 시작하는 "hold" 딜레이 (요청 없음).
- 텍스트/스팟라이트에 대한 자동 소멸.
- Vanishing Pen on/off 상태의 영구 저장(재시작 후 유지) — 항상 꺼진 채로 시작.
- 개별 획마다 다른 소멸 시간 설정 (전역 설정 하나만 사용).

## 작업 위치

- Fork: https://github.com/workcalmkite-hue/ZoomacIt (origin)
- Upstream: https://github.com/07JP27/ZoomacIt (추후 원본 업데이트 병합용)
- 로컬 클론: `~/Desktop/ZoomacIt`
