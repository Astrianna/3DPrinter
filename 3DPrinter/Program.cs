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

namespace IngameScript
{

    partial class Program : MyGridProgram
    {
        // Written (poorly) by Astrianna - 2020

        // TODO
        // add save/load system for when server restarts
        // finish arguments to change positions, modes, start, stop, etc
        // add mode for welding, grinding is the current default and only mode
        
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        // work area
        int maxX = 20;
        int maxY = 20;
        int maxZ = 20;
        int minX = 0;
        int minY = 0;
        int minZ = 0;

        // starting coordinates
        double xstart = 0;// default 0
        double ystart = 0;// default 0, even numbers only
        double zstart = 20;// default 20
        bool returnAfterDone = true; // returns to maxZ, minY, minX when job is completed
        
        // piston and tool settings
        float maxMovementSpeed = 5; // max speed while changing positions
        float maxToolSpeed = .5f; // max speed of tool while in auto mode
        int toolLength = 10; // length of tool in blocks
//        string mode = "grind"; // "grind" or "weld"
        bool autoMode = false;
        bool firstrun = true;
        
        // other variables
        string xdir;
        string ydir;
        string zdir;
        double xtar;
        double ytar;
        double ztar;
        double xpos;
        double ypos;
        double zpos;
        double xposmerge;
        double getxposmerge;
        double getypos;
        double getzpos;

        // block declarations
        IMySensorBlock Sensor;
        IMyShipConnector ConnectorX;
        IMyShipConnector ConnectorY;
        IMyShipConnector ConnectorZ1;
        IMyShipConnector ConnectorZ2;
        IMyShipConnector MoveConX;
        IMyShipConnector MoveConY;
        IMyShipConnector MoveConZ1;
        IMyShipConnector MoveConZ2;
        IMyPistonBase PistonX;
        IMyPistonBase PistonY;
        IMyPistonBase PistonZ;
        IMyShipMergeBlock MergeX;
        IMyShipMergeBlock MergeY;
        IMyShipMergeBlock MergeZ;

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
        }

