using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Localization;
using Sandbox.Game.WorldEnvironment.Modules;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Network;
using VRageMath;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        const string INI_SECTION_GENERAL = "Airlock Manager - General Settings";

        const string AIRLOCK_GROUP_TAG = "Airlock Group Tag";
        const string AIRLOCK_INNER_TAG = "Airlock Inner Tag";
        const string AIRLOCK_OUTER_TAG = "Airlock Outer Tag";

        const string RUN_ACTION_NAME = "Run";

        MyIni _ini = new MyIni();

        string _airlockGroupTag = "[Airlock]";
        string _airlockInnerTag = "[Inner]";
        string _airlockOuterTag = "[Outer]";

        List<AirLock> _airlocks;

        IMyDoor _door;

        public Program()
        {
            LoadIni();

            _airlocks = new List<AirLock>();
            List<IMyBlockGroup> airlockGroups = new List<IMyBlockGroup>();

            GridTerminalSystem.GetBlockGroups(airlockGroups, group => group.Name.Contains(_airlockGroupTag));

            foreach (IMyBlockGroup group in airlockGroups)
            {
                AirLock airlock = new AirLock(this, group);
                _airlocks.Add(airlock);

                Echo($"Found airlock: {airlock.Name}: {airlock}");
            }

            List<ITerminalAction> actions = new List<ITerminalAction>();
            Me.GetActions(actions);

            PrintToScreen($"Panel {Me.CustomName} - Action Count: {actions.Count}", false);

            foreach (ITerminalAction action in actions)
            {
                PrintToScreen($"  {action.Name}");
            }

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        void LoadIni()
        {
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
                Echo($"CustomData error:\nLine {result}");

            _airlockGroupTag = _ini.Get(INI_SECTION_GENERAL, AIRLOCK_GROUP_TAG).ToString(_airlockGroupTag);
            _ini.Set(INI_SECTION_GENERAL, AIRLOCK_GROUP_TAG, _airlockGroupTag);

            _airlockInnerTag = _ini.Get(INI_SECTION_GENERAL, AIRLOCK_INNER_TAG).ToString(_airlockInnerTag);
            _ini.Set(INI_SECTION_GENERAL, AIRLOCK_INNER_TAG, _airlockInnerTag);

            _airlockOuterTag = _ini.Get(INI_SECTION_GENERAL, AIRLOCK_OUTER_TAG).ToString(_airlockOuterTag);
            _ini.Set(INI_SECTION_GENERAL, AIRLOCK_OUTER_TAG, _airlockOuterTag);

            string iniOutput = _ini.ToString();

            if (iniOutput != Me.CustomData)
            {
                Me.CustomData = iniOutput;
            }

            string doorName = _ini.Get("demo", "door").ToString();

            if (doorName != null)
            {
                _door = GridTerminalSystem.GetBlockWithName(doorName) as IMyDoor;

                if (_door == null)
                    Echo($"No door named {doorName}");
            }
            else
                Echo($"No output panel defined under demo/door");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            //PrintToScreen($"Started with args: {argument}, type: {updateSource}\n");

            if (_door != null)
            {
                switch (updateSource)
                {
                    case UpdateType.Trigger:
                        CycleDoor(_door);

                        break;
                    case UpdateType.Update10:
                        CheckDoor(_door);

                        break;
                }
            }
        }

        void PrintToScreen(String text, bool append = true)
        {
            Me.GetSurface(0).WriteText(text + '\n', append);
        }

        ITerminalAction RunAction()
        {
            return Me.GetActionWithName(RUN_ACTION_NAME);
        }

        void CycleDoor(IMyDoor door)
        {
            switch (door.Status)
            {
                case DoorStatus.Closed:
                    door.Enabled = true;
                    door.OpenDoor();
                    break;
                case DoorStatus.Open:
                    door.Enabled = true;
                    door.CloseDoor();
                    break;
            }
        }

        void CheckDoor(IMyDoor door)
        {
            if (door.Enabled && (DoorStatus.Closed == door.Status || DoorStatus.Open == door.Status))
            {
                door.Enabled = false;
            }
        }

        public class AirLock
        {
            IMyBlockGroup _group;
            List<IMyDoor> _innerDoors;
            List<IMyDoor> _outerDoors;
            List<IMyButtonPanel> _buttons;

            public AirLock(Program parent, IMyBlockGroup group)
            {
                _group = group;

                _innerDoors = new List<IMyDoor>();
                _outerDoors = new List<IMyDoor>();
                _buttons = new List<IMyButtonPanel>();

                // parent.PrintToScreen(Name, false);
                // parent.PrintToScreen("");
                // parent.PrintToScreen($"Airlock Tag: {parent._airlockGroupTag}");
                // parent.PrintToScreen($"Inner Tag: {parent._airlockInnerTag}");
                // parent.PrintToScreen($"Outer Tag: {parent._airlockOuterTag}");
                // parent.PrintToScreen("");

                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();

                group.GetBlocks(blocks);

                foreach (IMyTerminalBlock block in blocks)
                {
                    // parent.PrintToScreen($"{block.CustomName}");

                    IMyDoor door = block as IMyDoor;
                    IMyButtonPanel button = block as IMyButtonPanel;

                    if (door != null)
                        if (door.CustomName.Contains(parent._airlockInnerTag))
                            _innerDoors.Add(door);
                        else if (door.CustomName.Contains(parent._airlockOuterTag))
                            _outerDoors.Add(door);

                    if (button != null)
                        _buttons.Add(button);
                }
            }

            public String Name
            {
                get { return _group.Name; }
            }

            public override string ToString()
            {
                return $"{_group.Name} - Inner: {_innerDoors.Count}, Outer: {_outerDoors.Count}, Buttons: {_buttons.Count}";
            }
        }
    }
}