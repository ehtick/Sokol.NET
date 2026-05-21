/* sokol_share_macos.m -- macOS share sheet implementation for Sokol.NET.
   Uses NSSharingService dispatched to the main thread.
   No entitlements or app sandbox config required for in-app generated images.
*/
#import <AppKit/AppKit.h>
#include "sokol_share.h"

void sokolshare_image_text(const char* image_path, const char* text)
{
    NSString* nsText = [NSString stringWithUTF8String:text ? text : ""];
    NSURL*    imgURL = nil;
    if (image_path && image_path[0])
        imgURL = [NSURL fileURLWithPath:[NSString stringWithUTF8String:image_path]];

    dispatch_async(dispatch_get_main_queue(), ^{
        NSMutableArray* items = [NSMutableArray array];
        if (imgURL) [items addObject:imgURL];
        [items addObject:nsText];

        NSSharingServicePicker* picker =
            [[NSSharingServicePicker alloc] initWithItems:items];

        /* Anchor the popover to the centre of the key window's content view. */
        NSWindow* win = [NSApp keyWindow];
        if (!win) win = [NSApp mainWindow];
        if (!win) { NSLog(@"sokol_share: no key window"); return; }

        NSView* view    = win.contentView;
        NSRect  centred = NSMakeRect(NSMidX(view.bounds) - 1,
                                     NSMidY(view.bounds) - 1, 2, 2);
        [picker showRelativeToRect:centred ofView:view preferredEdge:NSRectEdgeMinY];
    });
}
