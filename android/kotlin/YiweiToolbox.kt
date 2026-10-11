/*
 * 一维输入法：图标网格页（对标搜狗的「工具箱」和「键盘选择」）。
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
import android.widget.PopupMenu
import android.widget.Toast
import android.widget.ScrollView
import android.widget.TextView
import com.mikepenz.iconics.IconicsDrawable
import com.mikepenz.iconics.utils.sizeDp
import com.osfans.trime.data.theme.ColorManager
import com.osfans.trime.data.theme.KeyActionManager
import com.osfans.trime.data.theme.Theme
import com.osfans.trime.ime.core.TrimeInputMethodService
import com.osfans.trime.ime.window.BoardWindow
import com.osfans.trime.ime.window.BoardWindowManager
import org.kodein.di.instance
import splitties.dimensions.dp

class YiweiGridWindow(
    override val title: String,
    private val items: List<Item>,
) : BoardWindow.BarBoardWindow() {
    class Item(val label: String, val icon: String, val action: String)

    companion object {
        fun toolboxItems(): List<Item> = YiweiGridData.toolbox

        fun toolbox() = YiweiGridWindow("工具箱", YiweiGridData.toolbox)

        fun keyboards() = YiweiGridWindow("键盘选择", YiweiGridData.keyboards)
    }

    private val windowManager: BoardWindowManager by di.instance()
    private val actionListener: CommonKeyboardActionListener by di.instance()
    private val service: TrimeInputMethodService by di.instance()
    private val theme: Theme by di.instance()

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
                if (title == "工具箱") {
                    setOnLongClickListener {
                        showOrderMenu(this, item)
                        true
                    }
                }
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

    /** 长按工具箱里的一项：调整它在工具栏里的位置（顺序可调）。 */
    private fun showOrderMenu(anchor: View, item: Item) {
        val yaml = theme.toolBar.buttons
        val inBar = item.action in YiweiToolbarOrder.current(context, yaml)
        val popup = PopupMenu(context, anchor)
        popup.menu.add(0, 1, 0, "放到工具栏最前面")
        popup.menu.add(0, 2, 1, if (inBar) "从工具栏移除" else "加入工具栏")
        popup.menu.add(0, 3, 2, "工具栏恢复默认顺序")
        popup.setOnMenuItemClickListener { m ->
            val msg = when (m.itemId) {
                1 -> {
                    YiweiToolbarOrder.moveToFront(context, yaml, item.action)
                    "已放到工具栏最前面"
                }
                2 -> if (YiweiToolbarOrder.toggle(context, yaml, item.action)) "已加入工具栏" else "已从工具栏移除"
                else -> {
                    YiweiToolbarOrder.reset(context)
                    "工具栏已恢复默认"
                }
            }
            Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
            service.yiweiRebuildInputView()
            true
        }
        popup.show()
    }

    override fun onAttached() {}

    override fun onDetached() {}
}

/** 条目由 prepare.py 按 layouts.TOOLBOX / layouts.KEYBOARDS 生成。 */
object YiweiGridData {
    val toolbox: List<YiweiGridWindow.Item> = listOf(
/*YIWEI_TOOLBOX*/
    )

    val keyboards: List<YiweiGridWindow.Item> = listOf(
/*YIWEI_KEYBOARDS*/
    )
}
