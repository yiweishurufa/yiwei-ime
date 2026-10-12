/*
 * 一维输入法：工具箱里的「夜间模式」「单手模式」「长辈模式」（对标搜狗工具箱）。
 *  - 夜间模式：手动覆盖系统深色（0 跟随系统 / 1 强制深色 / 2 强制浅色），ColorManager 按它选配色（一维配色都带深色版）。
 *  - 单手模式：键盘缩到 78% 宽靠一侧（1 左手 / 2 右手），另一侧空白处点一下换边、长按退出。
 *  - 长辈模式：按键、候选文字放大 1.25 倍，键盘加高 10%。
 * 三个设置都存在 AppPrefs.keyboard 里；单手 / 长辈改了会重建键盘界面（recreateInputViewPrefs）。
 * SPDX-License-Identifier: GPL-3.0-or-later
 */
package com.osfans.trime.ime.keyboard

import android.content.Context
import android.content.res.Configuration
import android.view.View
import android.widget.Toast
import com.osfans.trime.data.prefs.AppPrefs
import com.osfans.trime.data.theme.ColorManager

object YiweiModes {
    private val kb get() = AppPrefs.defaultInstance().keyboard

    // ---------------- 夜间模式 ----------------
    const val NIGHT_SYSTEM = 0
    const val NIGHT_ON = 1
    const val NIGHT_OFF = 2

    fun nightOverride(): Int = kb.yiweiNightMode.getValue()

    /** ColorManager 用：手动开关优先于系统深色。 */
    fun effectiveNight(systemNight: Boolean): Boolean = when (nightOverride()) {
        NIGHT_ON -> true
        NIGHT_OFF -> false
        else -> systemNight
    }

    /** 点一下：跟随系统 → 与当前系统相反（强制）；已强制 → 回到跟随系统。 */
    fun toggleNight(context: Context) {
        val systemNight = (context.resources.configuration.uiMode and Configuration.UI_MODE_NIGHT_MASK) ==
            Configuration.UI_MODE_NIGHT_YES
        val next = if (nightOverride() == NIGHT_SYSTEM) (if (systemNight) NIGHT_OFF else NIGHT_ON) else NIGHT_SYSTEM
        kb.yiweiNightMode.setValue(next)
        ColorManager.yiweiRefresh()
        val msg = when (next) {
            NIGHT_ON -> "夜间模式：已开启"
            NIGHT_OFF -> "夜间模式：已关闭（浅色）"
            else -> "夜间模式：跟随系统"
        }
        Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
    }

    // ---------------- 单手模式 ----------------
    const val ONE_HAND_OFF = 0
    const val ONE_HAND_LEFT = 1
    const val ONE_HAND_RIGHT = 2
    private const val ONE_HAND_WIDTH = 0.78

    fun oneHand(): Int = kb.yiweiOneHand.getValue()

    /** 单手模式下左右两侧额外留白（像素）：Pair(左, 右)。横屏不用单手。 */
    fun oneHandExtra(context: Context): Pair<Int, Int> {
        val mode = oneHand()
        if (mode == ONE_HAND_OFF || context.resources.configuration.orientation == Configuration.ORIENTATION_LANDSCAPE) {
            return 0 to 0
        }
        val w = context.resources.displayMetrics.widthPixels
        val blank = (w * (1 - ONE_HAND_WIDTH)).toInt()
        return if (mode == ONE_HAND_LEFT) 0 to blank else blank to 0
    }

    fun toggleOneHand(context: Context) {
        val next = if (oneHand() == ONE_HAND_OFF) ONE_HAND_RIGHT else ONE_HAND_OFF
        kb.yiweiOneHand.setValue(next)
        Toast.makeText(
            context,
            if (next == ONE_HAND_OFF) "已退出单手模式" else "单手模式：点空白处换到另一边，长按空白处退出",
            Toast.LENGTH_LONG,
        ).show()
    }

    /** InputView.updateKeyboardSize 里调用：空白处点一下换边、长按退出。 */
    fun bindBlankSides(left: View, right: View) {
        if (oneHand() == ONE_HAND_OFF) return
        listOf(left, right).forEach { v ->
            v.setOnClickListener {
                kb.yiweiOneHand.setValue(if (oneHand() == ONE_HAND_LEFT) ONE_HAND_RIGHT else ONE_HAND_LEFT)
            }
            v.setOnLongClickListener {
                kb.yiweiOneHand.setValue(ONE_HAND_OFF)
                Toast.makeText(v.context, "已退出单手模式", Toast.LENGTH_SHORT).show()
                true
            }
        }
    }

    // ---------------- 长辈模式 ----------------
    fun elder(): Boolean = kb.yiweiElder.getValue()

    /** 按键 / 候选文字放大倍数。 */
    fun textScale(): Float = if (elder()) 1.25f else 1f

    /** 键盘高度放大倍数（乘在用户高度比例上）。 */
    fun heightScale(): Double = if (elder()) 1.10 else 1.0

    fun toggleElder(context: Context) {
        val next = !elder()
        kb.yiweiElder.setValue(next)
        Toast.makeText(context, if (next) "长辈模式：大字已开启" else "长辈模式：已关闭", Toast.LENGTH_SHORT).show()
    }
}
