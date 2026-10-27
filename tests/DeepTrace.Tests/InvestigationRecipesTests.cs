using DeepTrace.Core.Ai;
using DeepTrace.Indexing;
using TestLib;
using Xunit;

namespace DeepTrace.Tests;

public class InvestigationRecipesTests
{
    private readonly string _testDir;
    private readonly string _dbPath;
    private readonly SqliteIndexer _indexer;

    public InvestigationRecipesTests()
    {
        _testDir = Path.Combine(AppContext.BaseDirectory, $"deeptrace_recipe_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "test.db");

        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(TestLib.Greeter).Assembly.Location,
            dllPath,
            overwrite: true);

        _indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = _indexer.DiscoverAssemblies(_testDir)[0];
        _indexer.IndexAssembly(assembly);
    }

    [Fact]
    public void Recipes_List_ReturnsFiveRecipes()
    {
        var recipes = InvestigationRecipes.Recipes;
        Assert.Equal(5, recipes.Count);
    }

    [Fact]
    public void Recipes_NamesAreDistinct()
    {
        var names = InvestigationRecipes.Recipes.Select(r => r.Name).ToList();
        Assert.Equal(names.Distinct().Count(), names.Count);
    }

    [Fact]
    public void Recipes_Description_NotEmpty()
    {
        foreach (var recipe in InvestigationRecipes.Recipes)
        {
            Assert.NotEmpty(recipe.Description);
        }
    }

    [Fact]
    public void Recipes_Queries_NotEmpty()
    {
        foreach (var recipe in InvestigationRecipes.Recipes)
        {
            Assert.NotEmpty(recipe.Queries);
        }
    }

    [Fact]
    public void Execute_FindExtensionPoints_ReturnsResults()
    {
        var recipe = InvestigationRecipes.Recipes.First(r => r.Name == "Find Extension Points");
        var results = InvestigationRecipes.Execute(recipe, _indexer);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void Execute_SortsByRelevanceDescending()
    {
        var recipe = InvestigationRecipes.Recipes.First(r => r.Name == "Find Extension Points");
        var results = InvestigationRecipes.Execute(recipe, _indexer);

        for (var i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].RelevanceScore >= results[i].RelevanceScore);
        }
    }

    [Fact]
    public void Execute_DeduplicatesResults()
    {
        var recipe = InvestigationRecipes.Recipes.First(r => r.Name == "Find Extension Points");
        var results = InvestigationRecipes.Execute(recipe, _indexer);
        var ids = results.Select(r => r.SymbolId).ToList();
        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }

    [Fact]
    public void ExecuteAsText_ReturnsFormattedOutput()
    {
        var recipe = InvestigationRecipes.Recipes.First(r => r.Name == "Find Extension Points");
        var text = InvestigationRecipes.ExecuteAsText(recipe, _indexer);
        Assert.Contains($"Recipe: {recipe.Name}", text);
        Assert.Contains($"Description: {recipe.Description}", text);
        Assert.Contains("Results:", text);
    }

    [Fact]
    public void ExecuteWithNoResults_ReturnsEmpty()
    {
        var recipe = new InvestigationRecipe
        {
            Name = "Custom",
            Description = "Custom recipe",
            Queries = new[] { "zzzznonexistentzzz" },
            ResultLimit = 10,
        };
        var results = InvestigationRecipes.Execute(recipe, _indexer);
        Assert.Empty(results);
    }
}

