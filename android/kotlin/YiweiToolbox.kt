/*
 * 一维输入法：工具箱页（对标搜狗「点 Logo 打开工具箱」）。
 * 4 列图标网格，点一项执行 trime.yaml preset_keys 里同名动作（prepare.py 生成条目并在构建时检查动作存在）。
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

package com.osfans.trime.ime.keyboard

import android.content.res.ColorStateList
import android.graphics.drawable.GradientDrawable
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.GridLayout
import android.widget.ImageView
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import com.mikepenz.iconics.IconicsDrawable
import com.mikepenz.iconics.utils.sizeDp
import com.osfans.trime.data.theme.ColorManager
import com.osfans.trime.data.theme.KeyActionManager
import com.osfans.trime.ime.window.BoardWindow
import com.osfans.trime.ime.window.BoardWindowManager
import org.kodein.di.instance
import splitties.dimensions.dp

class YiweiToolboxWindow : BoardWindow.BarBoardWindow() {
    private val windowManager: BoardWindowManager by di.instance()
    private val actionListener: CommonKeyboardActionListener by di.instance()

    override val title: String = "工具箱"

    private class Item(val label: String, val icon: String, val action: String)

    private val items: List<Item> by lazy {
        listOf(
/*YIWEI_ITEMS*/
        )
    }

    override fun onCreateView(): View {
        val ctx = context
        val textColor = ColorManager.getColor("key_text_color")
        val tint = ColorStateList.valueOf(textColor)
        val grid = GridLayout(ctx).apply {
            columnCount = 4
            setPadding(ctx.dp(8), ctx.dp(8), ctx.dp(8), ctx.dp(8))
        }
        items.forEach { item ->
            val bubble = ImageView(ctx).apply {
                scaleType = ImageView.ScaleType.CENTER_INSIDE
                background = GradientDrawable().apply {
                    shape = GradientDrawable.OVAL
                    setColor(ColorManager.getColor("key_back_color"))
                }
                imageTintList = tint
                setImageDrawable(
                    IconicsDrawable(ctx, "cmd_${item.icon}").apply { sizeDp = 24 },
                )
            }
            val label = TextView(ctx).apply {
                text = item.label
                textSize = 12f
                setTextColor(textColor)
                gravity = Gravity.CENTER
                maxLines = 1
            }
            val cell = LinearLayout(ctx).apply {
                orientation = LinearLayout.VERTICAL
                gravity = Gravity.CENTER
                setPadding(0, ctx.dp(6), 0, ctx.dp(6))
                addView(bubble, LinearLayout.LayoutParams(ctx.dp(48), ctx.dp(48)))
                addView(
                    label,
                    LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.WRAP_CONTENT,
                        ViewGroup.LayoutParams.WRAP_CONTENT,
                    ).apply { topMargin = ctx.dp(4) },
                )
                isClickable = true
                setOnClickListener {
                    // 先回到键盘，再执行；需要换键盘页 / 弹窗的动作都在键盘窗口上进行
                    windowManager.attachWindow(KeyboardWindow)
                    actionListener.listener.onAction(KeyActionManager.getAction(item.action))
                }
            }
            val lp = GridLayout.LayoutParams(
                GridLayout.spec(GridLayout.UNDEFINED),
                GridLayout.spec(GridLayout.UNDEFINED, 1f),
            ).apply {
                width = 0
                height = ViewGroup.LayoutParams.WRAP_CONTENT
            }
            grid.addView(cell, lp)
        }
        return ScrollView(ctx).apply {
            isVerticalScrollBarEnabled = false
            addView(grid, ViewGroup.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT))
        }
    }

    override fun onAttached() {}

    override fun onDetached() {}
}
