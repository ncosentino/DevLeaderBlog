namespace DevLeader.Functions;

public sealed record NewsletterConfig(
    string Auth0ConnectionName,
    string MemberRoleId);
