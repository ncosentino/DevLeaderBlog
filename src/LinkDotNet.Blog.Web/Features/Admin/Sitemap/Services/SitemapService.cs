using LinkDotNet.Blog.Domain;
using LinkDotNet.Blog.Infrastructure.Persistence;

using Microsoft.AspNetCore.Components;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace LinkDotNet.Blog.Web.Features.Admin.Sitemap.Services;

public sealed class SitemapService : ISitemapService
{
    private readonly IRepository<BlogPost> repository;
    private readonly NavigationManager navigationManager;
    private readonly IXmlFileWriter xmlFileWriter;

    public SitemapService(
        IRepository<BlogPost> repository,
        NavigationManager navigationManager,
        IXmlFileWriter xmlFileWriter)
    {
        this.repository = repository;
        this.navigationManager = navigationManager;
        this.xmlFileWriter = xmlFileWriter;
    }

    public Task<SitemapUrlSet> CreateSitemapAsync()
        => CreateSitemapAsync(navigationManager.BaseUri);

    public async Task<SitemapUrlSet> CreateSitemapAsync(string baseUri)
    {
        baseUri = $"{(baseUri ?? "").TrimEnd('/')}/";

        var urlSet = new SitemapUrlSet();

        var blogPosts = await repository.GetAllAsync(f => f.IsPublished, b => b.UpdatedDate);

        urlSet.Urls.Add(new SitemapUrl { Location = baseUri });
        urlSet.Urls.Add(new SitemapUrl { Location = $"{baseUri}archive" });
        urlSet.Urls.AddRange(CreateUrlsForBlogPosts(baseUri, blogPosts));
        urlSet.Urls.AddRange(CreateUrlsForTags(baseUri, blogPosts));

        return urlSet;
    }

    public async Task SaveSitemapToFileAsync(SitemapUrlSet sitemap)
    {
        await xmlFileWriter.WriteObjectToXmlFileAsync(sitemap, "wwwroot/sitemap.xml");
    }

    public async Task<string> CreateSitemapXmlAsync(
        SitemapUrlSet sitemap,
        CancellationToken cancellationToken) => await Task.Run(() =>
    {
        StringBuilder xmlBuilder = new();
        XmlSerializer xmlSerializer = new(typeof(SitemapUrlSet));
        using var writer = XmlWriter.Create(xmlBuilder);
        xmlSerializer.Serialize(writer, sitemap);
        return xmlBuilder.ToString();
    }, cancellationToken);

    private static ImmutableArray<SitemapUrl> CreateUrlsForBlogPosts(string baseUri, IEnumerable<BlogPost> blogPosts)
    {
        return blogPosts.Select(b => new SitemapUrl
        {
            Location = $"{baseUri}{b.UpdatedDate.Year}/{b.UpdatedDate.Month:00}/{b.UpdatedDate.Day:00}/{b.Slug}",
            LastModified = b.UpdatedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        }).ToImmutableArray();
    }

    private static IEnumerable<SitemapUrl> CreateUrlsForTags(string baseUri, IEnumerable<BlogPost> blogPosts)
    {
        return blogPosts
            .SelectMany(b => b.Tags)
            .Distinct()
            .Select(t => new SitemapUrl
            {
                Location = $"{baseUri}tags/{Uri.EscapeDataString(t)}",
            });
    }
}
