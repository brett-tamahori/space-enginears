using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.Entities.Blocks;
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

        const string VENT_LIGHT_COLOR_NORMAL = "Vent Status Light Color - Normal";
        const string VENT_LIGHT_COLOR_WARNING = "Vent Status Light Color - Warning";

        const string PRODUCER_GROUP_TAG = "Base Producers";
        const string PRODUCER_STATUS_GROUP_TAG = "Producer Status Lights";

        const string VENT_LIGHT_COLOR_NORMAL_DEFAULT = "#ffffff";
        const string VENT_LIGHT_COLOR_WARNING_DEFAULT = "#ff0000";

        const string LIGHT_DETECTOR_NAME = "Light Detector Name";
        const string LIGHT_DETECTOR_PERCENT = "Light Detector Percent";
        const string LIGHT_DETECTOR_CONTROLLED_GROUP_TAG = "Light Detector Controlled Group";

        const decimal LARGE_GRID_SOLAR_PANEL_MW = 0.16m;

        MyIni _ini = new MyIni();

        string _ventGroupTag = "Internal Vents";
        string _ventStatusGroupTag = "Internal Vent Lights";

        string _producerGroupTag = "Base Producers";
        string _producerStatusGroupTag = "Base Producer Lights";

        List<IMyAirVent> _vents;
        List<IMyLightingBlock> _ventLights;
        List<IMyProductionBlock> _producers;
        List<IMyFunctionalBlock> _producerStausBlocks;

        Color _ventLightColorNormal = Color.White;
        Color _ventLightColorWarning = Color.Red;

        string _lightDetectorName = "Light Detector";
        decimal _lightDetectorPercent = 0.5m;
        IMySolarPanel _lightDetector;
        float _lightRequiredPower = 1;
        string _lightDetectorControlledGroupTag = "Light Detector Blocks";
        List<IMyFunctionalBlock> _lightDetectorControlled;

        List<string> _statusList;

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

            string ventLightColorNormalString = _ini.Get(INI_SECTION_GENERAL, VENT_LIGHT_COLOR_NORMAL).ToString(VENT_LIGHT_COLOR_NORMAL_DEFAULT);
            Color? testVentLightColorNormal = ColorExtensions.FromHtml(ventLightColorNormalString);

            if (testVentLightColorNormal.HasValue)
            {
                _ventLightColorNormal = testVentLightColorNormal.Value;
                _ini.Set(INI_SECTION_GENERAL, VENT_LIGHT_COLOR_NORMAL, ventLightColorNormalString);
            }
            else
                _ini.Set(INI_SECTION_GENERAL, VENT_LIGHT_COLOR_NORMAL, VENT_LIGHT_COLOR_NORMAL_DEFAULT);

            string ventLightColorWarningString = _ini.Get(INI_SECTION_GENERAL, VENT_LIGHT_COLOR_WARNING).ToString(VENT_LIGHT_COLOR_WARNING_DEFAULT);
            Color? testVentLightWarning = ColorExtensions.FromHtml(ventLightColorWarningString);

            if (testVentLightWarning.HasValue)
            {
                _ventLightColorWarning = testVentLightWarning.Value;
                _ini.Set(INI_SECTION_GENERAL, VENT_LIGHT_COLOR_WARNING, ventLightColorWarningString);
            }
            else
                _ini.Set(INI_SECTION_GENERAL, VENT_LIGHT_COLOR_WARNING, VENT_LIGHT_COLOR_WARNING_DEFAULT);

            _producerGroupTag = _ini.Get(INI_SECTION_GENERAL, PRODUCER_GROUP_TAG).ToString(_producerGroupTag);
            _ini.Set(INI_SECTION_GENERAL, PRODUCER_GROUP_TAG, _producerGroupTag);

            _producerStatusGroupTag = _ini.Get(INI_SECTION_GENERAL, PRODUCER_STATUS_GROUP_TAG).ToString(_producerStatusGroupTag);
            _ini.Set(INI_SECTION_GENERAL, PRODUCER_STATUS_GROUP_TAG, _producerStatusGroupTag);

            _lightDetectorName = _ini.Get(INI_SECTION_GENERAL, LIGHT_DETECTOR_NAME).ToString(_lightDetectorName);
            _ini.Set(INI_SECTION_GENERAL, LIGHT_DETECTOR_NAME, _lightDetectorName);

            _lightDetectorPercent = _ini.Get(INI_SECTION_GENERAL, LIGHT_DETECTOR_PERCENT).ToDecimal(_lightDetectorPercent);
            _ini.Set(INI_SECTION_GENERAL, LIGHT_DETECTOR_PERCENT, _lightDetectorPercent);

            _lightRequiredPower = (float)(LARGE_GRID_SOLAR_PANEL_MW * _lightDetectorPercent);

            _lightDetectorControlledGroupTag = _ini.Get(INI_SECTION_GENERAL, LIGHT_DETECTOR_CONTROLLED_GROUP_TAG).ToString(_lightDetectorControlledGroupTag);
            _ini.Set(INI_SECTION_GENERAL, LIGHT_DETECTOR_CONTROLLED_GROUP_TAG, _lightDetectorControlledGroupTag);

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
            _lightDetectorControlled = new List<IMyFunctionalBlock>();

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
                else if (group.Name.Contains(_lightDetectorControlledGroupTag))
                    group.GetBlocksOfType<IMyFunctionalBlock>(_lightDetectorControlled);
            }

            _lightDetector = GridTerminalSystem.GetBlockWithName(_lightDetectorName) as IMySolarPanel;

            Echo($"Found {_vents.Count} vents, {_ventLights.Count} vent lights, {_producers.Count} producers, " +
            $", {_producerStausBlocks.Count} producer status blocks, and {_lightDetectorControlled.Count} light detector blocks.");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            _statusList = new List<string>();

            CheckVents();
            CheckProducers();
            CheckLightLevel();

            Me.GetSurface(0).WriteText("Base Status\n");

            foreach (string entry in _statusList)
            {
                Me.GetSurface(0).WriteText($"\n{entry}", true);
            }
        }

        private void CheckVents()
        {
            bool pressurised = true;

            foreach (IMyAirVent vent in _vents)
                if (!vent.CanPressurize)
                    pressurised = false;

            Color lightColor = pressurised ? _ventLightColorNormal : _ventLightColorWarning;

            foreach (IMyLightingBlock light in _ventLights)
                light.Color = lightColor;

            _statusList.Add(pressurised ? "Base Pressurised" : "Base Depressurised");
        }

        private void CheckProducers()
        {
            bool producing = false;

            foreach (IMyProductionBlock producer in _producers)
                if (producer.IsProducing)
                    producing = true;

            foreach (IMyFunctionalBlock block in _producerStausBlocks)
                block.Enabled = producing;

            _statusList.Add(producing ? "Producer(s) Running" : "Producer(s) Standby");
        }

        private void CheckLightLevel()
        {
            if (_lightDetector != null)
            {
                bool light = _lightDetector.MaxOutput > _lightRequiredPower;

                foreach (IMyFunctionalBlock block in _lightDetectorControlled)
                    block.Enabled = !light;

                string status = light ? "It's light outside." : "It's dark outside.";

                _statusList.Add($"{status} ( {_lightDetector.MaxOutput:N2} / {_lightRequiredPower:N2} )");
            }
            else
                _statusList.Add($"No light detector found under '{_lightDetectorName}'");
        }
    }
}