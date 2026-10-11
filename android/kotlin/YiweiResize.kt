// 一维输入法：在键盘上直接拖动调整高度（像搜狗 / Gboard）
// 工具栏「调整高度」按钮（动作 yw_resize → FUNCTION keyboard_resize）打开覆盖层：
// 上下拖动实时预览，「完成」保存到 keyboardHeightPercent（设置里的滑块用同一个值），「恢复默认」回到 100%。
package com.osfans.trime.ime.keyboard

import android.annotation.SuppressLint
import android.content.Context
import android.graphics.Color
import android.graphics.drawable.GradientDrawable
import android.view.Gravity
import android.view.MotionEvent
import android.view.View
import android.view.ViewGroup
import android.widget.FrameLayout
import android.widget.LinearLayout
import android.widget.TextView
import com.osfans.trime.data.prefs.AppPrefs
import com.osfans.trime.ime.keyboard.KeyboardPrefs.isLandscapeMode
import java.lang.ref.WeakReference
import kotlin.math.roundToInt

object YiweiResize {
    const val MIN = 60
    const val MAX = 160
    const val STEP = 5

    /** 拖动时的预览比例；null 表示用已保存的设置。 */
    @Volatile
    var preview: Int? = null
        private set

    private var window: WeakReference<KeyboardWindow>? = null
    private var overlay: WeakReference<View>? = null

    private fun saved(): Int = AppPrefs.defaultInstance().keyboard.keyboardHeightPercent.getValue().coerceIn(MIN, MAX)

    fun percent(): Int = (preview ?: saved()).coerceIn(MIN, MAX)

    /** 主题高度 × 比例；横屏最多占屏幕高度 60%、竖屏最多 75%（但不会比主题原高度更矮）。 */
    fun scaled(context: Context, base: Int): Int {
        if (base <= 0) return base
        val h = base * percent() / 100
        val screen = context.resources.displayMetrics.heightPixels
        val cap = (screen * if (context.isLandscapeMode()) 0.60 else 0.75).toInt()
        return if (h > base && cap > 0) h.coerceAtMost(maxOf(base, cap)) else h
    }

    /** 键盘重新挂上时把覆盖层放回最上面。 */
    fun raise() {
        overlay?.get()?.bringToFront()
    }

    fun isShowing(): Boolean = overlay?.get()?.parent != null

    fun toggle(kw: KeyboardWindow) {
        if (isShowing()) finish(save = true) else show(kw)
    }

    private fun setPreview(kw: KeyboardWindow, p: Int) {
        val v = p.coerceIn(MIN, MAX)
        if (v == percent()) return
        preview = v
        kw.refreshKeyboards(true)
        raise()
        label?.get()?.text = labelText(v)
    }

    private var label: WeakReference<TextView>? = null

    private fun labelText(p: Int) = "上下拖动调整键盘高度 · $p%"

    private fun finish(save: Boolean) {
        val p = preview
        val ov = overlay?.get()
        overlay = null
        label = null
        (ov?.parent as? ViewGroup)?.removeView(ov)
        if (save && p != null && p != saved()) {
            // 写设置会让输入法按新高度重建键盘（同设置里的滑块）
            AppPrefs.defaultInstance().keyboard.keyboardHeightPercent.setValue(p)
            preview = null
        } else {
            preview = null
            window?.get()?.let { if (p != null) it.refreshKeyboards(true) }
        }
    }

    @SuppressLint("ClickableViewAccessibility", "SetTextI18n")
    private fun show(kw: KeyboardWindow) {
        window = WeakReference(kw)
        val container = kw.yiweiContainer
        val ctx = container.context
        fun dp(v: Int) = (v * ctx.resources.displayMetrics.density).roundToInt()
        val accent = Color.parseColor("#2F6FEB")

        val root = FrameLayout(ctx).apply {
            setBackgroundColor(Color.argb(170, 15, 23, 42))
            isClickable = true
        }
        // 顶部拖动条
        val handle = LinearLayout(ctx).apply {
            orientation = LinearLayout.VERTICAL
            gravity = Gravity.CENTER_HORIZONTAL
            setPadding(0, dp(8), 0, dp(6))
            setBackgroundColor(accent)
        }
        handle.addView(View(ctx).apply {
            background = GradientDrawable().apply { cornerRadius = dp(3).toFloat(); setColor(Color.WHITE) }
        }, LinearLayout.LayoutParams(dp(44), dp(5)))
        val tip = TextView(ctx).apply {
            setTextColor(Color.WHITE)
            textSize = 13f
            text = labelText(percent())
            gravity = Gravity.CENTER
            setPadding(0, dp(4), 0, 0)
        }
        label = WeakReference(tip)
        handle.addView(tip, LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT))
        root.addView(handle, FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.TOP))

        // 中间：↕ 提示 + 两个按钮
        val center = LinearLayout(ctx).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER
        }
        fun button(t: String, filled: Boolean, onClick: () -> Unit) = TextView(ctx).apply {
            text = t
            textSize = 15f
            gravity = Gravity.CENTER
            setTextColor(if (filled) Color.WHITE else accent)
            setPadding(dp(22), dp(10), dp(22), dp(10))
            background = GradientDrawable().apply {
                cornerRadius = dp(20).toFloat()
                setColor(if (filled) accent else Color.WHITE)
            }
            setOnClickListener { onClick() }
        }
        val lp = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
            marginStart = dp(10); marginEnd = dp(10)
        }
        center.addView(button("恢复默认", false) { setPreview(kw, 100) }, lp)
        center.addView(button("完成", true) { finish(save = true) }, lp)
        root.addView(center, FrameLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.CENTER))

        // 整个覆盖层都能拖：往上拖变高，往下拖变矮
        var startY = 0f
        var startPercent = 100
        var startHeight = 1
        val drag = View.OnTouchListener { _, e ->
            when (e.actionMasked) {
                MotionEvent.ACTION_DOWN -> {
                    startY = e.rawY
                    startPercent = percent()
                    startHeight = kw.yiweiKeyboardHeight.coerceAtLeast(1)
                    true
                }
                MotionEvent.ACTION_MOVE -> {
                    val dy = startY - e.rawY
                    val raw = startPercent * (startHeight + dy) / startHeight
                    setPreview(kw, (raw / STEP).roundToInt() * STEP)
                    true
                }
                else -> true
            }
        }
        root.setOnTouchListener(drag)
        handle.setOnTouchListener(drag)

        root.addOnAttachStateChangeListener(object : View.OnAttachStateChangeListener {
            override fun onViewAttachedToWindow(v: View) {}

            // 输入法被重建（旋转、换主题等）时也把调好的高度存下来
            override fun onViewDetachedFromWindow(v: View) {
                if (overlay?.get() === v) finish(save = true)
            }
        })
        overlay = WeakReference(root)
        container.addView(root, FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT))
    }
}
