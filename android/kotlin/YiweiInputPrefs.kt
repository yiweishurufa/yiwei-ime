/*
 * 一维输入法：「输入设置」面板（工具箱 → 输入设置），移植 Windows 一维助手的输入核心设置：
 *  - 模糊音：地区预设（南方通用 / 西南 / 两湖 / 闽粤 / 江浙沪 / 清除）+ 11 组单独开关
 *  - Emoji 候选：默认开 / 关
 *  - 自定义标点：几组常用替换（/ → 、，` → ·，[ ] → 「」，\ → 、）
 *  - 按习惯调候选（yiwei_learn.lua）：开关、学习强度 低/中/高、清除最近 1 小时 / 1 天 / 全部
 * 文件格式与 Windows 相同（rime 用户目录 yiwei/ 下的 learn.disabled、learn.level、learn.clear、emoji.disabled、
 * punct.txt、fuzzy.txt），所以与电脑版互导的备份里也通用。
 * 模糊音 / Emoji / 标点要改方案，写进 <方案>.custom.yaml 里「# yiwei-begin … # yiwei-end」一段，再重新部署；
 * 学习相关的开关 Lua 每 3 秒读一次，不用部署。
 * SPDX-License-Identifier: GPL-3.0-or-later
 */
package com.osfans.trime.ime.keyboard

import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import android.widget.Toast
import com.google.android.flexbox.FlexWrap
import com.google.android.flexbox.FlexboxLayout
import com.osfans.trime.daemon.RimeSession
import com.osfans.trime.daemon.launchOnReady
import com.osfans.trime.data.base.DataManager
import com.osfans.trime.data.theme.ColorManager
import com.osfans.trime.ime.window.BoardWindow
import org.kodein.di.instance
import timber.log.Timber
import java.io.File
import kotlin.math.roundToInt

object YiweiInputData {
    class Pair2(val id: String, val label: String, val rules: List<String>)

    val FUZZY = listOf(
        Pair2("zh_z", "zh=z", listOf("derive/^zh/z/", "derive/^z([^h])/zh$1/")),
        Pair2("ch_c", "ch=c", listOf("derive/^ch/c/", "derive/^c([^h])/ch$1/")),
        Pair2("sh_s", "sh=s", listOf("derive/^sh/s/", "derive/^s([^h])/sh$1/")),
        Pair2("n_l", "n=l", listOf("derive/^n/l/", "derive/^l/n/")),
        Pair2("f_h", "f=h", listOf("derive/^f/h/", "derive/^h/f/")),
        Pair2("r_l", "r=l", listOf("derive/^r/l/", "derive/^l/r/")),
        Pair2("an_ang", "an=ang", listOf("derive/(^|[^iu])an$/$1ang/", "derive/(^|[^iu])ang$/$1an/")),
        Pair2("en_eng", "en=eng", listOf("derive/en$/eng/", "derive/eng$/en/")),
        Pair2("in_ing", "in=ing", listOf("derive/in$/ing/", "derive/ing$/in/")),
        Pair2("ian_iang", "ian=iang", listOf("derive/ian$/iang/", "derive/iang$/ian/")),
        Pair2("uan_uang", "uan=uang", listOf("derive/uan$/uang/", "derive/uang$/uan/")),
    )

    val PRESETS = listOf(
        "南方通用" to listOf("zh_z", "ch_c", "sh_s", "an_ang", "en_eng", "in_ing"),
        "西南" to listOf("n_l", "f_h", "en_eng", "in_ing"),
        "两湖" to listOf("n_l", "f_h", "r_l", "an_ang", "en_eng", "in_ing"),
        "闽粤" to listOf("zh_z", "ch_c", "sh_s", "n_l", "f_h", "an_ang", "en_eng", "in_ing", "ian_iang", "uan_uang"),
        "江浙沪" to listOf("zh_z", "ch_c", "sh_s", "n_l", "en_eng", "in_ing"),
        "清除" to emptyList(),
    )

