# CLAUDE.md

이 파일은 이 저장소에서 작업할 때의 **작업 방식**을 담는다.
"코드가 무엇이고 왜 그렇게 설계됐는가" 는 [`README.md`](README.md) 가, "IIS 에 어떻게 올리는가" 는
[`DEPLOY.md`](DEPLOY.md) 가 담는다 — 여기서 되풀이하지 않는다.

# Workbench

내부용 이슈 추적 + 마크다운 지식 저장소. Microsoft Planner 대체.
ASP.NET Core 8 / Blazor Server / EF Core / Entra ID / Azure Blob / Markdig / Bootstrap 5.

---

## 회귀 가드 (정본)

- **단위 테스트 306/306** (`dotnet test`, skip 0) + **빌드 0 error / 0 warning** — 2026-09-16 실측 (`chore/bunit-2-anglesharp-fix` = `6d5f4e2`)
  - 테스트 메서드 선언 **206개**가 `[Theory]` 전개로 **306 케이스**가 된다. 두 수를 혼동하지 말 것.
  - 이전에 있던 `NU1902`(AngleSharp 1.2.0, moderate) 경고 2건은 해소됐다. bUnit 을 1.40 → 2.11.3 으로 올려 AngleSharp 1.8.1 을 정식으로 물게 했다(`bunit.web` 1.40.0 은 net8.0 대상에서 AngleSharp 1.2.0 에 바이너리로 고정돼 있어 단독으로 올릴 수 없었다 — `RenderComponent<T>()` → `Render<T>()`, `TestContext` → `BunitContext` 동반 마이그레이션 필요).

> **이 줄이 회귀 기준선의 유일한 정본이다.** 다른 문서·PR 본문·커밋 메시지는 숫자를 복제하지 말고 여기를 가리킨다.
> 값 갱신형 기록은 다음 사이클에 낡는 것이 기본값이다.

```bash
dotnet build          # 0 error 를 먼저 확인
dotnet test           # skip 0 · 위 정본과 대조
```

**착수 전에 baseline 을 직접 재라.** 위 수치를 읽고 시작하지 말고 `dotnet test` 를 한 번 돌려라.
문서가 stale 인 채로 작업하면 증분 계산이 전부 틀어지고, 그것은 조용히 틀어진다.

---

## 작업 방식

### 1. 계약은 "공허한 참" 이 아님을 증명해야 한다

테스트가 초록이라는 것과 그 테스트가 무언가를 지킨다는 것은 다른 명제다.
**고친 지점을 되돌려(뮤테이션) 실제로 빨개지는지 관측하고, 그 결과를 커밋 메시지에 적어라.**

이 저장소는 이미 두 번 겪었다:

- `IssueDisplayTests` 가 `StartsWith("text-bg-")` 를 요구했는데, 번들 Bootstrap 5.1 에 그 유틸리티가 없다.
  **규약을 지킬수록 모든 배지가 화면에서 사라지는 상태**였고 테스트는 내내 초록이었다.
- 마크다운 렌더 4지점 전부가 `@` 를 빠뜨려 식의 코드를 문자 그대로 출력했다. 그 지점에 **계약이 아예 없었다.**

두 결함 다 테스트 295개가 통과하는 상태에서 **앱을 띄워 보고서야** 드러났다.

### 2. 테스트가 통과해도 화면은 틀릴 수 있다 — 실기동으로 확인하라

UI 를 건드렸으면 실제로 띄워서 눈으로 봐라. Razor 는 컴파일되는 오타를 허용한다.

```bash
dotnet run --project src/Workbench.Web     # SQLite 데모 모드로 바로 뜬다
```

확인할 최소 집합: `/` · `/projects` · `/issues` · `/board` · `/issues/{key}` · `/pages` · `/pages/{slug}` · `/search`
— 전부 HTTP 200 이고 브라우저 콘솔 에러가 0 이어야 한다.

