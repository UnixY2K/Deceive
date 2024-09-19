using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Deceive.ViewModels;

public partial class GamePromptWindowViewModel : ViewModelBase, INotifyPropertyChanged
{
    public static string GameLaunch { get => "Which game would you like to launch?"; }
    public static string RememberDesitionText { get => "Remember my desition and skip this screen on future launches."; }

    [ObservableProperty]
    private bool rememberDesition;


}
