---
name: testing
description: C#/.NET 프로젝트의 테스트 작성 및 실행 방법을 안내한다. 테스트케이스를 작성하거나 테스트를 실행하는 경우 이 스킬을 사용한다.
---

# 테스트 가이드

이 가이드는 C#/.NET 테스트를 작성하거나 실행할 때 사용한다. 작업 전에는 저장소의 실제 솔루션/프로젝트 구조를 먼저 확인하고, 확인되지 않은 정보는 추측하지 않는다.

## 공통 원칙

- 부정확한 정보가 있거나 실행 환경이 불명확하면 사용자에게 질문한다.
- 테스트를 실행할 때는 대상 `.sln` 또는 `.csproj`가 있는 디렉토리에서 실행한다.
- 실패한 테스트를 고칠 때는 실패 범위를 먼저 좁힌 뒤 관련 테스트만 재실행하고, 마지막에 가능한 전체 검증을 실행한다.
- 외부 서비스, 실제 DB, 실제 API, 파일 시스템, 시간, 난수에 의존하는 테스트는 mock, fake, in-memory provider, test double을 우선 사용한다.
- 테스트 때문에 생성된 임시 파일은 필요한 경우에만 정리하고, 사용자가 만든 파일을 임의로 삭제하지 않는다.
- 테스트 프레임워크(xUnit, NUnit, MSTest)는 기존 프로젝트가 사용하는 것을 따른다. 새로 도입해야 하면 사용자에게 먼저 확인한다.

## 프로젝트 구조 확인

먼저 솔루션과 프로젝트 파일을 찾는다.

```bash
find . -name "*.sln" -o -name "*.csproj"
```

테스트 프로젝트는 보통 다음 이름을 사용한다.

- `ProjectName.Tests`
- `ProjectName.UnitTests`
- `ProjectName.IntegrationTests`
- `tests/ProjectName.Tests`

테스트 프로젝트 여부는 `.csproj`의 패키지 참조로 확인한다.

- xUnit: `xunit`, `xunit.runner.visualstudio`
- NUnit: `NUnit`, `NUnit3TestAdapter`
- MSTest: `MSTest.TestFramework`, `MSTest.TestAdapter`
- Mocking: `Moq`, `NSubstitute`, `FakeItEasy`
- Assertion helper: `FluentAssertions`

## 테스트 실행

전체 테스트:

```bash
dotnet test
```

솔루션 단위 실행:

```bash
dotnet test path/to/App.sln
```

테스트 프로젝트 단위 실행:

```bash
dotnet test path/to/App.Tests/App.Tests.csproj
```

간결한 출력:

```bash
dotnet test --verbosity minimal
```

빌드가 이미 끝난 뒤 테스트만 실행:

```bash
dotnet test --no-build
```

## 테스트 범위 좁히기

특정 테스트 이름으로 필터링한다.

```bash
dotnet test --filter "FullyQualifiedName~CreateUser"
dotnet test --filter "Name=CreateUser_ReturnsCreated"
```

특정 클래스만 실행한다.

```bash
dotnet test --filter "FullyQualifiedName~UserServiceTests"
```

카테고리/트레이트 기반 실행:

```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "TestCategory=Integration"
```

필터 속성 이름은 테스트 프레임워크와 프로젝트 설정에 따라 다를 수 있으므로 기존 테스트의 attribute를 확인한다.

## 테스트 작성 위치

- 기존 테스트 프로젝트가 있으면 그 구조를 따른다.
- 새 테스트 프로젝트가 필요하면 사용자에게 확인한 뒤 생성한다.
- 테스트 파일명은 일반적으로 대상 클래스명에 `Tests`를 붙인다.

예시:

```text
src/App/UserService.cs
tests/App.Tests/UserServiceTests.cs
```

프로덕션 코드와 테스트 코드의 namespace는 기존 프로젝트 규칙을 따른다.

## xUnit 테스트 작성 방식

기존 프로젝트가 xUnit을 사용하면 `[Fact]`와 `[Theory]`를 사용한다.

```csharp
using Xunit;

public sealed class UserServiceTests
{
    [Fact]
    public void CreateUser_WithValidInput_ReturnsUser()
    {
        var service = new UserService();

        var result = service.CreateUser("tester@example.com");

        Assert.Equal("tester@example.com", result.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    public void CreateUser_WithInvalidEmail_Throws(string email)
    {
        var service = new UserService();

        Assert.Throws<ArgumentException>(() => service.CreateUser(email));
    }
}
```

비동기 코드는 `async Task` 테스트로 작성한다.

