﻿/*
 * ArcsToLines.cs - part of CNC Controls library for Grbl
 *
 * v0.15 / 2020-04-20 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020, Io Engineering (Terje Io)
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

· Redistributions of source code must retain the above copyright notice, this
list of conditions and the following disclaimer.

· Redistributions in binary form must reproduce the above copyright notice, this
list of conditions and the following disclaimer in the documentation and/or
other materials provided with the distribution.

· Neither the name of the copyright holder nor the names of its contributors may
be used to endorse or promote products derived from this software without
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

*/

using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls
{
    public class ArcsToLines : IGCodeTransformer
    {
        public void Apply()
        {
            double arcTolerance = GrblSettings.GetDouble(GrblSetting.ArcTolerance); 
            GCodeEmulator emu = new GCodeEmulator();
            List<GCodeToken> toolPath = new List<GCodeToken>();

            using (new UIUtils.WaitCursor())
            {
                toolPath.Add(new GCComment(Commands.Comment, 0, "Arcs to lines transform applied"));

                foreach (var cmd in emu.Execute(GCode.File.Tokens))
                {
                    switch (cmd.Token.Command)
                    {
                        case Commands.G2:
                        case Commands.G3:
                            {
                                var arc = cmd.Token as GCArc;
                                var lnr = arc.LineNumber;
                                toolPath.Add(new GCComment(Commands.Comment, lnr++, "Arc to lines start: " + arc.ToString()));

                                List<Point3D> points = arc.GeneratePoints(emu.Plane, ToPos(cmd.Start, emu.IsImperial), arcTolerance, emu.DistanceMode == DistanceMode.Incremental); // Dynamic resolution
                                foreach (Point3D point in points)
                                    toolPath.Add(new GCLinearMotion(Commands.G1, lnr++, ToPos(point, emu.IsImperial), AxisFlags.XYZ));

                                toolPath.Add(new GCComment(Commands.Comment, lnr, "Arc to lines end"));
                            }
                            break;

                        case Commands.G5:
                            {
                                var spline = cmd.Token as GCSpline;
                                var lnr = spline.LineNumber;
                                toolPath.Add(new GCComment(Commands.Comment, lnr++, "Spline to lines start: " + spline.ToString()));

                                List<Point3D> points = spline.GeneratePoints(ToPos(cmd.Start, emu.IsImperial), arcTolerance, emu.DistanceMode == DistanceMode.Incremental); // Dynamic resolution
                                foreach (Point3D point in points)
                                    toolPath.Add(new GCLinearMotion(Commands.G1, lnr++, ToPos(point, emu.IsImperial), AxisFlags.XYZ));

                                toolPath.Add(new GCComment(Commands.Comment, lnr, "Spline to lines end"));
                            }
                            break;

                        default:
                            toolPath.Add(cmd.Token);
                            break;
                    }
                }

                List<string> gc = GCodeParser.TokensToGCode(toolPath);

                GCode.File.AddBlock(string.Format("Arcs to lines transform applied: {0}", GCode.File.Model.FileName), CNC.Core.Action.New);

                foreach (string block in gc)
                    GCode.File.AddBlock(block, CNC.Core.Action.Add);

                GCode.File.AddBlock("", CNC.Core.Action.End);
            }
        }

        double[] ToPos(Point3D pos, bool imperial)
        {
            int res = imperial ? 4 : 3;
            return new double[] { Math.Round(pos.X, res), Math.Round(pos.Y, res), Math.Round(pos.Z, res) };
        }
    }
}

