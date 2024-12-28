using Microsoft.AspNetCore.Http;

using System;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace LinkDotNet.Blog.Web;

public sealed class XmlResult<T> : IResult
{
    private static readonly XmlSerializer Serializer = new(typeof(T));

    private readonly T result;

    public XmlResult(T result)
    {
        this.result = result;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        using var ms = StreamManager.Instance.GetStream();
        Serializer.Serialize(ms, this.result);

        httpContext.Response.ContentType = "application/xml";
        ms.Position = 0;
        await ms.CopyToAsync(httpContext.Response.Body);
    }
}
