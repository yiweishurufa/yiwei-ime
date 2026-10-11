/*
 * 一维输入法：工具栏顺序可调。
 * 工具箱里长按一项 → 「放到工具栏最前面 / 加入或移出工具栏 / 恢复默认」。顺序存在本机（动作名列表），
 * 工具栏按这个顺序排；工具箱里所有功能都可以放进工具栏。
 * SPDX-License-Identifier: GPL-3.0-or-later
 */
package com.osfans.trime.ime.keyboard

import android.content.Context
import com.osfans.trime.data.theme.model.ToolBar

object YiweiToolbarOrder {
    private const val FILE = "yiwei_toolbar"
    private const val KEY = "order"

    private fun sp(ctx: Context) = ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE)

    /** 用户保存的顺序（动作名）；null = 用主题默认。 */
    fun saved(ctx: Context): List<String>? = sp(ctx).getString(KEY, null)?.split(',')?.filter { it.isNotEmpty() }

    private fun save(ctx: Context, order: List<String>?) {
        sp(ctx).edit().apply { if (order == null) remove(KEY) else putString(KEY, order.joinToString(",")) }.apply()
    }

    private fun defaultOrder(yaml: List<ToolBar.Button>): List<String> = yaml.drop(1).map { it.action }

    private fun extraButton(item: YiweiGridWindow.Item) = ToolBar.Button(
        foreground = ToolBar.Button.Foreground(style = "ic@${item.icon}", normal = "candidate_text_color"),
        action = item.action,
    )

    /** 主题里 buttons[0]（收起）固定在最右，其余按用户顺序；工具箱里的功能也都能放进来。 */
    fun arrange(ctx: Context, yaml: List<ToolBar.Button>): List<ToolBar.Button> {
        val hide = yaml.firstOrNull() ?: return yaml
        val byAction = LinkedHashMap<String, ToolBar.Button>()
        YiweiGridWindow.toolboxItems().forEach { byAction[it.action] = extraButton(it) }
        yaml.drop(1).forEach { byAction[it.action] = it }
        val order = saved(ctx) ?: defaultOrder(yaml)
        return listOf(hide) + order.mapNotNull { byAction[it] }
    }

    fun current(ctx: Context, yaml: List<ToolBar.Button>): List<String> = saved(ctx) ?: defaultOrder(yaml)

    fun moveToFront(ctx: Context, yaml: List<ToolBar.Button>, action: String) {
        save(ctx, listOf(action) + current(ctx, yaml).filter { it != action })
    }

    fun toggle(ctx: Context, yaml: List<ToolBar.Button>, action: String): Boolean {
        val cur = current(ctx, yaml)
        return if (action in cur) {
            save(ctx, cur - action)
            false
        } else {
            save(ctx, cur + action)
            true
        }
    }

    fun reset(ctx: Context) = save(ctx, null)
}
