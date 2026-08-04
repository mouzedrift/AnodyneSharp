
using AnodyneSharp.Dialogue;
using AnodyneSharp.Drawing.Effects;
using AnodyneSharp.Entities;
using AnodyneSharp.Entities.Lights;
using AnodyneSharp.GameEvents;
using AnodyneSharp.Logging;
using AnodyneSharp.MapData;
using AnodyneSharp.States;
using AnodyneSharp.UI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnodyneSharp.Registry
{
    public enum GameMode
    {
        Normal,
        Chaos,
        EXTREME_CHAOS
    }


    public static class GlobalState
    {
        public class Save
        {
            public static JsonSerializerOptions serializerOptions = new()
            {
                IncludeFields = true
            };

            public Dictionary<string, List<int>> minimap_state = GlobalState.Minimaps.interest;
            public Dictionary<Guid, EntityState> entity_state = EntityManager.State;
            public Dictionary<string, DialogueNPC> dialogue_state = DialogueManager.SceneTree;
            public EventRegister events = GlobalState.Events;
            public InventoryManager inventory = GlobalState.Inventory;
            public CheckPoint checkpoint = GlobalState.checkpoint;
            public CheckPoint ReturnTarget = GlobalState.ReturnTarget;
            public int PillarSwitchOn = GlobalState.PillarSwitchOn;

            public long playtime = PlayTime.Ticks;
            public int current_health = _currentHealth;
            public int max_health = _maxHealth;
            public int deaths = DeathCount;

            public static Save GetSave(string path)
            {
                string save;
                try
                {
                    save = File.ReadAllText(path);
                }
                catch
                {
                    return null;
                }
                return FromString(save);
            }

            public static string PathFromId(string id)
            {
                return $"{GameConstants.SavePath}Saves/Save_{id}.dat";
            }

            public void SaveTo(string id)
            {
                File.WriteAllText(PathFromId(id), ToString());
            }

            public override string ToString()
            {
                return JsonSerializer.Serialize(this, serializerOptions);
            }

            public static Save FromString(string s)
            {
                return JsonSerializer.Deserialize<Save>(s, serializerOptions);
            }
        }

        static public string CurrentSaveGame = "1";

        public static string serialized_quicksave = null;
        public static CheckPoint quicksave_checkpoint = null;

        public static void SaveGame(string id = null)
        {
            new Save().SaveTo(id ?? CurrentSaveGame);
        }

        public static void LoadSave(Save s)
        {
            Minimaps.interest = s.minimap_state;
            EntityManager.State = s.entity_state;
            DialogueManager.SceneTree = s.dialogue_state;
            DialogueManager.Reload();
            Events = s.events;
            Inventory = s.inventory;
            checkpoint = s.checkpoint;
            ReturnTarget = s.ReturnTarget;
            PillarSwitchOn = s.PillarSwitchOn;

            PlayerWarpTarget = checkpoint.Position;
            NextMapName = checkpoint.map;

            _totalPreviously = new TimeSpan(s.playtime);
            MaxHealth = s.max_health;
            CurrentHealth = s.current_health;
            DeathCount = s.deaths;
        }

        public static void ResetValues()
        {
            _startTime = DateTime.Now;

            CurrentMapName = "";
            NextMapName = "BLANK";
            PlayerWarpTarget = new Vector2(23, 130 - 20); //original reports 23,130 but ends up subtracting HEADER_HEIGHT(20) from Y value

            NewMapFacing = Facing.RIGHT;

            _maxHealth = 6;
            _currentHealth = 6;

            DeathCount = 0;

            RefreshKeyCount = true;
            RefreshMaxHealth = true;

            AlwaysCellGraphics = false;
            GameMode = GameMode.Normal;

            InDeathRoom = false;

            Minimaps = new();
            Events = new();
            Inventory = new();
            DialogueManager.SceneTree = null;
            DialogueManager.Reload();
            EntityManager.State = new();
            PauseState.Reset();

            DisableMenu = false;
        }

        private static TimeSpan _totalPreviously;
        private static DateTime _startTime;

        public static TimeSpan PlayTime => _totalPreviously + (DateTime.Now - _startTime);

        public static string Dialogue
        {
            get
            {
                return _dialogue;
            }
            set
            {
                _dialogue = value;
                SetDialogueMode = value != "";
                LastDialogueFinished = value == "";
                if (value == "") DialogueTop = false;
            }
        }

        public static bool DialogueTop = false;
        public static bool LastDialogueFinished = true;
        public static bool SetDialogueMode = false;

        public static IEnumerator<CutsceneState.CutsceneEvent> StartCutscene = null;

        public static Point CurrentMapGrid
        {
            get
            {
                return new(CurrentGridX, CurrentGridY);
            }
        }

        public static Point TopLeftTile => new(CurrentGridX * 10, CurrentGridY * 10);

        public static Rectangle ScreenHitbox => new(CurrentGridX * 160, CurrentGridY * 160, 160, 160);

        public static bool CanChangeBroom
        {
            get
            {
                return !InDeathRoom && !IsCell && !IsKnife;
            }
        }

        public static bool IsCell
        {
            get
            {
                return AlwaysCellGraphics || CurrentMapName == "CELL";
            }
        }

        public static bool IsKnife
        {
            get
            {
                return CurrentMapName == "SUBURB";
            }
        }

        public static bool IsDungeon
        {
            get
            {
                return ReturnTarget != null && ReturnTarget.map != "NEXUS";
            }
        }

        public static Settings settings = Settings.Load();
        public static bool ResolutionDirty = false;
        public static Language CurrentLanguage => settings.language;

        public static int PillarSwitchOn = 0; //Reset when entering a map with different name

        public static bool ScreenTransition = false; //Whether a screen transition is happening right now
        public static int CurrentGridX;
        public static int CurrentGridY;
        public static int MapGridWidth;
        public static int MapGridHeight;
        public static string CurrentMapName;
        public static int EnemiesKilledRoom;
        public static int PuzzlesSolvedRoom;

        public static IStateSetter GameState;

        public static Action<Entity> SpawnEntity;

        public static Action<GameEvent> FireEvent;

        public static Action<State> SetSubstate;

        public static Action DoQuickSave;
        public static Action DoQuickLoad;

        public static IPublicMap Map;

        private static MinimapTracker Minimaps = new();

        public static Minimap CurrentMinimap => Minimaps.GetMinimap(CurrentMapName);

        public static EventRegister Events = new();
        public static InventoryManager Inventory = new();


        public static Facing? NewMapFacing = Facing.RIGHT;
        public static bool Warping = false;
        public static string NextMapName;
        public static Vector2 PlayerWarpTarget;

        public static bool RefreshKeyCount = false;
        public static bool RefreshMaxHealth = false;
        public static bool RefreshLabels = false;

        public static bool BoiEaster = false;

        public record CheckPoint
        {
            public string map;
            public Vector2 Position;

            [JsonConstructor]
            public CheckPoint(string map, Vector2 position)
            {
                this.map = map;
                Position = position;
            }

            public CheckPoint(DoorMapPair doorpair)
            {
                map = doorpair.Map;
                Position = doorpair.Door.Position;
            }

            public void Warp(Vector2 offset)
            {
                NextMapName = map;
                PlayerWarpTarget = Position + offset;
                Warping = true;
            }
        }
        public static CheckPoint checkpoint;
        public static CheckPoint ReturnTarget;

        /**
         * Used for disabling the menu during an event because you could potentially break the game  otherwise
         */
        public static bool DisableMenu = false;

        public static bool FuckItModeOn = false;

        public static bool DrawHitboxes = false;

        //Health stuff
        public static int CurrentHealth
        {
            get
            {
                return _currentHealth;
            }
            set
            {
                _currentHealth = Math.Clamp(value,0,MaxHealth);
            }
        }

        public static int MaxHealth
        {
            get
            {
                return _maxHealth;
            }
            set
            {
                _maxHealth = value <= 16 ? value : 16;
                RefreshMaxHealth = true;
            }
        }

        public static string DamageDealer = "";

        private static int _currentHealth = 6;
        private static int _maxHealth = 6;

        //Cheatz stuff
        public static bool AlwaysCellGraphics = false;
        public static GameMode GameMode = GameMode.Normal;
        public static bool ForceTextureReload = false;

        public static Random RNG = new Random();

        public static bool ShowFPS = false;

        //Cool new debug values
#if DEBUG
        public static bool DrawBG = true;
        public static bool DrawBG2 = true;
        public static bool DrawFG = true;

        public static bool MovingCamera = true;
        public static bool FreeRoamCamera = false;
#endif

        public static bool ClosingGame = false;
        public static bool ToTitle = false;

        public static Light PlayerLight = null;

        public static Darkness Darkness = new Darkness();
        public static FadeEffect GameScreenFade = new() { fadeColor = Color.Black };

        public static TitleScreenOverlay TitleScreenFinish = new TitleScreenOverlay();

        public static Static StaticEffect = new();

        public static ScreenShake ScreenShake = new();

        public static FadeEffect BlackOverlay = new FadeEffect() { fadeColor = Color.Black };
        public static FlashEffect Flash = new();

        public static Pixelate pixelation = new Pixelate();

        public static FG_Blend fgBlend = new();
        public static Blend extraBlend = new();


        public static Wave wave = new();
        public static Glitch glitch = new();

        public static List<IFullScreenEffect> gameEffects = new List<IFullScreenEffect>() { fgBlend, StaticEffect, Darkness, GameScreenFade };
        public static List<IFullScreenEffect> fullScreenEffects = new List<IFullScreenEffect>() { BlackOverlay, glitch, new GrayScale(), TitleScreenFinish, pixelation, extraBlend, wave, Flash, ScreenShake };
        public static IEnumerable<IFullScreenEffect> AllEffects
        {
            get
            {
                return gameEffects.Concat(fullScreenEffects);
            }
        }

        public static int DeathCount = 0;


        //Effect deciding bool
        public static bool InDeathRoom;

        private static string _dialogue;

        public static List<Entity> UIEntities = new List<Entity>();
    }
}
