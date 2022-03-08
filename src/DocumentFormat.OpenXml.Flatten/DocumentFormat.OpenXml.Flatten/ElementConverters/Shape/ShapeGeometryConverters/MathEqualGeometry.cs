﻿using System.Text;

using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Flatten.Contexts;

using dotnetCampus.OpenXmlUnitConverter;

using static DocumentFormat.OpenXml.Flatten.ElementConverters.ShapeGeometryConverters.ShapeGeometryFormulaHelper;

using ElementEmuSize = dotnetCampus.OpenXmlUnitConverter.EmuSize;

namespace DocumentFormat.OpenXml.Flatten.ElementConverters.ShapeGeometryConverters
{
    /// <summary>
    /// 等号
    /// </summary>
    public class MathEqualGeometry : ShapeGeometryBase
    {

        public override string? ToGeometryPathString(EmuSize emuSize, AdjustValueList? adjusts = null)
        {
            return null;
        }

        public override ShapePath[]? GetMultiShapePaths(EmuSize emuSize, AdjustValueList? adjusts = null)
        {
            var (h, w, l, r, t, b, hd2, hd4, hd5, hd6, hd8, ss, hc, vc, ls, ss2, ss4, ss6, ss8, wd2, wd4, wd5, wd6, wd8, wd10, cd2, cd4, cd6, cd8) = GetFormulaProperties(emuSize);

            //<avLst xmlns="http://schemas.openxmlformats.org/drawingml/2006/main">
            //  <gd name="adj1" fmla="val 23520" />
            //  <gd name="adj2" fmla="val 11760" />
            //</avLst>
            var customAdj1 = adjusts?.GetAdjustValue("adj1");
            var adj1 = customAdj1 ?? 23520d;
            var customAdj2 = adjusts?.GetAdjustValue("adj2");
            var adj2 = customAdj2 ?? 11760d;

            //当adj1为最低为0，导致一些值为0，参与公式乘除运算，导致路径有误
            adj1 = System.Math.Max(adj1, 1);
            adj2 = System.Math.Max(adj2, 1);


            //<gdLst xmlns="http://schemas.openxmlformats.org/drawingml/2006/main">
            //  <gd name="a1" fmla="pin 0 adj1 36745" />
            //  <gd name="2a1" fmla="*/ a1 2 1" />
            //  <gd name="mAdj2" fmla="+- 100000 0 2a1" />
            //  <gd name="a2" fmla="pin 0 adj2 mAdj2" />
            //  <gd name="dy1" fmla="*/ h a1 100000" />
            //  <gd name="dy2" fmla="*/ h a2 200000" />
            //  <gd name="dx1" fmla="*/ w 73490 200000" />
            //  <gd name="y2" fmla="+- vc 0 dy2" />
            //  <gd name="y3" fmla="+- vc dy2 0" />
            //  <gd name="y1" fmla="+- y2 0 dy1" />
            //  <gd name="y4" fmla="+- y3 dy1 0" />
            //  <gd name="x1" fmla="+- hc 0 dx1" />
            //  <gd name="x2" fmla="+- hc dx1 0" />
            //  <gd name="yC1" fmla="+/ y1 y2 2" />
            //  <gd name="yC2" fmla="+/ y3 y4 2" />
            //</gdLst>

            //<gd name="a1" fmla="pin 0 adj1 36745" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var a1 = Pin(0, adj1, 36745);
            //<gd name="2a1" fmla="*/ a1 2 1" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var _2a1 = a1 * 2 / 1;
            //<gd name="mAdj2" fmla="+- 100000 0 2a1" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var mAdj2 = 100000 + 0 - _2a1;
            //<gd name="a2" fmla="pin 0 adj2 mAdj2" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var a2 = Pin(0, adj2, mAdj2);
            //<gd name="dy1" fmla="*/ h a1 100000" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var dy1 = h * a1 / 100000;
            //<gd name="dy2" fmla="*/ h a2 200000" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var dy2 = h * a2 / 200000;
            //<gd name="dx1" fmla="*/ w 73490 200000" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var dx1 = w * 73490 / 200000;
            //<gd name="y2" fmla="+- vc 0 dy2" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var y2 = vc + 0 - dy2;
            //<gd name="y3" fmla="+- vc dy2 0" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var y3 = vc + dy2 - 0;
            //<gd name="y1" fmla="+- y2 0 dy1" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var y1 = y2 + 0 - dy1;
            //<gd name="y4" fmla="+- y3 dy1 0" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var y4 = y3 + dy1 - 0;
            //<gd name="x1" fmla="+- hc 0 dx1" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var x1 = hc + 0 - dx1;
            //<gd name="x2" fmla="+- hc dx1 0" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var x2 = hc + dx1 - 0;
            //<gd name="yC1" fmla="+/ y1 y2 2" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var yC1 = (y1 + y2) / 2;
            //<gd name="yC2" fmla="+/ y3 y4 2" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            var yC2 = (y3 + y4) / 2;

            //<pathLst xmlns="http://schemas.openxmlformats.org/drawingml/2006/main">
            //  <path>
            //    <moveTo>
            //      <pt x="x1" y="y1" />
            //    </moveTo>
            //    <lnTo>
            //      <pt x="x2" y="y1" />
            //    </lnTo>
            //    <lnTo>
            //      <pt x="x2" y="y2" />
            //    </lnTo>
            //    <lnTo>
            //      <pt x="x1" y="y2" />
            //    </lnTo>
            //    <close />
            //    <moveTo>
            //      <pt x="x1" y="y3" />
            //    </moveTo>
            //    <lnTo>
            //      <pt x="x2" y="y3" />
            //    </lnTo>
            //    <lnTo>
            //      <pt x="x2" y="y4" />
            //    </lnTo>
            //    <lnTo>
            //      <pt x="x1" y="y4" />
            //    </lnTo>
            //    <close />
            //  </path>
            //</pathLst>

            var shapePaths = new ShapePath[1];

            // <path >
            //<moveTo xmlns="http://schemas.openxmlformats.org/drawingml/2006/main">
            //  <pt x="x1" y="y1" />
            //</moveTo>
            var currentPoint = new EmuPoint(x1, y1);
            var stringPath = new StringBuilder();
            stringPath.Append($"M {EmuToPixelString(currentPoint.X)},{EmuToPixelString(currentPoint.Y)} ");
            //<lnTo xmlns="http://schemas.openxmlformats.org/drawingml/2006/main">
            //  <pt x="x2" y="y1" />
            //</lnTo>
            currentPoint = LineToToString(stringPath, x2, y1);
            //<lnTo xmlns="http://schemas.openxmlformats.org/drawingml/2006/main">
            //  <pt x="x2" y="y2" />
            //</lnTo>
            currentPoint = LineToToString(stringPath, x2, y2);
            //<lnTo xmlns="http://schemas.openxmlformats.org/drawingml/2006/main">
            //  <pt x="x1" y="y2" />
            //</lnTo>
            currentPoint = LineToToString(stringPath, x1, y2);
            //<close xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            stringPath.Append("z ");
            //<moveTo xmlns="http://schemas.openxmlformats.org/drawingml/2006/main">
            //  <pt x="x1" y="y3" />
            //</moveTo>
            currentPoint = new EmuPoint(x1, y3);
            stringPath.Append($"M {EmuToPixelString(currentPoint.X)},{EmuToPixelString(currentPoint.Y)} ");
            //<lnTo xmlns="http://schemas.openxmlformats.org/drawingml/2006/main">
            //  <pt x="x2" y="y3" />
            //</lnTo>
            currentPoint = LineToToString(stringPath, x2, y3);
            //<lnTo xmlns="http://schemas.openxmlformats.org/drawingml/2006/main">
            //  <pt x="x2" y="y4" />
            //</lnTo>
            currentPoint = LineToToString(stringPath, x2, y4);
            //<lnTo xmlns="http://schemas.openxmlformats.org/drawingml/2006/main">
            //  <pt x="x1" y="y4" />
            //</lnTo>
            currentPoint = LineToToString(stringPath, x1, y4);
            //<close xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            stringPath.Append("z ");
            shapePaths[0] = new ShapePath(stringPath.ToString());


            //<rect l="x1" t="y1" r="x2" b="y4" xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" />
            InitializeShapeTextRectangle(x1, y1, x2, y4);

            return shapePaths;
        }
    }


}

