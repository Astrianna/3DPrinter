using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;
using Sandbox.Game;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using Sandbox.Game.WorldEnvironment.Modules;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.AI.Navigation;
using System.Xml.Serialization;
using Sandbox.Game.World.Generator;

namespace IngameScript
{

    partial class Program : MyGridProgram
    {
        // Astrianna - 2020
        //
        // Special thanks to Malware, for the MDK, and for the assistance on Discord.
        /////////////////////////////////////// 
        //
        // Available arguments: (not case sensitive) 
        //
        // stop         - use to immediatly pause all activity
        // start        - use to resume all activity
        // mode         - toggles mode between grinding and welding
        // auto         - toggles automatic mode
        // goto x,y,z   - stops auto mode and moves to specified coordinates. e.g. goto 3,10,20 (y must be an even number)
        // home         - stops auto mode and moves to X minimum, Y minimum, Z maximum
        // return       - toggles automaticly return to home position after completion
        // refresh      - refreshes all block names, and resets CustomData to last known valid configuration
        //
        // Use CustomData to change work area, tool size, and speed settings.
        // 
        ///////////////////////////////////////

        MyIni ini = new MyIni();
        MyIni customData = new MyIni();
        MyIniParseResult result;

        // declare variables before config
        double xTar, yTar, zTar, xPos, yPos, zPos, xPause, yPause, zPause, xPosMerge, getXPosMerge, getYPos, getZPos;
        int maxX, maxY, maxZ, minX, minY, minZ, toolLength;
        bool returnAfterDone, autoMode, firstRun, manualMove, moveReady;
        float maxMovementSpeed, maxToolSpeed, grindingSpeed, weldingSpeed;
        StringBuilder output = new StringBuilder("");
        enum EMode { grinding, welding }
        enum Dir { forward, backward, left, right, up, down, movingForward, movingBackward, movingLeft, movingRight, movingUp, movingdown }
        EMode mode;
        Dir xDir, yDir, zDir;

        // block declarations
        IMySensorBlock _Sensor;
        IMyShipConnector _ConnectorX, _ConnectorY, _ConnectorZ1, _ConnectorZ2, _MoveConX, _MoveConY, _MoveConZ1, _MoveConZ2;
        IMyPistonBase _PistonX, _PistonY, _PistonZ;
        IMyShipMergeBlock _MergeX, _MergeY, _MergeZ;
        List<IMyShipWelder> _Welders = new List<IMyShipWelder>();
        List<IMyShipGrinder> _Grinders = new List<IMyShipGrinder>();
        List<IMyTextPanel> _Lcds = new List<IMyTextPanel>();
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            ini.TryParse(Storage);
            // xyz starting position
            xTar = ini.Get("3DPrinter", "xtar").ToDouble(0);
            yTar = ini.Get("3DPrinter", "ytar").ToDouble(0); // even numbers only
            zTar = ini.Get("3DPrinter", "ztar").ToDouble(20);
            xPos = ini.Get("3DPrinter", "xpos").ToDouble(0);
            yPos = ini.Get("3DPrinter", "ypos").ToDouble(0);
            zPos = ini.Get("3DPrinter", "zpos").ToDouble(20);
            xPosMerge = ini.Get("3DPrinter", "xposmerge").ToDouble(0);
            if (!Enum.TryParse<Dir>(ini.Get("3DPrinter", "xdir").ToString("forward"), out xDir)) {; }
            if (!Enum.TryParse<Dir>(ini.Get("3DPrinter", "ydir").ToString("forward"), out yDir)) {; }
            if (!Enum.TryParse<Dir>(ini.Get("3DPrinter", "zdir").ToString("forward"), out zDir)) {; }
            // work area
            maxX = ini.Get("3DPrinter", "maxX").ToInt32(20);
            maxY = ini.Get("3DPrinter", "maxY").ToInt32(20);
            maxZ = ini.Get("3DPrinter", "maxZ").ToInt32(20);
            minX = ini.Get("3DPrinter", "minX").ToInt32(0);
            minY = ini.Get("3DPrinter", "minY").ToInt32(0);
            minZ = ini.Get("3DPrinter", "minZ").ToInt32(0);
            // piston and tool settings
            returnAfterDone = ini.Get("3DPrinter", "returnAfterDone").ToBoolean(true); // returns to maxZ, minY, minX when job is completed
            maxMovementSpeed = ini.Get("3DPrinter", "maxMovementSpeed").ToSingle(5.0f); // max speed while changing positions
            grindingSpeed = ini.Get("3DPrinter", "grindingSpeed").ToSingle(0.5f); // max speed of tool while in grinding mode
            weldingSpeed = ini.Get("3DPrinter", "weldingSpeed").ToSingle(0.2f); // max speed of tool while in welding mode
            toolLength = ini.Get("3DPrinter", "toolLength").ToInt32(10); // length of tool in blocks
            if (!Enum.TryParse<EMode>(ini.Get("3DPrinter", "mode").ToString("forward"), out mode)) {; }
            autoMode = ini.Get("3DPrinter", "autoMode").ToBoolean(false);
            firstRun = ini.Get("3DPrinter", "firstrun").ToBoolean(true);
            manualMove = ini.Get("3DPrinter", "manualMove").ToBoolean(false);
            moveReady = ini.Get("3DPrinter", "moveReady").ToBoolean(true);

