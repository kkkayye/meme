using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Content;
using RuneArena.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RuneArena.UI
{
    /// <summary>Main menu (team size, seed, start / spectate), hero select, round-end banner, match-end results, pause overlay.</summary>
    public sealed class MenuPanels : MonoBehaviour
    {
        private static readonly int[] TeamSizes = { 1, 2, 3 };

        private RectTransform _mainMenu;
        private RectTransform _heroSelect;
        private RectTransform _roundEnd;
        private RectTransform _matchEnd;
        private RectTransform _pause;
        private InputField _seedField;
        private Text _roundEndText;
        private Text _matchEndTitle;
        private Text _matchEndStats;
        private readonly List<Button> _sizeButtons = new List<Button>();
        private readonly List<string> _heroIds = new List<string>();
        private int _teamSize = GameConstants.DefaultTeamSize;

        public static MenuPanels Build(Transform parent)
        {
            RectTransform root = UiFactory.Stretch(parent, "Menus");
            MenuPanels menus = root.gameObject.AddComponent<MenuPanels>();
            menus.BuildMainMenu(root);
            menus.BuildHeroSelect(root);
            menus.BuildRoundEnd(root);
            menus.BuildMatchEnd(root);
            menus.BuildPause(root);
            menus.HideAll();
            return menus;
        }

        public void ShowMainMenu() { HideAll(); _mainMenu.gameObject.SetActive(true); }
        public void ShowHeroSelect() { HideAll(); _heroSelect.gameObject.SetActive(true); }

        public void ShowRoundEnd(Team winner, int round)
        {
            HideAll();
            _roundEndText.text = UiStyle.Colored(UiText.TeamName(winner) + " 赢得第 " + round + " 回合", UiStyle.TeamColor(winner)) + "\n<size=30>" + UiText.TeamName(winner) + " wins round " + round + "</size>";
            _roundEnd.gameObject.SetActive(true);
        }

        public void ShowMatchEnd(Team winner)
        {
            HideAll();
            _matchEndTitle.text = UiStyle.Colored(UiText.TeamName(winner) + " 获胜  " + UiText.TeamName(winner) + " wins the match!", UiStyle.TeamColor(winner));
            _matchEndStats.text = BuildStats();
            _matchEnd.gameObject.SetActive(true);
        }

        public void ShowPause(bool visible)
        {
            _pause.gameObject.SetActive(visible);
        }

        public void HideAll()
        {
            _mainMenu.gameObject.SetActive(false);
            _heroSelect.gameObject.SetActive(false);
            _roundEnd.gameObject.SetActive(false);
            _matchEnd.gameObject.SetActive(false);
            _pause.gameObject.SetActive(false);
        }

        private void BuildMainMenu(RectTransform root)
        {
            _mainMenu = UiFactory.Stretch(root, "MainMenu");
            UiFactory.Overlay(_mainMenu, "Bg", UiStyle.Opaque);
            Text title = UiFactory.OutlinedText(_mainMenu, "Title", "符文竞技场\n<size=40>RUNE ARENA</size>", 72, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 240f), new Vector2(900f, 160f));
            Text sub = UiFactory.Text(_mainMenu, "Sub", "符文抽选 · 装备商店 · 宝箱抽卡 · 回合制竞技\nWASD 移动  鼠标瞄准  左键普攻  Q/W/E/R 技能", 24, UiStyle.TextMuted, TextAnchor.MiddleCenter);
            UiFactory.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1000f, 70f));
            Text sizeLabel = UiFactory.Text(_mainMenu, "SizeLabel", "队伍规模 Team size", 24, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(sizeLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(600f, 32f));
            for (int i = 0; i < TeamSizes.Length; i++)
            {
                int size = TeamSizes[i];
                Button b = UiFactory.Button(_mainMenu, "Size" + size, size + "v" + size, 26, () => SetTeamSize(size));
                UiFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 150f, -10f), new Vector2(130f, 50f));
                _sizeButtons.Add(b);
            }
            SetTeamSize(_teamSize);
            Text seedLabel = UiFactory.Text(_mainMenu, "SeedLabel", "种子 Seed (0 = random)", 22, UiStyle.TextMuted, TextAnchor.MiddleCenter);
            UiFactory.Place(seedLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(600f, 30f));
            _seedField = UiFactory.InputField(_mainMenu, "Seed", "0", new Vector2(240f, 44f));
            UiFactory.Place((RectTransform)_seedField.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(240f, 44f));
            Button start = UiFactory.Button(_mainMenu, "Start", "开始 Start  (Enter)", 32, () => StartMatch(true));
            UiFactory.SetButtonColor(start, UiStyle.ButtonSelected);
            UiFactory.Place((RectTransform)start.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), new Vector2(320f, 64f));
            Button spectate = UiFactory.Button(_mainMenu, "Spectate", "观战 Bots only", 22, () => StartMatch(false));
            UiFactory.Place((RectTransform)spectate.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -262f), new Vector2(260f, 48f));
        }

        private void SetTeamSize(int size)
        {
            _teamSize = size;
            for (int i = 0; i < _sizeButtons.Count; i++)
            {
                UiFactory.SetButtonColor(_sizeButtons[i], TeamSizes[i] == size ? UiStyle.ButtonSelected : UiStyle.ButtonNormal);
            }
        }

        private void StartMatch(bool human)
        {
            int seed = 0;
            if (_seedField != null && !string.IsNullOrEmpty(_seedField.text)) int.TryParse(_seedField.text, out seed);
            var config = new MatchConfig { TeamSize = _teamSize, Seed = seed, HumanPlayer = human };
            if (GameServices.Match == null) return;
            if (human) GameServices.Match.BeginHeroSelect(config);
            else GameServices.Match.StartMatch(config);
        }

        private void BuildHeroSelect(RectTransform root)
        {
            _heroSelect = UiFactory.Stretch(root, "HeroSelect");
            UiFactory.Overlay(_heroSelect, "Bg", UiStyle.Opaque);
            Text title = UiFactory.OutlinedText(_heroSelect, "Title", "选择英雄  Choose your hero   <size=26>(按 1 / 2 / 3)</size>", 48, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(900f, 60f));
            IReadOnlyList<HeroDefinition> heroes = ContentCatalog.Heroes;
            var size = new Vector2(440f, 560f);
            for (int i = 0; i < heroes.Count; i++)
            {
                HeroDefinition hero = heroes[i];
                _heroIds.Add(hero.Id);
                float x = (i - (heroes.Count - 1) * 0.5f) * (size.x + 40f);
                RectTransform inner = UiFactory.Card(_heroSelect, "Hero_" + hero.Id, size, hero.Color, UiStyle.CardBg, 6f);
                RectTransform border = (RectTransform)inner.parent;
                UiFactory.Place(border, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -20f), size);
                border.gameObject.AddComponent<HoverScale>().Scale = 1.03f;
                Text name = UiFactory.Text(inner, "Name", UiStyle.Colored(hero.Name, hero.Color) + "\n<size=22>" + hero.Role + "</size>", 36, UiStyle.TextMain, TextAnchor.MiddleCenter);
                UiFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(size.x - 30f, 80f));
                Text body = UiFactory.Text(inner, "Body", hero.Description + "\n\n" + UiText.HeroStatLine(hero) + "\n\n" + SkillLines(hero), 19, UiStyle.TextMain, TextAnchor.UpperLeft);
                UiFactory.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(size.x - 40f, 360f));
                string id = hero.Id;
                Button pick = UiFactory.Button(inner, "Pick", "选择 Select  (" + (i + 1) + ")", 26, () => GameServices.Match?.ConfirmHero(id));
                UiFactory.SetButtonColor(pick, UiStyle.Darken(hero.Color, 0.7f));
                UiFactory.Place((RectTransform)pick.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(size.x - 60f, 54f));
            }
            Button back = UiFactory.Button(_heroSelect, "Back", "返回 Back", 22, () => GameServices.Match?.ReturnToMenu());
            UiFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(220f, 48f));
        }

        private static string SkillLines(HeroDefinition hero)
        {
            string text = UiText.SkillSummary(hero.BasicAttack);
            for (int i = 0; i < hero.Skills.Count; i++) text += "\n" + UiText.SkillSummary(hero.Skills[i]);
            return text;
        }

        private void BuildRoundEnd(RectTransform root)
        {
            _roundEnd = UiFactory.Stretch(root, "RoundEnd");
            _roundEndText = UiFactory.OutlinedText(_roundEnd, "Text", "", 60, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(_roundEndText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1200f, 140f));
        }

        private void BuildMatchEnd(RectTransform root)
        {
            _matchEnd = UiFactory.Stretch(root, "MatchEnd");
            UiFactory.Overlay(_matchEnd, "Dim", UiStyle.Dim);
            _matchEndTitle = UiFactory.OutlinedText(_matchEnd, "Title", "", 54, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(_matchEndTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1200f, 80f));
            _matchEndStats = UiFactory.Text(_matchEnd, "Stats", "", 26, UiStyle.TextMain, TextAnchor.UpperCenter);
            UiFactory.Place(_matchEndStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(900f, 300f));
            Button rematch = UiFactory.Button(_matchEnd, "Rematch", "再来一局 Rematch", 28, () => GameServices.Match?.Rematch());
            UiFactory.SetButtonColor(rematch, UiStyle.ButtonSelected);
            UiFactory.Place((RectTransform)rematch.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-170f, -200f), new Vector2(300f, 60f));
            Button menu = UiFactory.Button(_matchEnd, "Menu", "主菜单 Main Menu", 28, () => GameServices.Match?.ReturnToMenu());
            UiFactory.Place((RectTransform)menu.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(170f, -200f), new Vector2(300f, 60f));
        }

        private void BuildPause(RectTransform root)
        {
            _pause = UiFactory.Stretch(root, "Pause");
            UiFactory.Overlay(_pause, "Dim", UiStyle.Dim);
            Text title = UiFactory.OutlinedText(_pause, "Title", "暂停  Paused", 54, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 100f), new Vector2(800f, 80f));
            Button resume = UiFactory.Button(_pause, "Resume", "继续 Resume (Esc)", 28, () => GameServices.Match?.TogglePause());
            UiFactory.SetButtonColor(resume, UiStyle.ButtonSelected);
            UiFactory.Place((RectTransform)resume.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(340f, 60f));
            Button menu = UiFactory.Button(_pause, "Menu", "退出到主菜单 Main Menu", 24, () => GameServices.Match?.ReturnToMenu());
            UiFactory.Place((RectTransform)menu.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(340f, 56f));
        }

        /// <summary>Keyboard shortcuts: Enter starts from the main menu (or rematches), 1/2/3 pick a hero.</summary>
        private void Update()
        {
            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (_mainMenu.gameObject.activeInHierarchy)
            {
                if (enter) StartMatch(true);
                return;
            }
            if (_heroSelect.gameObject.activeInHierarchy)
            {
                for (int i = 0; i < _heroIds.Count && i < 3; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) GameServices.Match?.ConfirmHero(_heroIds[i]);
                }
                return;
            }
            if (_matchEnd.gameObject.activeInHierarchy && enter) GameServices.Match?.Rematch();
        }

        private static string BuildStats()
        {
            var match = GameServices.Match;
            if (match == null) return "";
            string text = "";
            for (int i = 0; i < match.Units.Count; i++)
            {
                Unit u = match.Units[i];
                if (u == null) continue;
                string line = u.UnitName + "   击杀 " + u.Kills + "   死亡 " + u.Deaths + "   符文 " + (u.Runes != null ? u.Runes.TotalRunes : 0) + "   装备 " + (u.Items != null ? u.Items.Count : 0);
                text += UiStyle.Colored(line, UiStyle.TeamColor(u.Team)) + "\n";
            }
            return text;
        }
    }
}
