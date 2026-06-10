namespace tool_r1ng.Utilities;

public static class PinyinInitials
{
    private static readonly Dictionary<char, char> Initials = BuildInitials();

    public static IReadOnlyList<PinyinInitial> Build(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var initials = new List<PinyinInitial>();
        for (var index = 0; index < value.Length; index++)
        {
            if (TryGetInitial(value[index], out var initial))
            {
                initials.Add(new PinyinInitial(initial, index));
            }
        }

        return initials;
    }

    private static bool TryGetInitial(char character, out char initial)
    {
        if (character is >= 'A' and <= 'Z')
        {
            initial = char.ToLowerInvariant(character);
            return true;
        }

        if (character is >= 'a' and <= 'z')
        {
            initial = character;
            return true;
        }

        return Initials.TryGetValue(character, out initial);
    }

    private static Dictionary<char, char> BuildInitials()
    {
        var initials = new Dictionary<char, char>();

        Add(initials, 'a', "阿啊锕");
        Add(initials, 'b', "八吧巴拔把爸霸白百柏败班般搬板半办帮包胞宝保报抱暴杯北备背本比笔必毕闭边变标表别宾冰兵病并播伯博补不布步部编便辨辩瓣卞");
        Add(initials, 'c', "擦才财采彩菜参仓藏曹草册侧测层曾查茶差产长常场厂唱超车陈成城程吃持尺赤冲充虫抽出初除楚处传窗床创春纯次词从丛凑粗促村存错");
        Add(initials, 'd', "大达打带代待单但旦当党到道得德灯等低地第点电店调丁定东动懂都斗读独度端短段对顿多朵躲");
        Add(initials, 'e', "俄额恶饿恩而二耳");
        Add(initials, 'f', "发法反饭方房放飞非费分份粉封风峰凤服福府复负富符");
        Add(initials, 'g', "该改盖干感刚高告哥歌个各给根更工公功共沟够古故顾瓜关管光广规贵国果过");
        Add(initials, 'h', "哈还海害含汉行好号合河核黑很红后候湖护花画话怀环换黄回会惠混火或获");
        Add(initials, 'j', "机级极几己技际季加家假价架间件建见键江讲交教接节结解界今金进近京经精井景静究九就局据决绝觉军均辑记计集");
        Add(initials, 'k', "开看康考靠科可课空口库快块款况");
        Add(initials, 'l', "拉来蓝览老了类里理力立利连联练两量亮聊料列林临领令流六龙楼路录陆旅绿乱论落");
        Add(initials, 'm', "妈马吗买卖满慢忙毛么没美门们梦米面名明命模末某目");
        Add(initials, 'n', "拿哪那纳南难脑内能你年念鸟您宁牛农弄努女暖");
        Add(initials, 'o', "哦欧偶");
        Add(initials, 'p', "排盘旁跑配朋碰批片偏品平评破普");
        Add(initials, 'q', "七期其奇起气器前钱强桥切且亲清情请区取去全权确群签");
        Add(initials, 'r', "然让热人认任日容荣如入软");
        Add(initials, 's', "三散色森杀山删闪善上少设社身神生声省师十时实识使始市式事是手受书输术数双水说司思私四送搜速算随所锁");
        Add(initials, 't', "他她它台太谈探堂套特提体天条调贴听通同统图土团推退托");
        Add(initials, 'w', "外完玩万网往为位未文问我五务物误无午");
        Add(initials, 'x', "西希息习席喜系细下先显现线相想向项小效些写谢新心信星行形性修需许序选学寻讯");
        Add(initials, 'y', "呀亚言研严眼演验央样要也页业夜一已以义意易因音引印应英影用由有又于与语雨玉预元原员园远院愿月越云运");
        Add(initials, 'z', "杂再在早造责则怎增展占站张找照者这真阵正政之知织直值指至制中种重州周主注助住抓专转装状追准资子字自总走组足族最罪作做坐座");

        return initials;
    }

    private static void Add(IDictionary<char, char> initials, char initial, string characters)
    {
        foreach (var character in characters)
        {
            initials.TryAdd(character, initial);
        }
    }
}

public sealed record PinyinInitial(char Initial, int Index);
