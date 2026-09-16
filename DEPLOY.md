# DEPLOY.md — IIS 배포

이 문서는 Workbench 를 **Windows Server + IIS**에 올릴 때 필요한 것만 다룬다.
"어떻게 짜여 있는가"는 [`README.md`](README.md), "어떻게 작업하는가"는 [`CLAUDE.md`](CLAUDE.md) 가 담는다 — 여기서는 배포만 다룬다.

> 아래 값·경로는 `main` 기준으로 직접 확인한 것이다(`dotnet publish` 실행, `Program.cs`·`DependencyInjection.cs`·`FileStorageExtensions.cs`·`AuthenticationExtensions.cs` 직접 읽음). CLAUDE.md 의 원칙대로, 문서화 이후 코드가 바뀌면 이 문서가 먼저 낡는다 — 배포 전에 아래 스크립트로 다시 확인하라.

---

## 0. 마이그레이션은 사람이 먼저 돌린다

`Program.cs` 에 `Migrate()` 호출이 **없다.** SQL Server 구성에서는 기동이 스키마를 건드리지 않는다
(SQLite 데모 모드에서만 `InitializeDemoDatabaseAsync` 가 `EnsureCreated` 로 스키마를 세운다 — 그마저
SQL Server 에서는 재현되지 않는 방식이라 마이그레이션 대체가 안 된다).

**최초 배포·스키마 변경 배포 전에 반드시:**

```powershell
dotnet ef database update `
  --project src\Workbench.Infrastructure `
  --startup-project src\Workbench.Web `
  --connection "<SQL Server 접속 문자열>"
```

앱을 먼저 띄우고 마이그레이션을 나중에 돌리면, 그 사이 요청은 없는 테이블/컬럼에 부딪혀 500 을 낸다.
**순서: 마이그레이션 → 앱 시작.**

---

## 1. 서버 사전 조건

| 항목 | 비고 |
|---|---|
| **.NET 8 Hosting Bundle** | ANCM v2 포함. 없으면 `500.19`(설정 파서가 `aspNetCore` 핸들러를 못 찾음) |
| **IIS 역할 서비스 — WebSocket Protocol** | Blazor Server 라 필수. 없으면 자동으로 **long polling 으로 조용히 강등** — 에러 없이 느려지기만 해서 원인 추적이 어렵다 |
| **앱 풀**: 관리 코드 없음(No Managed Code) | .NET 8 은 IIS 의 CLR 관리 기능을 쓰지 않는다. "통합" 파이프라인 모드 |

`preflight-iis.ps1`(아래 3절)의 1단계가 이 셋을 기동 전에 확인한다.

---

## 2. 설정 — appsettings.json 에 넣지 말 것

저장소의 `appsettings.json`·`appsettings.Development.json` 에는 운영 시크릿이 없다
(`AzureAd:TenantId`/`ClientId` 는 빈 문자열, `ClientSecret` 자리 자체가 없음, `ConnectionStrings`/`Database`
섹션은 `appsettings.Development.json` 에만 있고 SQLite 를 가리킨다). **User Secrets 는 개발 전용이고 운영
바이너리에 포함되지 않으므로, 운영 값은 환경변수 또는 IIS 관리자의 "구성 편집기"로 주입한다.**
IIS 는 `__`(더블 언더스코어)를 `:` 로 치환해 .NET 설정 바인더에 넘긴다.

| 환경변수 | 대응 코드 | 필수 |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT=Production` | `IHostEnvironment.IsDevelopment()` 가드 3개 전부 이걸로 판정 | ✅ |
| `ConnectionStrings__Workbench` | `DependencyInjection.cs` — 비어 있으면 기동 자체가 예외로 막힘 | ✅ |
| `Database__Provider=SqlServer` | 같은 곳 — `Sqlite` 인 채 Production 이면 기동 거부(탈출구 없음) | ✅ |
| `Authentication__Mode=EntraId` | `AuthenticationExtensions.cs` — `Development` 인 채 Production 이면 기동 거부 | ✅ |
| `AzureAd__TenantId` / `AzureAd__ClientId` / `AzureAd__ClientSecret` | `Microsoft.Identity.Web` (`AddMicrosoftIdentityWebApp`) | ✅ |
| `FileStorage__Provider=AzureBlob` | `FileStorageExtensions.cs` | ✅ (또는 아래 예외) |
| `FileStorage__ConnectionString` | 같은 곳 — Blob 을 쓰는 한 필수 | ✅ (Blob 일 때) |

**`FileStorage__Provider=Local` 로 운영을 돌리고 싶다면** (단일 서버·스케일아웃 없음을 의도적으로 받아들이는 경우에만)
`FileStorage__AllowLocalOutsideDevelopment=true` 를 명시해야 기동이 막히지 않는다. 기본값은 거부다 — 재배포·
스케일아웃에서 첨부가 조용히 사라지는 것을 막기 위한 설계다.

### Entra ID 리디렉션 URI

