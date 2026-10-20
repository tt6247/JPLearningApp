package com.kanasmallsteps.app;

import android.app.Notification;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.os.Build;

/** 专注计时到点：应用不在前台时发出通知（带声音和震动）。 */
public class TimerReceiver extends BroadcastReceiver {

    private static final String CHANNEL = "timer";

    @Override
    public void onReceive(Context c, Intent intent) {
        if (MainActivity.inForeground) {
            return; // 页面会自己响铃并弹窗
        }
        String title = intent.getStringExtra(MainActivity.PREFIX + "title");
        String text = intent.getStringExtra(MainActivity.PREFIX + "text");
        NotificationManager nm = (NotificationManager) c.getSystemService(Context.NOTIFICATION_SERVICE);
        Notification.Builder b = builder(c, nm);
        Intent open = new Intent(c, MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP | Intent.FLAG_ACTIVITY_CLEAR_TOP);
        b.setSmallIcon(R.drawable.ic_stat)
                .setColor(0xFF6856C7)
                .setContentTitle(title != null ? title : "时间到！")
                .setContentText(text != null ? text : "起身活动一下，再开始下一段。")
                .setAutoCancel(true)
                .setCategory(Notification.CATEGORY_ALARM)
                .setContentIntent(PendingIntent.getActivity(c, 0, open, PendingIntent.FLAG_UPDATE_CURRENT | MainActivity.FLAG_IMMUTABLE));
        nm.notify(1, b.build());
    }

    /** Android 8+ 需要通知渠道；编译用的 API 23 SDK 里没有相关类，用反射调用。 */
    @SuppressWarnings("deprecation")
    private static Notification.Builder builder(Context c, NotificationManager nm) {
        if (Build.VERSION.SDK_INT >= 26) {
            try {
                Class<?> ch = Class.forName("android.app.NotificationChannel");
                Object channel = ch.getConstructor(String.class, CharSequence.class, int.class).newInstance(CHANNEL, "专注计时提醒", 4);
                ch.getMethod("enableVibration", boolean.class).invoke(channel, true);
                NotificationManager.class.getMethod("createNotificationChannel", ch).invoke(nm, channel);
                return Notification.Builder.class.getConstructor(Context.class, String.class).newInstance(c, CHANNEL);
            } catch (Exception ignored) {
            }
        }
        Notification.Builder b = new Notification.Builder(c);
        b.setDefaults(Notification.DEFAULT_ALL);
        b.setPriority(Notification.PRIORITY_HIGH);
        return b;
    }
}