            GetBlocks();
        }
        public void GetBlocks() // you didn't change any names, did you?
        {
            _Sensor = GridTerminalSystem.GetBlockWithName("Tool Sensor") as IMySensorBlock;
            _ConnectorX = GridTerminalSystem.GetBlockWithName("X Connector") as IMyShipConnector;
            _ConnectorY = GridTerminalSystem.GetBlockWithName("Y Connector") as IMyShipConnector;
            _ConnectorZ1 = GridTerminalSystem.GetBlockWithName("Z Connector 1") as IMyShipConnector;
            _ConnectorZ2 = GridTerminalSystem.GetBlockWithName("Z Connector 2") as IMyShipConnector;
            _MoveConX = GridTerminalSystem.GetBlockWithName("X Move") as IMyShipConnector;
            _MoveConY = GridTerminalSystem.GetBlockWithName("Y Move") as IMyShipConnector;
            _MoveConZ1 = GridTerminalSystem.GetBlockWithName("Z Move 1") as IMyShipConnector;
            _MoveConZ2 = GridTerminalSystem.GetBlockWithName("Z Move 2") as IMyShipConnector;
            _PistonX = GridTerminalSystem.GetBlockWithName("X Piston") as IMyPistonBase;
            _PistonY = GridTerminalSystem.GetBlockWithName("Y Piston") as IMyPistonBase;
            _PistonZ = GridTerminalSystem.GetBlockWithName("Z Piston") as IMyPistonBase;
            _MergeX = GridTerminalSystem.GetBlockWithName("X Merge") as IMyShipMergeBlock;
            _MergeY = GridTerminalSystem.GetBlockWithName("Y Merge") as IMyShipMergeBlock;
            _MergeZ = GridTerminalSystem.GetBlockWithName("Z Merge") as IMyShipMergeBlock;
            GridTerminalSystem.GetBlockGroupWithName("3D-Printer").GetBlocksOfType(_Welders);
            GridTerminalSystem.GetBlockGroupWithName("3D-Printer").GetBlocksOfType(_Grinders);
            GridTerminalSystem.GetBlockGroupWithName("3D-Printer").GetBlocksOfType(_Lcds);
            if (_Sensor == null) output.Append("Tool Sensor not found!\n");
            if (_ConnectorX == null) output.Append("X Connector not found!\n");
            if (_ConnectorY == null) output.Append("Y Connector not found!\n");
            if (_ConnectorZ1 == null) output.Append("Z Connector 1 not found!\n");
            if (_ConnectorZ2 == null) output.Append("Z Connector 2 not found!\n");
            if (_MoveConX == null) output.Append("X Move not found!\n");
            if (_MoveConY == null) output.Append("Y Move not found!\n");
            if (_MoveConZ1 == null) output.Append("Z Move 1 not found!\n");
            if (_MoveConZ2 == null) output.Append("Z Move 2 not found!\n");
            if (_PistonX == null) output.Append("X Piston not found!\n");
            if (_PistonY == null) output.Append("Y Piston not found!\n");
            if (_PistonZ == null) output.Append("Z Piston not found!\n");
            if (_MergeX == null) output.Append("X Merge not found!\n");
            if (_MergeY == null) output.Append("Y Merge not found!\n");
            if (_MergeZ == null) output.Append("Z Merge not found!\n");
            if (_Welders.Count == 0 && _Grinders.Count == 0) output.Append("No Welders or Grinders found in Group '3D-Printer'\n");
            if (_Lcds.Count == 0) output.Append("No LCD Panels found in Group '3D-Printer'\n");
            if (_Welders.Count != 0 && _Grinders.Count == 0) mode = EMode.welding;
            if (_Welders.Count == 0 && _Grinders.Count != 0) mode = EMode.grinding;
        }

