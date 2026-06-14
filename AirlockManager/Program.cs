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

        MyIni _ini = new MyIni();

        string _airlockGroupTag = "[Airlock]";
        string _airlockInnerTag = "[Inner]";
        string _airlockOuterTag = "[Outer]";

        Dictionary<String, AirLock> _airlocks;

        public Program()
        {
            PrintToScreen("", false);

            LoadIni();

            _airlocks = new Dictionary<String, AirLock>();
            List<IMyBlockGroup> airlockGroups = new List<IMyBlockGroup>();

            GridTerminalSystem.GetBlockGroups(airlockGroups, group => group.Name.Contains(_airlockGroupTag));

            foreach (IMyBlockGroup group in airlockGroups)
            {
                AirLock airlock = new AirLock(this, group);
                _airlocks.Add(airlock.Name, airlock);

                Echo($"Found airlock: {airlock.Name}: {airlock}");
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
        }

        public void Main(string argument, UpdateType updateSource)
        {
            switch (updateSource)
            {
                case UpdateType.Trigger:
                    //PrintToScreen($"Called: {argument}");

                    AirLock airlock = _airlocks[argument];

                    if (airlock != null)
                        airlock.StartCycle();
                    else
                        PrintToScreen($"Error: Could not find airlock for '{argument}'");

                    break;
                case UpdateType.Update10:
                    foreach (AirLock updateAirlock in _airlocks.Values)
                        updateAirlock.UpdateState();

                    break;
            }
        }

        void PrintToScreen(String text, bool append = true)
        {
            Me.GetSurface(0).WriteText(text + '\n', append);
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

        public class AirLock
        {
            IMyBlockGroup _group;
            List<IMyDoor> _doors;
            List<IMyDoor> _innerDoors;
            List<IMyDoor> _outerDoors;

            AirlockStatus _status = AirlockStatus.In;

            public AirLock(Program parent, IMyBlockGroup group)
            {
                _group = group;

                _doors = new List<IMyDoor>();
                _innerDoors = new List<IMyDoor>();
                _outerDoors = new List<IMyDoor>();

                List<IMyDoor> doors = new List<IMyDoor>();

                group.GetBlocksOfType<IMyDoor>(doors);

                foreach (IMyDoor door in doors)
                {
                    _doors.Add(door);

                    if (door.CustomName.Contains(parent._airlockInnerTag))
                        _innerDoors.Add(door);
                    else if (door.CustomName.Contains(parent._airlockOuterTag))
                        _outerDoors.Add(door);
                }

                // parent.PrintToScreen("Airlock", false);

                // foreach (IMyDoor door in _doors)
                //     parent.PrintToScreen($"  Door {door.CustomName}");
                // foreach (IMyDoor door in _innerDoors)
                //     parent.PrintToScreen($"  Inner Door {door.CustomName}");
                // foreach (IMyDoor door in _outerDoors)
                //     parent.PrintToScreen($"  Outer Door {door.CustomName}");
            }

            public String Name
            {
                get { return _group.Name; }
            }

            /* If the airlock is in either end-state, then swtich to the matching 'closing' state and close all door. */
            public void StartCycle()
            {
                switch (_status)
                {
                    case AirlockStatus.In:
                        _status = AirlockStatus.InClosing;

                        foreach (IMyDoor door in _doors)
                            if (DoorStatus.Open == door.Status)
                            {
                                door.Enabled = true;
                                door.CloseDoor();
                            }

                        break;
                    case AirlockStatus.Out:
                        _status = AirlockStatus.OutClosing;

                        foreach (IMyDoor door in _doors)
                            if (DoorStatus.Open == door.Status)
                            {
                                door.Enabled = true;
                                door.CloseDoor();
                            }

                        break;
                }
            }

            public void UpdateState()
            {
                /* Any doors that have finished moving should be powered down. */
                foreach (IMyDoor door in _doors)
                    switch (door.Status)
                    {
                        case DoorStatus.Closed:
                        case DoorStatus.Open:
                            door.Enabled = false;

                            break;
                    }

                /* If already in an end state, nothing else needs to be done and we can exit. */
                /* If in a closing state, if all doors are closed move to the matching sealed state, otherwise exit. */
                /* If in a sealed state, move the status to the opposed opening state. */
                /* If in an opening state, start opening any direction door; if all door are open move to the matching end state, otherwise exit */
                switch (_status)
                {
                    case AirlockStatus.In:
                    case AirlockStatus.Out:
                        return;
                    case AirlockStatus.InClosing:
                        if (CheckDoorStatus(DoorStatus.Closed, _doors))
                            _status = AirlockStatus.InSealed;

                        return;
                    case AirlockStatus.OutClosing:
                        if (CheckDoorStatus(DoorStatus.Closed, _doors))
                            _status = AirlockStatus.OutSealed;

                        return;
                    case AirlockStatus.InSealed:
                        _status = AirlockStatus.OutOpening;

                        return;
                    case AirlockStatus.OutSealed:
                        _status = AirlockStatus.InOpening;

                        return;
                    case AirlockStatus.InOpening:
                        foreach (IMyDoor door in _innerDoors)
                        {
                            if (DoorStatus.Closed == door.Status)
                            {
                                door.Enabled = true;
                                door.OpenDoor();
                            }
                        }

                        if (CheckDoorStatus(DoorStatus.Open, _innerDoors))
                            _status = AirlockStatus.In;

                        return;
                    case AirlockStatus.OutOpening:
                        foreach (IMyDoor door in _outerDoors)
                        {
                            if (DoorStatus.Closed == door.Status)
                            {
                                door.Enabled = true;
                                door.OpenDoor();
                            }
                        }

                        if (CheckDoorStatus(DoorStatus.Open, _outerDoors))
                            _status = AirlockStatus.Out;

                        return;
                }
            }

            private Boolean CheckDoorStatus(DoorStatus status, List<IMyDoor> doors)
            {
                bool isStatus = true;

                foreach (IMyDoor door in doors)
                    if (door.Status != status)
                        isStatus = false;

                return isStatus;
            }

            public override string ToString()
            {
                return $"{_group.Name} - Inner: {_innerDoors.Count}, Outer: {_outerDoors.Count}";
            }

            private enum AirlockStatus
            {
                /* Inner open, outer closed. */
                In,
                /* Inner closing, outer closed. */
                InClosing,
                /* Inner closed, outer closed, moving to out. */
                InSealed,
                /* Inner closed, outer opening. */
                OutOpening,
                /* Inner closed, outer open. */
                Out,
                /* Inner closed, outer closing. */
                OutClosing,
                /* Inner closed, outer closed, moving to in. */
                OutSealed,
                /* Inner opening, outer closed. */
                InOpening
            }
        }
    }
}