`appsettings.json` 의 `AzureAd:CallbackPath=/signin-oidc`, `SignedOutCallbackPath=/signout-callback-oidc` 가
그대로 URL 경로가 된다. **IIS 가상 디렉터리(서브패스) 아래 올리면 이 경로 앞에 그 서브패스가 붙는다** —
앱 등록의 리디렉션 URI 를 실제 배포 경로와 정확히 맞춰야 한다. 사이트 루트(`/`)에 바인딩하는 것이 가장 덜
헷갈린다.

---

## 3. Blazor Server 라서 생기는 IIS 함정 4가지

| 함정 | 증상 | 조치 |
|---|---|---|
| **WebSocket 기능 미설치** | 에러 없이 long polling 으로 강등, 그냥 느려짐 | 1절의 역할 서비스 설치 확인 |
| **앱 풀 유휴 시간 초과(기본 20분) · 정기 재활용(기본 29시간)** | 회로가 끊기고 사용자가 "연결이 끊어졌습니다" 오버레이를 본다 | 유휴 시간 초과를 0(비활성) 또는 충분히 길게, 정기 재활용을 야간 고정 시각으로 변경 |
| **웹팜·다중 워커 프로세스** | 같은 회로 요청이 다른 프로세스로 가면 즉시 끊김 | sticky session(ARR Affinity 등) 필수 — 회로는 프로세스에 묶여 있다 |
| **ARR 리버스 프록시 타임아웃(기본 120초)** | 오래 열려 있는 SignalR 연결이 타임아웃으로 끊김 | ARR 을 쓴다면 프록시 타임아웃을 늘린다 |

---

## 4. 첨부 업로드 — IIS 요청 상한은 사실 이 경로에 안 걸린다 (그러나 명시는 해 둔다)

`AttachmentOptions.cs:11` 의 상한은 **25MB**(`25 * 1024 * 1024`, `appsettings.json` 의
`Attachments:MaxFileSizeBytes=26214400` 와 동일 값 — 화면 문구와 서버 검증이 이 값 하나를 같이 본다).

업로드 경로를 코드로 직접 추적한 결과: `AttachmentPanel.razor` 의 `<InputFile>` 은 `OpenReadStream`으로
Blazor **회로(SignalR)** 위에서 청크 단위로 스트리밍된다 — 평범한 HTTP `multipart/form-data` POST 가
아니다. IIS 의 `requestLimits/maxAllowedContentLength`, ASP.NET Core Kestrel 의 `MaxRequestBodySize` 는
**HTTP 요청 본문**에 적용되는 값이라 이 업로드 경로에는 해당하지 않는다. 다운로드(`/attachments/{id}`,
`AttachmentEndpoints.cs`)도 응답이라 같은 제약을 받지 않는다.

**즉 25MB 첨부에서 413 이 나는 시나리오는 지금 이 앱의 업로드 경로에서는 실측상 재현되지 않는다.**
다만 이후 HTTP POST 기반 업로드 엔드포인트가 추가되거나, `Microsoft.Identity.Web.UI` 가 붙이는
로그인 관련 폼 처리 등 다른 경로를 위한 방어적 여유값으로 아래 값을 web.config 에 같이 적어 둔다 —
없어도 지금 당장 깨지진 않지만, 있어서 나쁠 것도 없다.

---

## 5. web.config

`dotnet publish` 가 자동으로 만드는 기본 조각은 이것이다(직접 `dotnet publish -c Release` 로 확인):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" arguments=".\Workbench.Web.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess" />
    </system.webServer>
  </location>
</configuration>
```

운영에 필요한 조각을 더한 버전은 `deploy/web.config.snippet.xml` 에 있다 — 환경변수 주입,
`stdoutLogEnabled` 활성화(초기 500 진단용), 4절에서 말한 방어적 요청 상한을 담는다. **비밀값은 여기
직접 적지 않는다** — IIS 관리자 "구성 편집기"로 넣거나, `<environmentVariables>` 블록은 값 없이 키만
남겨 두고 서버에서 채운다.

---

## 6. 배포 순서

1. `deploy\preflight-iis.ps1` 실행 — 사전 조건 3개 확인
2. `dotnet ef database update` — 0절, 마이그레이션 먼저
3. `dotnet publish -c Release -o <배포 경로>`
4. IIS 사이트/앱 풀 등록, `web.config` 병합(5절), 환경변수 주입(2절)
5. `deploy\preflight-iis.ps1 -SmokeTestUrl https://<호스트>` 로 스모크 테스트

`preflight-iis.ps1` 은 이 5단계를 하나로 묶는다 — 문서를 읽고 사람이 순서를 외우는 대신, 스크립트가
먼저 잡아준다.

---

## 7. 아직 이 문서가 다루지 않는 것

- **무중단 배포**(블루/그린, 앱 풀 워커 프로세스 재활용 중 회로 보존) — 지금은 배포 중 접속자의 회로가
  끊기는 것을 전제로 한다.
- **로그 수집**(`stdout` 파일 로그 외의 중앙화) — `web.config` 의 `stdoutLogFile` 은 초기 진단용이며
  장기 운영 로그 전략이 아니다.
- **역방향 프록시로 ARR 대신 무엇을 쓸지**(YARP 등) — 3절의 ARR 타임아웃 항목은 ARR 을 쓴다는 전제다.