### 3. 수치는 인용하지 말고 실측하라

문서에 적힌 숫자는 인용 대상이 아니라 **대조 대상**이다. 다시 재서 다르면 문서를 고쳐라.
새 수치를 적을 때는 **무엇을 언제 어느 커밋에서 쟀는지**를 같이 적는다.

### 4. 알려진 한계를 숨기지 않는다

검증하지 못한 것은 "검증했다" 로 반올림하지 않는다. README 「아직 자동 검증되지 않는 것」 절이 그 자리다.
**지금 그 목록에 있는 것**: 실제 SQL Server 마이그레이션 적용 · `AzureBlobFileStorage` 실행 · 보드 외 화면의 bUnit 커버리지.

### 5. 외부 동작이 바뀌면 명시하라

화면 수치, 링크 대상, 응답 형식, 허용되던 입력이 달라지면 커밋 메시지와 PR 본문에 **의도된 변경**으로 적는다.
"리팩터링" 으로 묶어서 넘기지 않는다.

---

## 이 저장소 특유의 함정

### Development 가드 3개 — 느슨하게 고치지 마라

세 곳이 Development 환경이 아니면 **기동 시점에 예외를 던진다.** 버그가 아니라 설계다.

| 가드 | 위치 | 막는 것 |
|---|---|---|
| `Authentication:Mode=Development` | `AuthenticationExtensions.cs` | 설정 실수로 "아무나 관리자" 인 서비스가 조용히 운영에 뜨는 것 |
| `Database:Provider=Sqlite` | `Infrastructure/DependencyInjection.cs` | 마이그레이션이 적용되지 않은 스키마로 운영이 도는 것. **탈출구 없음** |
| `FileStorage:Provider=Local` | `BlobStorage/FileStorageExtensions.cs` | 재배포·스케일아웃에서 첨부가 사라지는 것 (명시 플래그로만 허용) |

기동이 막히면 가드를 고치지 말고 **설정을 고쳐라.**

### 번들 Bootstrap 은 5.1 이다

`wwwroot/bootstrap/bootstrap.min.css` 가 5.1 이라 **`text-bg-*` 유틸리티가 없다.**
그 클래스를 쓰면 `.badge` 의 `color:#fff` 만 남아 배경 없는 흰 글자가 된다 — 배지가 화면에서 사라진다.
배경과 글자색을 따로 지정한다(`bg-secondary`, `bg-light text-dark`, …).
`RazorMarkupContractTests` 가 마크업에 쓰인 배지 클래스가 번들 CSS 에 실재하는지 확인한다.

### Razor 컴포넌트 파라미터에 `@` 를 빠뜨리지 마라

`Markdown="_issue.DescriptionMarkdown"` 은 **리터럴 문자열**로 넘어간다. 파라미터 타입이 `string?` 이라 컴파일 오류도 없다.
`RazorMarkupContractTests` 가 `.razor` 원본을 스캔해 막는다.

### 마이그레이션은 SQL Server 전용이다

SQLite 데모 모드는 `EnsureCreated` 로 스키마를 세운다 — 인덱스 키 크기·`datetimeoffset` 컬럼 타입 같은 세부가 재현되지 않는다.
**SQLite 에서 통과했다고 SQL Server 에서 통과하는 것이 아니다.** 그 간극은 설계시점 EF 모델 계약이 담당한다.

---

## 계층 규약

의존 방향은 `Web → Infrastructure → Application → Domain` 이고, **아래 세 계층은 웹 프레임워크를 모른다.**

2026-09-16 실측: `Domain`·`Application`·`Infrastructure`(82파일 / 5,254줄)에
`Microsoft.AspNetCore` · `Microsoft.JSInterop` · `ComponentBase` 참조 **0건**.
컴포넌트가 `DbContext` 나 리포지토리를 직접 만지는 곳도 **0건** — 화면은 서비스 인터페이스 10개만 경유한다.

