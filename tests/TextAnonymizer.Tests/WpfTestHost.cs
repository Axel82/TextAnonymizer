using System.Windows;
using System.Windows.Threading;
using TextAnonymizer.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;

namespace TextAnonymizer.Tests;

/// <summary>
/// Une seule <see cref="Application"/> par processus : un fil STA dédié, partagé par tous les tests d'interface,
/// avec les vraies ressources de l'application (thème WPF UI, contrôles, <c>Themes/AppResources.xaml</c>).
/// On n'instancie pas <c>App</c> : son démarrage ouvrirait la vraie fenêtre et lirait les vrais paramètres.
/// Le fil est arrêté proprement à la fin du processus (sinon des messages de fenêtre arrivent pendant l'arrêt du CLR).
/// </summary>
public static class WpfTestHost
{
    private static readonly Lazy<Dispatcher> Host = new(Start);

    /// <summary>Exécute <paramref name="action"/> sur le fil de l'application de test et propage ses exceptions.</summary>
    public static void Run(Action action)
    {
        Exception? failure = null;
        Host.Value.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        if (failure is not null)
        {
            throw new Exception("Échec sur le fil de l'interface", failure);
        }
    }

    /// <summary>Laisse le répartiteur traiter les messages en attente (mise en page, rendus différés).</summary>
    public static void DoEvents() =>
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    /// <summary>Attend une tâche sur le fil de l'interface en continuant de traiter ses messages (pas d'interblocage).</summary>
    public static void Wait(Task task)
    {
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }

    private static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = ApplicationTheme.Light });
            app.Resources.MergedDictionaries.Add(new ControlsDictionary());
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/TextAnonymizer;component/Themes/AppResources.xaml")
            });
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "Fil WPF des tests"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            dispatcher!.InvokeShutdown();
            thread.Join(TimeSpan.FromSeconds(5));
        };
        return dispatcher!;
    }

    public sealed class FakeClipboard : IClipboardService
    {
        public string? Text { get; set; }

        public string? GetText() => Text;

        public void SetText(string text) => Text = text;
    }

    public sealed class FakeTheme : IThemeService
    {
        public AppTheme Current { get; private set; } = AppTheme.Light;

        public event EventHandler? ThemeChanged;

        public void ApplyPersisted()
        {
        }

        public void SetTheme(AppTheme theme)
        {
            Current = theme;
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
