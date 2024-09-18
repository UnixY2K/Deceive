namespace Deceive.ViewModels;

public partial class GamePromptWindowViewModel : ViewModelBase
{
#pragma warning disable CA1822 // Mark members as static
    public string GameLaunch => "Which game would you like to launch?";
    public string RememberDesition => "Remember my desition and skip this screen on future launches.";
#pragma warning restore CA1822 // Mark members as static
}
