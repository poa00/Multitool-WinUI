﻿using System.Xml;

namespace Multitool.DAL.Settings.Converters
{
    public interface ISettingConverter
    {
        XmlNode Convert(object toConvert);
        object Restore(XmlNode toRestore);
        object Restore(object defaultValue);
    }
}