        public void Main(string argument)
        {
            if (firstRun)
            {
                firstRun = false;
                _Sensor.Enabled = false;
                _PistonX.Enabled = true;
                _PistonY.Enabled = true;
                _PistonZ.Enabled = true;
                manualMove = true;
                SaveToCustomData();
                SensorSetup();
            }

            output.Clear();

            if (argument.Equals("start", StringComparison.OrdinalIgnoreCase)) Start();
            if (argument.Equals("stop", StringComparison.OrdinalIgnoreCase)) Stop();
            if (argument.Equals("mode", StringComparison.OrdinalIgnoreCase)) ChangeMode();
            if (argument.Equals("return", StringComparison.OrdinalIgnoreCase)) Return();
            if (argument.Equals("home", StringComparison.OrdinalIgnoreCase)) Home();
            if (argument.Equals("auto", StringComparison.OrdinalIgnoreCase)) Auto();
            if (argument.StartsWith("goto", StringComparison.OrdinalIgnoreCase)) GoTo(argument);
            if (argument.Equals("refresh", StringComparison.OrdinalIgnoreCase)) Refresh();
            if (argument.Equals("object_detected", StringComparison.OrdinalIgnoreCase)) SensorTrigger(true);
            if (argument.Equals("object_not_detected", StringComparison.OrdinalIgnoreCase)) SensorTrigger(false);

            ParseCustomData();
            if (Me.CustomData == "") SaveToCustomData();

            if (mode == EMode.grinding) maxToolSpeed = grindingSpeed;
            if (mode == EMode.welding) maxToolSpeed = weldingSpeed;

            if (manualMove)
            {
                if (xPos == xTar && yPos == yTar && zPos == zTar)
                {
                    xDir = Dir.forward;
                    yDir = Dir.right;
                    if (mode == EMode.grinding) zDir = Dir.down;
                    if (mode == EMode.welding) zDir = Dir.up;
                    manualMove = false;
                }
            }

            // raw pos based on merge block names
            getXPosMerge = GetPos(_MergeX);
            getYPos = GetPos(_MergeY);
            getZPos = GetPos(_MergeZ);
            if (getXPosMerge != -1)
            {
                xPosMerge = getXPosMerge;
                xPos = Math.Round(xPosMerge + (_PistonX.CurrentPosition - _PistonX.MinLimit) / 2.5, 1); // X Merge + X Piston
            }
            if (getYPos != -1) yPos = getYPos;
            if (getZPos != -1) zPos = getZPos;

            if ((int)xDir >= 6 || (int)yDir >= 6 || (int)zDir >= 6) // something is currently moving, check to see if its ready to stop 
            {
                if (zDir == Dir.movingdown && _PistonZ.CurrentPosition == _PistonZ.MaxLimit || zDir == Dir.movingUp && _PistonZ.CurrentPosition == _PistonZ.MinLimit) MoveZ();
                else if (yDir == Dir.movingRight && _PistonY.CurrentPosition == _PistonY.MinLimit || yDir == Dir.movingLeft && _PistonY.CurrentPosition == _PistonY.MaxLimit) MoveY();
                else if (xDir == Dir.movingForward && _PistonX.CurrentPosition == _PistonX.MinLimit || xDir == Dir.movingBackward && _PistonX.CurrentPosition == _PistonX.MaxLimit) MoveX();
            }
            else if ((int)xDir <= 5 && (int)yDir <= 5 && (int)zDir <= 5) // nothing is currently moving
            {
                if (autoMode && yPos == yTar && zPos == zTar && ((xDir == Dir.forward && xPos >= maxX) || (xDir == Dir.backward && xPos <= minX))) // automode only
                {
                    _PistonX.Enabled = false; // disable X Piston and Sensor while anything else is moving
                    _Sensor.Enabled = false;
                    if ((yPos == minY && yDir == Dir.left) || (yPos + toolLength == maxY && yDir == Dir.right)) // move z up/down
                    {
                        if ((zPos == maxZ && mode == EMode.welding) || (zPos == minZ) && mode == EMode.grinding) Done(); // done! returning to starting position
                        else if (mode == EMode.welding) zTar = zTar + 1;
                        else if (mode == EMode.grinding) zTar = zTar - 1;
                    }
                    else if ((yPos > minY) || (yPos + toolLength < maxY)) // move y left/right
                    {
                        if (yDir == Dir.right) yTar = yTar + toolLength;
                        else if (yDir == Dir.left) yTar = yTar - toolLength;
                    }
                }
                else if (autoMode && xDir == Dir.forward && _PistonX.CurrentPosition == _PistonX.MaxLimit && xPos < maxX) // automode only
                {
                    if (moveReady == true)
                    {
                        xTar = xTar + 3;
                        moveReady = false;
                    }
                    MoveX();
                }
                else if (autoMode && xDir == Dir.backward && _PistonX.CurrentPosition == _PistonX.MinLimit && xPosMerge > minX) //automode only
                {
                    if (moveReady == true)
                    {
                        xTar = xTar - 3;
                        moveReady = false;
                    }
                    MoveX();
                }

                // does something need to move?
                if (zPos != zTar) MoveZ();
                else if (yPos != yTar) MoveY();
                else if (!autoMode) // move X while automode is off
                {
                    if (xPos > xTar && xPosMerge == minX)
                    {
                        _PistonX.Velocity = -1 * maxMovementSpeed;
                    }
                    else if (xPos < xTar && xPos < maxX)
                    {
                        _PistonX.Velocity = maxMovementSpeed;
                    }
                    if (xPos < xTar ^ xTar < xPosMerge)
                    {
                        MoveX();
                    }
                }
                    // build standard output for LCDs/Terminal
                    output.Append("Mode: ").Append(mode).Append("\n");
                output.Append("Auto Mode: ").Append(autoMode.ToString()).Append("\n");
                output.Append("X: ").Append(xPos).Append("\n");
                output.Append("Y: ").Append(yPos).Append("\n");
                output.Append("Z: ").Append(zPos).Append("\n");
                output.Append("X Target: ").Append(xTar).Append("\n");
                output.Append("Y Target: ").Append(yTar).Append("\n");
                output.Append("Z Target: ").Append(zTar).Append("\n");
                output.Append("X Direction: ").Append(xDir).Append("\n");
                output.Append("Y Direction: ").Append(yDir).Append("\n");
                output.Append("Z Direction: ").Append(zDir).Append("\n");

                Echo(output.ToString());
                if (_Lcds.Count != 0)
                {
                    for (int i = 0; i < _Lcds.Count; i++)
                    {
                        _Lcds[i].ContentType = ContentType.TEXT_AND_IMAGE;
                        _Lcds[i].WriteText(output.ToString(), false);
                    }
                }

            }
        }
        public void Start()
        {
            if (autoMode) _Sensor.Enabled = true;
            _PistonX.Enabled = true;
            _PistonY.Enabled = true;
            _PistonZ.Enabled = true;
            if (mode == EMode.welding)
            {
                for (int i = 0; i < _Welders.Count; i++) _Welders[i].Enabled = true;
            }
            else if (mode == EMode.grinding)
            {
                for (int i = 0; i < _Grinders.Count; i++) _Grinders[i].Enabled = true;
            }

            if (xPause != xPos || yPause != yPos || zPause != zPos)
            {
                output.Append("Position changed while paused\nStarting forward\n");
                _PistonX.Velocity = maxToolSpeed;
            }
            SensorSetup();
        }
        public void Stop()
        {
            _Sensor.Enabled = false;
            _PistonX.Enabled = false;
            _PistonY.Enabled = false;
            _PistonZ.Enabled = false;
            xPause = xPos;
            yPause = yPos;
            zPause = zPos;
            for (int i = 0; i < _Welders.Count; i++) _Welders[i].Enabled = false;
            for (int i = 0; i < _Grinders.Count; i++) _Grinders[i].Enabled = false;
        }
        public void ChangeMode()
        {

            if (mode == EMode.grinding)
            {
                mode = EMode.welding;
                zDir = Dir.up;
            }

            else if (mode == EMode.welding)
            {
                mode = EMode.grinding;
                zDir = Dir.down;
            }
            SaveToCustomData();
        }
        public void Home()
        {
            if (mode == EMode.grinding) zTar = maxZ;
            if (mode == EMode.welding) zTar = minZ;
            yTar = minY;
            xTar = minX;
            autoMode = false;
            manualMove = true;
            for (int i = 0; i < _Welders.Count; i++) _Welders[i].Enabled = false;
            for (int i = 0; i < _Grinders.Count; i++) _Grinders[i].Enabled = false;
            SensorSetup();
        }
        public void Return()
        {
            returnAfterDone = !returnAfterDone;
            SaveToCustomData();
        }
        public void Auto()
        {
            autoMode = !autoMode;
            if (autoMode)
            {
                _PistonX.Velocity = maxToolSpeed;
                if (mode == EMode.welding)
                {
                    for (int i = 0; i < _Welders.Count; i++) _Welders[i].Enabled = true;
                }
                else if (mode == EMode.grinding)
                {
                    for (int i = 0; i < _Grinders.Count; i++) _Grinders[i].Enabled = true;
                }
            }
            else
            {
                for (int i = 0; i < _Welders.Count; i++) _Welders[i].Enabled = false;
                for (int i = 0; i < _Grinders.Count; i++) _Grinders[i].Enabled = false;
            }
            manualMove = false;
            SensorSetup();
        }
        public void GoTo(string argument)
        {
            for (int i = 0; i < _Welders.Count; i++) _Welders[i].Enabled = false;
            for (int i = 0; i < _Grinders.Count; i++) _Grinders[i].Enabled = false;

            string[] xyz = argument.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (xyz.Length == 4)
            {
                int x, y, z;
                if (int.TryParse(xyz[1], out x) && int.TryParse(xyz[2], out y) && int.TryParse(xyz[3], out z))
                {
                    if (y % 2 == 0 && x >= minX && x <= maxX && y >= minY && y <= maxY && z >= minZ && z <= maxZ)
                    {
                        autoMode = false;
                        xTar = x;
                        yTar = y;
                        zTar = z;
                        manualMove = true;
                        SensorSetup();
                    }
                }
            }
        }
        public void Refresh()
        {
            GetBlocks();
            SaveToCustomData();
        }
        public void Done()
        {

            if (zPos == zTar && yPos == yTar && xPosMerge == zTar)
            {
                if (returnAfterDone)
                {
                    zTar = maxZ;
                    yTar = minY;
                    xTar = minX;
                    SensorSetup();
                }

                else if (!returnAfterDone || (zTar == maxZ && yTar == minY && xTar == minX))

                {
                    _PistonX.Enabled = false;
                    _PistonY.Enabled = false;
                    _PistonZ.Enabled = false;
                    _Sensor.Enabled = false;
                    for (int i = 0; i < _Welders.Count; i++) _Welders[i].Enabled = false;
                    for (int i = 0; i < _Grinders.Count; i++) _Grinders[i].Enabled = false;
                }
            }
            autoMode = false;
            output.Insert(0, "Job Complete!");
        }
        double GetPos(IMyShipMergeBlock _Merge) //get x, y, or z position, based on named merge blocks. Thank you, JoeTheDestroyer, for posting this in 2016.
        {
            if (_Merge.IsConnected)
            {
                //Find direction that block merges to
                Matrix mat;
                _Merge.Orientation.GetMatrix(out mat);
                Vector3I right1 = new Vector3I(mat.Right);

                //Check if there is a block in front of merge face
                IMySlimBlock _Sb = _Merge.CubeGrid.GetCubeBlock(_Merge.Position + right1);
                if (_Sb == null) output.Append("No Blocks in front of Merge Block\n");

                //Check if the other block is actually a merge block
                IMyShipMergeBlock _Mb = _Sb.FatBlock as IMyShipMergeBlock;
                if (_Mb == null) output.Append("Not A Merge Block\n");

                //Check that other block is correctly oriented
                if (_Mb != null)
                {
                    _Mb.Orientation.GetMatrix(out mat);
                    Vector3I right2 = new Vector3I(mat.Right);
                    int pos = Convert.ToInt32(_Mb.CustomName.Split(new char[] { 'X', 'Y', 'Z' }).Last()); // remove the letter
                    pos = pos - 1;
                    return pos;
                }
                else return -1;
            }
            else return -1;
        }

