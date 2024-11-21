using System.Collections.Generic;

namespace LinkDotNet.Blog.Domain;

public sealed record Social
{
    public const string SocialSection = "Social";

    public IReadOnlyList<SocialAccountConfig>? Accounts { get; init; }
}

public sealed record SocialAccountConfig
{
    public string? Name { get; init; }

    public string? Url { get; init; }

    public string? SvgPath { get; init; }
}
