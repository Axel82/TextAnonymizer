using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using ICSharpCode.AvalonEdit;
using TextAnonymizer.Views.Behaviors;

namespace TextAnonymizer.Tests;

public partial class AvalonEditBehaviorTests
{
    private sealed partial class Holder : ObservableObject
    {
        [ObservableProperty]
        private string _text = string.Empty;
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
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
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new Exception("Échec dans le thread STA", failure);
        }
    }

    private static TextEditor CreateBoundEditor(Holder holder)
    {
        var editor = new TextEditor();
        BindingOperations.SetBinding(
            editor,
            AvalonEditBehavior.TextProperty,
            new Binding(nameof(Holder.Text)) { Source = holder, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        return editor;
    }

    [Fact]
    public void TypingInEditor_UpdatesViewModel_EvenWhenInitialValueIsEmpty()
    {
        RunSta(() =>
        {
            var holder = new Holder();
            var editor = CreateBoundEditor(holder);

            editor.Text = "Jean habite à Paris.";

            Assert.Equal("Jean habite à Paris.", holder.Text);
        });
    }

    [Fact]
    public void ViewModelChange_UpdatesEditor()
    {
        RunSta(() =>
        {
            var holder = new Holder();
            var editor = CreateBoundEditor(holder);

            holder.Text = "collé depuis le presse-papiers";

            Assert.Equal("collé depuis le presse-papiers", editor.Text);
        });
    }
}
