Shoulder Transition

## Korean / 한국어

총기 좌견착 / 우견착을 지원하는 모드입니다.

- 견착 변경
'Left Alt'를 입력하여 총, 총구, 탄 발사 위치를 좌/우로 전환합니다. 전환 시에는 짧은 효과음(기본 : 장착한 총기의 장전 소리)이 발생합니다.

- 견착 외관
견착 외관은 두 가지 방식을 지원합니다.
* Model(기본) : 캐릭터 전체를 반전시킵니다. 모델링 전체를 뒤집기에 손과 총기의 연결이 자연스럽습니다.
* Socket : 무기의 위치만 반전시킵니다. 총기 모델링만 반전시키기에 손과 총기의 연결이 각도에 따라 부자연스러워 보일 수 있습니다.

- 게임 내 설정 창
[SlimeNull Mod Settings](https://steamcommunity.com/sharedfiles/filedetails/?id=3789464208) 설정 → Mod Settings → ShoulderTransition 에서 모든 옵션을 바꿀 수 있습니다.
사용하지 않을 경우 모드 경로상의 `config.json`을 통해 변경해야 합니다.

- 코옵 동기화
같은 모드를 가진 다른 플레이어가 전환하면 내 화면에서도 다른 플레이어의 오리가 바뀝니다.
상대도 이 모드가 있어야 하고, 중계를 위해 호스트에도 있어야 합니다. 상대에게 모드가 없으면 평소처럼 오른손으로 보일 뿐입니다

- 조작
Left Alt : 왼손 ↔ 오른손 전환, 설정에서 변경 가능
기본 미할당 : Model ↔ Socket 전환, 설정 창의 Mode 드롭다운으로 바꾸거나 원하는 키를 지정

- 전환 효과음
GunReload : 장착하고 있는 총기의 장전 소리
GunUnload : 탄창을 탈착하는 소리
EquipGunLight : 가벼운 총기 장착 소리
EquipGunHeavy : 무거운 총기 장착 소리
EquipDefault : 장비 착용 소리
UiClick : UI 클릭음
None : 없음

- 알려진 문제
Model 방식은 오리 전체를 반전시켜 비대칭 얼굴 무늬·장비도 좌우가 바뀝니다.
Socket 방식은 총기만 반전 시켜 총기와 손의 연결이 부자연스러워 보입니다.

- 호환성
게임 버전 : 2.3.30 에서 제작·테스트
HarmonyLib : 필수 설치
SlimeNull Mod Settings : 선택. 설정 UI 제공


코드 일부는 AI(Claude) 보조로 작성했습니다.

-----------------------------------------------------------------------------------

## English / 영어

A mod that lets you shoulder your gun on the left or the right.

- Switching shoulders
Press ‘Left Alt’ to flip the gun, muzzle, and bullet spawn point between left and right. A short sound effect plays on each switch (default: the reload sound of your equipped gun).

- Shoulder appearance
Two display modes are supported.
* Model (default): Mirrors the entire character. Since the whole model is flipped, the connection between the hands and the gun looks natural.
* Socket: Mirrors only the weapon’s position. Since only the gun model is flipped, the connection between the hands and the gun may look awkward at some angles.

- In-game settings window
All options can be changed via SlimeNull Mod Settings → Mod Settings → ShoulderTransition.
If you don’t use it, you’ll need to edit config.json in the mod folder instead.

- Co-op sync
When another player with this mod switches shoulders, their duck changes on your screen too.
The other player must have this mod, and the host must have it as well to relay the change. If the other player doesn’t have the mod, they’ll simply appear right-handed as usual.

- Controls
Left Alt: Toggle left ↔ right hand (rebindable in settings)
Unassigned by default: Toggle Model ↔ Socket; change it via the Mode dropdown in the settings window, or bind a key of your choice

- Switch sound effects
GunReload: Reload sound of the equipped gun
GunUnload: Magazine removal sound
EquipGunLight: Light gun equip sound
EquipGunHeavy: Heavy gun equip sound
EquipDefault: Gear equip sound
UiClick: UI click sound
None: No sound

- Known issues
Model mode mirrors the entire duck, so asymmetric face patterns and gear are also flipped left-to-right.
Socket mode mirrors only the gun, so the connection between the gun and the hands may look awkward.

- Compatibility
Game version: Built and tested on 2.3.30
HarmonyLib: Required
SlimeNull Mod Settings: Optional. Provides the settings UI

Some of the code was written with AI (Claude) assistance.