    /** 自定义标点预设：(说明, 按键, 输出)。写成 punct.txt 的行「按键<Tab>输出」。 */
    val PUNCT = listOf(
        Triple("/ 打 、", "/", "、"),
        Triple("\\ 打 、", "\\", "、"),
        Triple("` 打 ·", "`", "·"),
        Triple("[ ] 打 「」", "[", "「"),
    )

    /** 全拼方案（规则追加在最后）与要插到最前面的方案（双拼、九键：先把全拼转成键位/数字）。 */
    val FULL_SCHEMAS = listOf("rime_ice")
    val BEFORE_SCHEMAS = listOf("double_pinyin_flypy", "t9")

    fun dir(): File = File(DataManager.userDataDir, "yiwei").also { it.mkdirs() }

    fun f(name: String) = File(dir(), name)

    fun has(name: String) = f(name).exists()

    fun set(name: String, on: Boolean) {
        if (on) f(name).writeText("1") else f(name).delete()
    }

    fun read(name: String, def: String) = runCatching { f(name).readText().trim() }.getOrNull()?.ifEmpty { def } ?: def

    fun fuzzy(): Set<String> = read("fuzzy.txt", "").split(',', ' ', '\n').map { it.trim() }.filter { it.isNotEmpty() }.toSet()

    fun setFuzzy(ids: Set<String>) = f("fuzzy.txt").writeText(FUZZY.map { it.id }.filter { it in ids }.joinToString(","))

    /** punct.txt → Map(按键 → 输出)，与 Windows 同格式（# 注释，按键与输出之间 Tab 或空格）。 */
    fun punct(): Map<String, String> {
        val file = f("punct.txt")
        if (!file.exists()) return emptyMap()
        return file.readLines().mapNotNull { raw ->
            val l = raw.trim()
            if (l.isEmpty() || l.startsWith("#")) return@mapNotNull null
            val parts = l.split('\t', ' ').filter { it.isNotEmpty() }
            if (parts.size < 2) null else parts[0] to parts[1]
        }.toMap()
    }

    fun setPunct(map: Map<String, String>) {
        val sb = StringBuilder("# 一维输入法 · 自定义中文标点：每行「按键<Tab>输出」，# 开头为注释（电脑版、手机版通用）\n")
        map.forEach { (k, v) -> sb.append(k).append('\t').append(v).append('\n') }
        if (map["["] == "「" && "]" !in map) sb.append("]\t」\n")
        f("punct.txt").writeText(sb.toString())
    }

    private fun yq(s: String) = "\"" + s.replace("\\", "\\\\").replace("\"", "\\\"") + "\""

    /** 每个方案的补丁行（patch: 下面）。 */
    fun patchLines(schema: String): String {
        val y = StringBuilder()
        if (has("emoji.disabled")) y.append("  \"switches/@3/reset\": 0\n")
        val p = punct()
        if (p.isNotEmpty() && schema != "t9") {
            y.append("  \"punctuator/half_shape/+\": {")
            y.append(p.entries.joinToString(", ") { (k, v) -> yq(k) + ": " + yq(v) })
            y.append("}\n")
        }
        val rules = FUZZY.filter { it.id in fuzzy() }.flatMap { it.rules }
        if (rules.isNotEmpty()) {
            if (schema in FULL_SCHEMAS) {
                y.append("  \"speller/algebra/+\":\n")
                rules.forEach { y.append("    - ").append(it).append('\n') }
            } else {
                rules.forEachIndexed { i, r -> y.append("  \"speller/algebra/@before $i\": ").append(r).append('\n') }
            }
        }
        return y.toString()
    }

    private const val BEGIN = "  # yiwei-begin（一维输入法 输入设置生成，请勿手改这一段）"
    private const val END = "  # yiwei-end"

