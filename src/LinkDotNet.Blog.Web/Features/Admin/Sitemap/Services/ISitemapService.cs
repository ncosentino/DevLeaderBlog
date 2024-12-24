using System.Threading;
using System.Threading.Tasks;

namespace LinkDotNet.Blog.Web.Features.Admin.Sitemap.Services;

public interface ISitemapService
{
    Task<SitemapUrlSet> CreateSitemapAsync();

    Task<SitemapUrlSet> CreateSitemapAsync(string baseUri);

    Task<string> CreateSitemapXmlAsync(
        SitemapUrlSet sitemap,
        CancellationToken cancellationToken);

    Task SaveSitemapToFileAsync(SitemapUrlSet sitemap);
}
