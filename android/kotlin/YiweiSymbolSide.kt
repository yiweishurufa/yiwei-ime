/*
 * 一维输入法：符号面板左侧分类竖栏（对标搜狗：左边 常用/中文/英文/数学/表情…竖排，右边网格，底部「返回」）。
 * LiquidWindow 的内容放到右边，原来工具栏位置的横排分类改成显示当前分类名。
 * SPDX-License-Identifier: GPL-3.0-or-later
 */
package com.osfans.trime.ime.keyboard

import android.content.Context
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import com.osfans.trime.data.theme.ColorManager
import kotlin.math.roundToInt

class YiweiSymbolSide(
    private val onPick: (Int) -> Unit,
    private val onBack: () -> Unit,
) {
    private val items = mutableListOf<TextView>()
    private var active = -1
    private var scroll: ScrollView? = null
    private var title: TextView? = null
    private var labels: List<String> = emptyList()

    private fun dp(ctx: Context, v: Int) = (v * ctx.resources.displayMetrics.density).roundToInt()

    /** 左侧竖栏 + 右侧原内容。 */
    fun wrap(ctx: Context, tags: List<String>, content: View): View {
        labels = tags
        items.clear()
        val textColor = ColorManager.getColor("key_text_color")
        val sideBack = ColorManager.getColor("key_back_color")
        val column = LinearLayout(ctx).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(sideBack)
        }
        val list = LinearLayout(ctx).apply { orientation = LinearLayout.VERTICAL }
        tags.forEachIndexed { i, label ->
            val tv = TextView(ctx).apply {
                text = label
                textSize = 14f
                gravity = Gravity.CENTER
                maxLines = 1
                setTextColor(textColor)
                setPadding(dp(ctx, 4), 0, dp(ctx, 4), 0)
                isClickable = true
                setOnClickListener { onPick(i) }
            }
            items.add(tv)
            list.addView(tv, LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(ctx, 44)))
        }
        scroll = ScrollView(ctx).apply {
            isVerticalScrollBarEnabled = false
            overScrollMode = View.OVER_SCROLL_NEVER
            addView(list, ViewGroup.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT))
        }
        column.addView(scroll, LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f))
        val back = TextView(ctx).apply {
            text = "返回"
            textSize = 15f
            gravity = Gravity.CENTER
            setTextColor(Color.WHITE)
            typeface = Typeface.DEFAULT_BOLD
            background = GradientDrawable().apply {
                cornerRadius = dp(ctx, 8).toFloat()
                setColor(ACCENT)
            }
            isClickable = true
            setOnClickListener { onBack() }
        }
        column.addView(
            back,
            LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(ctx, 40)).apply {
                setMargins(dp(ctx, 6), dp(ctx, 4), dp(ctx, 6), dp(ctx, 6))
            },
        )
        return LinearLayout(ctx).apply {
            orientation = LinearLayout.HORIZONTAL
            addView(column, LinearLayout.LayoutParams(dp(ctx, 68), ViewGroup.LayoutParams.MATCH_PARENT))
            addView(content, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MATCH_PARENT, 1f))
        }
    }

    /** 工具栏位置：显示当前分类名（左对齐，像搜狗的面板标题）。 */
    fun barView(ctx: Context): View = TextView(ctx).apply {
        textSize = 16f
        gravity = Gravity.CENTER_VERTICAL or Gravity.START
        setPadding(dp(ctx, 16), 0, dp(ctx, 16), 0)
        setTextColor(ColorManager.getColor("candidate_text_color"))
        typeface = Typeface.DEFAULT_BOLD
        text = labels.getOrNull(active) ?: "符号"
        title = this
    }

    fun activate(i: Int) {
        val textColor = ColorManager.getColor("key_text_color")
        items.forEachIndexed { idx, tv ->
            val on = idx == i
            tv.setTextColor(if (on) ACCENT else textColor)
            tv.typeface = if (on) Typeface.DEFAULT_BOLD else Typeface.DEFAULT
            tv.background = if (on) {
                GradientDrawable().apply {
                    setColor(ColorManager.getColor("keyboard_back_color"))
                }
            } else {
                null
            }
        }
        active = i
        title?.text = labels.getOrNull(i) ?: "符号"
        val tv = items.getOrNull(i) ?: return
        scroll?.post { scroll?.smoothScrollTo(0, (tv.top - tv.height * 2).coerceAtLeast(0)) }
    }

    companion object {
        /** 一维蓝。 */
        val ACCENT = Color.parseColor("#2B5BD7")
    }
}
