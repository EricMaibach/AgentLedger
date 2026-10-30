using System.Net;
using System.Security.Cryptography;
using System.Text;
using AgentLedger.FunctionalTests.ApiEndpoints;
using AgentLedger.FunctionalTests.Authentication;

namespace AgentLedger.FunctionalTests.Cli;

// The ledger serves the CLI and its installers (ADR 0024), to anyone: a machine being connected has no key yet.
public sealed class InstallScriptTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  [Fact]
  public async Task TheShellInstallerCarriesTheLedgersAddress()
  {
    var response = await factory.CreateBrowser().GetAsync("/install.sh");

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    response.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
    var script = await response.Content.ReadAsStringAsync();
    script.ShouldStartWith("#!/bin/sh");
    script.ShouldContain("LEDGER_URL=\"http://localhost\"");
    script.ShouldNotContain("__AGENTLEDGER_URL__");
    script.ShouldNotContain("\r"); // runs under sh: Unix line endings only
  }

  [Fact]
  public async Task ThePowerShellInstallerCarriesTheLedgersAddress()
  {
    var script = await (await factory.CreateBrowser().GetAsync("/install.ps1")).Content.ReadAsStringAsync();

    script.ShouldContain("$LedgerUrl = 'http://localhost'");
    script.ShouldNotContain("__AGENTLEDGER_URL__");
  }

  [Fact]
  public async Task WithoutBinariesSaysWhereTheyComeFrom()
  {
    // Running from source: binaries are only in released images.
    var response = await factory.CreateBrowser().GetAsync("/cli/linux-x64");

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    (await response.Content.ReadAsStringAsync()).ShouldContain("released");
  }

  [Fact]
  public async Task EveryAnswerCarriesTheLedgersVersion()
  {
    // What a connected CLI follows when it updates itself (ADR 0024).
    var ingest = await factory.CreateIngestClientAsync();

    var response = await ingest.PostAsync("/events", new StringContent(TestEnvelope.Json(), Encoding.UTF8, "application/json"));

    response.Headers.GetValues("X-AgentLedger-Version").Single().ShouldBe(Web.BuildInfo.Version);
  }
}

// A folder of pretend binaries, shared by the test class like its app (which reads Cli:Directory once).
public sealed class CliDirectoryFixture : IDisposable
{
  public static readonly byte[] Binary = Encoding.UTF8.GetBytes("pretend this is the linux-x64 CLI");

  public string Path { get; } = Directory.CreateTempSubdirectory("agentledger-cli-").FullName;

  public CliDirectoryFixture()
  {
    Directory.CreateDirectory(System.IO.Path.Combine(Path, "linux-x64"));
    File.WriteAllBytes(System.IO.Path.Combine(Path, "linux-x64", "agentledger"), Binary);
    Directory.CreateDirectory(System.IO.Path.Combine(Path, "win-x64"));
    File.WriteAllBytes(System.IO.Path.Combine(Path, "win-x64", "agentledger.exe"), Binary);
  }

  public void Dispose() => Directory.Delete(Path, recursive: true);
}

public sealed class CliBinaryTests : IClassFixture<CustomWebApplicationFactory<Program>>, IClassFixture<CliDirectoryFixture>
{
  private static readonly byte[] Binary = CliDirectoryFixture.Binary;
  private readonly CustomWebApplicationFactory<Program> _factory;

  public CliBinaryTests(CustomWebApplicationFactory<Program> factory, CliDirectoryFixture directory)
  {
    _factory = factory;
    _factory.UseSetting("Cli:Directory", directory.Path);
  }

  [Fact]
  public async Task ServesABinaryToAnyone()
  {
    var response = await _factory.CreateBrowser().GetAsync("/cli/linux-x64");

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    response.Content.Headers.ContentType!.MediaType.ShouldBe("application/octet-stream");
    (await response.Content.ReadAsByteArrayAsync()).ShouldBe(Binary);
  }

  [Fact]
  public async Task ServesTheWindowsBinaryUnderItsOwnName()
  {
    var response = await _factory.CreateBrowser().GetAsync("/cli/win-x64");

    response.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldBe("agentledger.exe");
  }

  [Fact]
  public async Task PublishesEachBinarysChecksum()
  {
    var checksum = await (await _factory.CreateBrowser().GetAsync("/cli/linux-x64.sha256")).Content.ReadAsStringAsync();

    checksum.ShouldBe($"{Convert.ToHexStringLower(SHA256.HashData(Binary))}  agentledger\n");
  }

  [Theory]
  [InlineData("/cli/plan9-x64")]
  [InlineData("/cli/osx-arm64")] // a known platform whose binary isn't there
  [InlineData("/cli/..%2F..%2Fappsettings.json")]
  public async Task AnswersNotFoundForAnythingElse(string path)
  {
    (await _factory.CreateBrowser().GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }
}
