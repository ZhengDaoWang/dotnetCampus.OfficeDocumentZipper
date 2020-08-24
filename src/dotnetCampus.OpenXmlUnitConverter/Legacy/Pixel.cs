﻿using System;
using System.ComponentModel;

namespace dotnetCampus.OpenXMLUnitConverter
{
    [EditorBrowsable(EditorBrowsableState.Never), Obsolete("请使用 dotnetCampus.OpenXmlUnitConverter 命名空间下的同名类型。")]
    public class Pixel : LegacyUnit<dotnetCampus.OpenXmlUnitConverter.Pixel, Pixel>
    {
        public Pixel(double value)
        {
            Value = value;
        }

        public double Value { get; }

        public static readonly Pixel ZeroPixel = new Pixel(0);

        public static implicit operator Pixel(dotnetCampus.OpenXmlUnitConverter.Pixel newUnit)
        {
            return new Pixel(newUnit.Value);
        }
    }
}
