using Foundation;
using UIKit;
using CoreGraphics;

namespace IOSSokolApp;

public class ViewController : UIViewController
{
    private MetalView? _metalView;
    private UIButton? _rotationButton;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        // Set background color
        View!.BackgroundColor = UIColor.Black;

        // Create Metal view
        _metalView = new MetalView(View.Bounds);
        View.AddSubview(_metalView);

        // Create button
        _rotationButton = UIButton.FromType(UIButtonType.System);
        _rotationButton.SetTitle("Change Rotation", UIControlState.Normal);
        _rotationButton.SetTitleColor(UIColor.White, UIControlState.Normal);
        _rotationButton.BackgroundColor = UIColor.FromRGBA(0, 122, 255, 255);
        _rotationButton.Layer.CornerRadius = 8;
        _rotationButton.TouchUpInside += (sender, e) => _metalView?.ChangeRotation();

        // Position button at bottom center
        _rotationButton.Frame = new CGRect(
            (View.Bounds.Width - 200) / 2,
            View.Bounds.Height - 100,
            200,
            50
        );

        View.AddSubview(_rotationButton);
    }

    public override void ViewWillLayoutSubviews()
    {
        base.ViewWillLayoutSubviews();

        if (_metalView != null)
        {
            _metalView.Frame = View!.Bounds;
        }

        if (_rotationButton != null)
        {
            _rotationButton.Frame = new CGRect(
                (View!.Bounds.Width - 200) / 2,
                View.Bounds.Height - 100,
                200,
                50
            );
        }
    }

    public override void ViewDidDisappear(bool animated)
    {
        base.ViewDidDisappear(animated);
        _metalView?.Cleanup();
    }
}
