namespace AndroidSokolApp;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    private OpenGLView? _glView;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Set our view from the "main" layout resource
        SetContentView(Resource.Layout.activity_main);

        // Get reference to the OpenGL view
        _glView = FindViewById<OpenGLView>(Resource.Id.opengl_view);

        // Set up the rotation change button
        var colorButton = FindViewById<Android.Widget.Button>(Resource.Id.color_button);
        if (colorButton != null)
        {
            colorButton.Click += (sender, e) => _glView?.ChangeRotation();
        }
    }

    protected override void OnPause()
    {
        base.OnPause();
        _glView?.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _glView?.OnResume();
    }
}