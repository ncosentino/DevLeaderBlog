using Microsoft.IO;

namespace LinkDotNet.Blog.Web;

public static class StreamManager
{
    public static readonly RecyclableMemoryStreamManager Instance = new();
}
