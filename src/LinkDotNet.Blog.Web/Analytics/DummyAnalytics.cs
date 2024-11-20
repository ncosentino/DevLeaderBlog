using Blazor.Analytics;

using LinkDotNet.Blog.Web.Authentication.Dummy;
using LinkDotNet.Blog.Web.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;

using System.Collections.Generic;
using System.Threading.Tasks;
using Blazor.Analytics.Abstractions;

namespace LinkDotNet.Blog.Web.Analytics;

internal sealed class DummyAnalytics : IAnalytics
{
    public Task ConfigureGlobalConfigData(Dictionary<string, object> globalConfigData) => Task.CompletedTask;
    public void ConfigureGlobalEventData(Dictionary<string, object> globalEventData) { }
    public void Disable() { }
    public void Enable() { }
    public Task TrackEvent(string eventName, string? eventCategory = null, string? eventLabel = null, int? eventValue = null) => Task.CompletedTask;
    public Task TrackEvent(string eventName, int eventValue, string? eventCategory = null, string? eventLabel = null) => Task.CompletedTask;
    public Task TrackEvent(string eventName, object eventData) => Task.CompletedTask;
    public Task TrackNavigation(string uri) => Task.CompletedTask;
}

internal sealed class DummyTrackingNavigationState : ITrackingNavigationState
{
    public void DisableTracking() { }
    public void EnableTracking() { }
    public bool IsTrackingEnabled() => false;
}

internal static class AnalyticsExtensions
{
    public static void UseDummyAnalytics(this IServiceCollection services)
    {
        services.AddSingleton<IAnalytics, DummyAnalytics>();
        services.AddSingleton<ITrackingNavigationState, DummyTrackingNavigationState>();
    }
}
