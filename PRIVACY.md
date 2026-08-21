# Privacy Policy — DeskCue

**Last updated: 2026-08-20**

DeskCue ("the app") is a Windows tray utility that shows the current virtual
desktop as an on-screen overlay and switches desktops with hotkeys.

## Summary

**DeskCue does not collect, store, or transmit any personal information.**
There are no user accounts, no sign-in, no telemetry, no analytics, no
advertising, and no third-party tracking of any kind. Nothing you do in the app
is sent to the developer.

## What the app stores, and where

Everything the app writes stays on your own PC and is never uploaded:

| Data | Location | Purpose |
|---|---|---|
| Your settings (overlay position, opacity, font size, colors, hotkey bindings) | `%AppData%\DeskCue\config.json` | Restore your configuration on the next launch |
| A rolling local debug log (timestamps and short status messages) | `%AppData%\DeskCue\debug.log` | Troubleshooting on your machine only |

Neither file is designed to contain personal information. Uninstalling the app
and deleting the `%AppData%\DeskCue` folder removes both.

To draw the overlay, the app reads the current virtual desktop number, the total
count, and the desktop name from the Windows APIs and registry. This information
is used only to render the overlay in memory and is never stored or transmitted.

## Network connections

**The Microsoft Store version of DeskCue makes no network connections at all.**
The package declares no internet capability, and the update check is disabled
because the Store handles updates.

Builds distributed outside the Microsoft Store (the installer and portable
`.exe` from GitHub Releases) check for a new version by requesting the public
GitHub Releases API at `https://api.github.com`. That request contains only the
app name and version number in the `User-Agent` header — no personal data, no
identifier, and no information about your PC or usage. If you choose to install
an update, the installer file is downloaded from GitHub. As with any web
request, GitHub as the host may see your IP address; its handling of that is
covered by the [GitHub Privacy Statement](https://docs.github.com/site-policy/privacy-policies/github-privacy-statement).

The "Support development" menu entry opens a GitHub Sponsors page in your normal
web browser. The app itself sends nothing and never handles payment details.

## Data sharing

There is nothing to share. No data is sold, rented, shared with third parties,
or used for profiling or advertising.

## Children's privacy

The app collects no data from anyone, including children under 13.

## Permissions

The app runs as a full-trust desktop app so it can register global hotkeys,
read virtual-desktop state, and synthesize the keystrokes used to switch
desktops. These permissions are used solely for those features.

## Changes to this policy

Any change will be published on this page with an updated date above.

## Contact

Questions about this policy or the app:
<https://github.com/MinaryHub/Virtual-Desktop-Indicator/issues>

---

# 개인정보 처리방침 — DeskCue

**최종 업데이트: 2026-08-20**

DeskCue(이하 "본 앱")는 현재 Windows 가상 데스크톱을 화면에 오버레이로 표시하고
단축키로 데스크톱을 전환하는 Windows 트레이 유틸리티입니다.

## 요약

**본 앱은 어떠한 개인 정보도 수집·저장·전송하지 않습니다.** 사용자 계정,
로그인, 원격 분석(텔레메트리), 분석 도구, 광고, 제3자 추적 기능이 전혀 없습니다.
앱 사용 내역이 개발자에게 전송되는 일은 없습니다.

## 저장되는 정보와 위치

앱이 기록하는 모든 정보는 사용자 PC 내에만 남으며 외부로 전송되지 않습니다.

| 데이터 | 위치 | 목적 |
|---|---|---|
| 설정값(오버레이 위치, 불투명도, 글자 크기, 색상, 단축키) | `%AppData%\DeskCue\config.json` | 다음 실행 시 설정 복원 |
| 로컬 디버그 로그(시간과 짧은 상태 메시지, 용량 제한 순환) | `%AppData%\DeskCue\debug.log` | 사용자 PC에서의 문제 진단 |

두 파일 모두 개인 정보를 담도록 설계되지 않았습니다. 앱을 제거하고
`%AppData%\DeskCue` 폴더를 삭제하면 함께 사라집니다.

오버레이 표시를 위해 현재 데스크톱 번호, 전체 개수, 데스크톱 이름을 Windows
API와 레지스트리에서 읽습니다. 이 정보는 메모리 상에서 오버레이를 그리는 데만
사용되며 저장되거나 전송되지 않습니다.

## 네트워크 연결

**Microsoft Store 버전은 네트워크 연결을 전혀 수행하지 않습니다.** 패키지에
인터넷 기능(capability)이 선언되어 있지 않으며, 업데이트는 스토어가 담당하므로
앱 내 업데이트 확인 기능이 비활성화됩니다.

Microsoft Store 외부로 배포되는 빌드(GitHub Releases의 설치 프로그램 및 포터블
`.exe`)는 새 버전 확인을 위해 공개 GitHub Releases API(`https://api.github.com`)에
요청을 보냅니다. 이 요청에는 `User-Agent` 헤더의 앱 이름과 버전 번호만 포함되며,
개인 정보나 식별자, PC·사용 내역 정보는 포함되지 않습니다. 사용자가 업데이트
설치를 선택하면 설치 파일을 GitHub에서 내려받습니다. 모든 웹 요청과 마찬가지로
호스트인 GitHub은 IP 주소를 확인할 수 있으며, 이에 대한 처리는
[GitHub 개인정보 보호 정책](https://docs.github.com/site-policy/privacy-policies/github-privacy-statement)을
따릅니다.

"Support development" 메뉴는 기본 웹 브라우저에서 GitHub Sponsors 페이지를 열
뿐이며, 앱이 직접 전송하는 정보나 결제 정보를 다루는 부분은 없습니다.

## 정보 제공 및 공유

수집하는 정보가 없으므로 판매·대여·제3자 제공, 프로파일링이나 광고 목적의
이용 역시 없습니다.

## 아동의 개인정보

본 앱은 만 13세 미만 아동을 포함한 누구로부터도 데이터를 수집하지 않습니다.

## 권한

전역 단축키 등록, 가상 데스크톱 상태 조회, 데스크톱 전환을 위한 키 입력 합성을
위해 완전 신뢰(full-trust) 데스크톱 앱으로 실행됩니다. 해당 권한은 이 기능
구현에만 사용됩니다.

## 방침 변경

변경 사항은 위 날짜와 함께 이 페이지에 게시됩니다.

## 문의

본 방침 또는 앱에 대한 문의:
<https://github.com/MinaryHub/Virtual-Desktop-Indicator/issues>