    /** 把补丁写进 <方案>.custom.yaml 的一维段落（保留用户自己的其他补丁）。返回是否有文件变化。 */
    fun writeCustom(): Boolean {
        var changed = false
        for (schema in FULL_SCHEMAS + BEFORE_SCHEMAS) {
            val file = File(DataManager.userDataDir, "$schema.custom.yaml")
            val old = if (file.exists()) file.readText() else ""
            val stripped = Regex("(?s)\\n?" + Regex.escape(BEGIN) + ".*?" + Regex.escape(END) + "\\n?")
                .replace(old, "\n").trimEnd('\n')
            val lines = patchLines(schema)
            val next = when {
                lines.isEmpty() ->
                    if (stripped.lines().map { it.trim() }.all { it.isEmpty() || it.startsWith("#") || it == "patch:" }) "" else stripped + "\n"
                Regex("(?m)^patch:\\s*$").containsMatchIn(stripped) ->
                    Regex("(?m)^patch:\\s*$").replaceFirst(
                        stripped,
                        Regex.escapeReplacement("patch:\n$BEGIN\n${lines.trimEnd('\n')}\n$END"),
                    ) + "\n"
                stripped.isBlank() -> "# 一维输入法\npatch:\n$BEGIN\n$lines$END\n"
                else -> stripped + "\npatch:\n$BEGIN\n$lines$END\n"
            }
            if (next == old) continue
            changed = true
            if (next.isBlank()) file.delete() else file.writeText(next)
        }
        return changed
    }
}

class YiweiInputPrefsWindow : BoardWindow.BarBoardWindow() {
    override val title: String = "输入设置"

    private val rime: RimeSession by di.instance()

    private var dirty = false
    private val fuzzyChips = mutableMapOf<String, TextView>()
    private var fuzzy = mutableSetOf<String>()

    private fun dp(v: Int) = (v * context.resources.displayMetrics.density).roundToInt()

    private val textColor get() = ColorManager.getColor("key_text_color")

    private fun chip(label: String, on: Boolean, onClick: (TextView) -> Unit): TextView = TextView(context).apply {
        text = label
        textSize = 14f
        gravity = Gravity.CENTER
        setPadding(dp(14), dp(7), dp(14), dp(7))
        paint(this, on)
        isClickable = true
        setOnClickListener { onClick(this) }
    }

    private fun paint(tv: TextView, on: Boolean) {
        tv.setTextColor(if (on) Color.WHITE else textColor)
        tv.typeface = if (on) Typeface.DEFAULT_BOLD else Typeface.DEFAULT
        tv.background = GradientDrawable().apply {
            cornerRadius = dp(16).toFloat()
            setColor(if (on) YiweiSymbolSide.ACCENT else ColorManager.getColor("key_back_color"))
        }
    }

