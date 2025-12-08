using Foundation;
using UIKit;

namespace IOSSokolApp;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        // Create window
        Window = new UIWindow(UIScreen.MainScreen.Bounds);

        // Create and set root view controller
        Window.RootViewController = new ViewController();

        // Make window visible
        Window.MakeKeyAndVisible();

        return true;
    }
}
