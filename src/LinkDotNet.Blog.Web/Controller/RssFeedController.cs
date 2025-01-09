using LinkDotNet.Blog.Domain;
using LinkDotNet.Blog.Infrastructure.Persistence;
using LinkDotNet.Blog.Web.Features;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

using InvalidOperationException = System.InvalidOperationException;

namespace LinkDotNet.Blog.Web.Controller;

[Route("rss")]
[Route("rss.xml")]
[Route("feed")]
[Route("feed.xml")]
[Route("feed.rss")]
[EnableRateLimiting("ip")]
public sealed class RssFeedController : ControllerBase
{
    private static readonly XmlWriterSettings Settings = CreateXmlWriterSettings();
    private readonly string description;
    private readonly string blogName;
    private readonly int blogPostsPerPage;
    private readonly IRepository<BlogPost> blogPostRepository;
    private readonly IMemoryCache memoryCache;

    public RssFeedController(
        IOptions<Introduction> introductionConfiguration,
        IOptions<ApplicationConfiguration> applicationConfiguration,
        IRepository<BlogPost> blogPostRepository,
        IMemoryCache memoryCache)
    {
        ArgumentNullException.ThrowIfNull(introductionConfiguration);
        ArgumentNullException.ThrowIfNull(applicationConfiguration);

        description = introductionConfiguration.Value.Description;
        blogName = applicationConfiguration.Value.BlogName;
        blogPostsPerPage = applicationConfiguration.Value.BlogPostsPerPage;
        this.blogPostRepository = blogPostRepository;
        this.memoryCache = memoryCache;
    }

