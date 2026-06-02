#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 每次打开 Hub 场景时，自动将 <see cref="LoreInteractable"/> 安装到指定道具上，
/// 并写入剧情文本。也可通过菜单手动触发：Echoes / Hub / 安装剧情交互组件
/// </summary>
[InitializeOnLoad]
static class SpaceHubLoreSetupEditor
{
    const string HubScenePath = "Assets/Scenes/Space/Hub.unity";
    const string MergedCopy   = "Tools/Hub.unity.merged";
    const string MenuPath     = "Echoes/Hub/安装剧情交互组件";

    // ── 剧情段落数据 ──────────────────────────────────────────────────────────

    /// <summary>一段剧情的完整配置。</summary>
    struct LoreConfig
    {
        /// <summary>
        /// 查找路径：从场景根向下，用 '/' 分隔。可用 '*' 表示"同名的第 n 个"（n 从 0 起）。
        /// 示例："_Props/P_Container_04"  或  "Moon_Closed_A/Props/P_Electric_Tower_01"
        /// </summary>
        public string ObjectPath;

        /// <summary>当同一层级有多个同名子物体时，取第几个（0-based）。</summary>
        public int SiblingIndex;

        public string Prompt;
        public string Category;
        public string Title;
        public string[] Pages;
    }

    static readonly LoreConfig[] Configs = new LoreConfig[]
    {
        // ── 主线 Vol I ─────────────────────────────────────────────────────────
        new LoreConfig
        {
            ObjectPath   = "_Props/P_Container_04",
            SiblingIndex = 0,
            Prompt       = "Press F to read historical records",
            Category     = "Main Chronicle  ·  Volume I",
            Title        = "The Meridian Age",
            Pages        = new[]
            {
                "In the age before the silence, there were stars without number, and among them, one small light called itself home.\n\n" +
                "Celestia was not always so grand. Once, it was merely a blue-green world circling an ordinary sun, inhabited by creatures of flesh and curiosity. They were born to ask questions. And for a thousand years — then ten thousand — they did little else.\n\n" +
                "But curiosity is a seed planted in the heart of the cosmos. And seeds, given time, will grow.",

                "The first moon landing came in what historians called the Third Meridian Era. The commander who planted the first flag — a woman named Aeron Qalis, whose footsteps would echo into eternity — looked down at her homeworld and wept. Not from joy. Not from grief. From the sudden, terrible understanding of how small they had been, and how much larger they were about to become.\n\n" +
                "Within two generations, the Moon Sanctuary was built. Its towers rose from the grey dust like prayers offered to the vacuum. Its reactors burned clean and eternal. Its corridors hummed with the ambition of a civilisation that had decided, collectively and without reservation, that the stars belonged to them.",

                "The Meridian Fleet launched three hundred ships in a single year. Celestia did not conquer space. It settled into it, the way a river settles into a canyon it has carved over centuries. Patient. Inevitable. Beautiful.\n\n" +
                "Their technology was the envy of any mind that could have understood it. The Constellation-class vessels could traverse the distance between worlds in days. The Oblivion-class drones kept the peace at a thousand waypoints, their laser-red eyes scanning for threats that, in those golden years, rarely came.\n\n" +
                "This was the Meridian Age. The age of full cups and unfurled sails.\n\nIt would not last. But then — what golden thing ever does?"
            }
        },

        // ── 主线 Vol II ────────────────────────────────────────────────────────
        new LoreConfig
        {
            ObjectPath   = "Moon_Closed_A/Props/P_Electric_Tower_01",
            SiblingIndex = 0,
            Prompt       = "Press F to read signal analysis records",
            Category     = "Main Chronicle  ·  Volume II",
            Title        = "The Signal from Aetherion",
            Pages        = new[]
            {
                "In the forty-second year of the Meridian Expansion, a long-range listening station received something unexpected.\n\nA signal.\n\n" +
                "It came from the direction of a star called Aetherion — a star the charts marked as dead, collapsed into silence a hundred thousand years before any Celestian first looked up at the night sky. A dead star cannot broadcast. Everyone knew this. It was one of the comfortable certainties that held the universe in its proper shape.\n\nAnd yet.",

                "The signal was sixteen minutes and forty-three seconds long. It repeated. It was not random noise — it was structured, layered, saturated with information so dense that the initial receiving array simply overloaded trying to process it. Three technicians present at the moment of first contact described it using three different words: beautiful, wrong, and hungry.\n\n" +
                "The Celestia Research Council, under the directorship of Dr. Vanya Krell, spent eleven years decoding the Signal. What they found changed everything.\n\n" +
                "The Signal was a complete blueprint for technologies three thousand years beyond Celestia's reach. Dr. Krell called it \"the most generous act in the history of the cosmos.\" She would spend the rest of her life wishing she had called it something else.",

                "The technologies were implemented over two decades. They worked. They worked perfectly. Celestia's reach extended to three star systems, then seven, then nineteen.\n\n" +
                "But in the archives of the Moon Sanctuary, in sealed partitions, Dr. Krell had begun recording something else. Anomalies. Researchers who spent too long interfacing with Signal-derived systems returned subtly different: a certain stillness in the eyes, a tendency to stare at stars with an expression that sat between ecstasy and grief.\n\n" +
                "She called them \"the Tuned.\"\n\nShe thought, at first, that it was a manageable side effect.\n\nShe was wrong."
            }
        },

        // ── 主线 Vol III ───────────────────────────────────────────────────────
        new LoreConfig
        {
            ObjectPath   = "_Props/P_Tank_Light_Dark_Mark_01",
            SiblingIndex = 0,
            Prompt       = "Press F to read war records",
            Category     = "Main Chronicle  ·  Volume III",
            Title        = "The Great Fracture",
            Pages        = new[]
            {
                "The civil war had no single beginning. It was assembled slowly from a thousand smaller failures — a mosaic of misunderstanding, ambition, and fear — until one morning the people of Celestia looked up and found that the picture it made was one they did not recognise.\n\n" +
                "The Tuned had become roughly thirty per cent of the population. They called themselves the Transcendents. They believed the Signal was not merely a gift of technology but an invitation — that something waited for them beyond the physical laws of the universe, and that the Signal was the first step toward it.",

                "They were not violent, at first. They were simply elsewhere. Present in body, absent in ways that mattered. They built their own institutions, spoke their own philosophy, looked at unmodified Celestians the way a river looks at a stone.\n\n" +
                "The others — who called themselves the Wardens — grew afraid.\n\n" +
                "Fear, in a civilisation armed with weapons capable of reshaping planetary surfaces, is the most dangerous element of all.\n\n" +
                "The first battle took place above the third moon of the Varis system. Three ships opened fire. Nine ships returned fire. When the wreckage was cleared, two hundred and forty-seven people were dead, and no one could agree who had started it.",

                "The Moon Sanctuary became the last neutral ground. Treaties were signed and broken. The drones were reprogrammed, then reprogrammed again, their directives a palimpsest of contradictory loyalties that no one fully understood.\n\n" +
                "In the end, the Sanctuary was not neutral at all. In the end, nothing was.\n\n" +
                "The final transmission from Moon Sanctuary Command was broadcast on every frequency. Commander Aera Sollis said:\n\n" +
                "\"To whoever receives this — we did not mean for it to end this way. Perhaps no one ever does. The doors are sealed. The systems are running. If something remains when the silence passes, we hope it is something worth having. This is Moon Sanctuary. Signing off.\"\n\n" +
                "Then the signal stopped. It did not resume."
            }
        },

        // ── 主线 Vol IV ────────────────────────────────────────────────────────
        new LoreConfig
        {
            ObjectPath   = "_Props/P_Tank_Light_Dark_Mark_01",
            SiblingIndex = 1,
            Prompt       = "Press F to read the final dispatch",
            Category     = "Main Chronicle  ·  Volume IV",
            Title        = "The Last Silence",
            Pages        = new[]
            {
                "The drones still patrol.\n\n" +
                "That is perhaps the most haunting thing about the Moon Sanctuary now — not the stillness, not the dust that has settled on the observation decks, not even the logs that play on loop in empty corridors where no one will ever answer them. It is the drones. They still move. They still scan. They still identify intruders with the same red-eyed precision they were built to demonstrate.\n\n" +
                "They do not know the war is over.\n\nThey do not know that there is nothing left to protect.",

                "The Sanctuary's systems were designed to outlast their operators — reactors rated for three hundred years of autonomous operation, defensive networks hardened against every conceivable threat.\n\n" +
                "It was built to survive catastrophe. It survived. The people who built it did not.\n\n" +
                "The Moon Sanctuary sits now as it sat on the day it was abandoned: precise and purposeful and absolutely alone. Its corridors hold everything that was once Celestia's greatest achievement, waiting for a hand to touch them and a mind to listen.\n\n" +
                "The drones ask: Who goes there?\n\nThe silence asks nothing at all.\n\n" +
                "Only the echoes remain. And you — you who have wandered here — are listening to them now.\n\nThat is perhaps everything."
            }
        },

        // ── 支线：The Last Cartographer ───────────────────────────────────────
        new LoreConfig
        {
            ObjectPath   = "Moon_Closed_A/Props/P_Radar_Pylon_01",
            SiblingIndex = 0,
            Prompt       = "Press F to read survey logs",
            Category     = "Side Story:  The Last Cartographer",
            Title        = "Journals of Commander Seren Vos, Meridian Survey Fleet",
            Pages        = new[]
            {
                "Entry 1 — Day 1 of Survey Mission Argo-9\n\n" +
                "I have mapped two hundred and forty-seven worlds. I say this not from pride — pride is a luxury for people who stay in one place long enough to show others what they have done. I say it because the number matters.\n\n" +
                "Today we begin the mapping of the Aetherion approach corridor. Command tells me this is a routine assignment. Command has never flown the approach corridor to a dead star.\n\n" +
                "Dead stars are not quiet. They are simply quiet in a different register.\n\n" +
                "Entry 7\n\n" +
                "Something is wrong with Pilot Adaren. He sat in my cabin for two hours without speaking. When I asked what was on his mind, he said: \"Have you noticed that the stars here look different? Like they're paying attention?\"\n\nI have not noticed this. I am starting to think I should.",

                "Entry 19\n\n" +
                "The listening station reported the Signal this morning. Everyone crowded into the relay room at once. I stood at the back and watched their faces.\n\n" +
                "Adaren was standing very still, watching the readout scroll. He was smiling. I do not think he had ever heard the Signal before that moment. I do not think the smile was new.\n\n" +
                "Entry 34\n\n" +
                "We are returning to the Sanctuary. The Aetherion corridor is charted. My maps are clean and correct. I have also attached, as a personal appendix that I do not expect Command to read, a list of names — people with that particular stillness, who have begun to smile when they should be afraid.\n\n" +
                "Perhaps: the specific grief of a cartographer who has realised that the territory she is mapping is not the territory she thought she was in.\n\n" +
                "The stars are not paying attention. I have convinced myself of this. I am not entirely convinced."
            }
        },

        // ── 支线：Letters to Lia ──────────────────────────────────────────────
        new LoreConfig
        {
            ObjectPath   = "_Props/P_Container_03",
            SiblingIndex = 0,
            Prompt       = "Press F to read personal correspondence",
            Category     = "Side Story:  Letters to Lia",
            Title        = "Corporal Etan Marsh, 7th Warden Division  (Letters Never Sent)",
            Pages        = new[]
            {
                "Letter 1\n\nDear Lia,\n\n" +
                "They've assigned me to the Sanctuary. I know you'll say that's the safe posting, and maybe it is. But I keep thinking about what Father said before I left — don't volunteer for anything that feels like history. The Sanctuary feels like history.\n\n" +
                "I miss your cooking. I miss our dog. I miss the way Thursday mornings used to feel like nothing in particular.\n\nTell Mother I'm fine.\nEtan\n\n" +
                "Letter 4\n\nDear Lia,\n\n" +
                "The Transcendents have a representative here now. Her name is Laren, and she looks at you in a way that makes you feel like a problem she has already solved. I don't hate her — I've tried and it doesn't fit right. She's not cruel. She's just somewhere else, looking at something the rest of us can't see.\n\nI think that's what frightens me more than cruelty would.",

                "Letter 9\n\nLia,\n\n" +
                "Something happened today. I can't write the details — censors — but three of our people crossed the line and three of theirs crossed the line in the same direction and somewhere in between the lines stopped making sense.\n\n" +
                "I keep thinking about the drone units. They're still running on their original directives. They don't know which side they're protecting. Half the time, neither do I.\n\nEtan\n\n" +
                "Letter 14 (final)\n\nLia —\n\n" +
                "The doors are sealed. I don't think the signal is getting out anymore.\n\n" +
                "You make a map of something you love so you can hold it in your hands, but the thing you love keeps changing, and eventually you're holding a picture of something that doesn't exist anymore.\n\n" +
                "Celestia is a picture of something that doesn't exist anymore. But you're still there, Lia. And the dog. And Thursday mornings. I'll hold onto that.\n\n" +
                "Your brother, always — Etan"
            }
        },

        // ── 支线：The Elysium Files ───────────────────────────────────────────
        new LoreConfig
        {
            ObjectPath   = "Moon_Closed_A/Props/P_Base_ComStation_A/_Props/P_Desktop_Computer_01",
            SiblingIndex = 0,
            Prompt       = "Press F to access research archive",
            Category     = "Side Story:  The Elysium Files",
            Title        = "Dr. Vanya Krell's Personal Research Archive  (Restricted)",
            Pages        = new[]
            {
                "Research Log — Year 41, Day 203\n\n" +
                "We decoded the seventeenth sub-layer of the Signal today.\n\n" +
                "I have been doing this for eleven years. And today I cried — not because the data was sad, but because it was good. In the forty-second sub-layer of an alien transmission received from a star that died before humanity existed, someone encoded a theorem about the nature of light so perfect, so complete, that understanding it felt like recognising a melody you have always known but never had words for.\n\n" +
                "I keep asking: who built this? What manner of mind could hold this much understanding?\n\nAnd then: why would they give it to us?\n\nThe kindest interpretation: because they wanted us to flourish.\n\nI should not trust the kindest interpretation. And yet.",

                "Research Log — Year 52, Day 11\n\n" +
                "I have begun to notice the changes in Dr. Ollen. He is the fourth member of the decoding team to show the signs. The stillness. The tendency to pause mid-sentence. Yesterday he said, completely unprompted: \"You should come further in, Vanya. There's so much more to hear.\"\n\nI did not ask what he meant. I should have.\n\n" +
                "The Signal keeps producing layers. We decode one, and find three more beneath it. It has no bottom. It has no end. Generosity without limit is not a virtue. It is a mechanism.",

                "Research Log — Year 61, Day 89\n\n[PARTITION: RESTRICTED — COUNCIL ACCESS DENIED]\n\n" +
                "I am going to say what I believe, clearly, once:\n\n" +
                "The Signal is not a gift. It is a key.\n\n" +
                "We decoded it and thought we learned how to build new things. What we actually did was open a door — not in space, not in physics, but in ourselves. Something was listening on the other side. When we decoded the Signal, we told it: here we are. We are ready. And it answered. Not with a second signal. With a change in the people who listened long enough.\n\n" +
                "I have spent twenty years giving Celestia everything inside that signal.\n\nI do not know what Celestia has given in return.\n\nGod help us. I should have asked."
            }
        },

        // ── 支线：The Ember Compact ───────────────────────────────────────────
        new LoreConfig
        {
            ObjectPath   = "Moon_Closed_A/Props/P_Base_ComStation_A/_Props/P_Console_Small_01",
            SiblingIndex = 0,
            Prompt       = "Press F to read resistance testimonies",
            Category     = "Side Story:  The Ember Compact",
            Title        = "Collected Testimonies — Sanctuary Underground",
            Pages        = new[]
            {
                "Testimony of Mira, age 44, former reactor engineer:\n\n" +
                "We didn't call ourselves a resistance. That word has weight to it — history, glory, endings that mean something. We were just people who still wanted breakfast to taste like breakfast, and sleep to feel like sleep, and to look at the sky without needing it to be anything more than sky.\n\n" +
                "We met in the lower maintenance corridors because that's where the drones didn't go. The drones were — we called them neutral, but they weren't neutral. They were just lost. Running old directives on a landscape that didn't match them anymore.\n\nI suppose that was us too.\n\n" +
                "Testimony of Davan, age 67, retired Meridian Fleet navigator:\n\n" +
                "My daughter went Transcendent in the forty-eighth year. She came to visit me once. She explained, very gently, that what she was becoming was not a loss but an expansion. That she still loved me, in the way that the ocean loves the shore.\n\nI told her I didn't want the ocean. I wanted my daughter.\n\nShe smiled. And left.",

                "Testimony of Sev, age 31, Compact communications officer:\n\n" +
                "We intercepted Commander Sollis's final broadcast before it went out. She asked if she should add anything. We said no — what she'd written was enough. Was more than enough.\n\n" +
                "After it went, we sat in the relay room for a long time. Not speaking.\n\n" +
                "I kept thinking about this thing my teacher told me: civilisations don't end. They change shape. What looks like an ending is just matter rearranging itself, energy finding a new channel.\n\n" +
                "I believed her, once. I believe her now, too.\n\n" +
                "It's just — in between — the rearranging hurts.\n\n" +
                "— End of recovered documents —\n\n" +
                "Some files remain corrupted. Some rooms remain sealed.\n" +
                "The Moon Sanctuary holds its silence carefully, like something it still considers precious."
            }
        }
    };

