using Microsoft.AspNetCore.Http;

namespace LinkDotNet.Blog.Web;

internal static class XmlResultExtensions
{
    public static IResult Xml<T>(this IResultExtensions _, T result)
        => new XmlResult<T>(result);
}
