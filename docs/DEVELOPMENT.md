# 개발 메모 (ShoulderTransition)

> 사용자용 안내는 저장소 루트의 [README.md](../README.md). 이 문서는 디컴파일로 확인한 게임 내부 구조, 테스트 기록, 빌드·업로드 절차 등 개발자용 메모다.

플레이어 오리가 총을 **왼쪽(왼손)** 으로 들고, 탄도 **왼쪽 총구**에서 나가게 하는 클라이언트 비주얼 모드.

## 동작 원리 (게임 코드 디컴파일로 확인, v2.3.30 기준)

- 총은 `CharacterModel.RightHandSocket` 트랜스폼의 자식으로 붙고(`ItemAgentHolder.ChangeHoldItem`),
  총구는 총 프리팹 안의 `Muzzle` 소켓이다(`ItemAgent_Gun.muzzle`). 탄은 `muzzle.position`에서 생성되고
  방향은 `조준점 - muzzle.position`이라 **소켓만 거울상으로 옮기면 탄도 따라온다.** 별도 발사 지점 패치 불필요.
- `CharacterModel.LateUpdate()` → `SyncRightHandRotation()` 이 매 프레임 소켓의 월드 회전을 조준 방향으로 덮어쓴다.
- 로컬 플레이어 판별: `CharacterMainControl.Main` (= `LevelManager.Instance.MainCharacter`). 코옵 원격 플레이어/NPC는 제외.

## 두 가지 모드 (옵션 창의 Mode 드롭다운 또는 `config.json`의 `mode`로 전환)

| mode | 방식 | Harmony | 결과 |
|---|---|---|---|
| `model` (기본) | `CharacterModel.transform.localScale.x = -1` | 불필요 | 날개·총·총구·장비 모두 거울상 (완전한 왼손잡이 룩) |
| `socket` | `CharacterModel.LateUpdate` Prefix(원복)/Postfix(미러) | 필요 | 몸은 그대로, 손 소켓(총)만 모델 루트 기준 YZ 평면 대칭 |

## 테스트 결과 (2026-10-30, v0.1.0, 게임 2.3.30, 벙커에서 UZI)

| 상태 | 발사 시 총구 위치 (modelRoot 로컬 x) | 실제 생성된 총알(`BulletSMG(Clone)`) 위치 | 스냅샷 |
|---|---|---|---|
| model ON | **-0.29 (LEFT)** | **-0.29 (LEFT)**, world x -4.44 | `screenshots/model_mirror_on.png` — 왼손에 총, 손도 따라감, 가면 무늬 좌우 반전 |
| socket ON | **-0.29 (LEFT)** | **-0.29 (LEFT)** | `screenshots/socket_mirror_on.png` — 총만 왼쪽, 가면 그대로, 손은 오른쪽에 남아 총이 떠 보임 |
| OFF | +0.29 (RIGHT) | +0.29 (RIGHT), world x -4.91 | `screenshots/mirror_off.png` |

총알 위치는 발사 이벤트 시점에 `ItemAgent_Gun.projInst`(방금 생성된 투사체)의 transform 을 직접 읽은 값. 진행 방향(dir local z≈+0.98)은 조준 방향 그대로.

캐릭터 리그: `Armature/Root/Pelvis/Spine.001~004/UpperArm.R/Elbow.R/ForeArm.R/Hand.R/Hand.Soket.R/RightHandSocket` (전체 계층은 `screenshots/hierarchy_dump.txt`).
model 모드는 날개(손)까지 거울상이라 완전한 왼손잡이 룩, socket 모드는 얼굴/장비 비대칭을 유지하고 싶을 때.

## 키 / 설정

설정은 두 곳에서 바꿀 수 있고 서로 동기화된다.
1. **게임 옵션 창** — SlimeNull.DuckovModSettings(워크샵 3789464208)가 설치돼 있으면 설정 → 마지막 "Mod Settings" 탭 → ShoulderTransition. 모드가 그 dll을 참조하지 않고, `ModBehaviour.config`(public, [Serializable])를 리플렉션으로 스캔해 UI를 만든다. 없으면 아무 일도 안 일어난다.
2. **config.json** (dll 옆 자동 생성, enum은 이름으로 저장)

