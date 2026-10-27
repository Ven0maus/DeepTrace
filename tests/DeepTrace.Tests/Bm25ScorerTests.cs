using DeepTrace.Core.Models;
using DeepTrace.Indexing;
using Xunit;

namespace DeepTrace.Tests;

public class Bm25ScorerTests
{
    [Fact]
    public void Score_ExactNameMatch_GetsBoost()
    {
        var result = new SearchResult { Name = "Greet", FullyQualifiedName = "TestLib.Greeter.Greet", Signature = "Greet(System.String) : System.String" };
        var score = Bm25Scorer.Score(result, "Greet");
        Assert.True(score > 10.0); // exact match boost of 10.0
    }

    [Fact]
    public void Score_PrefixMatch_GetsBoost()
    {
        var result = new SearchResult { Name = "Greetings", FullyQualifiedName = "TestLib.Greeter.Greetings" };
        var score = Bm25Scorer.Score(result, "Greet");
        Assert.True(score >= 3.0); // prefix match boost of 3.0
    }

    [Fact]
    public void Score_EmptyName_ReturnsZero()
    {
        var result = new SearchResult { Name = "", FullyQualifiedName = "TestLib.Greeter.Greet" };
        var score = Bm25Scorer.Score(result, "Greet");
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void Score_EmptyQuery_ReturnsZero()
    {
        var result = new SearchResult { Name = "Greet", FullyQualifiedName = "TestLib.Greeter.Greet" };
        var score = Bm25Scorer.Score(result, "");
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void Score_NameTermMatch_ScoringApplied()
    {
        var result = new SearchResult { Name = "Greet", FullyQualifiedName = "TestLib.Greeter.Greet" };
        var score = Bm25Scorer.Score(result, "greet");
        Assert.True(score > 0.0);
    }

    [Fact]
    public void Score_MultipleTerms_RankedHigher()
    {
        var result1 = new SearchResult { Name = "Greet", FullyQualifiedName = "TestLib.Greeter.Greet" };
        var result2 = new SearchResult { Name = "SomethingElse", FullyQualifiedName = "TestLib.Other.SomethingElse" };

        var score1 = Bm25Scorer.Score(result1, "Greet");
        var score2 = Bm25Scorer.Score(result2, "Greet");

        Assert.True(score1 > score2);
    }

    [Fact]
    public void ScoreSignature_ContributesToScore()
    {
        var result = new SearchResult { Name = "Greet", FullyQualifiedName = "TestLib.Greeter.Greet", Signature = "Greet(System.String) : System.String" };
        var score = Bm25Scorer.Score(result, "String");
        Assert.True(score > 0.0);
    }

    [Fact]
    public void ScoreSummary_NonEmpty_ReturnsScore()
    {
        var score = Bm25Scorer.ScoreSummary("Greets a player by name", "Greets");
        Assert.True(score > 0.0);
    }

    [Fact]
    public void ScoreSummary_EmptySummary_ReturnsZero()
    {
        var score = Bm25Scorer.ScoreSummary(null, "Greets");
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void ScoreSummary_EmptyQuery_ReturnsZero()
    {
        var score = Bm25Scorer.ScoreSummary("Greets a player", "");
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void Score_StopwordsFilteredFromQuery()
    {
        // Query composed entirely of stopwords — all query terms filtered, no term score, no exact/prefix boost
        var result = new SearchResult { Name = "Greet", FullyQualifiedName = "TestLib.Greeter.Greet" };
        var score = Bm25Scorer.Score(result, "load data get set");
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void Score_StopwordsFilteredFromDocument()
    {
        // Document name contains a stopword — should not boost
        var result = new SearchResult { Name = "GetPlayer", FullyQualifiedName = "TestLib.GetPlayer" };
        var score = Bm25Scorer.Score(result, "get");
        // "get" is a stopword, so no term match score; no exact/prefix boost either
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void Score_PreservesDomainTerms()
    {
        var result = new SearchResult { Name = "Vector3", FullyQualifiedName = "TestLib.Vector3" };
        var score = Bm25Scorer.Score(result, "Vector3");
        Assert.True(score > 10.0); // exact match boost
    }
}
