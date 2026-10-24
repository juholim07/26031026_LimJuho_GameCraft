# Game Resource

## Temporary

`Temporary/`에는 기존 코드로 그리던 임시 그래픽을 PNG 50개로 저장한다.
게임 시작 시 한 번 로드하며 빌드·배포 출력의 `resource/Temporary/`에도 자동으로 복사된다.

| 파일 | 용도 |
| --- | --- |
| `sky.png`, `moon.png`, `buildings.png`, `pillars.png` | 시차 스크롤 배경 |
| `ground.png`, `ground_tile.png` | 바닥과 반복 무늬 |
| `platform_*.png` | 두 맵에서 사용하는 폭별 발판 |
| `start_flag.png`, `torch_*.png` | 시작 깃발과 횃불 애니메이션 |
| `portal_locked.png`, `portal_open.png` | 출구 잠금·개방 상태 |
| `player_left_*.png`, `player_right_*.png`, `player_gun.png` | 플레이어의 좌우 걷기 8프레임과 조준용 총 |
| `grunt_*.png`, `gunner_*.png`, `lord_*.png` | 적 3종의 좌우 방향과 피격 상태 |

출처: 이 프로젝트의 기존 도형 그래픽을 `tools/TemporaryAssets`로 내보낸 자체 제작 리소스.
텍스트·HUD·탄환·폭발·파티클 등 변화하는 요소는 게임에서 계속 그린다.

재생성 방법과 이미지 교체 안내는 [Temporary/README.md](Temporary/README.md)를 참조한다.
