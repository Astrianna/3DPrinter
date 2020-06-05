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

namespace IngameScript
{

    partial class Program : MyGridProgram
    {
        // Written (poorly) by Astrianna - 2020
        //
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
        MyIni CustomData = new MyIni();
        MyIniParseResult result;

        // declare variables before config
        double xTar, yTar, zTar, xPos, yPos, zPos, xPause, yPause, zPause, xPosMerge, getXPosMerge, getYPos, getZPos;
        int maxX, maxY, maxZ, minX, minY, minZ, toolLength;
        bool returnAfterDone, autoMode, firstRun, manualMove, moveReady;
        float maxMovementSpeed, maxToolSpeed, grindingSpeed, weldingSpeed;
        StringBuilder output = new StringBuilder("");
        enum Emode
        {
            grinding,
            welding    
        }
        enum Dir
        {
            forward,
            backward,
            left,
            right,
            up,
            down,
            movingforward,
            movingbackward,
            movingleft,
            movingright,
            movingup,
            movingdown
        }
        Emode mode;
        Dir xDir, yDir, zDir;

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
            if (!Enum.TryParse<Emode>(ini.Get("3DPrinter", "mode").ToString("forward"), out mode)) {; }
            autoMode = ini.Get("3DPrinter", "autoMode").ToBoolean(false);
            firstRun = ini.Get("3DPrinter", "firstrun").ToBoolean(true);
            manualMove = ini.Get("3DPrinter", "manualMove").ToBoolean(false);

            GetBlocks();
        }

        // block declarations
        IMySensorBlock Sensor;
        IMyShipConnector ConnectorX, ConnectorY, ConnectorZ1, ConnectorZ2, MoveConX, MoveConY, MoveConZ1, MoveConZ2;
        IMyPistonBase PistonX, PistonY, PistonZ;
        IMyShipMergeBlock MergeX, MergeY, MergeZ;
        List<IMyShipWelder> welders = new List<IMyShipWelder>();
        List<IMyShipGrinder> grinders = new List<IMyShipGrinder>();
        List<IMyTextPanel> lcds = new List<IMyTextPanel>();

        public void GetBlocks() // you didn't change any names, did you?
        {
            Sensor = GridTerminalSystem.GetBlockWithName("Tool Sensor") as IMySensorBlock;
            ConnectorX = GridTerminalSystem.GetBlockWithName("X Connector") as IMyShipConnector;
            ConnectorY = GridTerminalSystem.GetBlockWithName("Y Connector") as IMyShipConnector;
            ConnectorZ1 = GridTerminalSystem.GetBlockWithName("Z Connector 1") as IMyShipConnector;
            ConnectorZ2 = GridTerminalSystem.GetBlockWithName("Z Connector 2") as IMyShipConnector;
            MoveConX = GridTerminalSystem.GetBlockWithName("X Move") as IMyShipConnector;
            MoveConY = GridTerminalSystem.GetBlockWithName("Y Move") as IMyShipConnector;
            MoveConZ1 = GridTerminalSystem.GetBlockWithName("Z Move 1") as IMyShipConnector;
            MoveConZ2 = GridTerminalSystem.GetBlockWithName("Z Move 2") as IMyShipConnector;
            PistonX = GridTerminalSystem.GetBlockWithName("X Piston") as IMyPistonBase;
            PistonY = GridTerminalSystem.GetBlockWithName("Y Piston") as IMyPistonBase;
            PistonZ = GridTerminalSystem.GetBlockWithName("Z Piston") as IMyPistonBase;
            MergeX = GridTerminalSystem.GetBlockWithName("X Merge") as IMyShipMergeBlock;
            MergeY = GridTerminalSystem.GetBlockWithName("Y Merge") as IMyShipMergeBlock;
            MergeZ = GridTerminalSystem.GetBlockWithName("Z Merge") as IMyShipMergeBlock;
            GridTerminalSystem.GetBlockGroupWithName("3D-Printer").GetBlocksOfType(welders);
            GridTerminalSystem.GetBlockGroupWithName("3D-Printer").GetBlocksOfType(grinders);
            GridTerminalSystem.GetBlockGroupWithName("3D-Printer").GetBlocksOfType(lcds);
            if (Sensor == null) output.Append("Tool Sensor not found!\n");
            if (ConnectorX == null) output.Append("X Connector not found!\n");
            if (ConnectorY == null) output.Append("Y Connector not found!\n");
            if (ConnectorZ1 == null) output.Append("Z Connector 1 not found!\n");
            if (ConnectorZ2 == null) output.Append("Z Connector 2 not found!\n");
            if (MoveConX == null) output.Append("X Move not found!\n");
            if (MoveConY == null) output.Append("Y Move not found!\n");
            if (MoveConZ1 == null) output.Append("Z Move 1 not found!\n");
            if (MoveConZ2 == null) output.Append("Z Move 2 not found!\n");
            if (PistonX == null) output.Append("X Piston not found!\n");
            if (PistonY == null) output.Append("Y Piston not found!\n");
            if (PistonZ == null) output.Append("Z Piston not found!\n");
            if (MergeX == null) output.Append("X Merge not found!\n");
            if (MergeY == null) output.Append("Y Merge not found!\n");
            if (MergeZ == null) output.Append("Z Merge not found!\n");
            if (welders.Count == 0 && grinders.Count == 0) output.Append("No Welders or Grinders found in Group '3D-Printer'\n");
            if (lcds.Count == 0) output.Append("No LCD Panels found in Group '3D-Printer'\n");
            if (welders.Count != 0 && grinders.Count == 0) mode = Emode.welding;
            if (welders.Count == 0 && grinders.Count != 0) mode = Emode.grinding;
        }