| 항목 | 기본값 | 설명 |
|---|---|---|
| `mirror` | true | 왼손 사격 On/Off. 토글 키로도 전환 |
| `mode` | Model | Model: 오리 전체 거울상(손도 따라감) / Socket: 총만 왼쪽(Harmony) |
| `toggleKey` | **LeftAlt** | Unity KeyCode 이름 |
| `modeKey` | None | Model/Socket 전환 핫키(선택). 기본 미할당, 평소엔 옵션 창 드롭다운 사용 |
| `toggleSound` | GunReload | 전환 효과음. 아래 표 |
| `socketMirrorMesh` | false | Socket 모드에서 총 메쉬도 거울상 |
| `socketMirrorMelee` | true | Socket 모드에서 근접무기 소켓도 미러 |
| `dumpKey` / `snapKey` / `fmodDumpKey` | F9 / F10 / F11 | 디버그: 계층 덤프 / 정면 근접 스냅샷 `snap_N.png` / FMOD 이벤트 목록 `fmod_events.txt` |

### 전환 효과음 (게임 내장 FMOD 이벤트 재활용, 외부 리소스 없음)

| 값 | 이벤트 | 느낌 |
|---|---|---|
| GunReload | `SFX/Combat/Gun/Reload/{든 총의 reloadKey}_end` (예: `mag_smg_end`, `mag_rifle_end`, `single_shotgun_end`), 총이 없으면 unload | 총기별 장전 마무리 철컥 |
| GunUnload | `SFX/Combat/Gun/unload` | 탄창 빼기 철컥 |
| EquipGunLight / EquipGunHeavy | `SFX/Item/equip_gun_light` / `equip_gun_heavy` | 총 장비 슥슥 |
| EquipDefault | `SFX/Item/equip_default` | 장비 착용 슥슥 |
| UiClick | `UI/click` | 클릭음 |
| None | 없음 | |

전체 373개 이벤트 목록은 게임 내 F11로 `fmod_events.txt`에 덤프된다(뱅크가 암호화돼 파일에서 직접 못 읽음). 재생은 `Duckov.AudioManager.Post(eventPath[, gameObject])`.

## 코옵 동기화 (`coopSync`, 기본 켜짐)

**상대가 토글하면 내 화면에서 그 사람의 오리가 바뀐다** (내 토글은 남들이 나를 보는 모습에만 영향). 구현은 `CoopSync.cs`.

```
클라이언트 ──(내 상태 mirror/mode)──▶ 호스트      채널 "ShoulderTransition.Hand"
호스트     ──(전원 상태 표 id→상태)──▶ 모든 클라이언트  (호스트 자신 포함, 새 접속자에게도 전송)
```
- id = 호스트가 부여하는 플레이어 id(`NetService.GetPlayerId`). 코옵 모드가 원격 캐릭터를 만들 때 `ModApiEvents.PlayerSpawned(cmc, id, isLocal:false)`로 같은 id를 알려주므로 그걸로 캐릭터를 찾아 **그 캐릭터만** 뒤집는다(Model: 스케일 반전, Socket: Harmony 패치의 ExtraModels).
- 보내는 시점: 내 캐릭터 스폰(네트워크 시작)·토글·모드 변경·접속자 입장. 나갈 때(모드 비활성화)는 오른손으로 되돌리라고 알림.
- 조건: **상대도 이 모드 필요, 중계를 위해 호스트에도 필요.** 호스트에 없으면 클라이언트끼리는 동기화 안 됨(코옵 모드는 모드 메시지를 자동 중계하지 않음).
- 코옵 모드(`EscapeFromDuckovModApi.dll`, `EscapeFromDuckovCoopMod.dll`, `LiteNetLib.dll`)는 컴파일 참조만 하고, 런타임엔 어셈블리 이름으로 존재를 확인한 뒤에만 `CoopSync` 메서드를 호출하므로 코옵 모드가 없어도 동작한다.
- 테스트(2026-10-30, 혼자 LAN 호스트): 네트워크 시작 → `coop host → 0 client(s): table {Host:9050:L/Model}`, Alt 토글마다 표 갱신 방송, NPC에 원격 미러 적용/해제 확인 (`screenshots/coop_host_test.log`). **실제 2인 접속 테스트는 아직 안 함.**
- 디버그: 옵션의 "Remote-mirror test key"를 지정하면 그 키로 가까운 NPC에 원격 미러 미리보기, Ctrl+그 키로 코옵 패널 없이 LAN 호스트 시작/정지.

