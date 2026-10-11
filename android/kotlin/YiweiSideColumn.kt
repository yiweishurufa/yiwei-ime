/*
 * 一维输入法：九键左侧竖列（对标搜狗）。
 *  - 没在打字：标点 ，。？！… 可上下滑动，点一下上屏；
 *  - 打字中：变成「拼音选择列」，列出当前数字可能对应的拼音（如按 32：da / fa / e / d），点一个就只看这个拼音开头的候选。
 * 拼音来自候选的注释（雾凇九键方案的注释就是拼音，如「da zi」）。选中的拼音只在界面上过滤候选，
 * 空格 / 回车上屏的是过滤后的第一个候选；选词后过滤自动取消。
 * SPDX-License-Identifier: GPL-3.0-or-later
 */
package com.osfans.trime.ime.keyboard

import android.content.Context
import android.graphics.drawable.GradientDrawable
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.FrameLayout
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import com.osfans.trime.core.CandidateProto
import com.osfans.trime.core.Candidates
import com.osfans.trime.data.theme.ColorManager
import java.lang.ref.WeakReference

object YiweiSideColumn {
    /** 当前选中的拼音（过滤候选用）；null 表示不过滤。 */
    @Volatile
    var syllable: String? = null
        private set

    @Volatile
    private var indexMap: IntArray = IntArray(0)

    private var column: WeakReference<Column>? = null
    private var onPunct: ((String) -> Unit)? = null

    private val punctuation = listOf("，", "。", "？", "！", "、", "：", "；", "…", "“", "”", "（", "）", "～", "—", "·")
    private val plain = Regex("^[a-z]+( [a-z]+)*$")

    /** 过滤后第 [position] 个候选在 Rime 候选表里的真实序号。 */
    fun realIndex(position: Int): Int {
        val real = indexMap.getOrNull(position) ?: position
        syllable = null // 选了词，本段拼音过滤结束
        return real
    }

    /** 过滤后第 [position] 个候选的真实序号，不结束过滤（长按菜单用）。 */
    fun peekIndex(position: Int): Int = indexMap.getOrNull(position) ?: position

    /** 空格 / 回车要上屏的候选序号；没有在过滤时返回 null（走 Rime 自己的逻辑）。 */
    fun filteredFirst(): Int? {
        if (syllable == null) return null
        val first = indexMap.firstOrNull() ?: return null
        syllable = null
        return first
    }

    private fun firstSyllable(comment: String): String? = comment.trim().takeIf { plain.matches(it) }?.substringBefore(' ')

    /** 每次候选更新都过一遍：更新左列、按选中的拼音过滤候选。 */
    fun process(data: Candidates.Bulk): Candidates.Bulk {
        val all = data.candidates
        if (all.isEmpty()) {
            syllable = null
            indexMap = IntArray(0)
            column?.get()?.let { c -> c.post { c.showPunctuation() } }
            return data
        }
        val syllables = LinkedHashSet<String>()
        all.forEach { cand -> firstSyllable(cand.comment)?.let { syllables.add(it) } }
        val initials = LinkedHashSet<String>()
        syllables.forEach { initials.add(it.substring(0, 1)) }
        val list = (syllables.take(10) + initials.filter { it !in syllables }.take(4))
        val chosen = syllable?.takeIf { it in list }
        if (chosen == null) syllable = null
        column?.get()?.let { c -> c.post { c.showSyllables(list, chosen) } }
        if (chosen == null) {
            indexMap = IntArray(all.size) { it }
            return data
        }
        val keep = all.indices.filter { i ->
            val s = firstSyllable(all[i].comment)
            s != null && (s == chosen || (chosen.length == 1 && s.startsWith(chosen)))
        }
        if (keep.isEmpty()) {
            syllable = null
            indexMap = IntArray(all.size) { it }
            return data
        }
        indexMap = keep.toIntArray()
        return Candidates.Bulk(data.total, 0, Array<CandidateProto>(keep.size) { all[keep[it]] })
    }

    private fun choose(s: String) {
        syllable = if (syllable == s) null else s
    }

    /** 键盘切到九键时挂上左列，切走时拿掉。 */
    fun onKeyboard(container: FrameLayout, keyboardId: String, keyboardHeight: Int, commitPunct: (String) -> Unit) {
        (column?.get()?.parent as? ViewGroup)?.removeView(column?.get())
        column = null
        onPunct = null
        if (keyboardId != "t9") return
        onPunct = commitPunct
        val ctx = container.context
        val c = Column(ctx)
        column = WeakReference(c)
        val width = (container.width * 16 / 100).takeIf { it > 0 } ?: (ctx.resources.displayMetrics.widthPixels * 16 / 100)
        container.addView(c, FrameLayout.LayoutParams(width, keyboardHeight * 3 / 4, Gravity.TOP or Gravity.START))
        c.showPunctuation()
    }

    private class Column(ctx: Context) : ScrollView(ctx) {
        private val box = LinearLayout(ctx).apply { orientation = LinearLayout.VERTICAL }
        private val itemHeight: Int get() = (layoutParams?.height ?: 0).takeIf { it > 0 }?.let { it / 4 } ?: 120

        init {
            isVerticalScrollBarEnabled = false
            overScrollMode = OVER_SCROLL_NEVER
            addView(box, ViewGroup.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT))
            background = GradientDrawable().apply {
                setColor(ColorManager.getColor("off_key_back_color"))
                cornerRadius = 12f * ctx.resources.displayMetrics.density
            }
            clipToOutline = true
        }

        private fun item(text: String, selected: Boolean, onClick: () -> Unit) = TextView(context).apply {
            this.text = text
            textSize = if (text.length > 1 && text[0].code < 128) 20f else 22f
            gravity = Gravity.CENTER
            setTextColor(ColorManager.getColor(if (selected) "hilited_candidate_text_color" else "key_text_color"))
            isClickable = true
            setOnClickListener { onClick() }
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, itemHeight)
        }

        fun showPunctuation() {
            box.removeAllViews()
            punctuation.forEach { p -> box.addView(item(p, false) { onPunct?.invoke(p) }) }
            scrollTo(0, 0)
        }

        fun showSyllables(list: List<String>, chosen: String?) {
            box.removeAllViews()
            list.forEach { s ->
                box.addView(
                    item(s, s == chosen) {
                        choose(s)
                        // 让候选栏按新的拼音重新过滤：重发一次最近的候选
                        YiweiSideColumn.refresh()
                    },
                )
            }
            scrollTo(0, 0)
        }
    }

    private var lastData: Candidates.Bulk? = null
    private var refreshHook: ((Candidates.Bulk) -> Unit)? = null

    /** 候选栏注册：拼音变了要重画。 */
    fun setRefreshHook(hook: (Candidates.Bulk) -> Unit) {
        refreshHook = hook
    }

    fun remember(data: Candidates.Bulk) {
        lastData = data
    }

    private fun refresh() {
        lastData?.let { refreshHook?.invoke(it) }
    }
}