        public void Main(string argument)
        {
            if (firstRun)
            {
                firstRun = false;
                Sensor.Enabled = false;
                PistonX.Enabled = true;
                PistonY.Enabled = true;
                PistonZ.Enabled = true;
                manualMove = true;
                SaveToCustomData();
            }

            output.Clear();

            if (argument.Equals("start", StringComparison.OrdinalIgnoreCase))
            {
                if (autoMode) Sensor.Enabled = true;
                PistonX.Enabled = true;
                PistonY.Enabled = true;
                PistonZ.Enabled = true;
                if (mode == Emode.welding)
                {
                    for (int i = 0; i < welders.Count; i++) welders[i].Enabled = true;
                }
                else if (mode == Emode.grinding)
                {
                    for (int i = 0; i < grinders.Count; i++) grinders[i].Enabled = true;
                }

                if (xPause != xPos || yPause != yPos || zPause != zPos)
                {
                    output.Append("Position changed while paused\nStarting forward\n");
                    PistonX.Velocity = maxToolSpeed;
                }
            }

            if (argument.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                Sensor.Enabled = false;
                PistonX.Enabled = false;
                PistonY.Enabled = false;
                PistonZ.Enabled = false;
                xPause = xPos;
                yPause = yPos;
                zPause = zPos;
                for (int i = 0; i < welders.Count; i++) welders[i].Enabled = false;
                for (int i = 0; i < grinders.Count; i++) grinders[i].Enabled = false;
            }

            if (argument.Equals("mode", StringComparison.OrdinalIgnoreCase))
            {

                if (mode == Emode.grinding)
                {
                    mode = Emode.welding;
                    zDir = Dir.up;
                }

                else if (mode == Emode.welding)
                {
                    mode = Emode.grinding;
                    zDir = Dir.down;
                }
                SaveToCustomData();
            }

            if (argument.Equals("return", StringComparison.OrdinalIgnoreCase))
            {
                returnAfterDone = !returnAfterDone;
                SaveToCustomData();
            }

            if (argument.Equals("home", StringComparison.OrdinalIgnoreCase))
            {
                if (mode == Emode.grinding) zTar = maxZ;
                if (mode == Emode.welding) zTar = minZ;
                yTar = minY;
                xTar = minX;
                autoMode = false;
                manualMove = true;
                for (int i = 0; i < welders.Count; i++) welders[i].Enabled = false;
                for (int i = 0; i < grinders.Count; i++) grinders[i].Enabled = false;

            }

            if (argument.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                autoMode = !autoMode;
                if (autoMode)
                {
                    PistonX.Velocity = maxToolSpeed;
                    if (mode == Emode.welding)
                    {
                        for (int i = 0; i < welders.Count; i++) welders[i].Enabled = true;
                    }
                    else if (mode == Emode.grinding)
                    {
                        for (int i = 0; i < grinders.Count; i++) grinders[i].Enabled = true;
                    }
                }
                else
                {
                    for (int i = 0; i < welders.Count; i++) welders[i].Enabled = false;
                    for (int i = 0; i < grinders.Count; i++) grinders[i].Enabled = false;
                }

                manualMove = false;
             
            }

            if (argument.StartsWith("goto", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < welders.Count; i++) welders[i].Enabled = false;
                for (int i = 0; i < grinders.Count; i++) grinders[i].Enabled = false;

                string[] xyz = argument.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (xyz.Length == 4)
                {
                    int x, y, z;
                    if (int.TryParse(xyz[1], out x) && int.TryParse(xyz[2], out y) && int.TryParse(xyz[3], out z))
                    {
                        if (y % 2 == 0 && x >= minX && x <= maxY && y >= minY && y <= maxY && z >= minZ && z <= maxZ)
                        {
                            autoMode = false;
                            xTar = x;
                            yTar = y;
                            zTar = z;
                            manualMove = true;
                        }
                    }
                }
            }
            if (argument.Equals("refresh", StringComparison.OrdinalIgnoreCase))
            {
                GetBlocks();
                SaveToCustomData();
            }
            if (!CustomData.TryParse(Me.CustomData, out result)) throw new Exception(result.ToString());

            maxX = CustomData.Get("3DPrinter", "maxX").ToInt32(maxX);
            int testmaxY = CustomData.Get("3DPrinter", "maxY").ToInt32(maxY);
            if (testmaxY % 2 == 0) maxY = testmaxY;
            else
            {
                output.Append("maxY must be a multiple of 2\n");
            }

            maxZ = CustomData.Get("3DPrinter", "maxZ").ToInt32(maxZ);
            minX = CustomData.Get("3DPrinter", "minX").ToInt32(minX);
            int testminY = CustomData.Get("3DPrinter", "minY").ToInt32(minY);
            if (testminY % 2 == 0) minY = testminY;
            else
            {
                output.Append("minY must be a multiple of 2\n");
            }
            minZ = CustomData.Get("3DPrinter", "minZ").ToInt32(minZ);
            returnAfterDone = CustomData.Get("3DPrinter", "returnAfterDone").ToBoolean(returnAfterDone);
            maxMovementSpeed = CustomData.Get("3DPrinter", "maxMovementSpeed").ToSingle(maxMovementSpeed);
            grindingSpeed = CustomData.Get("3DPrinter", "grindingSpeed").ToSingle(grindingSpeed);
            weldingSpeed = CustomData.Get("3DPrinter", "weldingSpeed").ToSingle(weldingSpeed);
            toolLength = CustomData.Get("3DPrinter", "toolLength").ToInt32(toolLength);
            if (!Enum.TryParse<Emode>(CustomData.Get("3DPrinter", "mode").ToString("welding"), out mode)) 
            {
                output.Append("Mode must be 'grinding' or 'welding'\n");
            }

            if (Me.CustomData == "") SaveToCustomData();
            string ERR_TXT = "";

            if (mode == Emode.grinding) maxToolSpeed = grindingSpeed;

            if (mode == Emode.welding) maxToolSpeed = weldingSpeed;

            if (manualMove)
            {
                if (xPos == xTar && yPos == yTar && zPos == zTar)
                {
                    xDir = Dir.forward;
                    yDir = Dir.right;
                    if (mode == Emode.grinding) zDir = Dir.down;
                    if (mode == Emode.welding) zDir = Dir.up;
                    manualMove = false;
                }
            }

            // raw pos based on merge block names
            getXPosMerge = getPos(MergeX);
            getYPos = getPos(MergeY);
            getZPos = getPos(MergeZ);
            if (getXPosMerge != -1)
            {
                xPosMerge = getXPosMerge;
                xPos = Math.Round(xPosMerge + (PistonX.CurrentPosition - PistonX.MinLimit) / 2.5, 1); // X Merge + X Piston
            }
            if (getYPos != -1) yPos = getYPos;
            if (getZPos != -1) zPos = getZPos;

            if ((int)xDir >= 6 || (int)yDir >=6 || (int)zDir >= 6) // something is currently moving, check to see if its ready to stop 
            {
                if (zDir == Dir.movingdown && PistonZ.CurrentPosition == PistonZ.MaxLimit || zDir == Dir.movingup && PistonZ.CurrentPosition == PistonZ.MinLimit) MoveZ();
                else if (yDir == Dir.movingright && PistonY.CurrentPosition == PistonY.MinLimit || yDir == Dir.movingleft && PistonY.CurrentPosition == PistonY.MaxLimit) MoveY();
                else if (xDir == Dir.movingforward && PistonX.CurrentPosition == PistonX.MinLimit || xDir == Dir.movingbackward && PistonX.CurrentPosition == PistonX.MaxLimit) MoveX();
            }
            else if ((int)xDir <= 5 && (int)yDir <= 5 && (int)zDir <= 5) // nothing is currently moving
            {
                if (autoMode && yPos == yTar && zPos == zTar && ((xDir == Dir.forward && xPos >= maxX) || (xDir == Dir.backward && xPos <= minX))) // automode only
                {
                    PistonX.Enabled = false; // disable X Piston and Sensor while anything else is moving
                    Sensor.Enabled = false;
                    if ((yPos == minY && yDir == Dir.left) || (yPos + toolLength == maxY && yDir == Dir.right)) // move z up/down
                    {
                        if ((zPos == maxZ && mode == Emode.welding) || (zPos == minZ) && mode == Emode.grinding) Done(); // done! returning to starting position
                        else if (mode == Emode.welding) zTar = zTar + 1;
                        else if (mode == Emode.grinding) zTar = zTar - 1;
                    }
                    else if ((yPos > minY) || (yPos + toolLength < maxY)) // move y left/right
                    {
                        if (yDir == Dir.right) yTar = yTar + toolLength;
                        else if (yDir == Dir.left) yTar = yTar - toolLength;
                    }
                }
                else if (autoMode && xDir == Dir.forward && PistonX.CurrentPosition == PistonX.MaxLimit && xPos < maxX) // automode only
                {
                    if (moveReady == true)
                    {
                        xTar = xTar + 3;
                        moveReady = false;
                    }
                    MoveX();
                }
                else if (autoMode && xDir == Dir.backward && PistonX.CurrentPosition == PistonX.MinLimit && xPosMerge > minX) //automode only
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
                        PistonX.Velocity = -1 * maxMovementSpeed;
                    }
                    else if (xPos < xTar && xPos < maxX)
                    {
                        PistonX.Velocity = maxMovementSpeed;
                    }
                    else if ((xPos > xTar && xPosMerge != minX) || (xPos < xTar && xPos < maxX))
                    {
                        MoveX();
                    }
                }
                // display errors
                if (ERR_TXT != "")
                {
                    output.Insert(0,"Script Errors:\n" + ERR_TXT + "(make sure block ownership is set correctly)\n");
                }
                // build standard output for LCDs/Terminal
                output.Append("Mode: " + mode + "\n");
                output.Append("Auto Mode: " + autoMode.ToString() + "\n");
                output.Append("X: " + xPos + "\n");
                output.Append("Y: " + yPos + "\n");
                output.Append("Z: " + zPos + "\n");
                output.Append("X Target: " + xTar + "\n");
                output.Append("Y Target: " + yTar + "\n");
                output.Append("Z Target: " + zTar + "\n");
                output.Append("X Direction: " + xDir + "\n");
                output.Append("Y Direction: " + yDir + "\n");
                output.Append("Z Direction: " + zDir + "\n");
                
