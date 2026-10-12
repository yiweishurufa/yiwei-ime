/*
 * 一维输入法：工具箱里的「夜间模式」「单手模式」「长辈模式」（对标搜狗工具箱）。
 *  - 夜间模式：手动覆盖系统深色（0 跟随系统 / 1 强制深色 / 2 强制浅色），ColorManager 按它选配色（一维配色都带深色版）。
 *  - 单手模式：键盘缩到 78% 宽靠一侧（1 左手 / 2 右手），另一侧空白处点一下换边、长按退出。
 *  - 长辈模式：按键、候选文字放大 1.25 倍，键盘加高 10%。
 *  - 悬浮键盘：键盘缩到 86% 宽、圆角带阴影，拖动上方的小横条移动位置（记住位置），长按小横条退出；
 *    只有键盘本身可点，其余地方的触摸交给后面的应用，应用界面也不被顶起。
 * 三个设置都存在 AppPrefs.keyboard 里；单手 / 长辈改了会重建键盘界面（recreateInputViewPrefs）。
 * SPDX-License-Identifier: GPL-3.0-or-later
 */
package com.osfans.trime.ime.keyboard

import android.annotation.SuppressLint
import android.content.Context
import android.content.res.Configuration
import android.graphics.Color
import android.graphics.Outline
import android.graphics.Rect
import android.graphics.drawable.GradientDrawable
import android.inputmethodservice.InputMethodService
import android.view.MotionEvent
import android.view.View
import android.view.ViewGroup
import android.view.ViewOutlineProvider
import android.widget.FrameLayout
import android.widget.Toast
import androidx.constraintlayout.widget.ConstraintLayout
import java.lang.ref.WeakReference
import kotlin.math.roundToInt
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
        if (mode == ONE_HAND_OFF || floating() || context.resources.configuration.orientation == Configuration.ORIENTATION_LANDSCAPE) {
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

    // ---------------- 悬浮键盘 ----------------
    private const val FLOAT_WIDTH = 0.86

    fun floating(): Boolean = kb.yiweiFloating.getValue()

    fun toggleFloating(context: Context) {
        val next = !floating()
        kb.yiweiFloating.setValue(next)
        Toast.makeText(
            context,
            if (next) "悬浮键盘：拖动上方小横条移动，长按小横条退出" else "已退出悬浮键盘",
            Toast.LENGTH_LONG,
        ).show()
    }

    /** InputView 里键盘的宽度：悬浮时 86% 屏宽，否则 -1（MATCH_PARENT）。 */
    fun keyboardWidth(context: Context): Int =
        if (floating()) (context.resources.displayMetrics.widthPixels * FLOAT_WIDTH).toInt() else ViewGroup.LayoutParams.MATCH_PARENT

    private var keyboardRef: WeakReference<View>? = null
    private var handleRef: WeakReference<View>? = null

    /** InputView 初始化最后调用：悬浮时给键盘加圆角阴影、在上方放拖动条，并恢复上次的位置。 */
    @SuppressLint("ClickableViewAccessibility")
    fun setupFloating(root: ConstraintLayout, keyboard: View, followers: List<View>) {
        keyboardRef = null
        handleRef = null
        if (!floating()) return
        val ctx = root.context
        fun dp(v: Int) = (v * ctx.resources.displayMetrics.density).roundToInt()
        keyboard.outlineProvider = object : ViewOutlineProvider() {
            override fun getOutline(view: View, outline: Outline) {
                outline.setRoundRect(0, 0, view.width, view.height, dp(16).toFloat())
            }
        }
        keyboard.clipToOutline = true
        keyboard.elevation = dp(10).toFloat()

        val handle = FrameLayout(ctx).apply {
            elevation = dp(10).toFloat()
            addView(
                View(ctx).apply {
                    background = GradientDrawable().apply {
                        cornerRadius = dp(3).toFloat()
                        setColor(Color.parseColor("#2B5BD7"))
                    }
                },
                FrameLayout.LayoutParams(dp(48), dp(6), android.view.Gravity.CENTER),
            )
        }
        if (keyboard.id == View.NO_ID) keyboard.id = View.generateViewId()
        root.addView(
            handle,
            ConstraintLayout.LayoutParams(dp(96), dp(24)).apply {
                bottomToTop = keyboard.id
                startToStart = keyboard.id
                endToEnd = keyboard.id
            },
        )
        val all = listOf(keyboard, handle) + followers

        fun place(x: Float, y: Float) {
            val maxX = ((root.width - keyboard.width) / 2f).coerceAtLeast(0f)
            val maxUp = (root.height - keyboard.height - handle.height).toFloat().coerceAtLeast(0f)
            val cx = x.coerceIn(-maxX, maxX)
            val cy = y.coerceIn(-maxUp, 0f)
            all.forEach {
                it.translationX = cx
                it.translationY = cy
            }
        }
        root.post { place(kb.yiweiFloatX.getValue().toFloat(), kb.yiweiFloatY.getValue().toFloat()) }

        var downX = 0f
        var downY = 0f
        var startX = 0f
        var startY = 0f
        handle.setOnTouchListener { v, e ->
            when (e.actionMasked) {
                MotionEvent.ACTION_DOWN -> {
                    downX = e.rawX
                    downY = e.rawY
                    startX = keyboard.translationX
                    startY = keyboard.translationY
                }
                MotionEvent.ACTION_MOVE -> place(startX + e.rawX - downX, startY + e.rawY - downY)
                MotionEvent.ACTION_UP, MotionEvent.ACTION_CANCEL -> {
                    val moved = kotlin.math.abs(e.rawX - downX) + kotlin.math.abs(e.rawY - downY)
                    if (moved < dp(6) && e.eventTime - e.downTime > 600) {
                        kb.yiweiFloating.setValue(false)
                        Toast.makeText(v.context, "已退出悬浮键盘", Toast.LENGTH_SHORT).show()
                        return@setOnTouchListener true
                    }
                    kb.yiweiFloatX.setValue(keyboard.translationX.roundToInt())
                    kb.yiweiFloatY.setValue(keyboard.translationY.roundToInt())
                    // 触摸区域跟着键盘走（onComputeInsets 在下一次布局时重算）
                    root.requestLayout()
                }
            }
            true
        }
        keyboardRef = WeakReference(keyboard)
        handleRef = WeakReference(handle)
    }

    private val tmp = IntArray(2)

    /** TrimeInputMethodService.onComputeInsets：悬浮时只有键盘和拖动条可点，应用不被顶起。返回 false 表示没在悬浮。 */
    fun computeInsets(decorHeight: Int, out: InputMethodService.Insets): Boolean {
        if (!floating()) return false
        val k = keyboardRef?.get() ?: return false
        if (!k.isAttachedToWindow) return false
        val r = Rect()
        k.getLocationInWindow(tmp)
        r.set(tmp[0], tmp[1], tmp[0] + k.width, tmp[1] + k.height)
        handleRef?.get()?.let { h ->
            h.getLocationInWindow(tmp)
            r.union(tmp[0], tmp[1], tmp[0] + h.width, tmp[1] + h.height)
        }
        out.contentTopInsets = decorHeight
        out.visibleTopInsets = decorHeight
        out.touchableInsets = InputMethodService.Insets.TOUCHABLE_INSETS_REGION
        out.touchableRegion.set(r)
        return true
    }
}
