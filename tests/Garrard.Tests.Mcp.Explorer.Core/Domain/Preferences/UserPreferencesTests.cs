using Garrard.Mcp.Explorer.Core.Domain.Chat;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Domain.LlmModels;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Garrard.Mcp.Explorer.Core.Domain.Workflows;

namespace Garrard.Tests.Mcp.Explorer.Core.Domain.Preferences;

public class UserPreferencesTests
{
    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void DefaultConstruction_Theme_IsCommandDark()
    {
        var prefs = new UserPreferences();

        Assert.Equal("command-dark", prefs.Theme);
    }

    [Fact]
    public void DefaultConstruction_ConnectionSortOrder_IsName()
    {
        var prefs = new UserPreferences();

        Assert.Equal("name", prefs.ConnectionSortOrder);
    }

    [Fact]
    public void DefaultConstruction_ShowConnectionGroups_IsTrue()
    {
        var prefs = new UserPreferences();

        Assert.True(prefs.ShowConnectionGroups);
    }

    [Fact]
    public void DefaultConstruction_AllCollections_AreEmpty()
    {
        var prefs = new UserPreferences();

        Assert.Empty(prefs.Connections);
        Assert.Empty(prefs.ConnectionGroups);
        Assert.Empty(prefs.FavoriteConnections);
        Assert.Empty(prefs.FavoriteTools);
        Assert.Empty(prefs.FavoritePrompts);
        Assert.Empty(prefs.FavoriteResources);
        Assert.Empty(prefs.FavoriteResourceTemplates);
        Assert.Empty(prefs.LlmModels);
        Assert.Empty(prefs.Workflows);
        Assert.Empty(prefs.WorkflowExecutions);
        Assert.Empty(prefs.ChatSessions);
    }

    [Fact]
    public void DefaultConstruction_NullableFields_AreNull()
    {
        var prefs = new UserPreferences();

        Assert.Null(prefs.SelectedConnectionName);
        Assert.Null(prefs.SelectedLlmModelName);
    }

    [Fact]
    public void DefaultConstruction_BooleanFlags_AreFalse()
    {
        var prefs = new UserPreferences();

        Assert.False(prefs.ShowFavoritesFirst);
        Assert.False(prefs.ShowPromptFavoritesFirst);
        Assert.False(prefs.ShowResourceFavoritesFirst);
        Assert.False(prefs.ShowResourceTemplateFavoritesFirst);
        Assert.False(prefs.ShowConnectionTimestamps);
    }

    // ── SensitiveFieldConfiguration ───────────────────────────────────────────

    [Fact]
    public void SensitiveFieldConfig_DefaultConstruction_IsNotNull()
    {
        var prefs = new UserPreferences();

        Assert.NotNull(prefs.SensitiveFieldConfig);
    }

    [Fact]
    public void SensitiveFieldConfig_DefaultConstruction_AiDetectionIsDisabled()
    {
        var config = new SensitiveFieldConfiguration();

        Assert.False(config.UseAiDetection);
        Assert.False(config.ShowDetectionDebug);
    }

    [Fact]
    public void SensitiveFieldConfig_DefaultStrictness_IsBalanced()
    {
        var config = new SensitiveFieldConfiguration();

        Assert.Equal(AiDetectionStrictness.Balanced, config.AiStrictness);
    }

    [Fact]
    public void SensitiveFieldConfig_DefaultFieldLists_AreEmpty()
    {
        var config = new SensitiveFieldConfiguration();

        Assert.Empty(config.AdditionalSensitiveFields);
        Assert.Empty(config.AllowedFields);
    }

    // ── Theme property ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("command-dark")]
    [InlineData("command-light")]
    [InlineData("ocean-dark")]
    [InlineData("ocean-light")]
    [InlineData("forest-dark")]
    [InlineData("forest-light")]
    public void Theme_AcceptsAllSixThemeIds(string themeId)
    {
        var prefs = new UserPreferences { Theme = themeId };

        Assert.Equal(themeId, prefs.Theme);
    }

    // ── Record with-expression ────────────────────────────────────────────────

    [Fact]
    public void WithExpression_ProducesNewInstance_WithUpdatedTheme()
    {
        var original = new UserPreferences();
        var updated = original with { Theme = "ocean-dark" };

        Assert.Equal("command-dark", original.Theme);
        Assert.Equal("ocean-dark", updated.Theme);
        Assert.NotSame(original, updated);
    }

    [Fact]
    public void WithExpression_AddingConnection_DoesNotMutateOriginal()
    {
        var original = new UserPreferences();
        var conn = new ConnectionDefinition { Name = "new-server" };
        var updated = original with { Connections = [..original.Connections, conn] };

        Assert.Empty(original.Connections);
        Assert.Single(updated.Connections);
    }

    // ── Equality (record semantics) ───────────────────────────────────────────

    [Fact]
    public void TwoDefaultPreferences_HaveSameScalarValues()
    {
        var p1 = new UserPreferences();
        var p2 = new UserPreferences();

        // Records with List<T> fields compare by reference, so we verify the scalar defaults
        Assert.Equal(p1.Theme, p2.Theme);
        Assert.Equal(p1.ConnectionSortOrder, p2.ConnectionSortOrder);
        Assert.Equal(p1.ShowConnectionGroups, p2.ShowConnectionGroups);
        Assert.Equal(p1.ShowFavoritesFirst, p2.ShowFavoritesFirst);
    }

    [Fact]
    public void PreferencesWithDifferentTheme_AreNotEqual()
    {
        var p1 = new UserPreferences { Theme = "command-dark" };
        var p2 = new UserPreferences { Theme = "ocean-dark" };

        Assert.NotEqual(p1, p2);
    }
}
