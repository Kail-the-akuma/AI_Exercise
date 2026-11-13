using Xunit;
namespace IdeaBoard.E2E.Fixtures;

/// <summary>
/// Puts every scenario in one collection, which is what makes them run one after another.
///
/// The suite is deliberately not parallel. State is reset before each scenario against a
/// single shared database, and two scenarios running at once would reset each other's.
/// With six scenarios against one browser the time saved would not pay for the flakiness.
/// </summary>
[CollectionDefinition(Name)]
public class AppHostCollection : ICollectionFixture<AppHostFixture>
{
    /// <summary>Name scenarios refer to the collection by.</summary>
    public const string Name = "IdeaBoard end to end";
}
