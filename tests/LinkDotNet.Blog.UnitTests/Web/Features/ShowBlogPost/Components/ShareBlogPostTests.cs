﻿using System;
using System.Linq;
using AngleSharp.Html.Dom;
using LinkDotNet.Blog.Web.Features.ShowBlogPost.Components;
using Microsoft.Extensions.DependencyInjection;

namespace LinkDotNet.Blog.UnitTests.Web.Features.ShowBlogPost.Components;

public class ShareBlogPostTests : BunitContext
{
    [Fact]
    public void ShouldCopyLinkToClipboard()
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("blogPost/1");
        var cut = Render<ShareBlogPost>();

        var element = cut.Find("#share-clipboard") as IHtmlAnchorElement;

        element.ShouldNotBeNull();
        var onclick = element!.Attributes.FirstOrDefault(a => a.Name.Equals("onclick", StringComparison.InvariantCultureIgnoreCase));
        onclick.ShouldNotBeNull();
        onclick!.Value.ShouldContain("blogPost/1");
    }

    [Fact]
    public void ShouldShareToLinkedIn()
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("blogPost/1");

        var cut = Render<ShareBlogPost>();

        var linkedInShare = (IHtmlAnchorElement)cut.Find("#share-linkedin");
        linkedInShare.Href.ShouldBe("https://www.linkedin.com/shareArticle?mini=true&url=http://localhost/blogPost/1");
    }
    
    [Fact]
    public void ShouldShareToX()
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("blogPost/1");

        var cut = Render<ShareBlogPost>();

        var linkedInShare = (IHtmlAnchorElement)cut.Find("#share-x");
        linkedInShare.Href.ShouldBe("https://twitter.com/intent/tweet?url=http://localhost/blogPost/1");
    }

    [Fact]
    public void ShouldShareToBluesky()
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("blogPost/1");
        
        var cut = Render<ShareBlogPost>();
        
        var blueskyShare = (IHtmlAnchorElement)cut.Find("#share-bluesky");
        blueskyShare.Href.ShouldBe("https://bsky.app/intent/compose?text=http://localhost/blogPost/1");
    }
}