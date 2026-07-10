# Virtual Desktop Indicator

Windows 가상 데스크톱의 **현재 위치를 화면에 반투명하게 상시 표시**하고,
**사용자 지정 단축키로 특정 데스크톱으로 즉시 이동**하는 트레이 앱입니다.

- 화면 상단(기본값)에 `현재번호 / 전체개수  데스크톱이름` 을 반투명·클릭 통과 오버레이로 표시
- 모든 가상 데스크톱에서 오버레이가 따라다니며 보임
- `Ctrl+Alt+1` ~ `Ctrl+Alt+9` (기본값)로 해당 번호의 데스크톱으로 즉시 이동
- 위치·투명도·글꼴 크기·색상·단축키를 설정 파일로 자유롭게 변경

## 다운로드

최신 설치 파일은 **[Releases 페이지](https://github.com/knoxxr/Virtual-Desktop-Indicator/releases/latest)** 에서 받으세요.
`VirtualDesktopIndicator-Setup-1.0.0.exe` 를 내려받아 실행하면 됩니다. (.NET 설치 불필요)

## 설치 (권장)

설치 파일: **`installer/VirtualDesktopIndicator-Setup-1.0.0.exe`**

더블클릭 → 마법사를 따라가면 설치됩니다.

- **.NET 설치 불필요** — 런타임이 포함된 self-contained 빌드라 대상 PC에 아무것도 미리 깔 필요가 없습니다.
- **관리자 권한 불필요** — 현재 사용자에게만 설치(`%LocalAppData%\Programs\Virtual Desktop Indicator`)됩니다.
- 설치 중 **바탕 화면 바로 가기**(선택)와 **Windows 시작 시 자동 실행**(기본 체크)을 고를 수 있습니다.
- 제거는 **설정 → 앱 → 설치된 앱** 또는 시작 메뉴의 *"Virtual Desktop Indicator 제거"* 로 하며,
  자동 실행 항목도 함께 정리됩니다.

## 요구 사항 / 소스 실행

- Windows 10/11 (64비트)
- 소스에서 개발/실행하려면 [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0):

```powershell
dotnet run -c Release              # 실행
dotnet publish -c Release          # 프레임워크 종속 exe(.NET 런타임 필요)
```

## 설치 파일 다시 빌드하기

[Inno Setup 6](https://jrsoftware.org/isdl.php) 필요 (`winget install JRSoftware.InnoSetup`).

```powershell
# 1) 런타임 포함 self-contained 게시
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-sc
Remove-Item publish-sc\*.pdb -EA SilentlyContinue
# 2) 설치 파일 컴파일 (installer\ 에 Setup.exe 생성)
& "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe" installer.iss
```

설치 마법사 구성은 [installer.iss](installer.iss) 에 있습니다(버전·바로가기·자동 실행 등).

## 트레이 메뉴

작업 표시줄 우측 **VD** 아이콘을 우클릭(또는 더블클릭 → 설정 창):

- **설정...** — 단축키 변경 및 자동 실행을 설정하는 창을 엽니다
- **Windows 시작 시 자동 실행** — 체크하면 로그인 시 자동 실행 (per-user, 관리자 권한 불필요)
- **설정 파일 열기** — `config.json`을 기본 편집기로 엽니다
- **설정 다시 읽기** — 파일을 직접 수정한 뒤 즉시 반영
- **위치** — 오버레이 위치를 빠르게 변경 (TopCenter, TopRight 등)
- **종료**

## 설정 창 (단축키 변경)

트레이 아이콘 더블클릭 또는 **설정...** 메뉴로 엽니다.

- **Windows 시작 시 자동 실행** 체크박스
- **단축키** — 데스크톱 1~9 각각에 대해 변경 버튼을 클릭한 뒤 원하는 조합을 그대로 누르면
  됩니다. 예: 버튼 클릭 → `Ctrl` `Alt` 를 누른 채 `3` → `Ctrl+Alt+3` 으로 지정됨.
  - **Esc** 입력 취소 · **Delete** 해당 단축키 지우기
  - 최소 하나의 수식어(`Ctrl`/`Alt`/`Shift`/`Win`)가 필요하며, 중복 조합은 저장 시 경고합니다
  - **저장**을 누르면 즉시 적용됩니다(재시작 불필요)

> 자동 실행은 레지스트리 `HKCU\...\CurrentVersion\Run` 에 실행 파일 경로를 기록합니다.
> 실행 파일을 다른 위치로 옮겼다면 자동 실행을 껐다 켜서 경로를 갱신하세요.

## 설정 파일

`%APPDATA%\VirtualDesktopIndicator\config.json` (최초 실행 시 기본값으로 생성)

| 항목 | 설명 | 기본값 |
|------|------|--------|
| `Position` | 오버레이 위치: `TopLeft` `TopCenter` `TopRight` `BottomLeft` `BottomCenter` `BottomRight` `Center` | `TopCenter` |
| `MarginX` / `MarginY` | 가장자리로부터의 여백(px) | `0` / `12` |
| `Opacity` | 배경 투명도 (0.05 ~ 1.0) | `0.55` |
| `FontSize` | 번호 글꼴 크기 | `28` |
| `ShowNumber` | 번호 표시 여부 | `true` |
| `ShowCount` | `2 / 5` 형식(전체 개수 포함) 여부 | `true` |
| `ShowName` | 데스크톱 이름 표시 여부 | `true` |
| `Foreground` / `Background` | 글자색 / 배경색 (`#RRGGBB`) | 흰색 / 검정 |
| `CornerRadius` | 모서리 둥글기 | `10` |
| `PollIntervalMs` | 상태 갱신 주기(ms) | `300` |
| `Hotkeys` | `{ "Hotkey": "Ctrl+Alt+1", "Desktop": 1 }` 목록 | 1~9번 |

**단축키 형식**: `수식어+수식어+키`. 수식어는 `Ctrl` `Alt` `Shift` `Win`,
키는 `1`~`0`, `A`~`Z`, `F1`~`F24`, 넘패드 숫자 `Num0`~`Num9`.
예) `Ctrl+Alt+3`, `Ctrl+Alt+F5`, `Ctrl+Win+Num1`. `Desktop`은 1부터 시작하는 데스크톱 번호입니다.
넘패드 숫자(`Num*`)는 **NumLock이 켜져 있어야** 인식됩니다.
다른 프로그램이 이미 쓰는 단축키는 등록에 실패하며, 시작 시 풍선 알림으로 알려줍니다.

> ⚠️ `Win+Shift+숫자`, `Win+숫자` 등 일부 조합은 **Windows가 이미 예약**(작업 표시줄 N번째 앱
> 실행 등)하여 등록에 실패합니다. 그래서 기본값은 충돌 없는 `Ctrl+Alt+숫자`를 사용합니다.
> 등록에 실패한 단축키는 시작 시 풍선 알림으로 알려주며, 설정 창에서 다른 조합으로 바꾸면 됩니다.

### 데스크톱 전환 동작

기본적으로 Windows 내부 API(`IVirtualDesktopManagerInternal.SwitchDesktop`)로 **목표 데스크톱에
곧바로 전환**합니다. 여러 칸 떨어져 있어도 중간 데스크톱을 거치지 않고 즉시 이동합니다.

이 내부 API는 문서화되지 않아 Windows 빌드에 따라 바뀔 수 있습니다. 그래서 사용 전 인터페이스가
정상 연결되는지 검증하고, 실패하면 **자동으로 키 입력 방식(`Win+Ctrl+←/→` 단계 이동)으로 폴백**합니다
(이 경우 한 칸씩 거쳐 가며 먼 데스크톱은 조금 느립니다). 어느 방식이든 이동 결과는 정확합니다.

> 💡 데스크톱 이름은 작업 보기(`Win+Tab`)에서 데스크톱 이름을 더블클릭해 지정할 수 있습니다.

## 동작 원리 (안정성 설계)

가상 데스크톱 관련 API는 상당수가 비공개(undocumented)이고 Windows 빌드마다 인터페이스가 바뀌어
잘 깨집니다. 이 앱은 **빌드 업데이트에 영향받지 않는 방식**만 사용합니다.

- **현재 위치 감지**: 레지스트리를 직접 읽습니다
  (`...\Explorer\VirtualDesktops\VirtualDesktopIDs` 순서 목록 + `CurrentVirtualDesktop`).
  비공개 COM에 의존하지 않아 버전 무관하게 동작합니다.
- **데스크톱 이동**: 내부 COM(`SwitchDesktop`)으로 목표 데스크톱에 직접 전환하며,
  이 인터페이스가 없거나 빌드가 달라지면 `Win+Ctrl+←/→` 키 입력 방식으로 자동 폴백합니다
  (위 "데스크톱 전환 동작" 참고). 폴백 경로는 Windows 기본 단축키라 항상 동작합니다.
- **모든 데스크톱에서 표시**: 문서화된 공개 COM 인터페이스
  `IVirtualDesktopManager.MoveWindowToDesktop` 로 오버레이를 현재 데스크톱으로 옮깁니다
  (Windows 10 1607부터 안정적).

## 구조

```
App.xaml(.cs)                  진입점, 트레이 아이콘/메뉴
OverlayWindow.xaml(.cs)        반투명·클릭통과 오버레이, 폴링·재배치·데스크톱 추적
SettingsWindow.xaml(.cs)       단축키 변경 UI + 자동 실행 체크박스
Services/
  AppConfig.cs                 config.json 로드/저장
  VirtualDesktopRegistry.cs    레지스트리에서 현재 번호/개수/이름 읽기
  DesktopSwitcher.cs           목표 데스크톱으로 이동(내부 COM 우선, 실패 시 키 입력 폴백)
  VirtualDesktopInternal.cs    내부 COM(SwitchDesktop)으로 직접 전환 + vtable 검증
  VirtualDesktopManagerCom.cs  공개 COM(MoveWindowToDesktop) 래퍼
  HotKeyManager.cs             전역 단축키 등록/처리
  StartupManager.cs            Windows 시작 시 자동 실행(HKCU\Run) 토글
```
