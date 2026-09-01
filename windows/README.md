# ZoomacIt for Windows

맥 전용 앱 [ZoomacIt](https://github.com/workcalmkite-hue/ZoomacIt)을 윈도우로 옮긴 트레이 앱입니다.
확대·그리기·타이머 같은 기본 기능부터, 맥 버전에만 있던 커스텀 기능 4개까지 **이 앱 하나로 통일**했습니다.
(예전에는 기본 기능을 Sysinternals ZoomIt이 담당했지만, 두 앱이 겹쳐 뜨면 헷갈려서 하나로 합쳤습니다.)

- 만든 이유: 중학교 기술 수업 중 화면을 띄워놓고 쓰기 위함
- 스택: WPF (.NET 8), 트레이 상주

## 기능과 단축키

ZoomIt과 같은 배치라 손에 익은 그대로 쓰면 됩니다.

| 단축키 | 기능 | 비고 |
|---|---|---|
| `Ctrl+1` | 확대 (정지) | 화면을 얼려서 확대 |
| `Ctrl+2` | 그리기 | 일반 펜 |
| `Ctrl+3` | 브레이크 타이머 | 원형 위젯 |
| `Ctrl+4` | 화면 캡처 | 드래그한 영역을 클립보드 + PNG 파일로 |
| `Ctrl+8` | 확대 (라이브) | 화면이 계속 움직이는 상태로 확대 |
| `Ctrl+Shift+D` | 그리기 (지워지는 펜) | 그린 선이 3초 뒤 사라짐 |
| `Ctrl+Shift+S` | 마우스 스포트라이트 | 커서 주변만 밝게 + 클릭 링 |
| `Ctrl+M` | 화면 위 메모 | 노란 포스트잇 |

단축키는 설정 파일에서 바꿀 수 있습니다. 다른 프로그램이 이미 쓰는 단축키는 등록에
실패하고 트레이 알림으로 알려줍니다 — 그 경우에도 트레이 메뉴로는 그대로 쓸 수 있습니다.

### 확대 화면 안에서 (`Ctrl+1` / `Ctrl+8`)

| 조작 | 동작 |
|---|---|
| 마우스 이동 | 확대해서 볼 지점 이동 |
| 휠 / `↑` `↓` | 배율 (1배 ~ 8배, 0.2씩) |
| `D` | 확대한 화면 위에 바로 그리기 (정지 확대만) |
| `Esc` / 오른쪽 클릭 | 닫기 |

### 그리기 오버레이 안에서 (`Ctrl+2` / `Ctrl+Shift+D`)

| 키 | 동작 | 키 | 동작 |
|---|---|---|---|
| 드래그 | 자유선 | `Shift`+드래그 | 직선 |
| `Ctrl`+드래그 | 사각형 | `Ctrl+Shift`+드래그 | 화살표 |
| `Tab`(누른 채)+드래그 | 타원 | 휠 | 펜 굵기 |
| `V` | 지워짐 ↔ 영구 전환 | `H` | 형광펜 |
| `R G B O Y P` | 빨강·초록·파랑·주황·노랑·분홍 | `E` | 전부 지우기 |
| `W` / `K` / `T` | 흰 배경 / 검은 배경 / 투명 | `Ctrl+Z` | 실행취소(영구 획) |
| `Esc` | 오버레이 닫기 | | |

화살표는 **드래그를 시작한 쪽이 화살촉**입니다 (ZoomIt과 같은 방식, 원본 그대로).

오른쪽 아래 배지가 지금 "지워지는 펜"인지 "일반 펜"인지 보여줍니다 —
모드를 켜둔 걸 깜박하고 수업하는 사고를 막기 위한 원본의 장치입니다.

### 화면 캡처 (`Ctrl+4`)

- 화면이 얼면서 어두워지고, **드래그로 영역을 고르면** 끝입니다 (크기가 실시간으로 표시됩니다).
- 고른 영역은 **클립보드에 복사**되고 동시에 **PNG 파일로 저장**됩니다 —
  기본 위치는 "내 그림 > ZoomacIt > 캡처_날짜_시각.png" (설정의 `SnipFolder`로 변경 가능).
- `Esc` 또는 오른쪽 클릭으로 취소.

### 브레이크 타이머 위젯

- 10:00 부터 시작하고 **멈춘 상태로 뜹니다** — 재생(▶)을 눌러야 갑니다.
- 원을 드래그해 이동, **휠로 크기 조절**(70~220), 위치·크기는 다음 실행 때 복원됩니다.
- 마우스를 올리면 `−` `▶` `＋` `✕` 버튼이 나타납니다 (멈춰 있을 땐 항상 보임).
- 0이 되면 링이 빨갛게 깜빡이고 숫자는 경과 시간 `(1:15)` 카운트업으로 바뀝니다.
- **다른 앱의 포커스를 뺏지 않습니다** — PPT를 넘기면서 띄워둘 수 있습니다.

### 화면 위 메모

- `Ctrl+M`을 누를 때마다 한 장씩, 조금씩 어긋나게(계단식) 생깁니다.
- 위쪽 진한 띠를 잡고 드래그, 오른쪽 아래 빗금 모서리로 크기 조절.
- `Ctrl+휠` 또는 `Ctrl` + `+` / `−` / `0` 으로 글자 크기 (뒷자리에서도 보이게 키우는 용도).
- `Ctrl+A/C/V/X/Z`는 한글 자판에서도 그대로 동작합니다(WPF가 물리 키 기준으로 처리).
- `✕`를 눌러야 닫힙니다. 마지막에 쓴 크기·글자 크기를 다음 메모가 물려받습니다.

## 설치·실행

```powershell
# 빌드 (개발용)
dotnet build

# 배포용 단일 exe (.NET 설치 없이도 실행됨, 약 63MB)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

만들어진 exe: `bin\Release\net8.0-windows\win-x64\publish\ZoomacItWin.exe`

**윈도우 시작 시 자동 실행**하려면 `Win+R` → `shell:startup` → 열린 폴더에 위 exe의
바로 가기를 넣으면 됩니다.

## 설정

트레이 아이콘 우클릭 → **설정 파일 열기** (`%APPDATA%\ZoomacItWin\settings.json`).
바꾼 뒤 앱을 재시작하면 적용됩니다.

| 항목 | 기본값 | 설명 |
|---|---|---|
| `VanishingPenLifetime` | `3.0` | 획이 사라지기까지의 초 |
| `VanishingPenOnByDefault` | `true` | 오버레이를 열 때 지워지는 펜으로 시작할지 |
| `PenWidth` | `5` | 펜 굵기 |
| `BreakTimerDefaultDurationSeconds` | `600` | 타이머 기본 시간(10분) |
| `BreakTimerColor` | `#FF3B30` | 진행 링 색 |
| `BreakTimerPlaySound` | `false` | 만료 시 소리 |
| `MouseSpotlightRadius` | `150` | 스포트라이트 반지름 (휠로 조절하면 저장됨) |
| `MouseSpotlightDim` | `0.5` | 바깥 어두움 (0~1) |
| `DefaultZoomLevel` | `2.0` | 확대를 켰을 때의 초기 배율 |
| `SnipFolder` | (비움) | 캡처 저장 폴더. 비우면 "내 그림 > ZoomacIt" |
| `Hotkey*` | 위 표 참고 | `"Ctrl+1"`, `"Ctrl+Shift+D"` 형식 |

문제가 생기면 `%APPDATA%\ZoomacItWin\log.txt` 에 단축키 등록 결과와 예외가 남습니다.

## 원본(맥)과 다른 점

의도적으로 다르게 만든 부분만 적었습니다.

1. **지워지는 펜에 전용 단축키가 하나 더 있습니다.** 맥에서는 Draw 모드 안의 `V` 토글뿐이지만,
   여기서는 `Ctrl+Shift+D`로 처음부터 지워지는 펜 상태로 열 수 있습니다(같은 오버레이이고,
   안에서 `V`로 전환하는 것도 그대로 됩니다).
2. **라이브 줌 창은 화면 캡처에서 제외됩니다.** 자기 화면을 되찍는 무한 거울을 막는
   유일하게 확실한 방법이라서입니다. 교실 프로젝터(화면 복제)에는 정상적으로 보이지만,
   **화면 녹화나 화상수업 공유 화면에는 라이브 줌이 찍히지 않습니다**(정지 확대 `Ctrl+1`은 찍힙니다).
3. **타이머 숫자 뒤에 옅은 어두운 원판을 깔았습니다.** 원본은 가운데를 투명하게 뒀지만,
   흰 배경 슬라이드 위에서 흰 숫자가 거의 안 보여 교실 가독성을 우선했습니다.
4. **메모 크기·글자 크기를 설정 파일에 저장합니다.** 원본은 앱 실행 중에만 기억했습니다.
5. **형광펜은 알파 합성(0.35)으로 근사했습니다.** WPF의 `DrawingContext`에는 원본이 쓰는
   곱셈(multiply) 블렌드가 없습니다.
6. **스포트라이트 크기 조절은 그냥 휠입니다**(원본과 동일). 스포트라이트를 켠 채 웹페이지를
   스크롤하면 원 크기가 같이 바뀝니다 — 맥 원본도 같은 동작입니다.
7. 메모 안에서 `Esc`는 닫기가 아니라 입력 포커스만 해제합니다(판서 내용을 실수로 날리지 않도록).

## 원본 코드와의 대응

순수 계산 로직은 원본 Swift 파일을 거의 그대로 옮겼습니다.

| 원본 (Swift) | 이식 (C#) |
|---|---|
| `Draw/VanishingPenFader.swift` | `Draw/VanishingPenFader.cs` |
| `Overlay/BreakTimerWidgetMetrics.swift` | `Overlay/BreakTimerWidgetMetrics.cs` |
| `Overlay/BreakTimerRingGeometry.swift` | `Overlay/BreakTimerRingGeometry.cs` |
| `Overlay/MouseSpotlightGeometry.swift` | `Overlay/MouseSpotlightGeometry.cs` |
| `Overlay/StickyNote.swift` 의 `StickyNoteMetrics` | `Overlay/StickyNoteMetrics.cs` |
| `Models/BreakTimerState.swift` | `Overlay/BreakTimerState.cs` |
| `Draw/FreehandRenderer` + `ShapeRenderer` | `Draw/StrokeRenderer.cs` |
| `Overlay/ZoomMath.swift` | `Core/ZoomMath.cs` |

## 진단 모드

화면에 아무것도 띄우지 않고 그리기 결과만 PNG로 뽑습니다.

```powershell
ZoomacItWin.exe --render-test C:\temp             # 페이드 단계별 획 + 타이머 링 상태
ZoomacItWin.exe --capture-draw C:\temp\a.png      # 드로잉 오버레이 창 자체를 캡처
ZoomacItWin.exe --capture-livezoom C:\temp\b.png  # 라이브 줌 창 (일반 스크린샷에는 안 찍힘)
```

## 알려진 제한

- 메모 **내용은 저장되지 않습니다** — 앱을 끄면 사라집니다.
- 드로잉 오버레이는 열려 있는 동안 그 모니터의 클릭을 가져갑니다(ZoomIt 드로잉과 동일).
  `Esc`로 닫으면 바로 원래대로 돌아옵니다.
- 오버레이는 **켤 때 커서가 있던 모니터**에 뜹니다. 다른 모니터에서 쓰려면 커서를 옮기고 다시 켜세요.
  (마우스 스포트라이트만 커서를 따라 모니터를 넘어갑니다.)

## PowerToys와의 관계

이 앱으로 통일하면서 PowerToys의 아래 모듈을 껐습니다 (PowerToys 자체는 그대로 두었고,
FancyZones·PowerToys Run 등 나머지는 전부 살아 있습니다).

| 끈 모듈 | 이유 |
|---|---|
| ZoomIt | `Ctrl+1~4`가 이 앱과 정면으로 충돌 |
| Find My Mouse | 왼쪽 `Ctrl` 두 번으로 켜져서 `Ctrl+1~4`를 연달아 누를 때 오작동 + 기능 중복 |
| Mouse Highlighter | 클릭 표시 기능이 이 앱의 스포트라이트 클릭 링과 중복 |

되돌리려면 **PowerToys 설정 → 각 모듈 → 켜기** 하면 됩니다.
설정 파일 백업본: `%LOCALAPPDATA%\Microsoft\PowerToys\settings.json.bak-zoomacit`

ZoomIt을 끄면서 같이 사라진 기능과 대체 방법:

| 잃은 기능 | 대체 |
|---|---|
| 화면 녹화 (`Ctrl+5`) | `Win`+`Alt`+`R` (Xbox Game Bar 녹화) |
| 화면 캡처 (`Ctrl+6`) | **이 앱의 `Ctrl+4` 캡처로 대체** |
| 데모 타이핑 (`Ctrl+7`) | 대체 없음 (수업용으로는 거의 안 쓰는 기능) |
