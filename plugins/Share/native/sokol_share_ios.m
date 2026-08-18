/* sokol_share_ios.m -- iOS share sheet implementation for Sokol.NET.
   Uses UIActivityViewController dispatched to the main queue.
   No special entitlements or Info.plist keys required for sharing in-app
   generated images (UIActivityViewController handles everything).
*/
#import <UIKit/UIKit.h>
#include "sokol_share.h"

void sokolshare_image_text(const char* image_path, const char* text)
{
    NSString* nsText = [NSString stringWithUTF8String:text ? text : ""];
    UIImage*  image  = nil;
    if (image_path && image_path[0])
    {
        NSString* nsPath = [NSString stringWithUTF8String:image_path];
        image = [UIImage imageWithContentsOfFile:nsPath];
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        NSMutableArray* items = [NSMutableArray array];
        if (image)    [items addObject:image];
        [items addObject:nsText];

        UIActivityViewController* vc =
            [[UIActivityViewController alloc] initWithActivityItems:items
                                              applicationActivities:nil];

        UIViewController* root =
            [UIApplication sharedApplication].windows.firstObject.rootViewController;
        if (!root) return;

        /* iPad requires a popover source anchor */
        if (UIDevice.currentDevice.userInterfaceIdiom == UIUserInterfaceIdiomPad)
        {
            vc.popoverPresentationController.sourceView = root.view;
            vc.popoverPresentationController.sourceRect =
                CGRectMake(root.view.bounds.size.width  / 2.0,
                           root.view.bounds.size.height / 2.0, 1.0, 1.0);
        }

        [root presentViewController:vc animated:YES completion:nil];
    });
}

/* Clipboard. sapp_set_clipboard_string() is a no-op on iOS (sokol_app.h implements
   it for macOS/Win32/X11/emscripten only), so it lives here. UIPasteboard is UIKit
   state, so the write is dispatched to the main queue like the share sheet above. */
void sokolshare_set_clipboard(const char* text)
{
    NSString* nsText = [NSString stringWithUTF8String:text ? text : ""];
    dispatch_async(dispatch_get_main_queue(), ^{
        [UIPasteboard generalPasteboard].string = nsText;
    });
}