**이 성질을 깨뜨리지 마라.** UI 프레임워크 교체·API 층 신설의 비용이 전부 여기에 달려 있다.

---

## Git 워크플로

- **`main` 직접 push 금지.** 항상 피처 브랜치 + PR.
- 브랜치 접두어: `feat/` · `fix/` · `docs/` · `chore/`
- 푸시 전 현재 브랜치를 확인한다.
- PR 본문에 회귀 수치를 적을 때는 **그 PR 에서 실측한 값**을 적는다.

---

## 디버깅 프로토콜

- 코드를 고치기 전에 **증상을 자기 말로 재진술**하고 확인받는다.
- 사용자 명시 컨펌 없이 다른 가설로 pivot 하지 않는다.
- 사용자가 끼어들거나 정정하면 **현재 접근을 즉시 중단**하고, 새 메시지를 두 번 읽고, 무엇이 바뀌었는지 한 줄로 요약한 뒤 재출발한다.
- 증상과 원인이 멀리 떨어진 버그는 가설 2~3개를 먼저 나열하고 선택을 기다리는 편이 빠르다.

---

## ⚠ `.claude/` 는 이 저장소에 두지 마라

`.claude/` 는 gitignore 대상이라 **clone·브랜치 체크아웃·다른 기계에 따라가지 않는다.**
거기 둔 규약·교훈은 만든 사람의 기계에만 존재하고, 다른 세션은 그것이 있는 줄 알면서 없이 작업하게 된다.

이 저장소가 참고한 `knox-mail-pipeline` 이 정확히 그 일을 겪었다 — 축적된 교훈 25건이 gitignore 안에 있어
"에이전트가 반드시 읽어야 한다" 고 지시받은 문서에 아무도 도달하지 못했다.

**세션을 넘어 살아야 하는 것은 추적되는 파일에 둔다.** 이 `CLAUDE.md` 가 그 자리다.

---

## 아직 이 저장소에 없는 것

- **CI** — GitHub Actions 워크플로가 없다. 지금은 `dotnet build` + `dotnet test` 를 사람이 돌린 결과만 있다.
- **UI 프레임워크 대안 검토** — Blazor Server 의 회로 의존이 문제가 되면, 위 「계층 규약」 덕분에
  업무 로직은 무변경이고 `.razor` 23파일(2,386줄, `src` 의 약 28%)만 바뀐다.

---

## 변경 이력

| 날짜 | 변경 내용 | 사유 |
|------|----------|------|
| 2026-09-16 | `CLAUDE.md` 신설 — `knox-mail-pipeline` 의 작업 방식(회귀 가드 규약·뮤테이션 게이트·실측 우선·디버깅 프로토콜)을 이 저장소 맥락으로 이관하고, 이번까지 겪은 함정 4건을 기록 | 저장소가 분리되면서 작업 규약이 원본 저장소에만 남아 있었다. README 는 설계 기록이라 "어떻게 작업하는가" 를 담는 자리가 없었다 |
| 2026-09-16 | bUnit 1.40 → 2.11.3, `NU1902`(AngleSharp 1.2.0) 경고 해소. 회귀 가드 정본 줄 갱신(`6d5f4e2`, 0 error / 0 warning) | AngleSharp 만 단독으로 올리면 bUnit 1.40 과 바이너리 비호환(뮤테이션 없이도 실패로 관측)이라, 실제 해법인 bUnit 메이저 업그레이드로 처리 |
| 2026-09-16 | `DEPLOY.md` + `deploy/web.config.snippet.xml` + `deploy/preflight-iis.ps1` 신설 — IIS 배포 가이드 | 「아직 이 저장소에 없는 것」에 있던 항목을 채움. 첨부 업로드가 HTTP POST 가 아니라 Blazor 회로(SignalR) 로 흐른다는 걸 코드로 확인해, IIS 요청 상한이 실제로는 이 경로를 막지 않는다는 점을 정정해 기록 |
