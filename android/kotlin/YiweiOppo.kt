/*
 * 一维输入法：OPPO / ColorOS（及一加、realme）适配 + 大屏适配。
 *  - keepAlive：后台保活向导（ColorOS 会冻结后台应用，输入法被冻结后再弹出会冷启动、卡一下）
 *  - capFontScale：系统字体放大时键盘文字最多放大到 1.15 倍，避免「分词 / ABC」撑破按键
 *  - extraSidePadding：折叠屏内屏 / 平板（Find N、OPPO Pad）上键盘不拉满全宽，居中限宽
 *  - excludeEdgeGestures：键盘左右边缘不触发系统返回手势（全面屏手势）
 * SPDX-License-Identifier: GPL-3.0-or-later
 */
package com.osfans.trime.ime.keyboard

import android.annotation.SuppressLint
import android.content.ActivityNotFoundException
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.res.Configuration
import android.graphics.Rect
import android.net.Uri
import android.os.Build
import android.provider.Settings
import android.view.View
import android.widget.Toast
import timber.log.Timber

object YiweiOppo {
    /** OPPO / 一加 / realme 都是 ColorOS（海外 OxygenOS 同源）。 */
    val isOppoFamily: Boolean
        get() = Build.MANUFACTURER.lowercase() in setOf("oppo", "oneplus", "realme")

    private const val MAX_FONT_SCALE = 1.15f

    /** 把键盘用到的字体放大倍数限制在 1.15 以内。onCreate 和 onConfigurationChanged 里调用。 */
    @Suppress("DEPRECATION")
    fun capFontScale(context: Context) {
        val res = context.resources
        val cfg = res.configuration
        if (cfg.fontScale <= MAX_FONT_SCALE) return
        val capped = Configuration(cfg).apply { fontScale = MAX_FONT_SCALE }
        res.updateConfiguration(capped, res.displayMetrics)
    }

    private const val MAX_KEYBOARD_WIDTH_DP = 720

    /** 窗口宽于 [MAX_KEYBOARD_WIDTH_DP] 时，在两侧补上多出来的宽度的一半。 */
    fun extraSidePadding(context: Context, basePx: Int): Int {
        val density = context.resources.displayMetrics.density
        val widthPx = (context.resources.configuration.screenWidthDp * density).toInt()
        val maxPx = (MAX_KEYBOARD_WIDTH_DP * density).toInt()
        return if (widthPx > maxPx) maxOf(basePx, (widthPx - maxPx) / 2) else basePx
    }

    /** 键盘左右各 32dp 的边缘排除系统返回手势（系统最多认每边 200dp 高）。 */
    @SuppressLint("NewApi")
    fun excludeEdgeGestures(view: View) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) return
        view.addOnLayoutChangeListener { v, l, t, r, b, _, _, _, _ ->
            val d = v.resources.displayMetrics.density
            val w = r - l
            val h = b - t
            val edge = (32 * d).toInt()
            val top = maxOf(0, h - (200 * d).toInt())
            v.systemGestureExclusionRects = listOf(Rect(0, top, edge, h), Rect(w - edge, top, w, h))
        }
    }

    private fun tryStart(context: Context, intent: Intent): Boolean = try {
        context.startActivity(intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        true
    } catch (e: ActivityNotFoundException) {
        false
    } catch (e: SecurityException) {
        false
    } catch (e: RuntimeException) {
        Timber.w(e, "keepAlive: start failed")
        false
    }

    /**
     * 后台保活向导：
     * ColorOS 13–16：设置 → 应用 → 应用启动管理 → 关闭「自动管理」→ 打开 自启动 / 关联启动 / 后台活动；
     * 设置 → 电池 → 应用耗电管理 → 允许完全后台行为；最近任务里下拉锁定。
     * 先试 ColorOS 的自启动页，打不开就到本应用的详情页（那里有「耗电管理」「自启动」入口）。
     */
    fun keepAlive(context: Context) {
        val pkg = context.packageName
        val steps = if (isOppoFamily) {
            "请允许：自启动 / 后台活动，耗电管理选「允许完全后台行为」，并在最近任务里锁定一维输入法"
        } else {
            "请把电池优化设为「不优化」，并允许自启动 / 后台运行"
        }
        Toast.makeText(context, steps, Toast.LENGTH_LONG).show()
        val details = Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.fromParts("package", pkg, null))
        if (isOppoFamily) {
            val startup = listOf(
                ComponentName("com.coloros.safecenter", "com.coloros.safecenter.startupapp.StartupAppListActivity"),
                ComponentName("com.oplus.battery", "com.oplus.startupapp.view.StartupAppListActivity"),
                ComponentName("com.oppo.safe", "com.oppo.safe.permission.startup.StartupAppListActivity"),
            )
            for (c in startup) {
                if (tryStart(context, Intent().setComponent(c))) return
            }
        } else if (tryStart(context, Intent(Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS))) {
            return
        }
        tryStart(context, details)
    }
}