```csharp
[Fact]
public async Task GetUserAsync_WhenUserExists_ReturnsUser()
{
    var repository = new FakeUserRepository();
    var service = new UserService(repository);

    var result = await service.GetUserAsync(1);

    Assert.NotNull(result);
}
```

## NUnit 테스트 작성 방식

기존 프로젝트가 NUnit을 사용하면 `[Test]`, `[TestCase]`, `[SetUp]`을 사용한다.

```csharp
using NUnit.Framework;

[TestFixture]
public sealed class UserServiceTests
{
    private UserService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new UserService();
    }

    [Test]
    public void CreateUser_WithValidInput_ReturnsUser()
    {
        var result = _service.CreateUser("tester@example.com");

        Assert.That(result.Email, Is.EqualTo("tester@example.com"));
    }
}
```

## MSTest 테스트 작성 방식

기존 프로젝트가 MSTest를 사용하면 `[TestClass]`, `[TestMethod]`, `[DataTestMethod]`를 사용한다.

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class UserServiceTests
{
    [TestMethod]
    public void CreateUser_WithValidInput_ReturnsUser()
    {
        var service = new UserService();

        var result = service.CreateUser("tester@example.com");

        Assert.AreEqual("tester@example.com", result.Email);
    }
}
```

## 테스트 작성 규칙

- 테스트 이름은 조건과 기대 결과가 드러나게 작성한다.
- Arrange, Act, Assert 흐름을 유지한다.
- 한 테스트는 하나의 핵심 동작만 검증한다.
- private 메서드보다 public 동작을 기준으로 검증한다.
- 구현 세부사항보다 관찰 가능한 결과, 반환값, 상태 변화, 발생 이벤트를 검증한다.
- 예외 테스트는 예외 타입과 필요한 경우 메시지/상태를 함께 확인한다.
- 날짜/시간은 `DateTime.Now`에 직접 의존하지 말고 clock abstraction을 사용한다.
- 난수, GUID, 현재 사용자, 환경 변수는 주입 가능한 dependency로 분리한다.

## Mocking과 Test Double

외부 의존성은 인터페이스로 분리하고 mock 또는 fake를 사용한다.

```csharp
using Moq;
using Xunit;

public sealed class UserServiceTests
{
    [Fact]
    public async Task SendWelcomeEmailAsync_WhenUserExists_SendsEmail()
    {
        var emailSender = new Mock<IEmailSender>();
        var service = new UserService(emailSender.Object);

        await service.SendWelcomeEmailAsync("tester@example.com");

        emailSender.Verify(
            sender => sender.SendAsync("tester@example.com", It.IsAny<string>()),
            Times.Once);
    }
}
```

Mock 대상 예시:

- HTTP 클라이언트 또는 API wrapper
- DB repository
- 메시지 큐/이벤트 publisher
- 이메일/SMS/푸시 발송
- 파일 저장소
- 인증/권한 provider
- clock, random, ID generator

## ASP.NET Core 테스트

ASP.NET Core API나 MVC 앱은 가능하면 `WebApplicationFactory<TEntryPoint>`를 사용해 통합 테스트를 작성한다.

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

통합 테스트에서 실제 운영 DB나 외부 API를 호출하지 않는다. 필요한 경우 테스트용 configuration, in-memory DB, Testcontainers, fake service를 사용한다. Testcontainers 도입 여부는 기존 프로젝트 사용 여부를 먼저 확인한다.

## EF Core 테스트

EF Core 테스트는 목적에 따라 provider를 선택한다.

- 순수 도메인/서비스 테스트: repository mock 또는 fake
- 쿼리 동작 검증: SQLite in-memory provider
- DB별 동작 검증: Testcontainers 같은 실제 DB 기반 테스트

EF Core InMemory provider는 관계형 DB와 동작이 다를 수 있으므로 쿼리 정확성 검증에는 신중하게 사용한다.

## 검증 명령

테스트 전후로 필요한 경우 빌드와 포맷 검증을 실행한다.

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
```

`dotnet format`이 설치되어 있지 않거나 프로젝트에서 사용하지 않으면 강제로 도입하지 않는다.

## 실패 분석 순서

1. 실패한 테스트 이름과 에러 메시지를 확인한다.
2. 관련 테스트 파일과 대상 프로덕션 코드를 함께 읽는다.
3. 테스트가 잘못된 것인지, 구현이 잘못된 것인지 구분한다.
4. 관련 테스트만 재실행한다.
5. 수정 후 가능한 범위의 전체 테스트를 실행한다.

환경 문제, 누락된 secret, 외부 서비스 의존성처럼 로컬에서 확정할 수 없는 원인은 사용자에게 확인한다.
