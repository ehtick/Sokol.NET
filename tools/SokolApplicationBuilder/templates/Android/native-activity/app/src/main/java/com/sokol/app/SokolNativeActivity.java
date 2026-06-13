package com.sokol.app;

import android.app.NativeActivity;
import android.content.Context;
import android.os.Build;
import android.os.Bundle;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.KeyEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.Window;
import android.view.WindowManager;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputMethodManager;
import android.widget.EditText;
import android.widget.FrameLayout;

public class SokolNativeActivity extends NativeActivity {
    
    // Load native library early so JNI methods are available
    static {
        System.loadLibrary("sokol");
    }
    
    private InputMethodManager inputMethodManager;
    private EditText hiddenEditText;
    private TextWatcher textWatcher;
    private boolean isProcessingText = false;
    private int lastSentLength = 0;

    // Blank padding kept in the hidden capture EditText so the soft keyboard's backspace ALWAYS has
    // something to delete — including when the on-screen GUI field opened already containing text
    // (e.g. the current player name). Without it the EditText starts empty, the IME has nothing to
    // shrink, no backspace is ever forwarded, and pre-existing text can't be deleted. Each deletion
    // is forwarded as one KEYCODE_DEL; the GUI field holds the real text and removes one character
    // per event (deletes past the real text are harmless no-ops).
    private static final int KB_PAD = 64;
    
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        // @TEMPLATE_RUNTIME_PERMISSIONS_REQUEST@

        // Get InputMethodManager for keyboard control
        inputMethodManager = (InputMethodManager) getSystemService(Context.INPUT_METHOD_SERVICE);
        
        // Create hidden EditText for capturing keyboard input
        createHiddenEditText();
        
        // Enable immersive fullscreen mode if the theme is set to fullscreen
        enableImmersiveMode();
    }
    
    private void createHiddenEditText() {
        // Create an invisible EditText to receive keyboard input
        hiddenEditText = new EditText(this);
        hiddenEditText.setLayoutParams(new ViewGroup.LayoutParams(1, 1));
        hiddenEditText.setAlpha(0.0f);
        hiddenEditText.setImeOptions(EditorInfo.IME_FLAG_NO_FULLSCREEN | EditorInfo.IME_FLAG_NO_EXTRACT_UI);
        
        // Create text watcher as member variable so we can remove/add it
        textWatcher = new TextWatcher() {
            @Override
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {
            }

            @Override
            public void onTextChanged(CharSequence s, int start, int before, int count) {
            }

            @Override
            public void afterTextChanged(Editable s) {
                // Skip our own programmatic buffer resets.
                if (isProcessingText) {
                    return;
                }
                int currentLength = s.length();

                // Text grew → forward the appended characters; shrank → forward one backspace each.
                if (currentLength > lastSentLength) {
                    String newChars = s.toString().substring(lastSentLength);
                    for (char c : newChars.toCharArray()) {
                        nativeOnKeyboardChar(c);
                    }
                } else if (currentLength < lastSentLength) {
                    int deleteCount = lastSentLength - currentLength;
                    for (int i = 0; i < deleteCount; i++) {
                        nativeOnKeyboardKey(67, true);  // KEYCODE_DEL down
                        nativeOnKeyboardKey(67, false); // KEYCODE_DEL up
                    }
                }
                lastSentLength = currentLength;

                // Replenish padding when it runs low so backspace never runs out of headroom.
                if (currentLength < KB_PAD / 2) {
                    resetKeyboardBuffer();
                }
            }
        };
        
        // Add text change listener to forward text to native code
        hiddenEditText.addTextChangedListener(textWatcher);
        
        // Add the EditText to content view
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                FrameLayout contentView = new FrameLayout(SokolNativeActivity.this);
                contentView.addView(hiddenEditText);
                setContentView(contentView);
            }
        });
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) {
            enableImmersiveMode();
        }
    }

    @SuppressWarnings("deprecation")
    private void enableImmersiveMode() {
        Window window = getWindow();
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            // Android 11 (API 30) and above - use WindowInsetsController
            window.setDecorFitsSystemWindows(false);
            android.view.WindowInsetsController controller = window.getInsetsController();
            if (controller != null) {
                controller.hide(android.view.WindowInsets.Type.statusBars() | android.view.WindowInsets.Type.navigationBars());
                controller.setSystemBarsBehavior(android.view.WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
            }
        } else if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.KITKAT) {
            // Android 4.4 (KitKat) to Android 10 - use deprecated setSystemUiVisibility for backward compatibility
            View decorView = window.getDecorView();
            decorView.setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                | View.SYSTEM_UI_FLAG_FULLSCREEN
                | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
            );
        }
        
        // Keep screen on
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
    }
    
    // Called from native code via JNI to show/hide the soft keyboard
    public void showKeyboard(final boolean show) {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (hiddenEditText != null && inputMethodManager != null) {
                    if (show) {
                        // Seed padding so backspace can delete text the on-screen GUI field already
                        // contained (it is opened pre-filled, e.g. with the current name).
                        resetKeyboardBuffer();
                        hiddenEditText.requestFocus();
                        inputMethodManager.showSoftInput(hiddenEditText, InputMethodManager.SHOW_IMPLICIT);
                    } else {
                        // Hide keyboard and clear focus
                        inputMethodManager.hideSoftInputFromWindow(hiddenEditText.getWindowToken(), 0);
                        hiddenEditText.clearFocus();
                    }
                }
            }
        });
    }

    // Fill the hidden capture EditText with KB_PAD blank characters and put the caret at the end, so
    // the soft keyboard always has padding to delete (backspace) and room to append (typing). Only
    // growth/shrink relative to this baseline is forwarded — the padding itself never is. Runs on the
    // UI thread (every caller already is).
    private void resetKeyboardBuffer() {
        if (hiddenEditText == null) return;
        isProcessingText = true;
        char[] pad = new char[KB_PAD];
        java.util.Arrays.fill(pad, ' ');
        hiddenEditText.setText(new String(pad));
        hiddenEditText.setSelection(KB_PAD);
        lastSentLength = KB_PAD;
        isProcessingText = false;
    }
    
    // Native methods to forward keyboard events
    private native void nativeOnKeyboardChar(int codepoint);
    private native void nativeOnKeyboardKey(int keycode, boolean down);

    // @TEMPLATE_RUNTIME_PERMISSIONS_CALLBACK@
}
