package com.kanasmallsteps.app;

import android.Manifest;
import android.app.Activity;
import android.app.AlarmManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.content.res.Configuration;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Vibrator;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.webkit.JavascriptInterface;
import android.webkit.ValueCallback;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.ByteArrayOutputStream;
import java.nio.charset.Charset;

/**
 * 假名小步 · 安卓版主界面：一个全屏 WebView，加载 assets/www 中的离线页面。
 * 学习逻辑全部在 core.js（与桌面版 C# 一一对应）；这里只提供存储、通知、震动等原生能力。
 */
public class MainActivity extends Activity {

    static final String PREFIX = "com.kanasmallsteps.app.";
    static final Charset UTF8 = Charset.forName("UTF-8");
    static final int FLAG_IMMUTABLE = 0x04000000; // PendingIntent.FLAG_IMMUTABLE（API 23 的 SDK 中没有这个常量）

    /** 应用在前台时由页面自己提醒，闹钟广播不再发通知。 */
    static volatile boolean inForeground;

    private WebView web;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        web = new WebView(this);
        // 上次的外观选择（页面每次切换时写入）；没有记录时跟随系统
        String mode = getSharedPreferences("ui", MODE_PRIVATE).getString("theme", "system");
        applyBars("dark".equals(mode) || (!"light".equals(mode) && isSystemDark()));
        web.setOverScrollMode(View.OVER_SCROLL_NEVER);
        WebSettings s = web.getSettings();
        s.setJavaScriptEnabled(true);
        s.setDomStorageEnabled(true);
        s.setMediaPlaybackRequiresUserGesture(false);
        s.setAllowFileAccess(false);
        s.setAllowContentAccess(false);
        s.setSupportZoom(false);
        web.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, String url) {
                if (url.startsWith("file:///android_asset/")) {
                    return false;
                }
                try {
                    startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(url)));
                } catch (Exception ignored) {
                }
                return true;
            }
        });
        web.addJavascriptInterface(new Bridge(), "AndroidBridge");
        setContentView(web);
        web.loadUrl("file:///android_asset/www/index.html");
    }

    // 配色与 app.css / 桌面版 Theme.cs 一致：亮色护眼 bg #F2EDE1 surface #FAF7F0；深色护眼 bg #1D1C1A surface #272522
    static final int LIGHT_BG = 0xFFF2EDE1, LIGHT_SURFACE = 0xFFFAF7F0, DARK_BG = 0xFF1D1C1A, DARK_SURFACE = 0xFF272522;

    boolean isSystemDark() {
        return (getResources().getConfiguration().uiMode & Configuration.UI_MODE_NIGHT_MASK) == Configuration.UI_MODE_NIGHT_YES;
    }

    void applyBars(boolean dark) {
        Window w = getWindow();
        w.setStatusBarColor(dark ? DARK_BG : LIGHT_BG);
        int flags = dark ? 0 : View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
        if (Build.VERSION.SDK_INT >= 26) {
            w.setNavigationBarColor(dark ? DARK_SURFACE : LIGHT_SURFACE);
            if (!dark) {
                flags |= 0x00000010; // SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR
            }
        }
        w.getDecorView().setSystemUiVisibility(flags);
        w.getDecorView().setBackgroundColor(dark ? DARK_BG : LIGHT_BG);
        web.setBackgroundColor(dark ? DARK_BG : LIGHT_BG);
    }

    @Override
    public void onConfigurationChanged(Configuration newConfig) {
        super.onConfigurationChanged(newConfig);
        web.evaluateJavascript("window.kanaSystemTheme && window.kanaSystemTheme(" + isSystemDark() + ")", null);
    }

    @Override
    public void onBackPressed() {
        web.evaluateJavascript("window.kanaBack ? window.kanaBack() : false", new ValueCallback<String>() {
            @Override
            public void onReceiveValue(String handled) {
                if (!"true".equals(handled)) {
                    MainActivity.super.onBackPressed();
                }
            }
        });
    }

    @Override
    protected void onResume() {
        super.onResume();
        inForeground = true;
        web.onResume();
        web.evaluateJavascript("window.kanaResume && window.kanaResume()", null);
    }

    @Override
    protected void onPause() {
        inForeground = false;
        web.evaluateJavascript("window.kanaPause && window.kanaPause()", null);
        web.onPause();
        super.onPause();
    }

    @Override
    protected void onDestroy() {
        if (web != null) {
            web.destroy();
        }
        super.onDestroy();
    }

    static PendingIntent alarmIntent(Context c, String title, String text) {
        Intent i = new Intent(c, TimerReceiver.class);
        if (title != null) {
            i.putExtra(PREFIX + "title", title);
            i.putExtra(PREFIX + "text", text);
        }
        return PendingIntent.getBroadcast(c, 1, i, PendingIntent.FLAG_UPDATE_CURRENT | FLAG_IMMUTABLE);
    }

    /** 提供给页面 JS 的原生接口（window.AndroidBridge）。在后台线程调用。 */
    final class Bridge {

        private final Object lock = new Object();

        private File file(String name) {
            return new File(getFilesDir(), name);
        }

        @JavascriptInterface
        public String loadProgress() {
            synchronized (lock) {
                String s = read(file("progress.json"));
                if (s == null) {
                    s = read(file("progress.json.bak"));
                }
                return s;
            }
        }

        /** 先写临时文件，再把旧文件改名为 .bak，最后改名替换，避免写到一半断电损坏。 */
        @JavascriptInterface
        public boolean saveProgress(String json) {
            synchronized (lock) {
                File main = file("progress.json"), bak = file("progress.json.bak"), tmp = file("progress.json.tmp");
                try {
                    FileOutputStream out = new FileOutputStream(tmp);
                    try {
                        out.write(json.getBytes(UTF8));
                        out.getFD().sync();
                    } finally {
                        out.close();
                    }
                    if (main.exists() && !main.renameTo(bak)) {
                        return false;
                    }
                    return tmp.renameTo(main);
                } catch (Exception e) {
                    return false;
                }
            }
        }

        @JavascriptInterface
        public boolean systemDark() {
            return isSystemDark();
        }

        /** 页面切换外观时调用：同步系统栏颜色，并记住选择，下次启动首帧即用对的颜色。 */
        @JavascriptInterface
        public void setDarkTheme(final boolean dark) {
            runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    applyBars(dark);
                }
            });
        }

        @JavascriptInterface
        public void rememberTheme(String mode) {
            SharedPreferences.Editor e = getSharedPreferences("ui", MODE_PRIVATE).edit();
            e.putString("theme", mode);
            e.apply();
        }

        @JavascriptInterface
        public String dataPath() {
            return file("progress.json").getAbsolutePath();
        }

        @JavascriptInterface
        public void scheduleTimer(String endAtMillis, String title, String text) {
            long at;
            try {
                at = Long.parseLong(endAtMillis);
            } catch (NumberFormatException e) {
                return;
            }
            AlarmManager am = (AlarmManager) getSystemService(Context.ALARM_SERVICE);
            PendingIntent pi = alarmIntent(MainActivity.this, title, text);
            try {
                am.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, pi);
            } catch (SecurityException e) {
                am.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, pi); // 没有精确闹钟权限时退回非精确
            }
        }

        @JavascriptInterface
        public void cancelTimer() {
            AlarmManager am = (AlarmManager) getSystemService(Context.ALARM_SERVICE);
            am.cancel(alarmIntent(MainActivity.this, null, null));
        }

        @JavascriptInterface
        public void keepScreenOn(final boolean on) {
            runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    if (on) {
                        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
                    } else {
                        getWindow().clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
                    }
                }
            });
        }

        @JavascriptInterface
        public void vibrate(int ms) {
            Vibrator v = (Vibrator) getSystemService(Context.VIBRATOR_SERVICE);
            if (v != null && v.hasVibrator()) {
                v.vibrate(Math.max(1, Math.min(ms, 2000)));
            }
        }

        @JavascriptInterface
        public void requestNotificationPermission() {
            if (Build.VERSION.SDK_INT >= 33
                    && checkSelfPermission("android.permission.POST_NOTIFICATIONS") != PackageManager.PERMISSION_GRANTED) {
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        requestPermissions(new String[] { "android.permission.POST_NOTIFICATIONS" }, 1);
                    }
                });
            }
        }

        private String read(File f) {
            if (!f.exists()) {
                return null;
            }
            try {
                FileInputStream in = new FileInputStream(f);
                try {
                    ByteArrayOutputStream buf = new ByteArrayOutputStream();
                    byte[] b = new byte[8192];
                    int n;
                    while ((n = in.read(b)) > 0) {
                        buf.write(b, 0, n);
                    }
                    return new String(buf.toByteArray(), UTF8);
                } finally {
                    in.close();
                }
            } catch (Exception e) {
                return null;
            }
        }
    }
}