    private fun flow(vararg views: View) = FlexboxLayout(context).apply {
        flexWrap = FlexWrap.WRAP
        views.forEach {
            addView(it, FlexboxLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                setMargins(dp(4), dp(4), dp(4), dp(4))
            })
        }
    }

    private fun header(t: String, hint: String = "") = TextView(context).apply {
        text = if (hint.isEmpty()) t else "$t  ·  $hint"
        textSize = 13f
        setTextColor(textColor)
        alpha = 0.75f
        setPadding(dp(8), dp(12), dp(8), dp(2))
    }

    private fun toast(s: String) = Toast.makeText(context, s, Toast.LENGTH_SHORT).show()

    override fun onCreateView(): View {
        val D = YiweiInputData
        fuzzy = D.fuzzy().toMutableSet()
        val col = LinearLayout(context).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(8), 0, dp(8), dp(12))
        }

        // 模糊音
        col.addView(header("模糊音", "选地区预设或单独开关，点「应用」生效"))
        val presets = D.PRESETS.map { (name, ids) ->
            chip(name, false) {
                fuzzy = ids.toMutableSet()
                refreshFuzzy()
                dirty = true
            }
        }
        col.addView(flow(*presets.toTypedArray()))
        val pairs = D.FUZZY.map { p ->
            chip(p.label, p.id in fuzzy) {
                if (!fuzzy.remove(p.id)) fuzzy.add(p.id)
                refreshFuzzy()
                dirty = true
            }.also { fuzzyChips[p.id] = it }
        }
        col.addView(flow(*pairs.toTypedArray()))

        // Emoji
        col.addView(header("Emoji 候选", "打「笑」出 😄"))
        var emojiOn = !D.has("emoji.disabled")
        val emoji = chip(if (emojiOn) "已开启" else "已关闭", emojiOn) { tv ->
            emojiOn = !emojiOn
            D.set("emoji.disabled", !emojiOn)
            tv.text = if (emojiOn) "已开启" else "已关闭"
            paint(tv, emojiOn)
            dirty = true
        }
        col.addView(flow(emoji))

        // 自定义标点
        col.addView(header("自定义标点", "中文状态下按键打出的符号"))
        val punct = D.punct().toMutableMap()
        val punctChips = D.PUNCT.map { (label, key, out) ->
            chip(label, punct[key] == out) { tv ->
                if (punct[key] == out) {
                    punct.remove(key)
                    if (key == "[") punct.remove("]")
                } else {
                    punct[key] = out
                    if (key == "[") punct["]"] = "」"
                }
                D.setPunct(punct)
                paint(tv, punct[key] == out)
                dirty = true
            }
        }
        col.addView(flow(*punctChips.toTypedArray()))

        // 按习惯调候选
        col.addView(header("按习惯调整候选", "常选的词自动排前面"))
        var learnOn = !D.has("learn.disabled")
        val learn = chip(if (learnOn) "已开启" else "已关闭", learnOn) { tv ->
            learnOn = !learnOn
            D.set("learn.disabled", !learnOn)
            tv.text = if (learnOn) "已开启" else "已关闭"
            paint(tv, learnOn)
        }
        val levels = listOf("1" to "学习：低", "2" to "学习：中", "3" to "学习：高")
        val levelChips = mutableListOf<TextView>()
        levels.forEach { (v, label) ->
            levelChips += chip(label, D.read("learn.level", "2") == v) {
                D.f("learn.level").writeText(v)
                levelChips.forEachIndexed { i, c -> paint(c, levels[i].first == v) }
            }
        }
        col.addView(flow(learn, *levelChips.toTypedArray()))
        val clears = listOf("3600" to "清除最近 1 小时", "86400" to "清除最近 1 天", "all" to "清除全部学习")
        col.addView(
            flow(
                *clears.map { (how, label) ->
                    chip(label, false) {
                        D.f("learn.clear").writeText(how)
                        toast("已清除（下次打字时生效）")
                    }
                }.toTypedArray(),
            ),
        )

        // 应用
        val apply = TextView(context).apply {
            text = "应用（重新部署）"
            textSize = 16f
            gravity = Gravity.CENTER
            setTextColor(Color.WHITE)
            typeface = Typeface.DEFAULT_BOLD
            background = GradientDrawable().apply {
                cornerRadius = dp(22).toFloat()
                setColor(YiweiSymbolSide.ACCENT)
            }
            isClickable = true
            setOnClickListener { applyNow() }
        }
        col.addView(
            apply,
            LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(44)).apply { setMargins(dp(8), dp(16), dp(8), 0) },
        )
        return ScrollView(context).apply {
            isVerticalScrollBarEnabled = false
            addView(col, ViewGroup.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT))
        }
    }

    private fun refreshFuzzy() {
        fuzzyChips.forEach { (id, tv) -> paint(tv, id in fuzzy) }
    }

    private fun applyNow() {
        try {
            YiweiInputData.setFuzzy(fuzzy)
            if (YiweiInputData.writeCustom()) {
                toast("正在重新部署，约 10–30 秒后生效")
                rime.launchOnReady { it.deploy() }
            } else {
                toast("设置没有变化")
            }
            dirty = false
        } catch (e: Exception) {
            Timber.w(e, "yiwei input prefs")
            toast("保存失败：${e.message}")
        }
    }

    override fun onAttached() {}

    // 离开面板时有改动但没点「应用」：自动应用
    override fun onDetached() {
        if (dirty) applyNow()
    }
}
