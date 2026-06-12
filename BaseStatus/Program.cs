using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        const string INI_SECTION_GENERAL = "Base Manager - General Settings";

        const string VENT_GROUP_TAG = "Base Internal Vents";
        const string VENT_STATUS_GROUP_TAG = "Vent Status Lights";

        const string PRODUCER_GROUP_TAG = "Base Producers";
        const string PRODUCER_STATUS_GROUP_TAG = "Producer Status Lights";

        MyIni _ini = new MyIni();

        string _ventGroupTag = "Internal Vents";
        string _ventStatusGroupTag = "Internal Vent Lights";

        string _producerGroupTag = "Base Producers";
        string _producerStatusGroupTag = "Base Producer Lights";

        List<IMyAirVent> _vents;
        List<IMyLightingBlock> _ventLights;
        List<IMyProductionBlock> _producers;
        List<IMyFunctionalBlock> _producerStausBlocks;

        Color _ventStatusLightColorDepressurised = Color.Red;
        Color _ventStatusLightColorPressurised = Color.White;

        public Program()
        {
            LoadIni();
            LoadObjects();

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        void LoadIni()
        {
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
                Echo($"CustomData error:\nLine {result}");

            _ventGroupTag = _ini.Get(INI_SECTION_GENERAL, VENT_GROUP_TAG).ToString(_ventGroupTag);
            _ini.Set(INI_SECTION_GENERAL, VENT_GROUP_TAG, _ventGroupTag);

            _ventStatusGroupTag = _ini.Get(INI_SECTION_GENERAL, VENT_STATUS_GROUP_TAG).ToString(_ventStatusGroupTag);
            _ini.Set(INI_SECTION_GENERAL, VENT_STATUS_GROUP_TAG, _ventStatusGroupTag);

            _producerGroupTag = _ini.Get(INI_SECTION_GENERAL, PRODUCER_GROUP_TAG).ToString(_producerGroupTag);
            _ini.Set(INI_SECTION_GENERAL, PRODUCER_GROUP_TAG, _producerGroupTag);

            _producerStatusGroupTag = _ini.Get(INI_SECTION_GENERAL, PRODUCER_STATUS_GROUP_TAG).ToString(_producerStatusGroupTag);
            _ini.Set(INI_SECTION_GENERAL, PRODUCER_STATUS_GROUP_TAG, _producerStatusGroupTag);

            string iniOutput = _ini.ToString();

            if (iniOutput != Me.CustomData)
            {
                Me.CustomData = iniOutput;
            }
        }

        void LoadObjects()
        {
            List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
            GridTerminalSystem.GetBlockGroups(groups);

            _vents = new List<IMyAirVent>();
            _ventLights = new List<IMyLightingBlock>();
            _producers = new List<IMyProductionBlock>();
            _producerStausBlocks = new List<IMyFunctionalBlock>();

            foreach (IMyBlockGroup group in groups)
            {
                if (group.Name.Contains(_ventGroupTag))
                    group.GetBlocksOfType<IMyAirVent>(_vents);
                else if (group.Name.Contains(_ventStatusGroupTag))
                    group.GetBlocksOfType<IMyLightingBlock>(_ventLights);
                else if (group.Name.Contains(_producerGroupTag))
                    group.GetBlocksOfType<IMyProductionBlock>(_producers);
                else if (group.Name.Contains(_producerStatusGroupTag))
                    group.GetBlocksOfType<IMyFunctionalBlock>(_producerStausBlocks);
            }

            Echo($"Found {_vents.Count} vents, {_ventLights.Count} vent lights, {_producers.Count} producers, and {_producerStausBlocks.Count} producer status blocks.");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            CheckVents();
            CheckProducers();
        }

        private void CheckVents()
        {
            bool pressurised = true;

            foreach (IMyAirVent vent in _vents)
                if (!vent.CanPressurize)
                    pressurised = false;

            Color lightColor = (pressurised) ? _ventStatusLightColorPressurised : _ventStatusLightColorDepressurised;

            foreach (IMyLightingBlock light in _ventLights)
                light.Color = lightColor;
        }

        private void CheckProducers()
        {
            bool producing = false;

            foreach (IMyProductionBlock producer in _producers)
                if (producer.IsProducing)
                    producing = true;

            foreach (IMyFunctionalBlock block in _producerStausBlocks)
                block.Enabled = producing;
        }
    }
}