        public void MoveX() // step 1, release and move piston
        {
            _PistonX.Enabled = true;
            if (xDir == Dir.forward || xDir == Dir.backward)
            {
                _MoveConX.Connect();
                if (_MoveConX.Status == MyShipConnectorStatus.Connected)
                {
                    _ConnectorX.Disconnect();
                    _MergeX.Enabled = false;
                    if (xPosMerge < xTar)
                    {
                        _PistonX.Velocity = -1 * maxMovementSpeed;
                        xDir = Dir.movingForward;
                    }
                    else if (xPosMerge > xTar)
                    {
                        _PistonX.Velocity = maxMovementSpeed;
                        xDir = Dir.movingBackward;
                    }
                }
            }
            else if (xDir == Dir.movingForward || xDir == Dir.movingBackward) // step 2, merge and continue
            {
                _MergeX.Enabled = true;
                if (_MergeX.IsConnected)
                {
                    moveReady = true;
                    _ConnectorX.Connect();
                    _MoveConX.Disconnect();
                    if (xDir == Dir.movingForward)
                    {
                        xDir = Dir.forward;
                        if (autoMode)
                        {
                            _PistonX.Velocity = maxToolSpeed;
                        }
                        else
                        {
                            _PistonX.Velocity = maxMovementSpeed;
                        }
                    }
                    if (xDir == Dir.movingBackward)
                    {
                        xDir = Dir.backward;
                        if (autoMode)
                        {
                            _PistonX.Velocity = -1 * maxToolSpeed;
                        }
                        else
                        {
                            _PistonX.Velocity = -1 * maxMovementSpeed;
                        }
                    }
                }
            }
        }
        public void MoveY() // step 1, release and move piston
        {
            if (yDir == Dir.right || yDir == Dir.left)
            {
                _MoveConY.Connect();
                if (_MoveConY.Status == MyShipConnectorStatus.Connected)
                {
                    _ConnectorY.Disconnect();
                    _MergeY.Enabled = false;
                    if (yPos < yTar)
                    {
                        _PistonY.Velocity = -1 * maxMovementSpeed;
                        yDir = Dir.movingRight;
                    }
                    else if (yPos > yTar)
                    {
                        _PistonY.Velocity = maxMovementSpeed;
                        yDir = Dir.movingLeft;
                    }
                }
            }
            else if (yDir == Dir.movingRight || yDir == Dir.movingLeft) // step 2, merge and continue
            {
                _MergeY.Enabled = true;
                if (_MergeY.IsConnected)
                {
                    _ConnectorY.Connect();

                    _MoveConY.Disconnect();
                    if (yDir == Dir.movingRight)
                    {
                        yDir = Dir.right;
                        _PistonY.Velocity = maxMovementSpeed;
                    }
                    else if (yDir == Dir.movingLeft)
                    {
                        yDir = Dir.left;
                        _PistonY.Velocity = -1 * maxMovementSpeed;
                    }
                    if (autoMode && yPos == yTar) // reverse x before continuing
                    {
                        if (xDir == Dir.forward)
                        {
                            xDir = Dir.backward;
                            _PistonX.Enabled = true;
                            _Sensor.Enabled = true;
                            _PistonX.Velocity = -1 * maxToolSpeed;
                        }
                        else if (xDir == Dir.backward)
                        {
                            xDir = Dir.forward;
                            _PistonX.Enabled = true;
                            _Sensor.Enabled = true;
                            _PistonX.Velocity = maxToolSpeed;
                        }
                    }
                }
            }
        }
        public void MoveZ() // step 1, release and move piston
        {
            if (zDir == Dir.up || zDir == Dir.down)
            {
                _MoveConZ1.Connect();
                _MoveConZ2.Connect();
                if (_MoveConZ1.Status == MyShipConnectorStatus.Connected || _MoveConZ2.Status == MyShipConnectorStatus.Connected)
                {
                    _ConnectorZ1.Disconnect();
                    _ConnectorZ2.Disconnect();
                    _MergeZ.Enabled = false;
                    if (zPos < zTar)
                    {
                        _PistonZ.Velocity = -1 * maxMovementSpeed;
                        zDir = Dir.movingUp;
                    }
                    else if (zPos > zTar)
                    {
                        _PistonZ.Velocity = maxMovementSpeed;
                        zDir = Dir.movingdown;
                    }
                }
            }
            else if (zDir == Dir.movingUp || zDir == Dir.movingdown) // step 2, merge and continue
            {
                _MergeZ.Enabled = true;
                if (_MergeZ.IsConnected)
                {
                    _ConnectorZ1.Connect();
                    _ConnectorZ2.Connect();
                    _MoveConZ1.Disconnect();
                    _MoveConZ2.Disconnect();
                    if (zDir == Dir.movingUp)
                    {
                        zDir = Dir.up;
                        _PistonZ.Velocity = maxMovementSpeed;
                    }
                    else if (zDir == Dir.movingdown)
                    {
                        zDir = Dir.down;
                        _PistonZ.Velocity = -1 * maxMovementSpeed;
                    }
                    if (autoMode && zPos == zTar) //reverse x and y before continuing 
                    {
                        if (yDir == Dir.left)
                        {
                            yDir = Dir.right;
                        }
                        else if (yDir == Dir.right)
                        {
                            yDir = Dir.left;
                        }

                        if (xDir == Dir.forward)
                        {
                            xDir = Dir.backward;
                            _PistonX.Velocity = -1 * maxToolSpeed;
                            _PistonX.Enabled = true;
                            _Sensor.Enabled = true;
                        }
                        else if (xDir == Dir.backward)
                        {
                            xDir = Dir.forward;
                            _PistonX.Velocity = maxToolSpeed;
                            _PistonX.Enabled = true;
                            _Sensor.Enabled = true;
                        }
                    }
                }
            }
        }
        public void SensorTrigger(Boolean detected)
        { 
            if (detected)
            {
                _PistonX.Enabled = false;
                _PistonY.Enabled = false;
                _PistonZ.Enabled = false;
            }
            else if (!detected)
            {
                _PistonX.Enabled = true;
                _PistonY.Enabled = true;
                _PistonZ.Enabled = true;
            }
        }
        public void SensorSetup()
        {
            if (autoMode)
            {
                _Sensor.LeftExtend = 1.75f;
                _Sensor.RightExtend = 23.75f;
                _Sensor.BottomExtend = 6.0f;
                _Sensor.TopExtend = 1.0f;
                _Sensor.BackExtend = 2.65f;
                _Sensor.FrontExtend = 0.1f;
            }
            if (!autoMode)
            {
                _Sensor.LeftExtend = 3.5f;
                _Sensor.RightExtend = 26.0f;
                _Sensor.BottomExtend = 8.0f;
                _Sensor.TopExtend = 1.0f;
                _Sensor.BackExtend = 4.5f;
                _Sensor.FrontExtend = 2.25f;
            }
        }