        public void Main(string argument)
        {
            if (firstrun) 
            {
                firstrun = false;
                GetBlocks();
                PistonX.ApplyAction("OnOff_On");
                PistonY.ApplyAction("OnOff_On");
                PistonZ.ApplyAction("OnOff_On");
                if (autoMode) Sensor.ApplyAction("OnOff_On");
                if (autoMode) PistonX.Velocity = maxToolSpeed;
                xdir = "forward";
                ydir = "right";
                zdir = "down";
                xtar = xstart;
                ytar = ystart;
                ztar = zstart;
            }

            if (argument.Equals("start", StringComparison.OrdinalIgnoreCase))
            {

            }
            if (argument.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {

            }
            if (argument.Equals("mode", StringComparison.OrdinalIgnoreCase))
            {

            }
            if (argument.Equals("home", StringComparison.OrdinalIgnoreCase))
            {

            }
            if (argument.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {

            }
            if (argument.StartsWith("goto", StringComparison.OrdinalIgnoreCase))
            {

            }

            string ERR_TXT = "";

            // raw pos based on merge block names
            getxposmerge = getPos(MergeX);
            getypos = getPos(MergeY);
            getzpos = getPos(MergeZ);
            if (getxposmerge != -1) xposmerge = getxposmerge;
            if (getypos != -1) ypos = getypos;
            if (getzpos != -1) zpos = getzpos;
            xpos = Math.Round(xposmerge + (PistonX.CurrentPosition - PistonX.MinLimit) / 2.5, 1); // X Merge + X Piston

            // debug, change to LCD later
            Echo("X: " + xpos);
            Echo("Y: " + ypos);
            Echo("Z: " + zpos);
            Echo("XMerge:" + xposmerge);
            Echo("X Tar:" + xtar);
            Echo("Y Tar:" + ytar);
            Echo("Z Tar:" + ztar);
            Echo("Auto Mode: " + autoMode.ToString());
            Echo("X Direction: " + xdir);
            Echo("Y Direction: " + ydir);
            Echo("Z Direction: " + zdir);

            if (xdir.IndexOf("moving") != -1 || ydir.IndexOf("moving") != -1 || zdir.IndexOf("moving") != -1) // something is currently moving, check to see if its ready to stop 
            {
                if (zdir == "moving-down" && PistonZ.CurrentPosition == PistonZ.MaxLimit || zdir == "moving-up" && PistonZ.CurrentPosition == PistonZ.MinLimit) MoveZ();
                else if (ydir == "moving-right" && PistonY.CurrentPosition == PistonY.MinLimit || ydir == "moving-left" && PistonY.CurrentPosition == PistonY.MaxLimit) MoveY();
                else if (xdir == "moving-forward" && PistonX.CurrentPosition == PistonX.MinLimit || xdir == "moving-backward" && PistonX.CurrentPosition == PistonX.MaxLimit) MoveX();
            }
            else if (xdir.IndexOf("moving") == -1 && ydir.IndexOf("moving") == -1 && zdir.IndexOf("moving") == -1) // nothing is currently moving
            {
                if (autoMode && ypos == ytar && zpos == ztar &&((xdir == "forward" && xpos >= maxX) || (xdir == "backward" && xpos <= minX))) // automode only
                {
                    PistonX.Enabled = false; // disable X Piston while anything else is moving
                    if ((ypos == minY && ydir == "left") || (ypos + toolLength == maxY && ydir == "right")) // move z up/down
                    {
                        if ((zpos == maxZ && zdir == "up") || (zpos == minZ) && zdir == "down") Done(); // done! returning to starting position
                        else if (zdir == "up") ztar = ztar + 1;
                        else if (zdir == "down") ztar = ztar - 1;
                    }
                    else if ((ypos > minY) || (ypos + toolLength < maxY)) // move y left/right
                    {
                        if (ydir == "right") ytar = ytar + toolLength;
                        else if (ydir == "left") ytar = ytar - toolLength;
                    }
                }
                if (autoMode && xdir == "forward" && PistonX.CurrentPosition == PistonX.MaxLimit && xpos < maxX) // automode only
                {
                    xtar = xtar + 3;
                    MoveX();
                }
                else if (autoMode && xdir == "backward" && PistonX.CurrentPosition == PistonX.MinLimit && xposmerge > minX) //automode only
                {
                    xtar = xtar - 3;
                    MoveX();
                }
                // does something need to move?
                if (zpos != ztar) MoveZ(); 
                else if (ypos != ytar) MoveY();
                else if (! autoMode) // move X while automode is off
                {
                    if (xpos > xtar && xposmerge == minX) PistonX.Velocity = -1 * maxMovementSpeed;
                    else if (xpos < xtar && xpos < maxX && xposmerge == minX) PistonX.Velocity = maxMovementSpeed;
                    else if ((xpos > xtar && xposmerge != minX) || (xpos < xtar && xpos < maxX)) MoveX();
                }
                // display errors
                if (ERR_TXT != "")
                {
                    Echo("Script Errors:\n" + ERR_TXT + "(make sure block ownership is set correctly)");
                    return;
                }
                else { Echo(""); }
            }
        }
        void Done()
        {
            
            if (returnAfterDone)
            {
                ztar = maxZ;
                ytar = minY;
                xtar = minX;
            }
            autoMode = false;
            Echo("Job Complete");

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
                if (sb == null) Echo("No Blocks in front of Merge Block");

                //Check if the other block is actually a merge block
                IMyShipMergeBlock mb = sb.FatBlock as IMyShipMergeBlock;
                if (mb == null) Echo("Not A Merge Block");

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
            Echo("Moving X " + xdir);
            if (xdir == "forward" || xdir == "backward")
            {
                MoveConX.Connect();
                if (MoveConX.Status == MyShipConnectorStatus.Connected)
                {
                    ConnectorX.Disconnect();
                    MergeX.ApplyAction("OnOff_Off");
                    if (xpos < xtar)
                    {
                        PistonX.Velocity = -1 * maxMovementSpeed;
                        xdir = "moving-forward";
                        Echo("X Target: " + xtar);
                    }
                    if (xpos > xtar)
                    {
                        PistonX.Velocity = maxMovementSpeed;
                        xdir = "moving-backward";
                        Echo("X Target: " + xtar);
                    }
                }
            }
            else if (xdir == "moving-forward" || xdir == "moving-backward") // step 2, merge and continue
            {
                MergeX.ApplyAction("OnOff_On");
                if (MergeX.IsConnected)
                {
                    ConnectorX.Connect();
                    MoveConX.Disconnect();
                    if (xdir == "moving-forward")
                    {
                        if (autoMode)
                        {
                            PistonX.Velocity = maxToolSpeed;
                            xdir = "forward";
                        }
                        else { PistonX.Velocity = maxMovementSpeed; }
                        xdir = "forward";
                        Echo("X Reached: " + xpos);
                    }
                    if (xdir == "moving-backward")
                    {
                        if (autoMode)
                        {
                            PistonX.Velocity = -1 * maxToolSpeed;
                            xdir = "backward";
                        }
                        else { PistonX.Velocity = -1 * maxMovementSpeed; }

                        xdir = "backward";
                        Echo("X Reached: " + xpos);
                    }
                }
            }
        }
        public void MoveY() // step 1, release and move piston
        {
            Echo("MoveY()" + ydir);
            if (ydir == "right" || ydir == "left")
            {
                MoveConY.Connect();
                if (MoveConY.Status == MyShipConnectorStatus.Connected)
                {
                    ConnectorY.Disconnect();
                    MergeY.ApplyAction("OnOff_Off");
                    if (ypos < ytar)
                    {
                        PistonY.Velocity = -1 * maxMovementSpeed;
                        ydir = "moving-right";
                        Echo("Y Target: " + ytar);
                    }
                    if (ypos > ytar)
                    {
                        PistonY.Velocity = maxMovementSpeed;
                        ydir = "moving-left";
                        Echo("Y Target: " + ytar);
                    }
                }
            }
            else if (ydir == "moving-right" || ydir == "moving-left") // step 2, merge and continue
            {
                MergeY.ApplyAction("OnOff_On");
                if (MergeY.IsConnected)
                {
                    ConnectorY.Connect();

                    MoveConY.Disconnect();
                    if (ydir == "moving-right")
                    {
                        ydir = "right";
                        PistonY.Velocity = maxMovementSpeed;
                        Echo("Y Reached: " + ypos);
                    }
                    if (ydir == "moving-left")
                    {
                        ydir = "left";
                        PistonY.Velocity = -1 * maxMovementSpeed;
                        Echo("Y Reached: " + ypos);
                    }
                    if (autoMode && ypos == ytar) // reverse x before continuing
                    {
                        if (xdir == "forward")
                        {
                            xdir = "backward";
                            PistonX.Enabled = true;
                            PistonX.Velocity = -1 * maxToolSpeed;
                        }
                        else if (xdir == "backward")
                        {
                            xdir = "forward";
                            PistonX.Enabled = true;
                            PistonX.Velocity = maxToolSpeed;
                        }
                    }
                }
            }
        }
        public void MoveZ() // step 1, release and move piston
        {
            Echo("MoveZ()" + zdir);
            if (zdir == "up" || zdir == "down")
            {
                MoveConZ1.Connect();
                MoveConZ2.Connect();
                Echo(MoveConZ1.Status.ToString() + MoveConZ2.Status.ToString());
                if (MoveConZ1.Status == MyShipConnectorStatus.Connected || MoveConZ2.Status == MyShipConnectorStatus.Connected)
                {
                    ConnectorZ1.Disconnect();
                    ConnectorZ2.Disconnect();
                    MergeZ.ApplyAction("OnOff_Off");
                    if (zpos < ztar)
                    {
                        PistonZ.Velocity = -1 * maxMovementSpeed;
                        zdir = "moving-up";
                        Echo("Z Target: " + ztar);
                    }
                    if (zpos > ztar)
                    {
                        PistonZ.Velocity = maxMovementSpeed;
                        zdir = "moving-down";
                        Echo("Z Target: " + ztar);
                    }
                }
            }
            else if (zdir == "moving-up" || zdir == "moving-down") // step 2, merge and continue
            {
                MergeZ.ApplyAction("OnOff_On");

                if (MergeZ.IsConnected)
                {
                    ConnectorZ1.Connect();
                    ConnectorZ2.Connect();
                    MoveConZ1.Disconnect();
                    MoveConZ2.Disconnect();
                    if (zdir == "moving-up")
                    {
                        zdir = "up";
                        PistonZ.Velocity = maxMovementSpeed;
                        Echo("Z Reached: " + zpos);
                    }
                    if (zdir == "moving-down")
                    {
                        zdir = "down";
                        PistonZ.Velocity = -1 * maxMovementSpeed;
                        Echo("Z Reached: " + zpos);
                    }
                    if (autoMode && zpos == ztar) //reverse x and y before continuing 
                    {
                        if (ydir == "left")
                        {
                            ydir = "right";
                        }
                        else if (ydir == "right")
                        {
                            ydir = "left";
                        }
                        
                        if (xdir == "forward")
                        {
                            xdir = "backward";
                            PistonX.Enabled = true;
                            PistonX.Velocity = -1 * maxToolSpeed;
                        }
                        else if (xdir == "backward")
                        {
                            xdir = "forward";
                            PistonX.Enabled = true;
                            PistonX.Velocity = maxToolSpeed;
                        }
                    }
                }
            }
        }
    }
}

