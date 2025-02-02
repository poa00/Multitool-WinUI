﻿using Multitool.Data.Settings.Converters;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;

using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace Multitool.Data.Settings
{
    public class XmlSettingManager : IUserSettingsManager
    {
        private readonly string filePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "settings.xml");
        private readonly XmlDocument document;
        private readonly XmlNode settingsRootNode;

        public XmlSettingManager()
        {
            if (File.Exists(filePath))
            {
                document = new();
                try
                {
                    document.Load(filePath);
                }
                catch (XmlException ex)
                {
                    Trace.TraceError("Failed to load XML setting file (settings.xml)\n" + ex.ToString());
                }

                settingsRootNode = document.SelectSingleNode(".//Settings");
                if (settingsRootNode == null)
                {
                    XmlNode node = document.CreateElement("Settings");
                    document.AppendChild(node);
                    settingsRootNode = node;
                }
            }
            else
            {
                throw new FileNotFoundException($"Setting file was not found in application local folder ('{filePath}')");
            }
        }

        public event TypedEventHandler<IUserSettingsManager, string> SettingsChanged;

        #region properties
        public bool AutoCommit { get; set; } = true;
        public ApplicationDataContainer DataContainer { get; init; }
        public string SettingFilePath => filePath;
        #endregion

        #region public methods

        #region ISettingsManager
        public void Load<T>(T toLoad, bool useSettingAttribute = true)
        {
            if (!useSettingAttribute)
            {
                throw new NotSupportedException("Function is not implemented to save all object properties.");
            }
            XmlNode values = settingsRootNode.SelectSingleNode($".//{typeof(T).FullName}");

            Trace.TraceInformation($"Loading settings for {toLoad.GetType().Name}");
            if (values == null)
            {
                Trace.TraceWarning($"No values associated with {toLoad.GetType().Name} in the setting file."); 
            }
            
            PropertyInfo[] props = typeof(T).GetProperties();
            for (int i = 0; i < props.Length; i++)
            {
                object[] attributes = props[i].GetCustomAttributes(false);
                foreach (Attribute attribute in attributes)
                {
                    if (attribute is SettingAttribute settingAttribute)
                    {
                        try
                        {
                            if (settingAttribute.SettingKey != null)
                            {
                                // delegate setting load
                                XmlNode node = settingsRootNode.SelectSingleNode($".//{settingAttribute.SettingKey}");
                                Trace.TraceInformation($"Loading values from {settingAttribute.SettingKey} for {typeof(T).Name}.{props[i].Name}");
                                if (node != null)
                                {
                                    LoadFromXml(props[i], settingAttribute, toLoad, node);
                                }
                            }
                            else if (values != null)
                            {
                                LoadFromXml(props[i], settingAttribute, toLoad, values);
                            }
                            else
                            {
                                Trace.TraceInformation($"Loading default value for {typeof(T).Name}.{props[i].Name}");
                                SetPropertyValue(props[i], toLoad, settingAttribute);
                            }
                        }
                        catch (TargetException ex)
                        {
                            Trace.TraceError($"Failed to load {typeof(T).Name}.{props[i].Name} :\n{ex}");
                        }
                        catch (TargetInvocationException ex)
                        {
                            Trace.TraceError($"Failed to load {typeof(T).Name}.{props[i].Name} :\n{ex}");
                        }
                        break;
                    }
                }
            }
        }

        public void Save<T>(T toSave, bool useSettingAttribute = true)
        {
            if (toSave is null)
            {
                throw new ArgumentNullException(nameof(toSave));
            }
            if (!useSettingAttribute)
            {
                throw new NotSupportedException("Function is not implemented to save all object properties. The property tree may be too big.");
            }

            XmlNode rootNode = settingsRootNode.SelectSingleNode(".//" + typeof(T).FullName);
            if (rootNode == null)
            {
                Trace.TraceInformation($"Node not existing for {typeof(T).Name}, creating one.");

                rootNode = document.CreateElement(typeof(T).FullName);
                XmlAttribute attribute = document.CreateAttribute("timestamp");
                attribute.Value = DateTime.Now.ToUniversalTime().ToLongTimeString();
                rootNode.Attributes.Append(attribute);
                settingsRootNode.AppendChild(rootNode);
            }
            else
            {
                XmlAttribute attribute = rootNode.Attributes["timestamp"];
                if (attribute != null)
                {
                    attribute.Value = DateTime.Now.ToUniversalTime().ToLongTimeString();
                }
                else
                {
                    attribute = document.CreateAttribute("timestamp");
                    attribute.Value = DateTime.Now.ToUniversalTime().ToLongTimeString();
                    rootNode.Attributes.Append(attribute);
                }
            }

            PropertyInfo[] props = typeof(T).GetProperties();
            for (int i = 0; i < props.Length; i++)
            {
                IEnumerable<Attribute> attributes = props[i].GetCustomAttributes();

                foreach (Attribute attribute in attributes)
                {
                    if (attribute is SettingAttribute settingAttribute)
                    {
                        try
                        {
                            object propValue = props[i].GetValue(toSave);
                            if (propValue != null)
                            {
                                XmlNode settingNode;
                                string settingName = settingAttribute.SettingName ?? props[i].Name;
                                XmlNode previousNode = rootNode.SelectSingleNode($".//{settingName}");
                                if (previousNode != null)
                                {
                                    rootNode.RemoveChild(previousNode);
                                }
                                settingNode = document.CreateElement(settingAttribute.SettingName ?? props[i].Name);

                                if (settingAttribute.Converter != null)
                                {
                                    if (IsList(props[i].PropertyType))
                                    {
                                        var list = (IList)propValue;
                                        foreach (var item in list)
                                        {
                                            XmlNode convertedNode = settingAttribute.Converter.Convert(item);
                                            if (convertedNode != null)
                                            {
                                                settingNode.AppendChild(document.ImportNode(convertedNode, true));
                                            }
                                        }
                                    }
                                    else
                                    {
                                        XmlNode convertedNode = settingAttribute.Converter.Convert(propValue);
                                        if (convertedNode != null)
                                        {
                                            settingNode.AppendChild(document.ImportNode(convertedNode, true));
                                        }
                                    }
                                }
                                else // auto convert
                                {
                                    if (props[i].PropertyType.IsPrimitive || props[i].PropertyType == typeof(string))
                                    {
                                        XmlAttribute valueAttribute = document.CreateAttribute("value");
                                        valueAttribute.Value = propValue.ToString();
                                        settingNode.Attributes.Append(valueAttribute);
                                    }
                                    else if (props[i].PropertyType.IsEnum)
                                    {
                                        XmlAttribute valueAttribute = document.CreateAttribute("value");
                                        valueAttribute.Value = ((int)propValue).ToString();
                                        settingNode.Attributes.Append(valueAttribute);
                                    }
                                    else if (IsList(props[i].PropertyType))
                                    {
                                        // can crash with casting exception
                                        FlattenList(settingNode, (IList)propValue, props[i].PropertyType);
                                    }
                                    else
                                    {
                                        throw new ArgumentException($"Cannot save {props[i].DeclaringType}.{props[i].Name}. It is neither a primitive type (string included) or a list and it does not have a custom converter.");
                                    }
                                }

                                rootNode.AppendChild(settingNode);
                            }
                            else
                            {
                                Trace.TraceWarning($"Not saving {typeof(T).Name}.{props[i].Name}, property value is null.");
                            }
                        }
                        catch (TargetException ex)
                        {
                            Trace.TraceError($"Failed to save {typeof(T).Name}.{props[i].Name} :\n{ex}");
                        }
                        catch (TargetInvocationException ex)
                        {
                            Trace.TraceError($"Failed to save {typeof(T).Name}.{props[i].Name} :\n{ex}");
                        }
                        break;
                    }
                }
            }

            settingsRootNode.AppendChild(rootNode);
            if (AutoCommit)
            {
                Commit();
            }
        }

        #region additional methods
        public void Save(string callerName, string name, object value)
        {
            if (value == null)
            {
                Trace.TraceWarning($"Not saving {callerName}/{name}, value is null");
            }

            XmlNode node = settingsRootNode.SelectSingleNode($".//{callerName}");
            if (node != null)
            {
                XmlNode settingNode = node.SelectSingleNode($".//{name}");

                if (settingNode == null)
                {
                    settingNode = document.CreateElement(name);
                }

                XmlAttribute valueAttribute = document.CreateAttribute("value");
                valueAttribute.Value = value.ToString();
                settingNode.Attributes.Append(valueAttribute);

                node.AppendChild(settingNode);
            }
            else
            {
                node = document.CreateElement(callerName);
                XmlNode toSave = document.CreateElement(name);

                XmlAttribute valueAttribute = document.CreateAttribute("value");
                valueAttribute.Value = value.ToString();
                toSave.Attributes.Append(valueAttribute);

                node.AppendChild(toSave);
                settingsRootNode.AppendChild(node);
            }

            if (AutoCommit)
            {
                Commit();
            }
        }

        public T Get<T>(string globalKey, string settingKey)
        {
            XmlNode globalNode = settingsRootNode.SelectSingleNode(".//" + globalKey);
            if (globalNode != null)
            {
                XmlNode settingNode = globalNode.SelectSingleNode(".//" + settingKey);

                if (settingNode != null)
                {
                    object value = GetValueFromLeaf(settingNode);
                    if (value != null)
                    {
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                    else
                    {
                        return default;
                    }
                }
                else
                {
                    throw new SettingNotFoundException(settingKey);
                }
            }
            else
            {
                throw new SettingNotFoundException(globalKey, "Root node was not found");
            }
        }

        public object TryGet(string globalKey, string settingKey)
        {
            XmlNode globalNode = settingsRootNode.SelectSingleNode(".//" + globalKey);
            if (globalNode != null)
            {
                XmlNode settingNode = globalNode.SelectSingleNode(".//" + settingKey);
                if (settingNode != null)
                {
                    return GetValueFromLeaf(settingNode);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public bool TryGet<T>(string callerName, string name, [MaybeNullWhen(false)] out T value)
        {
            XmlNode settingNode = settingsRootNode.SelectSingleNode($".//{callerName}/{name}");
            if (settingNode != null)
            {
                try
                {
                    value = (T)Convert.ChangeType(GetValueFromLeaf(settingNode), typeof(T));
                    return true;
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                    value = default;
                    return false;
                }
            }
            else
            {
                value = default;
                return false;
            }
        }

        public void Remove(string globalKey, string settingKey)
        {
            throw new NotImplementedException();
        } 

        public List<string> ListKeys()
        {
            List<string> keys = new();
            var nodes = settingsRootNode.ChildNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                keys.Add(nodes[i].Name);
            }
            return keys;
        }

        public List<string> ListKeys(string globalKey)
        {
            List<string> keys = new();
            var node = settingsRootNode.SelectSingleNode($".//{globalKey}");
            if (node != null)
            {
                var nodes = node.ChildNodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    keys.Add(nodes[i].Name);
                }
            }
            return keys;
        }

        public void Edit(string globalKey, string settingKey, object value)
        {
            XmlNode node = settingsRootNode.SelectSingleNode($".//{globalKey}/{settingKey}");
            if (node != null)
            {
                if (node.Attributes != null && node.Attributes.Count > 0)
                {
                    for (int i = 0; i < node.Attributes.Count; i++)
                    {
                        if (node.Attributes[i].Name == "value")
                        {
                            node.Attributes[i].Value = value.ToString();
                        }
                    }
                }
                else
                {
                    node.InnerText = value.ToString();
                }
            }

            /*if (AutoCommit)
            {
                Commit();
            }*/
        }

        public void EditSetting(string xpath, ISettingConverter converter, object value)
        {
            XmlNode node = settingsRootNode.SelectSingleNode(xpath);
            if (node != null)
            {
                node.AppendChild(converter.Convert(value));
            }
        }

        public void Reset()
        {
            document.RemoveAll();
            document.AppendChild(document.CreateElement("Settings"));
            Commit();
            Trace.TraceInformation("Cleared setting file.");
        }
        #endregion
        #endregion

        public void Commit()
        {
            // TODO
            Trace.TraceInformation("Commiting setting changes.");
            document.Save(filePath);
        }

        public static async Task<XmlSettingManager> Get()
        {
            await ApplicationData.Current.LocalFolder.CreateFileAsync("settings.xml", CreationCollisionOption.OpenIfExists);
            return new();
        }
        #endregion

        #region private methods
        private void FlattenList(XmlNode parentNode, IList list, Type propType)
        {
            Type genericType;
            Type[] generics = propType.GetGenericArguments();
            if (generics.Length > 0)
            {
                genericType = generics[0];
            }
            else
            {
                genericType = typeof(object);
            }

            XmlAttribute genericAttribute = document.CreateAttribute("type");
            genericAttribute.Value = genericType.FullName;
            parentNode.Attributes.Append(genericAttribute);

            foreach (var element in list)
            {
                XmlNode elementNode = document.CreateElement(genericType.Name);
                elementNode.InnerText = element.ToString();
                parentNode.AppendChild(elementNode);
            }
        }

        private static object GetTypeDefaultValue(Type type)
        {
            ConstructorInfo ctorInfo = type.GetConstructor(Array.Empty<Type>());
            if (ctorInfo == null)
            {
                return null;
            }
            else
            {
                object value = null;
                try
                {
                    value = Convert.ChangeType(ctorInfo.Invoke(Array.Empty<object>()), type);
                }
                catch (InvalidCastException ex)
                {
                    Trace.TraceError(ex.ToString());
                }

                return value;
            }
        }

        private static object GetValueFromLeaf(XmlNode settingNode)
        {
            if (settingNode.Attributes != null)
            {
                XmlAttributeCollection attributes = settingNode.Attributes;

                for (int i = 0; i < attributes.Count; i++)
                {
                    if (attributes[i].Name == "value")
                    {
                        return attributes[i].Value;
                    }
                }
            }

            if (settingNode.FirstChild != null)
            {
                return settingNode.FirstChild.InnerText;
            }
            else
            {
                return null;
            }
        }
        
        private static bool IsList(Type t)
        {
            Type[] interfaces = t.FindInterfaces((Type m, object filter) => m == typeof(IList), null);
            return interfaces.Length > 0;
        }
        
        private static void LoadFromXml<T>(PropertyInfo propertyInfo, SettingAttribute settingAttribute, T toLoad, XmlNode values)
        {
            // get prop name with SettingName property
            string propName = settingAttribute.SettingName ?? propertyInfo.Name;
            XmlNode node = values.SelectSingleNode($".//{propName}");
            if (node == null)
            {
                SetPropertyValue(propertyInfo, toLoad, settingAttribute);
            }
            else
            {
                object value = null;
                if (IsList(propertyInfo.PropertyType))
                {
                    var xmlGenericType = node.Attributes["type"];
                    Type[] generics = propertyInfo.PropertyType.GetGenericArguments();
                    Type genericType = generics.Length > 0 ? generics[0] : typeof(object);

                    if (xmlGenericType != null && xmlGenericType.Value != genericType.FullName)
                    {
                        throw new SettingTypeMismatch(genericType.Name, xmlGenericType.Value, "Saved IList generic parameter does not match property IList generic parameter.");
                    }
                    
                    var childNodes = node.ChildNodes;
                    IList list = (IList)GetTypeDefaultValue(propertyInfo.PropertyType);
                    if (settingAttribute.Converter == null)
                    {
                        foreach (XmlNode childNode in childNodes)
                        {
                            list.Add(Convert.ChangeType(childNode.InnerText, genericType));
                        }
                    }
                    else
                    {
                        foreach (XmlNode childNode in childNodes)
                        {
                            object restored = settingAttribute.Converter.Restore(childNode);
                            if (restored != null)
                            {
                                list.Add(restored);
                            }
                            else
                            {
                                Trace.TraceWarning($"{settingAttribute.Converter.GetType().Name} returned null after restoring a value from a list for {typeof(T).Name}.{propertyInfo.Name}. Ignoring this value.");
                            }
                        }
                    }

                    value = list;
                }
                else if (propertyInfo.PropertyType.IsEnum)
                {
                    var attr = node.Attributes["value"];
                    if (attr != null)
                    {
                        if (!Enum.TryParse(typeof(T), attr.Value, out value))
                        {
                            Trace.TraceWarning($"Cannot convert \"{attr.Value}\" to {typeof(T).Name}.");
                        }
                    }
                }
                else
                {
                    if (settingAttribute.Converter != null)
                    {
                        value = settingAttribute.Converter.Restore(node.FirstChild);
                        // if we cannot restore then we ask for the converter to restore the default value
                        if (value == null && settingAttribute.HasDefaultValue)
                        {
                            value = settingAttribute.Converter.Restore(settingAttribute.DefaultValue);
                        }
                    }
                    else
                    {
                        XmlAttribute xmlAttribute = node.Attributes["value"];
                        value = xmlAttribute != null ? xmlAttribute.Value : node.InnerText;
                    }
                }

                if (value != null)
                {
                    SetPropertyValue(propertyInfo, toLoad, settingAttribute, value);
                }
                else
                {
                    Trace.TraceWarning($"{typeof(T).Name}.{propertyInfo.Name} not assigned, value from XML file is null");
                }
            }
        }

        private static void SetPropertyValue<T>(PropertyInfo prop, T toLoad, SettingAttribute settingAttribute, object value = null)
        {
            if (prop.CanWrite)
            {
                try
                {
                    if (value == null)
                    {
                        if (settingAttribute.HasDefaultValue)
                        {
                            if (settingAttribute.Converter != null)
                            {
                                prop.SetValue(toLoad, settingAttribute.Converter.Restore(settingAttribute.DefaultValue));
                            }
                            else
                            {
                                prop.SetValue(toLoad, settingAttribute.DefaultValue);
                            }
                        }
                        else if (settingAttribute.DefaultInstanciate)
                        {
                            prop.SetValue(toLoad, GetTypeDefaultValue(prop.PropertyType));
                        }
                    }
                    else if (typeof(T) == typeof(Type))
                    {
                        prop.SetValue(toLoad, value);
                    }
                    else
                    {
                        prop.SetValue(toLoad, Convert.ChangeType(value, prop.PropertyType));
                    }
                }
                catch (InvalidCastException ex)
                {
                    Trace.TraceWarning($"Failed to convert value for {typeof(T).Name}.{prop.Name}, trying to set the value with no conversion. {ex.Message}");
                    prop.SetValue(toLoad, value);
                    Trace.TraceInformation($"Set {typeof(T).Name}.{prop.Name} value with no conversion.");
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                    throw;
                }
            }
            else
            {
                throw new TargetException($"Cannot set {typeof(T).Name}.{prop.Name}, property is readonly.");
            }
        }
        #endregion
    }
}
