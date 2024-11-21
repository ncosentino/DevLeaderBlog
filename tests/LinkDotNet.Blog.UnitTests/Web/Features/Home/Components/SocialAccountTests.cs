using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using LinkDotNet.Blog.Domain;
using LinkDotNet.Blog.Web.Features.Home.Components;

namespace LinkDotNet.Blog.UnitTests.Web.Features.Home.Components;

public class SocialAccountTests : BunitContext
{
    [Theory]
    [ClassData(typeof(SocialAccountTheoryData))]
    public void ShouldDisplaySocialAccountsOnlyWhenConfigured(
        Social social,
        Dictionary<string, bool> expectedVisibility)
    {
        var cut = Render<SocialAccounts>(s => s.Add(p => p.Social, social));

        foreach (var (iconId, shouldBeVisible) in expectedVisibility)
        {
            cut.FindAll($"#{iconId}").Any().ShouldBe(
                shouldBeVisible,
                $"Social icon #{iconId} visibility should be {shouldBeVisible}");
        }
    }
    
    private record SocialTestCase(
        string Name,
        string? Url,
        string IconId);

    private class SocialAccountTheoryData : TheoryData<Social, Dictionary<string, bool>>
    {
        [SuppressMessage("Design", "S1144: Unused private types or members should be removed", Justification = "Used by xUnit")]
        public SocialAccountTheoryData()
        {
            var testCases = new[]
            {
                new SocialTestCase("LinkedIn", "https://linkedin.com", "linkedin"),
                new SocialTestCase("GitHub", "https://github.com", "github"),
                new SocialTestCase("Twitter", "https://twitter.com", "twitter"),
                new SocialTestCase("YouTube", "https://youtube.com", "youtube"),
                new SocialTestCase("BlueSky", "https://bsky.app", "bluesky")
            };

            Add(new Social(), CreateExpectedResults(testCases, null));

            foreach (var testCase in testCases)
            {
                Add(
                    CreateSocial(testCase),
                    CreateExpectedResults(testCases, testCase.Name));
            }
        }

        private static Social CreateSocial(SocialTestCase activeAccount)
        {
            var accounts = new List<SocialAccountConfig>
            {
                new() { Name = "LinkedIn", Url = activeAccount.Name == "LinkedIn" ? activeAccount.Url : null, SvgPath = "linkedin" },
                new() { Name = "GitHub", Url = activeAccount.Name == "GitHub" ? activeAccount.Url : null, SvgPath = "github" },
                new() { Name = "Twitter", Url = activeAccount.Name == "Twitter" ? activeAccount.Url : null, SvgPath = "twitter" },
                new() { Name = "YouTube", Url = activeAccount.Name == "YouTube" ? activeAccount.Url : null, SvgPath = "youtube" },
                new() { Name = "BlueSky", Url = activeAccount.Name == "BlueSky" ? activeAccount.Url : null, SvgPath = "bluesky" }
            }
            .Where(x => x.Url is not null)
            .ToArray();
            return new Social { Accounts = accounts };
        }

        private static Dictionary<string, bool> CreateExpectedResults(
            IEnumerable<SocialTestCase> allCases,
            string? activeAccountName) => allCases.ToDictionary(
            tc => tc.IconId,
            tc => tc.Name == activeAccountName);
    }
}