    [HttpGet]
    public async Task<IActionResult> GetRssFeed(
        [FromQuery] bool withContent = false,
        [FromQuery] int? numberOfBlogPosts = null)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await memoryCache.GetOrCreateAsync("rss-feed", async entry =>
        {
            entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(1200));

            var url = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            var introductionDescription = MarkdownConverter.ToPlainString(description);
            var feed = new SyndicationFeed(blogName, introductionDescription, new Uri(url))
            {
                Items = withContent
                    ? await GetBlogPostsItemsWithContent(url, numberOfBlogPosts)
                    : await GetBlogPostItems(url),
            };
            feed.AttributeExtensions.Add(
                new XmlQualifiedName("media", XNamespace.Xmlns.ToString()),
                "http://search.yahoo.com/mrss/");

            feed.AttributeExtensions.Add(
                new XmlQualifiedName("content", XNamespace.Xmlns.ToString()),
                "http://purl.org/rss/1.0/modules/content/");

            feed.AttributeExtensions.Add(
                new XmlQualifiedName("atom", XNamespace.Xmlns.ToString()),
                "http://www.w3.org/2005/Atom");
            XElement atomLinkElement = new(XNamespace.Get(@"http://www.w3.org/2005/Atom") + "link");
            atomLinkElement.SetAttributeValue("href", $"{url.TrimEnd('/')}/feed");
            atomLinkElement.SetAttributeValue("rel", "self");
            atomLinkElement.SetAttributeValue("type", "application/rss+xml");
            feed.ElementExtensions.Add(atomLinkElement);

            using var stream = new MemoryStream();
            await WriteRssInfoToStreamAsync(stream, feed);

            return File(stream.ToArray(), "application/xml; charset=utf-8");
        });

        return result!;
    }

    private static async Task WriteRssInfoToStreamAsync(Stream stream, SyndicationFeed feed)
    {
        await using var xmlWriter = XmlWriter.Create(stream, Settings);
        var rssFormatter = new Rss20FeedFormatter(feed, false);
        rssFormatter.WriteTo(xmlWriter);
        await xmlWriter.FlushAsync();
    }

    private static XmlWriterSettings CreateXmlWriterSettings()
    {
        var settings = new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            NewLineHandling = NewLineHandling.Entitize,
            Indent = true,
            Async = true,
        };
        return settings;
    }

    private static SyndicationItem CreateSyndicationItemFromBlogPost(string url, BlogPostRssInfo blogPost)
    {
        var blogPostUrl = url + blogPost.RelativePermalink;

        var content = MarkdownConverter.ToMarkupString(blogPost.ShortDescription ?? blogPost.Content ??
            throw new InvalidOperationException("Blog post must have either short description or content."));

        // Legacy blog posts from DevLeader are numeric, but RSS feed
        // validators would like to see guids for ensuring uniqueness.
        var postId = int.TryParse(blogPost.Id, out var idAsInt)
            ? new Guid(idAsInt, 0, 0, new byte[8]).ToString("N")
            : blogPost.Id;

        var item = new SyndicationItem(
            blogPost.Title,
            default(SyndicationContent),
            new Uri(blogPostUrl),
            postId,
            blogPost.UpdatedDate)
        {
            PublishDate = blogPost.UpdatedDate,
            LastUpdatedTime = blogPost.UpdatedDate,
            ElementExtensions =
            {
                CreateCDataElement(content.Value),
            },
        };

        if (content.Value is not null)
        {
            item.ElementExtensions.Add(new XElement(
                XNamespace.Get(@"http://purl.org/rss/1.0/modules/content/") + "encoded",
                new XCData(content.Value)));
        }

        if (blogPost.PreviewImageUrl is not null)
        {
            // <image> tag with a URL as the body does not seem to be
            // standard. instead, we can use media:content for this:
            // https://www.rssboard.org/media-rss#media-content
            var @namespace = XNamespace.Get(@"http://search.yahoo.com/mrss/");
            var imageNode = new XElement(@namespace + "content");
            imageNode.SetAttributeValue("url", blogPost.PreviewImageUrl.Replace(" ", "%20", StringComparison.Ordinal));
            item.ElementExtensions.Add(imageNode);
        }

        AddCategories(item.Categories, blogPost);
        return item;
    }

    private static void AddCategories(Collection<SyndicationCategory> categories, BlogPostRssInfo blogPost)
    {
        foreach (var tag in blogPost.Tags ?? [])
        {
            categories.Add(new SyndicationCategory(tag));
        }
    }

    private async Task<IEnumerable<SyndicationItem>> GetBlogPostItems(string url)
    {
        var blogPosts = await blogPostRepository.GetAllByProjectionAsync(
            s => new BlogPostRssInfo(s.Id, s.Title, s.ShortDescription, null, s.UpdatedDate, s.PreviewImageUrl, s.Slug, s.Tags),
            f => f.IsPublished,
            orderBy: post => post.UpdatedDate);
        return blogPosts.Select(bp => CreateSyndicationItemFromBlogPost(url, bp));
    }

    private async Task<IEnumerable<SyndicationItem>> GetBlogPostsItemsWithContent(string url, int? numberOfBlogPosts)
    {
        numberOfBlogPosts ??= blogPostsPerPage;

        var blogPosts = await blogPostRepository.GetAllByProjectionAsync(
            s => new BlogPostRssInfo(s.Id, s.Title, null, s.Content, s.UpdatedDate, s.PreviewImageUrl, s.Slug, s.Tags),
            f => f.IsPublished,
            orderBy: post => post.UpdatedDate,
            pageSize: numberOfBlogPosts.Value);
        return blogPosts.Select(bp => CreateSyndicationItemFromBlogPost(url, bp));
    }

    private static XmlElement CreateCDataElement(string htmlContent)
    {
        var doc = new XmlDocument();
        var cdataSection = doc.CreateCDataSection(htmlContent);
        var element = doc.CreateElement("description");
        element.AppendChild(cdataSection);
        return element;
    }


    private sealed record BlogPostRssInfo(
        string Id,
        string Title,
        string? ShortDescription,
        string? Content,
        DateTime UpdatedDate,
        string PreviewImageUrl,
        string Slug,
        IEnumerable<string> Tags)
    {
        public string RelativePermalink => $"/{UpdatedDate.Year}/{UpdatedDate.Month:00}/{UpdatedDate.Day:00}/{Slug}";
    }
}