        public void ParseCustomData()
        {
            if (!customData.TryParse(Me.CustomData, out result)) throw new Exception(result.ToString());

            maxX = customData.Get("3DPrinter", "maxX").ToInt32(maxX);
            int testMaxY = customData.Get("3DPrinter", "maxY").ToInt32(maxY);
            if (testMaxY % 2 == 0) maxY = testMaxY;
            else { output.Append("maxY must be a multiple of 2\n"); }
            maxZ = customData.Get("3DPrinter", "maxZ").ToInt32(maxZ);
            minX = customData.Get("3DPrinter", "minX").ToInt32(minX);
            int testMinY = customData.Get("3DPrinter", "minY").ToInt32(minY);
            if (testMinY % 2 == 0) minY = testMinY;
            else { output.Append("minY must be a multiple of 2\n"); }
            minZ = customData.Get("3DPrinter", "minZ").ToInt32(minZ);
            returnAfterDone = customData.Get("3DPrinter", "returnAfterDone").ToBoolean(returnAfterDone);
            maxMovementSpeed = customData.Get("3DPrinter", "maxMovementSpeed").ToSingle(maxMovementSpeed);
            grindingSpeed = customData.Get("3DPrinter", "grindingSpeed").ToSingle(grindingSpeed);
            weldingSpeed = customData.Get("3DPrinter", "weldingSpeed").ToSingle(weldingSpeed);
            toolLength = customData.Get("3DPrinter", "toolLength").ToInt32(toolLength);
            if (!Enum.TryParse<EMode>(customData.Get("3DPrinter", "mode").ToString("welding"), true, out mode))
            {
                output.Append("Mode must be 'grinding' or 'welding'\n");
            }
        }
        public void SaveToCustomData()
        {
            MyIniParseResult result;
            if (!customData.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            customData.Set("3DPrinter", "maxX", maxX);
            customData.SetComment("3DPrinter", "maxX", " Work area dimentions:");
            customData.Set("3DPrinter", "maxY", maxY);
            customData.Set("3DPrinter", "maxZ", maxZ);
            customData.Set("3DPrinter", "minX", minX);
            customData.Set("3DPrinter", "minY", minY);
            customData.Set("3DPrinter", "minZ", minZ);
            customData.Set("3DPrinter", "mode", mode.ToString());
            customData.SetComment("3DPrinter", "mode", " Mode: grinding or welding");
            customData.Set("3DPrinter", "toolLength", toolLength);
            customData.SetComment("3DPrinter", "toolLength", " Tool Length in number of blocks(default:10)");
            customData.Set("3DPrinter", "returnAfterDone", returnAfterDone);
            customData.SetComment("3DPrinter", "returnAfterDone", " Return to home position after completion?");
            customData.Set("3DPrinter", "maxMovementSpeed", maxMovementSpeed);
            customData.SetComment("3DPrinter", "maxMovementSpeed", " Fastest pistons can move (0.0-5.0)");
            customData.Set("3DPrinter", "grindingSpeed", grindingSpeed);
            customData.SetComment("3DPrinter", "grindingSpeed", " Max piston speed while grinding (default:0.5)");
            customData.Set("3DPrinter", "weldingSpeed", weldingSpeed);
            customData.SetComment("3DPrinter", "weldingSpeed", " Max piston speed while welding (default:0.2)");

            Me.CustomData = customData.ToString();
        }
        public void Save()
        {
            ini.Clear();
            ini.Set("3DPrinter", "xtar", xTar);
            ini.Set("3DPrinter", "ytar", yTar);
            ini.Set("3DPrinter", "ztar", zTar);
            ini.Set("3DPrinter", "xpos", xPos);
            ini.Set("3DPrinter", "ypos", yPos);
            ini.Set("3DPrinter", "zpos", zPos);
            ini.Set("3DPrinter", "xposmerge", xPosMerge);
            ini.Set("3DPrinter", "xdir", xDir.ToString());
            ini.Set("3DPrinter", "ydir", yDir.ToString());
            ini.Set("3DPrinter", "zdir", zDir.ToString());
            ini.Set("3DPrinter", "maxX", maxX);
            ini.Set("3DPrinter", "maxY", maxY);
            ini.Set("3DPrinter", "maxZ", maxZ);
            ini.Set("3DPrinter", "minX", minX);
            ini.Set("3DPrinter", "minY", minY);
            ini.Set("3DPrinter", "minZ", minZ);
            ini.Set("3DPrinter", "returnAfterDone", returnAfterDone);
            ini.Set("3DPrinter", "maxMovementSpeed", maxMovementSpeed);
            ini.Set("3DPrinter", "grindingSpeed", grindingSpeed);
            ini.Set("3DPrinter", "weldingSpeed", weldingSpeed);
            ini.Set("3DPrinter", "toolLength", toolLength);
            ini.Set("3DPrinter", "mode", mode.ToString());
            ini.Set("3DPrinter", "autoMode", autoMode);
            ini.Set("3DPrinter", "firstrun", firstRun);
            ini.Set("3DPrinter", "manualMove", manualMove);
            ini.Set("3DPrinter", "moveReady", moveReady);

            Storage = ini.ToString();
        }
    }
}
