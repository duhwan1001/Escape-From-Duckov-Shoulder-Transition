# 창작마당 설명 (BBCode, 복사용)

> 항목: https://steamcommunity.com/sharedfiles/filedetails/?id=3830017440 (ID 3830017440)
> 내용은 README.md 와 같다. 아래 블록 전체를 Steam 페이지 설명란에 붙여 넣으면 된다(한국어 먼저, 영어 다음).
> 소스: GitHub 링크는 저장소 생성 후 아래 `GITHUB_URL` 자리에 채울 것.

```
[h1]ShoulderTransition — 왼손 사격 / Left-handed Duck[/h1]
[b]한국어[/b] 설명이 먼저, [b]English[/b] below.

[hr][/hr]
[h1]한국어[/h1]
오리가 총을 [b]왼손[/b]으로 들고, 총알도 [b]왼쪽 총구[/b]에서 나가게 하는 모드입니다. 모델만 뒤집은 것이 아니라 투사체 생성 위치까지 거울상이 되는 것을 검증했습니다.

[h2]주요 기능[/h2]
[list]
[*][b]왼손 사격[/b] — 총, 총구, 탄 발사 위치가 모두 왼쪽으로. 조준·명중 판정은 그대로.
[*][b]즉시 전환[/b] — Left Alt 한 번으로 왼손 ↔ 오른손. 전환할 때 짧은 효과음(기본: 든 총의 장전 철컥).
[*][b]두 가지 방식[/b]
  Model (기본) — 오리 전체를 거울상으로. 손(날개)이 총을 따라가서 완전한 왼손잡이 룩.
  Socket — 무기 소켓만 거울상. 얼굴 무늬·비대칭 장비는 그대로 두고 총만 왼쪽으로.
[*][b]게임 내 설정 창[/b] — SlimeNull Mod Settings 가 있으면 설정 → Mod Settings → ShoulderTransition 에서 모든 옵션 변경. 없으면 config.json.
[*][b]코옵 동기화[/b] — 같은 모드를 가진 다른 플레이어가 전환하면 내 화면에서도 [i]그 사람의[/i] 오리가 바뀝니다.
[*][b]안전[/b] — 내 캐릭터의 시각 요소만 건드립니다. 세이브·스탯·탄도·네트워크 게임 데이터는 바꾸지 않습니다.
[/list]

[h2]설치[/h2]
구독 → 게임 실행 → 메인 메뉴 Mods 에서 켜기 → 재시작. 필수 의존성 없음(0Harmony.dll 2.4.1 동봉). 워크샵 HarmonyLib 를 같이 쓰면 이 모드를 그 아래에 두세요.

[h2]조작[/h2]
[list]
[*][b]Left Alt[/b] — 왼손 ↔ 오른손 전환 (설정에서 변경 가능)
[*]Model ↔ Socket 전환 — 기본 미할당. 설정 창의 Mode 드롭다운으로 바꾸거나 원하는 키를 지정
[/list]

[h2]옵션[/h2] (설정 창 또는 config.json, 자동 동기화)
[list]
[*]mirror (true) — 왼손 사격 켜기/끄기
[*]mode (Model) — Model 오리 전체 거울상 / Socket 총만
[*]toggleKey (LeftAlt) — 전환 키, Unity KeyCode 이름
[*]modeKey (None) — Model/Socket 전환 핫키(선택)
[*]toggleSound (GunReload) — 전환 효과음: GunReload 든 총의 장전 철컥 / GunUnload 탄창 빼기 / EquipGunLight·EquipGunHeavy 총 장비 슥슥 / EquipDefault 장비 착용 슥슥 / UiClick / None. 모두 게임 내장 사운드.
[*]coopSync (true) — 코옵 동기화
[*]socketMirrorMesh (false), socketMirrorMelee (true) — Socket 방식 세부
[*]dumpKey F9 / snapKey F10 / fmodDumpKey F11 / remoteTestKey None — 디버그용
[/list]

[h2]코옵 동기화[/h2]
[list]
[*]각자 자기 상태만 보내고, 호스트가 작은 표로 만들어 모두에게 전달합니다(늦게 들어온 사람 포함). 받는 쪽은 해당 플레이어의 오리만 뒤집습니다.
[*]내 전환은 다른 사람을 바꾸지 않습니다. 남들이 나를 어떻게 보느냐만 달라집니다.
[*]조건: 상대도 이 모드가 있어야 하고, 중계를 위해 호스트에도 있어야 합니다. 상대에게 모드가 없으면 평소처럼 오른손으로 보일 뿐입니다.
[*]코옵 모드(EscapeFromDuckovCoopMod)가 없어도 모드는 정상 동작합니다.
[/list]

[h2]알려진 한계[/h2]
[list]
[*]Model 방식은 오리 전체가 거울상이라 비대칭 얼굴 무늬·장비도 좌우가 바뀝니다.
[*]Socket 방식은 총만 이동하고 손은 오른쪽에 남아 총이 살짝 떠 보입니다.
[*]Left Alt 는 Alt+Tab / Alt+Enter 의 시작 키라 창 전환 직후 한 번 튈 수 있습니다. 거슬리면 전환 키를 바꾸세요.
[*]코옵 2인 실접속 테스트는 아직 하지 않았습니다. 문제가 있으면 댓글로 알려주세요.
[/list]

[h2]호환성[/h2]
게임 2.3.30 에서 제작·테스트. HarmonyLib(워크샵) 호환. SlimeNull Mod Settings · EscapeFromDuckovCoopMod 는 선택.

[i]MIT 라이선스. 코드 일부는 AI(Claude) 보조로 작성했습니다. 소스: GITHUB_URL[/i]

[hr][/hr]
[h1]English[/h1]
Your duck holds the gun in the [b]left hand[/b], and bullets really leave the [b]left muzzle[/b]. Not just a flipped model: the projectile spawn position is mirrored too (verified).

[h2]Features[/h2]
[list]
[*][b]Left-handed shooting[/b] — gun, muzzle and bullet spawn all move to the left. Aiming and hit detection are untouched.
[*][b]Instant toggle[/b] — Left Alt flips left ↔ right at any time, with a short sound (default: the held gun's reload click).
[*][b]Two modes[/b]
  Model (default) — the whole duck is mirrored, so the hands/wings follow the gun. Complete left-handed look.
  Socket — only the weapon socket is mirrored. Face paint and asymmetric gear stay; just the gun moves.
[*][b]In-game settings[/b] — with SlimeNull Mod Settings installed, everything is under Options → Mod Settings → ShoulderTransition. Otherwise edit config.json.
[*][b]Co-op sync[/b] — when another player who has this mod toggles, you see [i]their[/i] duck change too.
[*][b]Safe[/b] — touches only your own character's visuals. No save, stat, ballistics or networked game data is modified.
[/list]

[h2]Install[/h2]
Subscribe → launch → enable in Mods on the main menu → restart. No required dependencies (0Harmony.dll 2.4.1 bundled). If you also use the HarmonyLib workshop item, load this mod below it.

[h2]Controls[/h2]
[list]
[*][b]Left Alt[/b] — toggle left ↔ right (rebindable)
[*]Model ↔ Socket — unassigned by default; use the Mode dropdown in settings or assign a key
[/list]

[h2]Options[/h2] (settings page or config.json, kept in sync)
[list]
[*]mirror (true) — left-handed on/off
[*]mode (Model) — Model whole duck mirrored / Socket weapon only
[*]toggleKey (LeftAlt) — toggle key, Unity KeyCode name
[*]modeKey (None) — optional hotkey to flip Model/Socket
[*]toggleSound (GunReload) — GunReload held gun's reload click / GunUnload magazine-out / EquipGunLight·EquipGunHeavy gun equip swoosh / EquipDefault gear swoosh / UiClick / None. All built-in game sounds.
[*]coopSync (true) — co-op sync
[*]socketMirrorMesh (false), socketMirrorMelee (true) — Socket mode details
[*]dumpKey F9 / snapKey F10 / fmodDumpKey F11 / remoteTestKey None — debug
[/list]

[h2]Co-op sync[/h2]
[list]
[*]Each player broadcasts only their own state; the host relays a small table to everyone, including late joiners. Receivers mirror only that player's duck.
[*]Your toggle never changes anyone else. It only changes how others see you.
[*]Requirements: the other player needs this mod, and the host needs it to relay. Without the mod on their side you simply see them right-handed.
[*]The mod works fine without the co-op mod (EscapeFromDuckovCoopMod).
[/list]

[h2]Known limitations[/h2]
[list]
[*]Model mode mirrors the whole duck, so asymmetric face paint and gear swap sides too.
[*]Socket mode moves only the gun; the hand stays on the right, so the gun floats slightly.
[*]Left Alt is the first key of Alt+Tab / Alt+Enter; it may trigger once when you switch back to the game. Rebind if it bothers you.
[*]Two-player co-op has not been tested live yet. Please report issues in the comments.
[/list]

[h2]Compatibility[/h2]
Built and tested on game 2.3.30. Compatible with HarmonyLib (workshop). SlimeNull Mod Settings and EscapeFromDuckovCoopMod are optional.

[i]MIT license. Part of the code was written with AI (Claude) assistance. Source: GITHUB_URL[/i]
```

## Change Notes (v0.1.0)

```
v0.1.0
- Left-handed duck: gun, muzzle and bullet spawn mirrored to the left (Model / Socket modes)
- Left Alt toggle with selectable built-in sound; optional mode-switch hotkey
- In-game settings via SlimeNull Mod Settings (optional), config.json fallback
- Co-op: your toggle never affects others; optional co-op sync shows other players' handedness (both sides need the mod)
```
