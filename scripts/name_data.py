"""W6 人名候选的数据：常见姓氏（带读音，含复姓）和常用名字用字（按大致常用程度排序，读音取雾凇字表里最常用的音）。"""

SURNAMES = """王wang 李li 张zhang 刘liu 陈chen 杨yang 黄huang 赵zhao 吴wu 周zhou 徐xu 孙sun 马ma 朱zhu 胡hu 郭guo 何he 高gao
林lin 罗luo 郑zheng 梁liang 谢xie 宋song 唐tang 许xu 韩han 冯feng 邓deng 曹cao 彭peng 曾zeng 肖xiao 田tian 董dong 袁yuan
潘pan 于yu 蒋jiang 蔡cai 余yu 杜du 叶ye 程cheng 苏su 魏wei 吕lv 丁ding 任ren 沈shen 姚yao 卢lu 姜jiang 崔cui 钟zhong 谭tan
陆lu 汪wang 范fan 金jin 石shi 廖liao 贾jia 夏xia 韦wei 付fu 方fang 白bai 邹zou 孟meng 熊xiong 秦qin 邱qiu 江jiang 尹yin
薛xue 闫yan 段duan 雷lei 侯hou 龙long 史shi 陶tao 黎li 贺he 顾gu 毛mao 郝hao 龚gong 邵shao 万wan 钱qian 严yan 覃qin 武wu
戴dai 莫mo 孔kong 向xiang 汤tang 常chang 温wen 康kang 施shi 文wen 牛niu 樊fan 葛ge 邢xing 安an 齐qi 易yi 乔qiao 伍wu 庞pang
颜yan 倪ni 庄zhuang 聂nie 章zhang 鲁lu 岳yue 翟zhai 殷yin 詹zhan 申shen 欧ou 耿geng 关guan 兰lan 焦jiao 俞yu 左zuo 柳liu
甘gan 祝zhu 包bao 宁ning 尚shang 符fu 舒shu 阮ruan 柯ke 纪ji 梅mei 童tong 凌ling 毕bi 单shan 季ji 裴pei 霍huo 涂tu 成cheng
苗miao 谷gu 盛sheng 曲qu 翁weng 冉ran 骆luo 蓝lan 路lu 游you 辛xin 靳jin 管guan 柴chai 蒙meng 鲍bao 华hua 喻yu 祁qi 蒲pu
房fang 滕teng 屈qu 饶rao 解xie 牟mou 艾ai 尤you 阳yang 时shi 穆mu 农nong 司si 卓zhuo 古gu 吉ji 缪miao 简jian 车che 项xiang
连lian 芦lu 麦mai 褚chu 娄lou 窦dou 戚qi 岑cen 景jing 党dang 宫gong 费fei 卜bu 冷leng 晏yan 席xi 卫wei 米mi 柏bai 宗zong
瞿qu 桂gui 全quan 佟tong 应ying 臧zang 闵min 苟gou 邬wu 边bian 卞bian 姬ji 师shi 和he 仇qiu 栾luan 隋sui 商shang 刁diao
沙sha 荣rong 巫wu 寇kou 桑sang 郎lang 甄zhen 丛cong 仲zhong 虞yu 敖ao 巩gong 明ming 佘she 池chi 查zha 麻ma 苑yuan 邝kuang
欧阳ouyang 司马sima 诸葛zhuge 上官shangguan 东方dongfang 皇甫huangfu 令狐linghu 慕容murong 夏侯xiahou 尉迟yuchi 公孙gongsun 长孙zhangsun"""

GIVEN = """伟 芳 娜 敏 静 丽 强 磊 军 洋 勇 艳 杰 娟 涛 明 超 秀 霞 平 刚 桂 英 华 玉 萍 红 玲 芬 燕 彬 鹏 辉 宇 浩 然 子
轩 涵 梓 欣 怡 宁 思 雨 晨 博 文 嘉 一 诺 依 佳 琪 雪 婷 颖 晓 琳 倩 萱 悦 瑶 晴 妍 泽 豪 俊 凯 鑫 斌 毅 峰 波 飞 龙 林 森
健 康 旭 阳 昊 天 志 国 建 新 海 春 梅 兰 香 云 凤 洁 慧 琴 莉 荣 丹 晶 蕾 薇 娇 媛 菲 璐 露 曦 彤 馨 淼 冉 可 心 安 乐 诗
语 若 梦 墨 言 逸 航 睿 哲 弘 亮 铭 宏 远 立 正 永 德 成 和 东 世 中 振 庆 江 河 山 川 岩 石 金 宝 贵 福 祥 瑞 民 学 武 兵
光 耀 祖 家 庭 雅 婉 秋 冬 夏 月 星 辰 玮 祎 维 威 卫 蔚 微 炜 巍 帆 凡 菁 靖 景 婧 京 昕 歆 萌 蒙 盟 珈 紫 姿 恩 尔 宸 奕
瀚 煜 皓 熙 锦 程 晗 钰 琦 瑜 茜 蓉 媚 婕 妮 嫣 霖 沐 柏 楠 桐 槿 芮 苒 蕊 菡 芊 茹 莹 盈 莺 翔 骏 驰 腾 崇 瑾 璇 琰 珊
珍 珏 璟 琛 瑞 祺 禹 尧 舜 涵 漫 澜 溪 清 源 润 洪 涌 鸿 鹤 凌 霄 震 霆 雷 电 冰 寒 烨 炎 焱 灿 朗 晟 昱 晖 暄 晔 曜 昭
"""

# 多音字在名字里的读音
GIVEN_READING = {"乐": "le", "长": "chang", "行": "xing", "重": "chong", "茜": "qian", "朝": "zhao", "和": "he", "调": "tiao"}