                Echo(output.ToString());
                if (lcds.Count != 0)
                {
                    for (int i = 0; i < lcds.Count; i++)
                    {
                        lcds[i].ContentType = ContentType.TEXT_AND_IMAGE;
                        lcds[i].WriteText(output.ToString(),false);
                    }
                }

            }
        }
        void Done()
        {

            if (zPos == zTar && yPos == yTar && xPosMerge == zTar)
            { 
                if (returnAfterDone)
                {
                    zTar = maxZ;
                    yTar = minY;
                    xTar = minX;
                }

                PistonX.Enabled = false;
                PistonY.Enabled = false;
                PistonZ.Enabled = false;
                Sensor.Enabled = false;
                for (int i = 0; i < welders.Count; i++) welders[i].Enabled = false;
                for (int i = 0; i < grinders.Count; i++) grinders[i].Enabled = false;
            }
            autoMode = false;
            output.Insert(0,"Job Complete!");
        }
        double getPos(IMyShipMergeBlock Merge) //get x, y, or z position, based on named merge blocks. Thank you, JoeTheDestroyer, for posting this in 2016.
        {
            if (Merge.IsConnected)
            {
                //Find direction that block merges to
                Matrix mat;
                Merge.Orientation.GetMatrix(out mat);
                Vector3I right1 = new Vector3I(mat.Right);

                //Check if there is a block in front of merge face
                IMySlimBlock sb = Merge.CubeGrid.GetCubeBlock(Merge.Position + right1);
                if (sb == null) output.Append("No Blocks in front of Merge Block\n");

                //Check if the other block is actually a merge block
                IMyShipMergeBlock mb = sb.FatBlock as IMyShipMergeBlock;
                if (mb == null) output.Append("Not A Merge Block\n");

                //Check that other block is correctly oriented
                if (mb != null)
                {
                    mb.Orientation.GetMatrix(out mat);
                    Vector3I right2 = new Vector3I(mat.Right);
                    int pos = Convert.ToInt32(mb.CustomName.Split(new char[] { 'X', 'Y', 'Z' }).Last()); // remove the letter
                    pos = pos - 1;
                    return pos;
                }
                else return -1;
            }
            else return -1;
        }

        public void MoveX() // step 1, release and move piston
        {
            PistonX.Enabled = true;
            if (xDir == Dir.forward || xDir == Dir.backward)
            {
                MoveConX.Enabled = true;
                MoveConX.Connect();
                if (MoveConX.Status == MyShipConnectorStatus.Connected)
                {
                    ConnectorX.Disconnect();
                    MergeX.Enabled = false;
                    if (xPosMerge < xTar)
                    {
                        PistonX.Velocity = -1 * maxMovementSpeed;
                        xDir = Dir.movingforward;
                    }
                    else if (xPosMerge > xTar)
                    {
                        PistonX.Velocity = maxMovementSpeed;
                        xDir = Dir.movingbackward;
                    }
                }
            }
            else if (xDir == Dir.movingforward || xDir == Dir.movingbackward) // step 2, merge and continue
            {
                MergeX.Enabled = true;
                if (MergeX.IsConnected)
                {
                    moveReady = true;
                    ConnectorX.Connect();
                    MoveConX.Disconnect();
                    MoveConX.Enabled = false;
                    if (xDir == Dir.movingforward)
                    {
                        xDir = Dir.forward;
                        if (autoMode)
                        {
                            PistonX.Velocity = maxToolSpeed;
                        }
                        else
                        {
                            PistonX.Velocity = maxMovementSpeed;
                        }
                    }
                    if (xDir == Dir.movingbackward)
                    {
                        xDir = Dir.backward;
                        if (autoMode)
                        {
                            PistonX.Velocity = -1 * maxToolSpeed;
                        }
                        else
                        {
                            PistonX.Velocity = -1 * maxMovementSpeed;
                        }
                    }
                }
            }
        }
        public void MoveY() // step 1, release and move piston
        {
            if (yDir == Dir.right || yDir == Dir.left)
            {
                MoveConY.Connect();
                if (MoveConY.Status == MyShipConnectorStatus.Connected)
                {
                    ConnectorY.Disconnect();
                    MergeY.Enabled = false;
                    if (yPos < yTar)
                    {
                        PistonY.Velocity = -1 * maxMovementSpeed;
                        yDir = Dir.movingright;
                    }
                    else if (yPos > yTar)
                    {
                        PistonY.Velocity = maxMovementSpeed;
                        yDir = Dir.movingleft;
                    }
                }
            }
            else if (yDir == Dir.movingright || yDir == Dir.movingleft) // step 2, merge and continue
            {
                MergeY.Enabled = true;
                if (MergeY.IsConnected)
                {
                    ConnectorY.Connect();

                    MoveConY.Disconnect();
                    if (yDir == Dir.movingright)
                    {
                        yDir = Dir.right;
                        PistonY.Velocity = maxMovementSpeed;
                    }
                    else if (yDir == Dir.movingleft)
                    {
                        yDir = Dir.left;
                        PistonY.Velocity = -1 * maxMovementSpeed;
                    }
                    if (autoMode && yPos == yTar) // reverse x before continuing
                    {
                        if (xDir == Dir.forward)
                        {
                            xDir = Dir.backward;
                            PistonX.Enabled = true;
                            Sensor.Enabled = true;
                            PistonX.Velocity = -1 * maxToolSpeed;
                        }
                        else if (xDir == Dir.backward)
                        {
                            xDir = Dir.forward;
                            PistonX.Enabled = true;
                            Sensor.Enabled = true;
                            PistonX.Velocity = maxToolSpeed;
                        }
                    }
                }
            }
        }
        public void MoveZ() // step 1, release and move piston
        {
            if (zDir == Dir.up || zDir == Dir.down)
            {
                MoveConZ1.Connect();
                MoveConZ2.Connect();
                if (MoveConZ1.Status == MyShipConnectorStatus.Connected || MoveConZ2.Status == MyShipConnectorStatus.Connected)
                {
                    ConnectorZ1.Disconnect();
                    ConnectorZ2.Disconnect();
                    MergeZ.Enabled = false;
                    if (zPos < zTar)
                    {
                        PistonZ.Velocity = -1 * maxMovementSpeed;
                        zDir = Dir.movingup;
                    }
                    else if (zPos > zTar)
                    {
                        PistonZ.Velocity = maxMovementSpeed;
                        zDir = Dir.movingdown;
                    }
                }
            }
            else if (zDir == Dir.movingup || zDir == Dir.movingdown) // step 2, merge and continue
            {
                MergeZ.Enabled = true;
                if (MergeZ.IsConnected)
                {
                    ConnectorZ1.Connect();
                    ConnectorZ2.Connect();
                    MoveConZ1.Disconnect();
                    MoveConZ2.Disconnect();
                    if (zDir == Dir.movingup)
                    {
                        zDir = Dir.up;
                        PistonZ.Velocity = maxMovementSpeed;
                    }
                    else if (zDir == Dir.movingdown)
                    {
                        zDir = Dir.down;
                        PistonZ.Velocity = -1 * maxMovementSpeed;
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
                            PistonX.Velocity = -1 * maxToolSpeed;
                            PistonX.Enabled = true;
                            Sensor.Enabled = true;
                        }
                        else if (xDir == Dir.backward)
                        {
                            xDir = Dir.forward;
                            PistonX.Velocity = maxToolSpeed;
                            PistonX.Enabled = true;
                            Sensor.Enabled = true;
                        }
                    }
                }
            }
        }
        public void SaveToCustomData()
        {
            MyIniParseResult result;
            if (!CustomData.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            CustomData.Set("3DPrinter", "maxX", maxX);
            CustomData.SetComment("3DPrinter", "maxX", " Work area dimentions:");
            CustomData.Set("3DPrinter", "maxY", maxY);
            CustomData.Set("3DPrinter", "maxZ", maxZ);
            CustomData.Set("3DPrinter", "minX", minX);
            CustomData.Set("3DPrinter", "minY", minY);
            CustomData.Set("3DPrinter", "minZ", minZ);
            CustomData.Set("3DPrinter", "mode", mode.ToString());
            CustomData.SetComment("3DPrinter", "mode", " Mode: grinding or welding");
            CustomData.Set("3DPrinter", "toolLength", toolLength);
            CustomData.SetComment("3DPrinter", "toolLength", " Tool Length in number of blocks(default:10)");
            CustomData.Set("3DPrinter", "returnAfterDone", returnAfterDone);
            CustomData.SetComment("3DPrinter", "returnAfterDone", " Return to home position after completion?");
            CustomData.Set("3DPrinter", "maxMovementSpeed", maxMovementSpeed);
            CustomData.SetComment("3DPrinter", "maxMovementSpeed", " Fastest pistons can move (0.0-5.0)");
            CustomData.Set("3DPrinter", "grindingSpeed", grindingSpeed);
            CustomData.SetComment("3DPrinter", "grindingSpeed", " Max piston speed while grinding (default:0.5)");
            CustomData.Set("3DPrinter", "weldingSpeed", weldingSpeed);
            CustomData.SetComment("3DPrinter", "weldingSpeed", " Max piston speed while welding (default:0.2)");

            Me.CustomData = CustomData.ToString();
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

            Storage = ini.ToString();
        }
    }
}
