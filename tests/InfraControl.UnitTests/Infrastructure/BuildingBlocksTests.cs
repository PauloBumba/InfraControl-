using FluentAssertions;
using InfraControl.BuildingBlocks.Application;

namespace InfraControl.UnitTests.Infrastructure;

public sealed class BuildingBlocksTests
{
    [Fact]
    public void BaseCommand_ShouldExposeEmptyCacheInvalidationByDefault()
    {
        var command = new TestCommand("value");

        command.CacheKeysToInvalidate.Should().BeEmpty();
    }

    [Fact]
    public void BaseQuery_ShouldProvideFiveMinuteCacheDuration()
    {
        var query = new TestQuery("value");

        query.CacheKey.Should().Be(nameof(TestQuery));
        query.CacheDuration.Should().Be(TimeSpan.FromMinutes(5));
        query.CacheTags.Should().BeEmpty(
            "a opt-in de tags é por query: o default vazio não registra índice nenhum");
    }

    [Fact]
    public void CacheKeyBuilder_ShouldBeStableAndUserScoped()
    {
        var request = new TestQuery("value");
        var firstUser = new TestUser(Guid.Parse("11111111-1111-1111-1111-111111111111"), ["Admin", "User"]);
        var sameUser = new TestUser(firstUser.UserId, ["User", "Admin"]);
        var otherUser = new TestUser(Guid.Parse("22222222-2222-2222-2222-222222222222"), ["Admin", "User"]);

        var first = CacheKeyBuilder.Build(nameof(TestQuery), request, firstUser);
        var same = CacheKeyBuilder.Build(nameof(TestQuery), request, sameUser);
        var other = CacheKeyBuilder.Build(nameof(TestQuery), request, otherUser);

        first.Should().Be(same);
        other.Should().NotBe(first);
        CacheKeyBuilder.TagIndexKey("identity:user").Should().Be("tagidx:identity:user");
    }

    private sealed record TestCommand(string Value) : BaseCommand<string>;
    private sealed record TestQuery(string Value) : BaseQuery<string>;

    private sealed class TestUser(Guid? userId, IReadOnlyCollection<string> roles) : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
        public string? UserName => "test@example.com";
        public bool IsAuthenticated => UserId.HasValue;
        public IReadOnlyCollection<string> Roles { get; } = roles;
    }
}
