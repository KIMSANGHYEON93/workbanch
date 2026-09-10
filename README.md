# Workbench

내부용 이슈 추적 + 마크다운 지식 저장소. Microsoft Planner 를 대체하기 위해 자체 제작한다.

- **Backend / Frontend**: ASP.NET Core 8 + Blazor Server (C# 풀스택)
- **DB**: Azure SQL Database (로컬은 LocalDB / Docker SQL Server)
- **ORM**: Entity Framework Core
- **인증**: Microsoft Entra ID (Microsoft.Identity.Web) — 개발 중에는 우회 옵션 제공
- **파일**: Azure Blob Storage (로컬은 Azurite)
- **마크다운**: Markdig 렌더링

---

## 진행 현황

| # | 단계 | 상태 |
|---|------|------|
| 1 | 솔루션 · 프로젝트 생성 + 기본 설정 | ✅ 완료 |
| 2 | Domain 엔티티 + Enum | ✅ 완료 |
| 3 | Infrastructure: DbContext + EF Core 마이그레이션 | ⬜ |
| 4 | 인증 (Entra ID) 연동 + 개발용 우회 | ⬜ |
| 5 | Project CRUD | ⬜ |
| 6 | Issue CRUD + 상태 변경 + 리스트 | ⬜ |
| 7 | 마크다운 에디터 컴포넌트 | ⬜ |
| 8 | Page CRUD + 계층 구조 | ⬜ |
| 9 | Comment | ⬜ |
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

---

## 개발 환경

### 요구 사항

- .NET 8 SDK
- SQL Server LocalDB 또는 Docker SQL Server *(3단계부터 필요)*
- Azurite 또는 개발용 Azure Storage 계정 *(10단계부터 필요)*

### 빌드 · 테스트 · 실행

```bash
cd Workbench

dotnet build
dotnet test
dotnet run --project src/Workbench.Web
```

Entra ID 앱 등록은 아직 필요 없다. 4단계에서 개발용 인증 우회 스위치를 추가한다.

### 설정값

접속 문자열·시크릿은 `appsettings.json` 에 커밋하지 않는다. User Secrets 를 쓴다.

```bash
dotnet user-secrets init --project src/Workbench.Web
dotnet user-secrets set "ConnectionStrings:Workbench" "<값>" --project src/Workbench.Web
```

---

## 코딩 규칙

- C# 최신 문법 (`record`, `required`, nullable reference types)
- 모든 비동기 public API 는 `CancellationToken` 을 받는다
- 매직 넘버·매직 문자열 금지 — 상수는 `DomainConstants` 등 단일 출처에
- 주석은 **"왜"** 만 남긴다. 자명한 코드에는 주석을 달지 않는다
- Blazor 컴포넌트는 작게 유지, `DbContext` 는 Scoped 등록
- nullable 경고는 빌드 오류로 처리한다 (`Directory.Build.props`)