## 빌드

```
# csproj 의 DuckovPath / HarmonyDir 확인 후
dotnet build -c Release ShoulderTransition/ShoulderTransition.csproj
```
빌드하면 `Duckov_Data\Mods\ShoulderTransition\` 에 dll + 0Harmony.dll + info.ini + preview.png 가 복사된다.

## 창작마당

**등록 완료 (2026-10-30 17:57)** — Published File ID **3830017440**, https://steamcommunity.com/sharedfiles/filedetails/?id=3830017440 (공개, 2.66MB, 태그 Visual Enhancements / Quality of Life / Mod).
`ReleaseExample/ShoulderTransition/info.ini` 에 `publishedFileId = 3830017440` 이 들어 있고 빌드 때 Mods 폴더로 복사되므로, **이 줄을 지우면 다음 업로드가 새 항목을 만든다.** 게임이 info.ini 를 다시 쓸 때 description 은 Regex.Escape 형식(`\ ` 등)으로 저장되는데 읽을 때 Unescape 하므로 그대로 두면 된다.

### 업데이트 절차
1. 코드 수정 → `dotnet build -c Release` (Mods 폴더에 배포됨). `version` 올리기.
2. 게임 실행 → Mods 메뉴 → 맨 아래 ShoulderTransition 항목의 ↑(업로드) 버튼 → 패널에서 "업로드". 변경 노트 입력란은 없음(코드상 "Unknown"으로 전송) → Steam 웹페이지의 Change Notes 에서 수정.
3. 업로드 직전 Mods 폴더의 `config.json`, `ShoulderTransition.log` 등 런타임 파일을 지울 것(폴더 전체가 올라감). 게임이 켜진 뒤에 지워도 된다(파일을 잠그지 않음).
4. 업로드 후 Mods 폴더의 info.ini 를 `ReleaseExample/` 로 되복사(동일해야 함).

### Steam 웹페이지에서 할 것
- 설명을 `WORKSHOP_DESCRIPTION.md` 의 BBCode 버전으로 교체(최초 업로드 설명은 한 줄 평문).
- 스크린샷 추가: `screenshots/model_mirror_on.png`, `mirror_off.png`, `socket_mirror_on.png`.
- Change Notes 작성.

게임 Mods 메뉴에서 로컬 모드 항목의 Upload 버튼으로 올리며, 게임이 info.ini 를 다시 써서 `publishedFileId` 를 기록한다
(`ModManager.WriteModInfoINI`). 업로드 전에 Mods 폴더의 `config.json`, `ShoulderTransition.log`, `hierarchy_dump.txt`, `fmod_events.txt`, `snap_*.png` 는 지울 것(폴더 전체가 올라감).
설명(description)은 **최초 업로드 때만** 전송되고 이후엔 Steam 웹페이지에서 수정해야 한다. info.ini 의 tags 는 게임이 `Mod` 태그로 덮어쓰므로 웹에서 다시 지정.

## 의존성

- socket 모드만 Harmony 사용. 워크샵 HarmonyLib(3589088839, 2.4.1)과 같은 `0Harmony.dll`을 동봉하므로 단독으로도 동작.
- 설정 UI는 SlimeNull.DuckovModSettings(3789464208) **선택 사항**. 없어도 config.json으로 동작.
- 효과음은 게임 내장 FMOD 이벤트만 사용(FMODUnity.dll 은 게임 동봉, 모드에 포함 안 함).
