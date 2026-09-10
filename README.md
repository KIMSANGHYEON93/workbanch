# Workbench

내부용 이슈 추적 + 마크다운 지식 저장소. Microsoft Planner 를 대체하기 위해 자체 제작한다.

- **Backend / Frontend**: ASP.NET Core 8 + Blazor Server (C# 풀스택)
- **DB**: Azure SQL Database (로컬은 LocalDB / Docker SQL Server)
- **ORM**: Entity Framework Core
- **인증**: Microsoft Entra ID (Microsoft.Identity.Web) — 개발 중에는 우회 옵션 제공
- **UI**: Bootstrap 5 (템플릿 로컬 번들, 외부 CDN 0건)
- **파일**: Azure Blob Storage (로컬은 Azurite)
- **마크다운**: Markdig 렌더링 + TextArea/미리보기 에디터 (JS interop 없음)

---

## 진행 현황

| # | 단계 | 상태 |
|---|------|------|
| 1 | 솔루션 · 프로젝트 생성 + 기본 설정 | ✅ 완료 |
| 2 | Domain 엔티티 + Enum | ✅ 완료 |
| 3 | Infrastructure: DbContext + EF Core 마이그레이션 | ✅ 완료 |
| 4 | 인증 (Entra ID) 연동 + 개발용 우회 | ✅ 완료 |
| 5 | Project CRUD | ✅ 완료 |
| 6 | Issue CRUD + 상태 변경 + 리스트 | ⬜ |
| 7 | 마크다운 에디터 컴포넌트 | ✅ 완료 |
| 8 | Page CRUD + 계층 구조 | ✅ 완료 |
| 9 | Comment | ✅ 완료 |
| 10 | 파일 첨부 (Azure Blob) | ⬜ |
| 11 | 칸반 보드 뷰 | ⬜ |
| 12 | 기본 검색 | ⬜ |
| 13 | UI 폴리싱 + 권한 체크 | ⬜ |

---

## 솔루션 구조

```
Workbench/
├── Directory.Build.props          # 공통 빌드 속성 (net8.0, nullable, 코드 스타일)
├── Workbench.sln
├── src/
│   ├── Workbench.Domain/          # 엔티티 · Enum · 저장소 인터페이스 (의존성 0)
│   ├── Workbench.Application/     # DTO · 서비스 · 애플리케이션 인터페이스
│   ├── Workbench.Infrastructure/  # EF Core · 저장소 구현 · Blob · Identity
│   └── Workbench.Web/             # Blazor Server 호스트
└── tests/
    └── Workbench.Tests/           # xUnit
```

의존 방향: `Web → Infrastructure → Application → Domain`.
Clean Architecture 를 가볍게만 따른다 — **MediatR / CQRS 는 MVP 에서 쓰지 않는다.**

---

## 도메인 모델

| 엔티티 | 역할 |
|--------|------|
| `AppUser` | Entra ID 사용자의 로컬 투영. `Id` = Entra Object Id(oid) |
| `Project` | 프로젝트. `Key`(DEV, DEPLOY)가 이슈 키 접두어 |
| `ProjectMember` | 프로젝트 단위 권한 (`Viewer` / `Member` / `Admin`) |
| `Issue` | 이슈. `Key`(DEV-123) · 상태 · 우선순위 · 담당자 · `Environment` / `Version` |
| `Page` | 마크다운 페이지. `ParentPageId` 로 계층 구성, 프로젝트 연결은 선택 |
| `Comment` | 이슈 **또는** 페이지에 달리는 댓글 (둘 중 정확히 하나) |
| `Attachment` | Blob 메타데이터. 실제 바이트는 Blob Storage 에 있고 `BlobPath` 만 DB 에 남는다 |

### 설계상 짚어 둘 점

- **`Project.LastIssueNumber`** — 이슈 키 번호는 `COUNT(*)` 로 만들 수 없다(삭제 이력 때문에 번호가 재사용된다).
  프로젝트에 발급 카운터를 두고 증가시킨다.
- **`Issue.Key` 비정규화** — 목록/검색/링크에서 `Project` 조인 없이 쓰기 위해 저장한다.
  값은 언제나 `Issue.FormatKey(projectKey, number)` 하나로만 만든다(문화권 무관 서식).
- **`Comment` / `Attachment` 의 이중 FK** — `IssueId` 와 `PageId` 중 정확히 하나만 채운다.
  DB CHECK 제약으로 강제할 예정(3단계).
- **길이 제약은 `DomainConstants.Lengths` 단일 출처** — EF 설정과 폼 검증이 서로 다른 상수로 갈라지지 않게 한다.
- **`Attachment.BlobPath` 는 512자** — UNIQUE 인덱스가 걸리는데 SQL Server 비클러스터드 인덱스 키
  상한이 1,700바이트다. `nvarchar(1024)` = 2,048바이트면 인덱스 생성은 경고만 내고 통과한 뒤
  **긴 값을 INSERT 하는 시점에** 터진다. `WorkbenchDbContextModelTests` 가 이 상한을 계약으로 문다.

---

## 영속성 (3단계)

- `WorkbenchDbContext` — 설정은 `Data/Configurations/*.cs` 로 분리하고 어셈블리 스캔으로 등록한다.
- `Repository<T>` — CRUD 만 하고 `SaveChanges` 는 부르지 않는다. 커밋 시점은 `IUnitOfWork` 를 가진 호출자가 정한다.
- `AddInfrastructure(IConfiguration)` — `ConnectionStrings:Workbench` 를 읽고, 없으면 **명확한 메시지로 기동을 막는다**.
- Azure SQL 의 일시적 연결 끊김에 대비해 `EnableRetryOnFailure()` 를 기본으로 켠다.

### DbContext 수명 (Blazor Server 주의)

Blazor Server 의 DI 스코프는 **회로(circuit) 수명**과 같아서, 순진하게 `AddDbContext` 로 등록한
`DbContext` 는 사용자가 탭을 열어 둔 몇 시간 동안 살아남는다. 그래서 팩토리(`AddDbContextFactory`)를
등록하고 스코프 인스턴스는 그 팩토리로 만든다 — 짧은 수명이 필요한 지점에서 등록 방식을 바꾸지 않고
`IDbContextFactory<WorkbenchDbContext>` 로 바로 갈아탈 수 있다.

### 스키마 규칙 (마이그레이션에 반영됨)

| 규칙 | 구현 |
|---|---|
| 이슈 키 중복 방지 | `Issues.Key` UNIQUE + `(ProjectId, Number)` UNIQUE — 문자열만 막으면 번호를 중복 발급하고 다른 접두어로 통과시킬 수 있다 |
| 댓글/첨부의 소유자는 정확히 하나 | `CK_Comments_SingleOwner` · `CK_Attachments_SingleOwner` CHECK 제약 |
| 사용자는 삭제하지 않는다 | 사용자 참조 FK 는 전부 `NO ACTION` — 담당자/작성자 이력 보존 (`IsActive` 로 비활성화) |
| 프로젝트를 지워도 지식 문서는 남는다 | `Pages.ProjectId` FK 는 `SET NULL` |
| 페이지 계층은 CASCADE 금지 | SQL Server 는 자기참조 CASCADE 를 만들 수 없다 — `NO ACTION` |

### ⚠ LocalDB 는 Windows 전용

`appsettings.Development.json` 의 기본 접속 문자열은 LocalDB 다. Linux/macOS 개발자는
`PlatformNotSupportedException: LocalDB is not supported on this platform` 을 만나므로,
User Secrets 또는 환경변수로 덮어쓴다.

```bash
# 예: 로컬 Docker SQL Server
export ConnectionStrings__Workbench="Server=localhost,1433;Database=Workbench;User Id=sa;Password=<암호>;Encrypt=False;TrustServerCertificate=True"
```

이 예외는 **의도적으로 흡수하지 않는다** — 일시적 장애가 아니라 설정 오류이므로 조용히 넘어가면
"DB 가 있는 줄 알았는데 아무것도 저장되지 않는" 상태가 된다.

### 마이그레이션 명령

```bash
cd Workbench

# 도구는 로컬 tool manifest 에 고정돼 있다 (.config/dotnet-tools.json)
dotnet tool restore

# 새 마이그레이션
dotnet dotnet-ef migrations add <이름> --project src/Workbench.Infrastructure --output-dir Data/Migrations

# DB 에 적용
dotnet dotnet-ef database update --project src/Workbench.Infrastructure

# DBA 검토용 SQL 스크립트
dotnet dotnet-ef migrations script --idempotent --project src/Workbench.Infrastructure -o init.sql
```

접속이 필요한 명령(`database update`)은 `WORKBENCH_DESIGNTIME_CONNECTION` 환경변수로 접속 문자열을
넘긴다. 설계 시점 기본값은 접속하지 않는 자리표시자이며 **시크릿을 소스에 두지 않는다**.

---

## 개발 환경

### 요구 사항

- .NET 8 SDK
- SQL Server LocalDB 또는 Docker SQL Server *(3단계부터 필요)*
- Azurite 또는 개발용 Azure Storage 계정 *(10단계부터 필요)*

### 빌드 · 테스트 · 실행

```bash
cd Workbench

dotnet tool restore          # dotnet-ef (마이그레이션 도구)
dotnet build
dotnet test
dotnet run --project src/Workbench.Web
```

DB 가 없어도 앱은 기동한다(홈 화면은 쿼리를 하지 않는다). 데이터 화면부터는
`dotnet dotnet-ef database update` 로 스키마를 만들어야 한다.

Entra ID 앱 등록은 아직 필요 없다. 4단계에서 개발용 인증 우회 스위치를 추가한다.

### 설정값

접속 문자열·시크릿은 `appsettings.json` 에 커밋하지 않는다. User Secrets 를 쓴다.

```bash
dotnet user-secrets init --project src/Workbench.Web
dotnet user-secrets set "ConnectionStrings:Workbench" "<값>" --project src/Workbench.Web
```

`appsettings.Development.json` 에는 LocalDB 기본값만 들어 있다. 운영 접속 문자열은
`appsettings.json` 에 두지 않고 Azure App Service 설정 / Key Vault 에서 주입한다 —
접속 문자열이 없으면 `AddInfrastructure` 가 명확한 메시지로 기동을 막는다.

---

## 인증 (4단계)

두 가지 모드가 있고 `Authentication:Mode` 로 고른다.

| 모드 | 동작 | 용도 |
|---|---|---|
| `EntraId` (기본값) | Microsoft Entra ID OpenID Connect. `/MicrosoftIdentity/Account/*` 로그인 엔드포인트 제공 | 운영 |
| `Development` | 고정 사용자로 **항상 로그인된 상태**. Entra 앱 등록 없이 개발 가능 | 로컬 개발 전용 |

### 개발용 우회는 운영에서 기동을 막는다

`Mode=Development` 인데 호스트 환경이 Development 가 아니면 `AddWorkbenchAuthentication` 이
**예외를 던져 앱이 뜨지 않는다.** 개발용 우회는 "아무나 로그인된 상태"와 같아서, 설정 실수로
운영에 켜지면 조용히 인증 없는 서비스가 되기 때문이다.

같은 이유로 **설정이 비어 있으면 `EntraId` 가 기본값**이다 — 설정 누락이 인증 우회로 해석되면 안 된다.

### 사용자 식별과 프로비저닝

- 사용자 Id 는 Entra **Object Id**(`oid`)다. 메일 주소와 달리 바뀌지 않는다.
- Object Id 가 없거나 GUID 가 아니면 **미인증으로 떨어뜨린다(fail-closed)** — 추측한 Id 로 프로비저닝하면
  담당자·작성자 이력이 다른 사람에게 붙는다.
- 클레임 → 사용자 변환은 `WorkbenchClaims.ToCurrentUser` **한 곳**에서만 한다. 미들웨어(HttpContext)와
  회로(`AuthenticationStateProvider`)가 같은 규칙을 써야 "프로비저닝된 사용자"와 "화면이 보는 사용자"가
  갈라지지 않는다.
- 로그인한 사용자는 최초 문서 요청에서 `Users` 테이블에 반영된다(10분 억제 캐시).

### 프로비저닝은 렌더의 전제조건이 아니다

DB 장애 시 `EnsureProvisionedAsync` 는 **예외를 던지지 않고 `false` 를 돌려준다.**
그렇지 않으면 DB 가 잠깐 죽었을 때 **모든 화면이 500** 이 된다(로그인 화면 포함).
실패는 캐시하지 않으므로 DB 가 돌아오면 다음 요청에서 곧바로 다시 시도한다.

실측: DB 를 못 여는 상태에서 `GET /` → **HTTP 200**, 경고 1줄, 미처리 예외 0건.

---

## 프로젝트 (5단계)

| 화면 | 경로 |
|---|---|
| 목록 | `/projects` — 키·이름·설명·이슈 수, 인라인 삭제 확인 |
| 생성 | `/projects/new` |
| 수정 | `/projects/{id}/edit` |

`ProjectService` 가 업무 규칙을 갖고, `IProjectRepository` 가 조회를 갖는다.

### 규칙 4가지

1. **키는 정규화한다** — 앞뒤 공백 제거 + 대문자. `dev`·`DEV `·`Dev` 가 서로 다른 프로젝트가 되면
   이슈 키 접두어가 갈라진다. 정규화·검증은 `ProjectKey` 한 곳에서만 한다.
2. **키는 생성 후 변경할 수 없다** — `Issue.Key` 가 `DEV-123` 으로 비정규화돼 있어, 키를 바꾸면
   이미 발급된 이슈 키 전부가 프로젝트와 어긋난다. 수정 화면에서 입력란이 비활성화되고,
   서버도 별도로 거부한다(화면만 막으면 우회된다).
3. **이슈가 남은 프로젝트는 삭제하지 않는다** — FK 가 CASCADE 라 이슈까지 조용히 사라진다.
   개수를 알려주고 거부한다.
4. **업무 규칙 위반은 예외가 아니라 `OperationResult`** — 화면이 오류를 폼 옆에 그대로 보여줘야 하고,
   예상 가능한 입력 실수로 스택 트레이스를 남기지 않는다.

### DB 장애 시 화면

데이터 화면은 조회 실패를 잡아 **조치 가능한 문장**만 보여준다(원인은 로그에 남는다).
DB 를 못 여는 상태에서 `/`·`/projects`·`/projects/new` 전부 **HTTP 200**, 미처리 예외 0건 — 실측.

---

## 이슈 (6단계)

| 화면 | 경로 |
|---|---|
| 목록 | `/issues` — 프로젝트·상태·담당자·검색어 필터, 완료 포함 토글 |
| 생성 | `/issues/new` |
| 상세 | `/issues/{key}` — 상태 인라인 변경, 삭제 확인 |
| 수정 | `/issues/{key}/edit` |

### 규칙 5가지

1. **이슈 번호는 `Project.LastIssueNumber` 로 발급하고 경합 시 재시도한다.**
   두 사람이 동시에 만들면 같은 번호를 집어 `UNIQUE(ProjectId, Number)` 가 한쪽을 거절한다 →
   추적 중인 변경을 버리고(`IUnitOfWork.DiscardChanges()`) 다시 읽어 다음 번호를 집는다(최대 3회).
   **마지막 시도의 예외는 흡수하지 않는다** — 원인이 경합이 아니면 조용히 삼키는 것이 더 나쁘다.
2. **삭제된 이슈 번호는 재사용하지 않는다** — 지워진 키가 다른 이슈로 되살아나면 과거 링크·문서가
   엉뚱한 이슈를 가리킨다. `LastIssueNumber` 를 되돌리지 않는다.
3. **이슈를 다른 프로젝트로 옮길 수 없다** — 키의 접두어가 프로젝트에 묶여 있다. 옮기려면 번호를
   새로 발급해야 하므로 MVP 범위 밖이다.
4. **비활성 계정에는 배정할 수 없다** — 아무도 보지 않는 곳으로 이슈가 사라진다.
   기존 담당 이력은 그대로 남는다.
5. **상태를 콕 집어 고르면 "완료 숨김" 이 그것을 덮지 않는다** — 안 그러면 "완료" 필터가 빈손을 돌려준다.

같은 상태로의 변경은 성공으로 보되 `UpdatedAt` 을 건드리지 않는다 — 목록 정렬이 이유 없이 흔들린다.

---

## 마크다운 (7단계)

| 구성요소 | 역할 |
|---|---|
| `MarkdownRenderer` | 마크다운 → HTML 변환의 단일 출처 (Markdig, 싱글턴) |
| `MarkdownView` | 렌더된 본문 표시 |
| `MarkdownEditor` | 작성/미리보기 탭. **JS interop 0줄** — 서버 렌더로만 동작한다 |

지원: 제목 · 강조 · 목록 · 코드블록 · 표(`UsePipeTables`) · 체크리스트(`UseTaskLists`) · 자동 링크.

### ★ 저장형 XSS 방어선 2개

이슈 설명과 페이지 본문은 **쓰는 사람과 읽는 사람이 다르다.** 내부 도구라도 저장형 XSS 가
성립하므로 두 표면을 모두 막는다.

1. **원시 HTML** — 마크다운은 원시 HTML 을 그대로 통과시키는 것이 **표준 동작**이다.
   `DisableHtml()` 로 끈다. 끄지 않으면 본문에 적힌 `<script>` 가 읽는 사람 브라우저에서 실행된다.
2. **링크 스킴** — `DisableHtml()` 은 태그만 막고 `href` 는 건드리지 않는다.
   `[클릭](javascript:alert(1))` 은 순수 마크다운 문법이라 그대로 통과한다 →
   렌더링 **전에 문서 트리(AST)** 에서 위험한 URL 을 `#` 로 바꾼다.
   HTML 문자열을 정규식으로 다루지 않는다. 허용 스킴은 `http` · `https` · `mailto` + 상대 경로.

**뮤테이션 실측** — 계약이 실제로 이 둘을 지키는지 확인했다.

| 주입 | 결과 |
|---|---|
| `DisableHtml()` 제거 | **RED 6** |
| 링크 무해화 제거 | **RED 5** |
| `Trim()` 제거 | **RED 1** |
| `ToLowerInvariant()` 제거 | **RED 1** |

⚠ 그 과정에서 **죽은 방어 코드 1건을 찾아 제거**했다. 처음에는 URL 의 공백·제어문자를 전부
털어내며 "회피 방어" 라고 주석을 달았는데, 뮤테이션이 **RED 0** 을 냈다 — 판정이 *허용목록*이라
`java\tscript` 같은 망가진 스킴은 목록에 없어서 어차피 거부된다. 정규화가 실제로 하는 일은
반대쪽(멀쩡한 링크를 잘못 막지 않는 것)이므로, 코드를 `Trim().ToLowerInvariant()` 로 줄이고
주석을 사실에 맞게 고쳤다. **설명이 틀린 방어 코드는 없는 것보다 나쁘다.**

---

## 페이지 (8단계)

| 화면 | 경로 |
|---|---|
| 목록 | `/pages` — 계층 트리 + 제목·본문 검색 |
| 생성 | `/pages/new` |
| 보기 | `/pages/{slug}` — 마크다운 렌더 |
| 수정 | `/pages/{slug}/edit` |

### 규칙 5가지

1. **슬러그는 한글을 그대로 둔다** — 음역하면 사람이 URL 을 보고 문서를 알아볼 수 없다.
   `배포 절차` → `배포-절차`. 생성·정규화는 `PageSlug` 한 곳에서만 한다.
2. **수정 시 슬러그를 비우면 기존 주소를 유지한다** — 제목을 고쳤다고 링크가 끊기면 안 된다.
3. **슬러그가 겹치면 `-2`, `-3` 으로 비켜 간다.** 제목이 전부 기호라 슬러그가 비면
   되돌릴 수 없는 링크가 되므로 대체값을 만든다.
4. **자기 자신·자기 자손을 상위로 고를 수 없다** — 그 고리가 생기면 해당 가지가 트리에서
   떨어져 나와 어느 화면에서도 다시 보이지 않는다. 선택 상자에서 빼고, 서버도 별도로 거부한다.
5. **하위 페이지가 있으면 삭제를 거부한다** — 자기참조 FK 가 `NO ACTION` 이라 DB 가 어차피
   거절하는데, 드라이버 오류를 화면에 흘리는 대신 몇 건이 남았는지 알려준다.

트리를 펼치는 코드는 **방문 집합으로 순회를 막고, 부모가 사라진 페이지도 목록에서 잃지 않는다** —
그렇지 않으면 데이터가 어긋난 순간 문서가 조용히 접근 불가가 된다.

> 설계 결정 "프로젝트를 지워도 지식 문서는 남긴다"(`Pages.ProjectId` = `SET NULL`)를 문서로만 두지
> 않고 **실제로 실행해 확인**했다 — 프로젝트 삭제 후 페이지는 남고 연결만 끊긴다.

---

## 댓글 (9단계)

이슈 상세와 페이지 보기에 같은 `CommentThread` 컴포넌트를 얹는다. 본문은 마크다운으로 렌더된다.

### ★ 규칙을 검증이 아니라 **타입으로** 표현했다

댓글은 이슈 **또는** 페이지 중 정확히 한쪽에 달린다. 이 규칙을 `if` 로 확인하는 대신
서비스 API 에서 **표현 불가능하게** 만들었다 — 대상 두 개를 받는 메서드가 없다.

```csharp
Task<OperationResult> AddToIssueAsync(Guid issueId, string? content, ...);
Task<OperationResult> AddToPageAsync(Guid pageId,  string? content, ...);
```

저장소를 직접 쓰는 코드가 나중에 생겨도 **DB CHECK 제약이 막는다** — 그것을 문서가 아니라
실행으로 확인했다(양쪽 채움 · 양쪽 비움 모두 `DbUpdateException`).

### 삭제는 서버가 판정한다

본인이 쓴 댓글만 지울 수 있다. 화면에서 버튼을 감추는 것만으로는 부족하다 — 남의 대화 기록을
지울 수 있으면 이력이 신뢰를 잃는다.

이슈·페이지를 지우면 그 댓글은 `CASCADE` 로 함께 사라진다(고아 행이 쌓이지 않는다) — 이것도 실행으로 확인했다.

---

## 테스트 전략

| 층 | 방식 | 잡는 것 |
|---|---|---|
| 도메인 | 순수 단위 테스트 | 키 조립·정규화 규칙 |
| 모델 형상 | 설계 시점 EF 모델 검사 (DB 불필요) | 인덱스·CHECK 제약·삭제 동작·인덱스 키 크기 |
| 서비스 | 메모리 페이크 | 업무 규칙(검증·거부 조건) |
| 저장소 / 종단 | **SQLite in-memory + 실제 `DbContext`** | LINQ 번역 실패, 실제 배선 |

⚠ SQLite 는 SQL Server 가 아니다. 스키마 세부(인덱스 키 크기, `SET NULL` 등)는 모델 형상 계약이 담당하고,
SQLite 테스트는 쿼리가 실제로 번역·실행되는지를 본다.

또한 SQLite 는 `ORDER BY` 에서 `DateTimeOffset` 을 지원하지 않는다. 목록 화면은 전부 시각으로
정렬하므로 **테스트 쪽에서** 값 변환기를 얹어 흡수한다(`SqliteTestDatabase`) — 운영 프로바이더는
`datetimeoffset` 을 그대로 정렬하므로 **운영 코드는 손대지 않았다.**

### 아직 자동 검증되지 않는 것

- **화면의 정상 경로** — 컴포넌트 렌더 테스트(bUnit 등)가 없다. 라우팅·DI·오류 경로는 실기동으로
  확인했지만(모든 경로 HTTP 200 · 미처리 예외 0), "데이터가 있을 때 표가 제대로 그려지는가" 는
  사람이 눈으로 봐야 한다.
- **실제 SQL Server 에 마이그레이션 적용** — 이 개발 환경에 SQL Server 가 없다.

---

## 코딩 규칙

- C# 최신 문법 (`record`, `required`, nullable reference types)
- 모든 비동기 public API 는 `CancellationToken` 을 받는다
- 매직 넘버·매직 문자열 금지 — 상수는 `DomainConstants` 등 단일 출처에
- 주석은 **"왜"** 만 남긴다. 자명한 코드에는 주석을 달지 않는다
- Blazor 컴포넌트는 작게 유지, `DbContext` 는 Scoped 등록
- nullable 경고는 빌드 오류로 처리한다 (`Directory.Build.props`)
- NuGet 감사가 켜져 있다(`NuGetAuditMode=all`). **high/critical(NU1903/NU1904) 은 빌드 오류**,
  moderate 이하는 경고. 당장 고칠 수 없으면 해당 프로젝트에서 `<NoWarn>` 로 한시 해제하되 이유를 커밋에 남긴다
