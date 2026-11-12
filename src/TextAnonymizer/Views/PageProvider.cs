using Wpf.Ui.Abstractions;

namespace TextAnonymizer.Views;

/// <summary>Fournit les pages du <c>NavigationView</c> (une instance par type, conservée entre deux navigations).</summary>
public sealed class PageProvider(Func<Type, object?> factory) : INavigationViewPageProvider
{
    private readonly Dictionary<Type, object?> _pages = [];

    public object? GetPage(Type pageType)
    {
        if (!_pages.TryGetValue(pageType, out var page))
        {
            page = factory(pageType);
            _pages[pageType] = page;
        }

        return page;
    }
}
