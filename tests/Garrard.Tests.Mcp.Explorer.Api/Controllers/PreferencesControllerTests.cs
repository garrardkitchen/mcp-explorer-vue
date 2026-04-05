using Garrard.Mcp.Explorer.Api.Controllers.v1;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Api.Controllers;

/// <summary>
/// Unit tests for <see cref="PreferencesController"/>.
/// The store is mocked so tests are fully isolated from I/O.
/// </summary>
public class PreferencesControllerTests
{
    private readonly Mock<IUserPreferencesStore> _storeMock;
    private readonly PreferencesController _sut;

    public PreferencesControllerTests()
    {
        _storeMock = new Mock<IUserPreferencesStore>();
        _sut = new PreferencesController(_storeMock.Object);
    }

    // ── GET /preferences ──────────────────────────────────────────────────────

    [Fact]
    public async Task Get_Returns200WithPreferences()
    {
        var prefs = new UserPreferences { Theme = "ocean-dark" };
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(prefs);

        var result = await _sut.Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        Assert.Equal(prefs, ok.Value);
    }

    [Fact]
    public async Task Get_CallsStoreLoadOnce()
    {
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences());

        await _sut.Get(CancellationToken.None);

        _storeMock.Verify(s => s.LoadAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GET /preferences/theme ────────────────────────────────────────────────

    [Fact]
    public async Task GetTheme_Returns200WithCurrentTheme()
    {
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences { Theme = "forest-dark" });

        var result = await _sut.GetTheme(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        // The anonymous object contains a "theme" property
        var value = ok.Value!;
        var theme = value.GetType().GetProperty("theme")?.GetValue(value) as string;
        Assert.Equal("forest-dark", theme);
    }

    // ── PUT /preferences/theme ────────────────────────────────────────────────

    [Fact]
    public async Task SetTheme_ValidTheme_Returns200()
    {
        var request = new UpdateThemeRequest("ocean-light");
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences());

        var result = await _sut.SetTheme(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task SetTheme_ValidTheme_SavesThemeToStore()
    {
        var request = new UpdateThemeRequest("command-light");
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences());

        await _sut.SetTheme(request, CancellationToken.None);

        _storeMock.Verify(
            s => s.SaveAsync(
                It.Is<UserPreferences>(p => p.Theme == "command-light"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetTheme_ValidTheme_ReturnsThemeInResponseBody()
    {
        var request = new UpdateThemeRequest("forest-light");
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences());

        var result = await _sut.SetTheme(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value!;
        var theme = value.GetType().GetProperty("theme")?.GetValue(value) as string;
        Assert.Equal("forest-light", theme);
    }

    [Fact]
    public async Task SetTheme_ValidTheme_DoesNotCallSaveWithOldTheme()
    {
        var request = new UpdateThemeRequest("ocean-dark");
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences { Theme = "command-dark" });

        await _sut.SetTheme(request, CancellationToken.None);

        // Ensure the saved preferences carry the NEW theme, not the old one
        _storeMock.Verify(
            s => s.SaveAsync(
                It.Is<UserPreferences>(p => p.Theme != "command-dark"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Validation behaviour for UpdateThemeRequest ───────────────────────────
    // The [Required, MinLength(1)] attribute on Theme means the [ApiController] pipeline
    // returns 400 before the action runs when ModelState is invalid.
    // Here we verify the request record validation attributes are present.

    [Fact]
    public void UpdateThemeRequest_ThemeProperty_HasRequiredAttribute()
    {
        // Attributes on primary constructor parameters are on the parameter, not the property.
        var param = typeof(UpdateThemeRequest).GetConstructors()[0]
            .GetParameters()
            .First(p => p.Name == "Theme");
        var required = param.GetCustomAttributes(
            typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), inherit: false);

        Assert.NotEmpty(required);
    }

    [Fact]
    public void UpdateThemeRequest_ThemeProperty_HasMinLengthAttribute()
    {
        var param = typeof(UpdateThemeRequest).GetConstructors()[0]
            .GetParameters()
            .First(p => p.Name == "Theme");
        var minLen = param.GetCustomAttributes(
            typeof(System.ComponentModel.DataAnnotations.MinLengthAttribute), inherit: false);

        Assert.NotEmpty(minLen);
    }

    // ── GET /preferences/sensitive-fields ────────────────────────────────────

    [Fact]
    public async Task GetSensitiveFields_Returns200WithConfig()
    {
        var config = new SensitiveFieldConfiguration
        {
            UseAiDetection = true,
            AiStrictness = AiDetectionStrictness.Aggressive
        };
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences { SensitiveFieldConfig = config });

        var result = await _sut.GetSensitiveFields(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<SensitiveFieldConfiguration>(ok.Value);
        Assert.True(returned.UseAiDetection);
        Assert.Equal(AiDetectionStrictness.Aggressive, returned.AiStrictness);
    }

    // ── PUT /preferences/sensitive-fields ────────────────────────────────────

    [Fact]
    public async Task SetSensitiveFields_SavesConfigToStore()
    {
        var config = new SensitiveFieldConfiguration { UseAiDetection = false };
        _storeMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new UserPreferences());

        var result = await _sut.SetSensitiveFields(config, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        _storeMock.Verify(
            s => s.SaveAsync(
                It.Is<UserPreferences>(p => p.SensitiveFieldConfig == config),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
