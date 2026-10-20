# 화성의 용병단

MercenariesOfMars — 아군 턴과 적군 턴이 번갈아 진행되는 3D 격자 기반 메카닉 SRPG 프로젝트.

## 실행 및 편집

Unity **6000.6.5f1**에서 프로젝트를 열고 `Assets/Scenes/SampleScene.unity`를 실행한다. 타이틀에서 캠페인을 시작한다. 스테이지 에디터와 맵 에디터에서는 개별 테스트 플레이도 가능하다.

**화성의 용병단** 메뉴에 기체·파일럿·유닛 편성·무장·맵·스테이지·캠페인·조립형 스크립트 에디터가 있다. 데이터는 `Assets/MercenariesOfMars/Data/`, 실행 코드는 `Runtime/`, 제작 도구는 `Editor/`에 있다. 파일럿은 발키리·해태·네뷸라 각각 10명과 기존 예제 파일럿을 포함한다.

## 기능 안내

- [기본 조작과 전투](Assets/MercenariesOfMars/사용안내.md)
- [기체·파일럿과 개조](Assets/MercenariesOfMars/기체와파일럿안내.md)
- [맵 에디터](Assets/MercenariesOfMars/맵에디터안내.md)
- [출격 배치·영입·이탈](Assets/MercenariesOfMars/아군출격배치안내.md)
- [캠페인 진행과 스테이지 테스트](Assets/MercenariesOfMars/스테이지와인터미션안내.md)
- [인터미션·구매·조건부 상점·저장](Assets/MercenariesOfMars/인터미션안내.md)
- [해태 파일럿](Assets/MercenariesOfMars/해태파일럿안내.md) · [네뷸라 파일럿](Assets/MercenariesOfMars/네뷸라파일럿안내.md)
- [미구현·결정 사항 기록](문서/프로젝트_미구현과_결정사항_2026-10-18.md) · [수치·계산식 결정 목록](문서/수치와_계산식_결정목록_2026-10-18.md)
- [파일 정리 기록](문서/프로젝트정리_2026-10-20.md)

## 검증과 버전 관리

Unity Editor 상태·컴파일·테스트는 Unity CLI로 확인한다. 테스트 어셈블리는 `MercenariesOfMars.Tests`（EditMode）와 `MercenariesOfMars.PlayMode.Tests`다. 새 데이터 작성 후 **화성의 용병단 → 저장 참조 목록 갱신**으로 빌드용 참조 목록을 갱신한다.

`Library/`, `Temp/`, `Logs/`, `UserSettings/`와 `작업결과/`의 로컬 백업·검증 결과는 Git에서 제외한다. 씬·프리팹 수정은 별도 승인을 받은 뒤 진행한다.