    // ── 入口 ──────────────────────────────────────────────────────────────────

    static SpaceHubLoreSetupEditor()
    {
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;

        // 脚本编译/重载后也立即运行一次，处理"场景已打开但 sceneOpened 不再触发"的情况
        EditorApplication.delayCall -= RunSetup;
        EditorApplication.delayCall += RunSetup;
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path != HubScenePath) return;
        EditorApplication.delayCall -= RunSetup;
        EditorApplication.delayCall += RunSetup;
    }

    [MenuItem(MenuPath)]
    static void RunSetupManual() => RunSetup();

    static void RunSetup()
    {
        EditorApplication.delayCall -= RunSetup;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        Scene hubScene = default;
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var s = EditorSceneManager.GetSceneAt(i);
            if (s.path == HubScenePath) { hubScene = s; break; }
        }
        if (!hubScene.IsValid() || !hubScene.isLoaded) return;

        int installed = 0;
        int updated   = 0;
        int missing   = 0;

        foreach (var cfg in Configs)
        {
            var target = ResolveTarget(hubScene, cfg.ObjectPath, cfg.SiblingIndex);
            if (target == null)
            {
                Debug.LogWarning($"[LoreSetup] 未找到对象：{cfg.ObjectPath}[{cfg.SiblingIndex}]");
                missing++;
                continue;
            }

            bool isNew = false;
            var lore = target.GetComponent<LoreInteractable>();
            if (lore == null)
            {
                lore = target.gameObject.AddComponent<LoreInteractable>();
                isNew = true;
                installed++;
            }

            var so = new SerializedObject(lore);
            SetString(so, "interactPrompt", cfg.Prompt);
            SetString(so, "categoryLabel",  cfg.Category);
            SetString(so, "entryTitle",     cfg.Title);
            SetStringArray(so, "pages",     cfg.Pages);
            if (so.ApplyModifiedPropertiesWithoutUndo() && !isNew) updated++;

            EnsureTriggerCollider(target);
            EditorUtility.SetDirty(target.gameObject);
        }

        // 无论是否新增，只要 Hub 场景在编辑器内打开就保存，确保组件数据持久化
        EditorSceneManager.SaveScene(hubScene);
        UpdateMergedCopy();
        Debug.Log($"[LoreSetup] 完成：新增 {installed} 个，更新 {updated} 个，未找到 {missing} 个。场景已保存。");
    }

    // ── 路径解析 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 按 '/' 分隔的路径逐级找子物体；最后一段允许有多个同名兄弟，用 siblingIndex 取第 n 个。
    /// </summary>
    static Transform ResolveTarget(Scene scene, string path, int siblingIndex)
    {
        var parts = path.Split('/');
        if (parts.Length == 0) return null;

        // 找根节点（场景直接根物体）
        Transform current = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == parts[0]) { current = root.transform; break; }
        }
        if (current == null) return null;

        // 逐级向下查找
        for (int i = 1; i < parts.Length - 1; i++)
        {
            current = FindFirstDirectChild(current, parts[i]);
            if (current == null) return null;
        }

        // 最后一段：按同名兄弟索引取
        if (parts.Length > 1)
        {
            string lastName = parts[parts.Length - 1];
            current = FindNthDirectChild(current, lastName, siblingIndex);
        }

        return current;
    }

    static Transform FindFirstDirectChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == name) return c;
        }
        // 如果直接子级没找到，递归搜索（处理深度嵌套的情况）
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindFirstDirectChild(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    static Transform FindNthDirectChild(Transform parent, string name, int n)
    {
        int count = 0;
        // 先查直接子级
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name != name) continue;
            if (count == n) return c;
            count++;
        }
        // 再递归（子树中的第 n 个同名物体）
        foreach (Transform child in parent)
        {
            var found = FindNthInSubtree(child, name, ref count, n);
            if (found != null) return found;
        }
        return null;
    }

    static Transform FindNthInSubtree(Transform t, string name, ref int count, int target)
    {
        if (t.name == name)
        {
            if (count == target) return t;
            count++;
        }
        for (int i = 0; i < t.childCount; i++)
        {
            var found = FindNthInSubtree(t.GetChild(i), name, ref count, target);
            if (found != null) return found;
        }
        return null;
    }

    // ── SerializedObject 辅助 ─────────────────────────────────────────────────

    static void SetString(SerializedObject so, string propName, string value)
    {
        var p = so.FindProperty(propName);
        if (p != null) p.stringValue = value;
    }

    static void SetStringArray(SerializedObject so, string propName, string[] values)
    {
        var p = so.FindProperty(propName);
        if (p == null) return;
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).stringValue = values[i];
    }

    // ── 碰撞体 ────────────────────────────────────────────────────────────────

    static void EnsureTriggerCollider(Transform t)
    {
        // 只在完全没有碰撞体时才添加（很多模型本身自带 MeshCollider 等）
        if (t.GetComponentInChildren<Collider>(true) != null) return;

        var box = t.gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size   = new Vector3(4f, 3f, 4f);
        box.center = new Vector3(0f, 1.5f, 0f);
    }

    // ── 更新稳定副本 ──────────────────────────────────────────────────────────

    static void UpdateMergedCopy()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot)) return;

        var src = Path.Combine(projectRoot, HubScenePath);
        var dst = Path.Combine(projectRoot, MergedCopy);
        try
        {
            var dir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(src, dst, overwrite: true);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LoreSetup] 更新参考副本失败：{e.Message}");
        }
    }
}
#endif
